using System;
using Antlr4.Runtime;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Processors.GrammarChecking
{
    public class AntlrErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public event EventHandler<Diagnosis>? ErrorEvent;

        public Source Source { get; }

        public AntlrErrorListener(Source source)
        {
            Source = source;
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
        {
            int column = charPositionInLine + LineColumnTextSpan.StartColumn;
            var start = Source.LineColumnToPosition(line, column);
            var parserError = new GrammarCheckingDiagnosis(AntlrHelper.ExtractTextSpanFromErrorMessage(start, msg, Source), msg);
            ErrorEvent?.Invoke(this, parserError);
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
        {
            var textSpan = TextSpan.FromBounds(offendingSymbol.StartIndex, offendingSymbol.StopIndex + 1, Source);
            ErrorEvent?.Invoke(this, new GrammarCheckingDiagnosis(textSpan, msg));
        }
    }
}
