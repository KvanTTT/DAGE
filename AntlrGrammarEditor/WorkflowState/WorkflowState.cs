using System;

namespace AntlrGrammarEditor
{
    public abstract class WorkflowState
    {
        public abstract WorkflowStage Stage { get; }

        public virtual bool HasErrors => Exception != null;

        public abstract WorkflowState PreviousState { get; }

        public Exception Exception { get; set; }
    }
}
