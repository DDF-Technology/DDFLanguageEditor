using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfBreakpointRemapResult
    {
        internal DdfBreakpointRemapResult(IReadOnlyList<int> lines, IReadOnlyList<int> unboundLines)
        {
            Lines = lines;
            UnboundLines = unboundLines;
        }

        public IReadOnlyList<int> Lines { get; }
        public IReadOnlyList<int> UnboundLines { get; }
    }

    public static class DdfBreakpointService
    {
        public static DdfBreakpointRemapResult Remap(string oldSource, string newSource, IEnumerable<int> breakpointLines)
        {
            string[] oldLines = SplitLines(oldSource);
            string[] newLines = SplitLines(newSource);
            int prefix = 0;
            while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix]) prefix++;
            int suffix = 0;
            while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix &&
                   oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix]) suffix++;

            var mapped = new SortedSet<int>();
            var unbound = new SortedSet<int>();
            foreach (int line in breakpointLines ?? Array.Empty<int>())
            {
                if (line <= 0) continue;
                int oldIndex = line - 1;
                int newLine;
                bool isBound = true;
                if (oldIndex < prefix)
                {
                    newLine = line;
                }
                else if (oldIndex >= oldLines.Length - suffix)
                {
                    newLine = line + newLines.Length - oldLines.Length;
                }
                else
                {
                    string oldText = oldIndex < oldLines.Length ? oldLines[oldIndex].Trim() : string.Empty;
                    int changedEnd = newLines.Length - suffix;
                    var matches = Enumerable.Range(prefix, Math.Max(0, changedEnd - prefix))
                        .Where(index => oldText.Length > 0 && newLines[index].Trim() == oldText)
                        .ToList();
                    if (matches.Count > 0)
                    {
                        int expected = prefix + Math.Max(0, oldIndex - prefix);
                        newLine = matches.OrderBy(index => Math.Abs(index - expected)).First() + 1;
                    }
                    else
                    {
                        newLine = Math.Min(Math.Max(1, prefix + 1 + Math.Max(0, oldIndex - prefix)), Math.Max(1, newLines.Length));
                        isBound = false;
                    }
                }

                if (newLine <= 0 || newLine > newLines.Length) continue;
                mapped.Add(newLine);
                if (!isBound) unbound.Add(newLine);
            }
            return new DdfBreakpointRemapResult(mapped.ToList(), unbound.ToList());
        }

        public static IReadOnlyCollection<int> GetExecutableLines(string source, CompilationUnitSyntax root)
        {
            var lines = new HashSet<int>();
            if (root == null) return lines;
            foreach (MemberSyntax member in root.Members)
            {
                if (member is FunctionDeclarationSyntax function) VisitStatement(source, function.Body, lines);
                else if (member is GlobalStatementSyntax global) VisitStatement(source, global.Statement, lines);
            }
            return lines;
        }

        private static void VisitStatement(string source, StatementSyntax statement, ISet<int> lines)
        {
            if (statement == null) return;
            if (!(statement is BlockStatementSyntax)) lines.Add(GetLine(source, statement.Start));
            if (statement is BlockStatementSyntax block)
                foreach (StatementSyntax child in block.Statements) VisitStatement(source, child, lines);
            else if (statement is IfStatementSyntax conditional) VisitStatement(source, conditional.Body, lines);
            else if (statement is WhileStatementSyntax loop) VisitStatement(source, loop.Body, lines);
            else if (statement is DoWhileStatementSyntax doLoop) VisitStatement(source, doLoop.Body, lines);
            else if (statement is ForStatementSyntax forLoop)
            {
                VisitStatement(source, forLoop.Initializer, lines);
                VisitStatement(source, forLoop.Body, lines);
            }
        }

        private static int GetLine(string source, int position)
        {
            source = source ?? string.Empty;
            int line = 1;
            int end = Math.Min(Math.Max(0, position), source.Length);
            for (int index = 0; index < end; index++) if (source[index] == '\n') line++;
            return line;
        }

        private static string[] SplitLines(string source)
        {
            return (source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }
}
