using System.IO;
using AntlrGrammarEditor.Sources;
using Avalonia.Media;

namespace DesktopAntlrGrammarEditor
{
    public class GrammarOrRuntimeFile
    {
        public Source Source { get; }

        public bool IsGenerated { get; }

        public string ShortFileName => Path.GetFileName(Source.Name);

        public IBrush Foreground => IsGenerated ? Brushes.Gray : Brushes.Black;

        public GrammarOrRuntimeFile(Source source, bool isGenerated)
        {
            Source = source;
            IsGenerated = isGenerated;
        }

        public override string ToString() => ShortFileName;

        /*public override bool Equals(object? obj)
        {
            if (obj is GrammarOrRuntimeFile grammarOrRuntimeFile)
                return Source.Equals(grammarOrRuntimeFile.Source);

            return false;
        }*/
    }
}