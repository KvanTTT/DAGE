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
                JarGenerator = "antlr-4.5.3-csharpsharwell.jar",
                DLanguage = "CSharp_v4_5",
                RuntimeLibrary = "Antlr4.Runtime.dll",
                Extensions = new [] { "cs" },
                MainFile = "Program.cs",
                AntlrInputStream = "AntlrInputStream",
                DefaultCompilerPath = Helpers.GetCSharpCompilerPath()
            },
            [Runtime.CSharp] = new RuntimeInfo
            {
                Runtime = Runtime.CSharp,
                Name = "C#",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "CSharp",
                RuntimeLibrary = "Antlr4.Runtime.dll",
                Extensions = new[] { "cs" },
                MainFile = "Program.cs",
                AntlrInputStream = "AntlrInputStream",
                DefaultCompilerPath = Helpers.GetCSharpCompilerPath()
            },
            [Runtime.Java] = new RuntimeInfo
            {
                Runtime = Runtime.Java,
                Name = "Java",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "Java",
                RuntimeLibrary = "antlr-runtime-4.5.3.jar",
                Extensions = new[] { "java" },
                MainFile = "Main.java",
                AntlrInputStream = "ANTLRInputStream",
                DefaultCompilerPath = Helpers.GetJavaExePath(@"bin\javac.exe") ?? ""
            },
            [Runtime.Python2] = new RuntimeInfo
            {
                Runtime = Runtime.Python2,
                Name = "Python2",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "Python2",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "main.py",
                AntlrInputStream = "InputStream",
                Interpreted = true,
                DefaultCompilerPath = "py"
            },
            [Runtime.Python3] = new RuntimeInfo
            {
                Runtime = Runtime.Python3,
                Name = "Python3",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "Python3",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "main.py",
                AntlrInputStream = "InputStream",
                Interpreted = true,
                DefaultCompilerPath = "py"
            },
            [Runtime.JavaScript] = new RuntimeInfo
            {
                Runtime = Runtime.JavaScript,
                Name = "JavaScript",
                JarGenerator = "antlr-4.5.3.jar",
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
                JarGenerator = "antlr-4.5.4-cpp.jar",
                DLanguage = "Cpp",
                RuntimeLibrary = "antlr4-runtime.dll",
                Extensions = new[] { "cpp", "h" }
            },
            [Runtime.Go] = new RuntimeInfo
            {
                Runtime = Runtime.Go,
                Name = "Go",
                JarGenerator = "antlr-4.5.2-go.jar",
                DLanguage = "Go",
                RuntimeLibrary = "",
                Extensions = new[] { "go" },
                DefaultCompilerPath = "go"
            }
        };

        static RuntimeInfo()
        {
        }

        public Runtime Runtime { get; set; }
        public string Name { get; set; }
        public string JarGenerator { get; set; }
        public string DLanguage { get; set; }
        public string RuntimeLibrary { get; set; }
        public string[] Extensions { get; set; }
        public string MainFile { get; set; }
        public string AntlrInputStream { get; set; }
        public bool Interpreted { get; set; } = false;
        public string DefaultCompilerPath { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
