using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor
{
    public class Diagnosis
    {
        public TextSpan TextSpan { get; }

        public string Message { get; }

        public bool IsWarning { get; }

        public WorkflowStage WorkflowStage { get; }

        public Diagnosis(Exception ex, WorkflowStage stage)
        {
            TextSpan = TextSpan.Empty;
            Message = ex.ToString();
            WorkflowStage = stage;
        }

        public Diagnosis(string message, CodeSource codeSource, WorkflowStage stage, bool isWarning = false)
            : this(1, 1, message, codeSource, stage, isWarning)
        {
        }

        public Diagnosis(int line, int column, string message, CodeSource codeSource, WorkflowStage stage, bool isWarning = false)
            : this(new LineColumnTextSpan(line, column, codeSource).GetTextSpan(), message, stage, isWarning)
        {
            Message = message;
            WorkflowStage = stage;
        }

        public Diagnosis(int beginLine, int beginColumn, int endLine, int endColumn,
            string message, CodeSource codeSource, WorkflowStage stage, bool isWarning = false)
            : this(new LineColumnTextSpan(beginLine, beginColumn, endLine, endColumn, codeSource).GetTextSpan(), message, stage, isWarning)
        {
            Message = message;
            WorkflowStage = stage;
        }

        public Diagnosis(TextSpan textSpan, string message, WorkflowStage stage, bool isWarning = false)
        {
            TextSpan = textSpan;
            Message = message;
            WorkflowStage = stage;
            IsWarning = isWarning;
        }

        public override bool Equals(object obj)
        {
            if (obj is Diagnosis diagnosis)
            {
                return TextSpan.Equals(diagnosis.TextSpan) &&
                       Message == diagnosis.Message &&
                       WorkflowStage == diagnosis.WorkflowStage &&
                       IsWarning == diagnosis.IsWarning;
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
