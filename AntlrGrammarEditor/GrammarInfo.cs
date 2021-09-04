using System.Collections.Generic;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public class GrammarInfo
    {
        public Source Source { get; }

        public IReadOnlyList<Fragment> Fragments { get; }

        public GrammarInfo(Source source, IReadOnlyList<Fragment> fragments)
        {
            Source = source;
            Fragments = fragments;
        }
    }
}