using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            string rootRule = "";
            string fileName = "Text";
            bool notParse = false;
            if (args.Length > 0)
            {
                rootRule = args[0];
                if (!string.IsNullOrEmpty(args[1]))
                {
                    fileName = args[1];
                }
                notParse = bool.Parse(args[2]);
            }
            var code = System.IO.File.ReadAllText(fileName);
            var codeStream = new AntlrInputStream(code);
            var lexer = new AntlrGrammarName42Lexer(codeStream);

            var stopwatch = Stopwatch.StartNew();
            var tokens = lexer.GetAllTokens();
            stopwatch.Stop();
            Console.WriteLine("LexerTime {0}", stopwatch.Elapsed);
            Console.WriteLine("Tokens {0}", TokensToString(tokens));

            if (!notParse)
            {
                var tokensSource = new ListTokenSource(tokens);
                var tokensStream = new CommonTokenStream(tokensSource);
                var parser = new AntlrGrammarName42Parser(tokensStream);

                stopwatch.Restart();
                ParserRuleContext ast;
                if (string.IsNullOrEmpty(rootRule))
                {
                    ast = parser.AntlrGrammarRoot42();
                }
                else
                {
                    var rootMethod = parser.GetType().GetMethod(rootRule);
                    ast = (ParserRuleContext)rootMethod.Invoke(parser, new object[0]);
                }
                stopwatch.Stop();
                Console.WriteLine("ParserTime {0}", stopwatch.Elapsed);

                var stringTree = ast.ToStringTree(parser);
                Console.WriteLine("Tree {0}", stringTree);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString().Replace("\r", "").Replace("\n", ""));
        }
    }

    static string TokensToString(IList<IToken> tokens)
    {
        var resultString = new StringBuilder();
        foreach (var token in tokens)
        {
            string symbolicName = AntlrGrammarName42Lexer.DefaultVocabulary.GetSymbolicName(token.Type);
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
