using NUnit.Framework;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
        private static string _javaPath;
        private static string _javaCompilerPath;

        [SetUp]
        public void Init()
        {
            var assemblyPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.Directory.SetCurrentDirectory(assemblyPath);

            _javaPath = Helpers.GetJavaExePath(@"bin\java.exe");
            _javaCompilerPath = Helpers.GetJavaExePath(@"bin\javac.exe");
        }
        
        [Test]
        public void GrammarCheckedStage()
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

            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(3, 25, "error: test.g4:3:25: token recognition error at: '-z'", "test.g4"),
                    new ParsingError(3, 27, "error: test.g4:3:27: token recognition error at: ']'", "test.g4"),
                    new ParsingError(3, 28, "error: test.g4:3:28: mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", "test.g4")
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void SeparatedLexerAndParser()
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

            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(2, 25, "error: testLexer.g4:2:25: token recognition error at: '-z'", "testLexer.g4"),
                    new ParsingError(2, 27, "error: testLexer.g4:2:27: token recognition error at: ']'", "testLexer.g4"),
                    new ParsingError(2, 28, "error: testLexer.g4:2:28: mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", "testLexer.g4"),
                    new ParsingError(3, 16, "error: testParser.g4:3:16: extraneous input '#' expecting {<EOF>, TOKEN_REF, RULE_REF, DOC_COMMENT, 'fragment', 'protected', 'public', 'private', 'catch', 'finally', 'mode'}", "testParser.g4")
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void ParserGeneratedStage()
        {
            var workflow = CreateWorkflow();
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

            ParserGeneratedState parserGeneratedState = state as ParserGeneratedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(2, 24, "error(56): test.g4:2:24: reference to undefined rule: rule1", "test.g4"),
                },
                parserGeneratedState.Errors);
        }

        [TestCase(Runtime.CSharpSharwell)]
        [TestCase(Runtime.CSharp)]
        [TestCase(Runtime.Java)]
        public void ParserCompiliedStage(Runtime runtime)
        {
            var workflow = CreateWorkflow();
            var grammarText =
                @"grammar test;
                start:  DIGIT+ { i++; };
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
            // TODO: correct line & column error handling
            Assert.AreEqual(1, parserGeneratedState.Errors.Count);
        }

        [TestCase(Runtime.CSharpSharwell)]
        [TestCase(Runtime.CSharp)]
        [TestCase(Runtime.Java)]
        public void TextParsedStage(Runtime runtime)
        {
            var workflow = CreateWorkflow();
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

            TextParsedState textParsedState = state as TextParsedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(1, 0, "line 1:0 token recognition error at: '!'", ""),
                    new ParsingError(1, 3, "line 1:3 extraneous input 'asdf' expecting DIGIT", "")
                },
                textParsedState.TextErrors);
            Assert.AreEqual("(start asdf 1234)", textParsedState.Tree);
        }

        [TestCase(Runtime.CSharpSharwell)]
        [TestCase(Runtime.CSharp)]
        [TestCase(Runtime.Java)]
        public void CaseInsensitive(Runtime runtime)
        {
            var workflow = CreateWorkflow();
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
            TextParsedState textParsedState = state as TextParsedState;
            Assert.AreEqual(0, textParsedState.TextErrors.Count);
            Assert.AreEqual("(start A a 1234)", textParsedState.Tree);
        }

        private Workflow CreateWorkflow()
        {
            var workflow = new Workflow
            {
                JavaPath = _javaPath,
                JavaCompilerPath = _javaCompilerPath
            };
            return workflow;
        }
    }
}
