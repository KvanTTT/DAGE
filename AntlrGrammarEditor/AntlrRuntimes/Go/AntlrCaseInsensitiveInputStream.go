package main

import (
    "github.com/antlr/antlr4/runtime/Go/antlr"
    "strings"
)

type AntlrCaseInsensitiveInputStream struct {
    *antlr.InputStream
    lookaheadData []rune
}

func NewAntlrCaseInsensitiveInputStream(input string) *AntlrCaseInsensitiveInputStream {
    caseInsenstiveStream := new(AntlrCaseInsensitiveInputStream)
    caseInsenstiveStream.InputStream = antlr.NewInputStream(input)
    caseInsenstiveStream.lookaheadData = []rune(strings.ToLower(input))
    return caseInsenstiveStream
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