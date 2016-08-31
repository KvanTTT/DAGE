using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class GrammarFactory
    {
        public const string PreprocessorPostfix = "Preprocessor";
        public const string LexerPostfix = "Lexer";
        public const string ParserPostfix = "Parser";
        public const string Extension = ".g4";

        public static Grammar CreateDefault()
        {
            var result = new Grammar
            {
                Name = "NewGrammar",
                Root = "rootRule",
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpSharwell },
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
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpSharwell },
            };

            InitFiles(result);

            foreach (var file in result.Files)
            {
                File.WriteAllText(Path.Combine(directory, file), grammarText);
            }

            result.AgeFileName = Path.Combine(directory, grammarName) + ".age";
            result.Save();
            return result;
        }

        public static Grammar CreateDefaultSeparatedAndFill(string lexerText, string parserText, string grammarName, string directory)
        {
            var result = new Grammar
            {
                Name = grammarName,
                Runtimes = new HashSet<Runtime>() { Runtime.CSharpSharwell },
                SeparatedLexerAndParser = true,
            };

            InitFiles(result);

            foreach (var file in result.Files)
            {
                string text = file.Contains(LexerPostfix) ? lexerText : parserText;
                File.WriteAllText(Path.Combine(directory, file), text);
            }

            result.AgeFileName = Path.Combine(directory, grammarName) + ".age";
            result.Save();
            return result;
        }

        public static void FillGrammarFiles(Grammar grammar, string directory, bool overwriteFiles)
        {
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
                    if (fileWithoutExtension.Contains(LexerPostfix))
                    {
                        text.Append("lexer ");
                    }
                    else if (fileWithoutExtension.Contains(ParserPostfix))
                    {
                        text.Append("parser ");
                    }
                    text.AppendLine($"grammar {fileWithoutExtension};");
                    text.AppendLine();

                    if (fileWithoutExtension.Contains(ParserPostfix))
                    {
                        text.AppendLine($"options {{ tokenVocab = {fileWithoutExtension.Replace(ParserPostfix, LexerPostfix)}; }}");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(LexerPostfix) && !string.IsNullOrEmpty(grammar.Root))
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

                    if (!fileWithoutExtension.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.AppendLine("TOKEN: [a-z]+;");
                        text.AppendLine();
                    }

                    File.WriteAllText(fullFileName, text.ToString());
                }
            }

            grammar.AgeFileName = Path.Combine(directory, grammar.Name) + ".age";
            grammar.Save();
        }

        private static void InitFiles(Grammar grammar)
        {
            grammar.Files.Clear();
            if (grammar.Preprocessor)
            {
                if (grammar.PreprocessorSeparatedLexerAndParser)
                {
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + LexerPostfix + Extension);
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + ParserPostfix + Extension);
                }
                else
                {
                    grammar.Files.Add(grammar.Name + PreprocessorPostfix + Extension);
                }
            }
            if (grammar.SeparatedLexerAndParser)
            {
                grammar.Files.Add(grammar.Name + LexerPostfix + Extension);
                grammar.Files.Add(grammar.Name + ParserPostfix + Extension);
            }
            else
            {
                grammar.Files.Add(grammar.Name + Extension);
            }
        }
    }
}
