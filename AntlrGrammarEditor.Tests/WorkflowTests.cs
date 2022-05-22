using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Processors.GrammarChecking;
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests : TestsBase
    {
        [Test]
        public void RuntimesExist()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));
            foreach (Runtime runtime in runtimes)
            {
                Assert.IsTrue(RuntimeInfo.Runtimes.ContainsKey(runtime), $"Runtime {runtime} does not exist");
            }
        }

        [Test]
        public void RuntimeInitialized()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));

            foreach (Runtime runtime in runtimes)
            {
                string message;
                if (runtime != Runtime.CPlusPlus && runtime != Runtime.Swift)
                {
                    var runtimeInfo = runtime.GetRuntimeInfo();
                    Assert.IsFalse(string.IsNullOrEmpty(runtimeInfo.Version), $"Failed to initialize {runtime} runtime");
                    message = $"{runtime}: {runtimeInfo.RuntimeToolName} {runtimeInfo.Version}";
                }
                else
                {
                    message = $"{runtime} is not supported for now";
                }
                Console.WriteLine(message);
            }
        }

        [Test]
        public void GeneratorsExist()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));
            var grammarText =
$@"grammar {TestGrammarName};
start: DIGIT+;
CHAR:  [a-z]+;
DIGIT: [0-9]+;
WS:    [ \r\n\t]+ -> skip;";

            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "."));
            workflow.EndStage = WorkflowStage.ParserGenerated;
            foreach (Runtime runtime in runtimes)
            {
                workflow.Runtime = runtime;
                var state = (ParserGeneratedState)workflow.Process();
                Assert.IsFalse(state.HasErrors, state.DiagnosisMessage);

                RuntimeInfo runtimeInfo = RuntimeInfo.Runtimes[runtime];
                var extensions = runtimeInfo.Extensions;
                var allFiles = Directory.GetFiles(Path.Combine(ParserGenerator.HelperDirectoryName, TestGrammarName, runtimeInfo.Runtime.ToString()));
                var actualFilesCount = allFiles.Count(file => extensions.Any(ext => Path.GetExtension(file).EndsWith(ext)));
                Assert.Greater(actualFilesCount, 0, $"Failed to initialize {runtime} runtime");

                /*foreach (var file in allFiles)
                {
                    File.Delete(file);
                }*/
            }
        }

        [Test]
        public void GrammarCheckedStageErrors()
        {
            var grammarText =
$@"grammar {TestGrammarName};
start: DIGIT+;
CHAR:   a-z]+;
DIGIT: [0-9]+;
WS:    [ \r\n\t]+ -> skip;";

            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "."));

            var state = workflow.Process();
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);

            var grammarSource = new Source(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            GrammarCheckedState grammarCheckedState = (GrammarCheckedState)state;
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new GrammarCheckingDiagnosis(3, 10, 3, 12, "token recognition error at: '-z'", grammarSource),
                    new GrammarCheckingDiagnosis(3, 12, 3, 13, "token recognition error at: ']'", grammarSource),
                    new GrammarCheckingDiagnosis(3, 13, 3, 14, "mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", grammarSource)
                },
                grammarCheckedState.Diagnoses);
        }

        [Test]
        public void ParserGeneratedStageErrors()
        {
            var grammarText =
                $@"grammar {TestGrammarName};
start:  {{true}}? rule1+;
rule:   DIGIT;
CHAR:   [a-z]+;
DIGIT:  [0-9]+;
WS:     [ \r\n\t]+ -> skip;";

            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "."));

            var state = workflow.Process();

            var grammarSource = new Source(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));

            Assert.IsInstanceOf<ParserGeneratedState>(state, state.DiagnosisMessage);
            var parserGeneratedState = (ParserGeneratedState)state;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParserGenerationDiagnosis(2, 17, "reference to undefined rule: rule1", grammarSource),
                },
                parserGeneratedState.Diagnoses);
        }

        [Test]
        public void SeparatedLexerAndParserErrors()
        {
            var lexerGrammarName = $"{TestGrammarName}{Grammar.LexerPostfix}";
            var lexerContent =
$@"lexer grammar {lexerGrammarName};
CHAR:   a-z]+;
DIGIT: [0-9]+;
WS:    [ \r\n\t]+ -> skip;";

            var parserContent =
