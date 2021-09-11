using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Processors.GrammarChecking
{
    public class GrammarInfoCollectorListener : ANTLRv4ParserBaseListener
    {
        private bool _question;
        private int _currentFragmentNumber;

        public List<string> Rules { get; } = new();

        public List<Fragment> Fragments { get; } = new();

        public string? GrammarName { get; private set; }

        public Source GrammarSource { get; }

        public GrammarType GrammarType { get; private set; }

        public string? SuperClass { get; private set; }

        public int CurrentFragmentNumber => _currentFragmentNumber;

        public GrammarInfoCollectorListener(Source grammarSource, int currentFragmentNumber)
        {
            GrammarSource = grammarSource;
            _currentFragmentNumber = currentFragmentNumber;
        }

        public void CollectInfo(ANTLRv4Parser.GrammarSpecContext context)
        {
            var walker = new ParseTreeWalker();
            walker.Walk(this, context);
        }

        public override void EnterGrammarType(ANTLRv4Parser.GrammarTypeContext context)
        {
            GrammarType = context.LEXER() != null
                ? GrammarType.Lexer
                : context.PARSER() != null
                    ? GrammarType.Separated
                    : GrammarType.Combined;
        }

        public override void EnterOption([NotNull] ANTLRv4Parser.OptionContext context)
        {
            string? optionName = context.identifier()?.GetText();

            if (optionName == "superClass")
            {
                SuperClass = context.optionValue()?.GetText();
            }
        }

        public override void EnterGrammarSpec([NotNull] ANTLRv4Parser.GrammarSpecContext context)
        {
            GrammarName = context.identifier().GetText();
        }

        public override void EnterParserRuleSpec([NotNull] ANTLRv4Parser.ParserRuleSpecContext context)
        {
            Rules.Add(context.RULE_REF().GetText());
        }

        public override void EnterLexerElement([NotNull] ANTLRv4Parser.LexerElementContext context)
        {
            if (context.QUESTION() != null)
            {
                _question = true;
            }
        }

        public override void ExitLexerElement([NotNull] ANTLRv4Parser.LexerElementContext context)
        {
            if (context.QUESTION() != null)
            {
                _question = false;
            }
        }
        public override void EnterElement([NotNull] ANTLRv4Parser.ElementContext context)
        {
            if (context.QUESTION() != null)
            {
                _question = true;
            }
        }

        public override void ExitElement([NotNull] ANTLRv4Parser.ElementContext context)
        {
            if (context.QUESTION() != null)
            {
                _question = false;
            }
        }

        public override void EnterActionBlock([NotNull] ANTLRv4Parser.ActionBlockContext context)
        {
            var textSpan = context.GetTextSpan(GrammarSource);
            textSpan = new TextSpan(textSpan.Start + 1, textSpan.Length - 2, GrammarSource);
            Fragments.Add(new Fragment(textSpan, _currentFragmentNumber++, _question));
        }
    }
}
