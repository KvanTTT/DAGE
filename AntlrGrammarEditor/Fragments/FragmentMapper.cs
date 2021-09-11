using System;
using System.Collections.Generic;
using System.Linq;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Fragments
{
    public class FragmentMapper
    {
        private readonly Runtime _runtime;
        private readonly Source _source;
        private readonly IReadOnlyList<MappedFragment> _fragments;
        private readonly int[] _offsets;
        private readonly Dictionary<MappedFragment, (TextSpan FromGenerated, TextSpan FromGrammar)[]> _mappedLines;

        public FragmentMapper(Runtime runtime, Source source, IReadOnlyList<MappedFragment> fragments)
        {
            _runtime = runtime;
            _source = source;
            _fragments = fragments;
            _offsets = new int[fragments.Count * 2];
            _mappedLines = new Dictionary<MappedFragment, (TextSpan FromGenerated, TextSpan FromGrammar)[]>(fragments.Count);
            int offsetsIndex = 0;
            foreach (var fragment in fragments)
            {
                var textSpan = fragment.TextSpan;
                _offsets[offsetsIndex++] = textSpan.Start;
                _offsets[offsetsIndex++] = textSpan.End;
                _mappedLines.Add(fragment, CalculateLinesMap(fragment));
            }
        }

        private (TextSpan FromGrammar, TextSpan FromGenerated)[] CalculateLinesMap(MappedFragment fragment)
        {
            var fragmentInGrammar = GetGrammarFragment(fragment);

            var fragmentLineSpans = fragment.TextSpan.GetLineTextSpans();
            // TODO: it's only actual for members
            var fragmentInGeneratedLineSpans = _runtime == Runtime.Go && fragmentLineSpans.ElementAt(0).Length == 0
                ? fragmentLineSpans.Skip(1)
                : fragmentLineSpans;
            var fragmentInGrammarLineSpans = fragmentInGrammar.TextSpan.GetLineTextSpans();

            return fragmentInGeneratedLineSpans.Zip(fragmentInGrammarLineSpans, (textSpanFromGenerated, textSpanFromGrammar) =>
                (textSpanFromGenerated, textSpanFromGrammar)).ToArray();
        }

        public MappedResult Map(int line, int column)
        {
            var position = _source.LineColumnToPosition(line, column);
            var textSpan = new TextSpan(position, 0, _source);
            var originalIndex = Array.BinarySearch(_offsets, position);
            var elementIndex = originalIndex == -1
                ? 0
                : originalIndex < 0
                    ? ~originalIndex - 1
                    : originalIndex;

            // Check if position is out of any fragment
            bool isExact = true;
            if (elementIndex % 2 == 1 && elementIndex < _offsets.Length - 1)
            {
                // More smart fragment detection is probably required
                var diffWithPrevious = position - _offsets[elementIndex];
                var diffWithNext = _offsets[elementIndex + 1] - position;
                if (diffWithNext < diffWithPrevious)
                    elementIndex = elementIndex + 1;
                isExact = false;
            } else if (originalIndex == -1 || position >= _offsets[^1])
            {
                isExact = false;
            }

            var fragmentNumber = elementIndex / 2;
            var fragment = _fragments[fragmentNumber];
            var fragmentInGrammar = GetGrammarFragment(fragment);
            TextSpan textSpanInGrammar;

            if (isExact)
            {
                var grammarLineColumn = fragmentInGrammar.TextSpan.ToLineColumn();
                int lineInGrammar = grammarLineColumn.BeginLine;
                int columnInGrammar = grammarLineColumn.BeginColumn;

                var mappedLines = _mappedLines[fragment];
                for (var lineIndex = 1; lineIndex < mappedLines.Length + 1; lineIndex++)
                {
                    if (lineIndex >= mappedLines.Length || position < mappedLines[lineIndex].FromGenerated.Start)
                    {
                        var previousLineIndex = lineIndex - 1;
                        var previousLine = mappedLines[previousLineIndex];
                        lineInGrammar += previousLineIndex;
                        columnInGrammar = previousLineIndex > 0
                            ? column - (previousLine.FromGenerated.Length - previousLine.FromGrammar.Length)
                            : grammarLineColumn.BeginColumn + (column - previousLine.FromGenerated.LineColumn.BeginColumn);

                        if (columnInGrammar < LineColumnTextSpan.StartColumn)
                            columnInGrammar = LineColumnTextSpan.StartColumn;
                        break;
                    }
                }

                textSpanInGrammar =
                    new LineColumnTextSpan(lineInGrammar, columnInGrammar, fragmentInGrammar.TextSpan.Source).ToLinear();
            }
            else
            {
                textSpanInGrammar = fragmentInGrammar.TextSpan;
            }

            return new MappedResult(fragment, textSpan, textSpanInGrammar);
        }

        private Fragment GetGrammarFragment(MappedFragment fragmentInGenerated)
        {
            var fragmentInMarkedGrammar = (MappedFragment)fragmentInGenerated.OriginalFragment;
            return (Fragment)fragmentInMarkedGrammar.OriginalFragment;
        }
    }
}