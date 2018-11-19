﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public class ParserGeneratedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; }

        public bool IncludeListener { get; }

        public bool IncludeVisitor { get; }

        public Exception Exception { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public ParserGeneratedState(GrammarCheckedState grammarCheckedState, bool includeListener, bool includeVisitor)
        {
            GrammarCheckedState = grammarCheckedState ?? throw new ArgumentNullException(nameof(grammarCheckedState));
            IncludeListener = includeListener;
            IncludeVisitor = includeVisitor;
        }
    }
}
