using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class ParserGenerator : StageProcessor
    {
        public const string HelperDirectoryName = "DageHelperDirectory";

        private CodeSource _currentGrammarSource;
        private ParserGeneratedState _result;

        public Runtime Runtime { get; }

        public string PackageName { get; set; }

        public bool GenerateListener { get; set; } = true;

        public bool GenerateVisitor { get; set; } = true;

        public string GeneratorTool { get; set; }

        public ParserGenerator(Runtime runtime)
        {
            Runtime = runtime;
        }

        public ParserGeneratedState Generate(GrammarCheckedState state, CancellationToken cancellationToken = default)
        {
            Grammar grammar = state.InputState.Grammar;

            _result = new ParserGeneratedState(state, PackageName, Runtime, GenerateListener, GenerateVisitor);

            Generate(grammar, state, cancellationToken);

            return _result;
        }

        private void Generate(Grammar grammar, GrammarCheckedState state, CancellationToken cancellationToken)
        {
            Processor processor = null;

            try
            {
                string runtimeDirectoryName = Path.Combine(HelperDirectoryName, grammar.Name, Runtime.ToString());

                if ((Runtime == Runtime.Java || Runtime == Runtime.Go) && !string.IsNullOrWhiteSpace(PackageName))
                {
                    runtimeDirectoryName = Path.Combine(runtimeDirectoryName, PackageName);
                }

                if (Directory.Exists(runtimeDirectoryName))
                {
                    Directory.Delete(runtimeDirectoryName, true);
                }

                Directory.CreateDirectory(runtimeDirectoryName);

                cancellationToken.ThrowIfCancellationRequested();

                RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime);

                var jarGenerator = GeneratorTool ?? Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (string grammarFileName in state.InputState.Grammar.Files)
                {
                    string extension = Path.GetExtension(grammarFileName);
                    if (extension != Grammar.AntlrDotExt)
                    {
                        continue;
                    }

                    _currentGrammarSource = state.GrammarFilesData[grammarFileName];

                    var arguments =
                        $@"-jar ""{jarGenerator}"" ""{Path.Combine(grammar.Directory, grammarFileName)}"" " +
                        $@"-o ""{runtimeDirectoryName}"" " +
                        $"-Dlanguage={runtimeInfo.DLanguage} " +
                        $"{(GenerateVisitor ? "-visitor" : "-no-visitor")} " +
                        $"{(GenerateListener ? "-listener" : "-no-listener")}";

                    if (!string.IsNullOrWhiteSpace(PackageName))
                    {
                        arguments += " -package " + PackageName;
                    }
                    else if (Runtime == Runtime.Go)
                    {
                        arguments += " -package main";
                    }

                    if (grammarFileName.Contains(Grammar.LexerPostfix) && state.LexerSuperClass != null)
                    {
                        arguments += " -DsuperClass=" + state.LexerSuperClass;
                    }

                    if (grammarFileName.Contains(Grammar.ParserPostfix) && state.ParserSuperClass != null)
                    {
                        arguments += " -DsuperClass=" + state.ParserSuperClass;
                    }

                    _result.Command = "java " + arguments;
                    processor = new Processor("java", arguments, ".");
                    processor.CancellationToken = cancellationToken;
                    processor.ErrorDataReceived += ParserGeneration_ErrorDataReceived;
                    processor.OutputDataReceived += ParserGeneration_OutputDataReceived;

                    processor.Start();

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (Exception ex)
            {
                _result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    ErrorEvent?.Invoke(this, new ParsingError(ex, WorkflowStage.ParserGenerated));
                }
            }
            finally
            {
                processor?.Dispose();
            }
        }

        private void ParserGeneration_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreJavaError())
            {
                var strs = e.Data.Split(':');
                int line = 1, column = 1;

                int locationIndex = strs.Length > 2 && strs[2].Length > 0 && strs[2][0] == '\\' ? 3 : 2;
                if (strs.Length > locationIndex)
                {
                    if (!int.TryParse(strs[locationIndex], out line))
                    {
                        line = 1;
                    }
                    if (strs.Length > locationIndex + 1)
                    {
                        if (!int.TryParse(strs[locationIndex + 1], out column))
                        {
                            column = 1;
                        }
                    }
                }

                bool isWarning = strs.Length > 0 && strs[0].StartsWith("warning");
                ParsingError error = new ParsingError(line, column, e.Data, _currentGrammarSource, WorkflowStage.ParserGenerated, isWarning);
                ErrorEvent?.Invoke(this, error);
                _result.Errors.Add(error);
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreJavaError())
            {
            }
        }
    }
}