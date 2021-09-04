from antlr4 import *


class AntlrCaseInsensitiveInputStream(InputStream):

    def __init__(self, input_str: str, lower_case: bool):
        super().__init__(input_str)
        input_normalized = input_str.lower() if lower_case else input_str.upper()
        self._lookaheadData = [ord(c) for c in input_normalized]

    def LA(self, offset: int):
        if offset == 0:
            return 0  # undefined
        if offset < 0:
            offset += 1  # e.g., translate LA(-1) to use offset=0
        pos = self._index + offset - 1
        if pos < 0 or pos >= self._size:  # invalid
            return Token.EOF
        return self._lookaheadData[pos]
