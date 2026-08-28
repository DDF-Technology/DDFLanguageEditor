using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfCompletionKind
    {
        Keyword,
        Type,
        Boolean,
        Function,
        Structure,
        Parameter,
        Field,
        Variable,
        Library
    }

    public sealed class DdfCompletionItem
    {
        public DdfCompletionItem(string displayText, string insertionText, DdfCompletionKind kind, string detail)
        {
            if (string.IsNullOrWhiteSpace(displayText)) throw new ArgumentException("Display text is required.", nameof(displayText));
            if (string.IsNullOrEmpty(insertionText)) throw new ArgumentException("Insertion text is required.", nameof(insertionText));
            DisplayText = displayText;
            InsertionText = insertionText;
            Kind = kind;
            Detail = detail ?? string.Empty;
        }

        public string DisplayText { get; }
        public string InsertionText { get; }
        public DdfCompletionKind Kind { get; }
        public string Detail { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? DisplayText : DisplayText + "    " + Detail;
        }
    }

    public sealed class DdfCompletionResult
    {
        public DdfCompletionResult(int replacementStart, int replacementLength, IReadOnlyList<DdfCompletionItem> items)
        {
            if (replacementStart < 0) throw new ArgumentOutOfRangeException(nameof(replacementStart));
            if (replacementLength < 0) throw new ArgumentOutOfRangeException(nameof(replacementLength));
            ReplacementStart = replacementStart;
            ReplacementLength = replacementLength;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int ReplacementStart { get; }
        public int ReplacementLength { get; }
        public IReadOnlyList<DdfCompletionItem> Items { get; }
    }

    public static class DdfCompletionService
    {
        public static DdfCompletionResult GetCompletions(
            string source,
            int position,
            bool includeAll = false,
            DdfLanguageDefinition language = null)
        {
            source = source ?? string.Empty;
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            language = language ?? DdfLanguageCatalog.Default;

            DdfLexResult lexResult = DdfLexer.Lex(source, language);
            bool libraryContext = TryGetLibraryPrefix(source, position, out int prefixStart) &&
                                  IsLibraryDirectiveAt(lexResult, prefixStart - 3);
            if (!libraryContext)
            {
                prefixStart = position;
                while (prefixStart > 0 && IsIdentifierPart(source[prefixStart - 1])) prefixStart--;
            }

            string prefix = source.Substring(prefixStart, position - prefixStart);
            if (!libraryContext && IsInProtectedToken(lexResult, position))
            {
                return Empty(prefixStart, prefix.Length);
            }

            DdfParseResult parseResult = DdfParser.Parse(source, lexResult, language);
            IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parseResult.Root).Symbols;
            var candidates = new List<DdfCompletionItem>();

            if (libraryContext)
            {
                AddLibrarySymbols(candidates, symbols);
            }
            else
            {
                foreach (DdfKeywordDefinition keyword in language.Keywords)
                {
                    candidates.Add(new DdfCompletionItem(
                        keyword.Text,
                        keyword.Text,
                        keyword.TokenKind == DdfTokenKind.DataTypeKeyword ? DdfCompletionKind.Type : DdfCompletionKind.Keyword,
                        DescribeKeyword(keyword)));
                }

                foreach (string literal in language.BooleanLiterals)
                {
                    candidates.Add(new DdfCompletionItem(literal, literal, DdfCompletionKind.Boolean, "valore booleano"));
                }

                AddVisibleSymbols(candidates, symbols, position);
            }

            IEnumerable<DdfCompletionItem> filtered = candidates
                .Where(item => (includeAll || prefix.Length > 0) &&
                               item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.DisplayText, StringComparer.Ordinal)
                .Select(group => group.OrderBy(item => KindRank(item.Kind)).First())
                .OrderBy(item => PrefixRank(item.DisplayText, prefix))
                .ThenBy(item => KindRank(item.Kind))
                .ThenBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase);

            return new DdfCompletionResult(prefixStart, prefix.Length, filtered.ToList().AsReadOnly());
        }

        private static bool IsInProtectedToken(DdfLexResult lexResult, int position)
        {
            foreach (DdfToken token in lexResult.Tokens)
            {
                bool contains = position > token.Start && position <= token.End;
                if (!contains) continue;
                return token.Kind == DdfTokenKind.LineComment ||
                       token.Kind == DdfTokenKind.BlockComment ||
                       token.Kind == DdfTokenKind.StringLiteral ||
                       token.Kind == DdfTokenKind.LibraryDirective;
            }

            return false;
        }

        private static bool IsLibraryDirectiveAt(DdfLexResult lexResult, int markerStart)
        {
            return lexResult.Tokens.Any(token =>
                token.Kind == DdfTokenKind.LibraryDirective && token.Start == markerStart);
        }

        private static bool TryGetLibraryPrefix(string source, int position, out int prefixStart)
        {
            int lineStart = position == 0 ? 0 : source.LastIndexOf('\n', position - 1) + 1;
            int marker = source.IndexOf("@@'", lineStart, StringComparison.Ordinal);
            if (marker >= 0 && marker + 3 <= position && source.IndexOf('\'', marker + 3, position - marker - 3) < 0)
            {
                prefixStart = marker + 3;
                return true;
            }

            prefixStart = position;
            return false;
        }

        private static void AddVisibleSymbols(List<DdfCompletionItem> result, IReadOnlyList<DdfDocumentSymbol> symbols, int position)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                if (symbol.Kind != DdfSymbolKind.Variable || symbol.SelectionStart <= position)
                {
                    AddSymbol(result, symbol);
                }
                if ((symbol.Kind == DdfSymbolKind.Function || symbol.Kind == DdfSymbolKind.Structure) &&
                    position >= symbol.Start && position < symbol.End)
                {
                    foreach (DdfDocumentSymbol child in symbol.Children.Where(child => child.SelectionStart <= position))
                    {
                        AddSymbol(result, child);
                    }
                }
            }
        }

        private static void AddLibrarySymbols(List<DdfCompletionItem> result, IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol library in Flatten(symbols).Where(symbol => symbol.Kind == DdfSymbolKind.Library))
            {
                AddSymbol(result, library);
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

        private static void AddSymbol(List<DdfCompletionItem> result, DdfDocumentSymbol symbol)
        {
            result.Add(new DdfCompletionItem(symbol.Name, symbol.Name, ToCompletionKind(symbol.Kind), symbol.Detail));
        }

        private static DdfCompletionKind ToCompletionKind(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return DdfCompletionKind.Library;
                case DdfSymbolKind.Structure: return DdfCompletionKind.Structure;
                case DdfSymbolKind.Function: return DdfCompletionKind.Function;
                case DdfSymbolKind.Parameter: return DdfCompletionKind.Parameter;
                case DdfSymbolKind.Field: return DdfCompletionKind.Field;
                default: return DdfCompletionKind.Variable;
            }
        }

        private static string DescribeKeyword(DdfKeywordDefinition keyword)
        {
            if (keyword.TokenKind == DdfTokenKind.DataTypeKeyword) return "tipo";
            if (keyword.TokenKind == DdfTokenKind.ControlFlowKeyword) return "controllo di flusso";
            return "parola chiave";
        }

        private static int PrefixRank(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.Ordinal) ? 0 : 1;
        }

        private static int KindRank(DdfCompletionKind kind)
        {
            switch (kind)
            {
                case DdfCompletionKind.Parameter: return 0;
                case DdfCompletionKind.Variable: return 1;
                case DdfCompletionKind.Field: return 2;
                case DdfCompletionKind.Function: return 3;
                case DdfCompletionKind.Structure: return 4;
                case DdfCompletionKind.Type: return 5;
                case DdfCompletionKind.Keyword: return 6;
                case DdfCompletionKind.Boolean: return 7;
                default: return 8;
            }
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static DdfCompletionResult Empty(int start, int length)
        {
            return new DdfCompletionResult(start, length, new List<DdfCompletionItem>().AsReadOnly());
        }
    }
}
