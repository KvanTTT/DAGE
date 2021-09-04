using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Processors.ParserCompilers;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors
{
    public class TextParser : StageProcessor
    {
        private static readonly Regex TextParserErrorMessageRegex =
            new ($@"line (?<{LineMark}>\d+):(?<{ColumnMark}>\d+) (?<{MessageMark}>.+)", RegexOptions.Compiled);

        private readonly TextParsedState _result;

        public string? TextFileName { get; }

        public string? Root { get; }

        public bool OnlyTokenize { get; set; }

        public PredictionMode PredictionMode { get; set; }

        public EventHandler<(TextParsedOutput, object)>? TextParsedOutputEvent { get; set; }

        public string? RuntimeLibrary { get; set; }

        public TextParser(ParserCompiledState state, string? textFileName, string? root)
        {
            TextFileName = textFileName;
            Root = root;
            _result = new TextParsedState(state,
                textFileName != null ? new Source(textFileName, File.ReadAllText(textFileName)) : null)
            {
                Root = root
            };
        }

        public TextParsedState Parse(CancellationToken cancellationToken = default)
        {
            if (_result.TextSource == null)
            {
                _result.AddDiagnosis(new Diagnosis("File to parse is not specified", WorkflowStage.TextParsed));
                return _result;
            }

            var parserCompiledState = _result.ParserCompiledState;
            Processor? processor = null;
            try
            {
                Grammar grammar = parserCompiledState.ParserGeneratedState.GrammarCheckedState.InputState.Grammar;
                Runtime runtime = parserCompiledState.ParserGeneratedState.Runtime;

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
                string runtimeDir = Path.Combine(ParserCompiler.RuntimesDirName, runtime.ToString());
                string runtimeLibraryPath = RuntimeLibrary ?? Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);

                string toolName;
                string args = "";
                string workingDirectory =
                    Path.Combine(ParserGenerator.HelperDirectoryName, grammar.Name, runtime.ToString());

                switch (runtime)
                {
                    case Runtime.CSharpOptimized:
                    case Runtime.CSharpStandard:
                        toolName = PrepareCSharpToolAndArgs(grammar, out args);
                        break;

                    case Runtime.Java:
                        toolName = PrepareJavaToolAndArgs(runtimeLibraryPath, out args);
                        break;

                    case Runtime.Python:
                        toolName = PreparePythonToolAndArgs(runtimeInfo, out args);
                        break;

                    case Runtime.Go:
                        toolName = PrepareGoToolAndArgs(workingDirectory, runtimeInfo);
                        break;

                    default:
                        toolName = PrepareDefaultToolAndArgs(runtimeInfo, out args);
                        break;
                }

                args +=
                    $" \"{TextFileName}\" {_result.RootOrDefault} {OnlyTokenize.ToString().ToLowerInvariant()} {PredictionMode.ToString().ToLowerInvariant()}";

                _result.Command = toolName + " " + args;
                processor = new Processor(toolName, args, workingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += TextParsing_ErrorDataReceived;
                processor.OutputDataReceived += TextParsing_OutputDataReceived;

                processor.Start();

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                AddDiagnosis(new Diagnosis(ex, WorkflowStage.TextParsed));
            }
            finally
            {
                processor?.Dispose();
            }

            return _result;
        }

        private string PrepareCSharpToolAndArgs(Grammar grammar, out string args)
        {
            args = $"\"{Path.Combine("bin", "netcoreapp3.1", grammar.Name + ".dll")}\"";
            return "dotnet";
        }

        private static string PrepareJavaToolAndArgs(string runtimeLibraryPath, out string args)
        {
            string relativeRuntimeLibraryPath = "\"" + Path.Combine("..", "..", "..", runtimeLibraryPath) + "\"";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                relativeRuntimeLibraryPath += ";.";
            }
            else
            {
                relativeRuntimeLibraryPath = ".:" + relativeRuntimeLibraryPath;
            }

            args = $@"-cp {relativeRuntimeLibraryPath} Main";
            return "java";
        }

        private static string PreparePythonToolAndArgs(RuntimeInfo runtimeInfo, out string args)
        {
            args = runtimeInfo.MainFile;
            return runtimeInfo.RuntimeToolName;
        }

        private static string PrepareGoToolAndArgs(string workingDirectory, RuntimeInfo runtimeInfo)
        {
            string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.ChangeExtension(runtimeInfo.MainFile, ".exe")
                : Path.GetFileNameWithoutExtension(runtimeInfo.MainFile);

            return Path.Combine(workingDirectory, fileName);
        }

        private static string PrepareDefaultToolAndArgs(RuntimeInfo runtimeInfo, out string args)
        {
            args = runtimeInfo.MainFile;
            return runtimeInfo.RuntimeToolName;
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!e.IsIgnoredMessage(_result.ParserCompiledState.ParserGeneratedState.Runtime))
            {
                var diagnosisString = FixEncoding(e.Data);
                Diagnosis diagnosis;
                try
                {
                    var match = TextParserErrorMessageRegex.Match(diagnosisString);
                    if (match.Success)
                    {
                        var groups = match.Groups;
                        int beginLine = int.Parse(groups[LineMark].Value);
                        int beginColumn = int.Parse(groups[ColumnMark].Value) + LineColumnTextSpan.StartColumn;
                        var message = groups[MessageMark].Value;
                        var textSource = _result.TextSource!;
                        int start = textSource.LineColumnToPosition(beginLine, beginColumn);
                        var textSpan = AntlrHelper.ExtractTextSpanFromErrorMessage(start, message, textSource);
                        diagnosis = new Diagnosis(textSpan, message, WorkflowStage.TextParsed);
                    }
                    else
                    {
                        throw new FormatException("Incorrect ANTLR error format");
                    }
                }
                catch
                {
                    diagnosis = new Diagnosis(diagnosisString, WorkflowStage.TextParsed);
                }

                AddDiagnosis(diagnosis);
            }
        }

        private void TextParsing_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!e.IsIgnoredMessage(_result.ParserCompiledState.ParserGeneratedState.Runtime))
            {
                var parts = e.Data.Split(new [] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

                if (Enum.TryParse(parts[0], out TextParsedOutput outputState))
                {
                    var data = parts[1];
                    switch (outputState)
                    {
                        case TextParsedOutput.LexerTime:
                            _result.LexerTime = TimeSpan.Parse(data);
                            TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.LexerTime, _result.LexerTime));
                            break;
                        case TextParsedOutput.ParserTime:
                            _result.ParserTime = TimeSpan.Parse(data);
                            TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.ParserTime, _result.ParserTime));
                            break;
                        case TextParsedOutput.Tokens:
                            _result.Tokens = data;
                            TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tokens, _result.Tokens));
                            break;
                        case TextParsedOutput.Tree:
                            _result.Tree = data.Replace("\\n", "\n");
                            TextParsedOutputEvent?.Invoke(this, (TextParsedOutput.Tree, _result.Tree));
                            break;
                    }
                }
                else
                {
                    AddDiagnosis(new Diagnosis(e.Data, WorkflowStage.TextParsed));
                }
            }
        }

        private void AddDiagnosis(Diagnosis diagnosis)
        {
            DiagnosisEvent?.Invoke(this, diagnosis);
            _result.AddDiagnosis(diagnosis);
        }
    }
}