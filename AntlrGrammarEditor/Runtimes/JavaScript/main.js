var fs = require("fs");
var antlr4 = require('antlr4/index');
var __TemplateGrammarName__Lexer = require('./__TemplateGrammarName__Lexer').__TemplateGrammarName__Lexer;
var __TemplateGrammarName__Parser = require('./__TemplateGrammarName__Parser').__TemplateGrammarName__Parser;
/*AntlrCaseInsensitive*/

var input = fs.readFileSync("../Text").toString();
var chars = new antlr4.InputStream(input);
var lexer = new __TemplateGrammarName__Lexer(chars);
var tokens = new antlr4.CommonTokenStream(lexer);
var parser = new __TemplateGrammarName__Parser(tokens);
parser.buildParseTrees = true;
var ast = parser.__TemplateGrammarRoot__();
console.log("Tree " + ast.toStringTree(null, parser));