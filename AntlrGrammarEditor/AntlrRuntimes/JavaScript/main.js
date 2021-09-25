import fs from 'fs';
import antlr4 from 'antlr4';
import PredictionMode from 'antlr4/atn/PredictionMode.js';
import __TemplateLexerName__ from './__TemplateLexerName__.js';
/*$ParserInclude*/import __TemplateParserName__ from './__TemplateParserName__.js';/*ParserInclude$*/
/*$AntlrCaseInsensitive*/import AntlrCaseInsensitiveInputStream from './AntlrCaseInsensitiveInputStream.js';/*AntlrCaseInsensitive$*/

var fileName = "../../Text"

if (process.argv.length > 2) {
    fileName = process.argv[2];
}

var input = fs.readFileSync(fileName).toString();
var chars = new antlr4.InputStream(input);
var lexer = new __TemplateLexerName__(chars);
var tokensStream = new antlr4.CommonTokenStream(lexer);

/*$ParserPart*/
var rootRule;
var mode = "ll";

if (process.argv.length > 3) {
    rootRule = process.argv[3];
    if (process.argv.length > 4) {
        // TODO: onlyTokenize parameter
        if (process.argv.length > 5) {
            mode = process.argv[5].toLowerCase();
        }
    }
}
var parser = new __TemplateParserName__(tokensStream);
parser._interp.predictionMode = mode === "sll"
    ? PredictionMode.PredictionMode.SLL
    : mode === "ll"
        ? PredictionMode.PredictionMode.LL
        : PredictionMode.PredictionMode.LL_EXACT_AMBIG_DETECTION;
var ruleName = rootRule === undefined ? parser.ruleNames[0] : rootRule;
var ast = parser[ruleName]();
console.log("Tree " + ast.toStringTree(null, parser));
/*ParserPart$*/