namespace AntlrGrammarEditor.Fragments
{
    public class MappedResult
    {
        public MappedFragment Fragment { get; }

        public TextSpan TextSpanInGenerated { get; }

        public TextSpan TextSpanInGrammar { get; }

        public MappedResult(MappedFragment fragment, TextSpan textSpanInGenerated, TextSpan textSpanInGrammar)
        {
            Fragment = fragment;
            TextSpanInGenerated = textSpanInGenerated;
            TextSpanInGrammar = textSpanInGrammar;
        }

        public override string ToString()
        {
            return $"{Fragment}; TextSpanInGenerated: {TextSpanInGenerated}; TextSpanInGrammar: {TextSpanInGrammar}";
        }
    }
}