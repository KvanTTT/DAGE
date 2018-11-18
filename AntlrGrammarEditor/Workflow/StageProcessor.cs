using System;

namespace AntlrGrammarEditor
{
    public abstract class StageProcessor
    {
        public EventHandler<ParsingError> ErrorEvent;
    }
}