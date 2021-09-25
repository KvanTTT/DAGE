using System;
using System.IO;
using System.Reflection;
using System.Xml;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DesktopAntlrGrammarEditor
{
    public static class Utils
    {
        public static string DefaultGrammarsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DAGE Grammars");

        public static void SetupHightlighting(this TextEditor textBox, string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".g4")
            {
                using Stream? stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("DesktopAntlrGrammarEditor.Antlr4.xshd");
                if (stream != null)
                {
                    using XmlTextReader reader = new XmlTextReader(stream);
                    textBox.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            else
            {
                textBox.SyntaxHighlighting =
                    HighlightingManager.Instance.GetDefinitionByExtension(extension);
            }
        }
    }
}