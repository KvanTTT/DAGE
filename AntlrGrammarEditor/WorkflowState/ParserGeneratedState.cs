﻿using System;
using System.Collections.Generic;
using System.Linq;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserGeneratedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; }

        public Runtime Runtime { get; }

        public string PackageName { get; }

        public bool IncludeListener { get; }

        public bool IncludeVisitor { get; }

        public Exception Exception { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public string Command { get; set; }

        public ParserGeneratedState(GrammarCheckedState grammarCheckedState, string packageName, Runtime runtime, bool includeListener, bool includeVisitor)
        {
            GrammarCheckedState = grammarCheckedState ?? throw new ArgumentNullException(nameof(grammarCheckedState));
            PackageName = packageName;
            Runtime = runtime;
            IncludeListener = includeListener;
            IncludeVisitor = includeVisitor;
        }
    }
}
