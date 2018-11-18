using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AntlrGrammarEditor
{
    public static class AntlrHelper
    {
        public static TextSpan GetTextSpan(this ParserRuleContext ruleContext, CodeSource source)
        {
            var start = ruleContext.Start;
            if (start.Text == "<EOF>")
                return TextSpan.Empty;

            var stop = ruleContext.Stop;
            if (stop == null)
            {
                if (ruleContext.Parent is ParserRuleContext parentParserRuleContext)
                {
                    stop = parentParserRuleContext.Stop;
                }
            }

            var result = new TextSpan(source, start.StartIndex, stop.StopIndex - start.StartIndex + 1);
            return result;
        }

        public static TextSpan GetTextSpan(this ITerminalNode node, CodeSource source)
        {
            return GetTextSpan(node.Symbol, source);
        }

        public static TextSpan GetTextSpan(this IToken token, CodeSource source)
        {
            var result = new TextSpan(source, token.StartIndex, token.StopIndex - token.StartIndex + 1);
            return result;
        }
    }
}
