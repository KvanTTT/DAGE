using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public abstract class WorkflowState
    {
        public abstract WorkflowStage Stage { get; }

        public virtual bool HasErrors
        {
            get
            {
                return Exception != null;
            }
        }

        public abstract WorkflowState PreviousState { get; }

        public Exception Exception { get; set; }
    }
}
