using System;

namespace AntlrGrammarEditor
{
    public class ParsingError
    {
        public TextSpan TextSpan { get; set; }

        public string Message { get; set; }

        public string FileName { get; set; }

        public ParsingError()
        {
        }

        public ParsingError(Exception ex)
        {
            TextSpan = TextSpan.Empty;
            Message = ex.ToString();
        }

        public ParsingError(TextSpan textSpan, string message, string fileName)
        {
            TextSpan = textSpan;
            Message = message;
            FileName = fileName;
        }

        public ParsingError(int line, int column, string message, string fileName, string fileData)
            : this(line, column, message, fileName)
        {
            RecalculatePosition(fileData);
        }

        public ParsingError(int line, int column, string message, string fileName)
        {
            TextSpan = new TextSpan(line, column, line, column + 1);
            Message = message;
            FileName = fileName;
        }

        public void RecalculatePosition(string fileData)
        {
            TextSpan.Start = TextHelpers.LineColumnToLinear(fileData, TextSpan.BeginLine, TextSpan.BeginChar);
            TextSpan.Length = 1;
        }

        public WorkflowStage WorkflowStage { get; set; } = WorkflowStage.GrammarChecked;

        public override bool Equals(object obj)
        {
            var parsingError = obj as ParsingError;
            if (parsingError != null)
            {
                return TextSpan.Equals(parsingError.TextSpan) &&
                       Message == parsingError.Message &&
                       FileName == parsingError.FileName;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
