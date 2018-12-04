using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
        private const string TestGrammarName = "test";
        
        [SetUp]
        public void Init()
        {
            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(assemblyPath);
        }

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
                    RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
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
        public void AllGeneratorsExists()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));
            var grammarText = $@"grammar {TestGrammarName};
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var workflow = new Workflow(GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, "."));
            workflow.EndStage = WorkflowStage.ParserGenerated;
            foreach (Runtime runtime in runtimes)
            {
                workflow.Runtime = runtime;
                var state = (ParserGeneratedState)workflow.Process();
                Assert.IsFalse(state.HasErrors, string.Join(Environment.NewLine, state.Errors));

                RuntimeInfo runtimeInfo = RuntimeInfo.Runtimes[runtime];
                var extensions = runtimeInfo.Extensions;
                var allFiles = Directory.GetFiles(Path.Combine(ParserGenerator.HelperDirectoryName, TestGrammarName, runtimeInfo.Runtime.ToString()));
                var actualFilesCount = allFiles.Count(file => extensions.Any(ext => Path.GetExtension(file).EndsWith(ext)));
                Assert.Greater(actualFilesCount, 0, $"Failed to initialize {runtime} runtime");

                foreach (var file in allFiles)
                {
                    File.Delete(file);
                }
            }
        }
        
        [Test]
        public void GrammarCheckedStageErrors()
        {
            var grammarText = $@"grammar {TestGrammarName};
                start: DIGIT+;
                CHAR:   a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var workflow = new Workflow(GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage, state.Exception?.ToString());

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(3, 25, "error: test.g4:3:25: token recognition error at: '-z'", grammarSource, WorkflowStage.GrammarChecked),
                    new ParsingError(3, 27, "error: test.g4:3:27: token recognition error at: ']'", grammarSource, WorkflowStage.GrammarChecked),
                    new ParsingError(3, 28, "error: test.g4:3:28: mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", grammarSource, WorkflowStage.GrammarChecked)
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void SeparatedLexerAndParserErrors()
        {
            var lexerText = $@"lexer grammar {TestGrammarName};
                CHAR:   a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var parserText = $@"parser grammar {TestGrammarName};
                start: DIGIT+;
                #";
            var workflow = new Workflow(GrammarFactory.CreateDefaultSeparatedAndFill(lexerText, parserText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage, state.Exception?.ToString());

            var testLexerSource = new CodeSource(TestGrammarName + "Lexer.g4", File.ReadAllText(TestGrammarName + "Lexer.g4"));
            var testParserSource = new CodeSource(TestGrammarName + "Parser.g4", File.ReadAllText(TestGrammarName + "Parser.g4"));
            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParsingError(2, 25, $"error: {TestGrammarName}Lexer.g4:2:25: token recognition error at: '-z'", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(2, 27, $"error: {TestGrammarName}Lexer.g4:2:27: token recognition error at: ']'", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(2, 28, $"error: {TestGrammarName}Lexer.g4:2:28: mismatched input '+' expecting {{ASSIGN, PLUS_ASSIGN}}", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(3, 16, $"error: {TestGrammarName}Parser.g4:3:16: extraneous input '#' expecting {{<EOF>, 'mode'}}", testParserSource, WorkflowStage.GrammarChecked)
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void ParserGeneratedStageErrors()
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                start:  rule1+;
                rule:   DIGIT;
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var workflow = new Workflow(GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserGenerated, state.Stage, state.Exception?.ToString());

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            ParserGeneratedState parserGeneratedState = state as ParserGeneratedState;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParsingError(2, 24, $"error(56): {TestGrammarName}.g4:2:24: reference to undefined rule: rule1", grammarSource, WorkflowStage.ParserGenerated),
                },
                parserGeneratedState.Errors);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        public void ParserCompiliedStageErrors(Runtime runtime)
        {
            var grammarText =
                @"grammar Test;
                start:  DIGIT+ {i^;};
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "Test", ".");
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            var workflow = new Workflow(grammar);

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompilied, state.Stage, state.Exception?.ToString());

            ParserCompiliedState parserGeneratedState = state as ParserCompiliedState;
            Assert.GreaterOrEqual(parserGeneratedState.Errors.Count, 1);
            Assert.AreEqual(2, parserGeneratedState.Errors[0].TextSpan.GetLineColumn().BeginLine);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        public void TextParsedStageErrors(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, ".");
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            var workflow = new Workflow(grammar);
            workflow.Text =
                @"!  asdf  1234";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage, state.Exception?.ToString());

            var textSource = new CodeSource("", workflow.Text);
            TextParsedState textParsedState = state as TextParsedState;
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParsingError(1, 1, "line 1:0 token recognition error at: '!'", textSource, WorkflowStage.TextParsed),
                    new ParsingError(1, 4, "line 1:3 extraneous input 'asdf' expecting DIGIT", textSource, WorkflowStage.TextParsed)
                },
                textParsedState.Errors);
            Assert.AreEqual("(start asdf 1234)", textParsedState.Tree);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        public void CaseInsensitive(Runtime runtime)
        {
            CheckCaseInsensitiveWorkflow(runtime, true);
            CheckCaseInsensitiveWorkflow(runtime, false);
        }

        private static void CheckCaseInsensitiveWorkflow(Runtime runtime, bool lowerCase)
        {
            char c = lowerCase ? 'a' : 'A';
            var grammarText =
                $@"grammar {TestGrammarName};
                start:  A A DIGIT;
                A:      '{c}';
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, ".");
            grammar.CaseInsensitiveType = lowerCase ? CaseInsensitiveType.lower : CaseInsensitiveType.UPPER;
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            var workflow = new Workflow(grammar);
            workflow.Text = @"A a 1234";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage, state.Exception?.ToString());
            TextParsedState textParsedState = state as TextParsedState;
            Assert.AreEqual(0, textParsedState.Errors.Count, string.Join(Environment.NewLine, textParsedState.Errors));
            Assert.AreEqual("(start A a 1234)", textParsedState.Tree);
        }

        [Test]
        public void DoNotStopProcessingIfWarnings()
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                t: T;
                T:  ['' ]+;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, ".");
            grammar.Runtimes = new HashSet<Runtime> {Runtime.Java};
            var workflow = new Workflow(grammar);
            workflow.Text = @" ";
            
            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage);
            Assert.IsTrue(((TextParsedState)state).ParserCompiliedState.ParserGeneratedState.Errors[0].IsWarning);
        }

        [Test]
        public void MultiruntimeGrammar()
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                t: T;
                T: [a-z]+;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, ".");
            grammar.Runtimes = new HashSet<Runtime>
            {
                Runtime.CSharpOptimized,
                Runtime.CSharpStandard,
                Runtime.Java,
                Runtime.Python2,
                Runtime.Python3,
                Runtime.JavaScript,
                Runtime.Go
            };
            var workflow = new Workflow(grammar);
            workflow.Text = @"asdf";

            var state = workflow.Process();
            TextParsedState textParsedState = state as TextParsedState;
            Assert.AreEqual(0, textParsedState.Errors.Count, string.Join(Environment.NewLine, textParsedState.Errors));
            Assert.AreEqual("(t asdf)", textParsedState.Tree);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        public void CheckListenersAndVisitors(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                t: T;
                T: [a-z]+;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, TestGrammarName, ".");
            var workflow = new Workflow(grammar)
            {
                GenerateListener = true,
                GenerateVisitor = true
            };
            workflow.Runtime = runtime;
            workflow.Text = @"asdf";

            var state = workflow.Process();
            TextParsedState textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(state.HasErrors);

            var allFiles = Directory.GetFiles(Path.Combine(ParserGenerator.HelperDirectoryName, TestGrammarName, runtime.ToString()));

            Assert.IsTrue(allFiles.Any(file => file.Contains("listener", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(allFiles.Any(file => file.Contains("visitor", StringComparison.OrdinalIgnoreCase)));
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        public void GrammarGeneratedCodeCorrectMapping(Runtime runtime)
        {
            Assert.Ignore("Not ready");

            var grammarText =
                @"grammar test;
                  rootRule
                      : {a==0}? tokensOrRules* EOF {a++;}
                      ;
                  tokensOrRules
                      : {a==0}? TOKEN+ {a++;}
                      ;
                  TOKEN: {b==0}? [a-z]+ {b++;};
                  DIGIT: {b==0}? [0-9]+ {b++;};";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            var workflow = new Workflow(grammar);

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompilied, state.Stage, state.Exception?.ToString());

            ParserCompiliedState parserGeneratedState = state as ParserCompiliedState;
            var errors = parserGeneratedState.Errors;
            Assert.AreEqual(8, errors.Count);
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 3).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 6).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 8).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 9).Count());
        }
    }
}
