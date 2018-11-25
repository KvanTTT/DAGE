using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AntlrGrammarEditor
{
    public class GrammarFactory
    {
        public const string PreprocessorPostfix = "Preprocessor";

        public static Grammar Open(string fileOrDirectoryName)
        {
            List<string> grammarFiles;
            string grammarName = "";
            string directoryName;
            bool separatedLexerAndParser = false;

            if (File.Exists(fileOrDirectoryName))
            {
                directoryName = Path.GetDirectoryName(fileOrDirectoryName);
                string grammarFile = Path.GetFileName(fileOrDirectoryName);
                grammarName = Path.GetFileNameWithoutExtension(grammarFile);
                grammarFiles = new List<string> { grammarFile };
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
                    }

                    string parserFileName =
                        g4Files.FirstOrDefault(grammarFile => grammarFile.Contains(Grammar.ParserPostfix));
                    if (!string.IsNullOrEmpty(parserFileName))
                    {
                        grammarFiles.Add(Path.GetFileName(parserFileName));
                        grammarName = Path.GetFileNameWithoutExtension(parserFileName)
                            .Replace(Grammar.ParserPostfix, "");
                    }

                    separatedLexerAndParser = true;
                }
            }
            else
            {
                throw new FileNotFoundException($"Not file nor directory exists at path {fileOrDirectoryName}");
            }

            string[] textFiles;
            string examplesDir = Path.Combine(directoryName, "examples");
            if (Directory.Exists(examplesDir))
            {
                textFiles = Directory.GetFiles(examplesDir, "*.*", SearchOption.AllDirectories);
            }
            else
            {
                textFiles = new string[0];
            }

            var result = new Grammar
            {
                Name = grammarName,
                Directory = fileOrDirectoryName,
                Runtimes = new HashSet<Runtime> {Runtime.Java},
                Files = grammarFiles,
                SeparatedLexerAndParser = separatedLexerAndParser,
                TextFiles = textFiles.ToList()
            };
                
            return result;
        }

        public static Grammar CreateDefault()
        {
            var result = new Grammar
            {
                Name = "NewGrammar",
                Root = "rootRule",
                Runtimes = new HashSet<Runtime> { Runtime.CSharpOptimized },
                SeparatedLexerAndParser = false,
                CaseInsensitive = false,
                Preprocessor = false,
                PreprocessorRoot = "preprocessorRootRule",
                PreprocessorSeparatedLexerAndParser = false,
                PreprocessorCaseInsensitive = false
            };
            return result;
        }

        public static Grammar CreateDefaultAndFill(string grammarText, string grammarName, string directory)
        {
            var result = new Grammar
            {
                Name = grammarName,
                Runtimes = new HashSet<Runtime> { Runtime.CSharpOptimized }
            };

            result.Directory = directory;
            InitFiles(result);

            foreach (var file in result.Files)
            {
                File.WriteAllText(Path.Combine(directory, file), grammarText);
            }

            return result;
        }

        public static Grammar CreateDefaultSeparatedAndFill(string lexerText, string parserText, string grammarName, string directory)
        {
            var result = new Grammar
            {
                Name = grammarName,
                Runtimes = new HashSet<Runtime> { Runtime.CSharpOptimized },
                SeparatedLexerAndParser = true,
            };

            result.Directory = directory;
            InitFiles(result);

            foreach (var file in result.Files)
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

            foreach (var file in grammar.Files)
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
                    text.AppendLine($"grammar {fileWithoutExtension};");
                    text.AppendLine();

                    if (fileWithoutExtension.Contains(Grammar.ParserPostfix))
                    {
                        text.AppendLine($"options {{ tokenVocab = {fileWithoutExtension.Replace(Grammar.ParserPostfix, Grammar.LexerPostfix)}; }}");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(Grammar.LexerPostfix) && !string.IsNullOrEmpty(grammar.Root))
                    {
                        text.AppendLine($"{(fileWithoutExtension.Contains(PreprocessorPostfix) ? grammar.PreprocessorRoot : grammar.Root)}");
                        text.AppendLine("    : tokensOrRules* EOF");
                        text.AppendLine("    ;");
                        text.AppendLine();
                        text.AppendLine("tokensOrRules");
                        text.AppendLine("    : TOKEN+");
                        text.AppendLine("    ;");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(Grammar.ParserPostfix))
                    {
                        text.AppendLine("TOKEN: [a-z]+;");
                        text.AppendLine();
                    }

                    File.WriteAllText(fullFileName, text.ToString());
                }
            }

            foreach (var file in grammar.TextFiles)
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
            grammar.Files.Clear();
            if (grammar.Preprocessor)
            {
                if (grammar.PreprocessorSeparatedLexerAndParser)
                {
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + Grammar.LexerPostfix + Grammar.AntlrDotExt);
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + Grammar.ParserPostfix + Grammar.AntlrDotExt);
                }
                else
                {
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + Grammar.AntlrDotExt);
                }
            }
            if (grammar.SeparatedLexerAndParser)
            {
                grammar.Files.Add(grammar.Name + Grammar.LexerPostfix + Grammar.AntlrDotExt);
                grammar.Files.Add(grammar.Name + Grammar.ParserPostfix + Grammar.AntlrDotExt);
            }
            else
            {
                grammar.Files.Add(grammar.Name + Grammar.AntlrDotExt);
            }

            grammar.TextFiles.Add(GenerateTextFileName(grammar));
        }
    }
}
