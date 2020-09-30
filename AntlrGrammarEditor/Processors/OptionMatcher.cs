using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public sealed class CaseInsensitiveTypeOptionMatcher : OptionMatcher<CaseInsensitiveType>
    {
        public override string Name => nameof(Grammar.CaseInsensitiveType);

        protected override GrammarType OptionGrammarType => GrammarType.Lexer;

        public CaseInsensitiveTypeOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent) :
            base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public sealed class RuntimeOptionMatcher : OptionMatcher<Runtime>
    {
        public override string Name => "Language";

        protected override GrammarType OptionGrammarType => GrammarType.Combined;

        public RuntimeOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent)
            : base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public sealed class PackageOptionMatcher : OptionMatcher<string>
    {
        public override string Name => nameof(GrammarCheckedState.Package);

        protected override GrammarType OptionGrammarType => GrammarType.Combined;

        public PackageOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent)
            : base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public sealed class VisitorOptionMatcher : OptionMatcher<bool>
    {
        public override string Name => nameof(GrammarCheckedState.Visitor);

        protected override GrammarType OptionGrammarType => GrammarType.Separated;

        public VisitorOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent) :
            base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public sealed class ListenerOptionMatcher : OptionMatcher<bool>
    {
        public override string Name => nameof(GrammarCheckedState.Listener);

        protected override GrammarType OptionGrammarType => GrammarType.Separated;

        public ListenerOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent) :
            base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public sealed class RootOptionMatcher : OptionMatcher<string>
    {
        public override string Name => nameof(TextParsedState.Root);

        protected override GrammarType OptionGrammarType => GrammarType.Separated;

        public List<string> ExistingRules { get; }

        public RootOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent, List<string> existingRules)
            : base(codeSource, grammarType, errorEvent)
        {
            ExistingRules = existingRules;
        }

        protected override bool AdditionalCheck(IToken token, Group group)
        {
            if (GrammarType != GrammarType.Lexer && !ExistingRules.Contains(group.Value))
            {
                ReportWarning($"Root {group.Value} is not exist", token, group, CodeSource);
                return false;
            }

            return true;
        }
    }

    public sealed class PredictionModeOptionMatcher : OptionMatcher<PredictionMode>
    {
        public override string Name => nameof(GrammarCheckedState.PredictionMode);

        protected override GrammarType OptionGrammarType => GrammarType.Separated;

        public PredictionModeOptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent)
            : base(codeSource, grammarType, errorEvent)
        {
        }
    }

    public abstract class OptionMatcher<T>
    {
        public abstract string Name { get; }

        public Regex Regex { get; }

        public CodeSource CodeSource { get; }

        public GrammarType GrammarType { get; }

        protected abstract GrammarType OptionGrammarType { get; }

        private bool IsAlreadyDefined { get; set; }

        public Action<ParsingError> ErrorAction { get; }

        protected OptionMatcher(CodeSource codeSource, GrammarType grammarType, Action<ParsingError> errorEvent)
        {
            Regex = new Regex($@"({Name})\s*=\s*(\w+);", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            CodeSource = codeSource ?? throw new ArgumentNullException(nameof(codeSource));
            GrammarType = grammarType;
            ErrorAction = errorEvent ?? throw new ArgumentNullException(nameof(errorEvent));
        }

        public bool Match(IToken token, out T value)
        {
            var match = Regex.Match(token.Text);
            if (match.Success)
            {
                value = default;

                string optionName = match.Groups[1].Value;
                Group group = match.Groups[2];

                if (IsAlreadyDefined)
                {
                    ReportWarning($"Option {optionName} is already defined", token, group, CodeSource);
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
                            token, group, CodeSource);
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
                            token, group, CodeSource);
                    }
                }
                else if (valueType == typeof(string))
                {
                    value = (T)(object)group.Value;
                }

                bool parserWarning = false;
                bool lexerWarning = false;

                if (OptionGrammarType == GrammarType.Lexer && GrammarType == GrammarType.Separated)
                {
                    parserWarning = true;
                }
                else if (OptionGrammarType == GrammarType.Separated && GrammarType == GrammarType.Lexer)
                {
                    lexerWarning = true;
                }

                if (parserWarning || lexerWarning)
                {
                    ReportWarning(
                        $"Option {optionName} should be defined in {(parserWarning ? "parser" : "lexer")} or combined grammar",
                        token, group, CodeSource);
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

        protected void ReportWarning(string message, IToken token, Group group, CodeSource codeSource)
        {
            var warningTextSpan = new TextSpan(token.StartIndex + group.Index, group.Length, codeSource);
            var lineColumn = codeSource.ToLineColumn(warningTextSpan);
            var error = new ParsingError(warningTextSpan,
                Helpers.FormatErrorMessage(codeSource, lineColumn.BeginLine, lineColumn.BeginColumn, message, true),
                WorkflowStage.GrammarChecked, true);
            ErrorAction(error);
        }
    }
}