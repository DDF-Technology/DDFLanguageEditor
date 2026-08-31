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

    public sealed class DdfWorkspaceReplacementChange
    {
        internal DdfWorkspaceReplacementChange(DdfWorkspaceSearchDocument document, string updatedSource, int replacementCount)
        {
            Document = document;
            UpdatedSource = updatedSource;
            ReplacementCount = replacementCount;
        }

        public DdfWorkspaceSearchDocument Document { get; }
        public string UpdatedSource { get; }
        public int ReplacementCount { get; }
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

        public static IReadOnlyList<DdfWorkspaceReplacementChange> CreateReplacementChanges(
            IEnumerable<DdfWorkspaceSearchResult> selectedResults,
            string replacement,
            CancellationToken cancellationToken = default)
        {
            if (selectedResults == null) throw new ArgumentNullException(nameof(selectedResults));
            replacement = replacement ?? string.Empty;
            var changes = new List<DdfWorkspaceReplacementChange>();
            foreach (IGrouping<string, DdfWorkspaceSearchResult> group in selectedResults.GroupBy(
                result => result.Document.Id, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<DdfWorkspaceSearchResult> results = group.OrderBy(result => result.Start).ToList();
                if (results.Count == 0) continue;
                DdfWorkspaceSearchDocument document = results[0].Document;
                int previousEnd = -1;
                foreach (DdfWorkspaceSearchResult result in results)
                {
                    if (result.Kind != DdfWorkspaceSearchKind.Text ||
                        !string.Equals(result.Document.Id, document.Id, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(result.Document.Source, document.Source, StringComparison.Ordinal))
                        throw new ArgumentException("I risultati selezionati non appartengono allo stesso snapshot testuale.", nameof(selectedResults));
                    if (result.Start < previousEnd || result.Start < 0 || result.Length < 0 ||
                        result.Start + result.Length > document.Source.Length)
                        throw new ArgumentException("I risultati selezionati contengono intervalli sovrapposti o non validi.", nameof(selectedResults));
                    previousEnd = result.Start + result.Length;
                }

                var builder = new System.Text.StringBuilder(document.Source.Length);
                int sourcePosition = 0;
                foreach (DdfWorkspaceSearchResult result in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.Append(document.Source, sourcePosition, result.Start - sourcePosition);
                    builder.Append(replacement);
                    sourcePosition = result.Start + result.Length;
                }
                builder.Append(document.Source, sourcePosition, document.Source.Length - sourcePosition);
                changes.Add(new DdfWorkspaceReplacementChange(document, builder.ToString(), results.Count));
            }
            return changes.AsReadOnly();
        }

        public static string CreateReplacementPreview(DdfWorkspaceSearchResult result, string replacement)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            replacement = replacement ?? string.Empty;
            string source = result.Document.Source;
            int lineStart = result.Start == 0 ? 0 : source.LastIndexOf('\n', result.Start - 1) + 1;
            int lineEnd = source.IndexOf('\n', result.Start);
            if (lineEnd < 0) lineEnd = source.Length;
            string original = source.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r').Trim();
            string updated = source.Substring(lineStart, result.Start - lineStart) + replacement +
                             source.Substring(result.Start + result.Length, lineEnd - result.Start - result.Length);
            return original + "  →  " + updated.TrimEnd('\r').Trim();
        }
    }
}
