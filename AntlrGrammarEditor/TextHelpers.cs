using System;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public static class TextHelpers
    {
        public static TextSpan ExtractTextSpan(int start, string errorMessage, Source source)
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
                if (errorMessage.StartsWith(errorTitle))
                {
                    int secondIndex = errorTitle2 != ""
                        ? errorMessage.IndexOf(errorTitle2, StringComparison.Ordinal)
                        : errorMessage.Length;
                    if (secondIndex != -1)
                    {
                        tokenValue = errorMessage.Substring(errorTitle.Length, secondIndex - errorTitle.Length);
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
