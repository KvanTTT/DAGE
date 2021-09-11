namespace AntlrGrammarEditor.Fragments
{
    public abstract class FragmentBase
    {
        public TextSpan TextSpan { get; }

        public abstract int Number { get; }

        public abstract bool IsPredicate { get; }

        protected FragmentBase(TextSpan textSpan)
        {
            TextSpan = textSpan;
        }

        public override string ToString()
        {
            string predicate = IsPredicate ? "Predicate" : "Action";
            return $"{TextSpan.Span.ToString()} at {TextSpan.LineColumn} ({Number}, {predicate})";
        }
    }
}