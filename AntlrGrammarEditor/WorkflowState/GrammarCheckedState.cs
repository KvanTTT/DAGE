using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class GrammarCheckedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.GrammarChecked;

        public override bool HasErrors => base.HasErrors || Errors.Count != 0;

        public override WorkflowState PreviousState => InputState;

        public InputState InputState { get; set; }

        public Grammar Grammar { get; set; }

        public Dictionary<string, CodeSource> GrammarFilesData { get; } = new Dictionary<string, CodeSource>();

        public Dictionary<string, List<CodeInsertion>> GrammarActionsTextSpan { get; } = new Dictionary<string, List<CodeInsertion>>();

        public List<string> Rules { get; set; }

        public List<string> PreprocessorRules { get; set; }

        public List<ParsingError> Errors { get; set; }

        public List<TextSpan> ActionTextSpans { get; set; }
    }
}
