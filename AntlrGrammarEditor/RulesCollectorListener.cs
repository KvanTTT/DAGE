using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrGrammarEditor
{
    public class GrammarInfoCollectorListener : ANTLRv4ParserBaseListener
    {
        private bool _question;
        private bool _lexer, _parser;
        private List<string> _rules = new List<string>();
        private List<TextSpanAndText> _lexerActions = new List<TextSpanAndText>();
        private List<TextSpanAndText> _lexerPredicates = new List<TextSpanAndText>();
        private List<TextSpanAndText> _parserActions = new List<TextSpanAndText>();
        private List<TextSpanAndText> _parserPredicates = new List<TextSpanAndText>();

        public List<string> Rules => _rules;

        public List<TextSpanAndText> LexerActionsAndPredicates
        {
            get
            {
                var result = new List<TextSpanAndText>(_lexerActions);
                result.AddRange(_lexerPredicates);
                return result;
            }
        }

        public List<TextSpanAndText> ParserActionsAndPredicates
        {
            get
            {
                var result = new List<TextSpanAndText>(_parserActions);
                result.AddRange(_parserPredicates);
                return result;
            }
        }

        public string GrammarName { get; private set; }

        public void CollectInfo(ANTLRv4Parser.GrammarSpecContext context)
        {
            var walker = new ParseTreeWalker();
            walker.Walk(this, context);
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
            _rules.Add(context.RULE_REF().GetText());
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
            var textSpan = context.GetTextSpan();
            textSpan.BeginChar++;
            textSpan.EndChar--;
            textSpan.Start++;
            textSpan.Length -= 2;

            var textSpanAndText = new TextSpanAndText
            {
                Text = text,
                TextSpan = textSpan
            };
            if (!_question)
            {
                if (_lexer)
                {
                    _lexerActions.Add(textSpanAndText);
                }
                else if (_parser)
                {
                    _parserActions.Add(textSpanAndText);
                }
            }
            else
            {
                if (_lexer)
                {
                    _lexerPredicates.Add(textSpanAndText);
                }
                else if (_parser)
                {
                    _parserPredicates.Add(textSpanAndText);
                }
            }
        }
    }
}
