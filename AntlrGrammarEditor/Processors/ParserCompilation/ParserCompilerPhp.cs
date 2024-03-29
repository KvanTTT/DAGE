using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerPhp : ParserCompiler
    {
        public ParserCompilerPhp(ParserGeneratedState state)
            : base(state)
        {
        }

        // PHP Parse error:  syntax error, unexpected ';' in <file_name.php> on line 145
        protected override Regex ParserCompilerMessageRegex { get; } =
            new($@"^([^:]+):\s*(?<{MessageMark}>.+?) in (?<{FileMark}>.+?) on line (?<{LineMark}>\d+)",
                RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("<?php");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"require_once '{GetPhpAutoloadPath()}';");

            foreach (string file in Result.ParserGeneratedState.RuntimeFileInfos.Keys)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"require_once '{shortFileName}.php';");
            }

            return CreateHelperFile(stringBuilder);
        }
    }
}