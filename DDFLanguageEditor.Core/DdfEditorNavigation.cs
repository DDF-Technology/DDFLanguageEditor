using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfEditorNavigationTarget
    {
        internal DdfEditorNavigationTarget(IDdfEditorDocument document,
            DdfDocumentSymbol symbol, string documentId = null, string documentPath = null)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            Document = document;
            DocumentId = document?.Id ?? documentId ?? string.Empty;
            DocumentPath = document?.FilePath ?? documentPath ?? string.Empty;
        }

        public IDdfEditorDocument Document { get; }
        public DdfDocumentSymbol Symbol { get; }
        public string DocumentId { get; }
        public string DocumentPath { get; }
        public string Name => Symbol.Name;
        public int Start => Symbol.SelectionStart;
        public int Length => Symbol.SelectionLength;
    }

    /// <summary>
    /// Resolves a definition from an immutable editor snapshot and the buffers
    /// explicitly supplied by the host. It performs no file-system access.
    /// </summary>
    public static class DdfEditorNavigationService
    {
        public static DdfEditorNavigationTarget FindDefinition(
            DdfEditorAnalysisSnapshot snapshot,
            IEnumerable<IDdfEditorDocument> documents)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            IReadOnlyList<IDdfEditorDocument> availableDocuments =
                (documents ?? Array.Empty<IDdfEditorDocument>()).Where(item => item != null).ToList();
            IDdfEditorDocument activeDocument = availableDocuments.FirstOrDefault(item =>
                string.Equals(item.Id, snapshot.DocumentId, StringComparison.Ordinal));

            DdfSymbolOccurrence occurrence = snapshot.SemanticModel.FindOccurrence(snapshot.Position);
            if (occurrence != null)
                return new DdfEditorNavigationTarget(activeDocument, occurrence.Symbol,
                    snapshot.DocumentId, snapshot.DocumentPath);

            DdfToken identifier = FindIdentifier(snapshot);
            if (identifier == null) return null;
            string name = snapshot.Source.Substring(identifier.Start, identifier.Length);
            DdfLanguageDefinition language = snapshot.Request.LanguagePackage.Language;
            foreach (IDdfEditorDocument document in availableDocuments)
            {
                if (ReferenceEquals(document, activeDocument) ||
                    string.Equals(document.Id, snapshot.DocumentId, StringComparison.Ordinal)) continue;
                DdfLexResult lex = DdfLexer.Lex(document.SourceText, language);
                DdfParseResult parse = DdfParser.Parse(document.SourceText, lex, language);
                DdfDocumentSymbol symbol = DdfSymbolIndex.Create(parse, document.SourceText).Symbols
                    .FirstOrDefault(item => IsWorkspaceVisible(item.Kind) &&
                        string.Equals(item.Name, name, StringComparison.Ordinal));
                if (symbol != null) return new DdfEditorNavigationTarget(document, symbol);
            }
            return null;
        }

        private static DdfToken FindIdentifier(DdfEditorAnalysisSnapshot snapshot)
        {
            int position = snapshot.Position;
            DdfToken exact = snapshot.LexResult.Tokens.FirstOrDefault(token =>
                token.Kind == DdfTokenKind.Identifier &&
                position >= token.Start && position < token.End);
            if (exact != null) return exact;
            return snapshot.LexResult.Tokens.FirstOrDefault(token =>
                token.Kind == DdfTokenKind.Identifier && position == token.End);
        }

        private static bool IsWorkspaceVisible(DdfSymbolKind kind)
        {
            return kind == DdfSymbolKind.Function || kind == DdfSymbolKind.Structure ||
                   kind == DdfSymbolKind.Variable || kind == DdfSymbolKind.Library;
        }
    }
}
