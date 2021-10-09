using System;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public abstract class Diagnosis
    {
        public abstract WorkflowStage WorkflowStage { get; }

        public TextSpan? TextSpan { get; }

        public string Message { get; }

        public DiagnosisType Type { get; }

        protected Diagnosis(Exception ex, DiagnosisType type = DiagnosisType.Error)
            : this(ex.ToString(), type)
        {
        }

        protected Diagnosis(int line, int column, string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : this(new LineColumnTextSpan(line, column, source).ToLinear(), message, type)
        {
        }

        protected Diagnosis(int beginLine, int beginColumn, int endLine, int endColumn,
            string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : this(new LineColumnTextSpan(beginLine, beginColumn, endLine, endColumn, source).ToLinear(), message, type)
        {
        }

        protected Diagnosis(TextSpan textSpan, string message, DiagnosisType type = DiagnosisType.Error)
            : this(message, type)
        {
            TextSpan = textSpan;
        }

        protected Diagnosis(string message, DiagnosisType type = DiagnosisType.Error)
        {
            Message = message;
            Type = type;
        }

        public override bool Equals(object obj)
        {
            if (obj is Diagnosis diagnosis)
            {
                return TextSpan.Equals(diagnosis.TextSpan) &&
                       Message == diagnosis.Message &&
                       WorkflowStage == diagnosis.WorkflowStage &&
                       Type == diagnosis.Type;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public override string ToString()
        {
            const string separator = ";";
            var textSpanString = TextSpan.HasValue ? $"{separator} {TextSpan.Value.LineColumn}" : "";
            return $"{WorkflowStage}{separator} {(Type == DiagnosisType.Warning ? "Warning" : "Error")}{textSpanString}{separator} {Message}";
        }
    }
}
