using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerDart : ParserCompiler
    {
        public ParserCompilerDart(ParserGeneratedState state)
            : base(state)
        {
        }

        // TestParser.dart:64:9: Error: Expected an identifier, but got ';'.
        protected override Regex ParserCompilerMessageRegex { get; } =
            new(
                $@"^(?<{FileMark}>.+?):(?<{LineMark}>\d+):(?<{ColumnMark}>\d+): (?<{TypeMark}>[^:]+): (?<{MessageMark}>.+)",
                RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var stringBuilder = new StringBuilder();

            var packageName = Result.ParserGeneratedState.PackageName;
            var lexerOnlyAndNotEmptyPackageName =
                !string.IsNullOrEmpty(packageName) &&
                Result.ParserGeneratedState.GrammarCheckedState.GrammarProjectType == GrammarProjectType.Lexer;

            if (lexerOnlyAndNotEmptyPackageName)
            {
                stringBuilder.AppendLine($"library {packageName};");
                stringBuilder.AppendLine("import 'package:antlr4/antlr4.dart';");

                var lexerFile = Result.ParserGeneratedState.RuntimeFileInfos
                    .First(info => info.Value.RelatedGrammarInfo.Type == GrammarFileType.Lexer)
                    .Key;

                stringBuilder.AppendLine($"part '{Path.GetFileNameWithoutExtension(lexerFile)}.dart';");
            }
            else
            {
                foreach (string file in Result.ParserGeneratedState.RuntimeFileInfos.Keys)
                {
                    var shortFileName = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(packageName) || shortFileName.EndsWith(CurrentRuntimeInfo.ParserFilePostfix))
                    {
                        stringBuilder.AppendLine($"import '{shortFileName}.dart';");
                    }
                }
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("void main() {}");

            string compileTestFileName = CreateHelperFile(stringBuilder);

            File.Copy(Path.Combine(RuntimeDir, "pubspec.yaml"), Path.Combine(WorkingDirectory, "pubspec.yaml"), true);

            // Get dependencies
            var dependenciesProcessor = new Processor(CurrentRuntimeInfo.RuntimeToolName, "pub get",
                WorkingDirectory);
            // TODO: handle dependencies warnings and errors
            dependenciesProcessor.Start();

            return compileTestFileName;
        }
    }
}