using System;

namespace AntlrGrammarEditor
{
    public class InputState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.Input;

        public bool HasErrors => Exception != null;

        public IWorkflowState PreviousState => null;

        public Exception Exception { get; set; }

        public Grammar Grammar { get; }

        public InputState(Grammar grammar)
        {
            Grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        }
    }
}
