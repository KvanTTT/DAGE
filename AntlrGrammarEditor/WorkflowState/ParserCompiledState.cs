using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserCompiledState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserCompiled;

        public override WorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; }

        public ParserCompiledState(ParserGeneratedState parserGeneratedState)
        {
            ParserGeneratedState =
                parserGeneratedState ?? throw new ArgumentNullException(nameof(parserGeneratedState));
        }
    }
}
