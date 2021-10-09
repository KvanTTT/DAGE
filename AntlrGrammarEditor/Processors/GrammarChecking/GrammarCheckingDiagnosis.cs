using System;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Processors.GrammarChecking
{
    public class GrammarCheckingDiagnosis : Diagnosis
    {
        public override WorkflowStage WorkflowStage => WorkflowStage.GrammarChecked;

        public GrammarCheckingDiagnosis(int beginLine, int beginColumn, int endLine, int endColumn,
            string message, Source source, DiagnosisType type = DiagnosisType.Error)
            : base(beginLine, beginColumn, endLine, endColumn, message, source, type)
        {
        }

        public GrammarCheckingDiagnosis(Exception ex, DiagnosisType type = DiagnosisType.Error) : base(ex, type)
        {
        }

        public GrammarCheckingDiagnosis(TextSpan textSpan, string message, DiagnosisType type = DiagnosisType.Error) : base(textSpan, message, type)
        {
        }
    }
}