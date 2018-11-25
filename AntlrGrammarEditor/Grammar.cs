using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.IO;

namespace AntlrGrammarEditor
{
    public class Grammar
    {
        public const string AntlrDotExt = ".g4";
        public const string ProjectDotExt = ".age";
        public const string LexerPostfix = "Lexer";
        public const string ParserPostfix = "Parser";

        public string Name { get; set; }

        public string Root { get; set; }

        public string FileExtension { get; set; } = "txt";

        public HashSet<Runtime> Runtimes = new HashSet<Runtime>();

        public bool SeparatedLexerAndParser { get; set; }

        public bool CaseInsensitive { get; set; }

        public bool Preprocessor { get; set; }

        public bool PreprocessorCaseInsensitive { get; set; }

        public string PreprocessorRoot { get; set; }

        public bool PreprocessorSeparatedLexerAndParser { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        public List<string> TextFiles { get; set; } = new List<string>();

        public string Directory { get; set; } = "";
    }
}