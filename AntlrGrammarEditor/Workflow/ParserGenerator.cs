using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class ParserGenerator : StageProcessor
    {
        private CodeSource _currentGrammarSource;
        private ParserGeneratedState _result;

        public ParserGeneratedState Generate(GrammarCheckedState state,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Grammar grammar = state.InputState.Grammar;

            _result = new ParserGeneratedState(state);
            Processor processor = null;
            try
            {
                string runtimeDirectoryName = Path.Combine(ParserCompiler.HelperDirectoryName, grammar.Name, grammar.MainRuntime.ToString());
                if (Directory.Exists(runtimeDirectoryName))
                {
                    Directory.Delete(runtimeDirectoryName, true);
                }
                Directory.CreateDirectory(runtimeDirectoryName);

                cancellationToken.ThrowIfCancellationRequested();

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(grammar.MainRuntime);

                var jarGenerator = Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (string grammarFileName in state.InputState.Grammar.Files)
                {
                    _currentGrammarSource = state.GrammarFilesData[grammarFileName];
                    var arguments = $@"-jar ""{jarGenerator}"" ""{Path.Combine(grammar.GrammarPath, grammarFileName)}"" -o ""{runtimeDirectoryName}"" " +
                        $"-Dlanguage={runtimeInfo.DLanguage} -no-visitor -no-listener";
                    if (grammar.MainRuntime == Runtime.Go)
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
            return _result;
        }
        
        private void ParserGeneration_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
            {
                var strs = e.Data.Split(':');
                ParsingError error;
                int line = 1, column = 1;
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
                error = new ParsingError(line, column, e.Data, _currentGrammarSource, WorkflowStage.ParserGenerated);
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