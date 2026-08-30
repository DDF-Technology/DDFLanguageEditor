using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfTextRange
    {
        public DdfTextRange(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            Start = start;
            Length = length;
        }

        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
    }

    public static class DdfSelectionService
    {
        public static DdfTextRange GetNextExpansion(string text, int selectionStart, int selectionLength)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (selectionStart < 0 || selectionStart > text.Length) throw new ArgumentOutOfRangeException(nameof(selectionStart));
            if (selectionLength < 0 || selectionStart + selectionLength > text.Length) throw new ArgumentOutOfRangeException(nameof(selectionLength));
            if (text.Length == 0) return null;

            DdfLexResult lexResult = DdfLexer.Lex(text);
            DdfParseResult parseResult = DdfParser.Parse(text, lexResult);
            var candidates = new List<DdfTextRange>();

            DdfToken token = FindToken(lexResult.Tokens, selectionStart, selectionLength);
            if (token != null) candidates.Add(new DdfTextRange(token.Start, token.Length));

            CollectContainingNodes(parseResult.Root, selectionStart, selectionLength, candidates);
            foreach (DdfDelimiterMatch pair in DdfDelimiterMatcher.FindPairs(text, lexResult))
            {
                AddIfContaining(candidates, pair.OpenStart, pair.CloseStart - pair.OpenStart + 1,
                    selectionStart, selectionLength);
            }
            candidates.Add(new DdfTextRange(0, text.Length));

            candidates.Sort((left, right) =>
            {
                int length = left.Length.CompareTo(right.Length);
                return length != 0 ? length : right.Start.CompareTo(left.Start);
            });

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (DdfTextRange candidate in candidates)
            {
                string key = candidate.Start + ":" + candidate.Length;
                if (!seen.Add(key)) continue;
                if (candidate.Start <= selectionStart && candidate.End >= selectionStart + selectionLength &&
                    (candidate.Start != selectionStart || candidate.Length != selectionLength))
                    return candidate;
            }
            return null;
        }

        private static DdfToken FindToken(IReadOnlyList<DdfToken> tokens, int selectionStart, int selectionLength)
        {
            int selectionEnd = selectionStart + selectionLength;
            DdfToken touchingLeft = null;
            foreach (DdfToken token in tokens)
            {
                if (selectionLength == 0)
                {
                    if (token.Start <= selectionStart && selectionStart < token.End) return token;
                    if (token.End == selectionStart) touchingLeft = token;
                }
                else if (token.Start <= selectionStart && token.End >= selectionEnd)
                {
                    return token;
                }
            }
            return touchingLeft;
        }

        private static void CollectContainingNodes(DdfSyntaxNode node, int start, int length, List<DdfTextRange> ranges)
        {
            if (node == null || node is MissingExpressionSyntax) return;
            AddIfContaining(ranges, node.Start, node.Length, start, length);

            if (node is CompilationUnitSyntax compilation)
                Collect(compilation.Members, start, length, ranges);
            else if (node is StructDeclarationSyntax structure)
                Collect(structure.Fields, start, length, ranges);
            else if (node is FunctionDeclarationSyntax function)
            {
                Collect(function.Parameters, start, length, ranges);
                CollectContainingNodes(function.ReturnType, start, length, ranges);
                CollectContainingNodes(function.Body, start, length, ranges);
            }
            else if (node is GlobalStatementSyntax global)
                CollectContainingNodes(global.Statement, start, length, ranges);
            else if (node is ParameterSyntax parameter)
                CollectContainingNodes(parameter.Type, start, length, ranges);
            else if (node is TypeReferenceSyntax type)
                Collect(type.ArrayLengths, start, length, ranges);
            else if (node is BlockStatementSyntax block)
                Collect(block.Statements, start, length, ranges);
            else if (node is VariableDeclarationStatementSyntax variable)
            {
                CollectContainingNodes(variable.Type, start, length, ranges);
                CollectContainingNodes(variable.Initializer, start, length, ranges);
            }
            else if (node is IfStatementSyntax conditional)
            {
                CollectContainingNodes(conditional.Condition, start, length, ranges);
                CollectContainingNodes(conditional.Body, start, length, ranges);
            }
            else if (node is WhileStatementSyntax whileStatement)
            {
                CollectContainingNodes(whileStatement.Condition, start, length, ranges);
                CollectContainingNodes(whileStatement.Body, start, length, ranges);
            }
            else if (node is DoWhileStatementSyntax doWhile)
            {
                CollectContainingNodes(doWhile.Body, start, length, ranges);
                CollectContainingNodes(doWhile.Condition, start, length, ranges);
            }
            else if (node is ForStatementSyntax forStatement)
            {
                CollectContainingNodes(forStatement.Initializer, start, length, ranges);
                CollectContainingNodes(forStatement.Condition, start, length, ranges);
                CollectContainingNodes(forStatement.Increment, start, length, ranges);
                CollectContainingNodes(forStatement.Body, start, length, ranges);
            }
            else if (node is ReturnStatementSyntax returnStatement)
                CollectContainingNodes(returnStatement.Expression, start, length, ranges);
            else if (node is ExpressionStatementSyntax expressionStatement)
                CollectContainingNodes(expressionStatement.Expression, start, length, ranges);
            else if (node is ParenthesizedExpressionSyntax parenthesized)
                CollectContainingNodes(parenthesized.Expression, start, length, ranges);
            else if (node is UnaryExpressionSyntax unary)
                CollectContainingNodes(unary.Operand, start, length, ranges);
            else if (node is BinaryExpressionSyntax binary)
            {
                CollectContainingNodes(binary.Left, start, length, ranges);
                CollectContainingNodes(binary.Right, start, length, ranges);
            }
            else if (node is PostfixExpressionSyntax postfix)
                CollectContainingNodes(postfix.Operand, start, length, ranges);
            else if (node is CallExpressionSyntax call)
            {
                CollectContainingNodes(call.Target, start, length, ranges);
                Collect(call.Arguments, start, length, ranges);
            }
            else if (node is IndexExpressionSyntax index)
            {
                CollectContainingNodes(index.Target, start, length, ranges);
                CollectContainingNodes(index.Index, start, length, ranges);
            }
            else if (node is MemberAccessExpressionSyntax member)
                CollectContainingNodes(member.Target, start, length, ranges);
        }

        private static void Collect<T>(IReadOnlyList<T> nodes, int start, int length, List<DdfTextRange> ranges)
            where T : DdfSyntaxNode
        {
            if (nodes == null) return;
            foreach (T node in nodes) CollectContainingNodes(node, start, length, ranges);
        }

        private static void AddIfContaining(List<DdfTextRange> ranges, int rangeStart, int rangeLength, int selectionStart, int selectionLength)
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

    public static class DdfDelimiterNavigation
    {
        public static int? GetMatchingPosition(string text, int caretPosition)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            DdfDelimiterMatch match = DdfDelimiterMatcher.FindMatch(text, caretPosition);
            if (match == null) return null;
            int touchedPosition = caretPosition < text.Length && IsDelimiter(text[caretPosition])
                ? caretPosition
                : caretPosition - 1;
            return touchedPosition == match.OpenStart ? match.CloseStart : match.OpenStart;
        }

        private static bool IsDelimiter(char value)
        {
            return value == '(' || value == ')' || value == '[' || value == ']' || value == '{' || value == '}';
        }
    }
}
