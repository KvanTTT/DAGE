using System.Collections.Generic;
using System.Linq;
using System.Text;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public abstract class WorkflowState
    {
        private readonly List<Diagnosis> _diagnoses = new();

        public abstract WorkflowStage Stage { get; }

        public abstract WorkflowState? PreviousState { get; }

        public bool HasErrors => _diagnoses.Any(error => error.Type == DiagnosisType.Error);

        public IReadOnlyList<Diagnosis> Diagnoses => _diagnoses;

        public string? Command { get; set; }

        public void AddDiagnosis(Diagnosis diagnosis)
        {
            lock (_diagnoses)
            {
                _diagnoses.Add(diagnosis);
            }
        }

        public string DiagnosisMessage
        {
            get
            {
                var result = new StringBuilder();
                foreach (Diagnosis diagnosis in Diagnoses)
                    result.Append(diagnosis);
                return result.ToString();
            }
        }
    }
}
