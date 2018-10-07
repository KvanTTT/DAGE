using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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
            string fileName = "../../../../Text";
            bool notParse = false;
            bool indented = false;
            if (args.Length > 0)
            {
                rootRule = args[0];
                if (!string.IsNullOrEmpty(args[1]))
                {
                    fileName = args[1];
                }
                notParse = bool.Parse(args[2]);
                indented = bool.Parse(args[3]);
            }
            var code = System.IO.File.ReadAllText(fileName);
            var codeStream = new AntlrInputStream(code);
            var lexer = new __TemplateGrammarName__Lexer(codeStream);
            lexer.AddErrorListener(new LexerErrorListener());

            var stopwatch = Stopwatch.StartNew();
            var tokens = lexer.GetAllTokens();
            stopwatch.Stop();
            Console.WriteLine("LexerTime {0}", stopwatch.Elapsed);
            Console.WriteLine("Tokens {0}", tokens.TokensToString());

            if (!notParse)
            {
                var tokensSource = new ListTokenSource(tokens);
                var tokensStream = new CommonTokenStream(tokensSource);
                var parser = new __TemplateGrammarName__Parser(tokensStream);
                parser.AddErrorListener(new ParserErrorListener());

                stopwatch.Restart();
                ParserRuleContext ast;
                if (string.IsNullOrEmpty(rootRule))
                {
                    ast = parser.__TemplateGrammarRoot__();
                }
                else
                {
                    var rootMethod = parser.GetType().GetMethod(rootRule);
                    ast = (ParserRuleContext)rootMethod.Invoke(parser, new object[0]);
                }
                stopwatch.Stop();
                Console.WriteLine("ParserTime {0}", stopwatch.Elapsed);

                var stringTree = indented
                    ? ast.ToStringTreeIndented(parser)
                    : ast.ToStringTree(parser);
                Console.WriteLine("Tree {0}", stringTree);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString().Replace("\r", "").Replace("\n", ""));
        }
    }

    class LexerErrorListener : IAntlrErrorListener<int>
    {
        public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] int offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
        {
            Console.Error.WriteLine($"line {line}:{charPositionInLine} {msg}");
        }
    }

    class ParserErrorListener : IAntlrErrorListener<IToken>
    {
        public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] IToken offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
        {
            Console.Error.WriteLine($"line {line}:{charPositionInLine} {msg}");
        }
    }
}

public static class ParseTreeFormatter
{
    public const int IndentSize = 2;

    public static string TokensToString(this IList<IToken> tokens)
    {
        var resultString = new StringBuilder();
        foreach (var token in tokens)
        {
            string symbolicName = __TemplateGrammarName__Lexer.DefaultVocabulary.GetSymbolicName(token.Type);
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

    public static string ToStringTreeIndented(this IParseTree parseTree, Parser parser)
    {
        var result = new StringBuilder();
        parseTree.ToStringTreeIndented(parser, result, 0);
        return result.ToString();
    }

    private static void ToStringTreeIndented(this IParseTree parseTree, Parser parser, StringBuilder builder, int level)
    {
        string currentLevelIndentString = string.Empty.PadLeft(level * IndentSize);
        builder.Append(currentLevelIndentString);
        var ruleContext = parseTree as RuleContext;
        if (ruleContext != null)
        {
            builder.Append("(" + parser.RuleNames[ruleContext.RuleIndex] + "\\n");

            for (int i = 0; i < ruleContext.ChildCount; i++)
            {
                ruleContext.GetChild(i).ToStringTreeIndented(parser, builder, level + 1);
                builder.Append("\\n");
            }

            builder.Append(currentLevelIndentString);
            builder.Append(")");
        }
        else
        {
            builder.Append('\'' + parseTree.GetText()
                .Replace("'", "\\'").Replace("\r", "").Replace("\n", "") + '\'');
        }
    }
}