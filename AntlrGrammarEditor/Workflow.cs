using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class Workflow
    {
        private static string HelperDirectoryName = "AntlrGrammarEditorHelperDirectory42";
        private const string PythonHelperFileName = "AntlrPythonCompileTest.py";
        private const string JavaScriptHelperFileName = "AntlrJavaScriptTest.js";
        private const string TextFileName = "Text";

        private const string TemplateGrammarName = "AntlrGrammarName42";
        private const string TemplateGrammarRoot = "AntlrGrammarRoot42";

        private const int GenerateParserProcessTimeout = 200;
        private const int CompileParserProcessTimeout = 200;
        private const int ParseTextTimeout = 200;

        private const string CheckGrammarCancelMessage = "Grammar checking has been cancelled.";
        private const string GenerateParserCancelMessage = "Parser generation has been cancelled.";
        private const string CompileParserCancelMessage = "Parser compilation has been cancelled.";
        private const string ParseTextCancelMessage = "Text parsing has been cancelled.";

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
        private int _eventInvokeCounter;

        private CancellationTokenSource _cancellationTokenSource;
        private InputState _inputState = new InputState();
        private object _lockObj = new object();
        private string _currentFileName;
        private Dictionary<string, string> _grammarFilesData = new Dictionary<string, string>();
        private Dictionary<string, List<CodeInsertion>> _grammarActionsTextSpan = new Dictionary<string, List<CodeInsertion>>();
        private Dictionary<string, List<TextSpanMapping>> _grammarCodeMapping = new Dictionary<string, List<TextSpanMapping>>();
        private string _currentFileData;

        public string JavaPath { get; set; }

        public Dictionary<Runtime, string> CompilerPaths { get; set; } = new Dictionary<Runtime, string>();

        public GrammarCheckedState GrammarCheckedState { get; private set; }

        public WorkflowState CurrentState
        {
            get
            {
                return _currentState;
            }
            private set
            {
                _currentState = value;
                StateChanged?.Invoke(this, _currentState);
            }
        }

        public Grammar Grammar
        {
            get
            {
                return _grammar;
            }
            set
            {
                StopIfRequired();
                _grammar = value;
                RollbackToStage(WorkflowStage.Input);
            }
        }

        public Runtime Runtime
        {
            get
            {
                return Grammar.Runtimes.First();
            }
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
            get
            {
                return _grammar.Root;
            }
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
            get
            {
                return _grammar.PreprocessorRoot;
            }
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
            get
            {
                return _text;
            }
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
            get
            {
                return _outputLexerTime;
            }
            set
            {
                _outputLexerTime = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.LexerTime, _outputLexerTime));
            }
        }

        public TimeSpan OutputParserTime
        {
            get
            {
                return _outputParserTime;
            }
            set
            {
                _outputParserTime = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.ParserTime, _outputParserTime));
            }
        }

        public string OutputTokens
        {
            get
            {
                return _outputTokens;
            }
            private set
            {
                _outputTokens = value;
                TextParsedOutputEvent?.Invoke(this, new Tuple<TextParsedOutput, object>(TextParsedOutput.Tokens, _outputTokens));
            }
        }

        public string OutputTree
        {
            get
            {
                return _outputTree;
            }
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
                    _antlrErrorListener.CurrentFileName = grammarFileName;
                    var inputStream = new AntlrFileStream(Path.Combine(_grammar.GrammarPath, grammarFileName));
                    _grammarFilesData[grammarFileName] = inputStream.ToString();
                    _antlrErrorListener.CurrentFileData = _grammarFilesData[grammarFileName];
                    var antlr4Lexer = new ANTLRv4Lexer(inputStream);
                    antlr4Lexer.RemoveErrorListeners();
                    antlr4Lexer.AddErrorListener(_antlrErrorListener);
                    var codeTokenSource = new ListTokenSource(antlr4Lexer.GetAllTokens());

                    CancelOperationIfRequired(CheckGrammarCancelMessage);

                    var codeTokenStream = new CommonTokenStream(codeTokenSource);
                    var antlr4Parser = new ANTLRv4Parser(codeTokenStream);

                    antlr4Parser.RemoveErrorListeners();
                    antlr4Parser.AddErrorListener(_antlrErrorListener);

                    var tree = antlr4Parser.grammarSpec();

                    var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
                    grammarInfoCollectorListener.CollectInfo(tree);

                    var shortFileName = Path.GetFileNameWithoutExtension(grammarFileName);
                    if (shortFileName.Contains(GrammarFactory.LexerPostfix))
                    {
                        _grammarActionsTextSpan[grammarFileName] = grammarInfoCollectorListener.CodeInsertions.Where(insertion => insertion.Lexer).ToList();
                    }
                    else if (shortFileName.Contains(GrammarFactory.ParserPostfix))
                    {
                        _grammarActionsTextSpan[grammarFileName] = grammarInfoCollectorListener.CodeInsertions.Where(insertion => !insertion.Lexer).ToList();
                    }
                    else
                    {
                        _grammarActionsTextSpan[shortFileName + GrammarFactory.LexerPostfix + Grammar.AntlrDotExt] =
                            grammarInfoCollectorListener.CodeInsertions.Where(insertion => insertion.Lexer).ToList();
                        _grammarActionsTextSpan[shortFileName + GrammarFactory.ParserPostfix + Grammar.AntlrDotExt] =
                            grammarInfoCollectorListener.CodeInsertions.Where(insertion => !insertion.Lexer).ToList();
                    }

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

                        CancelOperationIfRequired(CheckGrammarCancelMessage);
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
            GrammarCheckedState = result;
            return result;
        }

        private ParserGeneratedState GenerateParser(GrammarCheckedState state)
        {
            ParserGeneratedState result = new ParserGeneratedState
            {
                GrammarCheckedState = state,
                Errors = _parserGenerationErrors
            };
            Process process = null;
            try
            {
                if (!Directory.Exists(HelperDirectoryName))
                {
                    Directory.CreateDirectory(HelperDirectoryName);
                }
                CancelOperationIfRequired(GenerateParserCancelMessage);

                var runtrimeInfo = Runtime.GetRuntimeInfo();
                string[] extensions = runtrimeInfo.Extensions;

                foreach (var grammarFileName in state.Grammar.Files)
                {
                    _currentFileName = grammarFileName;
                    _currentFileData = _grammarFilesData[grammarFileName];
                    var arguments = $@"-jar ""Generators\{runtrimeInfo.JarGenerator}"" ""{Path.Combine(_grammar.GrammarPath, grammarFileName)}"" -o ""{HelperDirectoryName}"" " +
                        $"-Dlanguage={runtrimeInfo.DLanguage} -no-visitor -no-listener";
                    if (Runtime == Runtime.Go)
                    {
                        arguments += " -package main";
                    }

                    _eventInvokeCounter = 0;
                    process = ProcessHelpers.SetupHiddenProcessAndStart(JavaPath, arguments, null, ParserGeneration_ErrorDataReceived, ParserGeneration_OutputDataReceived);

                    while (!process.HasExited || _eventInvokeCounter > 0)
                    {
                        Thread.Sleep(GenerateParserProcessTimeout);
                        CancelOperationIfRequired(GenerateParserCancelMessage);
                    }

                    CancelOperationIfRequired(GenerateParserCancelMessage);
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
                if (process != null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
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
            var runtimeInfo = Runtime.GetRuntimeInfo();

            Process process = null;
            try
            {
                string compilerPath = "";
                string arguments = "";
                string templateName = "";
                string workingDirectory = HelperDirectoryName;
                string runtimeLibraryPath = Path.Combine("Runtimes", Runtime.ToString(), runtimeInfo.RuntimeLibrary);
                string extension = runtimeInfo.Extensions.First();

                var generatedFiles = new List<string>();
                generatedFiles.Add(_grammar.Name + runtimeInfo.LexerPostfix + "." + extension);
                generatedFiles.Add(_grammar.Name + runtimeInfo.ParserPostfix + "." + extension);
                generatedFiles = generatedFiles.Select(file => Path.Combine(HelperDirectoryName, file)).ToList();
                var compiliedFiles = new StringBuilder();
                _grammarCodeMapping.Clear();
                foreach (var codeFileName in generatedFiles)
                {
                    compiliedFiles.Append('"' + Path.GetFileName(codeFileName) + "\" ");
                    var text = File.ReadAllText(codeFileName);
                    var shortCodeFileName = Path.GetFileName(codeFileName);
                    var shortGrammarFileName = shortCodeFileName.Replace("." + extension, Grammar.AntlrDotExt);
                    if (codeFileName.Contains(runtimeInfo.LexerPostfix))
                    {
                        shortGrammarFileName = shortGrammarFileName.Replace(runtimeInfo.LexerPostfix, GrammarFactory.LexerPostfix);
                    }
                    else if (codeFileName.Contains(runtimeInfo.ParserPostfix))
                    {
                        shortGrammarFileName = shortGrammarFileName.Replace(runtimeInfo.ParserPostfix, GrammarFactory.ParserPostfix);
                    }
                    _grammarCodeMapping[shortCodeFileName] = TextHelpers.Map(_grammarActionsTextSpan[shortGrammarFileName], text);
                }

                templateName = runtimeInfo.MainFile;
                compilerPath = CompilerPaths[Runtime];
                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    compiliedFiles.Append('"' + templateName + '"');
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"..\\" + Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.cs") + "\"");
                    }
                    arguments = $@"/reference:""..\{runtimeLibraryPath}"" /out:{Runtime}_{state.GrammarCheckedState.Grammar.Name}Parser.exe " + compiliedFiles;
                }
                else if (Runtime == Runtime.Java)
                {
                    compiliedFiles.Append('"' + templateName + '"');
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.java\"");
                        File.Copy(Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.java"), Path.Combine(HelperDirectoryName, "AntlrCaseInsensitiveInputStream.java"), true);
                    }
                    arguments = $@"-cp ""..\{runtimeLibraryPath}"" " + compiliedFiles.ToString();
                }
                else if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (var file in generatedFiles)
                    {
                        var shortFileName = Path.GetFileNameWithoutExtension(file);
                        stringBuilder.AppendLine($"from {shortFileName} import {shortFileName}");
                    }
                    File.WriteAllText(Path.Combine(HelperDirectoryName, PythonHelperFileName), stringBuilder.ToString());

                    if (_grammar.CaseInsensitive)
                    {
                        File.Copy(Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.py"), Path.Combine(HelperDirectoryName, "AntlrCaseInsensitiveInputStream.py"), true);
                    }
                    
                    if (runtimeInfo.DefaultCompilerPath == "py")
                    {
                        arguments += Runtime == Runtime.Python2 ? "-2 " : "-3 ";
                    }
                    arguments += PythonHelperFileName;
                }
                else if (Runtime == Runtime.JavaScript)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (var file in generatedFiles)
                    {
                        var shortFileName = Path.GetFileNameWithoutExtension(file);
                        stringBuilder.AppendLine($"var {shortFileName} = require('./{shortFileName}');");
                    }
                    File.WriteAllText(Path.Combine(HelperDirectoryName, JavaScriptHelperFileName), stringBuilder.ToString());
                    if (_grammar.CaseInsensitive)
                    {
                        File.Copy(Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.js"), Path.Combine(HelperDirectoryName, "AntlrCaseInsensitiveInputStream.js"), true);
                    }
                    
                    arguments = JavaScriptHelperFileName;
                }
                else if (Runtime == Runtime.Go)
                {
                    foreach (var generatedFile in generatedFiles)
                    {
                        var fileText = File.ReadAllText(generatedFile); // TODO: bug in golang runtime.
                        if (fileText.Contains("package parser"))
                        {
                            fileText = fileText.Replace("package parser", "package main");
                            File.WriteAllText(generatedFile, fileText);
                        }
                    }
                    compiliedFiles.Insert(0, '"' + templateName + "\" ");
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.go\"");
                        File.Copy(Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.go"), Path.Combine(HelperDirectoryName, "AntlrCaseInsensitiveInputStream.go"), true);
                    }

                    arguments = "build " + compiliedFiles.ToString();
                }

                var templateFile = Path.Combine(HelperDirectoryName, templateName);
                var code = File.ReadAllText(Path.Combine("Runtimes", Runtime.ToString(), templateName));
                code = code.Replace(TemplateGrammarName, state.GrammarCheckedState.Grammar.Name);
                string root = _grammar.Root;
                if (Runtime == Runtime.Go)
                {
                    root = char.ToUpper(root[0]) + root.Substring(1);
                }
                code = code.Replace(TemplateGrammarRoot, root);
                if (_grammar.CaseInsensitive)
                {
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

                _eventInvokeCounter = 0;
                _buffer.Clear();
                process = ProcessHelpers.SetupHiddenProcessAndStart(compilerPath, arguments, workingDirectory, ParserCompilation_ErrorDataReceived, ParserCompilation_OutputDataReceived);

                while (!process.HasExited || _eventInvokeCounter > 0)
                {
                    Thread.Sleep(CompileParserProcessTimeout);
                    CancelOperationIfRequired(CompileParserCancelMessage);
                }

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

                CancelOperationIfRequired(CompileParserCancelMessage);
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
                if (process != null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
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
            Process process = null;
            try
            {
                File.WriteAllText(Path.Combine(HelperDirectoryName, TextFileName), result.Text);

                var runtimeInfo = Runtime.GetRuntimeInfo();
                string runtimeLibraryPath = Path.Combine("Runtimes", Runtime.ToString(), runtimeInfo.RuntimeLibrary);
                string parserFileName = "";
                string arguments = "";
                string workingDirectory = HelperDirectoryName;
                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    var antlrRuntimeDir = Path.Combine(HelperDirectoryName, runtimeInfo.RuntimeLibrary);
                    //if (!File.Exists(antlrRuntimeDir))
                    {
                        File.Copy(runtimeLibraryPath, antlrRuntimeDir, true);
                    }
                    parserFileName = Path.Combine(HelperDirectoryName, $"{Runtime}_{state.ParserGeneratedState.GrammarCheckedState.Grammar.Name}Parser.exe");
                    arguments = $"{Root} \"\" {(EndStage == WorkflowStage.TextParsed ? false : true)}";
                }
                else if (Runtime == Runtime.Java)
                {
                    parserFileName = JavaPath;
                    arguments = $@"-cp ""..\{runtimeLibraryPath}"";. " + "Main " + TextFileName;
                }
                else if (Runtime == Runtime.Python2 || Runtime == Runtime.Python3)
                {
                    parserFileName = CompilerPaths[Runtime];
                    if (parserFileName == "py")
                    {
                        arguments += Runtime == Runtime.Python2 ? "-2 " : "-3 ";
                    }
                    arguments += runtimeInfo.MainFile;
                }
                else if (Runtime == Runtime.JavaScript)
                {
                    parserFileName = CompilerPaths[Runtime];
                    arguments = runtimeInfo.MainFile;
                }
                else if (Runtime == Runtime.Go)
                {
                    parserFileName = Path.Combine(HelperDirectoryName, Path.ChangeExtension(runtimeInfo.MainFile, ".exe"));
                    /* Another way of starting.
                    parserFileName = CompilerPaths[Runtime];
                    var extension = runtimeInfo.Extensions.First();
                    var compiliedFiles = new StringBuilder();
                    compiliedFiles.Append('"' + runtimeInfo.MainFile + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.LexerPostfix + "." + extension + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.ParserPostfix + "." + extension + "\" ");
                    arguments = "run " + compiliedFiles.ToString();*/
                }

                _eventInvokeCounter = 0;
                process = ProcessHelpers.SetupHiddenProcessAndStart(parserFileName, arguments, workingDirectory, TextParsing_ErrorDataReceived, TextParsing_OutputDataReceived);

                while (!process.HasExited || _eventInvokeCounter > 0)
                {
                    Thread.Sleep(ParseTextTimeout);
                    CancelOperationIfRequired(ParseTextCancelMessage);
                }

                result.LexerTime = _outputLexerTime;
                result.ParserTime = _outputParserTime;
                result.Tokens = _outputTokens;
                result.Tree = _outputTree;

                CancelOperationIfRequired(ParseTextCancelMessage);
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
                if (process != null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
            }
            return result;
        }

        private void ParserGeneration_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var strs = e.Data.Split(':');
                    ParsingError error;
                    if (strs.Length >= 4)
                    {
                        error = new ParsingError(int.Parse(strs[2]), int.Parse(strs[3]), e.Data, _currentFileName, _currentFileData, WorkflowStage.ParserGenerated);
                    }
                    else
                    {
                        error = new ParsingError(0, 0, e.Data, _currentFileName, _currentFileData, WorkflowStage.ParserGenerated);
                    }
                    AddError(error);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            if (!string.IsNullOrEmpty(e.Data))
            {
            }

            Interlocked.Decrement(ref _eventInvokeCounter);
        }

        private void ParserCompilation_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                    if (Runtime == Runtime.Java)
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
            finally
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
            }
        }

        private void ParserCompilation_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                    if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                    {
                        AddCSharpError(e.Data);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
            }
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var errorString = Helpers.FixEncoding(e.Data);
                    ParsingError error;
                    try
                    {
                        var words = errorString.Split(' ');
                        var strs = words[1].Split(':');
                        error = new ParsingError(int.Parse(strs[0]), int.Parse(strs[1]), errorString, "", _text, WorkflowStage.TextParsed);  // TODO: fix fileName
                    }
                    catch
                    {
                        error = new ParsingError(0, 0, errorString, "", _text, WorkflowStage.TextParsed);  // TODO: fix fileName
                    }
                    AddError(error);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
            }
        }

        private void TextParsing_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Interlocked.Increment(ref _eventInvokeCounter);

            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var strs = e.Data.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    var outputState = (TextParsedOutput)Enum.Parse(typeof(TextParsedOutput), strs[0]);
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
                            OutputTree = data;
                            break;
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
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

        private void CancelOperationIfRequired(string message)
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException(message, _cancellationTokenSource.Token);
            }
        }

        private void AddCSharpError(string data)
        {
            if (data.Contains(": error CS"))
            {
                var errorString = Helpers.FixEncoding(data);
                ParsingError error;
                string grammarFileName = "";
                try
                {
                    // Format:
                    // Lexer.cs(106,11): error CS0103: The name 'a' does not exist in the current context
                    var strs = errorString.Split(':');
                    int leftParenInd = strs[0].IndexOf('(');
                    string codeFileName = strs[0].Remove(leftParenInd);
                    grammarFileName = Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);
                    string lineColumnString = strs[0].Substring(leftParenInd);
                    lineColumnString = lineColumnString.Substring(1, lineColumnString.Length - 2); // Remove parenthesis.
                    var strs2 = lineColumnString.Split(',');
                    int line = int.Parse(strs2[0]);
                    int column = int.Parse(strs2[1]);
                    string rest = string.Join(":", strs.Skip(1));
                    var grammarTextSpan = TextHelpers.GetSourceTextSpanForLineColumn(_grammarCodeMapping[codeFileName], line, column);
                    if (!_grammar.SeparatedLexerAndParser)
                    {
                        grammarFileName = grammarFileName.Replace(GrammarFactory.ParserPostfix, "").Replace(GrammarFactory.LexerPostfix, "");
                    }
                    if (grammarTextSpan != null)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.BeginLine}:{rest}", grammarFileName, WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        error = new ParsingError(0, 0, $"{grammarFileName}:{rest}", grammarFileName, WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(0, 0, errorString, grammarFileName, WorkflowStage.ParserCompilied);
                }
                AddError(error);
            }
        }

        private void AddJavaError(string data)
        {
            if (data.Count(c => c == ':') >= 2)
            {
                ParsingError error;
                string grammarFileName = "";
                try
                {
                    // Format:
                    // Lexer.java:98: error: cannot find symbol
                    var strs = data.Split(':');
                    string codeFileName = strs[0];
                    grammarFileName = Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);
                    int codeLine = int.Parse(strs[1]);
                    string rest = string.Join(":", strs.Skip(2));
                    var grammarTextSpan = TextHelpers.GetSourceTextSpanForLine(_grammarCodeMapping[codeFileName], codeLine);
                    if (!_grammar.SeparatedLexerAndParser)
                    {
                        grammarFileName = grammarFileName.Replace(GrammarFactory.ParserPostfix, "").Replace(GrammarFactory.LexerPostfix, "");
                    }
                    if (grammarTextSpan != null)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.BeginLine}:{rest}", grammarFileName, WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        error = new ParsingError(0, 0, $"{grammarFileName}:{rest}", grammarFileName, WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(0, 0, data, grammarFileName, WorkflowStage.ParserCompilied);
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
                    grammarFileName = Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);

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
                            errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine) ?? TextSpan.Empty;
                            if (!_grammar.SeparatedLexerAndParser)
                            {
                                grammarFileName = grammarFileName.Replace(GrammarFactory.ParserPostfix, "").Replace(GrammarFactory.LexerPostfix, "");
                            }
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
                finalMessage += errorSpan.BeginLine + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, grammarFileName, WorkflowStage.ParserCompilied));
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
                grammarFileName = Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);
                List<TextSpanMapping> mapping;
                if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                {
                    int codeLine = int.Parse(_buffer[0].Substring(semicolonLastIndex + 1));
                    errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine) ?? TextSpan.Empty;
                    if (!_grammar.SeparatedLexerAndParser)
                    {
                        grammarFileName = grammarFileName.Replace(GrammarFactory.ParserPostfix, "").Replace(GrammarFactory.LexerPostfix, "");
                    }
                }
                else
                {
                    grammarFileName = "";
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
                finalMessage += errorSpan.BeginLine + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, grammarFileName, WorkflowStage.ParserCompilied));
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
                    grammarFileName = Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt)
                        .Replace(runtimeInfo.LexerPostfix, GrammarFactory.LexerPostfix)
                        .Replace(runtimeInfo.ParserPostfix, GrammarFactory.ParserPostfix);
                    List<TextSpanMapping> mapping;
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                    {
                        int codeLine = int.Parse(strs[1]);
                        errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine) ?? TextSpan.Empty;
                        if (!_grammar.SeparatedLexerAndParser)
                        {
                            grammarFileName = grammarFileName.Replace(GrammarFactory.ParserPostfix, "").Replace(GrammarFactory.LexerPostfix, "");
                        }
                    }
                    else
                    {
                        grammarFileName = "";
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
                    finalMessage += errorSpan.BeginLine + ":";
                }
                finalMessage += message == "" ? "Unknown Error" : message;
                AddError(new ParsingError(errorSpan, finalMessage, grammarFileName, WorkflowStage.ParserCompilied));
            }
        }
    }
}
