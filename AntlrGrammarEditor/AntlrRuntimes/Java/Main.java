import org.antlr.v4.runtime.*;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.List;

public class Main {
    public static void main(String[] args) {
        try {
            CharStream codeStream = CharStreams.fromFileName(args[0]);
            __TemplateGrammarName__Lexer lexer = new __TemplateGrammarName__Lexer(codeStream);
            List<? extends Token> tokens = lexer.getAllTokens();
            ListTokenSource tokensSource = new ListTokenSource(tokens);
            CommonTokenStream tokensStream = new CommonTokenStream(tokensSource);
            __TemplateGrammarName__Parser parser = new __TemplateGrammarName__Parser(tokensStream);
            ParserRuleContext ast = parser.__TemplateGrammarRoot__();
            String stringTree = ast.toStringTree(parser);
            System.out.print("Tree " + stringTree);
        }
        catch (IOException e) {
        }
    }
}
