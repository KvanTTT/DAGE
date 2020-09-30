using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
        private const string TestGrammarName = "test";
        private static readonly string TestTextName = Path.Combine(Environment.CurrentDirectory, "Text");

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
            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, "."));
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
            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage, state.Exception?.ToString());

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new [] {
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
            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserGenerated, state.Stage, state.Exception?.ToString());

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            char separator = Path.DirectorySeparatorChar;
            string testGrammarFullName = $"{Environment.CurrentDirectory}{separator}.{separator}{TestGrammarName}.g4";
            ParserGeneratedState parserGeneratedState = state as ParserGeneratedState;
            string str = new ParsingError(2, 24, $"error(56): {testGrammarFullName}:2:24: reference to undefined rule: rule1", grammarSource, WorkflowStage.ParserGenerated).ToString();
            CollectionAssert.AreEquivalent(
                new [] {
                    new ParsingError(2, 24, $"error(56): {testGrammarFullName}:2:24: reference to undefined rule: rule1", grammarSource, WorkflowStage.ParserGenerated),
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
        [TestCase(Runtime.Php)]
        public void ParserCompiledStageErrors(Runtime runtime)
        {
            var grammarText =
                @"grammar Test;
                start:  DIGIT+ {i^;};
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "Test", ".");
            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompiled, state.Stage, state.Exception?.ToString());

            ParserCompiledState parserGeneratedState = state as ParserCompiledState;
            Assert.GreaterOrEqual(parserGeneratedState.Errors.Count, 1);
            //Assert.AreEqual(runtime == Runtime.Go ? 1 : 2, parserGeneratedState.Errors[0].TextSpan.GetLineColumn().BeginLine);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        public void TextParsedStageErrors(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, @"!  asdf  1234");

            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage, state.Exception?.ToString());

            TextParsedState textParsedState = state as TextParsedState;
            var textSource = textParsedState.Text;
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
        [TestCase(Runtime.Php)]
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
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            grammar.CaseInsensitiveType = lowerCase ? CaseInsensitiveType.lower : CaseInsensitiveType.UPPER;
            File.WriteAllText(TestTextName, @"A a 1234");

            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;

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
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, " ");

            var workflow = new Workflow(grammar);
            workflow.Runtime = Runtime.Java;
            workflow.TextFileName = TestTextName;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage);
            Assert.IsTrue(((TextParsedState)state).ParserCompiledState.ParserGeneratedState.Errors[0].IsWarning);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        public void CheckListenersAndVisitors(Runtime runtime)
        {
            var grammarText =
                $@"grammar {TestGrammarName};
                t: T;
                T: [a-z]+;";
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, @"asdf");

            var workflow = new Workflow(grammar)
            {
                GenerateListener = true,
                GenerateVisitor = true
            };
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;

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
        [TestCase(Runtime.Php)]
        public void CheckCustomRoot(Runtime runtime)
        {
            var grammarText =
                $"grammar {TestGrammarName};" +
                "root1: 'V1';" +
                "root2: 'V2';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;

            workflow.Root = null;
            File.WriteAllText(TestTextName, "V1");
            var state = workflow.Process();
            TextParsedState textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(state.HasErrors);

            workflow.Root = "root1";
            state = workflow.Process();
            textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(state.HasErrors);

            workflow.Root = "root2";
            File.WriteAllText(TestTextName, "V2");
            state = workflow.Process();
            textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(state.HasErrors);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        public void CheckPackageName(Runtime runtime)
        {
            const string packageName = "TestLanguage";

            var grammarText =
                $@"grammar {TestGrammarName};
                root:  TOKEN;
                TOKEN:  'a';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            grammar.CaseInsensitiveType = CaseInsensitiveType.lower;
            var workflow = new Workflow(grammar)
            {
                Runtime = runtime,
                PackageName = packageName,
                TextFileName = TestTextName
            };

            File.WriteAllText(TestTextName, "A");
            var state = workflow.Process();
            var textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(textParsedState.HasErrors);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        public void CheckPredictionMode(Runtime runtime)
        {
            var grammarText = $@"
grammar {TestGrammarName};

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

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;
            File.WriteAllText(TestTextName, @"static a.b");

            workflow.PredictionMode = PredictionMode.LL;
            var llState = workflow.Process();
            TextParsedState llTextParsedState = llState as TextParsedState;
            Assert.IsNotNull(llTextParsedState);
            Assert.IsFalse(llState.HasErrors);

            workflow.PredictionMode = PredictionMode.SLL;
            var sllState = workflow.Process();
            var sllTextParsedState = sllState as TextParsedState;
            Assert.IsNotNull(sllTextParsedState);
            Assert.IsTrue(sllTextParsedState.HasErrors);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        public void CheckLexerOnlyGrammar(Runtime runtime)
        {
            var grammarText =
                $"lexer grammar {TestGrammarName}Lexer;" +
                "T1: 'T1';" +
                "Digit: [0-9]+;" +
                "Space: ' '+ -> channel(HIDDEN);";

            var grammar = GrammarFactory.CreateDefaultLexerAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, "T1 1234");

            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;
            workflow.TextFileName = TestTextName;

            var state = workflow.Process();
            TextParsedState textParsedState = state as TextParsedState;
            Assert.IsNotNull(textParsedState);
            Assert.IsFalse(state.HasErrors);
        }

        [Test]
        public void CheckIncorrectGrammarDefinedOptions()
        {
            var grammarText =
                @$"grammar {TestGrammarName};
// caseInsensitiveType=incorrect;
// language=incorrect;
// package=incorrect;
// visitor=incorrect;
// listener=incorrect;
// root=incorrect;
// predictionMode=incorrect;

// caseInsensitiveType=lower;
// language=JavaScript;
// package=package;
// visitor=true;
// listener=true;
// root=root;
// predictionMode=sll;

root:
    .*? ;

TOKEN: 'token';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            var workflow = new Workflow(grammar);
            workflow.TextFileName = TestTextName;
            workflow.EndStage = WorkflowStage.GrammarChecked;
            var state = (GrammarCheckedState) workflow.Process();

            Assert.AreEqual(CaseInsensitiveType.lower, state.CaseInsensitiveType);
            Assert.AreEqual(Runtime.JavaScript, state.Runtime);
            Assert.AreEqual("package", state.Package);
            Assert.AreEqual(true, state.Listener);
            Assert.AreEqual(true, state.Visitor);
            Assert.AreEqual("root", state.Root);
            Assert.AreEqual(PredictionMode.SLL, state.PredictionMode);

            CheckIncorrect("caseInsensitiveType");
            CheckIncorrect("language");
            CheckIncorrect("package", true);
            CheckIncorrect("visitor");
            CheckIncorrect("listener");
            CheckIncorrect("root");
            CheckIncorrect("predictionMode");

            void CheckIncorrect(string optionName, bool notError = false)
            {
                Func<ParsingError, bool> checker = error => error.Message.Contains(optionName != "root" ? $"Incorrect option {optionName}" : "Root incorrect is not exist");
                if (!notError)
                {
                    Assert.IsTrue(state.Errors.Any(checker));
                }
                else
                {
                    Assert.IsFalse(state.Errors.Any(checker));
                }
            }

            CheckDuplication("caseInsensitiveType");
            CheckDuplication("language");
            CheckDuplication("package");
            CheckDuplication("visitor");
            CheckDuplication("listener");
            CheckDuplication("root");
            CheckDuplication("predictionMode");

            void CheckDuplication(string optionName)
            {
                Assert.IsTrue(state.Errors.Any(error => error.Message.Contains($"Option {optionName} is already defined")));
            }
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
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
            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, "test", ".");
            var workflow = new Workflow(grammar);
            workflow.Runtime = runtime;

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserCompiled, state.Stage, state.Exception?.ToString());

            ParserCompiledState parserGeneratedState = state as ParserCompiledState;
            var errors = parserGeneratedState.Errors;
            Assert.AreEqual(8, errors.Count);
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 3).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 6).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 8).Count());
            Assert.AreEqual(2, errors.Where(e => e.TextSpan.GetLineColumn().BeginLine == 9).Count());
        }
    }
}
