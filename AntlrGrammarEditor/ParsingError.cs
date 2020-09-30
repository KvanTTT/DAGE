using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor
{
    public class ParsingError
    {
        public TextSpan TextSpan { get; }

        public string Message { get; }

        public bool IsWarning { get; }

        public WorkflowStage WorkflowStage { get; }

        public ParsingError(Exception ex, WorkflowStage stage)
        {
            TextSpan = TextSpan.Empty;
            Message = ex.ToString();
            WorkflowStage = stage;
        }

        public ParsingError(string message, CodeSource codeSource, WorkflowStage stage, bool isWarning = false)
            : this(1, 1, message, codeSource, stage, isWarning)
        {
        }

        public ParsingError(int line, int column, string message, CodeSource codeSource, WorkflowStage stage, bool isWarning = false)
            : this(new LineColumnTextSpan(line, column, codeSource).GetTextSpan(), message, stage, isWarning)
        {
            Message = message;
            WorkflowStage = stage;
        }

        public ParsingError(TextSpan textSpan, string message, WorkflowStage stage, bool isWarning = false)
        {
            TextSpan = textSpan;
            Message = message;
            WorkflowStage = stage;
            IsWarning = isWarning;
        }

        public override bool Equals(object obj)
        {
            if (obj is ParsingError parsingError)
            {
                return TextSpan.Equals(parsingError.TextSpan) &&
                       Message == parsingError.Message &&
                       WorkflowStage == parsingError.WorkflowStage;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public override string ToString()
        {
            return WorkflowStage + ":" + Message;
        }
    }
}
