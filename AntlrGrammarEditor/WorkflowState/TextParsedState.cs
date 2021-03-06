﻿using System;
using System.Collections.Generic;
using System.Linq;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class TextParsedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.TextParsed;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => ParserCompiledState;

        public Exception Exception { get; set; }

        public CodeSource Text { get; }

        public string Root { get; set; }

        public ParserCompiledState ParserCompiledState { get; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string Tokens { get; set; }

        public string Tree { get; set; }

        public string Command { get; set; }

        public string RootOrDefault => string.IsNullOrEmpty(Root)
            ? ParserCompiledState.ParserGeneratedState.GrammarCheckedState.Rules.FirstOrDefault()
            : Root;

        public TextParsedState(ParserCompiledState parserCompiledState, CodeSource text)
        {
            ParserCompiledState =
                parserCompiledState ?? throw new ArgumentNullException(nameof(parserCompiledState));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
