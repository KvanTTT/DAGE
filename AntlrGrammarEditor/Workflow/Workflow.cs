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
        public const string PythonHelperFileName = "AntlrPythonCompileTest.py";
        public const string JavaScriptHelperFileName = "AntlrJavaScriptTest.js";
        public const string TextFileName = "Text";
        public const string RuntimesDirName = "AntlrRuntimes";

        public const string TemplateGrammarName = "__TemplateGrammarName__";
        public const string TemplateGrammarRoot = "__TemplateGrammarRoot__";

        private Grammar _grammar = new Grammar();
        private string _text = "";
        private WorkflowState _currentState;

        private List<ParsingError> _textErrors = new List<ParsingError>();
        private string _outputTree;
        private string _outputTokens;
        private TimeSpan _outputLexerTime;
        private TimeSpan _outputParserTime;
        private event EventHandler<ParsingError> _newErrorEvent;
        
        private bool _indentedTree;

        private CancellationTokenSource _cancellationTokenSource;
        private InputState _inputState = new InputState();
        private object _lockObj = new object();
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
                _newErrorEvent += value;
            }
            remove
            {
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
                    var grammarChecker = new GrammarChecker();
                    GrammarCheckedState = grammarChecker.Check(_grammar, _inputState, _newErrorEvent, _cancellationTokenSource.Token);
                    CurrentState = GrammarCheckedState;
                    break;
                case WorkflowStage.GrammarChecked:
                    var parserGenerator = new ParserGenerator();
                    CurrentState = parserGenerator.GenerateParser(_grammar, GrammarCheckedState, _newErrorEvent, _cancellationTokenSource.Token);
                    break;
                case WorkflowStage.ParserGenerated:
                    var parserCompiler = new ParserCompiler();
                    CurrentState = parserCompiler.Compile(_grammar, GrammarCheckedState, (ParserGeneratedState)CurrentState, _newErrorEvent, _cancellationTokenSource.Token);
                    break;
                case WorkflowStage.ParserCompilied:
                    CurrentState = ParseText((ParserCompiliedState)CurrentState);
                    break;
            }
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
                string workingDirectory = Path.Combine(HelperDirectoryName, _grammar.Name, Runtime.ToString());
                string parseTextFileName = Path.Combine("..", "..", TextFileName);

                if (Runtime == Runtime.CSharpOptimized || Runtime == Runtime.CSharpStandard)
                {
                    bool parse = EndStage != WorkflowStage.TextParsed;
                    arguments = $"\"{Path.Combine("bin", "netcoreapp2.1", _grammar.Name + ".dll")}\" {Root} \"{parseTextFileName}\" {parse} {IndentedTree}";
                    parserFileName = "dotnet";
                }
                else if (Runtime == Runtime.Java)
                {
                    string relativeRuntimeLibraryPath = "\"" + Path.Combine("..", "..", "..", runtimeLibraryPath) + "\"";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        relativeRuntimeLibraryPath += ";.";
                    }
                    else
                    {
                        relativeRuntimeLibraryPath = ".:" + relativeRuntimeLibraryPath;
                    }
                    arguments = $@"-cp {relativeRuntimeLibraryPath} Main ""{parseTextFileName}""";
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

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
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
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
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
    }
}
