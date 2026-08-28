using System;

namespace DDFLanguageEditor.Core
{
    public sealed class ClassifiedSpan
    {
        public ClassifiedSpan(int start, int length, SyntaxKind kind)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Start = start;
            Length = length;
            Kind = kind;
        }

        public int Start { get; }

        public int Length { get; }

        public SyntaxKind Kind { get; }
    }
}
