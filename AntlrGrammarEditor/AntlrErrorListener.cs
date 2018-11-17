using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;

namespace AntlrGrammarEditor
{
    public class AntlrErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public List<ParsingError> Errors { get; private set; } = new List<ParsingError>();

        public event EventHandler<ParsingError> ErrorEvent;

        public CodeSource CodeSource { get; set; }

        public AntlrErrorListener()
        {
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(line, charPositionInLine, msg);
            var lexerError = new ParsingError(line, charPositionInLine, message, CodeSource, WorkflowStage.GrammarChecked);

            lock (Errors)
                Errors.Add(lexerError);

            ErrorEvent?.Invoke(this, lexerError);
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(line, charPositionInLine, msg);
            var parserError = new ParsingError(line, charPositionInLine, message, CodeSource, WorkflowStage.GrammarChecked);

            lock (Errors)
                Errors.Add(parserError);

            ErrorEvent?.Invoke(this, parserError);
        }

        private string FormatErrorMessage(int line, int charPositionInLine, string msg)
        {
            return $"error: {Path.GetFileName(CodeSource.Name)}:{line}:{charPositionInLine}: {msg}";
        }
    }
}
