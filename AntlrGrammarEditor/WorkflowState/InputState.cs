using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class InputState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.Input;

        public override bool HasErrors => false;

        public override WorkflowState PreviousState => null;
    }
}
