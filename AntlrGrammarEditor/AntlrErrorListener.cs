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

        public event EventHandler<Tuple<WorkflowStage, ParsingError>> NewErrorEvent;

        public string CurrentFileName { get; set; }

        public AntlrErrorListener(List<ParsingError> errorsList)
        {
            Errors = errorsList;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(CurrentFileName, line, charPositionInLine, msg);
            var lexerError = new ParsingError(line, charPositionInLine, message) { WorkflowStage = WorkflowStage.GrammarChecked };
            Errors.Add(lexerError);
            NewErrorEvent?.Invoke(this, new Tuple<WorkflowStage, ParsingError>(WorkflowStage.GrammarChecked, lexerError));
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var message = FormatErrorMessage(CurrentFileName, line, charPositionInLine, msg);
            var parserError = new ParsingError(line, charPositionInLine, message) { WorkflowStage = WorkflowStage.GrammarChecked };
            Errors.Add(parserError);
            NewErrorEvent?.Invoke(this, new Tuple<WorkflowStage, ParsingError>(WorkflowStage.GrammarChecked, parserError));
        }

        private static string FormatErrorMessage(string fileName, int line, int charPositionInLine, string msg)
        {
            return $"error: {Path.GetFileName(fileName)}:{line}:{charPositionInLine}: {msg}";
        }
    }
}
