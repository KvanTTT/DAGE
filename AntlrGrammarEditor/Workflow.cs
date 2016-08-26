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
        private const string TextFileName = "Text";

        private const string TemplateGrammarName = "AntlrGrammarName42";
        private const string TemplateGrammarRoot = "AntlrGrammarRoot42";

        private const int GenerateParserProcessTimeout = 200;
        private const int CompileParserProcessTimeout = 200;
        private const int ParseTextTimeout = 200;

        private string CheckGrammarCancelMessage = "Grammar checking has been cancelled.";
        private string GenerateParserCancelMessage = "Parser generation has been cancelled.";
        private string CompileParserCancelMessage = "Parser compilation has been cancelled.";
        private string ParseTextCancelMessage = "Text parsing has been cancelled.";

        private Grammar _grammar = new Grammar();
        private string _text = "";
        private WorkflowState _currentState;

        private List<ParsingError> _parserGenerationErrors = new List<ParsingError>();
        private List<ParsingError> _parserCompilationErrors = new List<ParsingError>();

        private List<ParsingError> _textErrors = new List<ParsingError>();
        private string _outputTree;
        private string _outputTokens;
        private TimeSpan _outputLexerTime;
        private TimeSpan _outputParserTime;

        private CancellationTokenSource _cancellationTokenSource;
        private InputState _inputState = new InputState();
        private object _lockObj = new object();

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
                StopIfRequired();
                Grammar.Runtimes.Add(value);
                RollbackToStage(WorkflowStage.GrammarChecked);
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
                StopIfRequired();
                _grammar.Root = value;
                RollbackToStage(WorkflowStage.ParserGenerated); // TODO: make ParserGenerated stage.
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
                StopIfRequired();
                _grammar.PreprocessorRoot = value;
                RollbackToStage(WorkflowStage.ParserGenerated);
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
                StopIfRequired();
                _text = value;
                RollbackToStage(WorkflowStage.ParserCompilied);
            }
        }

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public event EventHandler<WorkflowState> StateChanged;

        static Workflow()
        {
            var tempDir = Path.GetTempPath();
            //GeneratedDirectoryName = Path.Combine(tempDir, GeneratedDirectoryName);
            //ParserDirectoryName = Path.Combine(tempDir, ParserDirectoryName);
        }

        public Workflow()
        {
            CurrentState = _inputState;
        }

        public WorkflowState Process()
        {
            StopIfRequired();
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
                CurrentState = CurrentState.PreviousState;
            }
        }

        public void RollbackToStage(WorkflowStage stage)
        {
            while (CurrentState.Stage > stage && CurrentState.PreviousState != null)
            {
                if (CurrentState.Stage <= WorkflowStage.TextParsed)
                {
                    _outputTree = "";
                    _textErrors.Clear();
                }
                if (CurrentState.Stage <= WorkflowStage.ParserCompilied)
                {
                    _parserCompilationErrors.Clear();
                }
                if (CurrentState.Stage <= WorkflowStage.ParserGenerated)
                {
                    _parserGenerationErrors.Clear();
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
            var errorListener = new AntlrErrorListener();

            var result = new GrammarCheckedState
            {
                Grammar = Grammar,
                InputState = _inputState,
                Rules = new List<string>()
            };
            try
            {
                foreach (var file in Grammar.Files)
                {
                    errorListener.CurrentFileName = Path.GetFileName(file);
                    var inputStream = new AntlrFileStream(file);
                    var antlr4Lexer = new ANTLRv4Lexer(inputStream);
                    antlr4Lexer.RemoveErrorListeners();
                    antlr4Lexer.AddErrorListener(errorListener);
                    var codeTokenSource = new ListTokenSource(antlr4Lexer.GetAllTokens());

                    CancelOperationIfRequired(CheckGrammarCancelMessage);

                    var codeTokenStream = new CommonTokenStream(codeTokenSource);
                    var antlr4Parser = new ANTLRv4Parser(codeTokenStream);

                    antlr4Parser.RemoveErrorListeners();
                    antlr4Parser.AddErrorListener(errorListener);

                    var tree = antlr4Parser.grammarSpec();

                    var shortFileName = Path.GetFileNameWithoutExtension(file);
                    if (!shortFileName.Contains(GrammarFactory.LexerPostfix))
                    {
                        var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
                        grammarInfoCollectorListener.CollectInfo(tree);

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
                result.Errors = errorListener.Errors;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            GrammarCheckedState = result;
            return result;
        }

        private ParserGeneratedState GenerateParser(GrammarCheckedState state)
        {
            ParserGeneratedState result = new ParserGeneratedState { GrammarCheckedState = state, Errors = _parserGenerationErrors };
            Process process = null;
            try
            {
                if (!Directory.Exists(HelperDirectoryName))
                {
                    Directory.CreateDirectory(HelperDirectoryName);
                }
                CancelOperationIfRequired(GenerateParserCancelMessage);
                _parserGenerationErrors.Clear();

                string extension = GetExtension(Runtime);
                var runtimeExtensionFiles = Directory.GetFiles(HelperDirectoryName, "*." + extension);
                foreach (var file in runtimeExtensionFiles)
                {
                    File.Delete(file);
                }
                var javaPath = "java";

                foreach (var fileName in state.Grammar.Files)
                {
                    var arguments = $@"-jar ""{GetAntlrGenerator(Runtime)}"" ""{fileName}"" -o ""{HelperDirectoryName}"" " +
                        $"-Dlanguage={GetLanguage(Runtime)} -no-visitor -no-listener";

                    process = SetupAndStartProcess(javaPath, arguments, null, ParserGeneration_ErrorDataReceived, ParserGeneration_OutputDataReceived);

                    while (!process.HasExited)
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
            ParserCompiliedState result = new ParserCompiliedState { ParserGeneratedState = state, Errors = _parserCompilationErrors };
            Process process = null;
            try
            {
                _parserCompilationErrors.Clear();
                string compilatorPath = "";
                string arguments = "";
                string templateName = "";
                string workingDirectory = HelperDirectoryName;
                string runtimeLibraryPath = Path.Combine("Runtimes", Runtime.ToString(), GetLibraryName(Runtime));

                string extension = GetExtension(Runtime);
                var generatedFiles = Directory.GetFiles(HelperDirectoryName, "*." + extension);
                var compiliedFiles = new StringBuilder();
                foreach (var file in generatedFiles)
                {
                    compiliedFiles.Append('"' + Path.GetFileName(file) + "\" ");
                }

                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    templateName = "Program.cs";
                    compiliedFiles.Append('"' + templateName + '"');
                    if (_grammar.CaseInsensitive)
                    {
                        compiliedFiles.Append(" \"..\\" + Path.Combine("Runtimes", Runtime.ToString(), "AntlrCaseInsensitiveInputStream.cs"));
                    }
                    compilatorPath = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "csc.exe");
                    arguments = $@"/reference:""..\{runtimeLibraryPath}"" /out:{Runtime}_{state.GrammarCheckedState.Grammar.Name}Parser.exe " + compiliedFiles;
                }
                else if (Runtime == Runtime.Java)
                {
                    templateName = "Main.java";
                    compiliedFiles.Append('"' + templateName + '"');
                    compilatorPath = @"C:\Program Files\Java\jdk1.8.0_66\bin\javac.exe";
                    arguments = $@"-cp ""..\{runtimeLibraryPath}"" " + compiliedFiles.ToString();
                }

                var templateFile = Path.Combine(HelperDirectoryName, templateName);
                var code = File.ReadAllText(Path.Combine("Runtimes", Runtime.ToString(), templateName));
                code = code.Replace(TemplateGrammarName, state.GrammarCheckedState.Grammar.Name);
                code = code.Replace(TemplateGrammarRoot, Grammar.Root);
                if (_grammar.CaseInsensitive)
                {
                    code = code.Replace("AntlrInputStream", "AntlrCaseInsensitiveInputStream");
                }
                File.WriteAllText(templateFile, code);

                process = SetupAndStartProcess(compilatorPath, arguments, workingDirectory, ParserCompilation_ErrorDataReceived, ParserCompilation_OutputDataReceived);

                while (!process.HasExited)
                {
                    Thread.Sleep(CompileParserProcessTimeout);
                    CancelOperationIfRequired(CompileParserCancelMessage);
                }

                result.Root = Grammar.Root;
                result.PreprocessorRoot = Grammar.PreprocessorRoot;

                CancelOperationIfRequired(CompileParserCancelMessage);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
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
                _textErrors.Clear();

                string runtimeLibraryPath = Path.Combine("Runtimes", Runtime.ToString(), GetLibraryName(Runtime));
                string parserFileName = "";
                string arguments = "";
                string workingDirectory = HelperDirectoryName;
                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    var antlrRuntimeDir = Path.Combine(HelperDirectoryName, GetLibraryName(Runtime));
                    //if (!File.Exists(antlrRuntimeDir))
                    {
                        File.Copy(runtimeLibraryPath, antlrRuntimeDir, true);
                    }
                    parserFileName = Path.Combine(HelperDirectoryName, $"{Runtime}_{state.ParserGeneratedState.GrammarCheckedState.Grammar.Name}Parser.exe");
                    arguments = $"{Root} \"\" {(EndStage == WorkflowStage.TextParsed ? false : true)}";
                }
                else if (Runtime == Runtime.Java)
                {
                    parserFileName = "java";
                    arguments = $@"-cp ""..\{runtimeLibraryPath}"";. " + "Main " + TextFileName;
                }

                process = SetupAndStartProcess(parserFileName, arguments, workingDirectory, TextParsing_ErrorDataReceived, TextParsing_OutputDataReceived);

                while (!process.HasExited)
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

        private void ParserGeneration_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var strs = e.Data.Split(':');
                if (strs.Length >= 4)
                {
                    _parserGenerationErrors.Add(new ParsingError(int.Parse(strs[2]), int.Parse(strs[3]), e.Data));
                }
                else
                {
                    _parserCompilationErrors.Add(new ParsingError(0, 0, e.Data));
                }
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {

            }
        }

        private void ParserCompilation_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (Runtime == Runtime.Java && e.Data.Contains(": error:"))
                {
                    _parserCompilationErrors.Add(new ParsingError(0, 0, e.Data));
                }
            }
        }

        private void ParserCompilation_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if ((Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp) && e.Data.Contains(": error CS"))
                {
                    var errorString = FixEncoding(e.Data);
                    try
                    {
                        var words = errorString.Split(' ');
                        var strs = words[1].Split(':');
                        _parserCompilationErrors.Add(new ParsingError(int.Parse(strs[0]), int.Parse(strs[1]), errorString));
                    }
                    catch
                    {
                        _parserCompilationErrors.Add(new ParsingError(0, 0, errorString));
                    }
                }
            }
        }

        private void TextParsing_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var errorString = FixEncoding(e.Data);
                try
                {
                    var words = errorString.Split(' ');
                    var strs = words[1].Split(':');
                    _textErrors.Add(new ParsingError(int.Parse(strs[0]), int.Parse(strs[1]), errorString));
                }
                catch
                {
                    _textErrors.Add(new ParsingError(0, 0, errorString));
                }
            }
        }

        private void TextParsing_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                try
                {
                    var strs = e.Data.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    var outputState = (TextParsedOutput)Enum.Parse(typeof(TextParsedOutput), strs[0]);
                    var data = strs[1];
                    switch (outputState)
                    {
                        case TextParsedOutput.LexerTime:
                            _outputLexerTime = TimeSpan.Parse(data);
                            break;
                        case TextParsedOutput.ParserTime:
                            _outputParserTime = TimeSpan.Parse(data);
                            break;
                        case TextParsedOutput.Tokens:
                            _outputTokens = data;
                            break;
                        case TextParsedOutput.Tree:
                            _outputTree = data;
                            break;
                    }
                }
                catch
                {
                }
            }
        }

        private string FixEncoding(string str)
        {
            string result = str;
            var bytes = Encoding.Default.GetBytes(result);
            using (var stream = new MemoryStream(bytes))
            {
                Ude.CharsetDetector charsetDetector = new Ude.CharsetDetector();
                charsetDetector.Feed(stream);
                charsetDetector.DataEnd();
                if (charsetDetector.Charset != null)
                {
                    var detectedEncoding = Encoding.GetEncoding(charsetDetector.Charset);
                    result = detectedEncoding.GetString(bytes);
                }
            }
            return result;
        }

        private static string GetAntlrGenerator(Runtime runtime)
        {
            if (runtime == Runtime.CSharpSharwell)
            {
                return "antlr4-csharp-4.5.3-complete.jar";
            }
            else
            {
                return "antlr-4.5.3-complete.jar";
            }
        }

        private static string GetLanguage(Runtime runtime)
        {
            if (runtime == Runtime.CSharpSharwell)
            {
                return "CSharp_v4_5";
            }
            else
            {
                return runtime.ToString();
            }
        }

        private static string GetLibraryName(Runtime runtime)
        {
            switch (runtime)
            {
                case Runtime.CSharp:
                    return "Antlr4.Runtime.dll";
                case Runtime.CSharpSharwell:
                    return "Antlr4.Runtime.dll";
                case Runtime.Java:
                    return "antlr-runtime-4.5.3.jar";
                /*case Runtime.Python2:
                case Runtime.Python3:
                case Runtime.JavaScript:*/
                default:
                    throw new NotImplementedException();
            }
        }
        
        private static string GetExtension(Runtime runtime)
        {
            switch (runtime)
            {
                case Runtime.CSharp:
                case Runtime.CSharpSharwell:
                    return "cs";
                case Runtime.Java:
                    return "java";
                /*case Runtime.Python2:
                case Runtime.Python3:
                    return "py";
                case Runtime.JavaScript:
                    return "js";*/
                default:
                    throw new NotImplementedException();
            }
        }

        private void CancelOperationIfRequired(string message)
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException(message, _cancellationTokenSource.Token);
            }
        }

        private Process SetupAndStartProcess(string fileName, string arguments, string workingDirectory,
            DataReceivedEventHandler errorDataReceived, DataReceivedEventHandler outputDataReceived)
        {
            var process = new Process();
            var startInfo = process.StartInfo;
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            if (workingDirectory != null)
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            process.ErrorDataReceived += errorDataReceived;
            process.OutputDataReceived += outputDataReceived;
            process.EnableRaisingEvents = true;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
    }
}
