using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.Processors.ParserCompilation;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerJavaScript : ParserCompiler
    {
        public ParserCompilerJavaScript(ParserGeneratedState state, CaseInsensitiveType? caseInsensitiveType)
            : base(state, caseInsensitiveType)
        {
        }

        //Absolute\Path\To\LexerOrParser.js:68
        //                break;
        //                ^^^^^
        //
        //SyntaxError: Unexpected token break
        //    at exports.runInThisContext (vm.js:53:16)
        //    at Module._compile (module.js:373:25)
        //    at Object.Module._extensions..js (module.js:416:10)
        //    at Module.load (module.js:343:32)
        //    at Function.Module._load (module.js:300:12)
        //    at Module.require (module.js:353:17)
        //    at require (internal/module.js:12:17)
        //    at Object.<anonymous> (Absolute\Path\To\AntlrJavaScriptTest.js:1:85)
        //    at Module._compile (module.js:409:26)
        //    at Object.Module._extensions..js (module.js:416:10)

        // (node:17616) ExperimentalWarning: The ESM module loader is experimental.

        // file:///C:/Users/User/Documents/My-Projects/DAGE/AntlrGrammarEditor.Tests/bin/Debug/netcoreapp3.1/DageHelperDirectory/Test/JavaScript/TestParser.js:58
        protected override Regex ParserCompilerMessageRegex { get; } = new($@"^(?<file>.+?):(?<line>\d+)", RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var stringBuilder = new StringBuilder();
            foreach (string file in Result.ParserGeneratedState.RuntimeFileInfos.Keys)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"import {shortFileName} from './{shortFileName}.js';");
            }

            string compileTestFileName = CreateHelperFile(stringBuilder);

            File.Copy(Path.Combine(RuntimeDir, "package.json"), Path.Combine(WorkingDirectory, "package.json"), true);
            if (CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(RuntimeDir, "AntlrCaseInsensitiveInputStream.js"),
                    Path.Combine(WorkingDirectory, "AntlrCaseInsensitiveInputStream.js"), true);
            }

            return compileTestFileName;
        }

        protected override void ProcessReceivedData(string data)
        {
            var match = JavaScriptWarningMarker.Match(data);
            if (match.Success)
            {
                AddDiagnosis(new ParserCompilationDiagnosis(match.Groups[MessageMark].Value, DiagnosisType.Warning));
            }
            else
            {
                if (data != JavaScriptIgnoreMessage)
                {
                    AddToBuffer(data);
                }
            }
        }

        protected override void Postprocess()
        {
            if (Buffer.Count == 0)
                return;

            string message = "";
            string? codeFileName = null;
            int line = -1;

            var match = ParserCompilerMessageRegex.Match(Buffer[0]);
            if (match.Success)
            {
                var groups = match.Groups;
                codeFileName = Path.GetFileName(groups[FileMark].Value);
                int.TryParse(groups[LineMark].Value, out line);
            }

            for (int i = 1; i < Buffer.Count; i++)
            {
                if (string.IsNullOrEmpty(Buffer[i]) && i + 1 < Buffer.Count)
                {
                    message = Buffer[i + 1];
                }
            }

            AddDiagnosis(CreateMappedGrammarDiagnosis(codeFileName, line, LineColumnTextSpan.StartColumn, message, DiagnosisType.Error));
        }
    }
}