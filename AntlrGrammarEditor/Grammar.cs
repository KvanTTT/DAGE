using System.IO;

namespace AntlrGrammarEditor
{
    public class Grammar
    {
        public const string AntlrDotExt = ".g4";
        public const string LexerPostfix = "Lexer";
        public const string ParserPostfix = "Parser";

        public string Name { get; }

        public string Directory { get; }

        public string? Root { get; }

        public string? Package { get; }

        public string? TextExtension { get; }

        public string FullFileName => Path.Combine(Directory, Name + AntlrDotExt);

        public string ExamplesDirectory => Path.Combine(Directory, "examples");

        public string DotTextExtension => string.IsNullOrEmpty(TextExtension) ? "" : "." + TextExtension;

        public Grammar(string name, string directory, string? root = null, string? package = null, string? textExtension = null)
        {
            Name = name;
            Directory = directory;
            TextExtension = textExtension;
            Root = root;
            Package = package;
        }

        public override string ToString() => FullFileName;
    }
}