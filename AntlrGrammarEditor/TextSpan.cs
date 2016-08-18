using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class TextSpan
    {
        public int BeginLine { get; set; }

        public int BeginChar { get; set; }

        public int EndLine { get; set; }

        public int EndChar { get; set; }

        public int Start { get; set; }

        public int Length { get; set; }

        public static readonly TextSpan Empty = new TextSpan(-1, -1, -1, -1) { Start = -1, Length = 0 };

        public TextSpan()
        {
        }

        public TextSpan(int beginLine, int beginChar, int endLine, int endChar)
        {
            BeginLine = beginLine;
            BeginChar = beginChar;
            EndLine = endLine;
            EndChar = endChar;
        }

        public TextSpan(TextSpan textSpan)
        {
            BeginLine = textSpan.BeginLine;
            BeginChar = textSpan.BeginChar;
            EndLine = textSpan.EndLine;
            EndChar = textSpan.EndChar;
            Start = textSpan.Start;
            Length = textSpan.Length;
        }

        public override string ToString()
        {
            return $"({BeginLine};{BeginChar})-({EndLine};{EndChar})";
        }

        public TextSpan UnionWith(TextSpan textSpan)
        {
            int beginLine, beginChar, endLine, endChar;

            if (textSpan.EndLine < BeginLine)
            {
                beginLine = textSpan.BeginLine;
                beginChar = textSpan.BeginChar;
                endLine = BeginLine;
                endChar = BeginChar;
            }
            else if (textSpan.EndLine == BeginLine)
            {
                beginLine = endLine = BeginLine;
                beginChar = Math.Min(BeginChar, textSpan.BeginChar);
                endChar = Math.Max(EndChar, textSpan.EndChar);
            }
            else
            {
                beginLine = BeginLine;
                beginChar = BeginChar;
                endLine = textSpan.BeginLine;
                endChar = textSpan.BeginChar;
            }

            int start = Math.Min(Start, textSpan.Start);
            int end1 = Start + Length;
            int end2 = textSpan.Start + textSpan.Length;
            int length = (end1 >= end2 ? end1 : end2) - start;

            return new TextSpan(beginLine, beginChar, endLine, endChar)
            {
                Start = start,
                Length = length
            };
        }

        public TextSpan IntersectWith(TextSpan textSpan)
        {
            int beginLine = Math.Max(BeginLine, textSpan.BeginLine);
            int beginChar = Math.Max(BeginChar, textSpan.BeginChar);
            int endLine = Math.Min(EndLine, textSpan.EndLine);
            int endChar = Math.Min(EndChar, textSpan.EndChar);

            if (endLine < beginLine)
            {
                return Empty;
            }

            if (endLine == beginLine)
            {
                if (endChar < beginChar)
                {
                    return Empty;
                }
            }

            return new TextSpan(
                Math.Min(BeginLine, textSpan.BeginLine),
                Math.Min(BeginChar, textSpan.BeginChar),
                Math.Max(EndLine, textSpan.EndLine),
                Math.Max(EndChar, textSpan.EndChar)
            )
            {
                Start = Math.Max(Start, textSpan.Start),
                Length = Math.Min(Length, textSpan.Length)
            };
        }

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
    }
}
