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

        private string _outputTree;
        private string _outputTokens;
        private TimeSpan _outputLexerTime;
        private TimeSpan _outputParserTime;
        private event EventHandler<ParsingError> _newErrorEvent;
        
        private bool _indentedTree;

        private CancellationTokenSource _cancellationTokenSource;
        private InputState _inputState = new InputState();
        private object _lockObj = new object();

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
            get => Grammar.MainRuntime;
            set
            {
                if (Grammar.MainRuntime!= value)
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
            add => _newErrorEvent += value;
            remove => _newErrorEvent -= value;
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
                    CurrentState = parserGenerator.Generate(_grammar, GrammarCheckedState, _newErrorEvent, _cancellationTokenSource.Token);
                    break;
                case WorkflowStage.ParserGenerated:
                    var parserCompiler = new ParserCompiler();
                    CurrentState = parserCompiler.Compile(_grammar, GrammarCheckedState, (ParserGeneratedState)CurrentState, _newErrorEvent, _cancellationTokenSource.Token);
                    break;
                case WorkflowStage.ParserCompilied:
                    var textParser = new TextParser
                    {
                        OnlyTokenize = EndStage < WorkflowStage.TextParsed,
                        IndentedTree = IndentedTree
                    };
                    TextParsedState parsedState = textParser.Parse(_grammar, Root, (ParserCompiliedState)CurrentState, Text, _newErrorEvent, _cancellationTokenSource.Token);
                    OutputLexerTime = parsedState.LexerTime;
                    OutputParserTime = parsedState.ParserTime;
                    OutputTokens = parsedState.Tokens;
                    OutputTree = parsedState.Tree;
                    CurrentState = parsedState;
                    break;
            }
        }

        private void ClearErrors(WorkflowStage stage)
        {
            ClearErrorsEvent?.Invoke(this, stage);
        }
    }
}
