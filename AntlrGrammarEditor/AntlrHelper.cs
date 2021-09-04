using System;
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

        public static TextSpan ExtractTextSpanFromErrorMessage(int start, string antlrErrorMessage, Source source)
        {
            string tokenValue = "";
            int length = 1;

            if (!ExtractTokenLength("token recognition error at: ", ""))
            {
                if (ExtractTokenLength("missing ", " at "))
                {
                    length = 1;
                }
                else if (!ExtractTokenLength("extraneous input ", " expecting "))
                {
                    if (!ExtractTokenLength("mismatched input ", " expecting "))
                    {
                        if (ExtractTokenLength("no viable alternative at input ", ""))
                        {
                            if (length > 0)
                            {
                                var tokenValueSpan = tokenValue.AsSpan();
                                int newIndex = start;

                                while (newIndex > 0)
                                {
                                    var span = source.Text.AsSpan(newIndex, length);
                                    if (span.SequenceEqual(tokenValueSpan))
                                    {
                                        length = newIndex + length - start;
                                        break;
                                    }

                                    newIndex--;
                                }
                            }
                        }
                    }
                }
            }

            bool ExtractTokenLength(string errorTitle, string errorTitle2)
            {
                if (antlrErrorMessage.StartsWith(errorTitle))
                {
                    int secondIndex = errorTitle2 != ""
                        ? antlrErrorMessage.IndexOf(errorTitle2, StringComparison.Ordinal)
                        : antlrErrorMessage.Length;
                    if (secondIndex != -1)
                    {
                        tokenValue = antlrErrorMessage.Substring(errorTitle.Length, secondIndex - errorTitle.Length);
                        tokenValue = TrimAndUnescape(tokenValue);
                        length = tokenValue == "<EOF>" ? 0 : tokenValue.Length;
                    }

                    return true;
                }

                return false;
            }

            return new TextSpan(start, length, source);
        }

        private static string TrimAndUnescape(string tokenValue)
        {
            tokenValue = tokenValue.Trim('\'');
            tokenValue = tokenValue.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
            return tokenValue;
        }
    }
}
