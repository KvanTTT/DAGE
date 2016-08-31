using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AntlrGrammarEditor
{
    public static class AntlrHelper
    {
        public static TextSpan GetTextSpan(this ParserRuleContext ruleContext)
        {
            var start = ruleContext.Start;
            if (start.Text == "<EOF>")
                return TextSpan.Empty;

            var stop = ruleContext.Stop;
            if (stop == null)
            {
                var parentParserRuleContext = ruleContext.Parent as ParserRuleContext;
                if (parentParserRuleContext != null)
                {
                    stop = parentParserRuleContext.Stop;
                }
            }

            var result = new TextSpan(start.Line, start.Column + 1, stop.Line, stop.Column + 1 + (stop.StopIndex - stop.StartIndex))
            {
                Start = start.StartIndex,
                Length = stop.StopIndex - start.StartIndex + 1
            };
            return result;
        }

        public static TextSpan GetTextSpan(this ITerminalNode node)
        {
            return GetTextSpan(node.Symbol);
        }

        public static TextSpan GetTextSpan(this IToken token)
        {
            var result = new TextSpan(token.Line, token.Column + 1, token.Line, token.Column + 1 + (token.StopIndex - token.StartIndex))
            {
                Start = token.StartIndex,
                Length = token.StopIndex - token.StartIndex + 1
            };
            return result;
        }
    }
}
