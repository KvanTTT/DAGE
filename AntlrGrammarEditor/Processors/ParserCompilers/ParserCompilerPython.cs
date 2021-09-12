using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public class ParserCompilerPython : ParserCompiler
    {
        public ParserCompilerPython(ParserGeneratedState state) : base(state)
        {
        }

        //Traceback(most recent call last):
        //  File "AntlrPythonCompileTest.py", line 1, in < module >
        //    from NewGrammarLexer import NewGrammarLexer
        //  File "Absolute\Path\To\LexerOrParser.py", line 23
        //    decisionsToDFA = [DFA(ds, i) for i, ds in enumerate(atn.decisionToState) ]
        //    ^
        //IndentationError: unexpected indent
        protected override Regex ParserCompilerMessageRegex { get; } =
            new ($@"^\s*File ""(?<{FileMark}>.+?)"", line (?<{LineMark}>\d+)(, in (.+))?", RegexOptions.Compiled);

        protected override string PrepareFilesAndGetArguments()
        {
            var stringBuilder = new StringBuilder();

            foreach (string file in GeneratedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"from {shortFileName} import {shortFileName}");
            }

            if (Grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.WriteAllText(Path.Combine(WorkingDirectory, "AntlrCaseInsensitiveInputStream.py"),
                    File.ReadAllText(Path.Combine(RuntimeDir, "AntlrCaseInsensitiveInputStream.py")));
            }

            return CreateHelperFile(stringBuilder);
        }

        protected override void ProcessReceivedData(string data)
        {
            AddToBuffer(data);
        }

        protected override void Postprocess()
        {
            if (Buffer.Count == 0)
                return;

            string message = "";
            string? codeFileName = null;
            int line = -1;
            for (int i = 0; i < Buffer.Count; i++)
            {
                Match match;
                if ((match = ParserCompilerMessageRegex.Match(Buffer[i])).Success)
                {
                    var groups = match.Groups;
                    codeFileName = Path.GetFileName(groups[FileMark].Value);
                    int.TryParse(groups[LineMark].Value, out line);
                }
                else if (i == Buffer.Count - 1)
                {
                    message = Buffer[i].Trim();
                }
            }

            AddDiagnosis(CreateMappedGrammarDiagnosis(codeFileName, line, LineColumnTextSpan.StartColumn, message, DiagnosisType.Error));
        }
    }
}