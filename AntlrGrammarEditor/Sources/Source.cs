using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor.Sources
{
    public class Source : IEquatable<Source>
    {
        private readonly int[] _lineIndexes;

        public string Name { get; }
        public string Text { get; }

        public Source(string name, string text)
        {
            Name = name;
            Text = text;

            var lineIndexesBuffer = new List<int>(text.Length / 25) { 0 };
            int textIndex = 0;
            while (textIndex < text.Length)
            {
                char c = text[textIndex];
                if (c == '\r' || c == '\n' || c == '\u2028' || c == '\u2029')
                {
                    if (c == '\r' && textIndex + 1 < text.Length && text[textIndex + 1] == '\n')
                    {
                        textIndex++;
                    }
                    lineIndexesBuffer.Add(textIndex + 1);
                }
                textIndex++;
            }

            _lineIndexes = lineIndexesBuffer.ToArray();
        }

        public LineColumnTextSpan ToLineColumn(TextSpan textSpan)
        {
            PositionToLineColumn(textSpan.Start, out int startLine, out int startColumn);
            PositionToLineColumn(textSpan.End, out int endLine, out int endColumn);
            return new LineColumnTextSpan(startLine, startColumn, endLine, endColumn, textSpan.Source);
        }

        public int LineColumnToPosition(int line, int column) =>
            _lineIndexes[line - LineColumnTextSpan.StartLine] + column - LineColumnTextSpan.StartColumn;

        public void PositionToLineColumn(int pos, out int line, out int column)
        {
            line = Array.BinarySearch(_lineIndexes, pos);
            if (line < 0)
            {
                line = line == -1 ? 0 : ~line - 1;
            }

            column = pos - _lineIndexes[line] + LineColumnTextSpan.StartColumn;
            line += LineColumnTextSpan.StartLine;
        }

        public bool Equals(Source other) => Name.Equals(other.Name);

        public override string ToString() => Name;
    }
}
