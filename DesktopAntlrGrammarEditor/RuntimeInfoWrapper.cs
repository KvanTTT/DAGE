using AntlrGrammarEditor;

namespace DesktopAntlrGrammarEditor
{
    public class RuntimeInfoWrapper
    {
        public RuntimeInfo? RuntimeInfo { get; }

        public RuntimeInfoWrapper(RuntimeInfo? runtimeInfo) => RuntimeInfo = runtimeInfo;

        public override string ToString() => RuntimeInfo?.ToString() ?? "<auto>";
    }
}