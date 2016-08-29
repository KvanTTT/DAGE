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
        public const string DefaultGrammarName = "NewGrammar";
        public const string DefaultRootRule = "rootRule";
        public const string DefaultPreprocessorRootRule = "preprocessorRootRule";
        public const string PreprocessorPostfix = "Preprocessor";
        public const string LexerPostfix = "Lexer";
        public const string ParserPostfix = "Parser";
        public const string Extension = ".g4";

        public static Grammar CreateDefaultGrammar(string grammarText, string path, string grammarName)
        {
            var grammar = new Grammar();
            grammar.Name = grammarName;
            grammar.Files.Add(Path.Combine(path, grammarName + Extension));
            grammar.Runtimes = new HashSet<Runtime>() { Runtime.Java };

            File.WriteAllText(grammar.Files.First(), grammarText);

            grammar.AgeFileName = Path.Combine(path, grammarName + ".age");
            grammar.Save();
            return grammar;
        }

        public static Grammar CreateOrOpenDefaultGrammar(string path, string grammarName)
        {
            var grammar = new Grammar();
            grammar.Name = grammarName;
            grammar.Files.Add(Path.Combine(path, grammarName + Extension));
            grammar.Runtimes = new HashSet<Runtime>() { Runtime.Java };

            foreach (var file in grammar.Files)
            {
                if (!File.Exists(file))
                {
                    var grammarText = new StringBuilder();
                    grammarText.AppendLine($"grammar {grammarName};");
                    grammarText.AppendLine();
                    grammarText.AppendLine(DefaultRootRule);
                    grammarText.AppendLine("    : tokensOrRules* EOF");
                    grammarText.AppendLine("    ;");
                    grammarText.AppendLine();
                    grammarText.AppendLine("tokensOrRules");
                    grammarText.AppendLine("    : TOKEN+");
                    grammarText.AppendLine("    ;");
                    grammarText.AppendLine();
                    grammarText.AppendLine("TOKEN: [a-z]+;");
                    File.WriteAllText(Path.Combine(path, grammarName + Extension), grammarText.ToString());
                }
            }

            grammar.AgeFileName = Path.Combine(path, grammarName + ".age");
            grammar.Save();
            return grammar;
        }
    }
}
