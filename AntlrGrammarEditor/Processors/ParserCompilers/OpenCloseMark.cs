namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class OpenCloseMark : Mark
    {
        public OpenCloseMark(string name, RuntimeInfo runtimeInfo)
            : base(name, runtimeInfo)
        {
        }

        public string OpenMark => $"{StartCommentToken}{Suffix}{Name}{EndCommentToken}";

        public string CloseMark => $"{StartCommentToken}{Name}{Suffix}{EndCommentToken}";

        public override string ToString() => $"{OpenMark}...{CloseMark}";
    }
}