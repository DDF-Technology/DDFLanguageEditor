using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public enum DdfBreadcrumbKind
    {
        Structure,
        Enumeration,
        Function,
        If,
        Select,
        While,
        DoWhile,
        For
    }

    public sealed class DdfBreadcrumbItem
    {
        public DdfBreadcrumbItem(string label, DdfBreadcrumbKind kind, int start, int length, int selectionStart, int selectionLength)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Kind = kind;
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
            SelectionStart = Math.Max(0, selectionStart);
            SelectionLength = Math.Max(0, selectionLength);
        }

        public string Label { get; }
        public DdfBreadcrumbKind Kind { get; }
        public int Start { get; }
        public int Length { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
    }

    public static class DdfBreadcrumbService
    {
        public static IReadOnlyList<DdfBreadcrumbItem> GetPath(DdfParseResult parseResult, string source, int position)
        {
            if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (parseResult.GenericRoot != null && parseResult.Grammar != null &&
                parseResult.GrammarDiagnostics.Count == 0)
                return DdfGenericIdeService.GetBreadcrumbPath(
                    parseResult.GenericRoot, parseResult.Grammar, source, position);
            return GetPath(parseResult.Root, position);
        }

        public static IReadOnlyList<DdfBreadcrumbItem> GetPath(CompilationUnitSyntax root, int position)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            int safePosition = Math.Max(0, Math.Min(position, root.End));
            var path = new List<DdfBreadcrumbItem>();
            foreach (MemberSyntax member in root.Members)
            {
                if (!Contains(member, safePosition)) continue;
                if (member is StructDeclarationSyntax structure)
                {
                    path.Add(new DdfBreadcrumbItem(
                        structure.Name,
                        DdfBreadcrumbKind.Structure,
                        structure.Start,
                        structure.Length,
                        structure.NameStart,
                        structure.NameLength));
                }
                else if (member is EnumDeclarationSyntax enumeration)
                {
                    path.Add(new DdfBreadcrumbItem(enumeration.Name,
                        DdfBreadcrumbKind.Enumeration, enumeration.Start,
                        enumeration.Length, enumeration.NameStart,
                        enumeration.NameLength));
                }
                else if (member is FunctionDeclarationSyntax function)
                {
                    path.Add(new DdfBreadcrumbItem(
                        function.Name + "()",
                        DdfBreadcrumbKind.Function,
                        function.Start,
                        function.Length,
                        function.NameStart,
                        function.NameLength));
                    AddStatementPath(function.Body, safePosition, path);
                }
                else if (member is GlobalStatementSyntax global)
                {
                    AddStatementPath(global.Statement, safePosition, path);
                }
                break;
            }
            return path.AsReadOnly();
        }

        private static bool AddStatementPath(StatementSyntax statement, int position, List<DdfBreadcrumbItem> path)
        {
            if (statement == null || !Contains(statement, position)) return false;
            if (statement is BlockStatementSyntax block)
            {
                foreach (StatementSyntax child in block.Statements)
                    if (AddStatementPath(child, position, path)) break;
                return true;
            }
            if (statement is IfStatementSyntax ifStatement)
            {
                AddControl(path, "if", DdfBreadcrumbKind.If, ifStatement, 2);
                if (!AddStatementPath(ifStatement.Body, position, path) && ifStatement.ElseBody != null)
                    AddStatementPath(ifStatement.ElseBody, position, path);
                return true;
            }
            if (statement is SelectStatementSyntax selection)
            {
                AddControl(path, "select", DdfBreadcrumbKind.Select, selection, 6);
                foreach (SelectClauseSyntax clause in selection.Clauses)
                    if (AddStatementPath(clause.Body, position, path)) break;
                return true;
            }
            if (statement is WhileStatementSyntax whileStatement)
            {
                AddControl(path, "while", DdfBreadcrumbKind.While, whileStatement, 5);
                AddStatementPath(whileStatement.Body, position, path);
                return true;
            }
            if (statement is DoWhileStatementSyntax doWhileStatement)
            {
                AddControl(path, "do…while", DdfBreadcrumbKind.DoWhile, doWhileStatement, 2);
                AddStatementPath(doWhileStatement.Body, position, path);
                return true;
            }
            if (statement is ForStatementSyntax forStatement)
            {
                AddControl(path, "for", DdfBreadcrumbKind.For, forStatement, 3);
                AddStatementPath(forStatement.Body, position, path);
                return true;
            }
            return true;
        }

        private static void AddControl(List<DdfBreadcrumbItem> path, string label, DdfBreadcrumbKind kind,
            StatementSyntax statement, int keywordLength)
        {
            path.Add(new DdfBreadcrumbItem(
                label,
                kind,
                statement.Start,
                statement.Length,
                statement.Start,
                Math.Min(keywordLength, statement.Length)));
        }

        private static bool Contains(DdfSyntaxNode node, int position)
        {
            return node != null && position >= node.Start && position <= node.End;
        }
    }
}
