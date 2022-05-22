using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AntlrGrammarEditor
{
    public static class GrammarFactory
    {
        public static Grammar Open(string fileName)
        {
            string grammarName;
            string directoryName;
            string? packageName = null;
            string? root = null;

            if (File.Exists(fileName))
            {
                directoryName = Path.GetDirectoryName(fileName) ?? "";
                grammarName = Path.GetFileNameWithoutExtension(fileName);
            }
            else
            {
                throw new FileNotFoundException($"File does not exist at path {fileName}");
            }

            string pomFile = Path.Combine(directoryName, "pom.xml");
            if (File.Exists(pomFile))
            {
                string content = File.ReadAllText(pomFile);

                // TODO: fix with XMLParser and XPath
                var entryPointRegex = new Regex("<entryPoint>(\\w+)</entryPoint>");
                var match = entryPointRegex.Match(content);
                if (match.Success)
                {
                    root = match.Groups[1].Value;
                }

                var packageRegex = new Regex("<packageName>(\\w+)</packageName>");
                match = packageRegex.Match(content);
                if (match.Success)
                {
                    packageName = match.Groups[1].Value;
                }
            }

            return new Grammar(grammarName, directoryName, root, packageName);
        }

        public static Grammar CreateDefaultCombinedAndFill(string content, string directory)
        {
            var grammar = new Grammar(GrammarFilesManager.ExtractGrammarName(content), directory);
            new GrammarFilesManager(grammar, GrammarProjectType.Combined).CreateFiles(combinedContent: content);
            return grammar;
        }

        public static Grammar CreateDefaultSeparatedAndFill(string lexerContent, string parserContent, string directory)
        {
            var grammar = new Grammar(GrammarFilesManager.ExtractGrammarName(parserContent), directory);
            new GrammarFilesManager(grammar, GrammarProjectType.Separated).CreateFiles(lexerContent, parserContent);
            return grammar;
        }

        public static Grammar CreateDefaultLexerAndFill(string lexerContent, string directory)
        {
            var grammar = new Grammar(GrammarFilesManager.ExtractGrammarName(lexerContent), directory);
            new GrammarFilesManager(grammar, GrammarProjectType.Lexer).CreateFiles(lexerContent);
            return grammar;
        }
    }
}
