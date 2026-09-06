using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
        Library,
        Snippet
    }

    public enum DdfCompletionContextKind
    {
        General,
        Statement,
        Expression,
        Type,
        Member,
        Library
    }

    public sealed class DdfCompletionItem
    {
        public DdfCompletionItem(string displayText, string insertionText, DdfCompletionKind kind, string detail)
            : this(displayText, insertionText, kind, detail, string.Empty, string.Empty, int.MaxValue)
        {
        }

        public DdfCompletionItem(
            string displayText,
            string insertionText,
            DdfCompletionKind kind,
            string detail,
            string typeName,
            string origin,
            int proximity)
            : this(displayText, insertionText, kind, detail, typeName, origin, proximity, null)
        {
        }

        public DdfCompletionItem(
            string displayText,
            string insertionText,
            DdfCompletionKind kind,
            string detail,
            string typeName,
            string origin,
            int proximity,
            DdfSnippetTemplate snippet)
        {
            if (string.IsNullOrWhiteSpace(displayText)) throw new ArgumentException("Display text is required.", nameof(displayText));
            if (string.IsNullOrEmpty(insertionText)) throw new ArgumentException("Insertion text is required.", nameof(insertionText));
            DisplayText = displayText;
            InsertionText = insertionText;
            Kind = kind;
            Detail = detail ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Origin = origin ?? string.Empty;
            Proximity = Math.Max(0, proximity);
            Snippet = snippet;
        }

        public string DisplayText { get; }
        public string InsertionText { get; }
        public DdfCompletionKind Kind { get; }
        public string Detail { get; }
        public string TypeName { get; }
        public string Origin { get; }
        public int Proximity { get; }
        public DdfSnippetTemplate Snippet { get; }
        public string CategoryLabel => DescribeKind(Kind);
        public string Glyph => KindGlyph(Kind);

        public override string ToString()
        {
            string metadata = string.Join(" · ", new[] { CategoryLabel, TypeName, Origin }
                .Where(value => !string.IsNullOrEmpty(value)));
            return Glyph + "  " + DisplayText + (string.IsNullOrEmpty(metadata) ? string.Empty : "    " + metadata);
        }

        private static string DescribeKind(DdfCompletionKind kind)
        {
            switch (kind)
            {
                case DdfCompletionKind.Parameter: return "parametro";
                case DdfCompletionKind.Variable: return "variabile";
                case DdfCompletionKind.Field: return "campo";
                case DdfCompletionKind.Function: return "funzione";
                case DdfCompletionKind.Structure: return "struttura";
                case DdfCompletionKind.Type: return "tipo";
                case DdfCompletionKind.Keyword: return "parola chiave";
                case DdfCompletionKind.Boolean: return "booleano";
                case DdfCompletionKind.Snippet: return "snippet";
                default: return "libreria";
            }
        }

        private static string KindGlyph(DdfCompletionKind kind)
        {
            switch (kind)
            {
                case DdfCompletionKind.Function: return "ƒ";
                case DdfCompletionKind.Structure: return "◆";
                case DdfCompletionKind.Type: return "T";
                case DdfCompletionKind.Parameter: return "p";
                case DdfCompletionKind.Field: return "▪";
                case DdfCompletionKind.Variable: return "v";
                case DdfCompletionKind.Keyword: return "k";
                case DdfCompletionKind.Boolean: return "b";
                case DdfCompletionKind.Snippet: return "◇";
                default: return "◈";
            }
        }
    }

    public sealed class DdfCompletionResult
    {
        public DdfCompletionResult(int replacementStart, int replacementLength, IReadOnlyList<DdfCompletionItem> items,
            DdfCompletionContextKind context = DdfCompletionContextKind.General, string expectedType = "")
        {
            if (replacementStart < 0) throw new ArgumentOutOfRangeException(nameof(replacementStart));
            if (replacementLength < 0) throw new ArgumentOutOfRangeException(nameof(replacementLength));
            ReplacementStart = replacementStart;
            ReplacementLength = replacementLength;
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Context = context;
            ExpectedType = expectedType ?? string.Empty;
        }

        public int ReplacementStart { get; }
        public int ReplacementLength { get; }
        public IReadOnlyList<DdfCompletionItem> Items { get; }
        public DdfCompletionContextKind Context { get; }
        public string ExpectedType { get; }
    }

    public static class DdfCompletionService
    {
        public static DdfCompletionResult GetCompletions(
            string source,
            int position,
            bool includeAll = false,
            DdfLanguageDefinition language = null,
            IEnumerable<DdfDocumentSymbol> externalSymbols = null,
            IEnumerable<DdfCompletionItem> externalItems = null,
            IEnumerable<CompilationUnitSyntax> externalRoots = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source = source ?? string.Empty;
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            language = language ?? DdfLanguageCatalog.Default;

            DdfLexResult lexResult = DdfLexer.Lex(source, language);
            cancellationToken.ThrowIfCancellationRequested();
            bool libraryContext = TryGetLibraryPrefix(source, position, language, out int prefixStart, out int directiveStart) &&
                                  IsLibraryDirectiveAt(lexResult, directiveStart);
            if (!libraryContext)
            {
                prefixStart = position;
                while (prefixStart > 0 && language.LexicalRules.IsIdentifierPart(source[prefixStart - 1])) prefixStart--;
            }

            string prefix = source.Substring(prefixStart, position - prefixStart);
            if (!libraryContext && IsInProtectedToken(lexResult, position))
            {
                return Empty(prefixStart, prefix.Length);
            }

            DdfParseResult parseResult = DdfParser.Parse(source, lexResult, language);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parseResult, source).Symbols;
            DdfSemanticModel semanticModel = DdfSemanticModel.Create(source, parseResult.Root);
            cancellationToken.ThrowIfCancellationRequested();
            string expectedType = libraryContext ? string.Empty :
                DdfExpectedTypeService.GetExpectedType(
                    source, parseResult.Root, prefixStart, externalRoots, language.Types);
            DdfCompletionContextKind context = GetContext(source, prefixStart, libraryContext, expectedType, language);
            var candidates = new List<DdfCompletionItem>();

            if (libraryContext)
            {
                AddLibrarySymbols(candidates, symbols, language.Intellisense.CurrentDocumentOrigin, directiveStart);
                AddLibraryTokens(candidates, source, lexResult, language);
            }
            else
            {
                foreach (DdfKeywordDefinition keyword in language.Keywords)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    candidates.Add(new DdfCompletionItem(
                        keyword.Text,
                        keyword.Text,
                        keyword.TokenKind == DdfTokenKind.DataTypeKeyword ? DdfCompletionKind.Type : DdfCompletionKind.Keyword,
                        DescribeKeyword(keyword, language.Intellisense),
                        keyword.TokenKind == DdfTokenKind.DataTypeKeyword ? keyword.Text : string.Empty,
                        language.Intellisense.KeywordOrigin,
                        int.MaxValue));
                }

                foreach (string literal in language.BooleanLiterals)
                {
                    candidates.Add(new DdfCompletionItem(literal, literal, DdfCompletionKind.Boolean,
                        language.Intellisense.BooleanDetail, "bool", language.Intellisense.BooleanOrigin, int.MaxValue));
                }

                foreach (DdfSnippetTemplate snippet in language.Intellisense.Snippets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (snippet.RequiresKeyword && !language.TryGetKeyword(snippet.Prefix, out DdfKeywordDefinition ignored))
                        continue;
                    candidates.Add(new DdfCompletionItem(
                        snippet.DisplayText,
                        snippet.Prefix,
                        DdfCompletionKind.Snippet,
                        snippet.Description,
                        string.Empty,
                        language.Intellisense.SnippetOrigin,
                        int.MaxValue,
                        snippet));
                }

                foreach (DdfStandardFunction standard in language.StandardLibrary.Functions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    candidates.Add(new DdfCompletionItem(
                        standard.Name,
                        standard.Name,
                        DdfCompletionKind.Function,
                        language.Intellisense.StandardFunctionDetailPrefix + standard.Signature,
                        standard.ReturnType,
                        language.Intellisense.StandardLibraryOrigin,
                        int.MaxValue));
                }

                foreach (DdfDocumentSymbol symbol in semanticModel.GetVisibleSymbols(position))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DdfDocumentSymbol packageSymbol = Flatten(symbols).FirstOrDefault(candidate =>
                        candidate.SelectionStart == symbol.SelectionStart &&
                        string.Equals(candidate.Name, symbol.Name, StringComparison.Ordinal)) ?? symbol;
                    AddSymbol(candidates, packageSymbol, language.Intellisense.CurrentDocumentOrigin,
                        Math.Abs(position - packageSymbol.SelectionStart));
                }
                if (externalSymbols != null)
                {
                    foreach (DdfDocumentSymbol symbol in externalSymbols)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddSymbol(candidates, symbol, language.Intellisense.WorkspaceOrigin, int.MaxValue - 1);
                    }
                }
                if (externalItems != null) candidates.AddRange(externalItems);
            }

            cancellationToken.ThrowIfCancellationRequested();
            List<DdfCompletionItem> filtered = candidates
                .Where(item => (item.Kind != DdfCompletionKind.Snippet || context == DdfCompletionContextKind.Statement) &&
                               (includeAll || prefix.Length > 0) &&
                               item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.DisplayText, StringComparer.Ordinal)
                .Select(group => group.OrderBy(item => ContextRank(item, context, expectedType))
                    .ThenBy(item => OriginRank(item.Origin)).ThenBy(item => item.Proximity).First())
                .OrderBy(item => PrefixRank(item.DisplayText, prefix))
                .ThenBy(item => ContextRank(item, context, expectedType))
                .ThenByDescending(item => UsageCount(item.DisplayText, source, prefixStart, lexResult))
                .ThenBy(item => OriginRank(item.Origin))
                .ThenBy(item => item.Proximity)
                .ThenBy(item => KindRank(item.Kind))
                .ThenBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cancellationToken.ThrowIfCancellationRequested();
            return new DdfCompletionResult(prefixStart, prefix.Length, filtered.AsReadOnly(), context, expectedType);
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

        private static bool TryGetLibraryPrefix(string source, int position, DdfLanguageDefinition language,
            out int prefixStart, out int directiveStart)
        {
            int lineStart = position == 0 ? 0 : source.LastIndexOf('\n', position - 1) + 1;
            string linePrefix = source.Substring(lineStart, position - lineStart);
            prefixStart = position;
            directiveStart = -1;
            foreach (DdfDelimitedTokenRule rule in language.LexicalRules.DelimitedTokens
                         .Where(item => item.TokenKind == DdfTokenKind.LibraryDirective))
            {
                int relativeMarker = linePrefix.LastIndexOf(rule.Start, StringComparison.Ordinal);
                int marker = relativeMarker < 0 ? -1 : lineStart + relativeMarker;
                int contentStart = marker < 0 ? -1 : marker + rule.Start.Length;
                if (marker > directiveStart && contentStart <= position &&
                    source.IndexOf(rule.End, contentStart, position - contentStart, StringComparison.Ordinal) < 0)
                {
                    prefixStart = contentStart;
                    directiveStart = marker;
                }
            }

            return directiveStart >= 0;
        }

        private static void AddLibrarySymbols(List<DdfCompletionItem> result,
            IReadOnlyList<DdfDocumentSymbol> symbols, string origin, int activeDirectiveStart)
        {
            foreach (DdfDocumentSymbol library in Flatten(symbols).Where(symbol => symbol.Kind == DdfSymbolKind.Library))
            {
                if (library.Start == activeDirectiveStart) continue;
                AddSymbol(result, library, origin, 0);
            }
        }

        private static void AddLibraryTokens(List<DdfCompletionItem> result, string source,
            DdfLexResult lexResult, DdfLanguageDefinition language)
        {
            foreach (DdfToken token in lexResult.Tokens.Where(item => item.Kind == DdfTokenKind.LibraryDirective))
            {
                DdfDelimitedTokenRule rule = language.LexicalRules.FindDelimitedToken(source, token.Start);
                if (rule == null || rule.EndsAtLineBreak || token.Length < rule.Start.Length + rule.End.Length) continue;
                int contentStart = token.Start + rule.Start.Length;
                int contentLength = token.Length - rule.Start.Length - rule.End.Length;
                if (contentLength <= 0 || string.CompareOrdinal(source, token.End - rule.End.Length,
                        rule.End, 0, rule.End.Length) != 0) continue;
                string name = source.Substring(contentStart, contentLength);
                result.Add(new DdfCompletionItem(name, name, DdfCompletionKind.Library,
                    language.Intellisense.GetSymbolLabel(DdfSymbolKind.Library), string.Empty,
                    language.Intellisense.CurrentDocumentOrigin, 0));
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

        public static DdfCompletionItem CreateSymbolItem(DdfDocumentSymbol symbol, string origin, int proximity = int.MaxValue)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            DdfCompletionKind kind = ToCompletionKind(symbol.Kind);
            return new DdfCompletionItem(symbol.Name, symbol.Name, kind, symbol.Detail,
                SymbolType(symbol, kind), origin, proximity);
        }

        private static void AddSymbol(List<DdfCompletionItem> result, DdfDocumentSymbol symbol, string origin, int proximity)
        {
            result.Add(CreateSymbolItem(symbol, origin, proximity));
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

        private static string DescribeKeyword(DdfKeywordDefinition keyword, DdfIntellisenseDefinition intellisense)
        {
            if (keyword.TokenKind == DdfTokenKind.DataTypeKeyword) return intellisense.TypeDetail;
            if (keyword.TokenKind == DdfTokenKind.ControlFlowKeyword) return intellisense.ControlFlowDetail;
            return intellisense.KeywordDetail;
        }

        private static string SymbolType(DdfDocumentSymbol symbol, DdfCompletionKind kind)
        {
            if (kind == DdfCompletionKind.Structure) return symbol.Name;
            if (kind != DdfCompletionKind.Function) return symbol.Detail;
            int arrow = symbol.Detail.LastIndexOf('→');
            return arrow < 0 ? string.Empty : symbol.Detail.Substring(arrow + 1).Trim();
        }

        private static DdfCompletionContextKind GetContext(
            string source,
            int prefixStart,
            bool libraryContext,
            string expectedType,
            DdfLanguageDefinition language)
        {
            if (libraryContext) return DdfCompletionContextKind.Library;
            int position = prefixStart - 1;
            while (position >= 0 && char.IsWhiteSpace(source[position])) position--;
            if (position >= 0 && source[position] == '.') return DdfCompletionContextKind.Member;

            int wordEnd = position + 1;
            while (position >= 0 && language.LexicalRules.IsIdentifierPart(source[position])) position--;
            string previousWord = wordEnd > position + 1
                ? source.Substring(position + 1, wordEnd - position - 1)
                : string.Empty;
            if (language.TryGetKeyword(previousWord, out DdfKeywordDefinition keyword) &&
                keyword.Role == DdfKeywordRole.ReturnTypeMarker)
                return DdfCompletionContextKind.Type;
            if (!string.IsNullOrEmpty(expectedType)) return DdfCompletionContextKind.Expression;

            char previous = position >= 0 ? source[position] : '\0';
            if (previous == '\0' || previous == '\n' || previous == '\r' || previous == '{' || previous == '}' || previous == ';')
                return DdfCompletionContextKind.Statement;
            return DdfCompletionContextKind.Expression;
        }

        private static int ContextRank(DdfCompletionItem item, DdfCompletionContextKind context, string expectedType)
        {
            if (context == DdfCompletionContextKind.Library)
                return item.Kind == DdfCompletionKind.Library ? 0 : 20;
            if (context == DdfCompletionContextKind.Type)
                return item.Kind == DdfCompletionKind.Type || item.Kind == DdfCompletionKind.Structure ? 0 : 20 + KindRank(item.Kind);
            if (context == DdfCompletionContextKind.Member)
                return item.Kind == DdfCompletionKind.Field ? 0 : item.Kind == DdfCompletionKind.Function ? 1 : 20 + KindRank(item.Kind);
            if (context == DdfCompletionContextKind.Statement)
            {
                if (item.Kind == DdfCompletionKind.Snippet) return 0;
                if (item.Kind == DdfCompletionKind.Type || item.Kind == DdfCompletionKind.Structure) return 0;
                if (item.Kind == DdfCompletionKind.Keyword && item.Detail == "controllo di flusso") return 1;
                if (item.Kind == DdfCompletionKind.Function) return 3;
                return 8 + KindRank(item.Kind);
            }

            int expectedRank = ExpectedTypeRank(item.TypeName, expectedType);
            int valueRank = item.Kind == DdfCompletionKind.Parameter ? 0 :
                item.Kind == DdfCompletionKind.Variable ? 1 :
                item.Kind == DdfCompletionKind.Field ? 2 :
                item.Kind == DdfCompletionKind.Boolean ? 3 :
                item.Kind == DdfCompletionKind.Function ? 4 : 12 + KindRank(item.Kind);
            return expectedRank * 20 + valueRank;
        }

        private static int ExpectedTypeRank(string candidateType, string expectedType)
        {
            if (string.IsNullOrEmpty(expectedType)) return 0;
            if (string.Equals(candidateType, expectedType, StringComparison.Ordinal)) return 0;
            if (IsNumeric(candidateType) && IsNumeric(expectedType)) return 1;
            return string.IsNullOrEmpty(candidateType) ? 3 : 5;
        }

        private static bool IsNumeric(string type)
        {
            return type == "int" || type == "float" || type == "char";
        }

        private static int OriginRank(string origin)
        {
            if (origin == "documento corrente") return 0;
            if (origin == "libreria standard") return 1;
            if (origin == "linguaggio DDF") return 3;
            return 2;
        }

        private static int UsageCount(string name, string source, int position, DdfLexResult lexResult)
        {
            int count = 0;
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (token.Start >= position || token.Length != name.Length) continue;
                if (string.CompareOrdinal(source, token.Start, name, 0, name.Length) == 0) count++;
            }
            return count;
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
                case DdfCompletionKind.Snippet: return 3;
                case DdfCompletionKind.Structure: return 4;
                case DdfCompletionKind.Type: return 5;
                case DdfCompletionKind.Keyword: return 6;
                case DdfCompletionKind.Boolean: return 7;
                default: return 8;
            }
        }

        private static DdfCompletionResult Empty(int start, int length)
        {
            return new DdfCompletionResult(start, length, new List<DdfCompletionItem>().AsReadOnly());
        }
    }
}
