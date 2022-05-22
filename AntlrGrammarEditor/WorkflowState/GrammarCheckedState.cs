using System;
using System.Collections.Generic;
using System.Linq;
using AntlrGrammarEditor.Processors;

namespace AntlrGrammarEditor.WorkflowState
{
    public class GrammarCheckedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.GrammarChecked;

        public override WorkflowState PreviousState => InputState;

        public InputState InputState { get; }

        public List<GrammarInfo> GrammarInfos { get; } = new();

        public Runtime? Runtime { get; set; }

        public bool? GenerateListener { get; set; }

        public bool? GenerateVisitor { get; set; }

        public string? Package { get; set; }

        public string? Root { get; set; }

        public PredictionMode? PredictionMode { get; set; }

        public string MainGrammarName => GrammarInfos.LastOrDefault()?.Name ??
                                     throw new InvalidOperationException("Invalid grammar name");

        public GrammarProjectType GrammarProjectType
        {
            get
            {
                if (GrammarInfos.Any(info => info.Type == GrammarFileType.Combined))
                    return GrammarProjectType.Combined;

                if (GrammarInfos.Any(info => info.Type == GrammarFileType.Parser))
                    return GrammarProjectType.Separated;

                if (GrammarInfos.Any(info => info.Type == GrammarFileType.Lexer))
                    return GrammarProjectType.Lexer;

                throw new NotImplementedException("Unsupported or invalid grammar type");
            }
        }

        public GrammarCheckedState(InputState inputState)
        {
            InputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        }
    }
}
