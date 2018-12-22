import sys;
from antlr4.InputStream import InputStream
from antlr4.CommonTokenStream import CommonTokenStream
from antlr4.ListTokenSource import ListTokenSource
from __TemplateGrammarName__Lexer import __TemplateGrammarName__Lexer
'''$ParserInclude'''from __TemplateGrammarName__Parser import __TemplateGrammarName__Parser'''ParserInclude$'''
'''AntlrCaseInsensitive'''

def main(argv):
    fileName = '../../Text'

    if len(argv) > 0:
        fileName = argv[1]
        if len(argv) > 1:
            rootRule = argv[2]

    code = open(fileName, 'r').read()
    codeStream = InputStream(code)
    lexer = __TemplateGrammarName__Lexer(codeStream)
    tokens = lexer.getAllTokens()
    tokensSource = ListTokenSource(tokens)
    tokensStream = CommonTokenStream(tokensSource)
'''$ParserPart'''
    parser = __TemplateGrammarName__Parser(tokensStream)
    ruleName = __TemplateGrammarName__Parser.ruleNames[0] if rootRule is None else rootRule
    tree = getattr(parser, ruleName)()
    '''PrintTree'''
'''ParserPart$'''

if __name__ == '__main__':
    main(sys.argv)