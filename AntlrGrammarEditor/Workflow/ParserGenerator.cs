using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class ParserGenerator : StageProcessor
    {
        public const string HelperDirectoryName = "DageHelperDirectory";

        private CodeSource _currentGrammarSource;
        private ParserGeneratedState _result;

        public bool GenerateListener { get; set; } = true;

        public bool GenerateVisitor { get; set; } = true;

        public string GeneratorTool { get; set; }

        public ParserGeneratedState Generate(GrammarCheckedState state,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Grammar grammar = state.InputState.Grammar;

            _result = new ParserGeneratedState(state, GenerateListener, GenerateVisitor);

            foreach (Runtime runtime in grammar.Runtimes)
            {
                Generate(grammar, runtime, state, cancellationToken);
            }

            return _result;
        }

        private void Generate(Grammar grammar, Runtime runtime, GrammarCheckedState state, CancellationToken cancellationToken)
        {
            Processor processor = null;
            try
            {
                string runtimeDirectoryName = Path.Combine(HelperDirectoryName, grammar.Name, runtime.ToString());
                if (Directory.Exists(runtimeDirectoryName))
                {
                    Directory.Delete(runtimeDirectoryName, true);
                }

                Directory.CreateDirectory(runtimeDirectoryName);

                cancellationToken.ThrowIfCancellationRequested();

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);

                var jarGenerator = GeneratorTool ?? Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (string grammarFileName in state.InputState.Grammar.Files)
                {
                    _currentGrammarSource = state.GrammarFilesData[grammarFileName];
                    var arguments =
                        $@"-jar ""{jarGenerator}"" ""{Path.Combine(grammar.Directory, grammarFileName)}"" " +
                        $@"-o ""{runtimeDirectoryName}"" " +
                        $"-Dlanguage={runtimeInfo.DLanguage} " +
                        $"{(GenerateVisitor ? "-visitor" : "-no-visitor")} " + 
                        $"{(GenerateListener ? "-listener" : "-no-listener")}";
                    if (runtime == Runtime.Go)
                    {
                        arguments += " -package main";
                    }

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
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
            {
                var strs = e.Data.Split(':');
                int line = 1, column = 1;
                bool warning = false;
                if (strs.Length >= 4)
                {
                    if (!int.TryParse(strs[2], out line))
                    {
                        line = 1;
                    }
                    if (!int.TryParse(strs[3], out column))
                    {
                        column = 1;
                    }
                }
                ParsingError error = new ParsingError(line, column, e.Data, _currentGrammarSource, WorkflowStage.ParserGenerated);
                if (strs.Length > 0 && strs[0].StartsWith("warning"))
                {
                    error.IsWarning = true;
                }
                ErrorEvent?.Invoke(this, error);
                _result.Errors.Add(error);
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
            {
            }
        }
    }
}