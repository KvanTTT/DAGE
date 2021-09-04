using System;

namespace AntlrGrammarEditor.Sources
{
    public class SourceWithMarks : Source
    {
        private readonly int[] _markOffsets;
        private readonly int _markLength;
        private readonly Source _originalSource;

        public SourceWithMarks(string fileName, string text, int[] markOffsets, int markLength, Source originalSource)
            : base(fileName, text)
        {
            _markOffsets = markOffsets;
            _markLength = markLength;
            _originalSource = originalSource;
        }

        public TextSpan GetOriginalTextSpan(int start, int length)
        {
            int index = Array.BinarySearch(_markOffsets, start);
            if (index < 0)
                index = ~index - 1;
            int marksCount = index + 1;

            return new TextSpan(start - marksCount * _markLength, length, _originalSource);
        }
    }
}