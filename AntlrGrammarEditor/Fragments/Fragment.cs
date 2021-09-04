namespace AntlrGrammarEditor.Fragments
{
    public class Fragment : FragmentBase
    {
        public override int Number { get; }

        public override bool IsLexer { get; }

        public override bool IsPredicate { get; }

        public Fragment(TextSpan textSpan, int number, bool isLexer, bool isPredicate)
            : base(textSpan)
        {
            Number = number;
            IsLexer = isLexer;
            IsPredicate = isPredicate;
        }
    }
}
