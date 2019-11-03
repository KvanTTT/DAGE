import { ANTLRInputStream, CommonTokenStream } from 'antlr4ts';
/*AntlrCaseInsensitive*/

// Create the lexer and parser
let inputStream = new ANTLRInputStream("text");
let lexer = new __TemplateGrammarName__Lexer(inputStream);
let tokenStream = new CommonTokenStream(lexer);
let parser = new __TemplateGrammarName__Parser(tokenStream);

let result = parser.__TemplateGrammarRoot__();