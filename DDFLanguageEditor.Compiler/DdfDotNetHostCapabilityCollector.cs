using System;
using System.Collections.Generic;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Compiler
{
    internal static class DdfDotNetHostCapabilityCollector
    {
        internal static IReadOnlyList<string> Collect(
            DdfDotNetCompilationWorkItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var result = new HashSet<string>(StringComparer.Ordinal);
            var symbols = item.BoundUnit.Symbols.ToDictionary(symbol => symbol.Id,
                StringComparer.Ordinal);
            foreach (DdfBoundIrDocument document in item.BoundUnit.Documents)
            {
                var visitor = new Visitor(document, symbols, result);
                visitor.VisitProgram(document.StructuralProgram);
            }
            return result.OrderBy(value => value, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        private sealed class Visitor
        {
            private readonly DdfBoundIrDocument document;
            private readonly IReadOnlyDictionary<string, DdfBoundSymbol> symbols;
            private readonly ISet<string> result;

            internal Visitor(DdfBoundIrDocument document,
                IReadOnlyDictionary<string, DdfBoundSymbol> symbols,
                ISet<string> result)
            {
                this.document = document;
                this.symbols = symbols;
                this.result = result;
            }

            internal void VisitProgram(DdfIrProgram program)
            {
                foreach (DdfIrMember member in program.Members)
                {
                    if (member is DdfIrFunction function)
                        VisitStatement(function.Body);
                    else if (member is DdfIrGlobal global)
                        VisitStatement(global.Statement);
                    else if (member is DdfIrStructure structure)
                        foreach (DdfIrVariableDeclaration field in structure.Fields)
                            VisitExpression(field.Initializer);
                }
            }

            private void VisitStatement(DdfIrStatement statement)
            {
                if (statement == null) return;
                if (statement is DdfIrBlock block)
                {
                    foreach (DdfIrStatement child in block.Statements)
                        VisitStatement(child);
                }
                else if (statement is DdfIrVariableDeclaration variable)
                    VisitExpression(variable.Initializer);
                else if (statement is DdfIrExpressionStatement expression)
                    VisitExpression(expression.Expression);
                else if (statement is DdfIrIf conditional)
                {
                    VisitExpression(conditional.Condition);
                    VisitStatement(conditional.Body);
                    VisitStatement(conditional.ElseBody);
                }
                else if (statement is DdfIrSelect selection)
                {
                    VisitExpression(selection.Expression);
                    foreach (DdfIrSelectClause clause in selection.Clauses)
                    {
                        VisitExpression(clause.Value);
                        VisitStatement(clause.Body);
                    }
                }
                else if (statement is DdfIrWhile loop)
                {
                    VisitExpression(loop.Condition);
                    VisitStatement(loop.Body);
                }
                else if (statement is DdfIrDoWhile postLoop)
                {
                    VisitStatement(postLoop.Body);
                    VisitExpression(postLoop.Condition);
                }
                else if (statement is DdfIrFor counted)
                {
                    VisitStatement(counted.Initializer);
                    VisitExpression(counted.Condition);
                    VisitExpression(counted.Increment);
                    VisitStatement(counted.Body);
                }
                else if (statement is DdfIrReturn returned)
                    VisitExpression(returned.Expression);
            }

            private void VisitExpression(DdfIrExpression expression,
                bool write = false)
            {
                if (expression == null) return;
                if (TryHostAccess(expression, out HostAccess access))
                {
                    string[] path = access.Members.Select(member => member.Name)
                        .ToArray();
                    result.Add(access.Index == null
                        ? path.Length == 0
                            ? write
                                ? DdfDotNetHostCapabilities.ForObjectWrite(
                                    access.HostObject.Name)
                                : DdfDotNetHostCapabilities.ForObjectRead(
                                    access.HostObject.Name)
                            : write
                                ? DdfDotNetHostCapabilities.ForMemberWrite(
                                    access.HostObject.Name, path)
                                : DdfDotNetHostCapabilities.ForMemberRead(
                                    access.HostObject.Name, path)
                        : write
                            ? DdfDotNetHostCapabilities.ForElementWrite(
                                access.HostObject.Name, path)
                            : DdfDotNetHostCapabilities.ForElementRead(
                                access.HostObject.Name, path));
                    if (access.Index != null)
                        VisitExpression(access.Index.Index);
                    return;
                }
                if (expression is DdfIrGroup group)
                    VisitExpression(group.Expression, write);
                else if (expression is DdfIrUnary unary)
                    VisitExpression(unary.Operand);
                else if (expression is DdfIrBinary binary)
                {
                    if (binary.OperationId == DdfVmExpressionOperations.Assign)
                    {
                        VisitExpression(binary.Left, write: true);
                        VisitExpression(binary.Right);
                    }
                    else
                    {
                        VisitExpression(binary.Left);
                        VisitExpression(binary.Right);
                    }
                }
                else if (expression is DdfIrPostfix postfix)
                {
                    VisitExpression(postfix.Operand);
                    VisitExpression(postfix.Operand, write: true);
                }
                else if (expression is DdfIrCall call)
                {
                    DdfBoundExpression bound = Bound(call);
                    if (bound != null && symbols.TryGetValue(
                            bound.CallTargetSymbolId, out DdfBoundSymbol target) &&
                        target.Kind == DdfBoundSymbolKind.HostFunction)
                        result.Add(DdfDotNetHostCapabilities.ForFunction(target.Name));
                    foreach (DdfIrExpression argument in call.Arguments)
                        VisitExpression(argument);
                }
                else if (expression is DdfIrIndex index)
                {
                    VisitExpression(index.Target);
                    VisitExpression(index.Index);
                }
                else if (expression is DdfIrMemberAccess memberAccess)
                    VisitExpression(memberAccess.Target);
            }

            private sealed class HostAccess
            {
                internal DdfBoundSymbol HostObject { get; set; }
                internal List<DdfBoundSymbol> Members { get; } = new();
                internal DdfIrIndex Index { get; set; }
            }

            private bool TryHostAccess(DdfIrExpression expression,
                out HostAccess access)
            {
                access = null;
                if (expression is DdfIrName)
                {
                    DdfBoundExpression bound = Bound(expression);
                    if (bound == null || !symbols.TryGetValue(
                            bound.ReferencedSymbolId,
                            out DdfBoundSymbol hostObject) ||
                        hostObject.Kind != DdfBoundSymbolKind.HostObject ||
                        hostObject.ModuleIdentity != "host") return false;
                    access = new HostAccess { HostObject = hostObject };
                    return true;
                }
                if (expression is DdfIrMemberAccess memberAccess)
                {
                    if (!TryHostAccess(memberAccess.Target, out access) ||
                        access.Index != null) return false;
                    DdfBoundExpression memberBound = Bound(memberAccess);
                    if (memberBound == null || !symbols.TryGetValue(
                            memberBound.ReferencedSymbolId,
                            out DdfBoundSymbol member) ||
                        member.Kind != DdfBoundSymbolKind.Field ||
                        member.ModuleIdentity != "host") return false;
                    access.Members.Add(member);
                    return true;
                }
                if (expression is DdfIrIndex index)
                {
                    if (!TryHostAccess(index.Target, out access) ||
                        access.Index != null) return false;
                    access.Index = index;
                    return true;
                }
                return false;
            }

            private DdfBoundExpression Bound(DdfIrExpression expression) =>
                document.Expressions.FirstOrDefault(item =>
                    item.Start == expression.Start &&
                    item.Length == expression.Length);
        }
    }
}
