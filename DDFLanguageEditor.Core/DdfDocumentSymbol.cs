using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public enum DdfSymbolKind
    {
        Library,
        Structure,
        Function,
        Parameter,
        Field,
        Variable
    }

    public sealed class DdfDocumentSymbol
    {
        public DdfDocumentSymbol(
            string name,
            DdfSymbolKind kind,
            string detail,
            int start,
            int length,
            int selectionStart,
            int selectionLength,
            IReadOnlyList<DdfDocumentSymbol> children)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A symbol name is required.", nameof(name));
            if (start < 0 || length < 0 || selectionStart < 0 || selectionLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            Name = name;
            Kind = kind;
            Detail = detail ?? string.Empty;
            Start = start;
            Length = length;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
            Children = children ?? throw new ArgumentNullException(nameof(children));
        }

        public string Name { get; }
        public DdfSymbolKind Kind { get; }
        public string Detail { get; }
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public int SelectionStart { get; }
        public int SelectionLength { get; }
        public IReadOnlyList<DdfDocumentSymbol> Children { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? Name : Name + "  " + Detail;
        }
    }
}
