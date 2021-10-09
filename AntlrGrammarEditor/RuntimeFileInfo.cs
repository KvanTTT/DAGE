using System.IO;

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
        public string FullName { get; }

        public RuntimeFileType Type { get; }

        public bool IsGenerated => Type == RuntimeFileType.Generated || Type == RuntimeFileType.GeneratedHelper;

        public GrammarInfo RelatedGrammarInfo { get; }

        public RuntimeFileInfo(string fullName, RuntimeFileType type, GrammarInfo relatedGrammarInfo)
        {
            FullName = fullName;
            Type = type;
            RelatedGrammarInfo = relatedGrammarInfo;
        }

        public override string ToString()
        {
            return $"{Path.GetFileName(FullName)}; Type: {Type}";
        }
    }
}