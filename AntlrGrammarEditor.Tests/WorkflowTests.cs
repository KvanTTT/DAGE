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
            var grammarText = $@"grammar {TestGrammarName};
start: DIGIT+;
CHAR:   a-z]+;
DIGIT: [0-9]+;
WS:    [ \r\n\t]+ -> skip;";
            var workflow = new Workflow(GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, "."));

            var state = workflow.Process();
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            GrammarCheckedState grammarCheckedState = (GrammarCheckedState)state;
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Diagnosis(3, 10, 3, 12, "Error: test.g4:3:10: token recognition error at: '-z'", grammarSource,
                        WorkflowStage.GrammarChecked),
                    new Diagnosis(3, 12, 3, 13, "Error: test.g4:3:12: token recognition error at: ']'", grammarSource,
                        WorkflowStage.GrammarChecked),
                    new Diagnosis(3, 13, 3, 14,
                        "Error: test.g4:3:13: mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}", grammarSource,
                        WorkflowStage.GrammarChecked)
                },
                grammarCheckedState.Diagnoses);
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

            var testLexerSource = new CodeSource(TestGrammarName + "Lexer.g4", File.ReadAllText(TestGrammarName + "Lexer.g4"));
            var testParserSource = new CodeSource(TestGrammarName + "Parser.g4", File.ReadAllText(TestGrammarName + "Parser.g4"));
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);
            var grammarCheckedState = (GrammarCheckedState)state;
            CollectionAssert.AreEquivalent(
                new [] {
                    new Diagnosis(2, 10, 2, 12, $"Error: {TestGrammarName}Lexer.g4:2:10: token recognition error at: '-z'", testLexerSource, WorkflowStage.GrammarChecked),
                    new Diagnosis(2, 12, 2, 13, $"Error: {TestGrammarName}Lexer.g4:2:12: token recognition error at: ']'", testLexerSource, WorkflowStage.GrammarChecked),
                    new Diagnosis(2, 13, 2, 14, $"Error: {TestGrammarName}Lexer.g4:2:13: mismatched input '+' expecting {{ASSIGN, PLUS_ASSIGN}}", testLexerSource, WorkflowStage.GrammarChecked),
                    new Diagnosis(3, 1, 3, 2, $"Error: {TestGrammarName}Parser.g4:3:1: extraneous input '#' expecting {{<EOF>, 'mode'}}", testParserSource, WorkflowStage.GrammarChecked)
                },
                grammarCheckedState.Diagnoses);
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

            var grammarSource = new CodeSource(TestGrammarName + ".g4", File.ReadAllText(TestGrammarName + ".g4"));
            char separator = Path.DirectorySeparatorChar;
            string testGrammarFullName = $"{Environment.CurrentDirectory}{separator}.{separator}{TestGrammarName}.g4";

            Assert.IsInstanceOf<ParserGeneratedState>(state, state.DiagnosisMessage);
            var parserGeneratedState = (ParserGeneratedState)state;
            CollectionAssert.AreEquivalent(
                new [] {
                    new Diagnosis(2, 24, $"error(56): {testGrammarFullName}:2:24: reference to undefined rule: rule1", grammarSource, WorkflowStage.ParserGenerated),
                },
                parserGeneratedState.Diagnoses);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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
            Assert.IsInstanceOf<ParserCompiledState>(state, state.DiagnosisMessage);
            var parserCompiledState = (ParserCompiledState)state;
            Assert.GreaterOrEqual(parserCompiledState.Diagnoses.Count, 1);
            var firstDiagnosis = parserCompiledState.Diagnoses[0];
            Assert.AreEqual(WorkflowStage.ParserCompiled, firstDiagnosis.WorkflowStage);
            Assert.AreEqual(DiagnosisType.Error, firstDiagnosis.Type);
            var textSpan = firstDiagnosis.TextSpan;
            Assert.AreEqual(2, textSpan?.LineColumn.BeginLine);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, @"#                       // token recognition error at: '#'
