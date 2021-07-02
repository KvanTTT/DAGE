using System;

namespace AntlrGrammarEditor
{
    public readonly struct TextSpan : IEquatable<TextSpan>, IComparable<TextSpan>
    {
        public int Start { get; }

        public int Length { get; }

        public CodeSource Source { get; }

        public LineColumnTextSpan LineColumn => Source.ToLineColumn(this);

        public static TextSpan GetEmpty(CodeSource source) => new TextSpan(0, 0, source);

        public TextSpan(int start, int length, CodeSource codeSource)
        {
            Source = codeSource ?? throw new ArgumentNullException(nameof(codeSource));

            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (start + length < start)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Start = start;
            Length = length;
        }

        public int End => Start + Length;

        public bool IsEmpty => Start == 0 && Length == 0;

        public static TextSpan FromBounds(int start, int end, CodeSource source)
        {
            return new TextSpan(start, end - start, source);
        }

        public bool Equals(TextSpan other)
        {
            return Source.Equals(other.Source) && Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            return obj is TextSpan textSpan && Equals(textSpan);
        }

        public override int GetHashCode()
        {
            return unchecked(Start * (int)0xA5555529 + Length) ^ Source.GetHashCode();
        }

        public int CompareTo(TextSpan other)
        {
            var diff = Start - other.Start;
            if (diff != 0)
            {
                return diff;
            }

            return Length - other.Length;
        }

        public override string ToString()
        {
            return Start == End ? $"[{Start})" : $"[{Start}..{End})";
        }
    }
}
