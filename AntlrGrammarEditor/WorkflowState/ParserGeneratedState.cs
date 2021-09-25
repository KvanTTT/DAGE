using System.Collections.Generic;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.WorkflowState
{
    public class ParserGeneratedState : WorkflowState
    {
        public override WorkflowStage Stage => WorkflowStage.ParserGenerated;

        public override WorkflowState PreviousState => GrammarCheckedState;

        public GrammarCheckedState GrammarCheckedState { get; }

        public string? LexerName { get; }

        public string? ParserName { get; }

        public Runtime Runtime { get; }

        public string? PackageName { get; }

        public bool IncludeListener { get; }

        public bool IncludeVisitor { get; }

        public IReadOnlyList<MappedFragment> MappedFragments { get; }

        public IReadOnlyDictionary<string, Source> GrammarSources { get; }

        public IReadOnlyDictionary<string, RuntimeFileInfo> RuntimeFileInfos { get; }

        public ParserGeneratedState(GrammarCheckedState grammarCheckedState,
            string? packageName,
            Runtime runtime,
            bool includeListener,
            bool includeVisitor,
            string? lexerName,
            string? parserName,
            IReadOnlyList<MappedFragment> mappedFragments,
            IReadOnlyDictionary<string, Source> grammarSources,
            IReadOnlyDictionary<string, RuntimeFileInfo> runtimeFileInfos)
        {
            GrammarCheckedState = grammarCheckedState;
            PackageName = packageName;
            Runtime = runtime;
            IncludeListener = includeListener;
            IncludeVisitor = includeVisitor;
            LexerName = lexerName;
            ParserName = parserName;
            MappedFragments = mappedFragments;
            GrammarSources = grammarSources;
            RuntimeFileInfos = runtimeFileInfos;
        }

        public TextSpan GetOriginalTextSpanForLineColumn(string grammarFileName, int line, int column)
        {
            var grammarSource = GrammarSources[grammarFileName];
            var position = grammarSource.LineColumnToPosition(line, column);
            if (grammarSource is SourceWithMarks codeSourceWithOffsets)
                return codeSourceWithOffsets.GetOriginalTextSpan(position, 0);

            return new TextSpan(position, 0, grammarSource);
        }
    }
}
