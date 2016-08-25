using System.IO;

namespace AntlrGrammarEditor
{
    public class FileName
    {
        public FileName(string fileName)
        {
            LongFileName = fileName;
            ShortFileName = Path.GetFileName(LongFileName);
        }

        public readonly string LongFileName;

        public readonly string ShortFileName;

        public override bool Equals(object obj)
        {
            if (obj as FileName == null)
            {
                return false;
            }

            return LongFileName == ((FileName)obj).LongFileName;
        }

        public override int GetHashCode()
        {
            return LongFileName.GetHashCode();
        }

        public override string ToString()
        {
            return ShortFileName;
        }
    }
}
