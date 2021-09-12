using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Processors.ParserCompilers;
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.TextParsing
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

                string workingDirectory =
                    Path.Combine(ParserGenerator.HelperDirectoryName, grammar.Name, runtime.ToString());

                string args = runtime switch
                {
                    Runtime.CSharpOptimized => PrepareCSharpArgs(grammar),
                    Runtime.CSharpStandard => PrepareCSharpArgs(grammar),
                    Runtime.Java => PrepareJavaArgs(runtimeLibraryPath),
                    Runtime.Go => "",
                    _ => runtimeInfo.MainFile
                };

                args +=
                    $" \"{TextFileName}\" {_result.RootOrDefault} {OnlyTokenize.ToString().ToLowerInvariant()} {PredictionMode.ToString().ToLowerInvariant()}";

                string runtimeToolName;
                if (runtimeInfo.IsNativeBinary)
                {
                    string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? Path.ChangeExtension(runtimeInfo.MainFile, ".exe")
                        : Path.GetFileNameWithoutExtension(runtimeInfo.MainFile);

                    runtimeToolName = Path.Combine(workingDirectory, fileName);
                }
                else
                {
                    runtimeToolName = runtimeInfo.RuntimeToolName;
                }

                _result.Command = runtimeToolName + " " + args;
                processor = new Processor(runtimeToolName, args, workingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += TextParsing_ErrorDataReceived;
                processor.OutputDataReceived += TextParsing_OutputDataReceived;
                foreach (var environmentVariable in runtimeInfo.TextParserEnvironmentVariables)
                    processor.EnvironmentVariables.Add(environmentVariable.Key, environmentVariable.Value);
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

        private string PrepareCSharpArgs(Grammar grammar)
        {
            return $"\"{Path.Combine("bin", "netcoreapp3.1", grammar.Name + ".dll")}\"";
        }

        private string PrepareJavaArgs(string runtimeLibraryPath)
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

            return $@"-cp {relativeRuntimeLibraryPath} Main";
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!e.IsIgnoredMessage(_result.ParserCompiledState.ParserGeneratedState.Runtime))
            {
                Diagnosis diagnosis;
                try
                {
                    var match = TextParserErrorMessageRegex.Match(e.Data);
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
                        diagnosis = new Diagnosis(e.Data, WorkflowStage.TextParsed);
                    }
                }
                catch
                {
                    diagnosis = new Diagnosis(e.Data, WorkflowStage.TextParsed);
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