using System;
using System.Collections.Generic;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor.Fragments
{
    public class FragmentFinder
    {
        private readonly Source _source;
        private readonly IReadOnlyList<MappedFragment> _fragments;
        private readonly int[] _offsets;

        public FragmentFinder(Source source, IReadOnlyList<MappedFragment> fragments)
        {
            _source = source;
            _fragments = fragments;
            _offsets = new int[fragments.Count * 2];
            int index = 0;
            foreach (var fragment in fragments)
            {
                var textSpan = fragment.TextSpan;
                _offsets[index++] = textSpan.Start;
                _offsets[index++] = textSpan.End;
            }
        }

        public MappedFragmentWithLocalOffset Find(int line, int column)
        {
            var position = _source.LineColumnToPosition(line, column);
            var originalIndex = Array.BinarySearch(_offsets, position);
            var elementIndex = originalIndex == -1
                ? 0
                : originalIndex < 0
                    ? ~originalIndex - 1
                    : originalIndex;

            // Check if position is out of any fragment
            if (elementIndex % 2 == 1 && elementIndex < _offsets.Length - 1)
            {
                // More smart fragment detection is probably required
                var diffWithPrevious = position - _offsets[elementIndex];
                var diffWithNext = _offsets[elementIndex + 1] - position;
                if (diffWithNext < diffWithPrevious)
                    elementIndex = elementIndex + 1;
            }

            var fragmentNumber = elementIndex / 2;
            var fragment = _fragments[fragmentNumber];
            int localOffset = position - fragment.TextSpan.Start;
            return new(fragment, localOffset < 0 ? 0 : localOffset);
        }
    }
}