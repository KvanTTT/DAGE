using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class ParsingError
    {
        public int Line { get; set; }

        public int Column { get; set; }

        public string Message { get; set; }

        public ParsingError(int line, int column, string message)
        {
            Line = line;
            Column = column;
            Message = message;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            var parsingError = obj as ParsingError;
            if (parsingError != null)
            {
                return Line == parsingError.Line && Column == parsingError.Column && Message == parsingError.Message;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
