namespace AntlrGrammarEditor
{
    public class TextSpanMapping
    {
        public TextSpan SourceTextSpan { get; set; }

        public TextSpan DestinationTextSpan { get; set; }

        public TextSpanMapping()
        {
        }

        public TextSpanMapping(TextSpan sourceTextSpan, TextSpan destinationTextSpan)
        {
            SourceTextSpan = sourceTextSpan;
            DestinationTextSpan = destinationTextSpan;
        }

        public override string ToString()
        {
            return $"Source: {SourceTextSpan}; Dest: {DestinationTextSpan}";
        }
    }
}
