import org.antlr.v4.runtime.*;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.List;

public class Main {
    public static void main(String[] args) {
        try {
            CharStream codeStream = CharStreams.fromFileName(args[0]);
            AntlrGrammarName42Lexer lexer = new AntlrGrammarName42Lexer(codeStream);
            List<? extends Token> tokens = lexer.getAllTokens();
            ListTokenSource tokensSource = new ListTokenSource(tokens);
            CommonTokenStream tokensStream = new CommonTokenStream(tokensSource);
            AntlrGrammarName42Parser parser = new AntlrGrammarName42Parser(tokensStream);
            ParserRuleContext ast = parser.AntlrGrammarRoot42();
            String stringTree = ast.toStringTree(parser);
            System.out.print("Tree " + stringTree);
        }
        catch (IOException e) {
        }
    }
}
