using System;
using System.Collections.Generic;
using System.Linq;

namespace AntlrGrammarEditor
{
    public static class TextHelpers
    {
        public static int LineColumnToLinear(string text, int line, int column)
        {
            int currentLine = 1;
            int currentColumn = 0;

            int i = 0;
            try
            {
                while (currentLine != line || currentLine == line && currentColumn != column)
                {
                    // General line endings:
                    //  Windows: '\r\n'
                    //  Mac (OS 9-): '\r'
                    //  Mac (OS 10+): '\n'
                    //  Unix/Linux: '\n'

                    switch (text[i])
                    {
                        case '\r':
                            currentLine++;
                            currentColumn = 0;
                            if (i + 1 < text.Length && text[i + 1] == '\n')
                            {
                                i++;
                            }
                            break;

                        case '\n':
                            currentLine++;
                            currentColumn = 0;
                            break;

                        default:
                            currentColumn++;
                            break;
                    }

                    i++;
                }
            }
            catch
            {
            }

            return i;
        }

        public static void LinearToLineColumn(int index, string text, out int line, out int column)
        {
            line = 1;
            column = 0;

            try
            {
                int i = 0;
                while (i != index)
                {
                    // General line endings:
                    //  Windows: '\r\n'
                    //  Mac (OS 9-): '\r'
                    //  Mac (OS 10+): '\n'
                    //  Unix/Linux: '\n'

                    switch (text[i])
                    {
                        case '\r':
                            line++;
                            column = 0;
                            if (i + 1 < text.Length && text[i + 1] == '\n')
                            {
                                i++;
                            }
                            break;

                        case '\n':
                            line++;
                            column = 0;
                            break;

                        default:
                            column++;
                            break;
                    }
                    i++;
                }
            }
            catch
            {
            }
        }

        public static List<TextSpanMapping> Map(List<CodeInsertion> source, string destination)
        {
            var result = new List<TextSpanMapping>();
            int destInd = 0;
            var sortedSource = source.OrderBy(s => s.Predicate).ToArray();
            foreach (var s in sortedSource)
            {
                destInd = destination.IgnoreWhitespaceIndexOf(s.Text, destInd);
                /*if (!s.Predicate)
                {
                    destInd = SelectIndexWithBoundaryNewlines(destination, destInd, s.Text);
                }*/
                int beginLine, beginColumn;
                int endLine, endColumn;
                LinearToLineColumn(destInd, destination, out beginLine, out beginColumn);
                LinearToLineColumn(destInd + s.TextSpan.Length, destination, out endLine, out endColumn);
                result.Add(new TextSpanMapping
                {
                    SourceTextSpan = s.TextSpan,
                    DestinationTextSpan = new TextSpan(beginLine, beginColumn, endLine, endColumn)
                    {
                        Start = destInd,
                        Length = s.Text.Length
                    }
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
                if (destinationLine >= m.DestinationTextSpan.BeginLine && destinationLine <= m.DestinationTextSpan.EndLine)
                {
                    return m.SourceTextSpan;
                }
            }
            return null;
        }

        public static TextSpan GetSourceTextSpanForLineColumn(List<TextSpanMapping> mapping, int destLine, int destColumn)
        {
            foreach (var m in mapping)
            {
                if (destLine >= m.DestinationTextSpan.BeginLine && destLine <= m.DestinationTextSpan.EndLine)
                {
                    return m.SourceTextSpan;
                }
            }
            return null;
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
