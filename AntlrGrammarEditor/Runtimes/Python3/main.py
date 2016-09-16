import sys;
from antlr4 import *
from AntlrGrammarName42Lexer import AntlrGrammarName42Lexer
from AntlrGrammarName42Parser import AntlrGrammarName42Parser
'''AntlrCaseInsensitive'''

def main(argv):
    code = open('Text', 'r').read()
    codeStream = InputStream(code)
    lexer = AntlrGrammarName42Lexer(codeStream)
    '''not working due to bug in runtime:
    tokens = lexer.getAllTokens()
    tokensSource = ListTokenSource(tokens)
    tokensStream = CommonTokenStream(tokensSource)'''
    tokensStream = CommonTokenStream(lexer)
    parser = AntlrGrammarName42Parser(tokensStream)
    tree = parser.AntlrGrammarRoot42()
    print("Tree ", tree.toStringTree(recog=parser));

if __name__ == '__main__':
    main(sys.argv)