﻿using System;
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
        private Grammar _grammar = new Grammar();
        private string _text = "";
        private bool _indentedTree;

        private IWorkflowState _currentState;

        private CancellationTokenSource _cancellationTokenSource;
        private object _lockObj = new object();

        private event EventHandler<ParsingError> _errorEvent;
        private event EventHandler<(TextParsedOutput, object)> _textParsedOutputEvent;

        public WorkflowStage EndStage { get; set; } = WorkflowStage.TextParsed;

        public event EventHandler<IWorkflowState> StateChanged;

        public event EventHandler<WorkflowStage> ClearErrorsEvent;

        public IWorkflowState CurrentState => _currentState;

        public Grammar Grammar
        {
            get => _grammar;
            set
            {
                StopIfRequired();
                RollbackToStage(WorkflowStage.Input);
                _grammar = value;
                _currentState = new InputState(_grammar);
            }
        }

        public Runtime Runtime
        {
            get => _grammar.MainRuntime;
            set
            {
                if (_grammar.MainRuntime!= value)
                {
                    StopIfRequired();
                    _grammar.Runtimes.Clear();
                    _grammar.Runtimes.Add(value);
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
                    var parserGenerator = new ParserGenerator {ErrorEvent = _errorEvent};
                    _currentState = parserGenerator.Generate((GrammarCheckedState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserGenerated:
                    var parserCompiler = new ParserCompiler {ErrorEvent = _errorEvent};
                    _currentState = parserCompiler.Compile((ParserGeneratedState)_currentState, _cancellationTokenSource.Token);
                    break;

                case WorkflowStage.ParserCompilied:
                    var textParser = new TextParser(Text)
                    {
                        Root = Root,
                        OnlyTokenize = EndStage < WorkflowStage.TextParsed,
                        IndentedTree = IndentedTree,
                        ErrorEvent = _errorEvent
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