using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class CodeInsertion
    {
        public TextSpan TextSpan { get; set; }

        public string Text { get; set; }

        public bool Lexer { get; set; }

        public bool Predicate { get; set; }

        public override string ToString()
        {
            string lexer = Lexer ? "Lexer" : "Parser";
            string predicate = Predicate ? "Predicate" : "Action";
            return $"{Text} at {TextSpan} ({lexer}, {predicate})";
        }
    }
}
