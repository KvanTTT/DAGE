using System;
using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class CodeSource : IEquatable<CodeSource>
    {
        public static CodeSource Empty = new CodeSource("", "");

        private readonly int textLength;
        private int[] lineIndexes;

        public string Name { get; }
        public string Text { get; }

        public CodeSource(string name, string text)
        {
            Name = name;
            Text = text;

            textLength = text.Length;

            InitLineIndexes();
        }

        public int LineColumnToPosition(int line, int column)
        {
            return lineIndexes[line - LineColumnTextSpan.StartLine] + column - LineColumnTextSpan.StartColumn;
        }

        public void PositionToLineColumn(int pos, out int line, out int column)
        {
            line = Array.BinarySearch(lineIndexes, pos);
            if (line < 0)
            {
                line = (line == -1) ? 0 : (~line - 1);
            }

            column = pos - lineIndexes[line] + LineColumnTextSpan.StartColumn;
            line += LineColumnTextSpan.StartLine;
        }

        public TextSpan GetTextSpanAtLine(int line)
        {
            line = line - LineColumnTextSpan.StartLine;

            if (line < 0 || line >= lineIndexes.Length)
            {
                throw new IndexOutOfRangeException(nameof(line));
            }

            int endInd;
            if (line + 1 < lineIndexes.Length)
            {
                endInd = lineIndexes[line + 1] - 1;
                if (endInd - 1 > 0 && Text[endInd - 1] == '\r')
                {
                    endInd--;
                }
            }
            else
            {
                endInd = Text.Length;
            }

            return new TextSpan(lineIndexes[line], endInd - lineIndexes[line], this);
        }

        public bool Equals(CodeSource other) => Name.Equals(other.Name);

        public override string ToString() => Name;

        private void InitLineIndexes()
        {
            int currentLine = LineColumnTextSpan.StartLine;
            int currentColumn = LineColumnTextSpan.StartColumn;
            string text = Text;

            var lineIndexesBuffer = new List<int>(text.Length / 25) { 0 };
            int textIndex = 0;
            while (textIndex < text.Length)
            {
                char c = text[textIndex];
                if (c == '\r' || c == '\n' || c == '\u2028' || c == '\u2029')
                {
                    currentLine++;
                    currentColumn = LineColumnTextSpan.StartColumn;
                    if (c == '\r' && textIndex + 1 < text.Length && text[textIndex + 1] == '\n')
                    {
                        textIndex++;
                    }
                    lineIndexesBuffer.Add(textIndex + 1);
                }
                else
                {
                    currentColumn++;
                }
                textIndex++;
            }

            lineIndexes = lineIndexesBuffer.ToArray();
        }
    }
}