error (( ;        // missing '))' at ';'
error id1 id2 ;   // extraneous input 'id2' expecting ';'
aa  dd            // no viable alternative at input 'aa  dd'
error 123 456 ;   // mismatched input '123' expecting Id");

            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};

            var state = workflow.Process();
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            TextParsedState textParsedState = (TextParsedState)state;
            var textSource = textParsedState.Text;
            CollectionAssert.AreEquivalent(
                new [] {
                    new Diagnosis(1, 1, 1, 2, "line 1:1 token recognition error at: '#'", textSource, WorkflowStage.TextParsed),
                    new Diagnosis(2, 10, 2, 11, "line 2:10 missing '))' at ';'", textSource, WorkflowStage.TextParsed),
                    new Diagnosis(3, 11, 3, 14, "line 3:11 extraneous input 'id2' expecting ';'", textSource, WorkflowStage.TextParsed),
                    new Diagnosis(4, 5, 4, 7, "line 4:5 no viable alternative at input 'aa  dd'", textSource, WorkflowStage.TextParsed),
                    new Diagnosis(5, 7, 5, 10, "line 5:7 mismatched input '123' expecting Id", textSource, WorkflowStage.TextParsed)
                },
                textParsedState.Diagnoses);

            // TODO: unify in different runtimes
            //Assert.AreEqual("(root (missingToken error (( <missing '))'> ;) (extraneousToken error id1 id2 ;) (noViableAlternative aa dd) (mismatchedInput error 123 456 ;) EOF)", textParsedState.Tree);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            var textParsedState = (TextParsedState)state;
            Assert.AreEqual(0, textParsedState.Diagnoses.Count, string.Join(Environment.NewLine, textParsedState.Diagnoses));
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
            Assert.IsInstanceOf<TextParsedState>(state, state.DiagnosisMessage);
            var textParsedState = (TextParsedState) state;
            Assert.IsTrue(textParsedState.ParserCompiledState.ParserGeneratedState.Diagnoses[0].Type == DiagnosisType.Warning);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);

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
        [TestCase(Runtime.Dart)]
        public void CheckCustomRoot(Runtime runtime)
        {
            var grammarText =
                $"grammar {TestGrammarName};" +
                "root1: 'V1';" +
                "root2: 'V2';";

            var grammar = GrammarFactory.CreateDefaultCombinedAndFill(grammarText, TestGrammarName, ".");
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

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
        public void CheckPackageName(Runtime runtime)
        {
            CheckPackageName(runtime, false);
            CheckPackageName(runtime, true);
        }

        private static void CheckPackageName(Runtime runtime, bool lexerOnly)
        {
            const string packageName = "TestLanguage";

            string lexerGrammarText, parserGrammarText;
            GrammarType grammarType;
            if (lexerOnly)
            {
                lexerGrammarText =
$@"lexer grammar {TestGrammarName}Lexer;
TOKEN: 'a';";
                parserGrammarText = "";
                grammarType = GrammarType.Lexer;
            }
            else
            {
                lexerGrammarText = "";
                parserGrammarText =
$@"grammar {TestGrammarName};
root:  TOKEN;
TOKEN:  'a';";
                grammarType = GrammarType.Combined;
            }

            var grammar = GrammarFactory.CreateDefaultAndFill(grammarType, lexerGrammarText, parserGrammarText, TestGrammarName, ".");
            grammar.CaseInsensitiveType = CaseInsensitiveType.lower;
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

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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
            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};
            File.WriteAllText(TestTextName, @"static a.b");

            workflow.PredictionMode = PredictionMode.LL;
            var llState = workflow.Process();
            Assert.IsTrue((llState as TextParsedState)?.HasErrors == false, llState.DiagnosisMessage);

            workflow.PredictionMode = PredictionMode.SLL;
            var sllState = workflow.Process();
            Assert.IsTrue((sllState as TextParsedState)?.HasErrors == true, sllState.DiagnosisMessage);
        }

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
        public void CheckLexerOnlyGrammar(Runtime runtime)
        {
            var grammarText =
                $"lexer grammar {TestGrammarName}Lexer;" +
                "T1: 'T1';" +
                "Digit: [0-9]+;" +
                "Space: ' '+ -> channel(HIDDEN);";

            var grammar = GrammarFactory.CreateDefaultLexerAndFill(grammarText, TestGrammarName, ".");
            File.WriteAllText(TestTextName, "T1 1234");

            var workflow = new Workflow(grammar) {Runtime = runtime, TextFileName = TestTextName};

            var state = workflow.Process();
            Assert.IsTrue((state as TextParsedState)?.HasErrors == false, state.DiagnosisMessage);
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
            var state = workflow.Process();
            Assert.IsInstanceOf<GrammarCheckedState>(state, state.DiagnosisMessage);
            var grammarCheckedState = (GrammarCheckedState)state;

            Assert.AreEqual(CaseInsensitiveType.lower, grammarCheckedState.CaseInsensitiveType);
            Assert.AreEqual(Runtime.JavaScript, grammarCheckedState.Runtime);
            Assert.AreEqual("package", grammarCheckedState.Package);
            Assert.AreEqual(true, grammarCheckedState.Listener);
            Assert.AreEqual(true, grammarCheckedState.Visitor);
            Assert.AreEqual("root", grammarCheckedState.Root);
            Assert.AreEqual(PredictionMode.SLL, grammarCheckedState.PredictionMode);

            CheckIncorrect("caseInsensitiveType");
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

            CheckDuplication("caseInsensitiveType");
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

        [TestCase(Runtime.CSharpOptimized)]
        [TestCase(Runtime.CSharpStandard)]
        [TestCase(Runtime.Java)]
        [TestCase(Runtime.Python2)]
        [TestCase(Runtime.Python3)]
        [TestCase(Runtime.JavaScript)]
        [TestCase(Runtime.Go)]
        [TestCase(Runtime.Php)]
        [TestCase(Runtime.Dart)]
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
            Assert.IsInstanceOf<ParserCompiledState>(state, state.DiagnosisMessage);
            ParserCompiledState parserCompiledState = (ParserCompiledState)state;
            var errors = parserCompiledState.Diagnoses;
            Assert.AreEqual(8, errors.Count);
            Assert.AreEqual(2, errors.Count(e => e.TextSpan?.LineColumn.BeginLine == 3));
            Assert.AreEqual(2, errors.Count(e => e.TextSpan?.LineColumn.BeginLine == 6));
            Assert.AreEqual(2, errors.Count(e => e.TextSpan?.LineColumn.BeginLine == 8));
            Assert.AreEqual(2, errors.Count(e => e.TextSpan?.LineColumn.BeginLine == 9));
        }
    }
}
