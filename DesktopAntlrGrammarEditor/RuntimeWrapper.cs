using AntlrGrammarEditor;

namespace DesktopAntlrGrammarEditor
{
    public class RuntimeWrapper
    {
        public Runtime? Runtime { get; }

        public RuntimeWrapper(Runtime? runtime) => Runtime = runtime;

        public override string ToString() => Runtime?.ToString() ?? "<auto>";
    }
}