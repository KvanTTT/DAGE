package main

import (
	"fmt"
	"io/ioutil"
	"os"

/*$ParserInclude*/
	"reflect"
	"strings"
/*ParserInclude$*/

	"github.com/antlr/antlr4/runtime/Go/antlr"
)


func main() {
	fileName := "../../Text"

	if len(os.Args) > 1 {
		fileName = os.Args[1]
	}

	bytes, err := ioutil.ReadFile(fileName)
	if err != nil {
		fmt.Print(err)
	}

	code := string(bytes)
	codeStream := antlr.NewInputStream(code)
	lexer := New__TemplateGrammarName__Lexer(codeStream)
	tokensStream := antlr.NewCommonTokenStream(lexer, 0)
	tokensStream.Fill()
	allTokens := tokensStream.GetAllTokens()

	_ = tokensStream
	_ = allTokens
/*$ParserPart*/
	rootRule := ""

	if len(os.Args) > 2 {
		rootRule = os.Args[2]
	}

	parser := New__TemplateGrammarName__Parser(tokensStream)

	var ruleName string
	if rootRule == "" {
		ruleName = parser.RuleNames[0]
	} else {
		ruleName = rootRule
	}
	ruleName = strings.ToUpper(ruleName[0:1]) + ruleName[1:len(ruleName)]

	method := reflect.ValueOf(parser).MethodByName(ruleName)
	tree := method.Call([]reflect.Value{})[0].Interface()
	treeElem := reflect.ValueOf(tree).Elem()
	treeContext := treeElem.FieldByName("BaseParserRuleContext").Interface().(*antlr.BaseParserRuleContext)

	fmt.Println("Tree " + treeContext.ToStringTree(nil, parser))
/*ParserPart$*/
}