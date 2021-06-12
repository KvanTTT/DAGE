using System;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserGeneratedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public override WorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; }

        public Runtime Runtime { get; }

        public string PackageName { get; }

        public bool IncludeListener { get; }

        public bool IncludeVisitor { get; }

        public ParserGeneratedState(GrammarCheckedState grammarCheckedState, string packageName, Runtime runtime, bool includeListener, bool includeVisitor)
        {
            GrammarCheckedState = grammarCheckedState ?? throw new ArgumentNullException(nameof(grammarCheckedState));
            PackageName = packageName;
            Runtime = runtime;
            IncludeListener = includeListener;
            IncludeVisitor = includeVisitor;
        }
    }
}
