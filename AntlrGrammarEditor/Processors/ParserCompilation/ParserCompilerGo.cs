using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerGo : ParserCompiler
    {
        public ParserCompilerGo(ParserGeneratedState state)
            : base(state)
        {
        }

        // .\test_parser.go:172:4: syntax error: unexpected semicolon, expecting expression
        protected override Regex ParserCompilerMessageRegex { get; } =
            new($@"^(?<{FileMark}>.+?):(?<{LineMark}>\d+):(?<{ColumnMark}>\d+): ((?<{TypeMark}>[^:]+): )?(?<{MessageMark}>.+)",
                RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var compiledFiles = new StringBuilder('"' + CurrentRuntimeInfo.MainFile + "\"");

            if (string.IsNullOrWhiteSpace(Result.ParserGeneratedState.PackageName))
            {
                foreach (string generatedFile in Result.ParserGeneratedState.RuntimeFileInfos.Keys)
                {
                    compiledFiles.Append($" \"{Path.GetFileName(generatedFile)}\"");
                }
            }

            File.Copy(Path.Combine(RuntimeDir, "go.mod"), Path.Combine(WorkingDirectory, "go.mod"));

            // Get dependencies
            var dependenciesProcessor = new Processor(CurrentRuntimeInfo.RuntimeToolName, "get",
                WorkingDirectory);
            // TODO: handle dependencies warnings and errors
            dependenciesProcessor.Start();

            return "build " + compiledFiles;
        }
    }
}