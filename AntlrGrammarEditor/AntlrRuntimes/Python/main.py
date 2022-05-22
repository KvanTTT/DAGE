import sys
from antlr4.InputStream import InputStream
from antlr4.CommonTokenStream import CommonTokenStream
from antlr4.ListTokenSource import ListTokenSource
from antlr4.atn.PredictionMode import PredictionMode
from __TemplateLexerName__ import __TemplateLexerName__
'''$ParserInclude'''from __TemplateParserName__ import __TemplateParserName__'''ParserInclude$'''

def main(argv):
    file_name = '../../Text'

    argv_len = len(argv)
    if argv_len > 1:
        file_name = argv[1]

    code = open(file_name, 'r', encoding="utf-8").read()
    code_stream = InputStream(code)
    lexer = __TemplateLexerName__(code_stream)
    tokens = lexer.getAllTokens()

'''$ParserPart'''
    root_rule = None
    mode = 'll'
    if argv_len > 2:
        root_rule = argv[2]
        if argv_len > 3:
            # TODO: onlyTokenize parameter processing
            if argv_len > 4:
                mode = argv[4].lower()

    tokens_source = ListTokenSource(tokens)
    tokens_stream = CommonTokenStream(tokens_source)
    parser = __TemplateParserName__(tokens_stream)
    parser._interp.predictionMode = PredictionMode.SLL if mode == "sll" else PredictionMode.LL if mode == "ll" else PredictionMode.LL_EXACT_AMBIG_DETECTION
    ruleName = __TemplateParserName__.ruleNames[0] if root_rule is None else root_rule
    tree = getattr(parser, ruleName)()
    print("Tree ", tree.toStringTree(recog=parser))
'''ParserPart$'''

if __name__ == '__main__':
    main(sys.argv)