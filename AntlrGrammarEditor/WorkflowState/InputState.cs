using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class InputState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.Input;

        public override WorkflowState? PreviousState => null;

        public Grammar Grammar { get; }

        public InputState(Grammar grammar)
        {
            Grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        }
    }
}
