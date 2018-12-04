using System.Collections.Generic;

namespace AntlrGrammarEditor
{
    public enum CaseInsensitiveType
    {
        None,
        lower,
        UPPER
    }

    public class Grammar
    {
        public const string AntlrDotExt = ".g4";
        public const string LexerPostfix = "Lexer";
        public const string ParserPostfix = "Parser";

        public string Name { get; set; }

        public string LexerSuperClass { get; set; }

        public string ParserSuperClass { get; set; }

        public string Root { get; set; }

        public string FileExtension { get; set; } = "txt";

        public bool SeparatedLexerAndParser { get; set; }

        public CaseInsensitiveType CaseInsensitiveType { get; set; }

        public bool Preprocessor { get; set; }

        public bool PreprocessorCaseInsensitive { get; set; }

        public string PreprocessorRoot { get; set; }

        public bool PreprocessorSeparatedLexerAndParser { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        public List<string> TextFiles { get; set; } = new List<string>();

        public string Directory { get; set; } = "";
    }
}