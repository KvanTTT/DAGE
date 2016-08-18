using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class AntlrErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        protected List<ParsingError> _errors = new List<ParsingError>();

        public IReadOnlyList<ParsingError> Errors => _errors;

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var lexerError = new ParsingError(line, charPositionInLine, msg);
            _errors.Add(lexerError);
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var parserError = new ParsingError(line, charPositionInLine, msg);
            _errors.Add(parserError);
        }
    }
}
