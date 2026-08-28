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
                        symbols.Add(CreateSymbol(name, DdfSymbolKind.Library, string.Empty, library, library.Start, library.Length));
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
                variable.NameLength));
        }

        private static DdfDocumentSymbol CreateSymbol(
            string name,
            DdfSymbolKind kind,
            string detail,
            DdfSyntaxNode node,
            int selectionStart,
            int selectionLength,
            List<DdfDocumentSymbol> children = null)
        {
            return new DdfDocumentSymbol(
                name,
                kind,
                detail,
                node.Start,
                node.Length,
                selectionStart,
                selectionLength,
                (children ?? new List<DdfDocumentSymbol>()).AsReadOnly());
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
