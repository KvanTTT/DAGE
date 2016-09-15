import sys;
from antlr4 import *
from AntlrGrammarName42Lexer import AntlrGrammarName42Lexer
from AntlrGrammarName42Parser import AntlrGrammarName42Parser

def main(argv):
    input = FileStream("Text")
    lexer = AntlrGrammarName42Lexer(input)
    stream = CommonTokenStream(lexer)
    parser = AntlrGrammarName42Parser(stream)
    tree = parser.AntlrGrammarRoot42()
    print("Tree ", tree.toStringTree(recog=parser));

if __name__ == '__main__':
    main(sys.argv)