using System;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Processors.TextParsing
{
    public class TextParsingDiagnosis : Diagnosis
    {
        public override WorkflowStage WorkflowStage => WorkflowStage.TextParsed;

        public TextParsingDiagnosis(int beginLine, int beginColumn, int endLine, int endColumn,
            string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : base(beginLine, beginColumn, endLine, endColumn, message, source, type)
        {
        }

        public TextParsingDiagnosis(int line, int column, string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : base(line, column, message, source, type)
        {
        }

        public TextParsingDiagnosis(Exception ex, DiagnosisType type = DiagnosisType.Error) : base(ex, type)
        {
        }

        public TextParsingDiagnosis(TextSpan textSpan, string message, DiagnosisType type = DiagnosisType.Error) : base(textSpan, message, type)
        {
        }

        public TextParsingDiagnosis(string message, DiagnosisType type = DiagnosisType.Error) : base(message, type)
        {
        }
    }
}