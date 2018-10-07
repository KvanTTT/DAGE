using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public static class TextHelpers
    {
        public static List<TextSpanMapping> Map(List<CodeInsertion> source, CodeSource destinationSource, bool lexer)
        {
            var result = new List<TextSpanMapping>();
            int destInd = 0;
            CodeInsertion[] sortedSource = source.Where(s => s.Lexer == lexer).OrderBy(s => s.Predicate).ToArray();
            foreach (CodeInsertion s in sortedSource)
            {
                destInd = destinationSource.Text.IgnoreWhitespaceIndexOf(s.Text, destInd);
                result.Add(new TextSpanMapping
                {
                    SourceTextSpan = s.TextSpan,
                    DestinationTextSpan = new TextSpan(destinationSource, destInd, s.Text.Length)
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

        public static TextSpan GetSourceTextSpanForLine(List<TextSpanMapping> mapping, int destinationLine)
        {
            foreach (var m in mapping)
            {
                if (destinationLine >= m.DestinationTextSpan.StartLineColumn.Line && destinationLine <= m.DestinationTextSpan.EndLineColumn.Line)
                {
                    return m.SourceTextSpan;
                }
            }
            return TextSpan.Empty;
        }

        public static TextSpan GetSourceTextSpanForLineColumn(List<TextSpanMapping> mapping, int destLine, int destColumn)
        {
            foreach (var m in mapping)
            {
                if (destLine >= m.DestinationTextSpan.StartLineColumn.Line && destLine <= m.DestinationTextSpan.EndLineColumn.Line)
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
