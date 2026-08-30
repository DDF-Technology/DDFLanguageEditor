using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSymbolOccurrence
    {
        internal DdfSymbolOccurrence(DdfDocumentSymbol symbol, int start, int length, bool isDeclaration)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            Start = start;
            Length = length;
            IsDeclaration = isDeclaration;
        }

        public DdfDocumentSymbol Symbol { get; }
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public bool IsDeclaration { get; }
    }

    public sealed class DdfRenameResult
    {
        internal DdfRenameResult(string text, int selectionStart, int selectionLength, int replacementCount)
        {
            Text = text;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
            ReplacementCount = replacementCount;
        }

        public string Text { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
        public int ReplacementCount { get; }
    }

    public sealed class DdfSemanticModel
    {
        private sealed class Binding
        {
            public DdfDocumentSymbol Symbol;
            public int ScopeStart;
            public int ScopeEnd;
            public int VisibleStart;
            public int Depth;
        }

        private readonly string source;
        private readonly IReadOnlyList<Binding> bindings;

        private DdfSemanticModel(
            string source,
            IReadOnlyList<DdfSymbolOccurrence> occurrences,
            IReadOnlyList<DdfDiagnostic> diagnostics,
            IReadOnlyList<Binding> bindings)
        {
            this.source = source;
            Occurrences = occurrences;
            Diagnostics = diagnostics;
            this.bindings = bindings;
        }

        public IReadOnlyList<DdfSymbolOccurrence> Occurrences { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }

        public static DdfSemanticModel Create(string source, CompilationUnitSyntax root)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));

            DdfSymbolIndex index = DdfSymbolIndex.Create(root);
            var symbolsByStart = Flatten(index.Symbols).ToDictionary(symbol => symbol.SelectionStart);
            var bindings = new List<Binding>();
            var occurrences = new List<DdfSymbolOccurrence>();
            var diagnostics = new List<DdfDiagnostic>();

            foreach (DdfDocumentSymbol symbol in symbolsByStart.Values)
            {
                occurrences.Add(new DdfSymbolOccurrence(symbol, symbol.SelectionStart, symbol.SelectionLength, true));
            }

            foreach (MemberSyntax member in root.Members)
            {
                if (member is StructDeclarationSyntax structure)
                {
                    AddBinding(bindings, symbolsByStart, structure.NameStart, 0, source.Length, 0, 0);
                    foreach (VariableDeclarationStatementSyntax field in structure.Fields)
                    {
                        AddBinding(bindings, symbolsByStart, field.NameStart, structure.Start, structure.End, structure.Start, 1);
                    }
                }
                else if (member is FunctionDeclarationSyntax function)
                {
                    AddBinding(bindings, symbolsByStart, function.NameStart, 0, source.Length, 0, 0);
                    foreach (ParameterSyntax parameter in function.Parameters)
                    {
                        AddBinding(bindings, symbolsByStart, parameter.NameStart, function.Body.Start, function.Body.End, function.Body.Start, 1);
                    }

                    CollectStatementBindings(function.Body, symbolsByStart, bindings, 1, function.Body.Start, function.Body.End);
                }
                else if (member is GlobalStatementSyntax global && global.Statement is VariableDeclarationStatementSyntax variable)
                {
                    AddBinding(bindings, symbolsByStart, variable.NameStart, 0, source.Length, 0, 0);
                }
            }

            AddDuplicateDiagnostics(bindings, diagnostics, source);
            foreach (MemberSyntax member in root.Members)
            {
                VisitMemberReferences(member, bindings, occurrences, diagnostics, source);
            }

            occurrences.Sort((left, right) => left.Start.CompareTo(right.Start));
            diagnostics.Sort((left, right) => left.Start.CompareTo(right.Start));
            return new DdfSemanticModel(
                source,
                occurrences.AsReadOnly(),
                diagnostics.AsReadOnly(),
                bindings.AsReadOnly());
        }

        public IReadOnlyList<DdfDocumentSymbol> GetVisibleSymbols(int position)
        {
            int safePosition = Math.Max(0, Math.Min(position, source.Length));
            return bindings
                .Where(binding => safePosition >= binding.ScopeStart &&
                                  (binding.Depth == 0 || safePosition < binding.ScopeEnd) &&
                                  safePosition >= binding.VisibleStart)
                .GroupBy(binding => binding.Symbol.Name, StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(binding => binding.Depth)
                    .ThenByDescending(binding => binding.VisibleStart)
                    .First())
                .OrderByDescending(binding => binding.Depth)
                .ThenByDescending(binding => binding.VisibleStart)
                .Select(binding => binding.Symbol)
                .ToList()
                .AsReadOnly();
        }

        public DdfSymbolOccurrence FindOccurrence(int position)
        {
            int safePosition = Math.Max(0, Math.Min(position, source.Length));
            DdfSymbolOccurrence exact = Occurrences.FirstOrDefault(item =>
                safePosition >= item.Start && safePosition < item.End);
            if (exact != null) return exact;
            return Occurrences.FirstOrDefault(item => safePosition == item.End && item.Length > 0);
        }

        public IReadOnlyList<DdfSymbolOccurrence> FindOccurrences(DdfDocumentSymbol symbol)
        {
            if (symbol == null) return new List<DdfSymbolOccurrence>().AsReadOnly();
            return Occurrences.Where(item => ReferenceEquals(item.Symbol, symbol)).ToList().AsReadOnly();
        }

        public DdfRenameResult Rename(int position, string newName, DdfLanguageDefinition language = null)
        {
            if (!IsValidIdentifier(newName))
            {
                throw new ArgumentException("Il nuovo nome non è un identificatore valido.", nameof(newName));
            }

            DdfLanguageDefinition effectiveLanguage = language ?? DdfLanguageCatalog.Default;
            if (effectiveLanguage.TryGetKeyword(newName, out DdfKeywordDefinition keyword))
            {
                throw new ArgumentException("Il nuovo nome è una parola riservata.", nameof(newName));
            }

            DdfSymbolOccurrence selected = FindOccurrence(position);
            if (selected == null) throw new InvalidOperationException("Nessun simbolo rinominabile nella posizione indicata.");

            List<DdfSymbolOccurrence> replacements = FindOccurrences(selected.Symbol).OrderByDescending(item => item.Start).ToList();
            string result = source;
            foreach (DdfSymbolOccurrence replacement in replacements)
            {
                result = result.Remove(replacement.Start, replacement.Length).Insert(replacement.Start, newName);
            }

            int selectionStart = selected.Start;
            foreach (DdfSymbolOccurrence replacement in replacements.Where(item => item.Start < selected.Start))
            {
                selectionStart += newName.Length - replacement.Length;
            }

            return new DdfRenameResult(result, selectionStart, newName.Length, replacements.Count);
        }

        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!(char.IsLetter(value[0]) || value[0] == '_')) return false;
            for (int index = 1; index < value.Length; index++)
            {
                if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_')) return false;
            }

            return true;
        }

        private static IEnumerable<DdfDocumentSymbol> Flatten(IEnumerable<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                yield return symbol;
                foreach (DdfDocumentSymbol child in Flatten(symbol.Children)) yield return child;
            }
        }

        private static void AddBinding(
            List<Binding> bindings,
            IDictionary<int, DdfDocumentSymbol> symbolsByStart,
            int selectionStart,
            int scopeStart,
            int scopeEnd,
            int visibleStart,
            int depth)
        {
            if (!symbolsByStart.TryGetValue(selectionStart, out DdfDocumentSymbol symbol)) return;
            bindings.Add(new Binding
            {
                Symbol = symbol,
                ScopeStart = scopeStart,
                ScopeEnd = scopeEnd,
                VisibleStart = visibleStart,
                Depth = depth
            });
        }

        private static void CollectStatementBindings(
            StatementSyntax statement,
            IDictionary<int, DdfDocumentSymbol> symbolsByStart,
            List<Binding> bindings,
            int depth,
            int scopeStart,
            int scopeEnd)
        {
            if (statement == null) return;
            if (statement is VariableDeclarationStatementSyntax variable)
            {
                AddBinding(bindings, symbolsByStart, variable.NameStart, scopeStart, scopeEnd, variable.End, depth);
                return;
            }

            if (statement is BlockStatementSyntax block)
            {
                foreach (StatementSyntax child in block.Statements)
                {
                    CollectStatementBindings(child, symbolsByStart, bindings, depth + 1, block.Start, block.End);
                }
                return;
            }

            if (statement is IfStatementSyntax conditional)
            {
                CollectStatementBindings(conditional.Body, symbolsByStart, bindings, depth + 1, conditional.Body.Start, conditional.Body.End);
            }
            else if (statement is WhileStatementSyntax loop)
            {
                CollectStatementBindings(loop.Body, symbolsByStart, bindings, depth + 1, loop.Body.Start, loop.Body.End);
            }
            else if (statement is DoWhileStatementSyntax doLoop)
            {
                CollectStatementBindings(doLoop.Body, symbolsByStart, bindings, depth + 1, doLoop.Body.Start, doLoop.Body.End);
            }
            else if (statement is ForStatementSyntax forLoop)
            {
                CollectStatementBindings(forLoop.Initializer, symbolsByStart, bindings, depth + 1, forLoop.Start, forLoop.End);
                CollectStatementBindings(forLoop.Body, symbolsByStart, bindings, depth + 2, forLoop.Start, forLoop.End);
            }
        }

        private static void VisitMemberReferences(
            MemberSyntax member,
            IReadOnlyList<Binding> bindings,
            List<DdfSymbolOccurrence> occurrences,
            List<DdfDiagnostic> diagnostics,
            string source)
        {
            if (member is StructDeclarationSyntax structure)
            {
                foreach (VariableDeclarationStatementSyntax field in structure.Fields)
                {
                    VisitTypeReference(field.Type, bindings, occurrences, diagnostics, source);
                    VisitExpression(field.Initializer, bindings, occurrences, diagnostics, source);
                }
            }
            else if (member is FunctionDeclarationSyntax function)
            {
                VisitTypeReference(function.ReturnType, bindings, occurrences, diagnostics, source);
                foreach (ParameterSyntax parameter in function.Parameters)
                {
                    VisitTypeReference(parameter.Type, bindings, occurrences, diagnostics, source);
                }
                VisitStatementReferences(function.Body, bindings, occurrences, diagnostics, source);
            }
            else if (member is GlobalStatementSyntax global)
            {
                VisitStatementReferences(global.Statement, bindings, occurrences, diagnostics, source);
            }
        }

        private static void VisitStatementReferences(
            StatementSyntax statement,
            IReadOnlyList<Binding> bindings,
            List<DdfSymbolOccurrence> occurrences,
            List<DdfDiagnostic> diagnostics,
            string source)
        {
            if (statement == null) return;
            if (statement is VariableDeclarationStatementSyntax variable)
            {
                VisitTypeReference(variable.Type, bindings, occurrences, diagnostics, source);
                VisitExpression(variable.Initializer, bindings, occurrences, diagnostics, source);
            }
            else if (statement is BlockStatementSyntax block)
            {
                foreach (StatementSyntax child in block.Statements) VisitStatementReferences(child, bindings, occurrences, diagnostics, source);
            }
            else if (statement is IfStatementSyntax conditional)
            {
                VisitExpression(conditional.Condition, bindings, occurrences, diagnostics, source);
                VisitStatementReferences(conditional.Body, bindings, occurrences, diagnostics, source);
            }
            else if (statement is WhileStatementSyntax loop)
            {
                VisitExpression(loop.Condition, bindings, occurrences, diagnostics, source);
                VisitStatementReferences(loop.Body, bindings, occurrences, diagnostics, source);
            }
            else if (statement is DoWhileStatementSyntax doLoop)
            {
                VisitStatementReferences(doLoop.Body, bindings, occurrences, diagnostics, source);
                VisitExpression(doLoop.Condition, bindings, occurrences, diagnostics, source);
            }
            else if (statement is ForStatementSyntax forLoop)
            {
                VisitStatementReferences(forLoop.Initializer, bindings, occurrences, diagnostics, source);
                VisitExpression(forLoop.Condition, bindings, occurrences, diagnostics, source);
                VisitExpression(forLoop.Increment, bindings, occurrences, diagnostics, source);
                VisitStatementReferences(forLoop.Body, bindings, occurrences, diagnostics, source);
            }
            else if (statement is ReturnStatementSyntax returnStatement) VisitExpression(returnStatement.Expression, bindings, occurrences, diagnostics, source);
            else if (statement is ExpressionStatementSyntax expressionStatement) VisitExpression(expressionStatement.Expression, bindings, occurrences, diagnostics, source);
        }

        private static void VisitExpression(
            ExpressionSyntax expression,
            IReadOnlyList<Binding> bindings,
            List<DdfSymbolOccurrence> occurrences,
            List<DdfDiagnostic> diagnostics,
            string source)
        {
            if (expression == null) return;
            if (expression is NameExpressionSyntax name)
            {
                Binding binding = Resolve(bindings, name.Name, name.Start);
                if (binding != null)
                {
                    occurrences.Add(new DdfSymbolOccurrence(binding.Symbol, name.Start, name.Length, false));
                }
                else
                {
                    if (!DdfRuntimeCatalog.TryGetStandardFunction(name.Name, out DdfStandardFunction ignored))
                        diagnostics.Add(CreateDiagnostic("DDF201", "Simbolo non risolto: '" + name.Name + "'.", name.Start, name.Length, source));
                }
                return;
            }

            if (expression is ParenthesizedExpressionSyntax parenthesized) VisitExpression(parenthesized.Expression, bindings, occurrences, diagnostics, source);
            else if (expression is UnaryExpressionSyntax unary) VisitExpression(unary.Operand, bindings, occurrences, diagnostics, source);
            else if (expression is BinaryExpressionSyntax binary)
            {
                VisitExpression(binary.Left, bindings, occurrences, diagnostics, source);
                var output = binary.Right as NameExpressionSyntax;
                if (binary.OperatorText != ">>" || output == null || output.Name != DdfRuntimeCatalog.ConsoleOutput)
                    VisitExpression(binary.Right, bindings, occurrences, diagnostics, source);
            }
            else if (expression is PostfixExpressionSyntax postfix) VisitExpression(postfix.Operand, bindings, occurrences, diagnostics, source);
            else if (expression is CallExpressionSyntax call)
            {
                VisitExpression(call.Target, bindings, occurrences, diagnostics, source);
                foreach (ExpressionSyntax argument in call.Arguments) VisitExpression(argument, bindings, occurrences, diagnostics, source);
            }
            else if (expression is IndexExpressionSyntax index)
            {
                VisitExpression(index.Target, bindings, occurrences, diagnostics, source);
                VisitExpression(index.Index, bindings, occurrences, diagnostics, source);
            }
            else if (expression is MemberAccessExpressionSyntax member)
            {
                VisitExpression(member.Target, bindings, occurrences, diagnostics, source);
            }
        }

        private static void VisitTypeReference(
            TypeReferenceSyntax type,
            IReadOnlyList<Binding> bindings,
            List<DdfSymbolOccurrence> occurrences,
            List<DdfDiagnostic> diagnostics,
            string source)
        {
            if (type == null) return;
            Binding binding = Resolve(bindings, type.Name, type.Start);
            if (binding != null && binding.Symbol.Kind == DdfSymbolKind.Structure)
            {
                occurrences.Add(new DdfSymbolOccurrence(binding.Symbol, type.Start, type.Name.Length, false));
            }

            foreach (ExpressionSyntax arrayLength in type.ArrayLengths)
            {
                VisitExpression(arrayLength, bindings, occurrences, diagnostics, source);
            }
        }

        private static Binding Resolve(IReadOnlyList<Binding> bindings, string name, int position)
        {
            return bindings
                .Where(binding => string.Equals(binding.Symbol.Name, name, StringComparison.Ordinal) &&
                                  position >= binding.ScopeStart && position <= binding.ScopeEnd &&
                                  position >= binding.VisibleStart)
                .OrderByDescending(binding => binding.Depth)
                .ThenByDescending(binding => binding.VisibleStart)
                .FirstOrDefault();
        }

        private static void AddDuplicateDiagnostics(List<Binding> bindings, List<DdfDiagnostic> diagnostics, string source)
        {
            foreach (IGrouping<string, Binding> group in bindings.GroupBy(binding =>
                binding.ScopeStart + ":" + binding.ScopeEnd + ":" + binding.Symbol.Name, StringComparer.Ordinal))
            {
                foreach (Binding duplicate in group.OrderBy(binding => binding.Symbol.SelectionStart).Skip(1))
                {
                    diagnostics.Add(CreateDiagnostic(
                        "DDF202",
                        "Dichiarazione duplicata nello stesso ambito: '" + duplicate.Symbol.Name + "'.",
                        duplicate.Symbol.SelectionStart,
                        duplicate.Symbol.SelectionLength,
                        source));
                }
            }
        }

        private static DdfDiagnostic CreateDiagnostic(string code, string message, int start, int length, string source)
        {
            int line = 1;
            int column = 1;
            for (int index = 0; index < Math.Min(start, source.Length); index++)
            {
                if (source[index] == '\n') { line++; column = 1; }
                else if (source[index] != '\r') column++;
            }

            return new DdfDiagnostic(code, message, start, Math.Max(1, length), line, column);
        }
    }
}
