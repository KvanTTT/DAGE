using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Processors.ParserCompilation;
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using NUnit.Framework;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class ParserCompilationTests : TestsBase
    {
        [TestCaseSource(nameof(SupportedRuntimes))]
        public void ParserCompiledStageErrors(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};
start:  DIGIT+ {{i^;}};
CHAR:   [a-z]+;
DIGIT:  [0-9]+; 
WS:     [ \r\n\t]+ -> skip;";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar) { Runtime = runtime };

            var state = workflow.Process();
            Assert.IsInstanceOf<ParserCompiledState>(state, state.DiagnosisMessage);
            var parserCompiledState = (ParserCompiledState)state;
            Assert.GreaterOrEqual(parserCompiledState.Diagnoses.Count, 1);
            var firstDiagnosis = parserCompiledState.Diagnoses[0];
            Assert.AreEqual(WorkflowStage.ParserCompiled, firstDiagnosis.WorkflowStage);
            Assert.AreEqual(DiagnosisType.Error, firstDiagnosis.Type);
            var textSpan = ((ParserCompilationDiagnosis)firstDiagnosis).GrammarTextSpan;
            Assert.AreEqual(2, textSpan?.LineColumn.BeginLine);
        }

        [Test]
        public void ParserGeneratedStageSyntaxErrors()
        {
            var grammarText =
$@"lexer grammar {TestGrammarName};
TEST: {{'}};";

            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "."));
            var state = workflow.Process();
            var grammarSource = new Source(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            Assert.IsInstanceOf<ParserGeneratedState>(state, state.DiagnosisMessage);
            var parserGeneratedState = (ParserGeneratedState)state;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParserGenerationDiagnosis(1, 1, "syntax error: mismatched character '<EOF>' expecting '''", grammarSource),
                    new ParserGenerationDiagnosis(2, 11, "syntax error: '<EOF>' came as a complete surprise to me while matching a lexer rule", grammarSource),
                },
                parserGeneratedState.Diagnoses);
        }

        [Test]
        public void ParserGeneratedStageInvalidPackageError()
        {
            var grammarText =
$@"lexer grammar {TestGrammarName};
TEST: 'test';";

            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "."));
            workflow.PackageName = "invalid package";
            var state = workflow.Process();
            var grammarSource = new Source(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            Assert.IsInstanceOf<ParserGeneratedState>(state, state.DiagnosisMessage);
            var parserGeneratedState = (ParserGeneratedState)state;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParserGenerationDiagnosis( "Package name (invalid package) should contain only latin letter, digits, and underscore"),
                },
                parserGeneratedState.Diagnoses);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void GeneratedToGrammarCorrectMapping(Runtime runtime)
        {
            var grammarText =
                @"grammar test;
rootRule
    : {a====0}? tokensOrRules* EOF {a+++;}
    ;
tokensOrRules
    : {a====0}? TOKEN+ {a+++;}
    ;
TOKEN: [a-z]+;";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar)
            {
                Runtime = runtime
            };

            var state = workflow.Process();
            Assert.IsInstanceOf<ParserCompiledState>(state, state.DiagnosisMessage);
            ParserCompiledState parserCompiledState = (ParserCompiledState)state;
            var errors = parserCompiledState.Diagnoses;

            var runtimeInfo = runtime.GetRuntimeInfo();
            if (!runtimeInfo.Interpreted)
            {
                if (runtime.IsCSharpRuntime())
                {
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 11)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 41)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 11)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 29)));
                }
                else if (runtime == Runtime.Java)
                {
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 8)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 37)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 8)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 25)));
                }
                else if (runtime == Runtime.Dart)
                {
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 9)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 37)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 41)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 9)));
                }
                else if (runtime == Runtime.Go)
                {
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 11)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 3, 40)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 11)));
                    Assert.IsTrue(errors.Any(e => Compare(e, 6, 28)));
                }
                else
                {
                    throw new NotSupportedException($"Not completed runtime: {runtime}");
                }
            }
            else
            {
                Assert.GreaterOrEqual(errors.Count, 1);
                Assert.IsTrue(errors.Any(e => Compare(e, 3, 8)));
            }
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void GeneratedToGrammarCorrectMultilineMapping(Runtime runtime)
        {
            var cSharpCode =
                @"void Test() {
    Console.WriteLine(error1);
    Console.WriteLine(error2);
}";

            var fragmentsMap = new Dictionary<Runtime, string>
            {
                [Runtime.CSharpStandard] = cSharpCode,
                [Runtime.CSharpOptimized] = cSharpCode,
                [Runtime.Java] =
                    @"void test() {
    System.out.println(error1);
    System.out.println(error2);
}",
                [Runtime.Python] =
                    @"def test():
    print(""test"")
    error~",
                [Runtime.JavaScript] =
                    @"function test() {
    print(""test"");
    error~
}",
                [Runtime.Go] =
                    @"func test() {
    println(error1)
    println(error2)
}",
                [Runtime.Php] =
                    @"function test() {
    print('test');
    error~
}",
                [Runtime.Dart] =
                    @"void test() {
    print(error1);
    print(error2);
}"
            };

            var grammarText =
                $@"grammar {TestGrammarName};

@lexer::members {{{fragmentsMap[runtime]}}}

start: CHAR+;
CHAR:   [a-z]+;
WS:     [ \r\n\t]+ -> skip;";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar) { Runtime = runtime };

            var state = workflow.Process();
            Assert.IsInstanceOf<ParserCompiledState>(state, state.DiagnosisMessage);
            ParserCompiledState parserCompiledState = (ParserCompiledState)state;
            var errors = parserCompiledState.Diagnoses;

            if (runtime.IsCSharpRuntime())
            {
                Assert.True(errors.Any(e => Compare(e, 4, 23)));
                Assert.True(errors.Any(e => Compare(e, 5, 23)));
            }
            else if (runtime == Runtime.Java)
            {
                Assert.True(errors.Any(e => Compare(e, 4, 1)));
                Assert.True(errors.Any(e => Compare(e, 5, 1)));
            }
            else if (runtime == Runtime.Go)
            {
                Assert.True(errors.Any(e => Compare(e, 4, 13)));
                Assert.True(errors.Any(e => Compare(e, 5, 13)));
            }
            else if (runtime == Runtime.Dart)
            {
                Assert.True(errors.Any(e => Compare(e, 4, 11)));
                Assert.True(errors.Any(e => Compare(e, 5, 11)));
            }
            else if (runtime.GetRuntimeInfo().Interpreted)
            {
                Assert.True(errors.Any(e => Compare(e, 5, 1)));
            }
            else
            {
                throw new NotSupportedException($"Not completed runtime: {runtime}");
            }
        }

        static bool Compare(Diagnosis diagnosis, int line, int column)
        {
            if (diagnosis is ParserCompilationDiagnosis parserCompilationDiagnosis)
            {
                var grammarTextSpan = parserCompilationDiagnosis.GrammarTextSpan;
                if (grammarTextSpan is null)
                    return false;

                var lineColumn = grammarTextSpan.Value.LineColumn;
                return lineColumn.BeginLine == line && lineColumn.BeginColumn == column;
            }

            return false;
        }
    }
}