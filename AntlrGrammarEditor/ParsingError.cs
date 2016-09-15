using System;

namespace AntlrGrammarEditor
{
    public class ParsingError
    {
        public TextSpan TextSpan { get; set; }

        public string Message { get; set; }

        public string FileName { get; set; }

        public WorkflowStage WorkflowStage { get; set; } = WorkflowStage.GrammarChecked;

        public ParsingError()
        {
        }

        public ParsingError(Exception ex, WorkflowStage stage)
        {
            TextSpan = TextSpan.Empty;
            Message = ex.ToString();
            WorkflowStage = stage;
        }

        public ParsingError(TextSpan textSpan, string message, string fileName, WorkflowStage stage)
        {
            TextSpan = textSpan;
            Message = message;
            FileName = fileName;
            WorkflowStage = stage;
        }

        public ParsingError(int line, int column, string message, string fileName, string fileData, WorkflowStage stage)
            : this(line, column, message, fileName, stage)
        {
            RecalculatePosition(fileData);
        }

        public ParsingError(int line, int column, string message, string fileName, WorkflowStage stage)
        {
            TextSpan = new TextSpan(line, column, line, column + 1);
            Message = message;
            FileName = fileName;
            WorkflowStage = stage;
        }

        public void RecalculatePosition(string fileData)
        {
            TextSpan.Start = TextHelpers.LineColumnToLinear(fileData, TextSpan.BeginLine, TextSpan.BeginChar);
            TextSpan.Length = 1;
        }

        public override bool Equals(object obj)
        {
            var parsingError = obj as ParsingError;
            if (parsingError != null)
            {
                return TextSpan.Equals(parsingError.TextSpan) &&
                       Message == parsingError.Message &&
                       FileName == parsingError.FileName &&
                       WorkflowStage == parsingError.WorkflowStage;
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
