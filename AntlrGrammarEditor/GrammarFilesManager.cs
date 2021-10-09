using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AntlrGrammarEditor
{
    public class GrammarFilesManager
    {
        public Grammar Grammar { get; }

        public GrammarProjectType GrammarProjectType { get; }

        public static readonly Regex GrammarNameRegex = new Regex(@"grammar\s+(\w+);", RegexOptions.Compiled);

        public static Grammar GetGrammarWithNotConflictingName(string directory, string defaultName = "TestGrammar",
            GrammarProjectType grammarProjectType = GrammarProjectType.Combined)
        {
            Grammar grammar;
            GrammarFilesManager grammarFilesManager;

            var suffix = "";
            do
            {
                grammar = new Grammar(defaultName + suffix, directory);
                grammarFilesManager = new GrammarFilesManager(grammar, grammarProjectType);
                suffix += "_";
            } while (grammarFilesManager.CheckExistence() != null);

            return grammar;
        }

        public static string GetNotConflictingTextFile(Grammar grammar)
        {
            var suffix = "";
            string fileName;
            do
            {
                fileName = Path.Combine(grammar.ExamplesDirectory, "test" + suffix + grammar.DotTextExtension);
                suffix += "_";
            } while (File.Exists(fileName));

            return fileName;
        }

        public GrammarFilesManager(Grammar grammar, GrammarProjectType grammarProjectType = GrammarProjectType.Combined)
        {
            Grammar = grammar;
            GrammarProjectType = grammarProjectType;
        }

        public string? CheckExistence()
        {
            var fileNamesWithTypes = CreateFileNamesWithTypes();

            foreach (var (_, fileName) in fileNamesWithTypes)
            {
                if (File.Exists(fileName))
                {
                    return fileName;
                }
            }

            return null;
        }

        public void CreateFiles(string? lexerContent = null, string? parserContent = null, string? combinedContent = null)
        {
            var name = Grammar.Name;
            var directory = Grammar.Directory;

            var fileNamesWithTypes = CreateFileNamesWithTypes();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            foreach (var (grammarFileType, fileName) in fileNamesWithTypes)
            {
                string grammarContent;
                string grammarFileName;
                if (grammarFileType == GrammarFileType.Lexer && lexerContent != null)
                {
                    grammarContent = lexerContent;
                    grammarFileName = ExtractGrammarName(lexerContent) + Grammar.AntlrDotExt;
                }
                else if (grammarFileType == GrammarFileType.Parser && parserContent != null)
                {
                    grammarContent = parserContent;
                    grammarFileName = ExtractGrammarName(parserContent) + Grammar.AntlrDotExt;
                }
                else if (grammarFileType == GrammarFileType.Combined && combinedContent != null)
                {
                    grammarContent = combinedContent;
                    grammarFileName = ExtractGrammarName(combinedContent) + Grammar.AntlrDotExt;
                }
                else
                {
                    var content = new StringBuilder();

                    if (grammarFileType == GrammarFileType.Lexer)
                    {
                        content.Append("lexer ");
                    }
                    else if (grammarFileType == GrammarFileType.Parser)
                    {
                        content.Append("parser ");
                    }

                    content.Append($"grammar {Grammar.Name};");

                    if (grammarFileType != GrammarFileType.Parser &&
                        Grammar.CaseInsensitiveType != CaseInsensitiveType.None)
                    {
                        content.AppendLine();
                        content.AppendLine();
                        content.Append($"/* CaseInsensitiveType: {Grammar.CaseInsensitiveType} */");
                    }
                    else if (grammarFileType == GrammarFileType.Parser)
                    {
                        content.AppendLine();
                        content.AppendLine();
                        content.Append($"options {{ tokenVocab = {name + Grammar.LexerPostfix}; }}");
                    }

                    if (grammarFileType != GrammarFileType.Lexer)
                    {
                        content.AppendLine();
                        content.AppendLine();
                        content.AppendLine("root");
                        content.AppendLine("    : .*? EOF");
                        content.AppendLine("    ;");
                    }

                    if (grammarFileType != GrammarFileType.Parser)
                    {
                        content.AppendLine("Id         : [A-Za-z]+;");
                        content.AppendLine("Whitespace : [ \\t\\r\\n]+ -> channel(HIDDEN);");
                        content.AppendLine("Number     : [0-9]+;");
                        content.AppendLine();
                    }

                    grammarContent = content.ToString();
                    grammarFileName = Grammar.Name;
                }

                File.WriteAllText(grammarFileName, grammarContent);
            }
        }

        private Dictionary<GrammarFileType, string> CreateFileNamesWithTypes()
        {
            var name = Grammar.Name;
            var directory = Grammar.Directory;
            var result = new Dictionary<GrammarFileType, string>();

            if (GrammarProjectType == GrammarProjectType.Lexer)
            {
                result[GrammarFileType.Lexer] = Path.Combine(directory, name + Grammar.AntlrDotExt);
            }
            else if (GrammarProjectType == GrammarProjectType.Separated)
            {
                result[GrammarFileType.Lexer] = Path.Combine(directory, name + Grammar.LexerPostfix + Grammar.AntlrDotExt);
                result[GrammarFileType.Parser] = Path.Combine(directory, name + Grammar.ParserPostfix + Grammar.AntlrDotExt);
            }
            else
            {
                result[GrammarFileType.Combined] = Path.Combine(directory, name + Grammar.AntlrDotExt);
            }

            return result;
        }

        public static string ExtractGrammarName(string content)
        {
            return GrammarNameRegex.Match(content).Groups[1].Value;
        }
    }
}