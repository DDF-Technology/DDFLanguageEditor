using System;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfToken
    {
        public DdfToken(DdfTokenKind kind, int start, int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Kind = kind;
            Start = start;
            Length = length;
        }

        public DdfTokenKind Kind { get; }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;
    }
}
