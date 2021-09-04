using System;
using System.Linq;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.WorkflowState
{
    public class TextParsedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.TextParsed;

        public override WorkflowState PreviousState => ParserCompiledState;

        public Source? TextSource { get; }

        public string? Root { get; set; }

        public ParserCompiledState ParserCompiledState { get; }

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string? Tokens { get; set; }

        public string? Tree { get; set; }

        public string RootOrDefault =>
            Root ?? (ParserCompiledState.ParserGeneratedState.GrammarCheckedState.Rules.FirstOrDefault() ?? "");

        public TextParsedState(ParserCompiledState parserCompiledState, Source? textSource)
        {
            ParserCompiledState = parserCompiledState;
            TextSource = textSource;
        }
    }
}
