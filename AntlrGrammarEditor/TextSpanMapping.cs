namespace AntlrGrammarEditor
{
    public class TextSpanMapping
    {
        public TextSpan SourceTextSpan { get; set; }

        public TextSpan DestTextSpan { get; set; }

        public TextSpanMapping()
        {
        }

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
