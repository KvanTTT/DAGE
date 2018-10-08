import sys;
from antlr4.InputStream import InputStream
from antlr4.CommonTokenStream import CommonTokenStream
from antlr4.ListTokenSource import ListTokenSource
from __TemplateGrammarName__Lexer import __TemplateGrammarName__Lexer
from __TemplateGrammarName__Parser import __TemplateGrammarName__Parser
'''AntlrCaseInsensitive'''

def main(argv):
    code = open('../Text', 'r').read()
    codeStream = InputStream(code)
    lexer = __TemplateGrammarName__Lexer(codeStream)
    tokens = lexer.getAllTokens()
    tokensSource = ListTokenSource(tokens)
    tokensStream = CommonTokenStream(tokensSource)
    parser = __TemplateGrammarName__Parser(tokensStream)
    tree = parser.__TemplateGrammarRoot__()
    print("Tree ", tree.toStringTree(recog=parser));

if __name__ == '__main__':
    main(sys.argv)