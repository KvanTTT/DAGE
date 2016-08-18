using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
        [SetUp]
        public void Init()
        {
            var assemblyPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.Directory.SetCurrentDirectory(assemblyPath);
        }
        
        [Test]
        public void GrammarCheckedStage()
        {
            var workflow = new Workflow();
            workflow.Grammar =
                @"grammar test;
                start: DIGIT+;
                CHAR:   a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.GrammarChecked, state.Stage);

            GrammarCheckedState grammarCheckedState = state as GrammarCheckedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(3, 25, "token recognition error at: '-z'"),
                    new ParsingError(3, 27, "token recognition error at: ']'"),
                    new ParsingError(3, 28, "mismatched input '+' expecting {ASSIGN, PLUS_ASSIGN}")
                },
                grammarCheckedState.Errors);
        }

        [Test]
        public void ParserGeneratedStage()
        {
            var workflow = new Workflow();
            workflow.Grammar =
                @"grammar test;
                start:  rule1+;
                rule:   DIGIT;
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.ParserGenerated, state.Stage);

            ParserGeneratedState parserGeneratedState = state as ParserGeneratedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(2, 24, "error(56): test.g4:2:24: reference to undefined rule: rule1"),
                },
                parserGeneratedState.Errors);
        }

        [TestCase(Runtime.CSharpSharwell)]
        [TestCase(Runtime.CSharp)]
        [TestCase(Runtime.Java)]
        public void ParserCompiliedStage(Runtime runtime)
        {
            var workflow = new Workflow();
            workflow.Runtime = runtime;
            workflow.Grammar =
                @"grammar test;
                start:  DIGIT+ { i++; };
                CHAR:   [a-z]+;
                DIGIT:  [0-9]+;
                WS:     [ \r\n\t]+ -> skip;";

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
            var workflow = new Workflow();
            workflow.Runtime = runtime;
            workflow.Grammar =
                @"grammar test;
                start: DIGIT+;
                CHAR:  [a-z]+;
                DIGIT: [0-9]+;
                WS:    [ \r\n\t]+ -> skip;";
            workflow.Text =
                @"!  asdf  1234";

            var state = workflow.Process();
            Assert.AreEqual(WorkflowStage.TextParsed, state.Stage);

            TextParsedState textParsedState = state as TextParsedState;
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(1, 0, "line 1:0 token recognition error at: '!'"),
                    new ParsingError(1, 3, "line 1:3 extraneous input 'asdf' expecting DIGIT")
                },
                textParsedState.TextErrors);
            Assert.AreEqual("(start asdf 1234)", textParsedState.StringTree);
        }
    }
}
