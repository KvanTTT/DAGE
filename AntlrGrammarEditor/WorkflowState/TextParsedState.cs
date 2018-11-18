using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class TextParsedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.TextParsed;

        public bool HasErrors => Exception != null || Errors.Count > 0;

        public IWorkflowState PreviousState => ParserCompiliedState;

        public Exception Exception { get; set; }

        public string Text { get; }
        
        public string Root { get; set; }

        public ParserCompiliedState ParserCompiliedState { get; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string Tokens { get; set; }

        public string Tree { get; set; }

        public TextParsedState(ParserCompiliedState parserCompiliedState, string text)
        {
            ParserCompiliedState =
                parserCompiliedState ?? throw new ArgumentNullException(nameof(parserCompiliedState));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
