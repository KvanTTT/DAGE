var antlr4 = require('antlr4/index');
var AntlrGrammarName42Lexer = require('./AntlrGrammarName42Lexer');
var AntlrGrammarName42Parser = require('./AntlrGrammarName42Parser');
var fs = require("fs");

var input = fs.readFileSync("./Text").toString();
var chars = new antlr4.InputStream(input);
var lexer = new AntlrGrammarName42Lexer.AntlrGrammarName42Lexer(chars);
var tokens = new antlr4.CommonTokenStream(lexer);
var parser = new AntlrGrammarName42Parser.AntlrGrammarName42Parser(tokens);
parser.buildParseTrees = true;
var ast = parser.AntlrGrammarRoot42();
console.log("Tree " + ast.toStringTree(null, parser));