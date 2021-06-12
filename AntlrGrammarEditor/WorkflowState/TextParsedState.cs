using System;
using System.Linq;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class TextParsedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.TextParsed;

        public override WorkflowState PreviousState => ParserCompiledState;

        public CodeSource Text { get; }

        public string Root { get; set; }

        public ParserCompiledState ParserCompiledState { get; }

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string Tokens { get; set; }

        public string Tree { get; set; }

        public string RootOrDefault => string.IsNullOrEmpty(Root)
            ? ParserCompiledState.ParserGeneratedState.GrammarCheckedState.Rules.FirstOrDefault()
            : Root;

        public TextParsedState(ParserCompiledState parserCompiledState, CodeSource text)
        {
            ParserCompiledState =
                parserCompiledState ?? throw new ArgumentNullException(nameof(parserCompiledState));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
