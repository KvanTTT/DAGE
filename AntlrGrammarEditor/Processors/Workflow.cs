using System;
using System.Threading;
using System.Threading.Tasks;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Processors.GrammarChecking;
using AntlrGrammarEditor.Processors.ParserCompilers;
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Processors.TextParsing;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class Workflow
    {
        private Grammar _grammar;
        private Runtime? _runtime;
        private string? _root;
        private CaseInsensitiveType? _caseInsensitiveType;
        private PredictionMode? _predictionMode;
        private string? _textFileName;
        private string? _packageName;
        private string? _generatorTool;

        private WorkflowState.WorkflowState _currentState;

        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObj = new();

        public bool? GenerateListener { get; set; }

        public bool? GenerateVisitor { get; set; }

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public WorkflowState.WorkflowState CurrentState => _currentState;

        public Grammar Grammar
        {
            get => _grammar;
            set
            {
                RollbackToStage(WorkflowStage.Input);
                _grammar = value;
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

        public CaseInsensitiveType? CaseInsensitiveType
        {
            get => _caseInsensitiveType;
            set
            {
                if (_caseInsensitiveType != value)
                {
                    StopIfRequired();
                    _caseInsensitiveType = value;
                    RollbackToStage(WorkflowStage.ParserGenerated);
                }
            }
        }

        public string? Root
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

        public string? TextFileName
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

        public string? GeneratorTool
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

        public string? RuntimeLibrary { get; set; }

        public string? PackageName
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

        public event EventHandler<WorkflowState.WorkflowState>? StateChanged;

        public event EventHandler<WorkflowStage>? ClearErrorsEvent;

        public event EventHandler<Diagnosis>? DiagnosisEvent;

        public event EventHandler<(TextParsedOutput, object)>? TextParsedOutputEvent;

        public event EventHandler<Runtime>? DetectedRuntimeEvent;

        public Workflow(Grammar grammar)
        {
            _grammar = grammar;
            _currentState = new InputState(_grammar);
        }

        public Task<WorkflowState.WorkflowState> ProcessAsync()
        {
            StopIfRequired();

            Func<WorkflowState.WorkflowState> func = Process;
            return Task.Run(func);
        }

        public GrammarCheckedState CheckGrammar()
        {
            if (_currentState.Stage == WorkflowStage.Input)
                ProcessOneStep();

            return _currentState.GetState<GrammarCheckedState>()!;
        }

        public WorkflowState.WorkflowState Process()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            while (!_currentState.HasErrors && _currentState.Stage < WorkflowStage.TextParsed &&
                   _currentState.Stage < EndStage)
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
            if (_currentState.HasErrors && _currentState.Stage > WorkflowStage.Input)
            {
                ClearErrorsEvent?.Invoke(this, _currentState.Stage);
                _currentState = _currentState.PreviousState!;
            }
        }

        public void RollbackToStage(WorkflowStage stage)
        {
            while (_currentState.Stage > stage)
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

                _currentState = _currentState.PreviousState!;
            }

            StateChanged?.Invoke(this, _currentState);
        }

        private void ProcessOneStep()
        {
            switch (_currentState)
            {
                case InputState inputState:
                    var grammarChecker = new GrammarChecker(inputState) {DiagnosisEvent = DiagnosisEvent};
                    _currentState = grammarChecker.Check(_cancellationTokenSource?.Token ?? default);
                    break;

                case GrammarCheckedState grammarCheckedState:
                    var parserGenerator = new ParserGenerator(
                        grammarCheckedState,
                        Runtime,
                        PackageName,
                        GenerateListener,
                        GenerateVisitor)
                    {
                        DiagnosisEvent = DiagnosisEvent,
                        GeneratorTool = GeneratorTool
                    };
                    DetectedRuntimeEvent?.Invoke(this, parserGenerator.Runtime);
                    _currentState = parserGenerator.Generate(_cancellationTokenSource?.Token ?? default);
                    break;

                case ParserGeneratedState parserGeneratedState:
                    var parserCompiler =
                        ParserCompilerFactory.Create(
                            parserGeneratedState,
                            CaseInsensitiveType,
                            RuntimeLibrary,
                            DiagnosisEvent);
                    _currentState = parserCompiler.Compile(_cancellationTokenSource?.Token ?? default);
                    break;

                case ParserCompiledState parserCompiledState:
                    var textParser = new TextParser(
                        parserCompiledState,
                        TextFileName,
                        Root,
                        PredictionMode)
                    {
                        OnlyTokenize = EndStage < WorkflowStage.TextParsed,
                        RuntimeLibrary = RuntimeLibrary,
                        DiagnosisEvent = DiagnosisEvent,
                    };
                    textParser.TextParsedOutputEvent += TextParsedOutputEvent;
                    _currentState = textParser.Parse(_cancellationTokenSource?.Token ?? default);
                    break;
            }

            StateChanged?.Invoke(this, _currentState);
        }
    }
}
