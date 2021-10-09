using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public sealed class CaseInsensitiveTypeOptionMatcher : OptionMatcher<CaseInsensitiveType>
    {
        public override string Name => nameof(Grammar.CaseInsensitiveType);

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Lexer;

        public CaseInsensitiveTypeOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent) :
            base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public sealed class RuntimeOptionMatcher : OptionMatcher<Runtime>
    {
        public override string Name => "Language";

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Combined;

        public RuntimeOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent)
            : base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public sealed class PackageOptionMatcher : OptionMatcher<string>
    {
        public override string Name => nameof(GrammarCheckedState.Package);

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Combined;

        public PackageOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent)
            : base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public sealed class VisitorOptionMatcher : OptionMatcher<bool>
    {
        public override string Name => "visitor";

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Parser;

        public VisitorOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent) :
            base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public sealed class ListenerOptionMatcher : OptionMatcher<bool>
    {
        public override string Name => "listener";

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Combined;

        public ListenerOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent) :
            base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public sealed class RootOptionMatcher : OptionMatcher<string>
    {
        public override string Name => nameof(TextParsedState.Root);

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Parser;

        private IReadOnlyList<string> ExistingRules { get; }

        public RootOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent, IReadOnlyList<string> existingRules)
            : base(source, grammarType, diagnosisEvent)
        {
            ExistingRules = existingRules;
        }

        protected override bool AdditionalCheck(IToken token, Group group)
        {
            if (GrammarType != GrammarFileType.Lexer && !ExistingRules.Contains(group.Value))
            {
                ReportWarning($"Root {group.Value} is not exist", token, group, Source);
                return false;
            }

            return true;
        }
    }

    public sealed class PredictionModeOptionMatcher : OptionMatcher<PredictionMode>
    {
        public override string Name => nameof(GrammarCheckedState.PredictionMode);

        protected override GrammarFileType OptionGrammarType => GrammarFileType.Combined;

        public PredictionModeOptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent)
            : base(source, grammarType, diagnosisEvent)
        {
        }
    }

    public abstract class OptionMatcher<T>
    {
        public abstract string Name { get; }

        public Regex Regex { get; }

        public Source Source { get; }

        public GrammarFileType GrammarType { get; }

        protected abstract GrammarFileType OptionGrammarType { get; }

        private bool IsAlreadyDefined { get; set; }

        public Action<Diagnosis> ErrorAction { get; }

        protected OptionMatcher(Source source, GrammarFileType grammarType, Action<Diagnosis> diagnosisEvent)
        {
            Regex = new Regex($@"({Name})\s*=\s*(\w+);", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Source = source ?? throw new ArgumentNullException(nameof(source));
            GrammarType = grammarType;
            ErrorAction = diagnosisEvent ?? throw new ArgumentNullException(nameof(diagnosisEvent));
        }

        public bool Match(IToken token, out T? value)
        {
            var match = Regex.Match(token.Text);
            if (match.Success)
            {
                value = default;

                string optionName = match.Groups[1].Value;
                Group group = match.Groups[2];

                if (IsAlreadyDefined)
                {
                    ReportWarning($"Option {optionName} is already defined", token, group, Source);
                }

                IsAlreadyDefined = true;
                var valueType = typeof(T);

                if (valueType.IsEnum)
                {
                    try
                    {
                        value = (T)Enum.Parse(valueType, group.Value, true);
                    }
                    catch
                    {
                        var allowedValues = (T[]) Enum.GetValues(typeof(T));
                        ReportWarning(
                            $"Incorrect option {optionName} '{group.Value}'. Allowed values: {string.Join(", ", allowedValues)}",
                            token, group, Source);
                    }
                }
                else if (valueType == typeof(bool))
                {
                    if (bool.TryParse(group.Value, out bool boolValue))
                    {
                        value = (T)(object)boolValue;
                    }
                    else
                    {
                        ReportWarning(
                            $"Incorrect option {optionName} '{group.Value}'. Allowed values: true, false",
                            token, group, Source);
                    }
                }
                else if (valueType == typeof(string))
                {
                    value = (T)(object)group.Value;
                }

                bool parserWarning = false;
                bool lexerWarning = false;

                if (OptionGrammarType == GrammarFileType.Lexer && GrammarType == GrammarFileType.Parser)
                {
                    parserWarning = true;
                }
                else if (OptionGrammarType == GrammarFileType.Parser && GrammarType == GrammarFileType.Lexer)
                {
                    lexerWarning = true;
                }

                if (parserWarning || lexerWarning)
                {
                    ReportWarning(
                        $"Option {optionName} should be defined in {(parserWarning ? "parser" : "lexer")} or combined grammar",
                        token, group, Source);
                }

                if (!AdditionalCheck(token, group))
                {
                    value = default;
                }

                return true;
            }

            value = default;
            return false;
        }

        protected virtual bool AdditionalCheck(IToken token, Group group) => true;

        protected void ReportWarning(string message, IToken token, Group group, Source source)
        {
            var textSpan = new TextSpan(token.StartIndex + group.Index, group.Length, source);
            var error = new ParserGenerationDiagnosis(textSpan, message, DiagnosisType.Warning);
            ErrorAction(error);
        }
    }
}