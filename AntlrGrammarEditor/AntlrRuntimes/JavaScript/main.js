var fs = require("fs");
var antlr4 = require('antlr4/index');
var __TemplateGrammarName__Lexer = require('./__TemplateGrammarName__Lexer').__TemplateGrammarName__Lexer;
var __TemplateGrammarName__Parser = require('./__TemplateGrammarName__Parser').__TemplateGrammarName__Parser;
/*AntlrCaseInsensitive*/

var fileName = "../../Text"
var rootRule;

if (process.argv.length >= 2) {
    fileName = process.argv[2];
    if (process.argv.length >= 3) {
        rootRule = process.argv[3];
    }
}

var input = fs.readFileSync(fileName).toString();
var chars = new antlr4.InputStream(input);
var lexer = new __TemplateGrammarName__Lexer(chars);
var tokens = new antlr4.CommonTokenStream(lexer);
var parser = new __TemplateGrammarName__Parser(tokens);
var ruleName = rootRule === undefined ? parser.ruleNames[0] : rootRule;
var ast = parser[ruleName]();
console.log("Tree " + ast.toStringTree(null, parser));