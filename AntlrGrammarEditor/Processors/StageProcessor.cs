using System;

namespace AntlrGrammarEditor.Processors
{
    public abstract class StageProcessor
    {
        public EventHandler<ParsingError> ErrorEvent;
    }
}