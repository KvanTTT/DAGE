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
        private static string GeneratedDirectoryName = "AntlrGrammarEditorGenerated";
        private static string ParserDirectoryName = "AntlrGrammarEditorParser";
        private const string TextFileName = "Text.txt";

        private const string TemplateGrammarName = "AntlrGrammarName42";
        private const string TemplateGrammarRoot = "AntlrGrammarRoot42";

        private const int GenerateParserProcessTimeout = 200;
        private const int CompileParserProcessTimeout = 200;
        private const int ParseTextTimeout = 200;

        private string CheckGrammarCancelMessage = "Grammar checking has been cancelled.";
        private string GenerateParserCancelMessage = "Parser generation has been cancelled.";
        private string CompileParserCancelMessage = "Parser compilation has been cancelled.";
        private string ParseTextCancelMessage = "Text parsing has been cancelled.";

        private string _grammar = "";
        private Runtime _runtime;
        private string _root = "";
        private string _text = "";
        private List<ParsingError> _parserGenerationErrors;
        private List<ParsingError> _parserCompilationErrors;
        private List<ParsingError> _textErrors;
        private string _stringTree;

        private CancellationTokenSource _cancellationTokenSource;
        private object _lock = new object();

        public bool AutoProcessing { get; set; }

        public GrammarCheckedState GrammarCheckedState { get; private set; }
        public WorkflowState CurrentState { get; private set; } = new InputState();

        public string Grammar
        {
            get
            {
                return _grammar;
            }
            set
            {
                _grammar = value;
                RollbackToStageAndProcessIfRequired(WorkflowStage.Input);
            }
        }

        public Runtime Runtime
        {
            get
            {
                return _runtime;
            }
            set
            {
                _runtime = value;
                RollbackToStageAndProcessIfRequired(WorkflowStage.GrammarChecked);
            }
        }

        public string Root
        {
            get
            {
                return _root;
            }
            set
            {
                _root = value;
                RollbackToStageAndProcessIfRequired(WorkflowStage.ParserGenerated);
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
                _text = value;
                RollbackToStageAndProcessIfRequired(WorkflowStage.ParserCompilied);
            }
        }

        static Workflow()
        {
            var tempDir = Path.GetTempPath();
            //GeneratedDirectoryName = Path.Combine(tempDir, GeneratedDirectoryName);
            //ParserDirectoryName = Path.Combine(tempDir, ParserDirectoryName);
        }

        public Workflow()
        {
        }

        public WorkflowState Process()
        {
            if (_cancellationTokenSource != null)
            {
                lock (_lock)
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
            _cancellationTokenSource = new CancellationTokenSource();

            while (!CurrentState.HasErrors && CurrentState.Stage < WorkflowStage.TextParsed)
            {
                ProcessOneStep();
            }

            _cancellationTokenSource = null;
            return CurrentState;
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

        protected void RollbackToStageAndProcessIfRequired(WorkflowStage stage)
        {
            RollbackToStage(stage);
            if (AutoProcessing)
            {
                Process();
            }
        }

        protected GrammarCheckedState CheckGrammar()
        {
            var errorListener = new AntlrErrorListener();

            var result = new GrammarCheckedState
            {
                Grammar = Grammar
            };
            try
            {
                var inputStream = new AntlrInputStream(Grammar);
                var antlr4Lexer = new ANTLRv4Lexer(inputStream);
                antlr4Lexer.RemoveErrorListeners();
                antlr4Lexer.AddErrorListener(errorListener);
                var codeTokenSource = new ListTokenSource(antlr4Lexer.GetAllTokens());

                CancelOperationIfRequired(CheckGrammarCancelMessage);

                var codeTokenStream = new CommonTokenStream(codeTokenSource);
                var antlr4Parser = new ANTLRv4Parser(codeTokenStream);
                antlr4Parser.RemoveErrorListeners();
                antlr4Parser.AddErrorListener(errorListener);

                var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
                var tree = antlr4Parser.grammarSpec();
                grammarInfoCollectorListener.CollectInfo(tree);

                result.GrammarName = grammarInfoCollectorListener.GrammarName;
                result.Rules = grammarInfoCollectorListener.Rules;
                result.Errors = errorListener.Errors;
                if (string.IsNullOrEmpty(Root) && result.Rules.Count > 0)
                {
                    Root = result.Rules.First();
                }

                CancelOperationIfRequired(CheckGrammarCancelMessage);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            GrammarCheckedState = result;
            return result;
        }

        protected ParserGeneratedState GenerateParser(GrammarCheckedState state)
        {
            ParserGeneratedState result = new ParserGeneratedState { GrammarCheckedState = state };
            Process process = null;
            try
            {
                if (Directory.Exists(GeneratedDirectoryName))
                {
                    Directory.Delete(GeneratedDirectoryName, true);
                }
                Directory.CreateDirectory(GeneratedDirectoryName);
                var fileName = $"{state.GrammarName}.g4";
                File.WriteAllText(fileName, state.Grammar);

                CancelOperationIfRequired(GenerateParserCancelMessage);

                _parserGenerationErrors = new List<ParsingError>();
                
                var javaPath = "java";
                var arguments = $@"-jar ""{GetAntlrGenerator(Runtime)}"" ""{fileName}"" -o ""{GeneratedDirectoryName}"" " +
                    $"-Dlanguage={GetLanguage(Runtime)} -no-visitor -no-listener";

                process = SetupAndStartProcess(javaPath, arguments, null, ParserGeneration_ErrorDataReceived, ParserGeneration_OutputDataReceived);

                while (!process.HasExited)
                {
                    Thread.Sleep(GenerateParserProcessTimeout);
                    CancelOperationIfRequired(GenerateParserCancelMessage);
                }
                result.Errors = _parserGenerationErrors;

                CancelOperationIfRequired(GenerateParserCancelMessage);
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

        protected ParserCompiliedState CompileParser(ParserGeneratedState state)
        {
            ParserCompiliedState result = new ParserCompiliedState { ParserGeneratedState = state };
            Process process = null;
            try
            {
                if (Directory.Exists(ParserDirectoryName))
                {
                    Directory.Delete(ParserDirectoryName, true);
                }
                Directory.CreateDirectory(ParserDirectoryName);

                _parserCompilationErrors = new List<ParsingError>();
                string compilatorPath = "";
                string arguments = "";
                string templateName = "";
                string workingDirectory = ParserDirectoryName;
                string runtimeLibraryPath = Path.Combine(Runtime + "_Runtime", GetLibraryName(Runtime));

                string extension = GetExtension(Runtime);
                var generatedFiles = Directory.GetFiles(GeneratedDirectoryName, "*." + extension);
                var compiliedFiles = new StringBuilder();
                foreach (var file in generatedFiles)
                {
                    compiliedFiles.Append('"' + Path.GetFileName(file) + "\" ");
                    File.Copy(file, Path.Combine(ParserDirectoryName, Path.GetFileName(file)), true);
                }

                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    templateName = "Program.cs";
                    compiliedFiles.Append('"' + templateName + '"');
                    compilatorPath = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "csc.exe");
                    arguments = $@"/reference:""..\{runtimeLibraryPath}"" /out:{Runtime}_{state.GrammarCheckedState.GrammarName}Parser.exe " + compiliedFiles;
                }
                else if (Runtime == Runtime.Java)
                {
                    templateName = "Main.java";
                    compiliedFiles.Append('"' + templateName + '"');
                    compilatorPath = @"C:\Program Files\Java\jdk1.8.0_66\bin\javac.exe";
                    arguments = $@"-cp ""..\{runtimeLibraryPath}"" " + compiliedFiles.ToString();
                }

                var templateFile = Path.Combine(ParserDirectoryName, templateName);
                var code = File.ReadAllText(Path.Combine(Runtime + "_Runtime", templateName));
                code = code.Replace(TemplateGrammarName, state.GrammarCheckedState.GrammarName).Replace(TemplateGrammarRoot, Root);
                File.WriteAllText(templateFile, code);

                process = SetupAndStartProcess(compilatorPath, arguments, workingDirectory, ParserCompilation_ErrorDataReceived, ParserCompilation_OutputDataReceived);

                while (!process.HasExited)
                {
                    Thread.Sleep(CompileParserProcessTimeout);
                    CancelOperationIfRequired(CompileParserCancelMessage);
                }

                result.Root = Root;
                result.Errors = _parserCompilationErrors;

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

        protected TextParsedState ParseText(ParserCompiliedState state)
        {
            var result = new TextParsedState
            {
                ParserCompiliedState = state,
                Text = Text
            };
            Process process = null;
            try
            {
                File.WriteAllText(Path.Combine(ParserDirectoryName, TextFileName), result.Text);
                _textErrors = new List<ParsingError>();

                string runtimeLibraryPath = Path.Combine(Runtime + "_Runtime", GetLibraryName(Runtime));
                string parserFileName = "";
                string arguments = "";
                string workingDirectory = ParserDirectoryName;
                if (Runtime == Runtime.CSharpSharwell || Runtime == Runtime.CSharp)
                {
                    var antlrRuntimeDir = Path.Combine(ParserDirectoryName, GetLibraryName(Runtime));
                    //if (!File.Exists(antlrRuntimeDir))
                    {
                        File.Copy(runtimeLibraryPath, antlrRuntimeDir, true);
                    }
                    parserFileName = Path.Combine(ParserDirectoryName, $"{Runtime}_{state.ParserGeneratedState.GrammarCheckedState.GrammarName}Parser.exe");
                    arguments = TextFileName;
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

                result.StringTree = _stringTree;
                result.TextErrors = _textErrors;

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

        private void RollbackToStage(WorkflowStage stage)
        {
            while (CurrentState.Stage > stage && CurrentState.PreviousState != null)
            {
                CurrentState = CurrentState.PreviousState;
            }
        }

        private void ParserGeneration_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var strs = e.Data.Split(':');
                _parserGenerationErrors.Add(new ParsingError(int.Parse(strs[2]), int.Parse(strs[3]), e.Data));
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
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
                    _parserCompilationErrors.Add(new ParsingError(0, 0, e.Data));
                }
            }
        }

        private void TextParsing_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var words = e.Data.Split(' ');
                var strs = words[1].Split(':');
                _textErrors.Add(new ParsingError(int.Parse(strs[0]), int.Parse(strs[1]), e.Data));
            }
        }

        private void TextParsing_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _stringTree = e.Data;
            }
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
                case Runtime.Python2:
                case Runtime.Python3:
                case Runtime.JavaScript:
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
                case Runtime.Python2:
                case Runtime.Python3:
                    return "py";
                case Runtime.JavaScript:
                    return "js";
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
