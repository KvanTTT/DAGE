namespace AntlrGrammarEditor.Fragments
{
    public class Fragment : FragmentBase
    {
        public override int Number { get; }

        public override bool IsPredicate { get; }

        public Fragment(TextSpan textSpan, int number, bool isPredicate)
            : base(textSpan)
        {
            Number = number;
            IsPredicate = isPredicate;
        }
    }
}
