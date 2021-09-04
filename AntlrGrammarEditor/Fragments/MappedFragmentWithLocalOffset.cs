namespace AntlrGrammarEditor.Fragments
{
    public class MappedFragmentWithLocalOffset
    {
        public MappedFragment Fragment { get; }

        public int LocalOffset { get; }

        public MappedFragmentWithLocalOffset(MappedFragment fragment, int localOffset)
        {
            Fragment = fragment;
            LocalOffset = localOffset;
        }
    }
}