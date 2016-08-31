using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
