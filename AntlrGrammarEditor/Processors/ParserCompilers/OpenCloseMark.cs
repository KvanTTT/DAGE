namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class OpenCloseMark : Mark
    {
        public OpenCloseMark(string name, RuntimeInfo runtimeInfo, string suffix = "$")
            : base(name, runtimeInfo, suffix)
        {
        }

        public string OpenMark => $"{StartCommentToken}{Suffix}{Name}{EndCommentToken}";

        public string CloseMark => $"{StartCommentToken}{Name}{Suffix}{EndCommentToken}";

        public override string ToString() => $"{OpenMark}...{CloseMark}";
    }
}