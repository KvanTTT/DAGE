﻿using Antlr4.Runtime;
using System;
using System.IO;
using System.Threading;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class GrammarChecker : StageProcessor
    {
        private GrammarCheckedState _result;

        public GrammarCheckedState Check(InputState inputState, CancellationToken cancellationToken = default)
        {
            var grammar = inputState.Grammar;
            _result = new GrammarCheckedState(inputState);
            try
            {
                var antlrErrorListener = new AntlrErrorListener();
                antlrErrorListener.ErrorEvent += ErrorEvent;
                antlrErrorListener.ErrorEvent += (sender, error) =>
                {
                    lock (_result.Errors)
                    {
                        _result.Errors.Add(error);
                    }
                };

                foreach (string grammarFileName in grammar.Files)
                {
                    ProcessGrammarFile(grammar, grammarFileName, antlrErrorListener, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    ErrorEvent?.Invoke(this, new ParsingError(ex, WorkflowStage.GrammarChecked));
                }
            }

            return _result;
        }

        private void ProcessGrammarFile(Grammar grammar, string grammarFileName,
            AntlrErrorListener antlrErrorListener, CancellationToken cancellationToken)
        {
            string code = File.ReadAllText(Path.Combine(grammar.Directory, grammarFileName));
            var inputStream = new AntlrInputStream(code);
            var codeSource = new CodeSource(grammarFileName, inputStream.ToString());
            _result.GrammarFilesData.Add(grammarFileName, codeSource);

            string extension = Path.GetExtension(grammarFileName);
            if (extension != Grammar.AntlrDotExt)
            {
                return;
            }

            antlrErrorListener.CodeSource = codeSource;
            var antlr4Lexer = new ANTLRv4Lexer(inputStream);
            antlr4Lexer.RemoveErrorListeners();
            antlr4Lexer.AddErrorListener(antlrErrorListener);
            var tokens = antlr4Lexer.GetAllTokens();
            var codeTokenSource = new ListTokenSource(tokens);

            cancellationToken.ThrowIfCancellationRequested();

            var codeTokenStream = new CommonTokenStream(codeTokenSource);
            var antlr4Parser = new ANTLRv4Parser(codeTokenStream);

            antlr4Parser.RemoveErrorListeners();
            antlr4Parser.AddErrorListener(antlrErrorListener);

            var tree = antlr4Parser.grammarSpec();

            var grammarInfoCollectorListener = new GrammarInfoCollectorListener();
            grammarInfoCollectorListener.CollectInfo(antlrErrorListener.CodeSource, tree);

            var shortFileName = Path.GetFileNameWithoutExtension(grammarFileName);
            _result.GrammarActionsTextSpan[grammarFileName] = grammarInfoCollectorListener.CodeInsertions;

            var grammarType = grammarInfoCollectorListener.GrammarType;

            if (grammarType == GrammarType.Lexer || grammarType == GrammarType.Combined)
            {
                _result.LexerSuperClass = grammarInfoCollectorListener.SuperClass;
            }

            if (grammarType == GrammarType.Separated || grammarType == GrammarType.Combined)
            {
                _result.ParserSuperClass = grammarInfoCollectorListener.SuperClass;
                _result.Rules = grammarInfoCollectorListener.Rules;
            }

            void ErrorAction(ParsingError parsingError)
            {
                ErrorEvent?.Invoke(this, parsingError);
                _result.Errors.Add(parsingError);
            }

            var caseInsensitiveTypeOptionMatcher = new CaseInsensitiveTypeOptionMatcher(codeSource, grammarType, ErrorAction);
            var runtimeOptionMatcher = new RuntimeOptionMatcher(codeSource, grammarType, ErrorAction);
            var visitorOptionMatcher = new VisitorOptionMatcher(codeSource, grammarType, ErrorAction);
            var listenerOptionMatcher = new ListenerOptionMatcher(codeSource, grammarType, ErrorAction);
            var packageOptionMatcher = new PackageOptionMatcher(codeSource, grammarType, ErrorAction);
            var rootOptionMatcher = new RootOptionMatcher(codeSource, grammarType, ErrorAction, _result.Rules);
            var predictionOptionMatcher = new PredictionModeOptionMatcher(codeSource, grammarType, ErrorAction);

            foreach (IToken token in tokens)
            {
                if (token.Type == ANTLRv4Lexer.LINE_COMMENT || token.Type == ANTLRv4Lexer.BLOCK_COMMENT)
                {
                    if (caseInsensitiveTypeOptionMatcher.Match(token, out var caseInsensitiveType))
                    {
                        _result.CaseInsensitiveType = caseInsensitiveType;
                        continue;
                    }

                    if (runtimeOptionMatcher.Match(token, out Runtime runtime))
                    {
                        _result.Runtime = runtime;
                        continue;
                    }

                    if (packageOptionMatcher.Match(token, out string package))
                    {
                        _result.Package = package;
                        continue;
                    }

                    if (visitorOptionMatcher.Match(token, out bool generateVisitor))
                    {
                        _result.Visitor = generateVisitor;
                        continue;
                    }

                    if (listenerOptionMatcher.Match(token, out bool generateListener))
                    {
                        _result.Listener = generateListener;
                        continue;
                    }

                    if (rootOptionMatcher.Match(token, out string root))
                    {
                        _result.Root = root;
                        continue;
                    }

                    if (predictionOptionMatcher.Match(token, out PredictionMode predictionMode))
                    {
                        _result.PredictionMode = predictionMode;
                        continue;
                    }
                }
            }
        }
    }
}
