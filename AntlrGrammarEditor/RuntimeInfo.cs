using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public class RuntimeInfo
    {
        public static Dictionary<Runtime, RuntimeInfo> Runtimes = new Dictionary<Runtime, RuntimeInfo>()
        {
            [Runtime.CSharpSharwell] = new RuntimeInfo
            {
                Runtime = Runtime.CSharpSharwell,
                Name = "C# Sharwell",
                JarGenerator = "antlr-4.6.4-csharpsharwell.jar",
                DLanguage = "CSharp_v4_5",
                RuntimeLibrary = "Antlr4.Runtime.dll",
                Extensions = new[] { "cs" },
                MainFile = "Program.cs",
                AntlrInputStream = "AntlrInputStream",
                DefaultCompilerPath = "dotnet"
            },
            [Runtime.CSharp] = new RuntimeInfo
            {
                Runtime = Runtime.CSharp,
                Name = "C#",
                DLanguage = "CSharp",
                RuntimeLibrary = "Antlr4.Runtime.Standard.dll",
                Extensions = new[] { "cs" },
                MainFile = "Program.cs",
                AntlrInputStream = "AntlrInputStream",
                DefaultCompilerPath = "dotnet"
            },
            [Runtime.Java] = new RuntimeInfo
            {
                Runtime = Runtime.Java,
                Name = "Java",
                DLanguage = "Java",
                RuntimeLibrary = "antlr-runtime-4.7.jar",
                Extensions = new[] { "java" },
                MainFile = "Main.java",
                AntlrInputStream = "ANTLRInputStream",
                DefaultCompilerPath = (Helpers.IsLinux || ProcessHelpers.IsProcessCanBeExecuted("javac", "-version"))
                    ? "javac"
                    : (Helpers.GetJavaExePath(@"bin\javac.exe") ?? "javac")
            },
            [Runtime.Python2] = new RuntimeInfo
            {
                Runtime = Runtime.Python2,
                Name = "Python2",
                DLanguage = "Python2",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "main.py",
                AntlrInputStream = "InputStream",
                Interpreted = true,
                DefaultCompilerPath = Helpers.IsLinux ? "python2" : "py"
            },
            [Runtime.Python3] = new RuntimeInfo
            {
                Runtime = Runtime.Python3,
                Name = "Python3",
                DLanguage = "Python3",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "main.py",
                AntlrInputStream = "InputStream",
                Interpreted = true,
                DefaultCompilerPath = Helpers.IsLinux ? "python3" : "py"
            },
            [Runtime.JavaScript] = new RuntimeInfo
            {
                Runtime = Runtime.JavaScript,
                Name = "JavaScript",
                DLanguage = "JavaScript",
                RuntimeLibrary = "",
                Extensions = new[] { "js" },
                MainFile = "main.js",
                AntlrInputStream = "antlr4.InputStream",
                Interpreted = true,
                DefaultCompilerPath = "node"
            },
            [Runtime.CPlusPlus] = new RuntimeInfo
            {
                Runtime = Runtime.CPlusPlus,
                Name = "C++ SoftGems",
                DLanguage = "Cpp",
                RuntimeLibrary = "antlr4-runtime.dll",
                Extensions = new[] { "cpp", "h" }
            },
            [Runtime.Go] = new RuntimeInfo
            {
                Runtime = Runtime.Go,
                Name = "Go",
                DLanguage = "Go",
                RuntimeLibrary = "",
                Extensions = new[] { "go" },
                MainFile = "main.go",
                AntlrInputStream = "antlr.NewInputStream",
                DefaultCompilerPath = "go",
                LexerPostfix = "_lexer",
                ParserPostfix = "_parser",
            },
            [Runtime.Swift] = new RuntimeInfo
            {
                Runtime = Runtime.Swift,
                Name = "Swift",
                DLanguage = "Swift",
                RuntimeLibrary = "",
                Extensions = new[] { "swift" },
                MainFile = "",
                AntlrInputStream = "",
                DefaultCompilerPath = "",
                LexerPostfix = "_lexer",
                ParserPostfix = "_parser",
            }
        };

        static RuntimeInfo()
        {
        }

        public Runtime Runtime { get; set; }
        public string Name { get; set; }
        public string JarGenerator { get; set; } = "antlr-4.7-complete.jar";
        public string DLanguage { get; set; }
        public string RuntimeLibrary { get; set; }
        public string[] Extensions { get; set; }
        public string MainFile { get; set; }
        public string AntlrInputStream { get; set; }
        public bool Interpreted { get; set; } = false;
        public string DefaultCompilerPath { get; set; }
        public string LexerPostfix { get; set; } = "Lexer";
        public string ParserPostfix { get; set; } = "Parser";

        public override string ToString()
        {
            return Name;
        }
    }
}
