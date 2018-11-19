using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntlrGrammarEditor
{
    public class GrammarFactory
    {
        public const string PreprocessorPostfix = "Preprocessor";

        public static Grammar CreateDefault()
        {
            var result = new Grammar
            {
                Name = "NewGrammar",
                Root = "rootRule",
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpOptimized },
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
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpOptimized },
            };

            result.AgeFileName = Path.Combine(directory, grammarName) + Grammar.ProjectDotExt;
            InitFiles(result);

            foreach (var file in result.Files)
            {
                File.WriteAllText(Path.Combine(directory, file), grammarText);
            }

            result.Save();
            return result;
        }

        public static Grammar CreateDefaultSeparatedAndFill(string lexerText, string parserText, string grammarName, string directory)
        {
            var result = new Grammar
            {
                Name = grammarName,
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpOptimized },
                SeparatedLexerAndParser = true,
            };

            result.AgeFileName = Path.Combine(directory, grammarName) + Grammar.ProjectDotExt;
            InitFiles(result);

            foreach (var file in result.Files)
            {
                string text = file.Contains(Grammar.LexerPostfix) ? lexerText : parserText;
                File.WriteAllText(Path.Combine(directory, file), text);
            }

            result.Save();
            return result;
        }

        public static void FillGrammarFiles(Grammar grammar, string directory, bool overwriteFiles)
        {
            grammar.AgeFileName = Path.Combine(directory, grammar.Name) + Grammar.ProjectDotExt;
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

            grammar.Save();
        }

        public static string GenerateTextFileName(Grammar grammar)
        {
            return Path.Combine(grammar.GrammarPath, grammar.Name + "_text." + grammar.FileExtension);
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
