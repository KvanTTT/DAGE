using System;

namespace AntlrGrammarEditor
{
    public class TextSpan
    {
        public int BeginLine { get; set; }

        public int BeginChar { get; set; }

        public int EndLine { get; set; }

        public int EndChar { get; set; }

        public int Start { get; set; }

        public int Length { get;  set; }

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

        public override bool Equals(object obj)
        {
            var textSpan = obj as TextSpan;
            if (textSpan != null)
            {
                return BeginLine == textSpan.BeginLine && BeginChar == textSpan.BeginChar &&
                       EndLine == textSpan.EndLine && EndChar == textSpan.EndChar;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return BeginLine ^ BeginChar ^ EndLine ^ EndChar ^ Start ^ Length;
        }

        public override string ToString()
        {
            return $"({BeginLine};{BeginChar})-({EndLine};{EndChar})";
        }
    }
}
