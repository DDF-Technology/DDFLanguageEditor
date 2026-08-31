using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    public enum DdfWorkspaceSearchKind
    {
        Text,
        Symbol
    }

    public sealed class DdfWorkspaceSearchDocument
    {
        public DdfWorkspaceSearchDocument(string id, string path, string displayName, string source)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A document id is required.", nameof(id));
            Id = id;
            Path = path;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Source = source ?? string.Empty;
        }

        public string Id { get; }
        public string Path { get; }
        public string DisplayName { get; }
        public string Source { get; }
    }

    public sealed class DdfWorkspaceSearchResult
    {
        internal DdfWorkspaceSearchResult(DdfWorkspaceSearchDocument document, int start, int length,
            int line, int column, string preview, DdfWorkspaceSearchKind kind, DdfSymbolKind? symbolKind)
        {
            Document = document;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            Preview = preview;
            Kind = kind;
            SymbolKind = symbolKind;
        }

        public DdfWorkspaceSearchDocument Document { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public string Preview { get; }
        public DdfWorkspaceSearchKind Kind { get; }
        public DdfSymbolKind? SymbolKind { get; }
    }

    public static class DdfWorkspaceSearchService
    {
        public static IReadOnlyList<DdfWorkspaceSearchResult> Search(
            IEnumerable<DdfWorkspaceSearchDocument> documents,
            string query,
            DdfWorkspaceSearchKind kind,
            bool matchCase = false,
            CancellationToken cancellationToken = default)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<DdfWorkspaceSearchResult>();
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var results = new List<DdfWorkspaceSearchResult>();

            foreach (DdfWorkspaceSearchDocument document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<int> lineStarts = BuildLineStarts(document.Source);
                if (kind == DdfWorkspaceSearchKind.Symbol)
                    SearchSymbols(document, query, comparison, lineStarts, results, cancellationToken);
                else
                    SearchText(document, query, comparison, lineStarts, results, cancellationToken);
            }

            return results
                .OrderBy(result => result.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Start)
                .ToList()
                .AsReadOnly();
        }

        private static void SearchText(DdfWorkspaceSearchDocument document, string query,
            StringComparison comparison, IReadOnlyList<int> lineStarts,
            List<DdfWorkspaceSearchResult> results, CancellationToken cancellationToken)
        {
            int position = 0;
            while (position <= document.Source.Length - query.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int match = document.Source.IndexOf(query, position, comparison);
                if (match < 0) break;
                results.Add(CreateResult(document, match, query.Length, lineStarts, DdfWorkspaceSearchKind.Text, null));
                position = match + Math.Max(1, query.Length);
            }
        }

        private static void SearchSymbols(DdfWorkspaceSearchDocument document, string query,
            StringComparison comparison, IReadOnlyList<int> lineStarts,
            List<DdfWorkspaceSearchResult> results, CancellationToken cancellationToken)
        {
            DdfParseResult parseResult = DdfParser.Parse(document.Source);
            IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parseResult.Root).Symbols;
            foreach (DdfDocumentSymbol symbol in Flatten(symbols))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (symbol.Name.IndexOf(query, comparison) < 0) continue;
                results.Add(CreateResult(document, symbol.SelectionStart, symbol.SelectionLength, lineStarts,
                    DdfWorkspaceSearchKind.Symbol, symbol.Kind));
            }
        }

        private static IEnumerable<DdfDocumentSymbol> Flatten(IEnumerable<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                yield return symbol;
                foreach (DdfDocumentSymbol child in Flatten(symbol.Children)) yield return child;
            }
        }

        private static DdfWorkspaceSearchResult CreateResult(DdfWorkspaceSearchDocument document,
            int start, int length, IReadOnlyList<int> lineStarts,
            DdfWorkspaceSearchKind kind, DdfSymbolKind? symbolKind)
        {
            int safeStart = Math.Max(0, Math.Min(start, document.Source.Length));
            int lineIndex = FindLineIndex(lineStarts, safeStart);
            int lineStart = lineStarts[lineIndex];
            int lineEnd = document.Source.IndexOf('\n', safeStart);
            if (lineEnd < 0) lineEnd = document.Source.Length;
            string preview = document.Source.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r').Trim();
            int column = safeStart - lineStart + 1;
            return new DdfWorkspaceSearchResult(document, safeStart, length, lineIndex + 1, column, preview, kind, symbolKind);
        }

        private static IReadOnlyList<int> BuildLineStarts(string source)
        {
            var starts = new List<int> { 0 };
            for (int index = 0; index < source.Length; index++)
                if (source[index] == '\n') starts.Add(index + 1);
            return starts;
        }

        private static int FindLineIndex(IReadOnlyList<int> lineStarts, int position)
        {
            int low = 0;
            int high = lineStarts.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (lineStarts[middle] <= position) low = middle + 1;
                else high = middle - 1;
            }
            return Math.Max(0, high);
        }
    }
}
