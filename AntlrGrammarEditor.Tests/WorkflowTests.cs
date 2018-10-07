using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
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
                Assert.IsTrue(RuntimeInfo.Runtimes.ContainsKey(runtime));
            }
        }

        [Test]
        public void RuntimeInitialized()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));

            foreach (Runtime runtime in runtimes)
            {
                if (runtime != Runtime.CPlusPlus && runtime != Runtime.Swift)
                {
                    RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
                    Assert.IsTrue(!string.IsNullOrEmpty(runtimeInfo.Version));
                    Console.WriteLine($"{runtime}: {runtimeInfo.RuntimeToolName} {runtimeInfo.Version}");
                }
                else
                {
                    Console.WriteLine($"{runtime} is not supported for now");
                }
            }
        }

        [Test]
        public void AllGeneratorsExists()
        {
            var runtimes = (Runtime[])Enum.GetValues(typeof(Runtime));
            var workflow = new Workflow();
            var grammarText = @"grammar test;
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            workflow.Grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");
            workflow.EndStage = WorkflowStage.ParserGenerated;
            foreach (Runtime runtime in runtimes)
            {
                workflow.Runtime = runtime;
                var state = (ParserGeneratedState)workflow.Process();
                Assert.IsFalse(state.HasErrors);
                
                RuntimeInfo runtimeInfo = RuntimeInfo.Runtimes[runtime];
                var extensions = runtimeInfo.Extensions;
                var allFiles = Directory.GetFiles(Path.Combine(Workflow.HelperDirectoryName, runtimeInfo.Runtime.ToString()));
                var actualFilesCount = allFiles.Where
                    (file => extensions.Any(ext => Path.GetExtension(file).EndsWith(ext))).Count();
                Assert.Greater(actualFilesCount, 0);

                foreach (var file in allFiles)
                {
                    File.Delete(file);
                }
            }
        }
        
        [Test]
        public void GrammarCheckedStageErrors()
        {
            var workflow = new Workflow();
            var grammarText = @"grammar test;
                start: DIGIT+;
                CHAR:   a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            workflow.Grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage);

            var grammarSource = new CodeSource("test.g4", File.ReadAllText("test.g4"));
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
            var workflow = new Workflow();
            var lexerText = @"lexer grammar test;
                CHAR:   a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var parserText = @"parser grammar test;
                start: DIGIT+;
                #";
            workflow.Grammar = GrammarFactory.CreateDefaultSeparatedAndFill(lexerText, parserText, "test", ".");

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage);

            var testLexerSource = new CodeSource("testLexer.g4", File.ReadAllText("testLexer.g4"));
            var testParserSource = new CodeSource("testParser.g4", File.ReadAllText("testParser.g4"));
            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(2, 25, "error: testLexer.g4:2:25: token recognition error at: '-z'", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(2, 27, "error: testLexer.g4:2:27: token recognition error at: ']'", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(2, 28, "error: testLexer.g4:2:28: mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", testLexerSource, WorkflowStage.GrammarChecked),
                    new ParsingError(3, 16, "error: testParser.g4:3:16: extraneous input '#' expecting {<EOF>, 'mode'}", testParserSource, WorkflowStage.GrammarChecked)
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void ParserGeneratedStageErrors()
        {
            var workflow = new Workflow();
            var grammarText =
                @"grammar test;
                start:  rule1+;
                rule:   DIGIT;
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            workflow.Grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserGenerated, state.Stage);

            var grammarSource = new CodeSource("test.g4", File.ReadAllText("test.g4"));
            ParserGeneratedState parserGeneratedState = state as ParserGeneratedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(2, 24, "error(56): test.g4:2:24: reference to undefined rule: rule1", grammarSource, WorkflowStage.ParserGenerated),
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && runtime == Runtime.Python3)
            {
                Assert.Ignore("Python3 runtime only works on Windows for now");
            }

            var workflow = new Workflow();
            var grammarText =
                @"grammar test;
                start:  DIGIT+ {i^;};
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            workflow.Grammar = grammar;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompilied, state.Stage);

            ParserCompiliedState parserGeneratedState = state as ParserCompiliedState;
            Assert.GreaterOrEqual(parserGeneratedState.Errors.Count, 1);
            Assert.AreEqual(2, parserGeneratedState.Errors[0].TextSpan.StartLineColumn.Line);
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && runtime == Runtime.Python3)
            {
                Assert.Ignore("Python3 runtime only works on Windows for now");
            }

            var workflow = new Workflow();
            var grammarText =
                @"grammar test;
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            workflow.Grammar = grammar;
            workflow.Text =
                @"!  asdf  1234";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage);

            var textSource = new CodeSource("", workflow.Text);
            TextParsedState textParsedState = state as TextParsedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(1, 1, "line 1:0 token recognition error at: '!'", textSource, WorkflowStage.TextParsed),
                    new ParsingError(1, 4, "line 1:3 extraneous input 'asdf' expecting DIGIT", textSource, WorkflowStage.TextParsed)
                },
                textParsedState.TextErrors);
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
            if (runtime == Runtime.Java)
            {
                Assert.Ignore("Java 4.7 custom streams are not supported yet.");
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && runtime == Runtime.Python3)
            {
                Assert.Ignore("Python3 runtime only works on Windows for now");
            }

            var workflow = new Workflow();
            var grammarText =
                @"grammar test;
                start:  A A DIGIT;
                A:      'a';
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultAndFill(grammarText, "test", ".");
            grammar.CaseInsensitive = true;
            grammar.Runtimes.Clear();
            grammar.Runtimes.Add(runtime);
            workflow.Grammar = grammar;
            workflow.Text = @"A a 1234";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage);
            TextParsedState textParsedState = state as TextParsedState;
            Assert.AreEqual(0, textParsedState.TextErrors.Count);
            Assert.AreEqual("(start A a 1234)", textParsedState.Tree);
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

            var workflow = new Workflow();
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
            workflow.Grammar = grammar;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompilied, state.Stage);

            ParserCompiliedState parserGeneratedState = state as ParserCompiliedState;
            var errors = parserGeneratedState.Errors;
            Assert.AreEqual(8, errors.Count);
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.StartLineColumn.Line == 3).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.StartLineColumn.Line == 6).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.StartLineColumn.Line == 8).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.StartLineColumn.Line == 9).Count());
        }
    }
}
