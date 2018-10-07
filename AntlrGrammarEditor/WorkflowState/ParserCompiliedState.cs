using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class ParserCompiliedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserCompilied;

        public override bool HasErrors => base.HasErrors || Errors.Count != 0;

        public override WorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; set; }

        public string Root { get; set; }

        public string PreprocessorRoot { get; set; }

        public IReadOnlyList<ParsingError> Errors { get; set; } = new ParsingError[0];
    }
}
