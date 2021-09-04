using System;
using AntlrGrammarEditor.Diagnoses;

namespace AntlrGrammarEditor.Processors
{
    public abstract class StageProcessor
    {
        public EventHandler<Diagnosis>? DiagnosisEvent;
    }
}