using System;
using System.Collections.Generic;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class GrammarCheckedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.GrammarChecked;

        public override WorkflowState PreviousState => InputState;

        public InputState InputState { get; }

        public Dictionary<string, CodeSource> GrammarFilesData { get; } = new Dictionary<string, CodeSource>();

        public Dictionary<string, List<CodeInsertion>> GrammarActionsTextSpan { get; } = new Dictionary<string, List<CodeInsertion>>();

        public CaseInsensitiveType CaseInsensitiveType { get; set; }

        public GrammarType GrammarType { get; set; }

        public Runtime? Runtime { get; set; }

        public bool? Listener { get; set; }

        public bool? Visitor { get; set; }

        public string Package { get; set; }

        public string Root { get; set; }

        public PredictionMode? PredictionMode { get; set; }

        public List<string> Rules { get; set; } = new List<string>();

        public string LexerSuperClass { get; set; }

        public string ParserSuperClass { get; set; }

        public GrammarCheckedState(InputState inputState)
        {
            InputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        }
    }
}
