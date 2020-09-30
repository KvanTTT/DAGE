namespace AntlrGrammarEditor.WorkflowState
{
    public static class StateUtils
    {
        public static T GetState<T>(this IWorkflowState workflowState)
            where T : IWorkflowState
        {
            IWorkflowState state = workflowState;

            while (state != null)
            {
                if (state is T stateT)
                {
                    return stateT;
                }

                state = state.PreviousState;
            }

            return default;
        }
    }
}