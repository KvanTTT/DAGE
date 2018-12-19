using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AntlrGrammarEditor
{
    public class RuntimeInfo
    {
        public static Dictionary<Runtime, RuntimeInfo> Runtimes = new Dictionary<Runtime, RuntimeInfo>()
        {
            [Runtime.CSharpOptimized] = new RuntimeInfo
            (
                runtime: Runtime.CSharpOptimized,
                name: "C# Optimized",
                jarGenerator: "antlr-4.6.5-csharp-optimized.jar",
                dLanguage: "CSharp_v4_5",
                runtimeLibrary: "Antlr4.Runtime.dll",
                extensions: new[] { "cs" },
                mainFile: "Program.cs",
                antlrInputStream: "AntlrInputStream",
                runtimeToolName: "dotnet",
                versionArg: "--version"
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
                runtimeLibrary: "antlr-runtime-4.7.2.jar",
                extensions: new[] { "java" },
                mainFile: "Main.java",
                antlrInputStream: "CharStreams.fromFileName",
                runtimeToolName: "javac",
                versionArg: "-version",
                errorVersionStream: true
            ),
            [Runtime.Python2] = new RuntimeInfo
            (
                runtime: Runtime.Python2,
                name: "Python2",
                dLanguage: "Python2",
                runtimeLibrary: "",
                extensions: new[] { "py" },
                mainFile: "main.py",
                antlrInputStream: "InputStream",
                baseListenerPostfix: null,
                baseVisitorPostfix: null,
                interpreted: true,
                runtimeToolName: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "py" : "python2",
                versionArg: (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-2 " : "") + "--version",
                errorVersionStream: true
            ),
            [Runtime.Python3] = new RuntimeInfo
            (
                runtime: Runtime.Python3,
                name: "Python3",
                dLanguage: "Python3",
                runtimeLibrary: "",
                extensions: new[] { "py" },
                mainFile: "main.py",
                antlrInputStream: "InputStream",
                baseListenerPostfix: null,
                baseVisitorPostfix: null,
                interpreted: true,
                runtimeToolName: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "py" : "python3",
                versionArg: (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-3 " : "") + "--version",
                errorVersionStream: false
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
                baseListenerPostfix: null,
                baseVisitorPostfix: null,
                interpreted: true,
                runtimeToolName: "node",
                versionArg: "-v",
                errorVersionStream: false
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
                versionArg: ""
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
                errorVersionStream: false
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
        public bool Interpreted { get; } = false;
        public string RuntimeToolName { get; }
        public string LexerPostfix { get; }
        public string ParserPostfix { get; }
        public string BaseListenerPostfix { get; }
        public string ListenerPostfix { get; }
        public string BaseVisitorPostfix { get; }
        public string VisitorPostfix { get; }
        public string VersionArg { get; }
        public bool ErrorVersionStream { get; }

        public bool Initialized { get; private set; }

        public string Version { get; private set; }

        public static RuntimeInfo InitOrGetRuntimeInfo(Runtime runtime)
        {
            RuntimeInfo runtimeInfo = Runtimes[runtime];
            if (!runtimeInfo.Initialized)
            {
                try
                {
                    var processor = new Processor(runtimeInfo.RuntimeToolName, runtimeInfo.VersionArg);
                    string version = "";

                    if (runtimeInfo.ErrorVersionStream)
                        processor.ErrorDataReceived += VersionCollectFunc;
                    else
                        processor.OutputDataReceived += VersionCollectFunc;
                    
                    void VersionCollectFunc(object sender, DataReceivedEventArgs e)
                    {
                        if (!(runtime == Runtime.Java && e.IsIgnoreJavaError()))
                            version += e.Data + Environment.NewLine;
                    }

                    processor.Start();
                    runtimeInfo.Version = version.Trim();
                }
                catch
                {
                    runtimeInfo.Version = null;
                }
                runtimeInfo.Initialized = true;
            }
            return runtimeInfo;
        }

        public RuntimeInfo(Runtime runtime, string name,
            string dLanguage, string runtimeLibrary, string[] extensions, string mainFile, string antlrInputStream,
            string runtimeToolName, string versionArg, bool interpreted = false,
            string jarGenerator = "antlr-4.7.2-complete.jar",
            string lexerPostfix = "Lexer", string parserPostfix = "Parser",
            string baseListenerPostfix = "BaseListener", string listenerPostfix = "Listener",
            string baseVisitorPostfix = "BaseVisitor", string visitorPostfix = "Visitor",
            bool errorVersionStream = false)
        {
            Runtime = runtime;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            JarGenerator = jarGenerator ?? throw new ArgumentNullException(nameof(jarGenerator));
            DLanguage = dLanguage ?? throw new ArgumentNullException(nameof(dLanguage));
            RuntimeLibrary = runtimeLibrary ?? throw new ArgumentNullException(nameof(runtimeLibrary));
            Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
            MainFile = mainFile ?? throw new ArgumentNullException(nameof(mainFile));
            AntlrInputStream = antlrInputStream ?? throw new ArgumentNullException(nameof(antlrInputStream));
            Interpreted = interpreted;
            RuntimeToolName = runtimeToolName ?? throw new ArgumentNullException(nameof(runtimeToolName));
            LexerPostfix = lexerPostfix ?? throw new ArgumentNullException(nameof(lexerPostfix));
            ParserPostfix = parserPostfix ?? throw new ArgumentNullException(nameof(parserPostfix));
            BaseListenerPostfix = baseListenerPostfix;
            ListenerPostfix = listenerPostfix ?? throw new ArgumentNullException(nameof(listenerPostfix));
            BaseVisitorPostfix = baseVisitorPostfix;
            VisitorPostfix = visitorPostfix ?? throw new ArgumentNullException(nameof(visitorPostfix));
            VersionArg = versionArg ?? throw new ArgumentNullException(nameof(versionArg));
            ErrorVersionStream = errorVersionStream;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
