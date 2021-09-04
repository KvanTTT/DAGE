using System.IO;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerCSharp : ParserCompiler
    {
        public ParserCompilerCSharp(ParserGeneratedState state) : base(state)
        {
        }

        // Lexer.cs(106,11): error CS0103: The  name 'a' does not exist in the current context
        protected override Regex ParserCompilerMessageRegex { get; } =
            new ($@"^(?<{FileMark}>[^(]+)\((?<{LineMark}>\d+),(?<{ColumnMark}>\d+)\): ?(?<{TypeMark}>[^:]+): ?(?<{MessageMark}>.+)",
                RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            string antlrCaseInsensitivePath = Path.Combine(WorkingDirectory, "AntlrCaseInsensitiveInputStream.cs");
            if (Grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(RuntimeDir, "AntlrCaseInsensitiveInputStream.cs"), antlrCaseInsensitivePath, true);
            }

            File.Copy(Path.Combine(RuntimeDir, "Program.cs"), Path.Combine(WorkingDirectory, "Program.cs"), true);
            File.Copy(Path.Combine(RuntimeDir, "AssemblyInfo.cs"), Path.Combine(WorkingDirectory, "AssemblyInfo.cs"), true);

            var projectContent = File.ReadAllText(Path.Combine(RuntimeDir, "Project.csproj"));
            projectContent = projectContent.Replace("<DefineConstants></DefineConstants>",
                $"<DefineConstants>{CurrentRuntimeInfo.Runtime}</DefineConstants>");
            File.WriteAllText(Path.Combine(WorkingDirectory, $"{Grammar.Name}.csproj"), projectContent);

            return "build";
        }
    }
}