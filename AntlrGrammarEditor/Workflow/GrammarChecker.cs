using Antlr4.Runtime;
using System;
using System.IO;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class GrammarChecker : StageProcessor
    {
        public GrammarCheckedState Check(InputState inputState, CancellationToken cancellationToken = default)
        {
            var grammar = inputState.Grammar;
            var result = new GrammarCheckedState(inputState);
            try
            {
                var antlrErrorListener = new AntlrErrorListener();
                antlrErrorListener.ErrorEvent += ErrorEvent;
                antlrErrorListener.ErrorEvent += (sender, error) =>
                {
                    lock (result.Errors)
                    {
                        result.Errors.Add(error);
                    }
                };

                foreach (string grammarFileName in grammar.Files)
                {
                    string code = File.ReadAllText(Path.Combine(grammar.Directory, grammarFileName));
                    var inputStream = new AntlrInputStream(code);
                    var codeSource = new CodeSource(grammarFileName, inputStream.ToString());
                    result.GrammarFilesData.Add(grammarFileName, codeSource);

                    string extension = Path.GetExtension(grammarFileName);
                    if (extension != Grammar.AntlrDotExt)
                    {
                        continue;
                    }

                    antlrErrorListener.CodeSource = codeSource;
                    var antlr4Lexer = new ANTLRv4Lexer(inputStream);
                    antlr4Lexer.RemoveErrorListeners();
                    antlr4Lexer.AddErrorListener(antlrErrorListener);
                    var codeTokenSource = new ListTokenSource(antlr4Lexer.GetAllTokens());

                    cancellationToken.ThrowIfCancellationRequested();

                    var codeTokenStream = new CommonTokenStream(codeTokenSource);
                    var antlr4Parser = new ANTLRv4Parser(codeTokenStream);

                    antlr4Parser.RemoveErrorListeners();
                    antlr4Parser.AddErrorListener(antlrErrorListener);

                    var tree = antlr4Parser.grammarSpec();

                    var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
                    grammarInfoCollectorListener.CollectInfo(antlrErrorListener.CodeSource, tree);

                    var shortFileName = Path.GetFileNameWithoutExtension(grammarFileName);
                    result.GrammarActionsTextSpan[grammarFileName] = grammarInfoCollectorListener.CodeInsertions;

                    if (grammarFileName.Contains(Grammar.LexerPostfix))
                    {
                        grammar.LexerSuperClass = grammarInfoCollectorListener.SuperClass;
                    }

                    if (grammarFileName.Contains(Grammar.ParserPostfix))
                    {
                        grammar.ParserSuperClass = grammarInfoCollectorListener.SuperClass;
                    }

                    if (!shortFileName.Contains(Grammar.LexerPostfix))
                    {
                        result.Rules = grammarInfoCollectorListener.Rules;
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    ErrorEvent?.Invoke(this, new ParsingError(ex, WorkflowStage.GrammarChecked));
                }
            }

            return result;
        }
    }
}
