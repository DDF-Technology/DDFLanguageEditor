using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    internal static class DdfGenericIdeService
    {
        public static IReadOnlyList<DdfDocumentSymbol> CreateSymbols(
            DdfGenericSyntaxNode root, DdfCompiledGrammar grammar, string source)
        {
            var symbols = CollectSymbols(root, grammar, source ?? string.Empty);
            symbols.Sort((left, right) => left.Start.CompareTo(right.Start));
            return symbols.AsReadOnly();
        }

        public static IReadOnlyList<DdfFoldingRange> CreateFoldingRanges(
            DdfGenericSyntaxNode root, DdfCompiledGrammar grammar, string source)
        {
            var ranges = new List<DdfFoldingRange>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            CollectFoldingRanges(root, grammar, source ?? string.Empty, ranges, seen);
            ranges.Sort((left, right) => left.Start != right.Start
                ? left.Start.CompareTo(right.Start)
                : right.End.CompareTo(left.End));
            return ranges.AsReadOnly();
        }

        public static IReadOnlyList<DdfBreadcrumbItem> GetBreadcrumbPath(
            DdfGenericSyntaxNode root, DdfCompiledGrammar grammar, string source, int position)
        {
            var path = new List<DdfBreadcrumbItem>();
            int safePosition = Math.Max(0, Math.Min(position, (source ?? string.Empty).Length));
            CollectBreadcrumbPath(root, grammar, source ?? string.Empty, safePosition, path);
            return path.AsReadOnly();
        }

        public static void CollectSelectableRanges(DdfGenericSyntaxNode node, DdfCompiledGrammar grammar,
            int selectionStart, int selectionLength, ICollection<DdfTextRange> ranges)
        {
            if (node == null || node.IsMissing) return;
            if (grammar.TryGetNodeDefinition(node.RuleName, out DdfGrammarNodeDefinition definition) &&
                definition.Ide.Selectable)
            {
                AddIfContaining(ranges, node.Start, node.Length, selectionStart, selectionLength);
            }
            foreach (DdfGenericSyntaxNode child in node.Children)
                CollectSelectableRanges(child, grammar, selectionStart, selectionLength, ranges);
        }

        private static List<DdfDocumentSymbol> CollectSymbols(
            DdfGenericSyntaxNode node, DdfCompiledGrammar grammar, string source)
        {
            var descendants = new List<DdfDocumentSymbol>();
            foreach (DdfGenericSyntaxNode child in node.Children)
                descendants.AddRange(CollectSymbols(child, grammar, source));

            if (!grammar.TryGetNodeDefinition(node.RuleName, out DdfGrammarNodeDefinition definition) ||
                !definition.Ide.SymbolKind.HasValue)
                return descendants;

            if (definition.Ide.ChildSymbolKind.HasValue)
                descendants = descendants.Select(item => WithKind(item, definition.Ide.ChildSymbolKind.Value)).ToList();

            DdfGenericSyntaxNode selection = GetFieldNode(node, definition.Ide.NameField) ?? node;
            string name = Slice(source, selection).Trim();
            name = TrimAffixes(name, definition.Ide.NamePrefix, definition.Ide.NameSuffix);
            if (string.IsNullOrWhiteSpace(name)) return descendants;

            var symbol = new DdfDocumentSymbol(name, definition.Ide.SymbolKind.Value,
                GetSymbolDetail(node, definition, grammar, source), node.Start, node.Length,
                selection.Start, selection.Length,
                descendants.OrderBy(item => item.Start).ToList().AsReadOnly());
            return new List<DdfDocumentSymbol> { symbol };
        }

        private static string GetSymbolDetail(DdfGenericSyntaxNode node, DdfGrammarNodeDefinition definition,
            DdfCompiledGrammar grammar, string source)
        {
            string type = Slice(source, GetFieldNode(node, definition.Ide.TypeField)).Trim();
            if (!string.IsNullOrWhiteSpace(type)) return type;
            string returnType = Slice(source, GetFieldNode(node, definition.Ide.ReturnTypeField)).Trim();
            if (!string.IsNullOrWhiteSpace(definition.Ide.ParametersField) &&
                node.Fields.TryGetValue(definition.Ide.ParametersField,
                    out IReadOnlyList<DdfGenericSyntaxNode> parameters))
            {
                var types = new List<string>();
                foreach (DdfGenericSyntaxNode parameter in parameters)
                {
                    if (grammar.TryGetNodeDefinition(parameter.RuleName,
                        out DdfGrammarNodeDefinition parameterDefinition))
                        types.Add(Slice(source, GetFieldNode(parameter, parameterDefinition.Ide.TypeField)).Trim());
                }
                return "(" + string.Join(", ", types) + ") → " + returnType;
            }
            return definition.Ide.Detail;
        }

        private static DdfDocumentSymbol WithKind(DdfDocumentSymbol symbol, DdfSymbolKind kind)
        {
            return new DdfDocumentSymbol(symbol.Name, kind, symbol.Detail, symbol.Start, symbol.Length,
                symbol.SelectionStart, symbol.SelectionLength, symbol.Children,
                symbol.IsReadOnly, symbol.IsFixed);
        }

        private static void CollectFoldingRanges(DdfGenericSyntaxNode node, DdfCompiledGrammar grammar,
            string source, ICollection<DdfFoldingRange> ranges, ISet<string> seen)
        {
            if (grammar.TryGetNodeDefinition(node.RuleName, out DdfGrammarNodeDefinition definition) &&
                definition.Ide.FoldingKind.HasValue)
            {
                DdfGenericSyntaxNode target = GetFieldNode(node, definition.Ide.FoldingField) ?? node;
                int searchStart = Math.Max(0, Math.Min(target.Start, source.Length));
                int searchEnd = Math.Max(searchStart, Math.Min(target.End, source.Length));
                int open = source.IndexOf('{', searchStart, searchEnd - searchStart);
                int close = searchEnd > searchStart ? source.LastIndexOf('}', searchEnd - 1, searchEnd - searchStart) : -1;
                if (open >= 0 && close > open && source.IndexOf('\n', open, close - open) >= 0)
                {
                    string key = open + ":" + close;
                    if (seen.Add(key)) ranges.Add(new DdfFoldingRange(definition.Ide.FoldingKind.Value, open, close + 1));
                }
            }
            foreach (DdfGenericSyntaxNode child in node.Children)
                CollectFoldingRanges(child, grammar, source, ranges, seen);
        }

        private static bool CollectBreadcrumbPath(DdfGenericSyntaxNode node, DdfCompiledGrammar grammar,
            string source, int position, ICollection<DdfBreadcrumbItem> path)
        {
            if (node == null || position < node.Start || position > node.End) return false;
            if (grammar.TryGetNodeDefinition(node.RuleName, out DdfGrammarNodeDefinition definition) &&
                definition.Ide.BreadcrumbKind.HasValue)
            {
                DdfGenericSyntaxNode selection = GetFieldNode(node,
                    definition.Ide.BreadcrumbSelectionField ?? definition.Ide.NameField) ?? node;
                int selectionLength = definition.Ide.BreadcrumbSelectionLength > 0
                    ? Math.Min(definition.Ide.BreadcrumbSelectionLength, node.Length)
                    : selection.Length;
                int selectionStart = definition.Ide.BreadcrumbSelectionLength > 0 ? node.Start : selection.Start;
                string name = TrimAffixes(Slice(source, GetFieldNode(node, definition.Ide.NameField) ?? node).Trim(),
                    definition.Ide.NamePrefix, definition.Ide.NameSuffix);
                string label = definition.Ide.BreadcrumbLabel.Replace("{name}", name);
                if (string.IsNullOrWhiteSpace(label)) label = name;
                if (!string.IsNullOrWhiteSpace(label))
                    path.Add(new DdfBreadcrumbItem(label, definition.Ide.BreadcrumbKind.Value,
                        node.Start, node.Length, selectionStart, selectionLength));
            }
            foreach (DdfGenericSyntaxNode child in node.Children)
                if (CollectBreadcrumbPath(child, grammar, source, position, path)) return true;
            return true;
        }

        private static DdfGenericSyntaxNode GetFieldNode(DdfGenericSyntaxNode node, string fieldName)
        {
            if (node == null || string.IsNullOrWhiteSpace(fieldName)) return null;
            return node.Fields.TryGetValue(fieldName, out IReadOnlyList<DdfGenericSyntaxNode> values)
                ? values.FirstOrDefault(item => !item.IsMissing)
                : null;
        }

        private static string Slice(string source, DdfGenericSyntaxNode node)
        {
            if (node == null || node.Start < 0 || node.Start >= source.Length || node.Length <= 0) return string.Empty;
            return source.Substring(node.Start, Math.Min(node.Length, source.Length - node.Start));
        }

        private static string TrimAffixes(string value, string prefix, string suffix)
        {
            if (!string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.Ordinal))
                value = value.Substring(prefix.Length);
            if (!string.IsNullOrEmpty(suffix) && value.EndsWith(suffix, StringComparison.Ordinal))
                value = value.Substring(0, value.Length - suffix.Length);
            return value;
        }

        private static void AddIfContaining(ICollection<DdfTextRange> ranges, int rangeStart, int rangeLength,
            int selectionStart, int selectionLength)
        {
            if (rangeLength <= 0) return;
            int rangeEnd = rangeStart + rangeLength;
            int selectionEnd = selectionStart + selectionLength;
            bool contains = selectionLength == 0
                ? rangeStart <= selectionStart && selectionStart <= rangeEnd
                : rangeStart <= selectionStart && rangeEnd >= selectionEnd;
            if (contains) ranges.Add(new DdfTextRange(rangeStart, rangeLength));
        }
    }
}
