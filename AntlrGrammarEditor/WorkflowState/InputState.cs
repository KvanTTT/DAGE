using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class InputState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.Input;

        public bool HasErrors => Exception != null;

        public IWorkflowState PreviousState => null;

        public Exception Exception { get; set; }

        public Grammar Grammar { get; }

        public string Command { get; set; }

        public InputState(Grammar grammar)
        {
            Grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        }
    }
}
