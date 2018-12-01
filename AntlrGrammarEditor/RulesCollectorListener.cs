using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrGrammarEditor
{
    public class GrammarInfoCollectorListener : ANTLRv4ParserBaseListener
    {
        private bool _question;
        private bool _lexer, _parser;

        public List<string> Rules { get; } = new List<string>();

        public List<CodeInsertion> CodeInsertions { get; } = new List<CodeInsertion>();

        public string GrammarName { get; private set; }

        public CodeSource GrammarSource { get; private set; }

        public string SuperClass { get; private set; }

        public void CollectInfo(CodeSource grammarSource, ANTLRv4Parser.GrammarSpecContext context)
        {
            GrammarSource = grammarSource;
            var walker = new ParseTreeWalker();
            walker.Walk(this, context);
        }

        public override void EnterOption([NotNull] ANTLRv4Parser.OptionContext context)
        {
            string optionName = context.identifier()?.GetText();

            if (optionName == "superClass")
            {
                SuperClass = context.optionValue()?.GetText();
            }
        }

        public override void EnterAction([NotNull] ANTLRv4Parser.ActionContext context)
        {
            if (context.actionScopeName() != null)
            {
                if (context.actionScopeName().LEXER() != null)
                {
                    _lexer = true;
                }
                else if (context.actionScopeName().PARSER() != null)
                {
                    _parser = true;
                }
            }
        }

        public override void ExitAction([NotNull] ANTLRv4Parser.ActionContext context)
        {
            if (context.actionScopeName() != null)
            {
                if (context.actionScopeName().LEXER() != null)
                {
                    _lexer = false;
                }
                else if (context.actionScopeName().PARSER() != null)
                {
                    _parser = false;
                }
            }
        }

        public override void EnterGrammarSpec([NotNull] ANTLRv4Parser.GrammarSpecContext context)
        {
            GrammarName = context.identifier().GetText();
        }

        public override void EnterLexerRuleSpec([NotNull] ANTLRv4Parser.LexerRuleSpecContext context)
        {
            _lexer = true;
        }

        public override void ExitLexerRuleSpec([NotNull] ANTLRv4Parser.LexerRuleSpecContext context)
        {
            _lexer = false;
        }

        public override void EnterParserRuleSpec([NotNull] ANTLRv4Parser.ParserRuleSpecContext context)
        {
            Rules.Add(context.RULE_REF().GetText());
            _parser = true;
        }

        public override void ExitParserRuleSpec([NotNull] ANTLRv4Parser.ParserRuleSpecContext context)
        {
            _parser = false;
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
            var text = context.GetText();
            text = text.Substring(1, text.Length - 2);
            var textSpan = context.GetTextSpan(GrammarSource);
            textSpan = new TextSpan(GrammarSource, textSpan.Start + 1, textSpan.Length - 2);

            if (_lexer || _parser)
            {
                var codeInsertion = new CodeInsertion
                {
                    TextSpan = textSpan,
                    Text = text,
                    Lexer = _lexer,
                    Predicate = _question
                };
                CodeInsertions.Add(codeInsertion);
            }
        }
    }
}
