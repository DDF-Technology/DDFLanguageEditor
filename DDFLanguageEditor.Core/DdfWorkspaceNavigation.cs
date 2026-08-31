using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfWorkspaceNavigationLocation
    {
        internal DdfWorkspaceNavigationLocation(DdfWorkspaceSearchDocument document, string name, string detail,
            int start, int length, int line, int column, DdfSymbolKind? symbolKind)
        {
            Document = document;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            SymbolKind = symbolKind;
        }

        public DdfWorkspaceSearchDocument Document { get; }
        public string Name { get; }
        public string Detail { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public DdfSymbolKind? SymbolKind { get; }
    }

    public static class DdfWorkspaceNavigationService
    {
        public static IReadOnlyList<DdfWorkspaceNavigationLocation> ListFiles(
            IEnumerable<DdfWorkspaceSearchDocument> documents)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            return documents
                .Select(document => CreateLocation(document, document.DisplayName, "file", 0, 0, null))
                .OrderBy(location => location.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        public static IReadOnlyList<DdfWorkspaceNavigationLocation> ListSymbols(
            IEnumerable<DdfWorkspaceSearchDocument> documents,
            CancellationToken cancellationToken = default)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            var result = new List<DdfWorkspaceNavigationLocation>();
            foreach (DdfWorkspaceSearchDocument document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DdfParseResult parseResult = DdfParser.Parse(document.Source);
                foreach (DdfDocumentSymbol symbol in Flatten(DdfSymbolIndex.Create(parseResult.Root).Symbols))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(CreateLocation(document, symbol.Name,
                        GetKindLabel(symbol.Kind) + (string.IsNullOrWhiteSpace(symbol.Detail) ? string.Empty : " — " + symbol.Detail),
                        symbol.SelectionStart, symbol.SelectionLength, symbol.Kind));
                }
            }
            return result
                .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Start)
                .ToList()
                .AsReadOnly();
        }

        public static IReadOnlyList<DdfWorkspaceNavigationLocation> FindReferences(
            IEnumerable<DdfWorkspaceSearchDocument> documents,
            string declarationDocumentId,
            int declarationStart,
            CancellationToken cancellationToken = default)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            List<DdfWorkspaceSearchDocument> snapshots = documents.ToList();
            DdfWorkspaceSearchDocument declarationDocument = snapshots.FirstOrDefault(document =>
                string.Equals(document.Id, declarationDocumentId, StringComparison.OrdinalIgnoreCase));
            if (declarationDocument == null) return Array.Empty<DdfWorkspaceNavigationLocation>();

            DdfParseResult declarationParse = DdfParser.Parse(declarationDocument.Source);
            DdfSemanticModel declarationModel = DdfSemanticModel.Create(declarationDocument.Source, declarationParse.Root);
            DdfSymbolOccurrence declaration = declarationModel.Occurrences.FirstOrDefault(occurrence =>
                occurrence.IsDeclaration && occurrence.Start == declarationStart);
            if (declaration == null) return Array.Empty<DdfWorkspaceNavigationLocation>();

            HashSet<int> topLevelStarts = new HashSet<int>(
                DdfSymbolIndex.Create(declarationParse.Root).Symbols.Select(symbol => symbol.SelectionStart));
            if (!topLevelStarts.Contains(declaration.Symbol.SelectionStart))
            {
                return declarationModel.FindOccurrences(declaration.Symbol)
                    .Select(occurrence => CreateLocation(declarationDocument, declaration.Symbol.Name,
                        occurrence.IsDeclaration ? "dichiarazione" : "riferimento",
                        occurrence.Start, occurrence.Length, declaration.Symbol.Kind))
                    .ToList()
                    .AsReadOnly();
            }

            string name = declaration.Symbol.Name;
            var result = new List<DdfWorkspaceNavigationLocation>();
            foreach (DdfWorkspaceSearchDocument document in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DdfParseResult parse = DdfParser.Parse(document.Source);
                DdfSemanticModel model = DdfSemanticModel.Create(document.Source, parse.Root);
                HashSet<int> globalStarts = new HashSet<int>(
                    DdfSymbolIndex.Create(parse.Root).Symbols.Select(symbol => symbol.SelectionStart));
                DdfLexResult lex = DdfLexer.Lex(document.Source);
                foreach (DdfToken token in lex.Tokens)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (token.Kind != DdfTokenKind.Identifier || token.Length != name.Length ||
                        !string.Equals(document.Source.Substring(token.Start, token.Length), name, StringComparison.Ordinal)) continue;
                    DdfSymbolOccurrence occurrence = model.FindOccurrence(token.Start);
                    if (occurrence != null && !globalStarts.Contains(occurrence.Symbol.SelectionStart)) continue;
                    bool isDeclaration = occurrence?.IsDeclaration == true;
                    result.Add(CreateLocation(document, name, isDeclaration ? "dichiarazione" : "riferimento",
                        token.Start, token.Length, declaration.Symbol.Kind));
                }
            }
            return result
                .OrderBy(location => location.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Start)
                .ToList()
                .AsReadOnly();
        }

        public static bool TryParseLineColumn(string value, int lineCount, out int line, out int column)
        {
            line = 0;
            column = 1;
            string[] parts = (value ?? string.Empty).Trim().Split(':');
            if (parts.Length < 1 || parts.Length > 2 || !int.TryParse(parts[0], out line) || line < 1 || line > lineCount)
                return false;
            if (parts.Length == 2 && (!int.TryParse(parts[1], out column) || column < 1)) return false;
            return true;
        }

        public static int GetPosition(string source, int line, int column)
        {
            source = source ?? string.Empty;
            int currentLine = 1;
            int start = 0;
            while (currentLine < line && start < source.Length)
            {
                int next = source.IndexOf('\n', start);
                if (next < 0) return source.Length;
                start = next + 1;
                currentLine++;
            }
            int end = source.IndexOf('\n', start);
            if (end < 0) end = source.Length;
            if (end > start && source[end - 1] == '\r') end--;
            return Math.Min(start + Math.Max(0, column - 1), end);
        }

        private static DdfWorkspaceNavigationLocation CreateLocation(DdfWorkspaceSearchDocument document,
            string name, string detail, int start, int length, DdfSymbolKind? symbolKind)
        {
            int safeStart = Math.Max(0, Math.Min(start, document.Source.Length));
            int line = 1;
            int lineStart = 0;
            for (int index = 0; index < safeStart; index++)
            {
                if (document.Source[index] != '\n') continue;
                line++;
                lineStart = index + 1;
            }
            return new DdfWorkspaceNavigationLocation(document, name, detail, safeStart, length,
                line, safeStart - lineStart + 1, symbolKind);
        }

        private static IEnumerable<DdfDocumentSymbol> Flatten(IEnumerable<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                yield return symbol;
                foreach (DdfDocumentSymbol child in Flatten(symbol.Children)) yield return child;
            }
        }

        private static string GetKindLabel(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "libreria";
                case DdfSymbolKind.Structure: return "struttura";
                case DdfSymbolKind.Function: return "funzione";
                case DdfSymbolKind.Parameter: return "parametro";
                case DdfSymbolKind.Field: return "campo";
                default: return "variabile";
            }
        }
    }
}
