using System.Collections.Generic;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserCompiledState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserCompiled;

        public override WorkflowState PreviousState => ParserGeneratedState;

        public ParserGeneratedState ParserGeneratedState { get; }

        public CaseInsensitiveType CaseInsensitiveType { get; }

        public IReadOnlyDictionary<string, (Source, RuntimeFileInfo)> RuntimeFileSources { get; }

        public ParserCompiledState(ParserGeneratedState parserGeneratedState,
            CaseInsensitiveType caseInsensitiveType,
            IReadOnlyDictionary<string, (Source, RuntimeFileInfo)> runtimeFileSources)
        {
            CaseInsensitiveType = caseInsensitiveType;
            ParserGeneratedState = parserGeneratedState;
            RuntimeFileSources = runtimeFileSources;
        }
    }
}
