using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AntlrGrammarEditor
{
    public class RuntimeInfo
    {
        public static readonly Dictionary<Runtime, RuntimeInfo> Runtimes = new()
        {
            [Runtime.CSharpOptimized] = new RuntimeInfo
            (
                runtime: Runtime.CSharpOptimized,
                name: "C# Optimized",
                dLanguage: "CSharp_v4_5",
                runtimeLibrary: "Antlr4.Runtime.dll",
                extensions: new[] { "cs" },
                mainFile: "Program.cs",
                antlrInputStream: "AntlrInputStream",
                runtimeToolName: "dotnet",
                versionArg: "--version",
                jarGenerator: "antlr-4.6.6-csharp-optimized.jar"
            ),
            [Runtime.CSharpStandard] = new RuntimeInfo
            (
                runtime: Runtime.CSharpStandard,
                name: "C# Standard",
                dLanguage: "CSharp",
                runtimeLibrary: "Antlr4.Runtime.Standard.dll",
                extensions: new[] { "cs" },
                mainFile: "Program.cs",
                antlrInputStream: "AntlrInputStream",
                runtimeToolName: "dotnet",
                versionArg: "--version"
            ),
            [Runtime.Java] = new RuntimeInfo
            (
                runtime: Runtime.Java,
                name: "Java",
                dLanguage: "Java",
                runtimeLibrary: "antlr-runtime-4.9.2.jar",
                extensions: new[] { "java" },
                mainFile: "Main.java",
                antlrInputStream: "CharStreams.fromFileName",
                runtimeToolName: "java",
                versionArg: "-version",
                runtimeCompilerToolName: "javac",
                textParserEnvironmentVariables: new Dictionary<string, string>
                {
                    ["JAVA_TOOL_OPTIONS"] = "-Dfile.encoding=UTF8"
                }
            ),
            [Runtime.Python] = new RuntimeInfo
            (
                runtime: Runtime.Python,
                name: "Python3",
                dLanguage: "Python3",
                runtimeLibrary: "",
                extensions: new[] { "py" },
                mainFile: "main.py",
                antlrInputStream: "InputStream",
                interpreted: true,
                runtimeToolName: "python",
                versionArg: "--version",
                baseListenerPostfix: null,
                baseVisitorPostfix: null,
                startCommentToken: "'''",
                endCommentToken: "'''",
                textParserEnvironmentVariables: new Dictionary<string, string>
                {
                    ["PYTHONIOENCODING"] = "utf-8"
                }
            ),
            [Runtime.JavaScript] = new RuntimeInfo
            (
                runtime: Runtime.JavaScript,
                name: "JavaScript",
                dLanguage: "JavaScript",
                runtimeLibrary: "",
                extensions: new[] { "js" },
                mainFile: "main.js",
                antlrInputStream: "antlr4.InputStream",
                interpreted: true,
                runtimeToolName: "node",
                versionArg: "-v",
                baseListenerPostfix: null,
                baseVisitorPostfix: null
            ),
            [Runtime.CPlusPlus] = new RuntimeInfo
            (
                runtime: Runtime.CPlusPlus,
                name: "C++",
                dLanguage: "Cpp",
                runtimeLibrary: "antlr4-runtime.dll",
                extensions: new[] { "cpp", "h" },
                mainFile: "",
                antlrInputStream: "",
                runtimeToolName: "",
                versionArg: "",
                baseListenerPostfix: null,
                baseVisitorPostfix: null
            ),
            [Runtime.Go] = new RuntimeInfo
            (
                runtime: Runtime.Go,
                name: "Go",
                dLanguage: "Go",
                runtimeLibrary: "",
                extensions: new[] { "go" },
                mainFile: "main.go",
                antlrInputStream: "antlr.NewInputStream",
                runtimeToolName: "go",
                versionArg: "version",
                lexerPostfix: "_lexer",
                parserPostfix: "_parser",
                baseListenerPostfix: "_base_listener",
                listenerPostfix: "_listener",
                baseVisitorPostfix: "_base_visitor",
                visitorPostfix: "_visitor",
                isNativeBinary: true
            ),
            [Runtime.Swift] = new RuntimeInfo
            (
                runtime: Runtime.Swift,
                name: "Swift",
                dLanguage: "Swift",
                runtimeLibrary: "",
                extensions: new[] { "swift" },
                mainFile: "",
                antlrInputStream: "",
                runtimeToolName: "swift",
                versionArg: "--version"
            ),
            [Runtime.Php] = new RuntimeInfo
            (
                runtime: Runtime.Php,
                name: "Php",
                dLanguage: "PHP",
                runtimeLibrary: "",
                extensions: new[] { "php" },
                mainFile: "index.php",
                antlrInputStream: "InputStream",
                runtimeToolName: "php",
                versionArg: "--version",
                interpreted: true
            ),
            [Runtime.Dart] = new RuntimeInfo
            (
                Runtime.Dart,
                "Dart",
                "Dart",
                "",
                new [] { "dart" },
                "main.dart",
                "InputStream",
                "dart",
                "--version"
            )
        };

        public Runtime Runtime { get; }
        public string Name { get; }
        public string JarGenerator { get; }
        public string DLanguage { get; }
        public string RuntimeLibrary { get; }
        public string[] Extensions { get; }
        public string MainFile { get; }
        public string AntlrInputStream { get; }
        public bool Interpreted { get; }
        public string RuntimeCompilerToolToolName { get; }
        public string RuntimeToolName { get; }
        public bool IsNativeBinary { get; }
        public string LexerPostfix { get; }
        public string ParserPostfix { get; }
        public string? BaseListenerPostfix { get; }
        public string ListenerPostfix { get; }
        public string? BaseVisitorPostfix { get; }
        public string VisitorPostfix { get; }
        public string VersionArg { get; }
        public bool Initialized { get; private set; }
        public string? Version { get; private set; }
        public string StartCommentToken { get; }
        public string EndCommentToken { get; }
        public IReadOnlyDictionary<string, string> TextParserEnvironmentVariables { get; }

        public static RuntimeInfo InitOrGetRuntimeInfo(Runtime runtime)
        {
            RuntimeInfo runtimeInfo = Runtimes[runtime];
            if (!runtimeInfo.Initialized)
            {
                try
                {
                    var processor = new Processor(runtimeInfo.RuntimeToolName, runtimeInfo.VersionArg);
                    string version = "";

                    processor.ErrorDataReceived += VersionCollectFunc;
                    processor.OutputDataReceived += VersionCollectFunc;

                    void VersionCollectFunc(object sender, DataReceivedEventArgs e)
                    {
                        if (!e.IsIgnoredMessage(Runtime.Java))
                            version += e.Data + Environment.NewLine;
                    }

                    processor.Start();
                    if (runtime == Runtime.Python && version.StartsWith("Python", StringComparison.OrdinalIgnoreCase))
                        version = version.Substring("Python".Length);
                    else if (version.StartsWith(runtimeInfo.RuntimeToolName, StringComparison.OrdinalIgnoreCase))
                        version = version.Substring(runtimeInfo.RuntimeToolName.Length);
                    version = version.Trim();
                    runtimeInfo.Version = version;
                }
                catch
                {
                    runtimeInfo.Version = null;
                }
                runtimeInfo.Initialized = true;
            }
            return runtimeInfo;
        }

        private RuntimeInfo(Runtime runtime, string name,
            string dLanguage, string runtimeLibrary, string[] extensions, string mainFile, string antlrInputStream,
            string runtimeToolName,
            string versionArg, bool interpreted = false,
            string jarGenerator = "antlr-4.9.2-complete.jar",
            string lexerPostfix = "Lexer", string parserPostfix = "Parser",
            string? baseListenerPostfix = "BaseListener", string listenerPostfix = "Listener",
            string? baseVisitorPostfix = "BaseVisitor", string visitorPostfix = "Visitor",
            string startCommentToken = "/*", string endCommentToken = "*/",
            Dictionary<string, string>? textParserEnvironmentVariables = null,
            string? runtimeCompilerToolName = null,
            bool isNativeBinary = false)
        {
            Runtime = runtime;
            Name = name;
            JarGenerator = jarGenerator;
            DLanguage = dLanguage;
            RuntimeLibrary = runtimeLibrary;
            Extensions = extensions;
            MainFile = mainFile;
            AntlrInputStream = antlrInputStream;
            Interpreted = interpreted;
            RuntimeCompilerToolToolName = runtimeCompilerToolName ?? runtimeToolName;
            RuntimeToolName = runtimeToolName;
            IsNativeBinary = isNativeBinary;
            LexerPostfix = lexerPostfix;
            ParserPostfix = parserPostfix;
            BaseListenerPostfix = baseListenerPostfix;
            ListenerPostfix = listenerPostfix;
            BaseVisitorPostfix = baseVisitorPostfix;
            VisitorPostfix = visitorPostfix;
            VersionArg = versionArg;
            StartCommentToken = startCommentToken;
            EndCommentToken = endCommentToken;
            TextParserEnvironmentVariables = textParserEnvironmentVariables ?? new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
