using System;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Processors.ParserGeneration
{
    public class ParserGenerationDiagnosis : Diagnosis
    {
        public override WorkflowStage WorkflowStage => WorkflowStage.ParserGenerated;

        public ParserGenerationDiagnosis(int line, int column, string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : base(line, column, message, source, type)
        {
        }

        public ParserGenerationDiagnosis(Exception ex, DiagnosisType type = DiagnosisType.Error) : base(ex, type)
        {
        }

        public ParserGenerationDiagnosis(TextSpan textSpan, string message, DiagnosisType type = DiagnosisType.Error) : base(textSpan, message, type)
        {
        }

        public ParserGenerationDiagnosis(string message, DiagnosisType type = DiagnosisType.Error) : base(message, type)
        {
        }
    }
}