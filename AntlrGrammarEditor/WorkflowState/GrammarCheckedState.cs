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

        public Dictionary<string, GrammarInfo> GrammarInfos { get; } = new();

        public CaseInsensitiveType CaseInsensitiveType { get; set; }

        public GrammarType GrammarType { get; set; }

        public Runtime? Runtime { get; set; }

        public bool? Listener { get; set; }

        public bool? Visitor { get; set; }

        public string? Package { get; set; }

        public string? Root { get; set; }

        public PredictionMode? PredictionMode { get; set; }

        public List<string> Rules { get; set; } = new();

        public string? LexerSuperClass { get; set; }

        public string? ParserSuperClass { get; set; }

        public GrammarCheckedState(InputState inputState)
        {
            InputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        }
    }
}
