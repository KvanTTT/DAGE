using System;
using System.IO;
using NUnit.Framework;

namespace AntlrGrammarEditor.Tests
{
    public abstract class TestsBase
    {
        protected const string TestGrammarName = "test";
        protected static readonly string TestTextName = Path.Combine(Environment.CurrentDirectory, "Text");

        protected static Runtime[] SupportedRuntimes => Helpers.SupportedRuntimes;

        [SetUp]
        public void Init()
        {
            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(assemblyPath);
        }
    }
}