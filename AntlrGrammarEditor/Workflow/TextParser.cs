using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class TextParser : StageProcessor
    {
        public const string TextFileName = "Text";

        private TextParsedState _result;

        public string Text { get; }

        public string Root { get; set; }
 
        public bool OnlyTokenize { get; set; }

        public bool IndentedTree { get; set; }

        public EventHandler<(TextParsedOutput, object)> TextParsedOutputEvent { get; set; }

        public string RuntimeLibrary { get; set; }

        public TextParser(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public TextParsedState Parse(ParserCompiliedState state,
             CancellationToken cancellationToken = default(CancellationToken))
        {
            _result = new TextParsedState(state, Text)
            {
                Root = Root
            };

            Grammar grammar = state.ParserGeneratedState.GrammarCheckedState.InputState.Grammar;
            Runtime runtime = state.ParserGeneratedState.Runtime;

            Processor processor = null;
            try
            {
                File.WriteAllText(Path.Combine(ParserGenerator.HelperDirectoryName, TextFileName), _result.Text);

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
                string runtimeDir = Path.Combine(ParserCompiler.RuntimesDirName, runtime.ToString());
                string runtimeLibraryPath = RuntimeLibrary ?? Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);

                string toolName = "";
                string args = "";
                string workingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName, grammar.Name, runtime.ToString());
                string parseTextFileName = Path.Combine("..", "..", TextFileName);

                switch (runtime)
                {
                    case Runtime.CSharpOptimized:
                    case Runtime.CSharpStandard:
                        toolName = PrepareCSharpToolAndArgs(grammar, parseTextFileName, out args);
                        break;

                    case Runtime.Java:
                        toolName = PrepareJavaToolAndArgs(runtimeLibraryPath, parseTextFileName, out args);
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
                }

                processor = new Processor(toolName, args, workingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += TextParsing_ErrorDataReceived;
                processor.OutputDataReceived += TextParsing_OutputDataReceived;

                processor.Start();

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
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

        private string PrepareCSharpToolAndArgs(Grammar grammar, string parseTextFileName, out string args)
        {
            args = $"\"{Path.Combine("bin", "netcoreapp2.1", grammar.Name + ".dll")}\" {Root} \"{parseTextFileName}\" {OnlyTokenize} {IndentedTree}";
            return "dotnet";
        }

        private static string PrepareJavaToolAndArgs(string runtimeLibraryPath, string parseTextFileName,
            out string args)
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

            args = $@"-cp {relativeRuntimeLibraryPath} Main ""{parseTextFileName}""";
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
            string parserFileName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                parserFileName = Path.Combine(workingDirectory, Path.ChangeExtension(runtimeInfo.MainFile, ".exe"));
            }
            else
            {
                parserFileName = Path.Combine(workingDirectory, Path.GetFileNameWithoutExtension(runtimeInfo.MainFile));
            }

            /* Another way of starting.
                    parserFileName = CompilerPaths[Runtime];
                    var extension = runtimeInfo.Extensions.First();
                    var compiliedFiles = new StringBuilder();
                    compiliedFiles.Append('"' + runtimeInfo.MainFile + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.LexerPostfix + "." + extension + "\" ");
                    compiliedFiles.Append('"' + _grammar.Name + runtimeInfo.ParserPostfix + "." + extension + "\" ");
                    arguments = "run " + compiliedFiles.ToString();*/
            
            return parserFileName;
        }

        private void TextParsing_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            { 
                var errorString = Helpers.FixEncoding(e.Data);
                ParsingError error;
                var codeSource = new CodeSource("", Text);  // TODO: fix fileName
                try
                {
                    var words = errorString.Split(new[] { ' ' }, 3);
                    var strs = words[1].Split(':');
                    int.TryParse(strs[0], out var line);
                    int.TryParse(strs[1], out var column);
                    error = new ParsingError(line, column + 1, errorString, codeSource, WorkflowStage.TextParsed);
                }
                catch
                {
                    error = new ParsingError(errorString, codeSource, WorkflowStage.TextParsed);
                }
                AddError(error);
            }
        }

        private void TextParsing_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
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
                    AddError(new ParsingError(e.Data, null, WorkflowStage.TextParsed));
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