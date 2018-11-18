using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class GrammarChecker : StageProcessor
    {
        public GrammarCheckedState Check(InputState inputState,
            CancellationToken cancellationToken = default(CancellationToken)) 
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
                    string code = File.ReadAllText(Path.Combine(grammar.GrammarPath, grammarFileName));
                    var inputStream = new AntlrInputStream(code);
                    antlrErrorListener.CodeSource = new CodeSource(grammarFileName, inputStream.ToString());
                    result.GrammarFilesData[grammarFileName] = antlrErrorListener.CodeSource;
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

                    if (!shortFileName.Contains(GrammarFactory.LexerPostfix))
                    {
                        string root;
                        bool preprocessor;
                        List<string> rules;
                        if (!shortFileName.Contains(GrammarFactory.PreprocessorPostfix))
                        {
                            result.Rules = grammarInfoCollectorListener.Rules;
                            rules = result.Rules;
                            root = grammar.Root;
                            preprocessor = false;
                        }
                        else
                        {
                            result.PreprocessorRules = grammarInfoCollectorListener.Rules;
                            rules = result.PreprocessorRules;
                            root = grammar.PreprocessorRoot;
                            preprocessor = true;
                        }
                        if (rules.Count > 0 && !rules.Contains(root))
                        {
                            root = rules[0];
                            if (!preprocessor)
                            {
                                grammar.Root = root;
                            }
                            else
                            {
                                grammar.PreprocessorRoot = root;
                            }
                        }

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
