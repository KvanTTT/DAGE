namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public abstract class Mark
    {
        public string StartCommentToken { get; }

        public string EndCommentToken { get; }

        public string Suffix { get; }

        public string Name { get; }

        protected Mark(string name, RuntimeInfo runtimeInfo, string suffix = "$")
        {
            Name = name;
            StartCommentToken = runtimeInfo.StartCommentToken;
            EndCommentToken = runtimeInfo.EndCommentToken;
            Suffix = suffix;
        }
    }
}