using Antlr4.Runtime;
using System;
using System.IO;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor
{
    public class AntlrErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public event EventHandler<ParsingError> ErrorEvent;

        public CodeSource CodeSource { get; set; }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = Helpers.FormatErrorMessage(CodeSource, line, charPositionInLine, msg);
            var parserError = new ParsingError(line, charPositionInLine, message, CodeSource, WorkflowStage.GrammarChecked);

            ErrorEvent?.Invoke(this, parserError);
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = Helpers.FormatErrorMessage(CodeSource, line, charPositionInLine, msg);
            var textSpan = TextSpan.FromBounds(offendingSymbol.StartIndex, offendingSymbol.StopIndex + 1, CodeSource);
            var lexerError = new ParsingError(textSpan, message, WorkflowStage.GrammarChecked);

            ErrorEvent?.Invoke(this, lexerError);
        }
    }
}
