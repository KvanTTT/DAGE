using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public class ParserCompiliedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserCompilied;

        public bool HasErrors => Exception != null || Errors.Any(error => !error.IsWarning);

        public IWorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; }

        public Exception Exception { get; set; }

        public string Root { get; set; }

        public string RootOrDefault => string.IsNullOrEmpty(Root)
            ? ParserGeneratedState.GrammarCheckedState.Rules.FirstOrDefault()
            : Root;

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public ParserCompiliedState(ParserGeneratedState parserGeneratedState)
        {
            ParserGeneratedState =
                parserGeneratedState ?? throw new ArgumentNullException(nameof(parserGeneratedState));
        }
    }
}
