using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class AntlrErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public List<ParsingError> Errors { get; private set; }

        public event EventHandler<ParsingError> NewErrorEvent;

        public CodeSource CodeSource { get; set; }

        public AntlrErrorListener(List<ParsingError> errorsList)
        {
            Errors = errorsList;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(line, charPositionInLine, msg);
            var lexerError = new ParsingError(line, charPositionInLine, message, CodeSource, WorkflowStage.GrammarChecked);
            Errors.Add(lexerError);
            NewErrorEvent?.Invoke(this, lexerError);
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(line, charPositionInLine, msg);
            var parserError = new ParsingError(line, charPositionInLine, message, CodeSource, WorkflowStage.GrammarChecked);
            Errors.Add(parserError);
            NewErrorEvent?.Invoke(this, parserError);
        }

        private string FormatErrorMessage(int line, int charPositionInLine, string msg)
        {
            return $"error: {Path.GetFileName(CodeSource.Name)}:{line}:{charPositionInLine}: {msg}";
        }
    }
}
