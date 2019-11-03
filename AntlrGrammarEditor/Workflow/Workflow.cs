using System;
using System.Threading;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class Workflow
    {
        private Grammar _grammar;
        private Runtime _runtime = Runtime.Java;
        private string _root = "";
        private string _textFileName = "";
        private string _packageName;
        private string _generatorTool;

        private IWorkflowState _currentState;

        private CancellationTokenSource _cancellationTokenSource;
        private object _lockObj = new object();

        private event EventHandler<ParsingError> _errorEvent;
        private event EventHandler<(TextParsedOutput, object)> _textParsedOutputEvent;

        public bool GenerateListener { get; set; }

        public bool GenerateVisitor { get; set; }

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public event EventHandler<IWorkflowState> StateChanged;

        public event EventHandler<WorkflowStage> ClearErrorsEvent;

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

        public Runtime Runtime
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

        public string Root
        {
            get => _root;
            set
            {
                if (_root != value)
                {
                    StopIfRequired();
                    _root = value;
                    RollbackToStage(WorkflowStage.ParserCompilied);
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
                    RollbackToStage(WorkflowStage.ParserCompilied);
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

        public event EventHandler<ParsingError> ErrorEvent
        {
            add => _errorEvent += value;
            remove => _errorEvent -= value;
        }

        public event EventHandler<(TextParsedOutput, object)> TextParsedOutputEvent
        {
            add => _textParsedOutputEvent += value;
            remove => _textParsedOutputEvent -= value;
        }

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
                        _textParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tokens, ""));
                        _textParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tree, ""));
                        ClearErrorsEvent?.Invoke(this, WorkflowStage.TextTokenized);
                        ClearErrorsEvent?.Invoke(this, WorkflowStage.TextParsed);
                        break;

                    case WorkflowStage.ParserCompilied:
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
            switch (_currentState.Stage)
            {
                case WorkflowStage.Input:
                    var grammarChecker = new GrammarChecker {ErrorEvent = _errorEvent};
                    _currentState = grammarChecker.Check((InputState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.GrammarChecked:
                    var parserGenerator = new ParserGenerator(Runtime)
                    {
                        ErrorEvent = _errorEvent,
                        GeneratorTool = GeneratorTool,
                        PackageName = PackageName,
                        GenerateListener = GenerateListener,
                        GenerateVisitor = GenerateVisitor
                    };
                    _currentState = parserGenerator.Generate((GrammarCheckedState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserGenerated:
                    var parserCompiler = new ParserCompiler
                    {
                        ErrorEvent = _errorEvent,
                        RuntimeLibrary = RuntimeLibrary
                    };
                    _currentState = parserCompiler.Compile((ParserGeneratedState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserCompilied:
                    var textParser = new TextParser(TextFileName)
                    {
                        OnlyTokenize = EndStage < WorkflowStage.TextParsed,
                        RuntimeLibrary = RuntimeLibrary,
                        ErrorEvent = _errorEvent,
                        Root = Root
                    };
                    textParser.TextParsedOutputEvent += _textParsedOutputEvent;
                    var textParsedState = textParser.Parse((ParserCompiliedState)_currentState, _cancellationTokenSource.Token);
                    _currentState = textParsedState;
                    break;
            }

            StateChanged?.Invoke(this, _currentState);
        }
    }
}
