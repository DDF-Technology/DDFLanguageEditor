using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfFlowchartNodeKind
    {
        Entry,
        Exit,
        Process,
        Call,
        Decision,
        LoopDecision,
        Return,
        Break,
        Terminate,
        Partial,
        Group
    }

    public enum DdfFlowchartEdgeKind
    {
        Next,
        True,
        False,
        Break,
        Return,
        Terminate
    }

    public sealed class DdfFlowchartNode
    {
        internal DdfFlowchartNode(string id, DdfFlowchartNodeKind kind, string label,
            int start, int length, IEnumerable<string> calledFunctions,
            IEnumerable<string> groupedNodeIds = null)
        {
            Id = id;
            Kind = kind;
            Label = label ?? string.Empty;
            Start = start;
            Length = length;
            CalledFunctions = (calledFunctions ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
            GroupedNodeIds = (groupedNodeIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public string Id { get; }
        public DdfFlowchartNodeKind Kind { get; }
        public string Label { get; }
        public int Start { get; }
        public int Length { get; }
        public IReadOnlyList<string> CalledFunctions { get; }
        public IReadOnlyList<string> GroupedNodeIds { get; }
        public bool IsCollapsedGroup => Kind == DdfFlowchartNodeKind.Group && GroupedNodeIds.Count > 0;
    }

    public sealed class DdfFlowchartEdge
    {
        internal DdfFlowchartEdge(string from, string to, DdfFlowchartEdgeKind kind, bool isBackEdge)
        { From = from; To = to; Kind = kind; IsBackEdge = isBackEdge; }

        public string From { get; }
        public string To { get; }
        public DdfFlowchartEdgeKind Kind { get; }
        public bool IsBackEdge { get; }
    }

    public sealed class DdfFlowchart
    {
        internal DdfFlowchart(string documentPath, string functionName, string entryNodeId, string exitNodeId,
            IEnumerable<DdfFlowchartNode> nodes, IEnumerable<DdfFlowchartEdge> edges)
        {
            DocumentPath = documentPath ?? string.Empty;
            FunctionName = functionName ?? string.Empty;
            EntryNodeId = entryNodeId;
            ExitNodeId = exitNodeId;
            Nodes = nodes.ToList().AsReadOnly();
            Edges = edges.ToList().AsReadOnly();
        }

        public const string FormatVersion = "1.0";
        public string DocumentPath { get; }
        public string FunctionName { get; }
        public string EntryNodeId { get; }
        public string ExitNodeId { get; }
        public IReadOnlyList<DdfFlowchartNode> Nodes { get; }
        public IReadOnlyList<DdfFlowchartEdge> Edges { get; }
        public DdfFlowchartNode FindNode(string id) => Nodes.FirstOrDefault(node => node.Id == id);
    }

    public sealed class DdfFlowchartBuildResult
    {
        internal DdfFlowchartBuildResult(DdfFlowchart flowchart, IEnumerable<DdfDiagnostic> diagnostics)
        { Flowchart = flowchart; Diagnostics = diagnostics.ToList().AsReadOnly(); }

        public DdfFlowchart Flowchart { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public bool Success => Flowchart != null && Diagnostics.All(item => item.Severity != DdfDiagnosticSeverity.Error);
    }

    public static class DdfFlowchartBuilder
    {
        public static DdfFlowchartBuildResult Build(DdfIrModule module, string functionName)
        {
            return Build(module, functionName, null);
        }

        public static DdfFlowchartBuildResult Build(DdfIrModule module, string functionName,
            IEnumerable<DdfDiagnostic> diagnostics)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            DdfFlowchartBuildResult result = Build(module.Source, module.Program, functionName, module.DocumentPath);
            return new DdfFlowchartBuildResult(result.Flowchart,
                (diagnostics ?? Array.Empty<DdfDiagnostic>()).Concat(result.Diagnostics));
        }

        public static DdfFlowchartBuildResult Build(string source, string functionName,
            DdfLanguageDefinition language = null, string documentPath = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentException("Function name is required.", nameof(functionName));
            language = language ?? DdfLanguageCatalog.Default;
            DdfParseResult parse = DdfParser.Parse(source, language);
            DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, parse, language);
            var diagnostics = parse.Diagnostics.Concat(lowered.Diagnostics).GroupBy(item => new
            {
                item.Code,
                item.Start,
                item.Length,
                item.Message
            }).Select(group => group.First()).ToList();
            if (!lowered.Success || diagnostics.Any(item => item.Severity == DdfDiagnosticSeverity.Error))
                return new DdfFlowchartBuildResult(null, diagnostics);
            DdfFlowchartBuildResult result = Build(source, lowered.Program, functionName, documentPath);
            return new DdfFlowchartBuildResult(result.Flowchart, diagnostics.Concat(result.Diagnostics));
        }

        public static DdfFlowchartBuildResult Build(string source, DdfIrProgram program, string functionName,
            string documentPath = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentException("Function name is required.", nameof(functionName));
            DdfIrFunction function = program.Members.OfType<DdfIrFunction>()
                .FirstOrDefault(item => string.Equals(item.Name, functionName, StringComparison.Ordinal));
            if (function == null)
            {
                var diagnostic = new DdfDiagnostic("DDFC001", "Funzione non trovata per la flowchart: '" + functionName + "'.",
                    0, 1, 1, 1, documentPath: documentPath);
                return new DdfFlowchartBuildResult(null, new[] { diagnostic });
            }
            return new DdfFlowchartBuildResult(new Builder(source, documentPath, function).Build(), Array.Empty<DdfDiagnostic>());
        }

        private sealed class Builder
        {
            private readonly string source;
            private readonly string documentPath;
            private readonly DdfIrFunction function;
            private readonly List<DdfFlowchartNode> nodes = new List<DdfFlowchartNode>();
            private readonly List<DdfFlowchartEdge> edges = new List<DdfFlowchartEdge>();
            private int nextId;

            public Builder(string source, string documentPath, DdfIrFunction function)
            { this.source = source; this.documentPath = documentPath; this.function = function; }

            public DdfFlowchart Build()
            {
                string exit = AddNode(DdfFlowchartNodeKind.Exit, "Fine " + function.Name,
                    function.NameStart, function.NameLength);
                Target exitTarget = new Target(exit, DdfFlowchartEdgeKind.Next);
                string body = BuildBlock(function.Body, exitTarget, exitTarget);
                string entry = AddNode(DdfFlowchartNodeKind.Entry, function.Name + "()",
                    function.NameStart, function.NameLength);
                AddEdge(entry, body ?? exit, DdfFlowchartEdgeKind.Next);
                return new DdfFlowchart(documentPath, function.Name, entry, exit, nodes, edges);
            }

            private string BuildBlock(DdfIrBlock block, Target continuation, Target breakTarget)
            {
                Target current = continuation;
                if (block != null)
                    for (int index = block.Statements.Count - 1; index >= 0; index--)
                        current = new Target(BuildStatement(block.Statements[index], current, breakTarget), DdfFlowchartEdgeKind.Next);
                return current.Id;
            }

            private string BuildStatement(DdfIrStatement statement, Target continuation, Target breakTarget)
            {
                if (statement == null) return continuation.Id;
                if (statement is DdfIrBlock block) return BuildBlock(block, continuation, breakTarget);
                if (statement is DdfIrIf conditional) return BuildIf(conditional, continuation, breakTarget);
                if (statement is DdfIrWhile loop) return BuildWhile(loop, continuation);
                if (statement is DdfIrDoWhile postLoop) return BuildDoWhile(postLoop, continuation);
                if (statement is DdfIrFor forLoop) return BuildFor(forLoop, continuation);
                if (statement is DdfIrReturn returned)
                {
                    string node = AddNode(DdfFlowchartNodeKind.Return, Slice(statement), statement.Start, statement.Length,
                        Calls(returned.Expression));
                    AddEdge(node, FindExit(), DdfFlowchartEdgeKind.Return);
                    return node;
                }
                if (statement is DdfIrBreak)
                {
                    string node = AddNode(DdfFlowchartNodeKind.Break, Slice(statement), statement.Start, statement.Length);
                    AddEdge(node, breakTarget.Id, DdfFlowchartEdgeKind.Break);
                    return node;
                }
                if (statement is DdfIrEnd)
                {
                    string node = AddNode(DdfFlowchartNodeKind.Terminate, Slice(statement), statement.Start, statement.Length);
                    AddEdge(node, FindExit(), DdfFlowchartEdgeKind.Terminate);
                    return node;
                }

                DdfIrExpression expression = statement is DdfIrVariableDeclaration variable ? variable.Initializer :
                    (statement as DdfIrExpressionStatement)?.Expression;
                IReadOnlyList<string> calls = Calls(expression);
                DdfFlowchartNodeKind kind = calls.Count > 0 ? DdfFlowchartNodeKind.Call :
                    expression is DdfIrMissingExpression ? DdfFlowchartNodeKind.Partial : DdfFlowchartNodeKind.Process;
                string process = AddNode(kind, Slice(statement), statement.Start, statement.Length, calls);
                AddEdge(process, continuation.Id, continuation.Kind, continuation.IsBackEdge);
                return process;
            }

            private string BuildIf(DdfIrIf conditional, Target continuation, Target breakTarget)
            {
                string decision = AddNode(DdfFlowchartNodeKind.Decision, Slice(conditional.Condition),
                    conditional.Condition?.Start ?? conditional.Start, Math.Max(1, conditional.Condition?.Length ?? 1),
                    Calls(conditional.Condition));
                string whenTrue = BuildStatement(conditional.Body, continuation, breakTarget);
                string whenFalse = conditional.ElseBody == null ? continuation.Id :
                    BuildStatement(conditional.ElseBody, continuation, breakTarget);
                AddEdge(decision, whenTrue, DdfFlowchartEdgeKind.True,
                    conditional.Body == null && continuation.IsBackEdge);
                AddEdge(decision, whenFalse, DdfFlowchartEdgeKind.False,
                    conditional.ElseBody == null && continuation.IsBackEdge);
                return decision;
            }

            private string BuildWhile(DdfIrWhile loop, Target continuation)
            {
                string decision = AddNode(DdfFlowchartNodeKind.LoopDecision, Slice(loop.Condition),
                    loop.Condition?.Start ?? loop.Start, Math.Max(1, loop.Condition?.Length ?? 1), Calls(loop.Condition));
                string body = BuildStatement(loop.Body, new Target(decision, DdfFlowchartEdgeKind.Next, true), continuation);
                AddEdge(decision, body, DdfFlowchartEdgeKind.True);
                AddEdge(decision, continuation.Id, DdfFlowchartEdgeKind.False);
                return decision;
            }

            private string BuildDoWhile(DdfIrDoWhile loop, Target continuation)
            {
                string decision = AddNode(DdfFlowchartNodeKind.LoopDecision, Slice(loop.Condition),
                    loop.Condition?.Start ?? loop.Start, Math.Max(1, loop.Condition?.Length ?? 1), Calls(loop.Condition));
                string body = BuildStatement(loop.Body, new Target(decision, DdfFlowchartEdgeKind.Next), continuation);
                AddEdge(decision, body, DdfFlowchartEdgeKind.True, true);
                AddEdge(decision, continuation.Id, DdfFlowchartEdgeKind.False);
                return body;
            }

            private string BuildFor(DdfIrFor loop, Target continuation)
            {
                string conditionLabel = loop.Condition == null ? "true" : Slice(loop.Condition);
                string decision = AddNode(DdfFlowchartNodeKind.LoopDecision, conditionLabel,
                    loop.Condition?.Start ?? loop.Start, Math.Max(1, loop.Condition?.Length ?? 1), Calls(loop.Condition));
                string increment = decision;
                if (loop.Increment != null)
                {
                    increment = AddNode(DdfFlowchartNodeKind.Process, Slice(loop.Increment), loop.Increment.Start,
                        Math.Max(1, loop.Increment.Length), Calls(loop.Increment));
                    AddEdge(increment, decision, DdfFlowchartEdgeKind.Next, true);
                }
                string body = BuildStatement(loop.Body, new Target(increment, DdfFlowchartEdgeKind.Next), continuation);
                AddEdge(decision, body, DdfFlowchartEdgeKind.True);
                AddEdge(decision, continuation.Id, DdfFlowchartEdgeKind.False);
                return loop.Initializer == null ? decision :
                    BuildStatement(loop.Initializer, new Target(decision, DdfFlowchartEdgeKind.Next), continuation);
            }

            private string AddNode(DdfFlowchartNodeKind kind, string label, int start, int length,
                IEnumerable<string> calledFunctions = null)
            {
                string id = "n" + nextId++;
                int safeStart = Math.Min(Math.Max(0, start), source.Length);
                int safeLength = Math.Min(Math.Max(0, length), source.Length - safeStart);
                nodes.Add(new DdfFlowchartNode(id, kind, label, safeStart, safeLength, calledFunctions));
                return id;
            }

            private void AddEdge(string from, string to, DdfFlowchartEdgeKind kind, bool isBackEdge = false)
            {
                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    edges.Add(new DdfFlowchartEdge(from, to, kind, isBackEdge));
            }

            private string FindExit() => nodes.First(node => node.Kind == DdfFlowchartNodeKind.Exit).Id;

            private string Slice(DdfIrNode node)
            {
                if (node == null) return string.Empty;
                int start = Math.Min(Math.Max(0, node.Start), source.Length);
                int length = Math.Min(Math.Max(0, node.Length), source.Length - start);
                return Regex.Replace(source.Substring(start, length).Trim(), "\\s+", " ");
            }

            private static IReadOnlyList<string> Calls(DdfIrExpression expression)
            {
                var result = new List<string>();
                CollectCalls(expression, result);
                return result.AsReadOnly();
            }

            private static void CollectCalls(DdfIrExpression expression, ICollection<string> result)
            {
                if (expression == null) return;
                if (expression is DdfIrCall call)
                {
                    if (call.Target is DdfIrName name) result.Add(name.Name);
                    else if (call.Target is DdfIrMemberAccess member) result.Add(member.Name);
                    CollectCalls(call.Target, result);
                    foreach (DdfIrExpression argument in call.Arguments) CollectCalls(argument, result);
                }
                else if (expression is DdfIrGroup group) CollectCalls(group.Expression, result);
                else if (expression is DdfIrUnary unary) CollectCalls(unary.Operand, result);
                else if (expression is DdfIrBinary binary)
                { CollectCalls(binary.Left, result); CollectCalls(binary.Right, result); }
                else if (expression is DdfIrPostfix postfix) CollectCalls(postfix.Operand, result);
                else if (expression is DdfIrIndex index)
                { CollectCalls(index.Target, result); CollectCalls(index.Index, result); }
                else if (expression is DdfIrMemberAccess member) CollectCalls(member.Target, result);
            }

            private readonly struct Target
            {
                public Target(string id, DdfFlowchartEdgeKind kind, bool isBackEdge = false)
                { Id = id; Kind = kind; IsBackEdge = isBackEdge; }
                public string Id { get; }
                public DdfFlowchartEdgeKind Kind { get; }
                public bool IsBackEdge { get; }
            }
        }
    }
}
