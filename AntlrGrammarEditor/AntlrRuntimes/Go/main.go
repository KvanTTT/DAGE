package main

import (
    "io/ioutil"
    "github.com/antlr/antlr4/runtime/Go/antlr"
    "fmt"
)

func main() {
    bytes, err := ioutil.ReadFile("../../Text")
    if err != nil {
        fmt.Print(err)
    }
    code := string(bytes)
    codeStream := antlr.NewInputStream(code)
    lexer := New__TemplateGrammarName__Lexer(codeStream)
    tokensStream := antlr.NewCommonTokenStream(lexer, 0)
    parser := New__TemplateGrammarName__Parser(tokensStream)
    tree := parser.__TemplateGrammarRoot__()
    fmt.Print("Tree " + tree.ToStringTree(nil, parser))
}