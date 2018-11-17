using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class ParserGeneratedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public override bool HasErrors => base.HasErrors || Errors.Count != 0;

        public override WorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();
    }
}
