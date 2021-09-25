namespace AntlrGrammarEditor
{
    public enum RuntimeFileType
    {
        Generated,
        GeneratedHelper,
        Helper
    }

    public class RuntimeFileInfo
    {
        public RuntimeFileType Type { get; }

        public GrammarInfo RelatedGrammarInfo { get; }

        public RuntimeFileInfo(RuntimeFileType type, GrammarInfo relatedGrammarInfo)
        {
            Type = type;
            RelatedGrammarInfo = relatedGrammarInfo;
        }
    }
}