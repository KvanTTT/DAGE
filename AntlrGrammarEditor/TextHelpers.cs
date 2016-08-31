using System;
using System.Collections.Generic;

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

        public static int GetLinesCount(string text)
        {
            int result = 1;
            int length = text.Length;
            int i = 0;
            while (i < length)
            {
                if (text[i] == '\r')
                {
                    result++;
                    if (i + 1 < length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else if (text[i] == '\n')
                {
                    result++;
                }
                i++;
            }
            return result;
        }

        public static List<TextSpanMapping> Map(List<TextSpanAndText> source, string destination)
        {
            var result = new List<TextSpanMapping>();
            int destInd = 0;
            foreach (var s in source)
            {
                destInd = destination.IgnoreWhitespaceIndexOf(s.Text, destInd);
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

        public static TextSpan GetSourceTextSpanForLine(List<TextSpanMapping> map, int destinationLine)
        {
            foreach (var m in map)
            {
                if (destinationLine >= m.DestinationTextSpan.BeginLine && destinationLine <= m.DestinationTextSpan.EndLine)
                {
                    return m.SourceTextSpan;
                }
            }
            return null;
        }

        public static TextSpan GetSourceTextSpanForLineColumn(List<TextSpanMapping> map, int destLine, int destColumn)
        {
            foreach (var m in map)
            {
                if (destLine >= m.DestinationTextSpan.BeginLine && destLine <= m.DestinationTextSpan.EndLine &&
                    destColumn >= m.DestinationTextSpan.BeginChar && destColumn <= m.DestinationTextSpan.EndChar)
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
    }
}
