using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class TextParsedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.TextParsed;

        public override bool HasErrors => base.HasErrors || Errors == null || Errors.Count != 0;

        public override WorkflowState PreviousState => ParserCompiliedState;

        public string Text { get; set; }

        public ParserCompiliedState ParserCompiliedState { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string Tokens { get; set; }

        public string Tree { get; set; }
    }
}
