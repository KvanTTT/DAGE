using Antlr4.Runtime;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public static class AntlrHelper
    {
        public static TextSpan GetTextSpan(this ParserRuleContext ruleContext, Source source)
        {
            var start = ruleContext.Start;
            if (start.Text == "<EOF>")
                return TextSpan.GetEmpty(source);

            var stop = ruleContext.Stop;
            if (stop == null)
            {
                if (ruleContext.Parent is ParserRuleContext parentParserRuleContext)
                {
                    stop = parentParserRuleContext.Stop;
                }
            }

            return stop != null
                ? new TextSpan(start.StartIndex, stop.StopIndex - start.StartIndex + 1, source)
                : TextSpan.GetEmpty(source);
        }
    }
}
