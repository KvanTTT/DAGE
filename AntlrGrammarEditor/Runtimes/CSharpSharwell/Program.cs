using System;
using Antlr4.Runtime;

class Program
{
    static void Main(string[] args)
    {
        var code = System.IO.File.ReadAllText(args[0]);
        var codeStream = new AntlrInputStream(code);
        var lexer = new AntlrGrammarName42Lexer(codeStream);
        var tokens = lexer.GetAllTokens();
        var tokensSource = new ListTokenSource(tokens);
        var tokensStream = new CommonTokenStream(tokensSource);
        var parser = new AntlrGrammarName42Parser(tokensStream);
        var ast = parser.AntlrGrammarRoot42();
        var stringTree = ast.ToStringTree(parser);
        Console.Write(stringTree);
    }
}
