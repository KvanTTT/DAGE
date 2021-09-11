using System;
using System.Collections.Generic;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public readonly struct TextSpan : IEquatable<TextSpan>, IComparable<TextSpan>
    {
        public int Start { get; }

        public int Length { get; }

        public Source Source { get; }

        public LineColumnTextSpan LineColumn => Source.ToLineColumn(this);

        public ReadOnlySpan<Char> Span => Source.Text.AsSpan().Slice(Start, Length);

        public static TextSpan GetEmpty(Source source) => new TextSpan(0, 0, source);

        public TextSpan(int start, int length, Source source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));

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

        public static TextSpan FromBounds(int start, int end, Source source)
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

        public LineColumnTextSpan ToLineColumn()
        {
            return Source.ToLineColumn(this);
        }

        public IReadOnlyList<TextSpan> GetLineTextSpans()
        {
            return Source.GetLineTextSpans(this);
        }

        public override string ToString()
        {
            return $"{LineColumn} (Linear)";
        }
    }
}
