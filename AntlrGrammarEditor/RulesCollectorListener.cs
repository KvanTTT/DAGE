using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrGrammarEditor
{
    public class GrammarInfoCollectorListener : ANTLRv4ParserBaseListener
    {
        private List<string> _rules = new List<string>();
        private List<TextSpanAndText> _textSpanAndTexts = new List<TextSpanAndText>();

        public IReadOnlyList<string> Rules => _rules;

        public IReadOnlyList<TextSpanAndText> TextSpanAndTexts => _textSpanAndTexts;

        public string GrammarName { get; private set; }

        public void CollectInfo(ANTLRv4Parser.GrammarSpecContext context)
        {
            var walker = new ParseTreeWalker();
            walker.Walk(this, context);
        }

        public override void EnterGrammarSpec([NotNull] ANTLRv4Parser.GrammarSpecContext context)
        {
            GrammarName = context.identifier().GetText();
        }

        public override void EnterParserRuleSpec([NotNull] ANTLRv4Parser.ParserRuleSpecContext context)
        {
            _rules.Add(context.RULE_REF().GetText());
        }

        public override void EnterActionBlock([NotNull] ANTLRv4Parser.ActionBlockContext context)
        {
            _textSpanAndTexts.Add(new TextSpanAndText
            {
                Text = context.GetText(),
                TextSpan = context.GetTextSpan()
            });
        }
    }
}
