using System;
using System.Threading;
using System.Threading.Tasks;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class Workflow
    {
        private Grammar _grammar;
        private Runtime? _runtime;
        private string _root;
        private PredictionMode? _predictionMode = AntlrGrammarEditor.Processors.PredictionMode.LL;
        private string _textFileName = "";
        private string _packageName;
        private string _generatorTool;

        private IWorkflowState _currentState;

        private CancellationTokenSource _cancellationTokenSource;
        private object _lockObj = new object();

        public bool? GenerateListener { get; set; }

        public bool? GenerateVisitor { get; set; }

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public IWorkflowState CurrentState => _currentState;

        public Grammar Grammar
        {
            get => _grammar;
            set
            {
                RollbackToStage(WorkflowStage.Input);
                _grammar = value ?? throw new ArgumentException(nameof(Grammar));
                _currentState = new InputState(_grammar);
            }
        }

        public Runtime? Runtime
        {
            get => _runtime;
            set
            {
                if (_runtime != value)
                {
                    StopIfRequired();
                    _runtime = value;
                    RollbackToStage(WorkflowStage.Input);
                }
            }
        }

        public Runtime DetectedRuntime { get; private set; }

        public string Root
        {
            get => _root;
            set
            {
                if (_root != value)
                {
                    StopIfRequired();
                    _root = value;
                    RollbackToStage(WorkflowStage.ParserCompiled);
                }
            }
        }

        public PredictionMode? PredictionMode
        {
            get => _predictionMode;
            set
            {
                if (_predictionMode != value)
                {
                    StopIfRequired();
                    _predictionMode = value;
                    RollbackToStage(WorkflowStage.ParserCompiled);
                }
            }
        }

        public string TextFileName
        {
            get => _textFileName;
            set
            {
                if (_textFileName != value)
                {
                    StopIfRequired();
                    _textFileName = value;
                    RollbackToStage(WorkflowStage.ParserCompiled);
                }
            }
        }

        public string GeneratorTool
        {
            get => _generatorTool;
            set
            {
                if (_generatorTool != value)
                {
                    StopIfRequired();
                    _generatorTool = value;
                    RollbackToStage(WorkflowStage.Input);
                }
            }
        }

        public string RuntimeLibrary { get; set; }

        public string PackageName
        {
            get => _packageName;
            set
            {
                if (_packageName != value)
                {
                    StopIfRequired();
                    _packageName = value;
                    RollbackToStage(WorkflowStage.Input);
                }
            }
        }

        public event EventHandler<IWorkflowState> StateChanged;

        public event EventHandler<WorkflowStage> ClearErrorsEvent;

        public event EventHandler<ParsingError> ErrorEvent;

        public event EventHandler<(TextParsedOutput, object)> TextParsedOutputEvent;

        public event EventHandler<Runtime> DetectedRuntimeEvent;

        public Workflow(Grammar grammar)
        {
            Grammar = grammar;
        }

        public Task<IWorkflowState> ProcessAsync()
        {
            StopIfRequired();

            Func<IWorkflowState> func = Process;
            return Task.Run(func);
        }

        public IWorkflowState Process()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            while (!_currentState.HasErrors && _currentState.Stage < WorkflowStage.TextParsed && _currentState.Stage < EndStage)
            {
                ProcessOneStep();
            }

            _cancellationTokenSource = null;
            return _currentState;
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
            if (_currentState.HasErrors)
            {
                ClearErrorsEvent?.Invoke(this, _currentState.Stage);
                _currentState = _currentState.PreviousState;
            }
        }

        public void RollbackToStage(WorkflowStage stage)
        {
            while (_currentState?.Stage > stage)
            {
                switch (_currentState.Stage)
                {
                    case WorkflowStage.TextParsed:
                        TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tokens, ""));
                        TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tree, ""));
                        ClearErrorsEvent?.Invoke(this, WorkflowStage.TextTokenized);
                        ClearErrorsEvent?.Invoke(this, WorkflowStage.TextParsed);
                        break;

                    case WorkflowStage.ParserCompiled:
                    case WorkflowStage.ParserGenerated:
                    case WorkflowStage.GrammarChecked:
                        ClearErrorsEvent?.Invoke(this, _currentState.Stage);
                        break;
                }

                _currentState = _currentState.PreviousState;
            }

            if (StateChanged != null && _currentState != null)
            {
                StateChanged.Invoke(this, _currentState);
            }
        }

        private void ProcessOneStep()
        {
            GrammarCheckedState grammarCheckedState;

            switch (_currentState.Stage)
            {
                case WorkflowStage.Input:
                    var grammarChecker = new GrammarChecker {ErrorEvent = ErrorEvent};
                    _currentState = grammarChecker.Check((InputState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.GrammarChecked:
                    grammarCheckedState = (GrammarCheckedState) _currentState;

                    DetectedRuntime = Runtime ?? grammarCheckedState.Runtime ?? AntlrGrammarEditor.Runtime.Java;
                    DetectedRuntimeEvent?.Invoke(this, DetectedRuntime);

                    var parserGenerator = new ParserGenerator(DetectedRuntime)
                    {
                        ErrorEvent = ErrorEvent,
                        GeneratorTool = GeneratorTool,
                        PackageName = !string.IsNullOrWhiteSpace(PackageName) ? PackageName : grammarCheckedState.Package,
                        GenerateListener = GenerateListener ?? grammarCheckedState.Listener ?? false,
                        GenerateVisitor = GenerateVisitor ?? grammarCheckedState.Visitor ?? false
                    };
                    _currentState = parserGenerator.Generate(grammarCheckedState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserGenerated:
                    var parserCompiler = new ParserCompiler
                    {
                        ErrorEvent = ErrorEvent,
                        RuntimeLibrary = RuntimeLibrary
                    };
                    _currentState = parserCompiler.Compile((ParserGeneratedState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserCompiled:
                    grammarCheckedState = (GrammarCheckedState) _currentState.PreviousState.PreviousState;

                    var textParser = new TextParser(TextFileName)
                    {
                        OnlyTokenize = EndStage < WorkflowStage.TextParsed,
                        RuntimeLibrary = RuntimeLibrary,
                        ErrorEvent = ErrorEvent,
                        Root = !string.IsNullOrWhiteSpace(Root) ? Root : grammarCheckedState.Root,
                        PredictionMode = PredictionMode ?? grammarCheckedState.PredictionMode ?? Processors.PredictionMode.LL
                    };
                    textParser.TextParsedOutputEvent += TextParsedOutputEvent;
                    _currentState = textParser.Parse((ParserCompiledState)_currentState, _cancellationTokenSource.Token);
                    break;
            }

            StateChanged?.Invoke(this, _currentState);
        }
    }
}
