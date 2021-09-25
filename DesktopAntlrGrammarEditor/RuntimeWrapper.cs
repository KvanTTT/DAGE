using AntlrGrammarEditor;

namespace DesktopAntlrGrammarEditor
{
    public class RuntimeWrapper
    {
        public SupportedRuntime? Runtime { get; }

        public RuntimeWrapper(SupportedRuntime? runtime) => Runtime = runtime;

        public override string ToString() => Runtime?.ToString() ?? "<auto>";
    }
}