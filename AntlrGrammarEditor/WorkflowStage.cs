using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public enum WorkflowStage
    {
        Input,
        GrammarChecked,
        ParserGenerated,
        ParserCompilied,
        TextTokenized,
        TextParsed
    }
}
