using System;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.WorkflowState
{
    public class TextParsedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.TextParsed;

        public override WorkflowState PreviousState => ParserCompiledState;

        public ParserCompiledState ParserCompiledState { get; }

        public Source? TextSource { get; }

        public string Root { get; }

        public PredictionMode PredictionMode { get; }

        public TimeSpan LexerTime { get; set; }

        public TimeSpan ParserTime { get; set; }

        public string? Tokens { get; set; }

        public string? Tree { get; set; }

        public TextParsedState(ParserCompiledState parserCompiledState,
            string root,
            PredictionMode predictionMode,
            Source? textSource)
        {
            ParserCompiledState = parserCompiledState;
            Root = root;
            PredictionMode = predictionMode;
            TextSource = textSource;
        }
    }
}