$@"parser grammar {TestGrammarName}{Grammar.ParserPostfix};
options {{ tokenVocab={lexerGrammarName}; }}
start: DIGIT+;
#";
            var workflow = new Workflow(GrammarFactory.CreateDefaultSeparatedAndFill(lexerContent, parserContent, "."));

            var state = workflow.Process();

            var testLexerSource = new Source(TestGrammarName + "Lexer.g4", File.ReadAllText(TestGrammarName + "Lexer.g4"));
            var testParserSource = new Source(TestGrammarName + "Parser.g4", File.ReadAllText(TestGrammarName + "Parser.g4"));
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);
            var grammarCheckedState = (GrammarCheckedState)state;
            var expectedDiagnoses = new[]
            {
                new GrammarCheckingDiagnosis(2, 10, 2, 12, "token recognition error at: '-z'", testLexerSource),
                new GrammarCheckingDiagnosis(2, 12, 2, 13, "token recognition error at: ']'", testLexerSource),
                new GrammarCheckingDiagnosis(2, 13, 2, 14, "mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", testLexerSource),
                new GrammarCheckingDiagnosis(4, 1, 4, 2, "extraneous input '#' expecting {<EOF>, 'mode'}", testParserSource)
            };
            CollectionAssert.AreEquivalent(expectedDiagnoses, grammarCheckedState.Diagnoses);
        }

        [Test]
        public void DoNotStopProcessingIfWarnings()
        {
            var grammarText =
$@"grammar {TestGrammarName};
t: T;
T:  ['' ]+;";
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            File.WriteAllText(TestTextName, " ");

            var workflow = new Workflow(grammar);
            workflow.Runtime = Runtime.Java;
            workflow.TextFileName = TestTextName;

            var state = workflow.Process();
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            var textParsedState = (TextParsedState) state;
            Assert.IsTrue(textParsedState.ParserCompiledState.ParserGeneratedState.Diagnoses[0].Type == DiagnosisType.Warning);
        }

        [TestCaseSource(nameof(SupportedRuntimes))]
        public void CheckListenersAndVisitors(Runtime runtime)
        {
            var grammarText =
$@"grammar {TestGrammarName};
t: T;
T: [a-z]+;";
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            File.WriteAllText(TestTextName, @"asdf");

            var workflow = new Workflow(grammar)
            {
                GenerateListener = true,
                GenerateVisitor = true,
                Runtime = runtime,
                TextFileName = TestTextName
            };

            var state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);

            var allFiles = Directory.GetFiles(Path.Combine(ParserGenerator.HelperDirectoryName, TestGrammarName, runtime.ToString()));

            Assert.IsTrue(allFiles.Any(file => file.Contains("listener", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(allFiles.Any(file => file.Contains("visitor", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void CheckIncorrectGrammarDefinedOptions()
        {
            var grammarText =
@$"grammar {TestGrammarName};
// language=incorrect;
// package=incorrect;
// visitor=incorrect;
// listener=incorrect;
// root=incorrect;
// predictionMode=incorrect;

// language=JavaScript;
// package=package;
// visitor=true;
// listener=true;
// root=root;
// predictionMode=sll;

root:
    .*? ;

TOKEN: 'token';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, ".");
            var workflow = new Workflow(grammar);
            workflow.TextFileName = TestTextName;
            workflow.EndStage = WorkflowStage.GrammarChecked;
            var state = workflow.Process();
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);
            var grammarCheckedState = (GrammarCheckedState)state;

            Assert.AreEqual(Runtime.JavaScript, grammarCheckedState.Runtime);
            Assert.AreEqual("package", grammarCheckedState.Package);
            Assert.AreEqual(true, grammarCheckedState.GenerateListener);
            Assert.AreEqual(true, grammarCheckedState.GenerateVisitor);
            Assert.AreEqual("root", grammarCheckedState.Root);
            Assert.AreEqual(PredictionMode.SLL, grammarCheckedState.PredictionMode);

            CheckIncorrect("language");
            CheckIncorrect("package", true);
            CheckIncorrect("visitor");
            CheckIncorrect("listener");
            CheckIncorrect("root");
            CheckIncorrect("predictionMode");

            void CheckIncorrect(string optionName, bool notError = false)
            {
                var contains = state.Diagnoses.Any(diagnosis => diagnosis.Message.Contains(optionName != "root"
                    ? $"Incorrect option {optionName}"
                    : "Root incorrect is not exist"));

                if (!notError)
                {
                    Assert.IsTrue(contains, state.DiagnosisMessage);
                }
                else
                {
                    Assert.IsFalse(contains, state.DiagnosisMessage);
                }
            }

            CheckDuplication("language");
            CheckDuplication("package");
            CheckDuplication("visitor");
            CheckDuplication("listener");
            CheckDuplication("root");
            CheckDuplication("predictionMode");

            void CheckDuplication(string optionName)
            {
                Assert.IsTrue(state.Diagnoses.Any(error => error.Message.Contains($"Option {optionName} is already defined")), state.DiagnosisMessage);
            }
        }
    }
}
