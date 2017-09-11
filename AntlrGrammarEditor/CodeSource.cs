using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class CodeSource : IEquatable<CodeSource>
    {
        public static CodeSource Empty = new CodeSource("", "");

        private readonly int textLength;
        private readonly int[] lineIndexes;

        public string Name { get; }
        public string Text { get; }

        public CodeSource(string name, string text)
        {
            Name = name;
            Text = text;

            textLength = text.Length;

            var lineIndexesBuffer = new List<int>(textLength / 25) { 0 };
            var pos = 0;
            while (pos < textLength)
            {
                switch (text[pos])
                {
                    case '\r':
                        ++pos;
                        if (pos < textLength && text[pos] == '\n')
                        {
                            ++pos;
                        }
                        lineIndexesBuffer.Add(pos);
                        break;

                    case '\n':
                    case '\u2028': /*  line separator       */
                    case '\u2029': /*  paragraph separator  */
                        ++pos;
                        lineIndexesBuffer.Add(pos);
                        break;

                    default:
                        ++pos;
                        break;
                }
            }
            lineIndexes = lineIndexesBuffer.ToArray();
        }

        public LineColumn PositionToLineColumn(int pos)
        {
            var index = Array.BinarySearch(lineIndexes, pos);
            if (index < 0)
            {
                index = ~index;

                return index > 0
                    ? new LineColumn(index, pos - lineIndexes[index - 1] + 1)
                    : new LineColumn(1, 1);
            }

            return new LineColumn(index + 1, pos - lineIndexes[index] + 1);
        }

        public int LineColumnToPosition(LineColumn lineColumn)
        {
            return (lineColumn.Line <= lineIndexes.Length)
                ? Math.Min(lineIndexes[lineColumn.Line - 1] + lineColumn.Column - 1, textLength)
                : textLength;
        }

        public bool Equals(CodeSource other) => Name.Equals(other.Name);

        public override string ToString() => Name;
    }
}
