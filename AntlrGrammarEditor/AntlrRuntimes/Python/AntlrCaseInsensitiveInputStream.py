from antlr4 import *

class AntlrCaseInsensitiveInputStream(InputStream):

    def __init__(self, input''': str'''):
        super('''SuperCall''').__init__(input)
        inputLower = input.lower()
        self._lookaheadData = [ord(c) for c in inputLower]

    def LA(self, offset''': int'''):
        if offset == 0:
            return 0 # undefined
        if offset < 0:
            offset += 1 # e.g., translate LA(-1) to use offset=0
        pos = self._index + offset - 1
        if pos < 0 or pos >= self._size: # invalid
            return Token.EOF
        return self._lookaheadData[pos]
