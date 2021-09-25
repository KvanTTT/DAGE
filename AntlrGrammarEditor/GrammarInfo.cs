using System.Collections.Generic;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public class GrammarInfo
    {
        public Source Source { get; }

        public GrammarFileType Type { get; }

        public string? Name { get; }

        public string? TokenVocab { get; }

        public string? SuperClass { get; }

        public IReadOnlyList<string> Rules { get; }

        public IReadOnlyList<Fragment> Fragments { get; }

        public GrammarInfo(Source source,
            GrammarFileType type,
            string? name,
            string? tokenVocab,
            string? superClass,
            IReadOnlyList<string> rules,
            IReadOnlyList<Fragment> fragments)
        {
            Source = source;
            Type = type;
            Name = name;
            TokenVocab = tokenVocab;
            SuperClass = superClass;
            Rules = rules;
            Fragments = fragments;
        }

        public override string ToString()
        {
            return $"{Name}; Type: {Type}";
        }
    }
}