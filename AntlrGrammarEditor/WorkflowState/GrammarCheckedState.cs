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

        public string Grammar { get; set; }

        public string GrammarName { get; set; }

        public IReadOnlyList<string> Rules { get; set; }

        public IReadOnlyList<ParsingError> Errors { get; set; }

        public IList<TextSpan> ActionTextSpans { get; set; }
    }
}
