import org.antlr.v4.runtime.*;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.List;

public class Main {

    public static void main(String[] args) {
        try {
            String code = readFile(args[0]);
            ANTLRInputStream codeStream = new ANTLRInputStream(code);
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

    public static String readFile(String filename) throws IOException {
        String content = null;
        File file = new File(filename);
        FileReader reader = null;
        try {
            reader = new FileReader(file);
            char[] chars = new char[(int) file.length()];
            reader.read(chars);
            content = new String(chars);
            reader.close();
        } catch (IOException e) {
            e.printStackTrace();
        } finally {
            if(reader !=null) {
                reader.close();
            }
        }
        return content;
    }
}
