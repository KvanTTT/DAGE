using System;

namespace AntlrGrammarEditor
{
    public readonly struct LineColumnTextSpan : IEquatable<LineColumnTextSpan>, IComparable<LineColumnTextSpan>
    {
        public const int StartLine = 1;

        public const int StartColumn = 1;

        public int BeginLine { get; }

        public int BeginColumn { get; }

        public int EndLine { get; }

        public int EndColumn { get; }

        public CodeSource Source { get; }

        public LineColumnTextSpan(int line, int column, CodeSource source)
            : this(line, column, line, column, source)
        {
        }

        public LineColumnTextSpan(int startLine, int startColumn, int endLine, int endColumn, CodeSource source)
        {
            BeginLine = startLine;
            BeginColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            Source = source;
        }

        public TextSpan GetTextSpan()
        {
            int start = Source.LineColumnToPosition(BeginLine, BeginColumn);
            int end = Source.LineColumnToPosition(EndLine, EndColumn);
            var result = TextSpan.FromBounds(start, end, Source);
            return result;
        }

        public override int GetHashCode()
        {
            int result = GetHash(BeginLine, BeginColumn);
            result = GetHash(result, EndLine);
            result = GetHash(result, EndColumn);
            return result;
        }

        public override bool Equals(object other)
        {
            return other is LineColumnTextSpan lineColumnTextSpan && Equals(lineColumnTextSpan);
        }

        public bool Equals(LineColumnTextSpan other)
        {
            return BeginLine == other.BeginLine &&
                   BeginColumn == other.BeginColumn &&
                   EndLine == other.EndLine &&
                   EndColumn == other.EndColumn &&
                   Source == other.Source;
        }

        public int CompareTo(LineColumnTextSpan other)
        {
            if (Source != other.Source)
            {
                return 1;
            }

            int result = BeginLine - other.BeginLine;
            if (result != 0)
            {
                return result;
            }

            result = BeginColumn - other.BeginColumn;
            if (result != 0)
            {
                return result;
            }

            result = EndLine - other.EndLine;
            if (result != 0)
            {
                return result;
            }

            result = EndColumn - other.EndColumn;

            return result;
        }

        public static bool operator ==(LineColumnTextSpan a, LineColumnTextSpan b) =>
            a.Equals(b);

        public static bool operator !=(LineColumnTextSpan a, LineColumnTextSpan b) =>
            !a.Equals(b);

        public override string ToString()
        {
            string result;

            if (BeginLine == EndLine)
            {
                result = BeginColumn == EndColumn
                    ? $"[{BeginLine},{BeginColumn})"
                    : $"[{BeginLine},{BeginColumn}..{EndColumn})";
            }
            else
            {
                result = BeginColumn == EndColumn
                    ? $"[{BeginLine}..{EndLine},{BeginColumn})"
                    : $"[{BeginLine},{BeginColumn}..{EndLine},{EndColumn})";
            }

            return result;
        }

        private int GetHash(int x, int y)
        {
            return unchecked(x * (int)0xA5555529 + y);
        }
    }
}
