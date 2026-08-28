using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public enum DdfFoldingKind
    {
        Structure,
        Function,
        Block
    }

    public sealed class DdfFoldingRange
    {
        public DdfFoldingRange(DdfFoldingKind kind, int start, int end)
        {
            if (start < 0 || end <= start) throw new ArgumentOutOfRangeException(nameof(start));
            Kind = kind;
            Start = start;
            End = end;
        }

        public DdfFoldingKind Kind { get; }
        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
        public int ContentStart => Start + 1;
        public int ContentLength => Math.Max(0, End - Start - 2);
        public bool Contains(int position) => position >= Start && position < End;
    }

    public static class DdfFoldingRangeProvider
    {
        public static IReadOnlyList<DdfFoldingRange> Create(CompilationUnitSyntax root, string text)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (text == null) throw new ArgumentNullException(nameof(text));

            var ranges = new List<DdfFoldingRange>();
            foreach (MemberSyntax member in root.Members)
            {
                if (member is StructDeclarationSyntax structure)
                {
                    if (structure.HasCloseBrace)
                    {
                        AddRange(ranges, DdfFoldingKind.Structure, structure.OpenBraceStart, structure.CloseBraceStart, text);
                    }
                }
                else if (member is FunctionDeclarationSyntax function)
                {
                    AddBlock(ranges, function.Body, DdfFoldingKind.Function, text);
                    VisitBlockContents(ranges, function.Body, text);
                }
                else if (member is GlobalStatementSyntax global)
                {
                    VisitStatement(ranges, global.Statement, text);
                }
            }

            ranges.Sort((left, right) => left.Start != right.Start
                ? left.Start.CompareTo(right.Start)
                : right.End.CompareTo(left.End));
            return ranges.AsReadOnly();
        }

        private static void VisitStatement(List<DdfFoldingRange> ranges, StatementSyntax statement, string text)
        {
            if (statement is BlockStatementSyntax block)
            {
                AddBlock(ranges, block, DdfFoldingKind.Block, text);
                VisitBlockContents(ranges, block, text);
            }
            else if (statement is IfStatementSyntax ifStatement)
            {
                VisitStatement(ranges, ifStatement.Body, text);
            }
            else if (statement is WhileStatementSyntax whileStatement)
            {
                VisitStatement(ranges, whileStatement.Body, text);
            }
            else if (statement is DoWhileStatementSyntax doWhileStatement)
            {
                VisitStatement(ranges, doWhileStatement.Body, text);
            }
            else if (statement is ForStatementSyntax forStatement)
            {
                VisitStatement(ranges, forStatement.Body, text);
            }
        }

        private static void VisitBlockContents(List<DdfFoldingRange> ranges, BlockStatementSyntax block, string text)
        {
            if (block == null) return;
            foreach (StatementSyntax statement in block.Statements) VisitStatement(ranges, statement, text);
        }

        private static void AddBlock(List<DdfFoldingRange> ranges, BlockStatementSyntax block, DdfFoldingKind kind, string text)
        {
            if (block == null || !block.HasCloseBrace) return;
            AddRange(ranges, kind, block.Start, block.CloseBraceStart, text);
        }

        private static void AddRange(List<DdfFoldingRange> ranges, DdfFoldingKind kind, int open, int close, string text)
        {
            if (open < 0 || close <= open || close >= text.Length) return;
            if (text[open] != '{' || text[close] != '}') return;
            if (text.IndexOf('\n', open, close - open) < 0) return;
            ranges.Add(new DdfFoldingRange(kind, open, close + 1));
        }
    }
}
