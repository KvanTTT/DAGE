using System;

namespace AntlrGrammarEditor.Processors.ParserCompilation
{
    public class ParserCompilationDiagnosis : Diagnosis
    {
        public override WorkflowStage WorkflowStage => WorkflowStage.ParserCompiled;

        public TextSpan? GrammarTextSpan { get; }

        public ParserCompilationDiagnosis(string message, DiagnosisType type = DiagnosisType.Error)
            : base(message, type)
        {
        }

        public ParserCompilationDiagnosis(Exception ex)
            : base(ex)
        {
        }

        public ParserCompilationDiagnosis(TextSpan? textSpanInGrammar, TextSpan textSpanInGenerated, string message, DiagnosisType type = DiagnosisType.Error)
            : base(textSpanInGenerated, message, type)
        {
            GrammarTextSpan = textSpanInGrammar;
        }
    }
}