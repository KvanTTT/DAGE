using System;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Diagnoses
{
    public class Diagnosis
    {
        public TextSpan? TextSpan { get; }

        public string Message { get; }

        public DiagnosisType Type { get; }

        public WorkflowStage WorkflowStage { get; }

        public Diagnosis(Exception ex, WorkflowStage stage, DiagnosisType type = DiagnosisType.Error)
            : this(ex.ToString(), stage, type)
        {
        }

        public Diagnosis(int line, int column, string message, Source source, WorkflowStage stage, DiagnosisType type = DiagnosisType.Error)
            : this(new LineColumnTextSpan(line, column, source).ToLinear(), message, stage, type)
        {
        }

        public Diagnosis(int beginLine, int beginColumn, int endLine, int endColumn,
            string message, Source source, WorkflowStage stage, DiagnosisType type = DiagnosisType.Error)
            : this(new LineColumnTextSpan(beginLine, beginColumn, endLine, endColumn, source).ToLinear(), message, stage, type)
        {
        }

        public Diagnosis(TextSpan textSpan, string message, WorkflowStage stage, DiagnosisType type = DiagnosisType.Error)
            : this(message, stage, type)
        {
            TextSpan = textSpan;
        }

        public Diagnosis(string message, WorkflowStage stage, DiagnosisType type = DiagnosisType.Error)
        {
            Message = message;
            WorkflowStage = stage;
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
