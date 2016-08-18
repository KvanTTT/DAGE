using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class TextParsedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.TextParsed;

        public override bool HasErrors => base.HasErrors || TextErrors == null || TextErrors.Count != 0;

        public override WorkflowState PreviousState => ParserCompiliedState;

        public string Text { get; set; }

        public ParserCompiliedState ParserCompiliedState { get; set; }

        public IReadOnlyList<ParsingError> TextErrors { get; set; } = new ParsingError[0];

        public string StringTree { get; set; }
    }
}
