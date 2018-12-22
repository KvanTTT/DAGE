using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AntlrGrammarEditor
{
    public class GrammarFactory
    {
        private static readonly Regex LexerGrammarRegex = new Regex(@"lexer\s+grammar");

        private static readonly Regex CaseInsensitiveTypeRegex = new Regex(@"/\*\s*CaseInsensitiveType:\s*(\w+)\s*\*/");

        public static Grammar Open(string fileOrDirectoryName, out string root)
        {
            List<string> grammarFiles;
            string grammarName = "";
            string directoryName;
            GrammarType grammarType = GrammarType.Combined;
            CaseInsensitiveType caseInsensitiveType = CaseInsensitiveType.None;
            string lexerOrCombinedGrammarFile = null;
            string parserGrammarFile = null;
            root = null;

            if (File.Exists(fileOrDirectoryName))
            {
                directoryName = Path.GetDirectoryName(fileOrDirectoryName);
                string grammarFile = Path.GetFileName(fileOrDirectoryName);
                grammarName = Path.GetFileNameWithoutExtension(grammarFile);
                grammarFiles = new List<string> { grammarFile };
                lexerOrCombinedGrammarFile = fileOrDirectoryName;
            }
            else if (Directory.Exists(fileOrDirectoryName))
            {
                directoryName = fileOrDirectoryName;
                string[] g4Files = Directory.GetFiles(fileOrDirectoryName, "*.g4");

                grammarFiles = new List<string>(g4Files.Length);

                if (g4Files.Length == 1)
                {
                    string grammarFile = Path.GetFileName(g4Files[0]);
                    grammarName = Path.GetFileNameWithoutExtension(grammarFile);
                    grammarFiles.Add(grammarFile);
                    lexerOrCombinedGrammarFile = g4Files[0];
                }
                else if (g4Files.Length > 1)
                {
                    /*grammarName = Path.GetFileNameWithoutExtension(g4Files[0]).Replace(LexerPostfix, "")
                        .Replace(ParserPostfix, "");
    
                    grammarFiles = g4Files.Select(file => Path.GetFileName(file)).ToList();*/

                    string lexerFileName =
                        g4Files.FirstOrDefault(grammarFile => grammarFile.Contains(Grammar.LexerPostfix));
                    if (!string.IsNullOrEmpty(lexerFileName))
                    {
                        grammarFiles.Add(Path.GetFileName(lexerFileName));
                        grammarName = Path.GetFileNameWithoutExtension(lexerFileName).Replace(Grammar.LexerPostfix, "");
                        lexerOrCombinedGrammarFile = lexerFileName;
                    }

                    string parserFileName =
                        g4Files.FirstOrDefault(grammarFile => grammarFile.Contains(Grammar.ParserPostfix));
                    if (!string.IsNullOrEmpty(parserFileName))
                    {
                        grammarFiles.Add(Path.GetFileName(parserFileName));
                        grammarName = Path.GetFileNameWithoutExtension(parserFileName)
                            .Replace(Grammar.ParserPostfix, "");
                        parserGrammarFile = parserFileName;
                    }

                    grammarType = GrammarType.Separated;
                }
            }
            else
            {
                throw new FileNotFoundException($"Not file nor directory exists at path {fileOrDirectoryName}");
            }

            string pomFile = Path.Combine(directoryName, "pom.xml");
            if (File.Exists(pomFile))
            {
                string content = File.ReadAllText(pomFile);
                // TODO: fix with XPath
                var caseInsensitiveRegex = new Regex("<caseInsensitiveType>(\\w+)</caseInsensitiveType>");

                var match = caseInsensitiveRegex.Match(content);
                if (match.Success)
                {
                    Enum.TryParse(match.Groups[1].Value, out caseInsensitiveType);
                }
                
                var entryPointRegex = new Regex("<entryPoint>(\\w+)</entryPoint>");
                match = entryPointRegex.Match(content);
                if (match.Success)
                {
                    root = match.Groups[1].Value;
                }
            }
            else if (!string.IsNullOrEmpty(lexerOrCombinedGrammarFile))
            {
                string lexerOrCombinedGrammar = File.ReadAllText(lexerOrCombinedGrammarFile);

                if (LexerGrammarRegex.IsMatch(lexerOrCombinedGrammar) && parserGrammarFile == null)
                {
                    grammarType = GrammarType.Lexer;
                }

                var match = CaseInsensitiveTypeRegex.Match(lexerOrCombinedGrammar);
                if (match.Success)
                {
                    Enum.TryParse(match.Groups[1].Value, out caseInsensitiveType);
                }
            }

            string examplesDir = Path.Combine(directoryName, "examples");

            string[] textFiles = Directory.Exists(examplesDir)
                ? Directory.GetFiles(examplesDir, "*.*", SearchOption.AllDirectories)
                : new string[0];

            var result = new Grammar
            {
                Name = grammarName,
                Directory = fileOrDirectoryName,
                CaseInsensitiveType = caseInsensitiveType,
                Files = grammarFiles,
                Type = grammarType,
                TextFiles = textFiles.ToList()
            };

            return result;
        }

        public static Grammar CreateDefault()
        {
            var result = new Grammar
            {
                Name = "NewGrammar"
            };
            return result;
        }

        public static Grammar CreateDefaultCombinedAndFill(string grammarText, string grammarName, string directory)
        {
            return CreateDefaultAndFill(GrammarType.Combined, "", grammarText, grammarName, directory);
        }

        public static Grammar CreateDefaultSeparatedAndFill(string lexerText, string parserText, string grammarName, string directory)
        {
            return CreateDefaultAndFill(GrammarType.Separated, lexerText, parserText, grammarName, directory);
        }

        public static Grammar CreateDefaultLexerAndFill(string grammarText, string grammarName, string directory)
        {
            return CreateDefaultAndFill(GrammarType.Lexer, grammarText, "", grammarName, directory);
        }

        private static Grammar CreateDefaultAndFill(GrammarType type, string lexerText, string parserText, string grammarName, string directory)
        {
            var result = new Grammar
            {
                Name = grammarName,
                Type = type,
            };

            result.Directory = directory;
            InitFiles(result);

            foreach (string file in result.Files)
            {
                string text = file.Contains(Grammar.LexerPostfix) ? lexerText : parserText;
                File.WriteAllText(Path.Combine(directory, file), text);
            }

            return result;
        }

        public static void FillGrammarFiles(Grammar grammar, string directory, bool overwriteFiles)
        {
            grammar.Directory = directory;
            InitFiles(grammar);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            foreach (string file in grammar.Files)
            {
                var fullFileName = Path.Combine(directory, file);
                if (!File.Exists(fullFileName) || overwriteFiles)
                {
                    var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    var text = new StringBuilder();

                    if (fileWithoutExtension.Contains(Grammar.LexerPostfix))
                    {
                        text.Append("lexer ");
                    }
                    else if (fileWithoutExtension.Contains(Grammar.ParserPostfix))
                    {
                        text.Append("parser ");
                    }
                    text.Append($"grammar {fileWithoutExtension};");

                    if (!fileWithoutExtension.Contains(Grammar.ParserPostfix) &&
                        grammar.CaseInsensitiveType != CaseInsensitiveType.None)
                    {
                        text.Append($" /* CaseInsensitiveType: {grammar.CaseInsensitiveType} */");
                    }

                    text.AppendLine();
                    text.AppendLine();

                    if (fileWithoutExtension.Contains(Grammar.ParserPostfix))
                    {
                        text.AppendLine($"options {{ tokenVocab = {fileWithoutExtension.Replace(Grammar.ParserPostfix, Grammar.LexerPostfix)}; }}");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(Grammar.LexerPostfix))
                    {
                        text.AppendLine($"root");
                        text.AppendLine("    : .*? EOF");
                        text.AppendLine("    ;");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(Grammar.ParserPostfix))
                    {
                        text.AppendLine("Id         : [A-Za-z]+;");
                        text.AppendLine("Whitespace : [ \\t\\r\\n]+ -> channel(HIDDEN);");
                        text.AppendLine("Number     : [0-9]+;");
                        text.AppendLine();
                    }

                    File.WriteAllText(fullFileName, text.ToString());
                }
            }

            foreach (string file in grammar.TextFiles)
            {
                File.WriteAllText(file, "test");
            }
        }

        public static string GenerateTextFileName(Grammar grammar)
        {
            return Path.Combine(grammar.Directory, grammar.Name + "_text." + grammar.FileExtension);
        }

        private static void InitFiles(Grammar grammar)
        {
            var grammarFiles = grammar.Files;
            grammarFiles.Clear();

            if (grammar.Type == GrammarType.Lexer)
            {
                grammarFiles.Add(grammar.Name + Grammar.LexerPostfix + Grammar.AntlrDotExt);
            }
            else if (grammar.Type == GrammarType.Separated)
            {
                grammarFiles.Add(grammar.Name + Grammar.LexerPostfix + Grammar.AntlrDotExt);
                grammarFiles.Add(grammar.Name + Grammar.ParserPostfix + Grammar.AntlrDotExt);
            }
            else
            {
                grammarFiles.Add(grammar.Name + Grammar.AntlrDotExt);
            }

            grammar.TextFiles.Add(GenerateTextFileName(grammar));
        }
    }
}
