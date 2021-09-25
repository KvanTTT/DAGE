using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerJava : ParserCompiler
    {
        public ParserCompilerJava(ParserGeneratedState state, CaseInsensitiveType? caseInsensitiveType)
            : base(state, caseInsensitiveType)
        {
        }

        // Lexer.java:98: error: cannot find symbol
        protected override Regex ParserCompilerMessageRegex { get; } =
            new($@"^(?<{FileMark}>.+?):(?<{LineMark}>\d+): (?<{TypeMark}>[^:]+): (?<{MessageMark}>.+)",
                RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var compiledFiles = new StringBuilder();

            string packageName = Result.ParserGeneratedState.PackageName ?? "";

            compiledFiles.Append('"' + CurrentRuntimeInfo.MainFile + '"');

            if (CaseInsensitiveType != CaseInsensitiveType.None)
            {
                compiledFiles.Append(" \"AntlrCaseInsensitiveInputStream.java\"");
                File.Copy(Path.Combine(RuntimeDir, "AntlrCaseInsensitiveInputStream.java"),
                    Path.Combine(WorkingDirectory, "AntlrCaseInsensitiveInputStream.java"), true);
            }

            var filesToCompile =
                Directory.GetFiles(Path.Combine(WorkingDirectory, packageName), "*.java");

            foreach (string helperFile in filesToCompile)
            {
                compiledFiles.Append(" \"");

                if (!string.IsNullOrEmpty(packageName))
                {
                    compiledFiles.Append(Result.ParserGeneratedState.PackageName);
                    compiledFiles.Append(Path.DirectorySeparatorChar);
                }

                compiledFiles.Append(Path.GetFileName(helperFile));

                compiledFiles.Append("\"");
            }

            return $@"-cp ""{Path.Combine("..", "..", "..", RuntimeLibraryPath)}"" -Xlint:deprecation " + compiledFiles;
        }
    }
}