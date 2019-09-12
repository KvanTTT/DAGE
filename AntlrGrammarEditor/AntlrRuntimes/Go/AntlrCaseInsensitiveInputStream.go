package main

import (
    "github.com/antlr/antlr4/runtime/Go/antlr"
    "strings"
)

type AntlrCaseInsensitiveInputStream struct {
    *antlr.InputStream
    lookaheadData []rune
}

func NewAntlrCaseInsensitiveInputStream(input string, lowerCase bool) *AntlrCaseInsensitiveInputStream {
    caseInsensitiveStream := new(AntlrCaseInsensitiveInputStream)
    caseInsensitiveStream.InputStream = antlr.NewInputStream(input)
    if (lowerCase) {
        caseInsensitiveStream.lookaheadData = []rune(strings.ToLower(input))
    } else {
        caseInsensitiveStream.lookaheadData = []rune(strings.ToUpper(input))
    }
    return caseInsensitiveStream
}

func (is *AntlrCaseInsensitiveInputStream) LA(offset int) int {
    if offset == 0 {
        return 0 // nil
    }
    if offset < 0 {
        offset++ // e.g., translate LA(-1) to use offset=0
    }
    var pos = is.Index() + offset - 1

    if pos < 0 || pos >= is.Size() { // invalid
        return antlr.TokenEOF
    }

    return int(is.lookaheadData[pos])
}