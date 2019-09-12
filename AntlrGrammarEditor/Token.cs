using System;
using System.Collections.Generic;
using System.Text;

namespace AntlrGrammarEditor
{
    public class Token
    {
        public int Type { get; }

        public int Channel { get; }

        public TextSpan TextSpan { get; }

        public string TypeName;

        public string ChannelName;

        public string Text => TextSpan.Text;

        public Token(int type, int channel, TextSpan textSpan)
        {
            Type = type;
            Channel = channel;
            TextSpan = textSpan;
        }
    }
}
