namespace AntlrGrammarEditor
{
    public class CodeInsertion
    {
        public TextSpan TextSpan { get; }

        public string Text { get; }

        public bool Lexer { get; }

        public bool Predicate { get; }

        public CodeInsertion(TextSpan textSpan, string text, bool lexer, bool predicate)
        {
            TextSpan = textSpan;
            Text = text;
            Lexer = lexer;
            Predicate = predicate;
        }

        public override string ToString()
        {
            string lexer = Lexer ? "Lexer" : "Parser";
            string predicate = Predicate ? "Predicate" : "Action";
            return $"{Text} at {TextSpan} ({lexer}, {predicate})";
        }
    }
}
