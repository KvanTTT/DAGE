import org.antlr.v4.runtime.*;
import org.antlr.v4.runtime.atn.PredictionMode;
import java.lang.reflect.Method;
import java.util.List;
/*$PackageName*/import __PackageName__.*;/*PackageName$*/

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
            String mode = "ll";

            if (args.length > 1) {
                rootRule = args[1];
                if (args.length > 2) {
                    // TODO: process OnlyTokenize parameter
                    if (args.length > 3) {
                        mode = args[3].toLowerCase();
                    }
                }
            }

            ListTokenSource tokensSource = new ListTokenSource(tokens);
            CommonTokenStream tokensStream = new CommonTokenStream(tokensSource);
            __TemplateGrammarName__Parser parser = new __TemplateGrammarName__Parser(tokensStream);
            PredictionMode predictionMode = mode.equals("sll")
                ? PredictionMode.SLL
                : mode.equals("ll")
                    ? PredictionMode.LL
                    : PredictionMode.LL_EXACT_AMBIG_DETECTION;
            parser.getInterpreter().setPredictionMode(predictionMode);
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
