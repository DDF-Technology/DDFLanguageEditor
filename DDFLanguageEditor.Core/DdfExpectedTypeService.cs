using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public static class DdfExpectedTypeService
    {
        public static string GetExpectedType(
            string source,
            CompilationUnitSyntax root,
            int position,
            IEnumerable<CompilationUnitSyntax> externalRoots = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            DdfTypeCheckResult types = DdfTypeChecker.Check(source, root, externalRoots);
            foreach (MemberSyntax member in root.Members)
            {
                string expected = FindInMember(member, position, types);
                if (!string.IsNullOrEmpty(expected)) return expected;
            }
            return string.Empty;
        }

        private static string FindInMember(MemberSyntax member, int position, DdfTypeCheckResult types)
        {
            if (member is FunctionDeclarationSyntax function && Contains(function, position))
                return FindInStatement(function.Body, position, Display(function.ReturnType), types);
            if (member is GlobalStatementSyntax global)
                return FindInStatement(global.Statement, position, string.Empty, types);
            return string.Empty;
        }

        private static string FindInStatement(StatementSyntax statement, int position, string returnType, DdfTypeCheckResult types)
        {
            if (statement == null || !Contains(statement, position)) return string.Empty;
            if (statement is VariableDeclarationStatementSyntax variable && variable.Initializer != null &&
                position >= variable.Initializer.Start)
                return Display(variable.Type);
            if (statement is ReturnStatementSyntax returnStatement && position >= returnStatement.Start)
                return returnType;
            if (statement is IfStatementSyntax conditional)
            {
                if (Contains(conditional.Condition, position)) return "bool";
                return FindInStatement(conditional.Body, position, returnType, types);
            }
            if (statement is WhileStatementSyntax whileStatement)
            {
                if (Contains(whileStatement.Condition, position)) return "bool";
                return FindInStatement(whileStatement.Body, position, returnType, types);
            }
            if (statement is DoWhileStatementSyntax doWhile)
            {
                if (Contains(doWhile.Condition, position)) return "bool";
                return FindInStatement(doWhile.Body, position, returnType, types);
            }
            if (statement is ForStatementSyntax forStatement)
            {
                if (Contains(forStatement.Condition, position)) return "bool";
                string nested = FindInStatement(forStatement.Initializer, position, returnType, types);
                return string.IsNullOrEmpty(nested)
                    ? FindInStatement(forStatement.Body, position, returnType, types)
                    : nested;
            }
            if (statement is BlockStatementSyntax block)
            {
                foreach (StatementSyntax child in block.Statements)
                {
                    string nested = FindInStatement(child, position, returnType, types);
                    if (!string.IsNullOrEmpty(nested)) return nested;
                }
            }
            if (statement is ExpressionStatementSyntax expressionStatement)
                return FindInExpression(expressionStatement.Expression, position, types);
            return string.Empty;
        }

        private static string FindInExpression(ExpressionSyntax expression, int position, DdfTypeCheckResult types)
        {
            if (expression == null || !Contains(expression, position)) return string.Empty;
            if (expression is BinaryExpressionSyntax binary && binary.OperatorText == "<<" &&
                position >= binary.Right.Start)
            {
                DdfTypedSpan left = types.FindTypeAt(binary.Left.Start);
                if (left != null && !left.Type.IsError) return left.Type.DisplayName;
            }
            return string.Empty;
        }

        private static bool Contains(DdfSyntaxNode node, int position)
        {
            return node != null && position >= node.Start && position <= node.End;
        }

        private static string Display(TypeReferenceSyntax type)
        {
            if (type == null || string.IsNullOrEmpty(type.Name)) return string.Empty;
            return type.Name + string.Concat(System.Linq.Enumerable.Repeat("[]", type.ArrayLengths.Count));
        }
    }
}
