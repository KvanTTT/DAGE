using System;
using System.Collections.Generic;
using System.Linq;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserCompiliedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserCompilied;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; }

        public Exception Exception { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public string Command { get; set; }

        public ParserCompiliedState(ParserGeneratedState parserGeneratedState)
        {
            ParserGeneratedState =
                parserGeneratedState ?? throw new ArgumentNullException(nameof(parserGeneratedState));
        }
    }
}
