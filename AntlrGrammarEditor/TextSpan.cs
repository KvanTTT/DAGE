using System;

namespace AntlrGrammarEditor
{
    /// <summary>
    /// Source: Roslyn, http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis/Text/TextSpan.cs
    /// </summary>
    public struct TextSpan : IEquatable<TextSpan>, IComparable<TextSpan>
    {
        public static TextSpan Empty => new TextSpan(CodeSource.Empty, 0, 0);

        private readonly CodeSource source;

        public TextSpan(CodeSource codeSource, int start, int length)
        {
            if (codeSource == null)
            {
                throw new ArgumentNullException(nameof(codeSource));
            }

            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (start + length < start)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            source = codeSource;
            Start = start;
            Length = length;
        }

        public TextSpan(TextSpan textSpan)
        {
            source = textSpan.Source;
            Start = textSpan.Start;
            Length = textSpan.Length;
        }

        public CodeSource Source => source ?? CodeSource.Empty;

        public int Start { get; }

        public int End => Start + Length;

        public int Length { get; }

        public bool IsEmpty => this.Length == 0;

        public LineColumn StartLineColumn => Source.PositionToLineColumn(Start);

        public LineColumn EndLineColumn => Source.PositionToLineColumn(End);

        public bool Contains(int position)
        {
            return unchecked((uint)(position - Start) < (uint)Length);
        }

        public bool Contains(TextSpan span)
        {
            return span.Start >= Start && span.End <= End;
        }

        public bool IntersectsWith(TextSpan span)
        {
            return span.Start <= End && span.End >= Start;
        }

        public bool IntersectsWith(int position)
        {
            return unchecked((uint)(position - Start) <= (uint)Length);
        }

        public TextSpan Intersection(TextSpan span)
        {
            int intersectStart = Math.Max(Start, span.Start);
            int intersectEnd = Math.Min(End, span.End);

            return intersectStart <= intersectEnd
                ? FromBounds(source, intersectStart, intersectEnd)
                : default(TextSpan);
        }

        public TextSpan Union(TextSpan span)
        {
            int unionStart = Math.Min(Start, span.Start);
            int unionEnd = Math.Max(End, span.End);

            return FromBounds(source, unionStart, unionEnd);
        }

        public static TextSpan FromBounds(CodeSource source, int start, int end)
        {
            return new TextSpan(source, start, end - start);
        }

        public static bool operator ==(TextSpan left, TextSpan right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextSpan left, TextSpan right)
        {
            return !left.Equals(right);
        }

        public bool Equals(TextSpan other)
        {
            return Source.Equals(other.Source) && Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            return obj is TextSpan && Equals((TextSpan)obj);
        }

        public override int GetHashCode()
        {
            return unchecked((Start * (int)0xA5555529) + Length) ^ Source.GetHashCode();
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
            return $"[{Start}..{End})";
        }
    }
}
