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
                AntlrInputStream = "AntlrInputStream"

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
                AntlrInputStream = "AntlrInputStream"
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
                AntlrInputStream = "ANTLRInputStream"
            },
            [Runtime.Python2] = new RuntimeInfo
            {
                Runtime = Runtime.Python2,
                Name = "Python2",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "Python2",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "",
                AntlrInputStream = ""
            },
            [Runtime.Python3] = new RuntimeInfo
            {
                Runtime = Runtime.Python3,
                Name = "Python3",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "Python3",
                RuntimeLibrary = "",
                Extensions = new[] { "py" },
                MainFile = "",
                AntlrInputStream = ""
            },
            [Runtime.JavaScript] = new RuntimeInfo
            {
                Runtime = Runtime.JavaScript,
                Name = "JavaScript",
                JarGenerator = "antlr-4.5.3.jar",
                DLanguage = "JavaScript",
                RuntimeLibrary = "",
                Extensions = new[] { "js" },
                MainFile = "",
                AntlrInputStream = ""
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
                Extensions = new[] { "go" }
            }
        };

        public Runtime Runtime;
        public string Name;
        public string JarGenerator;
        public string DLanguage;
        public string RuntimeLibrary;
        public string[] Extensions;
        public string MainFile;
        public string AntlrInputStream;

        public override string ToString()
        {
            return Name;
        }
    }
}
