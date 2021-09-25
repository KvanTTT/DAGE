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

	/*$PackageName*/"./__PackageName__"/*PackageName$*/
)


func main() {
	fileName := "../../Text"

	argsLen := len(os.Args)
	if argsLen > 1 {
		fileName = os.Args[1]
	}

	bytes, err := ioutil.ReadFile(fileName)
	if err != nil {
		fmt.Print(err)
	}

	code := string(bytes)
	codeStream := antlr.NewInputStream(code)
	lexer := /*$PackageName2$*/New__TemplateLexerName__(codeStream)
	tokensStream := antlr.NewCommonTokenStream(lexer, 0)
	tokensStream.Fill()
	allTokens := tokensStream.GetAllTokens()

	_ = tokensStream
	_ = allTokens
/*$ParserPart*/
	rootRule := ""
	mode := "ll"

	if argsLen > 2 {
		rootRule = os.Args[2]
		if argsLen > 3 {
			// TODO: tokenizeOnly processing
			if argsLen > 4 {
				mode = strings.ToLower(os.Args[4])
			}
		}
	}

	parser := /*$PackageName2$*/New__TemplateParserName__(tokensStream)

	var predictionMode int
	if mode == "sll" {
		predictionMode = antlr.PredictionModeSLL
	} else if mode == "ll" {
		predictionMode = antlr.PredictionModeLL
	} else {
		predictionMode = antlr.PredictionModeLLExactAmbigDetection
	}
    parser.Interpreter.SetPredictionMode(predictionMode)

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