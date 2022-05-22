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

        public IReadOnlyDictionary<string, (Source, RuntimeFileInfo)> RuntimeFileSources { get; }

        public ParserCompiledState(ParserGeneratedState parserGeneratedState,
            IReadOnlyDictionary<string, (Source, RuntimeFileInfo)> runtimeFileSources)
        {
            ParserGeneratedState = parserGeneratedState;
            RuntimeFileSources = runtimeFileSources;
        }
    }
}
