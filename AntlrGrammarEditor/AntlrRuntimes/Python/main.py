import sys;
from antlr4.InputStream import InputStream
from antlr4.CommonTokenStream import CommonTokenStream
from antlr4.ListTokenSource import ListTokenSource
from antlr4.atn.PredictionMode import PredictionMode
from __TemplateGrammarName__Lexer import __TemplateGrammarName__Lexer
'''$ParserInclude'''from __TemplateGrammarName__Parser import __TemplateGrammarName__Parser'''ParserInclude$'''
'''$AntlrCaseInsensitive$'''

def main(argv):
    fileName = '../../Text'

    argvLen = len(argv)
    if argvLen > 0:
        fileName = argv[1]

    code = open(fileName, 'r').read()
    codeStream = InputStream(code)
    lexer = __TemplateGrammarName__Lexer(codeStream)
    tokens = lexer.getAllTokens()

'''$ParserPart'''
    mode = 'll'
    if argvLen > 1:
        rootRule = argv[2]
        if argvLen > 2:
            # TODO: onlyTokenize parameter processing
            if argvLen > 3:
                mode = argv[4].lower()

    tokensSource = ListTokenSource(tokens)
    tokensStream = CommonTokenStream(tokensSource)
    parser = __TemplateGrammarName__Parser(tokensStream)
    parser._interp.predictionMode = PredictionMode.SLL if mode == "sll" else PredictionMode.LL if mode == "ll" else PredictionMode.LL_EXACT_AMBIG_DETECTION
    ruleName = __TemplateGrammarName__Parser.ruleNames[0] if rootRule is None else rootRule
    tree = getattr(parser, ruleName)()
    '''$PrintTree$'''
'''ParserPart$'''

if __name__ == '__main__':
    main(sys.argv)