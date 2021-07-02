namespace AntlrGrammarEditor
{
    public class TextSpanMapping
    {
        public TextSpan SourceTextSpan { get; }

        public TextSpan DestTextSpan { get; }

        public TextSpanMapping(TextSpan sourceTextSpan, TextSpan destinationTextSpan)
        {
            SourceTextSpan = sourceTextSpan;
            DestTextSpan = destinationTextSpan;
        }

        public override string ToString()
        {
            return $"Source: {SourceTextSpan}; Dest: {DestTextSpan}";
        }
    }
}
