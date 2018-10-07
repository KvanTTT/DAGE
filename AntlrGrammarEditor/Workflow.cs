﻿using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class Workflow
    {
        public const string HelperDirectoryName = "DageHelperDirectory";
        private const string PythonHelperFileName = "AntlrPythonCompileTest.py";
        private const string JavaScriptHelperFileName = "AntlrJavaScriptTest.js";
        private const string TextFileName = "Text";
        private const string RuntimesDirName = "AntlrRuntimes";

        private const string TemplateGrammarName = "__TemplateGrammarName__";
        private const string TemplateGrammarRoot = "__TemplateGrammarRoot__";

        private const int GenerateParserProcessTimeout = 200;
        private const int CompileParserProcessTimeout = 200;
        private const int ParseTextTimeout = 200;

        private Grammar _grammar = new Grammar();
        private string _text = "";
        private WorkflowState _currentState;

        private List<ParsingError> _grammarCheckErrors = new List<ParsingError>();
        private List<ParsingError> _parserGenerationErrors = new List<ParsingError>();
        private List<ParsingError> _parserCompilationErrors = new List<ParsingError>();
        private List<ParsingError> _textErrors = new List<ParsingError>();
        private string _outputTree;
        private string _outputTokens;
        private TimeSpan _outputLexerTime;
        private TimeSpan _outputParserTime;
        private AntlrErrorListener _antlrErrorListener;
        private event EventHandler<ParsingError> _newErrorEvent;
        private List<string> _buffer = new List<string>();
        private bool _indentedTree;

        private CancellationTokenSource _cancellationTokenSource;
        private InputState _inputState = new InputState();
        private object _lockObj = new object();
        private Dictionary<string, CodeSource> _grammarFilesData = new Dictionary<string, CodeSource>();
        private Dictionary<string, List<CodeInsertion>> _grammarActionsTextSpan = new Dictionary<string, List<CodeInsertion>>();
        private Dictionary<string, List<TextSpanMapping>> _grammarCodeMapping = new Dictionary<string, List<TextSpanMapping>>();
        private CodeSource _currentGrammarSource;

        public GrammarCheckedState GrammarCheckedState { get; private set; }

        public WorkflowState CurrentState
        {
            get => _currentState;
            private set
            {
                _currentState = value;
                StateChanged?.Invoke(this, _currentState);
            }
        }

        public Grammar Grammar
        {
            get => _grammar;
            set
            {
                StopIfRequired();
                _grammar = value;
                RollbackToStage(WorkflowStage.Input);
            }
        }

        public Runtime Runtime
        {
            get => Grammar.Runtimes.First();
            set
            {
                if (Grammar.Runtimes.First() != value)
                {
                    StopIfRequired();
                    Grammar.Runtimes.Clear();
                    Grammar.Runtimes.Add(value);
                    RollbackToStage(WorkflowStage.GrammarChecked);
                }
            }
        }

        public string Root
        {
            get => _grammar.Root;
            set
            {
                if (_grammar.Root != value)
                {
                    StopIfRequired();
                    _grammar.Root = value;
                    RollbackToStage(WorkflowStage.ParserCompilied);
                }
            }
        }

        public string PreprocessorRoot
        {
            get => _grammar.PreprocessorRoot;
            set
            {
                if (_grammar.PreprocessorRoot != value)
                {
                    StopIfRequired();
                    _grammar.PreprocessorRoot = value;
                    RollbackToStage(WorkflowStage.ParserGenerated);
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    StopIfRequired();
                    _text = value;
                    RollbackToStage(WorkflowStage.ParserCompilied);
                }
            }
        }

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public bool IndentedTree
        {
            get => _indentedTree;
            set
            {
                if (_indentedTree != value)
                {
                    StopIfRequired();
                    _indentedTree = value;
                    RollbackToStage(WorkflowStage.ParserCompilied);
                }
            }
        }

        public event EventHandler<WorkflowState> StateChanged;

        public event EventHandler<ParsingError> NewErrorEvent
        {
            add
            {
                _antlrErrorListener.NewErrorEvent += value;
                _newErrorEvent += value;
            }
            remove
            {
                _antlrErrorListener.NewErrorEvent -= value;
                _newErrorEvent -= value;
            }
        }

        public event EventHandler<WorkflowStage> ClearErrorsEvent;

        public event EventHandler<Tuple<TextParsedOutput, object>> TextParsedOutputEvent;

        public TimeSpan OutputLexerTime
        {
            get => _outputLexerTime;
            set
            {
                _outputLexerTime = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.LexerTime, _outputLexerTime));
            }
        }

        public TimeSpan OutputParserTime
        {
            get => _outputParserTime;
            set
            {
                _outputParserTime = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.ParserTime, _outputParserTime));
            }
        }

        public string OutputTokens
        {
            get => _outputTokens;
            private set
            {
                _outputTokens = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.Tokens, _outputTokens));
            }
        }

        public string OutputTree
        {
            get => _outputTree;
            private set
            {
                _outputTree = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.Tree, _outputTree));
            }
        }

        static Workflow()
        {
        }

        public Workflow() 
        {
            CurrentState = _inputState;
            _antlrErrorListener = new AntlrErrorListener(_grammarCheckErrors);
        }

        public Task<WorkflowState> ProcessAsync()
        {
            StopIfRequired();

            Func<WorkflowState> func = Process;
            return Task.Run(func);
        }

        public WorkflowState Process()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            while (!CurrentState.HasErrors && CurrentState.Stage < WorkflowStage.TextParsed && CurrentState.Stage < EndStage)
            {
                ProcessOneStep();
            }

            _cancellationTokenSource = null;
            return CurrentState;
        }

        public void StopIfRequired()
        {
            if (_cancellationTokenSource != null)
            {
                lock (_lockObj)
                {
                    if (_cancellationTokenSource != null)
                    {
                        _cancellationTokenSource.Cancel();
                        while (_cancellationTokenSource != null)
                        {
                            Thread.Sleep(250);
                        }
                    }
                }
            }
        }

        public void RollbackToPreviousStageIfErrors()
        {
            if (CurrentState.HasErrors)
            {
                ClearErrors(CurrentState.Stage);
                CurrentState = CurrentState.PreviousState;
            }
        }

        public void RollbackToStage(WorkflowStage stage)
        {
            while (CurrentState.Stage > stage && CurrentState.PreviousState != null)
            {
                if (CurrentState.Stage <= WorkflowStage.TextParsed)
                {
                    OutputTokens = "";
                    OutputTree = "";
                    ClearErrors(WorkflowStage.TextTokenized);
                    ClearErrors(WorkflowStage.TextParsed);
                }
                if (CurrentState.Stage <= WorkflowStage.ParserCompilied)
                {
                    ClearErrors(WorkflowStage.ParserCompilied);
                }
                if (CurrentState.Stage <= WorkflowStage.ParserGenerated)
                {
                    ClearErrors(WorkflowStage.ParserGenerated);
                }
                if (CurrentState.Stage <= WorkflowStage.GrammarChecked)
                {
                    ClearErrors(WorkflowStage.GrammarChecked);
                }
                CurrentState = CurrentState.PreviousState;
            }
        }

        private void ProcessOneStep()
        {
            switch (CurrentState.Stage)
            {
                case WorkflowStage.Input:
                    CurrentState = CheckGrammar();
                    break;
                case WorkflowStage.GrammarChecked:
                    CurrentState = GenerateParser((GrammarCheckedState)CurrentState);
                    break;
                case WorkflowStage.ParserGenerated:
                    CurrentState = CompileParser((ParserGeneratedState)CurrentState);
                    break;
                case WorkflowStage.ParserCompilied:
                    CurrentState = ParseText((ParserCompiliedState)CurrentState);
                    break;
            }
        }

        private GrammarCheckedState CheckGrammar()
        {
            var result = new GrammarCheckedState
            {
                Grammar = _grammar,
                InputState = _inputState,
                Rules = new List<string>()
            };
            try
            {
                _grammarActionsTextSpan.Clear();
                foreach (var grammarFileName in _grammar.Files)
                {
                    string code = File.ReadAllText(Path.Combine(_grammar.GrammarPath, grammarFileName));
                    var inputStream = new AntlrInputStream(code);
                    _antlrErrorListener.CodeSource = new CodeSource(grammarFileName, inputStream.ToString());
                    _grammarFilesData[grammarFileName] = _antlrErrorListener.CodeSource;
                    var antlr4Lexer = new ANTLRv4Lexer(inputStream);
                    antlr4Lexer.RemoveErrorListeners();
                    antlr4Lexer.AddErrorListener(_antlrErrorListener);
                    var codeTokenSource = new ListTokenSource(antlr4Lexer.GetAllTokens());

                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var codeTokenStream = new CommonTokenStream(codeTokenSource);
                    var antlr4Parser = new ANTLRv4Parser(codeTokenStream);

                    antlr4Parser.RemoveErrorListeners();
                    antlr4Parser.AddErrorListener(_antlrErrorListener);

                    var tree = antlr4Parser.grammarSpec();

                    var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
                    grammarInfoCollectorListener.CollectInfo(_antlrErrorListener.CodeSource, tree);

                    var shortFileName = Path.GetFileNameWithoutExtension(grammarFileName);
                    _grammarActionsTextSpan[grammarFileName] = grammarInfoCollectorListener.CodeInsertions.ToList();

                    if (!shortFileName.Contains(GrammarFactory.LexerPostfix))
                    {
                        string root;
                        bool preprocesor;
                        List<string> rules;
                        if (!shortFileName.Contains(GrammarFactory.PreprocessorPostfix))
                        {
                            result.Rules = grammarInfoCollectorListener.Rules;
                            rules = result.Rules;
                            root = _grammar.Root;
                            preprocesor = false;
                        }
                        else
                        {
                            result.PreprocessorRules = grammarInfoCollectorListener.Rules;
                            rules = result.PreprocessorRules;
                            root = _grammar.PreprocessorRoot;
                            preprocesor = true;
                        }
                        if (rules.Count > 0 && !rules.Contains(root))
                        {
                            root = rules.First();
                            if (!preprocesor)
                            {
                                _grammar.Root = root;
                            }
                            else
                            {
                                _grammar.PreprocessorRoot = root;
                            }
                        }

                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                }
                result.Errors = _antlrErrorListener.Errors;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.GrammarChecked));
                }
            }
            finally
            {
                GrammarCheckedState = result;
            }
            return result;
        }

        private ParserGeneratedState GenerateParser(GrammarCheckedState state)
        {
            ParserGeneratedState result = new ParserGeneratedState
            {
                GrammarCheckedState = state,
                Errors = _parserGenerationErrors
            };
            Processor processor = null;
            try
            {
                string runtimeDirectoryName = Path.Combine(HelperDirectoryName, Runtime.ToString());
                if (Directory.Exists(runtimeDirectoryName))
                {
                    Directory.Delete(runtimeDirectoryName, true);
                }
                Directory.CreateDirectory(runtimeDirectoryName);

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime);
                string[] extensions = runtimeInfo.Extensions;

                var jarGenerator = Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (var grammarFileName in state.Grammar.Files)
                {
                    _currentGrammarSource = _grammarFilesData[grammarFileName];
                    var arguments = $@"-jar ""{jarGenerator}"" ""{Path.Combine(_grammar.GrammarPath, grammarFileName)}"" -o ""{runtimeDirectoryName}"" " +
                        $"-Dlanguage={runtimeInfo.DLanguage} -no-visitor -no-listener";
                    if (Runtime == Runtime.Go)
                    {
                        arguments += " -package main";
                    }

                    processor = new Processor("java", arguments, ".");
                    processor.CancellationToken = _cancellationTokenSource.Token;
                    processor.ErrorDataReceived += ParserGeneration_ErrorDataReceived;
                    processor.OutputDataReceived += ParserGeneration_OutputDataReceived;

                    processor.Start();

                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.ParserGenerated));
                }
            }
            finally
            {
                processor?.Dispose();
            }
            return result;
        }

        private ParserCompiliedState CompileParser(ParserGeneratedState state)
        {
            ParserCompiliedState result = new ParserCompiliedState
            {
                ParserGeneratedState = state,
                Errors = _parserCompilationErrors,
                Root = _grammar.Root,
                PreprocessorRoot = _grammar.PreprocessorRoot
            };
            RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime);

            Processor processor = null;
            try
            {
                string arguments = "";
                string templateName = "";
                string runtimeDir = Path.Combine(RuntimesDirName, Runtime.ToString());
                string runtimeLibraryPath = Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);
                string extension = runtimeInfo.Extensions.First();
                string workingDirectory = Path.Combine(HelperDirectoryName, Runtime.ToString());

                var generatedFiles = new List<string>();
                generatedFiles.Add(_grammar.Name + runtimeInfo.LexerPostfix + "." + extension);
                generatedFiles.Add(_grammar.Name + runtimeInfo.ParserPostfix + "." + extension);
                generatedFiles = generatedFiles.Select(file => Path.Combine(workingDirectory, file)).ToList();
                var compiliedFiles = new StringBuilder();
                _grammarCodeMapping.Clear();

                foreach (string codeFileName in generatedFiles)
                {
                    compiliedFiles.Append('"' + Path.GetFileName(codeFileName) + "\" ");
                    CodeSource codeSource = new CodeSource(codeFileName, File.ReadAllText(codeFileName));
                    string shortCodeFileName = Path.GetFileName(codeFileName);

                    bool isLexer = Path.GetFileNameWithoutExtension(shortCodeFileName).EndsWith(runtimeInfo.LexerPostfix);
                    string shortGrammarFileName = GetGrammarFromCodeFileName(runtimeInfo, shortCodeFileName);

                    _grammarCodeMapping[shortCodeFileName] = TextHelpers.Map(_grammarActionsTextSpan[shortGrammarFileName], codeSource, isLexer);
                }

                templateName = runtimeInfo.MainFile;

                if (Runtime == Runtime.CSharpOptimized || Runtime == Runtime.CSharpStandard)
                {
                    string antlrCaseInsensitivePath = Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.cs");
                    if (_grammar.CaseInsensitive)
                    {
                        File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.cs"), antlrCaseInsensitivePath);
                    }

                    File.Copy(Path.Combine(runtimeDir, "Program.cs"), Path.Combine(workingDirectory, "Program.cs"));
                    File.Copy(Path.Combine(runtimeDir, "AssemblyInfo.cs"), Path.Combine(workingDirectory, "AssemblyInfo.cs"));
                    File.Copy(Path.Combine(runtimeDir, "Project.csproj"), Path.Combine(workingDirectory, $"{_grammar.Name}.csproj"));

                    arguments = "build";
                }
                else if (Runtime == Runtime.Java)
                {
                    compiliedFiles.Append('"' + templateName + '"');
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.java\"");
                        File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.java"), Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.java"), true);
                    }
                    arguments = $@"-cp ""{(Path.Combine("..", "..", runtimeLibraryPath))}"" " + compiliedFiles.ToString();
                }
                else if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (string file in generatedFiles)
                    {
                        var shortFileName = Path.GetFileNameWithoutExtension(file);
                        stringBuilder.AppendLine($"from {shortFileName} import {shortFileName}");
                    }
                    if (_grammar.CaseInsensitive)
                    {
                        File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.py"), Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.py"), true);
                    }
                    File.WriteAllText(Path.Combine(workingDirectory, PythonHelperFileName), stringBuilder.ToString());

                    if (runtimeInfo.RuntimeToolName == "py")
                    {
                        arguments += Runtime == Runtime.Python2 ? "-2 " : "-3 ";
                    }
                    arguments += PythonHelperFileName;
                }
                else if (Runtime == Runtime.JavaScript)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (string file in generatedFiles)
                    {
                        var shortFileName = Path.GetFileNameWithoutExtension(file);
                        stringBuilder.AppendLine($"var {shortFileName} = require('./{shortFileName}');");
                    }
                    File.WriteAllText(Path.Combine(workingDirectory, JavaScriptHelperFileName), stringBuilder.ToString());
                    if (_grammar.CaseInsensitive)
                    {
                        File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.js"), Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.js"), true);
                    }

                    arguments = JavaScriptHelperFileName;
                }
                else if (Runtime == Runtime.Go)
                {
                    compiliedFiles.Insert(0, '"' + templateName + "\" ");
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.go\"");
                        File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.go"), Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.go"), true);
                    }

                    arguments = "build " + compiliedFiles.ToString();
                }

                var templateFile = Path.Combine(workingDirectory, templateName);
                var code = File.ReadAllText(Path.Combine(runtimeDir, templateName));
                code = code.Replace(TemplateGrammarName, state.GrammarCheckedState.Grammar.Name);
                string root = _grammar.Root;
                if (Runtime == Runtime.Go)
                {
                    root = char.ToUpper(root[0]) + root.Substring(1);
                }
                code = code.Replace(TemplateGrammarRoot, root);
                if (_grammar.CaseInsensitive)
                {
                    code = code.Replace("from antlr4.InputStream import InputStream", "");
                    code = code.Replace(runtimeInfo.AntlrInputStream, (Runtime == Runtime.Go ? "New" : "") + "AntlrCaseInsensitiveInputStream");
                    if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                    {
                        code = code.Replace("'''AntlrCaseInsensitive'''",
                            "from AntlrCaseInsensitiveInputStream import AntlrCaseInsensitiveInputStream");
                    }
                    else if (Runtime == Runtime.JavaScript)
                    {
                        code = code.Replace("/*AntlrCaseInsensitive*/",
                            "var AntlrCaseInsensitiveInputStream = require('./AntlrCaseInsensitiveInputStream').AntlrCaseInsensitiveInputStream;");
                    }
                }
                else
                {
                    if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                    {
                        code = code.Replace("'''AntlrCaseInsensitive'''", "");
                    }
                    else if (Runtime == Runtime.JavaScript)
                    {
                        code = code.Replace("/*AntlrCaseInsensitive*/", "");
                    }
                }
                File.WriteAllText(templateFile, code);

                _buffer.Clear();

                processor = new Processor(runtimeInfo.RuntimeToolName, arguments, workingDirectory);
                processor.CancellationToken = _cancellationTokenSource.Token;
                processor.ErrorDataReceived += ParserCompilation_ErrorDataReceived;
                processor.OutputDataReceived += ParserCompilation_OutputDataReceived;

                processor.Start();

                if (_buffer.Count > 0)
                {
                    if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                    {
                        AddPythonError();
                    }
                    else if (Runtime == Runtime.JavaScript)
                    {
                        AddJavaScriptError();
                    }
                }

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.ParserCompilied));
                }
            }
            finally
            {
                processor?.Dispose();
            }
            return result;
        }

        private TextParsedState ParseText(ParserCompiliedState state)
        {
            var result = new TextParsedState
            {
                ParserCompiliedState = state,
                Text = Text,
                TextErrors = _textErrors
            };
            Processor processor = null;
            try
            {
                File.WriteAllText(Path.Combine(HelperDirectoryName, TextFileName), result.Text);

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime);
                string runtimeDir = Path.Combine(RuntimesDirName, Runtime.ToString());
                string runtimeLibraryPath = Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);

                string parserFileName = "";
                string arguments = "";
                string workingDirectory = Path.Combine(HelperDirectoryName, Runtime.ToString());

                if (Runtime == Runtime.CSharpOptimized || Runtime == Runtime.CSharpStandard)
                {
                    bool parse = EndStage != WorkflowStage.TextParsed;
                    arguments = $"\"{Path.Combine("bin", "Debug", "netcoreapp2.1", _grammar.Name + ".dll")}\" {Root} \"{Path.Combine("..", TextFileName)}\" {parse} {IndentedTree}";
                    parserFileName = "dotnet";
                }
                else if (Runtime == Runtime.Java)
                {
                    string relativeRuntimeLibraryPath = "\"" + Path.Combine("..", "..", runtimeLibraryPath) + "\"";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        relativeRuntimeLibraryPath += ";.";
                    }
                    else
                    {
                        relativeRuntimeLibraryPath = ".:" + relativeRuntimeLibraryPath;
                    }
                    arguments = $@"-cp {relativeRuntimeLibraryPath} Main ""{Path.Combine("..", TextFileName)}""";
                    parserFileName = "java";
                }
                else if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                {
                    parserFileName = runtimeInfo.RuntimeToolName;
                    if (parserFileName == "py")
                    {
                        arguments += Runtime == Runtime.Python2 ? "-2 " : "-3 ";
                    }
                    arguments += runtimeInfo.MainFile;
                }
                else if (Runtime == Runtime.JavaScript)
                {
                    parserFileName = runtimeInfo.RuntimeToolName;
                    arguments = runtimeInfo.MainFile;
                }
                else if (Runtime == Runtime.Go)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        parserFileName = Path.Combine(workingDirectory, Path.ChangeExtension(runtimeInfo.MainFile, ".exe"));
                    }
                    else
                    {
                        parserFileName = Path.Combine(workingDirectory, Path.GetFileNameWithoutExtension(runtimeInfo.MainFile));
                    }
                    /* Another way of starting.
                    parserFileName = CompilerPaths[Runtime];
                    var extension = runtimeInfo.Extensions.First();
                    var compiliedFiles = new StringBuilder();
                    compiliedFiles.Append('"' + runtimeInfo.MainFile + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.LexerPostfix + "." + extension + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.ParserPostfix + "." + extension + "\" ");
                    arguments = "run " + compiliedFiles.ToString();*/
                }

                processor = new Processor(parserFileName, arguments, workingDirectory);
                processor.CancellationToken = _cancellationTokenSource.Token;
                processor.ErrorDataReceived += TextParsing_ErrorDataReceived;
                processor.OutputDataReceived += TextParsing_OutputDataReceived;

                processor.Start();

                result.LexerTime = _outputLexerTime;
                result.ParserTime = _outputParserTime;
                result.Tokens = _outputTokens;
                result.Tree = _outputTree;

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.TextParsed));
                }
            }
            finally
            {
                processor?.Dispose();
            }
            return result;
        }

        private void Processor_OutputDataReceived(object sender, string e)
        {
            throw new NotImplementedException();
        }

        private void ParserGeneration_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var strs = e.Data.Split(':');
                ParsingError error;
                int line = 1, column = 1;
                if (strs.Length >= 4)
                {
                    if (!int.TryParse(strs[2], out line))
                    {
                        line = 1;
                    }
                    if (!int.TryParse(strs[3], out column))
                    {
                        column = 1;
                    }
                }
                error = new ParsingError(line, column, e.Data, _currentGrammarSource, WorkflowStage.ParserGenerated);
                AddError(error);
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
            }
        }

        private void ParserCompilation_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine(e.Data);
                if (Runtime == Runtime.CSharpStandard || Runtime == Runtime.CSharpOptimized)
                {
                    AddCSharpError(e.Data);
                }
                else if (Runtime == Runtime.Java)
                {
                    AddJavaError(e.Data);
                }
                else if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3 || Runtime == Runtime.JavaScript)
                {
                    lock (_buffer)
                    {
                        _buffer.Add(e.Data);
                    }
                }
                else if (Runtime == Runtime.Go)
                {
                    AddGoError(e.Data);
                }
            }
        }
        private void ParserCompilation_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine(e.Data);
                if (Runtime == Runtime.CSharpOptimized || Runtime == Runtime.CSharpStandard)
                {
                    AddCSharpError(e.Data);
                }
            }
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var errorString = Helpers.FixEncoding(e.Data);
                ParsingError error;
                var codeSource = new CodeSource("", _text);  // TODO: fix fileName
                try
                {
                    var words = errorString.Split(new[] { ' ' }, 3);
                    var strs = words[1].Split(':');
                    int line = 0, column = 0;
                    int.TryParse(strs[0], out line);
                    int.TryParse(strs[1], out column);
                    error = new ParsingError(line, column + 1, errorString, codeSource, WorkflowStage.TextParsed);
                }
                catch
                {
                    error = new ParsingError(errorString, codeSource, WorkflowStage.TextParsed);
                }
                AddError(error);
            }
        }

        private void TextParsing_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var strs = e.Data.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                TextParsedOutput outputState;
                if (Enum.TryParse(strs[0], out outputState))
                {
                    var data = strs[1];
                    switch (outputState)
                    {
                        case TextParsedOutput.LexerTime:
                            OutputLexerTime = TimeSpan.Parse(data);
                            break;
                        case TextParsedOutput.ParserTime:
                            OutputParserTime = TimeSpan.Parse(data);
                            break;
                        case TextParsedOutput.Tokens:
                            OutputTokens = data;
                            break;
                        case TextParsedOutput.Tree:
                            OutputTree = data.Replace("\\n", "\n");
                            break;
                    }
                }
                else
                {
                    AddError(new ParsingError(e.Data, _currentGrammarSource, WorkflowStage.TextParsed));
                }
            }
        }

        private void AddError(ParsingError error)
        {
            switch (error.WorkflowStage)
            {
                case WorkflowStage.GrammarChecked:
                    lock (_grammarCheckErrors)
                    {
                        _grammarCheckErrors.Add(error);
                    }
                    break;
                case WorkflowStage.ParserGenerated:
                    lock (_parserGenerationErrors)
                    {
                        _parserGenerationErrors.Add(error);
                    }
                    break;
                case WorkflowStage.ParserCompilied:
                    lock (_parserCompilationErrors)
                    {
                        _parserCompilationErrors.Add(error);
                    }
                    break;
                case WorkflowStage.TextTokenized:
                case WorkflowStage.TextParsed:
                    lock (_textErrors)
                    {
                        _textErrors.Add(error);
                    }
                    break;
            }
            _newErrorEvent?.Invoke(this, error);
        }

        private void ClearErrors(WorkflowStage stage)
        {
            switch (stage)
            {
                case WorkflowStage.GrammarChecked:
                    lock (_grammarCheckErrors)
                    {
                        _grammarCheckErrors.Clear();
                    }
                    break;
                case WorkflowStage.ParserGenerated:
                    lock (_parserGenerationErrors)
                    {
                        _parserGenerationErrors.Clear();
                    }
                    break;
                case WorkflowStage.ParserCompilied:
                    lock (_parserCompilationErrors)
                    {
                        _parserCompilationErrors.Clear();
                    }
                    break;
                case WorkflowStage.TextTokenized:
                case WorkflowStage.TextParsed:
                    lock (_textErrors)
                    {
                        _textErrors.Clear();
                    }
                    break;
            }
            ClearErrorsEvent?.Invoke(this, stage);
        }

        private string GetGrammarFromCodeFileName(RuntimeInfo runtimeInfo, string codeFileName)
        {
            if (!Grammar.SeparatedLexerAndParser)
            {
                var grammarFileName = Path.GetFileNameWithoutExtension(codeFileName);
                if (grammarFileName.EndsWith(runtimeInfo.LexerPostfix))
                {
                    grammarFileName = grammarFileName.Remove(grammarFileName.Length - runtimeInfo.LexerPostfix.Length);
                }
                else if (grammarFileName.EndsWith(runtimeInfo.ParserPostfix))
                {
                    grammarFileName = grammarFileName.Remove(grammarFileName.Length - runtimeInfo.ParserPostfix.Length);
                }
                return grammarFileName + Grammar.AntlrDotExt;
            }
            else
            {
                return Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);
            }
        }

        private void AddCSharpError(string data)
        {
            if (data.Contains(": error CS"))
            {
                var errorString = Helpers.FixEncoding(data);
                ParsingError error;
                CodeSource grammarSource = CodeSource.Empty;
                try
                {
                    // Format:
                    // Lexer.cs(106,11): error CS0103: The name 'a' does not exist in the current context
                    var strs = errorString.Split(':');
                    int leftParenInd = strs[0].IndexOf('(');
                    string codeFileName = strs[0].Remove(leftParenInd);
                    string grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.CSharpStandard], codeFileName);
                    string lineColumnString = strs[0].Substring(leftParenInd);
                    lineColumnString = lineColumnString.Substring(1, lineColumnString.Length - 2); // Remove parenthesis.
                    var strs2 = lineColumnString.Split(',');
                    int line = int.Parse(strs2[0]);
                    int column = int.Parse(strs2[1]);
                    string rest = string.Join(":", strs.Skip(1));
                    var grammarTextSpan = TextHelpers.GetSourceTextSpanForLineColumn(_grammarCodeMapping[codeFileName], line, column);

                    grammarSource = _grammarFilesData[grammarFileName];
                    if (grammarTextSpan != null)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.StartLineColumn.Line}:{rest}", WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        error = new ParsingError($"{grammarFileName}:{rest}", grammarSource, WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(errorString, grammarSource, WorkflowStage.ParserCompilied);
                }
                AddError(error);
            }
        }

        private void AddJavaError(string data)
        {
            if (data.Count(c => c == ':') >= 2)
            {
                ParsingError error = null;
                string grammarFileName = "";
                try
                {
                    // Format:
                    // Lexer.java:98: error: cannot find symbol
                    string[] strs = data.Split(':');
                    string codeFileName = strs[0];
                    int codeLine = int.Parse(strs[1]);
                    string rest = string.Join(":", strs.Skip(2));
                    TextSpan grammarTextSpan = TextHelpers.GetSourceTextSpanForLine(_grammarCodeMapping[codeFileName], codeLine);
                    grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Java], codeFileName);
                    if (grammarTextSpan != null)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.StartLineColumn.Line}:{rest}", WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        error = new ParsingError($"{grammarFileName}:{rest}", _grammarFilesData[grammarFileName], WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(data, CodeSource.Empty, WorkflowStage.ParserCompilied);
                }
                AddError(error);
            }
        }

        private void AddPythonError()
        {
            //Format:
            //Traceback(most recent call last):
            //  File "AntlrPythonCompileTest.py", line 1, in < module >
            //    from NewGrammarLexer import NewGrammarLexer
            //  File "Absolute\Path\To\LexerOrParser.py", line 23
            //    decisionsToDFA = [DFA(ds, i) for i, ds in enumerate(atn.decisionToState) ]
            //    ^
            //IndentationError: unexpected indent
            string message = "";
            string grammarFileName = "";
            TextSpan errorSpan = TextSpan.Empty;
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i].TrimStart().StartsWith("File"))
                {
                    if (grammarFileName != "")
                    {
                        continue;
                    }

                    string codeFileName = _buffer[i];
                    codeFileName = codeFileName.Substring(codeFileName.IndexOf('"') + 1);
                    codeFileName = codeFileName.Remove(codeFileName.IndexOf('"'));
                    codeFileName = Path.GetFileName(codeFileName);

                    List<TextSpanMapping> mapping;
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                    {
                        try
                        {
                            var lineStr = "\", line ";
                            lineStr = _buffer[i].Substring(_buffer[i].IndexOf(lineStr) + lineStr.Length);
                            int commaIndex = lineStr.IndexOf(',');
                            if (commaIndex != -1)
                            {
                                lineStr = lineStr.Remove(commaIndex);
                            }
                            int codeLine = int.Parse(lineStr);
                            errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                            grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Python2], codeFileName);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        grammarFileName = "";
                    }
                }
                else if (i == _buffer.Count - 1)
                {
                    message = _buffer[i].Trim();
                }
            }
            string finalMessage = "";
            if (grammarFileName != "")
            {
                finalMessage = grammarFileName + ":";
            }
            if (!errorSpan.IsEmpty)
            {
                finalMessage += errorSpan.StartLineColumn.Line + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
        }

        private void AddJavaScriptError()
        {
            //Absolute\Path\To\LexerOrParser.js:68
            //                break;
            //                ^^^^^
            //
            //SyntaxError: Unexpected token break
            //    at exports.runInThisContext (vm.js:53:16)
            //    at Module._compile (module.js:373:25)
            //    at Object.Module._extensions..js (module.js:416:10)
            //    at Module.load (module.js:343:32)
            //    at Function.Module._load (module.js:300:12)
            //    at Module.require (module.js:353:17)
            //    at require (internal/module.js:12:17)
            //    at Object.<anonymous> (Absolute\Path\To\AntlrJavaScriptTest.js:1:85)
            //    at Module._compile (module.js:409:26)
            //    at Object.Module._extensions..js (module.js:416:10)
            string message = "";
            string grammarFileName = "";
            TextSpan errorSpan = TextSpan.Empty;
            try
            {
                int semicolonLastIndex = _buffer[0].LastIndexOf(':');
                string codeFileName = Path.GetFileName(_buffer[0].Remove(semicolonLastIndex));
                List<TextSpanMapping> mapping;
                if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                {
                    int codeLine = int.Parse(_buffer[0].Substring(semicolonLastIndex + 1));
                    errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                    grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.JavaScript], codeFileName);
                }
            }
            catch
            {
            }
            if (_buffer.Count > 0)
            {
                message = _buffer.LastOrDefault(line => !line.StartsWith("    at")) ?? "";
            }
            string finalMessage = "";
            if (grammarFileName != "")
            {
                finalMessage = grammarFileName + ":";
            }
            if (!errorSpan.IsEmpty)
            {
                finalMessage += errorSpan.StartLineColumn.Line + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
        }

        private void AddGoError(string data)
        {
            if (data.Contains(": syntax error:"))
            {
                // Format:
                // .\newgrammar_parser.go:169: syntax error: unexpected semicolon or newline, expecting expression
                string grammarFileName = "";
                TextSpan errorSpan = TextSpan.Empty;
                string message = "";
                var strs = data.Split(':');
                try
                {
                    var runtimeInfo = RuntimeInfo.Runtimes[Runtime.Go];
                    string codeFileName = strs[0].Substring(2);
                    List<TextSpanMapping> mapping;
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                    {
                        int codeLine = int.Parse(strs[1]);
                        errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                        grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Go], codeFileName);
                    }
                    message = strs[3];
                }
                catch
                {
                }
                string finalMessage = "";
                if (grammarFileName != "")
                {
                    finalMessage = grammarFileName + ":";
                }
                if (!errorSpan.IsEmpty)
                {
                    finalMessage += errorSpan.StartLineColumn.Line + ":";
                }
                finalMessage += message == "" ? "Unknown Error" : message;
                AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
            }
        }
    }
}
