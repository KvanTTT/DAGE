using System.IO;

namespace AntlrGrammarEditor
{
    public class FileName
    {
        public string FullFileName { get; set; }

        public string ShortFileName => Path.GetFileName(FullFileName);

        public static FileName Empty => new FileName("");

        public FileName(string fullFileName)
        {
            FullFileName = fullFileName;
        }

        public override bool Equals(object obj)
        {
            var fileName = obj as FileName;
            if (fileName == null)
            {
                return false;
            }

            return FullFileName.Equals(fileName.FullFileName);
        }

        public override int GetHashCode()
        {
            return FullFileName.GetHashCode();
        }

        public override string ToString() => ShortFileName;
    }
}
