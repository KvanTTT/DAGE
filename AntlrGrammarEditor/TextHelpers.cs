using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public static class TextHelpers
    {
        public static TextSpan ExtractTextSpan(int start, string errorMessage, CodeSource source)
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

        public static List<TextSpanMapping> Map(List<CodeInsertion> source, CodeSource destinationSource, bool lexer)
        {
            var result = new List<TextSpanMapping>(source.Count);
            int destInd = 0;
            IEnumerable<CodeInsertion> sortedSource = source.Where(s => s.Lexer == lexer).OrderBy(s => s.Predicate);

            foreach (CodeInsertion s in sortedSource)
            {
                destInd = destinationSource.Text.IgnoreWhitespaceIndexOf(s.Text, destInd);
                result.Add(new TextSpanMapping(s.TextSpan, new TextSpan(destInd, s.Text.Length, destinationSource)));
                destInd += s.Text.Length;
            }

            return result;
        }

        public static TextSpan? GetSourceTextSpanForLineColumn(List<TextSpanMapping> mapping, int destLine, int destColumn)
        {
            foreach (var m in mapping)
            {
                LineColumnTextSpan destLineColumnTextSpan = m.DestTextSpan.LineColumn;
                if (destLine >= destLineColumnTextSpan.BeginLine &&
                    destLine <= destLineColumnTextSpan.EndLine)
                {
                    return m.SourceTextSpan;
                }
            }

            return null;
        }

        private static int IgnoreWhitespaceIndexOf(this string source, string value, int startIndex)
        {
            int sourceIndex = startIndex;
            var trimmedValue = value.Trim();
            while (sourceIndex < source.Length)
            {
                int valueInd = 0;
                int sourceIndex2 = sourceIndex;
                while (source[sourceIndex2] == trimmedValue[valueInd])
                {
                    sourceIndex2++;
                    valueInd++;

                    if (sourceIndex2 == source.Length || valueInd == trimmedValue.Length)
                    {
                        break;
                    }

                    while (char.IsWhiteSpace(source[sourceIndex2]))
                    {
                        sourceIndex2++;
                    }
                    while (char.IsWhiteSpace(trimmedValue[valueInd]))
                    {
                        valueInd++;
                    }

                    if (sourceIndex2 == source.Length || valueInd == trimmedValue.Length)
                    {
                        break;
                    }
                }
                if (valueInd == trimmedValue.Length)
                {
                    return sourceIndex;
                }
                sourceIndex++;
            }
            return -1;
        }

        private static int FirstNotWhitespaceCharIndexLeft(string text, int index)
        {
            while (index >= 0 && (text[index] == ' ' || text[index] == '\t'))
            {
                index--;
            }

            return index;
        }

        private static int FirstNotWhitespaceCharIndexRight(string text, int index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
            {
                index++;
            }

            return index;
        }
    }
}
