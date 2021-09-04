using Antlr4.Runtime;
using System;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
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
            int column = charPositionInLine + 1;
            var message = Helpers.FormatErrorMessage(Source, line, column, msg);
            var start = Source.LineColumnToPosition(line, column);
            var parserError = new Diagnosis(TextHelpers.ExtractTextSpan(start, msg, Source), message,
                WorkflowStage.GrammarChecked);
            ErrorEvent?.Invoke(this, parserError);
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = Helpers.FormatErrorMessage(Source, line, charPositionInLine + 1, msg);
            var textSpan = TextSpan.FromBounds(offendingSymbol.StartIndex, offendingSymbol.StopIndex + 1, Source);
            var lexerError = new Diagnosis(textSpan, message, WorkflowStage.GrammarChecked, DiagnosisType.Error);
            ErrorEvent?.Invoke(this, lexerError);
        }
    }
}
