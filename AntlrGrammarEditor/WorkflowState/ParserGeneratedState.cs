using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class ParserGeneratedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public bool HasErrors => Exception != null || Errors.Count > 0;

        public IWorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; }

        public Exception Exception { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public ParserGeneratedState(GrammarCheckedState grammarCheckedState)
        {
            GrammarCheckedState = grammarCheckedState ?? throw new ArgumentNullException(nameof(grammarCheckedState));
        }
    }
}
