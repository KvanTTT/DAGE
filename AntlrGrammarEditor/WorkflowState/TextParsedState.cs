using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public class TextParsedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.TextParsed;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => ParserCompiliedState;

        public Exception Exception { get; set; }

        public CodeSource Text { get; }

        public string Root { get; set; }

        public ParserCompiliedState ParserCompiliedState { get; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string Tokens { get; set; }

        public string Tree { get; set; }

        public string Command { get; set; }

        public string RootOrDefault => string.IsNullOrEmpty(Root)
            ? ParserCompiliedState.ParserGeneratedState.GrammarCheckedState.Rules.FirstOrDefault()
            : Root;

        public TextParsedState(ParserCompiliedState parserCompiliedState, CodeSource text)
        {
            ParserCompiliedState =
                parserCompiliedState ?? throw new ArgumentNullException(nameof(parserCompiliedState));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
