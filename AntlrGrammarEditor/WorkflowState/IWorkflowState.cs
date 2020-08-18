using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
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
