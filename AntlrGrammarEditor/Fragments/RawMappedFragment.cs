using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Fragments
{
    public class RawMappedFragment
    {
        private readonly int _start;
        private readonly int _length;

        private FragmentBase OriginalFragment { get; }

        public RawMappedFragment(int start, int length, FragmentBase originalFragment)
        {
            _start = start;
            _length = length;
            OriginalFragment = originalFragment;
        }

        public MappedFragment ToMappedFragment(Source newSource)
        {
            return new MappedFragment(new TextSpan(_start, _length, newSource), OriginalFragment);
        }

        public override string ToString()
        {
            return $"[{_start}; {_start + _length});";
        }
    }
}