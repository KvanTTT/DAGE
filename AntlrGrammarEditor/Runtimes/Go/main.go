package main

import (
    "io/ioutil"
    "github.com/pboyer/antlr4/runtime/Go/antlr"
    "fmt"
)

func main() {
    bytes, err := ioutil.ReadFile("Text")
    if err != nil {
        fmt.Print(err)
    }
    code := string(bytes)
    codeStream := antlr.NewInputStream(code)
    lexer := NewAntlrGrammarName42Lexer(codeStream)
    tokensStream := antlr.NewCommonTokenStream(lexer, 0)
    parser := NewAntlrGrammarName42Parser(tokensStream)
    tree := parser.AntlrGrammarRoot42()
    fmt.Print("Tree " + tree.ToStringTree(nil, parser))
}