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
        protected List<ParsingError> _errors = new List<ParsingError>();

        public string CurrentFileName { get; set; }

        public List<ParsingError> Errors => _errors;

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(CurrentFileName, line, charPositionInLine, msg);
            var lexerError = new ParsingError(line, charPositionInLine, message);
            _errors.Add(lexerError);
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(CurrentFileName, line, charPositionInLine, msg);
            var parserError = new ParsingError(line, charPositionInLine, message);
            _errors.Add(parserError);
        }

        private static string FormatErrorMessage(string fileName, int line, int charPositionInLine, string msg)
        {
            return $"error: {Path.GetFileName(fileName)}:{line}:{charPositionInLine}: {msg}";
        }
    }
}
