import sys;
from antlr4.InputStream import InputStream
from antlr4.CommonTokenStream import CommonTokenStream
from antlr4.ListTokenSource import ListTokenSource
from AntlrGrammarName42Lexer import AntlrGrammarName42Lexer
from AntlrGrammarName42Parser import AntlrGrammarName42Parser
'''AntlrCaseInsensitive'''

def main(argv):
    code = open('Text', 'r').read()
    codeStream = InputStream(code)
    lexer = AntlrGrammarName42Lexer(codeStream)
    tokens = lexer.getAllTokens()
    tokensSource = ListTokenSource(tokens)
    tokensStream = CommonTokenStream(tokensSource)
    parser = AntlrGrammarName42Parser(tokensStream)
    tree = parser.AntlrGrammarRoot42()
    print "Tree " + tree.toStringTree(recog=parser);

if __name__ == '__main__':
    main(sys.argv)