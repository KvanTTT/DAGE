import org.antlr.v4.runtime.*;
import java.lang.reflect.Method;
import java.util.List;

public class Main {
    public static void main(String[] args) {
        try {
            String fileName = "../../Text";

            if (args.length > 0) {
                fileName = args[0];
            }

            CharStream codeStream = CharStreams.fromFileName(fileName);
            __TemplateGrammarName__Lexer lexer = new __TemplateGrammarName__Lexer(codeStream);
            List<? extends Token> tokens = lexer.getAllTokens();

/*$ParserPart*/
            String rootRule = null;
            if (args.length > 1) {
                rootRule = args[1];
            }

            ListTokenSource tokensSource = new ListTokenSource(tokens);
            CommonTokenStream tokensStream = new CommonTokenStream(tokensSource);
            __TemplateGrammarName__Parser parser = new __TemplateGrammarName__Parser(tokensStream);
            String ruleName = rootRule == null ? __TemplateGrammarName__Parser.ruleNames[0] : rootRule;
            Method parseMethod = __TemplateGrammarName__Parser.class.getDeclaredMethod(ruleName);
            ParserRuleContext ast = (ParserRuleContext)parseMethod.invoke(parser);
            String stringTree = ast.toStringTree(parser);
            System.out.print("Tree " + stringTree);
/*ParserPart$*/
        }
        catch (Exception e) {
        }
    }
}
