using Antlr4.Runtime;
using AntlrGrammarEditor;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopAntlrGrammarEditor
{
    class GuiErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        ObservableCollection<ParsingError> parsingErrors;

        public int ErrorCount => parsingErrors.Count;

        public GuiErrorListener(ObservableCollection<ParsingError> errors)
        {
            parsingErrors = errors;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var lexerError = new ParsingError(line, charPositionInLine, msg);
            Dispatcher.UIThread.InvokeAsync(() => parsingErrors.Add(lexerError));
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var parsingError = new ParsingError(line, charPositionInLine, msg);
            Dispatcher.UIThread.InvokeAsync(() => parsingErrors.Add(parsingError));
        } 
    }
}
