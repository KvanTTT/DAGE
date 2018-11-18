using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class ParserCompiliedState : IWorkflowState
    {
        public WorkflowStage Stage => WorkflowStage.ParserCompilied;

        public bool HasErrors => Exception != null || Errors.Count > 0;

        public IWorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; }

        public Exception Exception { get; set; }

        public string Root { get; set; }

        public string PreprocessorRoot { get; set; }

        public List<ParsingError> Errors { get; } = new List<ParsingError>();

        public ParserCompiliedState(ParserGeneratedState parserGeneratedState)
        {
            ParserGeneratedState =
                parserGeneratedState ?? throw new ArgumentNullException(nameof(parserGeneratedState));
        }
    }
}
