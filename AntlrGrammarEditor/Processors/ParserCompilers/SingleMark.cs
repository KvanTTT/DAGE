namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class SingleMark : Mark
    {
        public SingleMark(string name, RuntimeInfo runtimeInfo)
            : base(name, runtimeInfo)
        {
        }

        public override string ToString()
        {
            return $"{StartCommentToken}{Suffix}{Name}{Suffix}{EndCommentToken}";
        }
    }
}