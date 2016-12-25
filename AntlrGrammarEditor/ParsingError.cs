﻿using System;

namespace AntlrGrammarEditor
{
    public class ParsingError
    {
        public TextSpan TextSpan { get; set; }

        public string Message { get; set; }

        public WorkflowStage WorkflowStage { get; set; } = WorkflowStage.GrammarChecked;

        public ParsingError()
        {
        }

        public ParsingError(Exception ex, WorkflowStage stage)
        {
            TextSpan = TextSpan.Empty;
            Message = ex.ToString();
            WorkflowStage = stage;
        }

        public ParsingError(string message, CodeSource codeSource, WorkflowStage stage)
            : this(0, 0, message, codeSource, stage)
        {
        }

        public ParsingError(int line, int column, string message, CodeSource codeSource, WorkflowStage stage)
            : this(new TextSpan(codeSource, codeSource.LineColumnToPosition(new LineColumn(line, column)), 1), message, stage)
        {
            Message = message;
            WorkflowStage = stage;
        }

        public ParsingError(TextSpan textSpan, string message, WorkflowStage stage)
        {
            TextSpan = textSpan;
            Message = message;
            WorkflowStage = stage;
        }

        public override bool Equals(object obj)
        {
            var parsingError = obj as ParsingError;
            if (parsingError != null)
            {
                return TextSpan.Equals(parsingError.TextSpan) &&
                       Message == parsingError.Message &&
                       WorkflowStage == parsingError.WorkflowStage;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public override string ToString()
        {
            return WorkflowStage + ":" + Message;
        }
    }
}
