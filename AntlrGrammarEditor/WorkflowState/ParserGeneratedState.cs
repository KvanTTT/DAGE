using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class ParserGeneratedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public override bool HasErrors => base.HasErrors || Errors.Count != 0;

        public override WorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; set; }

        public IReadOnlyList<ParsingError> Errors { get; set; }
    }
}
