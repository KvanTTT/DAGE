using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public abstract class WorkflowState
    {
        public abstract WorkflowStage Stage { get; }

        public abstract WorkflowState PreviousState { get; }

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public Exception Exception { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public string Command { get; set; }

        public string ErrorMessage
        {
            get
            {
                var result = new StringBuilder();
                if (Exception != null)
                    result.Append(Exception);
                foreach (ParsingError parsingError in Errors)
                    result.Append(parsingError);
                return result.ToString();
            }
        }
    }
}
