using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public struct LineColumn : IEquatable<LineColumn>, IComparable<LineColumn>
    {
        public int Line { get; }

        public int Column { get; }

        public LineColumn(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public override int GetHashCode() =>
            unchecked(Line * (int)0xA5555529 + Column);

        public override bool Equals(object other) =>
            (other is LineColumn) && Equals((LineColumn)other);

        public bool Equals(LineColumn other) =>
            Line == other.Line &&
            Column == other.Column;

        public int CompareTo(LineColumn other) =>
            Line != other.Line ? 
            Line - other.Line :
            Column - other.Column;

        public static bool operator ==(LineColumn a, LineColumn b) =>
            a.Equals(b);

        public static bool operator !=(LineColumn a, LineColumn b) =>
            !a.Equals(b);

        public override string ToString() =>
            $"({Line};{Column})";
    }
}
