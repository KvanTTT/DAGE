﻿using System;
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
                        length = tokenValue.Length;
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
                result.Add(new TextSpanMapping
                {
                    SourceTextSpan = s.TextSpan,
                    DestTextSpan = new TextSpan(destInd, s.Text.Length, destinationSource)
                });
                destInd += s.Text.Length;
            }

            return result;
        }

        private static int SelectIndexWithBoundaryNewlines(string destination, int destInd, string source)
        {
            do
            {
                int ind = FirstNotWhitespaceCharIndexLeft(destination, destInd - 1);
                if (ind <= 0)
                {
                    ind = 0;
                }
                if (ind == 0 || destination[ind] == '\r' || destination[ind] == '\n')
                {
                    break;
                }
                else
                {
                    destInd = destination.IgnoreWhitespaceIndexOf(source, destInd + source.Length);
                }
            }
            while (true);

            do
            {
                int ind = FirstNotWhitespaceCharIndexRight(destination, destInd + source.Length);
                if (ind == destination.Length - 1 || destination[ind] == '\r' || destination[ind] == '\n')
                {
                    break;
                }
                else
                {
                    destInd = destination.IgnoreWhitespaceIndexOf(source, destInd + source.Length);
                }
            }
            while (true);
            return destInd;
        }

        public static TextSpan GetSourceTextSpanForLine(List<TextSpanMapping> textSpanMappings, int destinationLine, string sourceFileName)
        {
            foreach (TextSpanMapping textSpanMapping in textSpanMappings)
            {
                LineColumnTextSpan destLineColumnTextSpan = textSpanMapping.DestTextSpan.GetLineColumn();
                if (destinationLine >= destLineColumnTextSpan.BeginLine &&
                    destinationLine <= destLineColumnTextSpan.EndLine)
                {
                    return textSpanMapping.SourceTextSpan;
                }
            }

            if (textSpanMappings.Count > 0)
            {
                return new TextSpan(0, 0, textSpanMappings[0].SourceTextSpan.Source);
            }

            return TextSpan.Empty;
        }

        public static TextSpan GetSourceTextSpanForLineColumn(List<TextSpanMapping> mapping, int destLine, int destColumn)
        {
            foreach (var m in mapping)
            {
                LineColumnTextSpan destLineColumnTextSpan = m.DestTextSpan.GetLineColumn();
                if (destLine >= destLineColumnTextSpan.BeginLine &&
                    destLine <= destLineColumnTextSpan.EndLine)
                {
                    return m.SourceTextSpan;
                }
            }
            return TextSpan.Empty;
        }

        public static int IgnoreWhitespaceIndexOf(this string source, string value, int startIndex)
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
