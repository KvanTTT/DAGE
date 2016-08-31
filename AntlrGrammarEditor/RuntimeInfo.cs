using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class RuntimeInfo
    {
        public readonly Runtime Runtime;
        public readonly string Language;
        public readonly string Extension;

        public RuntimeInfo(Runtime runtime, string language, string extension)
        {
            Language = language;
            Extension = extension;
        }
    }
}
