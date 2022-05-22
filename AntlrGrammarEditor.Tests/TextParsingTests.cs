using System.Collections.Generic;
using System.IO;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Processors.ParserCompilers;
using AntlrGrammarEditor.Processors.TextParsing;
using AntlrGrammarEditor.WorkflowState;
using NUnit.Framework;

namespace AntlrGrammarEditor.Tests
{
    public class TextParsingTests : TestsBase
    {
        [TestCaseSource(nameof(SupportedRuntimes))]
        public void TextParsedStageErrors(Runtime runtime)
        {
            var grammarText =
$@"grammar {TestGrammarName};

root
    : missingToken extraneousToken noViableAlternative mismatchedInput EOF
    ;

missingToken
    : Error LParen RParen Semi
    ;

extraneousToken
    : Error Id Semi
    ;

mismatchedInput
    : Error Id Semi
    ;

noViableAlternative
    : AA BB
    | AA CC
    ;
    
AA: 'aa';
BB: 'bb';
CC: 'cc';
DD: 'dd';
LParen     : '((';
RParen     : '))';
Semi       : ';';
Error      : 'error';
Id         : [A-Za-z][A-Za-z0-9]+;
Whitespace : [ \t\r\n]+ -> channel(HIDDEN);
Comment    : '//' ~[\r\n]* -> channel(HIDDEN);
Number     : [0-9']+;";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            File.WriteAllText(TestTextName,
@"#                       // token recognition error at: '#'
error (( ;        // missing '))' at ';'
error id1 id2 ;   // extraneous input 'id2' expecting ';'
aa  dd            // no viable alternative at input 'aa  dd'
error 123 456 ;   // mismatched input '123' expecting Id");

            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};

            var state = workflow.Process();
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            TextParsedState textParsedState = (TextParsedState)state;
            var textSource = textParsedState.TextSource!;
            CollectionAssert.AreEquivalent(
                new [] {
                    new TextParsingDiagnosis(1, 1, 1, 2, "token recognition error at: '#'", textSource),
                    new TextParsingDiagnosis(2, 10, 2, 11, "missing '))' at ';'", textSource),
                    new TextParsingDiagnosis(3, 11, 3, 14, "extraneous input 'id2' expecting ';'", textSource),
                    new TextParsingDiagnosis(4, 5, 4, 7, "no viable alternative at input 'aa  dd'", textSource),
                    new TextParsingDiagnosis(5, 7, 5, 10, "mismatched input '123' expecting Id", textSource)
                },
                textParsedState.Diagnoses);

            // TODO: unify in different runtimes
            //Assert.AreEqual("(root (missingToken error (( <missing '))'> ;) (extraneousToken error id1 id2 ;) (noViableAlternative aa dd) (mismatchedInput error 123 456 ;) EOF)", textParsedState.Tree);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckLexerOnlyGrammar(Runtime runtime)
        {
            var grammarText =
                $"lexer grammar {TestGrammarName};" +
                "T1: 'T1';" +
                "Digit: [0-9]+;" +
                "Space: ' '+ -> channel(HIDDEN);";

            var grammar = GrammarFactory.CreateDefaultLexerAndFill(grammarText, ".");
            File.WriteAllText(TestTextName, "T1 1234");

            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};

            var state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckCustomRoot(Runtime runtime)
        {
            var grammarText =
                @$"grammar {TestGrammarName};
root1: 'V1';
root2: 'V2';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName, Root = null};

            File.WriteAllText(TestTextName, "V1");
            var state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);

            workflow.Root = "root1";
            state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);

            workflow.Root = "root2";
            File.WriteAllText(TestTextName, "V2");
            state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckPredictionMode(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};

root
    : (stmt1 | stmt2) EOF
    ;
    
stmt1
    : name
    ;

stmt2
    : 'static' name '.' Id
    ;

name
    : Id ('.' Id)*
    ;

Dot        : '.';
Static     : 'static';
Id         : [A-Za-z]+;
Whitespace : [ \t\r\n]+ -> channel(HIDDEN);
";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};
            File.WriteAllText(TestTextName, @"static a.b");

            workflow.PredictionMode = PredictionMode.LL;
            var llState = workflow.Process();
            Assert.IsTrue((llState as TextParsedState)?.HasErrors == false, llState.DiagnosisMessage);

            workflow.PredictionMode = PredictionMode.SLL;
            var sllState = workflow.Process();
            Assert.IsTrue((sllState as TextParsedState)?.HasErrors == true, sllState.DiagnosisMessage);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckPackageName(Runtime runtime)
        {
            CheckPackageName(runtime, false);
            CheckPackageName(runtime, true);
        }

        private static void CheckPackageName(Runtime runtime, bool lexerOnly)
        {
            const string packageName = "TestLanguage";

            Grammar grammar;
            if (lexerOnly)
            {
                var grammarContent =
                    $@"lexer grammar {TestGrammarName};
TOKEN: 'a';";
                grammar = GrammarFactory.CreateDefaultLexerAndFill(grammarContent, ".");
            }
            else
            {
                var grammarContent =
                    $@"grammar {TestGrammarName};
root:  TOKEN;
TOKEN:  'a';";
                grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarContent, ".");
            }

            var workflow = new Workflow(grammar)
            {
                Runtime = runtime,
                PackageName = packageName,
                TextFileName = TestTextName
            };

            File.WriteAllText(TestTextName, "A");
            var state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckSuperclass(Runtime runtime)
        {
            const string parserSuperclassName = TestGrammarName + "ParserBase";
            const string predicateName = "isOk";

            var superclassesAndPredicates = new Dictionary<Runtime, (string SuperClass, string Predicate)>
            {
                [Runtime.Java] = (
$@"import org.antlr.v4.runtime.Parser;
import org.antlr.v4.runtime.TokenStream;

public abstract class {parserSuperclassName} extends Parser {{
    protected testParserBase(TokenStream input) {{
        super(input);
    }}

    protected boolean {predicateName}() {{
        return getTokenStream().LT(1).getText().equals(""OK"");
    }}
}}",
$"{predicateName}()"),

                [Runtime.CSharp] = (
$@"using System.IO;
using Antlr4.Runtime;

public abstract class {parserSuperclassName} : Parser
{{
    protected testParserBase(ITokenStream input) : base(input) {{ }}

    protected testParserBase(ITokenStream input, TextWriter output, TextWriter errorOutput) : base(input, output, errorOutput) {{ }}

    protected bool {predicateName}()
    {{
        return TokenStream.LT(1).Text == ""OK"";
    }}
}}",
$"{predicateName}()"),

                [Runtime.Python] = (
$@"from antlr4 import *

class {parserSuperclassName}(Parser):
    def {predicateName}(self):
        return self._input.LT(1).text == ""OK""",
$"self.{predicateName}()"),

                [Runtime.JavaScript] = (
$@"import antlr4 from 'antlr4';

export default class {parserSuperclassName} extends antlr4.Parser {{
    constructor(input) {{
        super(input);
    }}

    {predicateName}() {{
        return this._input.LT(1).text === ""OK"";
    }}
}}",
$"this.{predicateName}()"),

                [Runtime.Go] = (
$@"package main

import (
	""github.com/antlr/antlr4/runtime/Go/antlr""
)

type {parserSuperclassName} struct {{
	*antlr.BaseParser
}}

func (p *{parserSuperclassName}) {predicateName}() bool {{
	return p.GetTokenStream().LT(1).GetText() == ""OK""
}}
",
$"p.{predicateName}()"),

                [Runtime.Php] = (
                    $@"<?php

namespace {{
    use Antlr\Antlr4\Runtime\Parser;

    abstract class {parserSuperclassName} extends Parser
    {{
        protected function {predicateName}() : bool
        {{
            return \$this->getCurrentToken()->getText() == ""OK"";
        }}
    }}
}}",
                    $@"\$this->{predicateName}()"
                ),

                [Runtime.Dart] = (
$@"import 'package:antlr4/antlr4.dart';
import 'dart:io';

abstract class {parserSuperclassName} extends Parser {{
  testParserBase(TokenStream input) : super(input) {{}}

  bool {predicateName}() {{
    return tokenStream.LT(1) == ""OK"";
  }}
}}",
$@"{predicateName}()")
            };

            var predicateMark = new SingleMark("predicate", Runtime.Java.GetRuntimeInfo()).ToString();
            var lexerName = $"{TestGrammarName}Lexer";

            var lexerContent =
$@"lexer grammar {lexerName};
WORD: [A-Z]+;";

            var parserContent =
$@"parser grammar {TestGrammarName}Parser;

options {{
    tokenVocab={lexerName};
    superClass={parserSuperclassName};
}}

start: {{{predicateMark}}}? WORD;";

            if (runtime == Runtime.Dart)
                Assert.Ignore("superClass in Dart is not supported (https://github.com/antlr/antlr4/pull/3247)");
            else if (runtime == Runtime.Php)
                Assert.Ignore("superClass in Php is not supported (https://github.com/antlr/antlr4/issues/3298");

            var superclassAndPredicate = superclassesAndPredicates[runtime];
            parserContent = parserContent.Replace(predicateMark, superclassAndPredicate.Predicate);
            var grammar = GrammarFactory.CreateDefaultSeparatedAndFill(lexerContent, parserContent, ".");
            File.WriteAllText(parserSuperclassName + "." + runtime.GetRuntimeInfo().MainExtension , superclassAndPredicate.SuperClass);
            File.WriteAllText(TestTextName, "OK");
            var workflow = new Workflow(grammar) { Runtime = runtime, TextFileName = TestTextName };
            var state = workflow.Process();
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            Assert.IsFalse(state.HasErrors, state.DiagnosisMessage);
            Assert.AreEqual(((TextParsedState)state).Tree, "(start OK)");
        }
    }
}