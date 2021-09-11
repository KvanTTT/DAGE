namespace AntlrGrammarEditor.Fragments
{
    public class MappedFragment : FragmentBase
    {
        public FragmentBase OriginalFragment { get; }

        public override int Number => OriginalFragment.Number;

        public override bool IsPredicate => OriginalFragment.IsPredicate;

        public MappedFragment(TextSpan textSpan, FragmentBase originalFragment)
            : base(textSpan)
        {
            OriginalFragment = originalFragment;
        }

        public override string ToString()
        {
            return base.ToString() + $" mapped to {OriginalFragment.TextSpan.Source.Name}";
        }
    }
}