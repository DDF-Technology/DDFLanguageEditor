using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSymbolIndex
    {
        private DdfSymbolIndex(IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            Symbols = symbols;
        }

        public IReadOnlyList<DdfDocumentSymbol> Symbols { get; }

        public static DdfSymbolIndex Create(DdfParseResult parseResult, string source)
        {
            if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (parseResult.GenericRoot != null && parseResult.Grammar != null &&
                parseResult.GrammarDiagnostics.Count == 0)
            {
                IReadOnlyList<DdfDocumentSymbol> generic =
                    DdfGenericIdeService.CreateSymbols(parseResult.GenericRoot,
                        parseResult.Grammar, source);
                IReadOnlyList<DdfDocumentSymbol> typed = Create(parseResult.Root).Symbols;
                return new DdfSymbolIndex(ReconcileTypedSymbols(generic, typed));
            }
            return Create(parseResult.Root);
        }

        public static DdfSymbolIndex Create(CompilationUnitSyntax root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var symbols = new List<DdfDocumentSymbol>();
            foreach (MemberSyntax member in root.Members)
            {
                if (member is LibraryDirectiveSyntax library)
                {
                    string name = ExtractLibraryName(library.Text);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        int nameStart = library.Text.StartsWith("@@'", StringComparison.Ordinal)
                            ? library.Start + 3
                            : library.Start;
                        symbols.Add(CreateSymbol(name, DdfSymbolKind.Library, string.Empty, library, nameStart, name.Length));
                    }
                }
                else if (member is StructDeclarationSyntax structure && !string.IsNullOrWhiteSpace(structure.Name))
                {
                    var fields = new List<DdfDocumentSymbol>();
                    foreach (VariableDeclarationStatementSyntax field in structure.Fields)
                    {
                        AddVariable(fields, field, DdfSymbolKind.Field);
                    }

                    symbols.Add(CreateSymbol(
                        structure.Name,
                        DdfSymbolKind.Structure,
                        "struttura",
                        structure,
                        structure.NameStart,
                        structure.NameLength,
                        fields));
                }
                else if (member is EnumDeclarationSyntax enumeration &&
                         !string.IsNullOrWhiteSpace(enumeration.Name))
                {
                    List<DdfDocumentSymbol> values = enumeration.Members
                        .Where(value => !string.IsNullOrWhiteSpace(value.Name))
                        .Select(value => CreateSymbol(value.Name,
                            DdfSymbolKind.EnumerationMember, enumeration.Name,
                            value, value.Start, value.Length))
                        .ToList();
                    symbols.Add(CreateSymbol(enumeration.Name,
                        DdfSymbolKind.Enumeration, "enum", enumeration,
                        enumeration.NameStart, enumeration.NameLength, values));
                }
                else if (member is FunctionDeclarationSyntax function && !string.IsNullOrWhiteSpace(function.Name))
                {
                    var children = new List<DdfDocumentSymbol>();
                    foreach (ParameterSyntax parameter in function.Parameters)
                    {
                        if (!string.IsNullOrWhiteSpace(parameter.Name))
                        {
                            children.Add(CreateSymbol(
                                parameter.Name,
                                DdfSymbolKind.Parameter,
                                FormatType(parameter.Type),
                                parameter,
                                parameter.NameStart,
                                parameter.NameLength));
                        }
                    }

                    AddStatementSymbols(children, function.Body);
                    string signature = "(" + string.Join(", ", function.Parameters.Select(parameter => FormatType(parameter.Type))) +
                                       ") → " + FormatType(function.ReturnType);
                    symbols.Add(CreateSymbol(
                        function.Name,
                        DdfSymbolKind.Function,
                        signature,
                        function,
                        function.NameStart,
                        function.NameLength,
                        children));
                }
                else if (member is GlobalStatementSyntax global && global.Statement is VariableDeclarationStatementSyntax variable)
                {
                    AddVariable(symbols, variable, DdfSymbolKind.Variable);
                }
            }

            symbols.Sort((left, right) => left.Start.CompareTo(right.Start));
            return new DdfSymbolIndex(symbols.AsReadOnly());
        }

        private static void AddStatementSymbols(List<DdfDocumentSymbol> symbols, StatementSyntax statement)
        {
            if (statement == null) return;
            if (statement is VariableDeclarationStatementSyntax variable)
            {
                AddVariable(symbols, variable, DdfSymbolKind.Variable);
            }
            else if (statement is BlockStatementSyntax block)
            {
                foreach (StatementSyntax child in block.Statements) AddStatementSymbols(symbols, child);
            }
            else if (statement is IfStatementSyntax ifStatement)
            {
                AddStatementSymbols(symbols, ifStatement.Body);
                AddStatementSymbols(symbols, ifStatement.ElseBody);
            }
            else if (statement is SelectStatementSyntax selection)
            {
                foreach (SelectClauseSyntax clause in selection.Clauses)
                    AddStatementSymbols(symbols, clause.Body);
            }
            else if (statement is WhileStatementSyntax whileStatement)
            {
                AddStatementSymbols(symbols, whileStatement.Body);
            }
            else if (statement is DoWhileStatementSyntax doWhileStatement)
            {
                AddStatementSymbols(symbols, doWhileStatement.Body);
            }
            else if (statement is ForStatementSyntax forStatement)
            {
                AddStatementSymbols(symbols, forStatement.Initializer);
                AddStatementSymbols(symbols, forStatement.Body);
            }
        }

        private static void AddVariable(List<DdfDocumentSymbol> symbols, VariableDeclarationStatementSyntax variable, DdfSymbolKind kind)
        {
            if (variable == null || string.IsNullOrWhiteSpace(variable.Name)) return;
            symbols.Add(CreateSymbol(
                variable.Name,
                kind,
                FormatType(variable.Type),
                variable,
                variable.NameStart,
                variable.NameLength,
                null,
                variable.IsFixed,
                variable.IsFixed));
        }

        private static DdfDocumentSymbol CreateSymbol(
            string name,
            DdfSymbolKind kind,
            string detail,
            DdfSyntaxNode node,
            int selectionStart,
            int selectionLength,
            List<DdfDocumentSymbol> children = null,
            bool isReadOnly = false,
            bool isFixed = false)
        {
            return new DdfDocumentSymbol(
                name,
                kind,
                detail,
                node.Start,
                node.Length,
                selectionStart,
                selectionLength,
                (children ?? new List<DdfDocumentSymbol>()).AsReadOnly(),
                isReadOnly,
                isFixed);
        }

        private static IReadOnlyList<DdfDocumentSymbol> ReconcileTypedSymbols(
            IReadOnlyList<DdfDocumentSymbol> symbols,
            IReadOnlyList<DdfDocumentSymbol> typedSymbols)
        {
            var flattened = Flatten(typedSymbols).ToList();
            var typedByIdentity = flattened
                .GroupBy(item => item.SelectionStart + ":" + item.Name,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            var typedByDeclaration = flattened
                .GroupBy(DeclarationKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            return symbols.Select(item => ReconcileTypedSymbol(item,
                    typedByIdentity, typedByDeclaration))
                .ToList().AsReadOnly();
        }

        private static DdfDocumentSymbol ReconcileTypedSymbol(
            DdfDocumentSymbol symbol,
            IReadOnlyDictionary<string, DdfDocumentSymbol> typedByIdentity,
            IReadOnlyDictionary<string, DdfDocumentSymbol> typedByDeclaration)
        {
            if (!typedByIdentity.TryGetValue(
                    symbol.SelectionStart + ":" + symbol.Name,
                    out DdfDocumentSymbol match))
                typedByDeclaration.TryGetValue(DeclarationKey(symbol), out match);

            // A declarative grammar identifies fields by source rule. When a
            // declaration contains two identifiers (for example
            // `fixed Priority DefaultPriority`) both `type` and `name` can
            // otherwise point at the first token. The typed tree owns the
            // canonical declaration identity; grammar metadata still owns the
            // hierarchy and presentation for language-package extensibility.
            string name = match?.Name ?? symbol.Name;
            string detail = match?.Detail ?? symbol.Detail;
            int selectionStart = match?.SelectionStart ?? symbol.SelectionStart;
            int selectionLength = match?.SelectionLength ?? symbol.SelectionLength;
            return new DdfDocumentSymbol(name, symbol.Kind, detail,
                symbol.Start, symbol.Length, selectionStart,
                selectionLength,
                symbol.Children.Select(item => ReconcileTypedSymbol(item,
                        typedByIdentity, typedByDeclaration))
                    .ToList().AsReadOnly(),
                match?.IsReadOnly ?? symbol.IsReadOnly,
                match?.IsFixed ?? symbol.IsFixed);
        }

        private static string DeclarationKey(DdfDocumentSymbol symbol) =>
            symbol.Start + ":" + symbol.Length + ":" + (int)symbol.Kind;

        private static IEnumerable<DdfDocumentSymbol> Flatten(
            IEnumerable<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                yield return symbol;
                foreach (DdfDocumentSymbol child in Flatten(symbol.Children))
                    yield return child;
            }
        }

        private static string FormatType(TypeReferenceSyntax type)
        {
            if (type == null || string.IsNullOrWhiteSpace(type.Name)) return "tipo sconosciuto";
            return type.Name + string.Concat(Enumerable.Repeat("[]", type.ArrayLengths.Count));
        }

        private static string ExtractLibraryName(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            const string prefix = "@@'";
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.EndsWith("'", StringComparison.Ordinal) && text.Length > 4)
            {
                return text.Substring(prefix.Length, text.Length - prefix.Length - 1);
            }

            return text;
        }
    }
}
