using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class TextParser : StageProcessor
    {
        private TextParsedState _result;

        public string TextFileName { get; }

        public string Root { get; set; }

        public bool OnlyTokenize { get; set; }

        public PredictionMode PredictionMode { get; set; }

        public EventHandler<(TextParsedOutput, object)> TextParsedOutputEvent { get; set; }

        public string RuntimeLibrary { get; set; }

        public TextParser(string textFileName)
        {
            TextFileName = textFileName;
        }

        public TextParsedState Parse(ParserCompiledState state, CancellationToken cancellationToken = default)
        {
            Processor processor = null;
            try
            {
                _result = new TextParsedState(state, new CodeSource(TextFileName, File.ReadAllText(TextFileName)))
                {
                    Root = Root
                };

                Grammar grammar = state.ParserGeneratedState.GrammarCheckedState.InputState.Grammar;
                Runtime runtime = state.ParserGeneratedState.Runtime;

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
                string runtimeDir = Path.Combine(ParserCompiler.RuntimesDirName, runtime.ToString());
                string runtimeLibraryPath = RuntimeLibrary ?? Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);

                string toolName = "";
                string args = "";
                string workingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName, grammar.Name, runtime.ToString());

                switch (runtime)
                {
                    case Runtime.CSharpOptimized:
                    case Runtime.CSharpStandard:
                        toolName = PrepareCSharpToolAndArgs(grammar, out args);
                        break;

                    case Runtime.Java:
                        toolName = PrepareJavaToolAndArgs(runtimeLibraryPath, out args);
                        break;

                    case Runtime.Python2:
                    case Runtime.Python3:
                        toolName = PreparePythonToolAndArgs(runtimeInfo, out args);
                        break;

                    case Runtime.JavaScript:
                        toolName = PrepareJavaScriptToolAndArgs(runtimeInfo, out args);
                        break;

                    case Runtime.Go:
                        toolName = PrepareGoToolAndArgs(workingDirectory, runtimeInfo);
                        break;

                    case Runtime.Php:
                        toolName = PreparePhpToolAndArgs(runtimeInfo, out args);
                        break;
                }

                args += $" \"{TextFileName}\" {_result.RootOrDefault} {OnlyTokenize.ToString().ToLowerInvariant()} {PredictionMode.ToString().ToLowerInvariant()}";

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
                if (_result == null)
                {
                    _result = new TextParsedState(state, new CodeSource("", ""));
                }
                _result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.TextParsed));
                }
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
            args = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                args += runtimeInfo.Runtime == Runtime.Python2 ? "-2 " : "-3 ";
            }
            args += runtimeInfo.MainFile;
            return runtimeInfo.RuntimeToolName;
        }

        private static string PrepareJavaScriptToolAndArgs(RuntimeInfo runtimeInfo, out string args)
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

        private static string PreparePhpToolAndArgs(RuntimeInfo runtimeInfo, out string args)
        {
            args = runtimeInfo.MainFile;
            return runtimeInfo.RuntimeToolName;
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) &&
                !(_result.ParserCompiledState.ParserGeneratedState.Runtime == Runtime.Java && e.IsIgnoreJavaError()))
            {
                var errorString = Helpers.FixEncoding(e.Data);
                ParsingError error;
                try
                {
                    var words = errorString.Split(new[] { ' ' }, 3);
                    var parts = words[1].Split(':');
                    string errorMessage = words[2];
                    int.TryParse(parts[0], out int beginLine);
                    int.TryParse(parts[1], out int beginColumn);
                    int start = _result.Text.LineColumnToPosition(beginLine, beginColumn + 1);
                    string tokenValue = "";
                    int tokenLength = 0;

                    if (!ExtractTokenLength("token recognition error at: ", ""))
                    {
                        if (ExtractTokenLength("missing ", " at "))
                        {
                            tokenLength = 1;
                        }
                        else if (!ExtractTokenLength("extraneous input ", " expecting "))
                        {
                            if (!ExtractTokenLength("mismatched input ", " expecting "))
                            {
                                if (ExtractTokenLength("no viable alternative at input ", ""))
                                {
                                    var tokenValueSpan = tokenValue.AsSpan();
                                    int newIndex = start;

                                    while (newIndex > 0)
                                    {
                                        var span = _result.Text.Text.AsSpan(newIndex, tokenLength);
                                        if (span.SequenceEqual(tokenValueSpan))
                                        {
                                            tokenLength = newIndex + tokenLength - start;
                                            break;
                                        }

                                        newIndex--;
                                    }
                                }
                            }
                        }
                    }

                    bool ExtractTokenLength(string errorTitle, string errorTitle2)
                    {
                        if (errorMessage.StartsWith(errorTitle))
                        {
                            int secondIndex = errorTitle2 != ""
                                ? errorMessage.IndexOf(errorTitle2, StringComparison.Ordinal)
                                : errorMessage.Length;
                            if (secondIndex != -1)
                            {
                                tokenValue = errorMessage.Substring(errorTitle.Length, secondIndex - errorTitle.Length);
                                tokenValue = TrimAndUnescape(tokenValue);
                                tokenLength = tokenValue.Length;
                            }

                            return true;
                        }

                        return false;
                    }

                    error = new ParsingError(new TextSpan(start, tokenLength, _result.Text), errorString, WorkflowStage.TextParsed);
                }
                catch
                {
                    error = new ParsingError(errorString, _result.Text, WorkflowStage.TextParsed);
                }
                AddError(error);
            }
        }

        private static string TrimAndUnescape(string tokenValue)
        {
            tokenValue = tokenValue.Trim('\'');
            tokenValue = tokenValue.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
            return tokenValue;
        }

        private void TextParsing_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) &&
                !(_result.ParserCompiledState.ParserGeneratedState.Runtime == Runtime.Java && e.IsIgnoreJavaError()))
            {
                var strs = e.Data.Split(new [] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

                if (Enum.TryParse(strs[0], out TextParsedOutput outputState))
                {
                    var data = strs[1];
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
                    AddError(new ParsingError(e.Data, CodeSource.Empty, WorkflowStage.TextParsed));
                }
            }
        }

        private void AddError(ParsingError error)
        {
            ErrorEvent?.Invoke(this, error);
            _result.Errors.Add(error);
        }
    }
}