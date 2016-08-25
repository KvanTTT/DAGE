using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class GrammarCheckedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.GrammarChecked;

        public override bool HasErrors => base.HasErrors || Errors.Count != 0;

        public override WorkflowState PreviousState => InputState;

        public InputState InputState { get; set; }

        public Grammar Grammar { get; set; }

        public List<string> Rules { get; set; }

        public List<string> PreprocessorRules { get; set; }

        public List<ParsingError> Errors { get; set; }

        public List<TextSpan> ActionTextSpans { get; set; }
    }
}
