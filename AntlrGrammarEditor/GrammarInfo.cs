using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class GrammarInfo
    {
        public string Name { get; set; }

        public int ErrorsCount { get; set; }

        public GrammarInfo()
        {
        }

        public GrammarInfo(string name, int errorsCount)
        {
            Name = name;
            ErrorsCount = errorsCount;
        }
    }
}
