﻿using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
/*$PackageName*/using __PackageName__;/*PackageName$*/

class Program
{
    static void Main(string[] args)
    {
        try
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            string fileName = "../../../../Text";

            if (args.Length > 0)
            {
                fileName = args[0];
            }

            var code = File.ReadAllText(fileName);
            var codeStream = new AntlrInputStream(code);
            var lexer = new __TemplateLexerName__(codeStream);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new LexerErrorListener());

            var stopwatch = Stopwatch.StartNew();
            var tokens = lexer.GetAllTokens();
            stopwatch.Stop();
            Console.WriteLine("LexerTime {0}", stopwatch.Elapsed);
            Console.WriteLine("Tokens {0}", tokens.TokensToString());

/*$ParserPart*/
            string rootRule = null;
            bool notParse = false;
            string predictionMode = "ll";

            if (args.Length > 1)
            {
                rootRule = args[1];
                if (args.Length > 2)
                {
                    bool.TryParse(args[2], out notParse);
                    if (args.Length > 3)
                    {
                        predictionMode = args[3].ToLowerInvariant();
                    }
                }
            }

            if (!notParse)
            {
                var tokensSource = new ListTokenSource(tokens);
                var tokensStream = new CommonTokenStream(tokensSource);
                var parser = new __TemplateParserName__(tokensStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new ParserErrorListener());
                parser.Interpreter.PredictionMode = predictionMode == "sll"
                        ? PredictionMode.SLL
                        : predictionMode == "ll"
                            ? PredictionMode.LL
                            : PredictionMode.LL_EXACT_AMBIG_DETECTION
                            ;

                stopwatch.Restart();
                string ruleName = rootRule == null ? __TemplateParserName__.ruleNames[0] : rootRule;
                var rootMethod = typeof(__TemplateParserName__).GetMethod(ruleName);
                var ast = (ParserRuleContext)rootMethod.Invoke(parser, new object[0]);
                stopwatch.Stop();

                Console.WriteLine("ParserTime {0}", stopwatch.Elapsed);
                Console.WriteLine("Tree {0}", ast.ToStringTree(parser));
            }
/*ParserPart$*/
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString().Replace("\r", "").Replace("\n", ""));
        }
    }

    class LexerErrorListener : IAntlrErrorListener<int>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Console.Error.WriteLine($"line {line}:{charPositionInLine} {msg}");
        }
    }

    class ParserErrorListener : IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Console.Error.WriteLine($"line {line}:{charPositionInLine} {msg}");
        }
    }
}

public static class ParseTreeFormatter
{
    public static string TokensToString(this IList<IToken> tokens)
    {
        var resultString = new StringBuilder();
        foreach (var token in tokens)
        {
            string symbolicName = __TemplateLexerName__.DefaultVocabulary.GetSymbolicName(token.Type);
            string value = token.Text ?? "";
            value = value.Replace("\r", "").Replace("\n", "");
            if (string.Compare(symbolicName, value, StringComparison.OrdinalIgnoreCase) != 0)
            {
                symbolicName += "(";
                if (value.Length <= 8)
                {
                    symbolicName += value;
                }
                else
                {
                    symbolicName += value.Substring(0, 8) + "...";
                }
                symbolicName += ")";
            }
            symbolicName += " ";
            resultString.Append(symbolicName);
        }
        resultString.Append("EOF");
        return resultString.ToString();
    }
}