using System;

namespace AntlrGrammarEditor
{
    public interface IWorkflowState
    {
        WorkflowStage Stage { get; }

        bool HasErrors { get; }

        IWorkflowState PreviousState { get; }

        Exception Exception { get; set; }

        string Command { get; set; }
    }
}
