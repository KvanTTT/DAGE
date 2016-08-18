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

        [TestCase(Runtime.CSharpSharwell)]
        [TestCase(Runtime.CSharp)]
        [TestCase(Runtime.Java)]
        public void FullProcess(Runtime runtime)
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

            TextParsedState state = (TextParsedState)workflow.Process();
            CollectionAssert.AreEquivalent(
                new ParsingError[] {
                    new ParsingError(1, 0, "line 1:0 token recognition error at: '!'"),
                    new ParsingError(1, 3, "line 1:3 extraneous input 'asdf' expecting DIGIT")
                },
                state.TextErrors);
            Assert.AreEqual("(start asdf 1234)", state.StringTree);
        }
    }
}
