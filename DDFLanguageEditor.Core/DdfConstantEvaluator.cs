using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfConstantKind
    {
        Integer,
        Floating,
        Boolean,
        Character,
        String,
        Enumeration
    }

    public sealed class DdfConstantValue
    {
        private DdfConstantValue(string typeName, DdfConstantKind kind, object value)
        {
            TypeName = typeName ?? string.Empty;
            Kind = kind;
            Value = value;
        }

        public string TypeName { get; }
        public DdfConstantKind Kind { get; }
        public object Value { get; }

        internal static DdfConstantValue Integer(int value) =>
            new DdfConstantValue("int", DdfConstantKind.Integer, value);
        internal static DdfConstantValue Floating(double value) =>
            new DdfConstantValue("float", DdfConstantKind.Floating, value);
        internal static DdfConstantValue Boolean(bool value) =>
            new DdfConstantValue("bool", DdfConstantKind.Boolean, value);
        internal static DdfConstantValue Character(char value) =>
            new DdfConstantValue("char", DdfConstantKind.Character, value);
        internal static DdfConstantValue String(string value) =>
            new DdfConstantValue("string", DdfConstantKind.String, value ?? string.Empty);
        internal static DdfConstantValue Enumeration(string typeName,
            string memberName) => new DdfConstantValue(typeName,
                DdfConstantKind.Enumeration,
                new DdfEnumValue(typeName, memberName));
    }

    public sealed class DdfConstantEvaluationIssue
    {
        internal DdfConstantEvaluationIssue(string documentPath, DdfDiagnostic diagnostic)
        {
            DocumentPath = documentPath ?? string.Empty;
            Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        }

        public string DocumentPath { get; }
        public DdfDiagnostic Diagnostic { get; }
    }

    /// <summary>
    /// Backend-neutral evaluator for fixed values. It materializes primitive values
    /// directly on the structural IR, before either the VM or Roslyn consumes it.
    /// </summary>
    public static class DdfConstantEvaluator
    {
        public static IReadOnlyList<DdfConstantEvaluationIssue> Evaluate(
            IEnumerable<DdfIrModule> modules)
        {
            return new Evaluation(modules).Run();
        }

        private sealed class Declaration
        {
            public DdfIrModule Module;
            public DdfIrVariableDeclaration Variable;
        }

        private sealed class Evaluation
        {
            private readonly List<DdfIrModule> modules;
            private readonly Dictionary<string, List<DdfIrModule>> modulesByIdentity;
            private readonly Dictionary<string, Dictionary<string, Declaration>> globals;
            private readonly Dictionary<string, Dictionary<string, DdfIrEnum>> enumerations;
            private readonly Dictionary<DdfIrVariableDeclaration, int> states =
                new Dictionary<DdfIrVariableDeclaration, int>();
            private readonly List<DdfConstantEvaluationIssue> issues =
                new List<DdfConstantEvaluationIssue>();
            private readonly HashSet<DdfIrVariableDeclaration> cycleReported =
                new HashSet<DdfIrVariableDeclaration>();

            internal Evaluation(IEnumerable<DdfIrModule> modules)
            {
                this.modules = (modules ?? throw new ArgumentNullException(nameof(modules)))
                    .Where(item => item != null).ToList();
                modulesByIdentity = this.modules.GroupBy(Identity)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
                globals = new Dictionary<string, Dictionary<string, Declaration>>(StringComparer.Ordinal);
                enumerations = new Dictionary<string, Dictionary<string, DdfIrEnum>>(
                    StringComparer.Ordinal);
                foreach (DdfIrModule module in this.modules)
                {
                    string identity = Identity(module);
                    if (!globals.TryGetValue(identity, out Dictionary<string, Declaration> values))
                    {
                        values = new Dictionary<string, Declaration>(StringComparer.Ordinal);
                        globals.Add(identity, values);
                    }
                    if (!enumerations.TryGetValue(identity,
                            out Dictionary<string, DdfIrEnum> enumValues))
                    {
                        enumValues = new Dictionary<string, DdfIrEnum>(
                            StringComparer.Ordinal);
                        enumerations.Add(identity, enumValues);
                    }
                    foreach (DdfIrEnum enumeration in module.Program.Members
                        .OfType<DdfIrEnum>())
                        enumValues.TryAdd(enumeration.Name, enumeration);
                    foreach (DdfIrVariableDeclaration variable in module.Program.Members
                        .OfType<DdfIrGlobal>()
                        .Select(item => item.Statement)
                        .OfType<DdfIrVariableDeclaration>()
                        .Where(item => item.IsFixed))
                    {
                        if (!values.ContainsKey(variable.Name))
                            values.Add(variable.Name, new Declaration { Module = module, Variable = variable });
                        variable.ConstantValue = null;
                    }
                }
            }

            internal IReadOnlyList<DdfConstantEvaluationIssue> Run()
            {
                foreach (Dictionary<string, Declaration> values in globals.Values)
                    foreach (Declaration declaration in values.Values)
                        EvaluateGlobal(declaration);

                foreach (DdfIrModule module in modules)
                    foreach (DdfIrFunction function in module.Program.Members.OfType<DdfIrFunction>())
                    {
                        var scopes = new List<Dictionary<string, DdfConstantValue>>
                        {
                            new Dictionary<string, DdfConstantValue>(StringComparer.Ordinal)
                        };
                        EvaluateStatement(module, function.Body, scopes);
                    }
                return issues.AsReadOnly();
            }

            private bool EvaluateGlobal(Declaration declaration)
            {
                if (declaration.Variable.ConstantValue != null) return true;
                states.TryGetValue(declaration.Variable, out int state);
                if (state == 2) return true;
                if (state == 3) return false;
                if (state == 1)
                {
                    if (cycleReported.Add(declaration.Variable))
                        Report(declaration.Module, declaration.Variable, "DDF318",
                            "Dipendenza ciclica fra valori fixed che coinvolge '" +
                            declaration.Variable.Name + "'.");
                    states[declaration.Variable] = 3;
                    return false;
                }

                states[declaration.Variable] = 1;
                try
                {
                    if (TryEvaluate(declaration.Module, declaration.Variable.Initializer,
                        Array.Empty<Dictionary<string, DdfConstantValue>>(), out DdfConstantValue raw) &&
                        TryConvert(raw, declaration.Variable.Type?.Name, out DdfConstantValue converted))
                    {
                        declaration.Variable.ConstantValue = converted;
                        states[declaration.Variable] = 2;
                        return true;
                    }
                }
                catch (ConstantFault fault)
                {
                    Report(declaration.Module, fault.Node ?? declaration.Variable,
                        "DDF319", fault.Message);
                }
                catch (Exception exception) when (exception is OverflowException ||
                                                   exception is FormatException)
                {
                    Report(declaration.Module, (DdfIrNode)declaration.Variable.Initializer ?? declaration.Variable,
                        "DDF319", "Overflow o letterale non valido in un inizializzatore fixed.");
                }
                states[declaration.Variable] = 3;
                return false;
            }

            private void EvaluateStatement(DdfIrModule module, DdfIrStatement statement,
                List<Dictionary<string, DdfConstantValue>> scopes)
            {
                if (statement == null) return;
                if (statement is DdfIrBlock block)
                {
                    scopes.Add(new Dictionary<string, DdfConstantValue>(StringComparer.Ordinal));
                    foreach (DdfIrStatement child in block.Statements)
                        EvaluateStatement(module, child, scopes);
                    scopes.RemoveAt(scopes.Count - 1);
                }
                else if (statement is DdfIrVariableDeclaration variable && variable.IsFixed)
                {
                    variable.ConstantValue = null;
                    try
                    {
                        if (TryEvaluate(module, variable.Initializer, scopes, out DdfConstantValue raw) &&
                            TryConvert(raw, variable.Type?.Name, out DdfConstantValue converted))
                        {
                            variable.ConstantValue = converted;
                            scopes[scopes.Count - 1][variable.Name] = converted;
                        }
                    }
                    catch (ConstantFault fault)
                    {
                        Report(module, fault.Node ?? variable, "DDF319", fault.Message);
                    }
                    catch (Exception exception) when (exception is OverflowException ||
                                                       exception is FormatException)
                    {
                        Report(module, (DdfIrNode)variable.Initializer ?? variable, "DDF319",
                            "Overflow o letterale non valido in un inizializzatore fixed.");
                    }
                }
                else if (statement is DdfIrIf conditional)
                {
                    EvaluateStatement(module, conditional.Body, scopes);
                    EvaluateStatement(module, conditional.ElseBody, scopes);
                }
                else if (statement is DdfIrSelect selection)
                {
                    foreach (DdfIrSelectClause clause in selection.Clauses)
                        EvaluateStatement(module, clause.Body, scopes);
                }
                else if (statement is DdfIrWhile loop)
                    EvaluateStatement(module, loop.Body, scopes);
                else if (statement is DdfIrDoWhile postLoop)
                    EvaluateStatement(module, postLoop.Body, scopes);
                else if (statement is DdfIrFor counted)
                {
                    scopes.Add(new Dictionary<string, DdfConstantValue>(StringComparer.Ordinal));
                    EvaluateStatement(module, counted.Initializer, scopes);
                    EvaluateStatement(module, counted.Body, scopes);
                    scopes.RemoveAt(scopes.Count - 1);
                }
            }

            private bool TryEvaluate(DdfIrModule module, DdfIrExpression expression,
                IReadOnlyList<Dictionary<string, DdfConstantValue>> scopes,
                out DdfConstantValue value)
            {
                value = null;
                if (expression == null || expression is DdfIrMissingExpression) return false;
                if (expression is DdfIrLiteral literal)
                {
                    value = ParseLiteral(literal);
                    return true;
                }
                if (expression is DdfIrGroup group)
                    return TryEvaluate(module, group.Expression, scopes, out value);
                if (expression is DdfIrName name)
                {
                    for (int index = scopes.Count - 1; index >= 0; index--)
                        if (scopes[index].TryGetValue(name.Name, out value)) return true;
                    Declaration global = ResolveGlobal(module, name.Name);
                    if (global == null || !EvaluateGlobal(global)) return false;
                    value = global.Variable.ConstantValue;
                    return value != null;
                }
                if (expression is DdfIrMemberAccess member &&
                    member.Target is DdfIrName enumName &&
                    ResolveEnum(module, enumName.Name, out DdfIrEnum enumeration) &&
                    enumeration.Members.Any(item => item.Name == member.Name))
                {
                    value = DdfConstantValue.Enumeration(enumeration.Name,
                        member.Name);
                    return true;
                }
                if (expression is DdfIrUnary unary &&
                    TryEvaluate(module, unary.Operand, scopes, out DdfConstantValue operand))
                {
                    value = EvaluateUnary(unary, operand);
                    return true;
                }
                if (expression is DdfIrBinary binary)
                {
                    if (!TryEvaluate(module, binary.Left, scopes, out DdfConstantValue left)) return false;
                    if (binary.OperationId == DdfVmExpressionOperations.LogicalOr && AsBoolean(left, binary))
                    { value = DdfConstantValue.Boolean(true); return true; }
                    if (binary.OperationId == DdfVmExpressionOperations.LogicalAnd && !AsBoolean(left, binary))
                    { value = DdfConstantValue.Boolean(false); return true; }
                    if (!TryEvaluate(module, binary.Right, scopes, out DdfConstantValue right)) return false;
                    value = EvaluateBinary(binary, left, right);
                    return true;
                }
                return false;
            }

            private Declaration ResolveGlobal(DdfIrModule module, string name)
            {
                if (globals.TryGetValue(Identity(module), out Dictionary<string, Declaration> own) &&
                    own.TryGetValue(name, out Declaration local)) return local;
                Declaration match = null;
                foreach (DdfProjectDependency dependency in module.Dependencies)
                {
                    string identity = dependency.Id + "@" + dependency.Version;
                    if (!globals.TryGetValue(identity, out Dictionary<string, Declaration> values) ||
                        !values.TryGetValue(name, out Declaration candidate)) continue;
                    if (!modulesByIdentity.TryGetValue(identity, out List<DdfIrModule> dependencyModules) ||
                        !dependencyModules.Any(item => item.Exports.Contains(name, StringComparer.Ordinal))) continue;
                    if (match != null) return null;
                    match = candidate;
                }
                return match;
            }

            private bool ResolveEnum(DdfIrModule module, string name,
                out DdfIrEnum enumeration)
            {
                if (enumerations.TryGetValue(Identity(module),
                        out Dictionary<string, DdfIrEnum> own) &&
                    own.TryGetValue(name, out enumeration)) return true;
                enumeration = null;
                foreach (DdfProjectDependency dependency in module.Dependencies)
                {
                    string identity = dependency.Id + "@" + dependency.Version;
                    if (!enumerations.TryGetValue(identity,
                            out Dictionary<string, DdfIrEnum> values) ||
                        !values.TryGetValue(name, out DdfIrEnum candidate) ||
                        !modulesByIdentity.TryGetValue(identity,
                            out List<DdfIrModule> dependencyModules) ||
                        !dependencyModules.Any(item => item.Exports.Contains(name,
                            StringComparer.Ordinal))) continue;
                    if (enumeration != null)
                    {
                        enumeration = null;
                        return false;
                    }
                    enumeration = candidate;
                }
                return enumeration != null;
            }

            private static DdfConstantValue ParseLiteral(DdfIrLiteral literal)
            {
                if (literal.Kind == DdfLiteralKind.Boolean)
                    return DdfConstantValue.Boolean(literal.BooleanValue == true);
                if (literal.Kind == DdfLiteralKind.String)
                    return DdfConstantValue.String(Unescape(literal.Text.Substring(1,
                        Math.Max(0, literal.Text.Length - 2))));
                if (literal.Text.IndexOf('.') >= 0)
                    return Finite(double.Parse(literal.Text, NumberStyles.Float,
                        CultureInfo.InvariantCulture), literal);
                return DdfConstantValue.Integer(int.Parse(literal.Text,
                    NumberStyles.Integer, CultureInfo.InvariantCulture));
            }

            private static DdfConstantValue EvaluateUnary(DdfIrUnary unary,
                DdfConstantValue operand)
            {
                if (unary.OperationId == DdfVmExpressionOperations.LogicalNot)
                    return DdfConstantValue.Boolean(!AsBoolean(operand, unary));
                if (unary.OperationId == DdfVmExpressionOperations.Positive) return operand;
                if (unary.OperationId == DdfVmExpressionOperations.Negate)
                {
                    if (operand.Kind == DdfConstantKind.Integer)
                        return DdfConstantValue.Integer(checked(-(int)operand.Value));
                    return Finite(-AsDouble(operand, unary), unary);
                }
                throw new ConstantFault(unary, "Operatore unario non valutabile come costante.");
            }

            private static DdfConstantValue EvaluateBinary(DdfIrBinary binary,
                DdfConstantValue left, DdfConstantValue right)
            {
                string operation = binary.OperationId;
                if (operation == DdfVmExpressionOperations.LogicalOr)
                    return DdfConstantValue.Boolean(AsBoolean(left, binary) || AsBoolean(right, binary));
                if (operation == DdfVmExpressionOperations.LogicalAnd)
                    return DdfConstantValue.Boolean(AsBoolean(left, binary) && AsBoolean(right, binary));
                if (operation == DdfVmExpressionOperations.LogicalXor)
                    return DdfConstantValue.Boolean(AsBoolean(left, binary) ^ AsBoolean(right, binary));
                if (operation == DdfVmExpressionOperations.Equal)
                    return DdfConstantValue.Boolean(AreEqual(left, right));
                if (operation == DdfVmExpressionOperations.NotEqual)
                    return DdfConstantValue.Boolean(!AreEqual(left, right));
                if (operation == DdfVmExpressionOperations.Less) return Compare(left, right, (a, b) => a < b, binary);
                if (operation == DdfVmExpressionOperations.LessOrEqual) return Compare(left, right, (a, b) => a <= b, binary);
                if (operation == DdfVmExpressionOperations.Greater) return Compare(left, right, (a, b) => a > b, binary);
                if (operation == DdfVmExpressionOperations.GreaterOrEqual) return Compare(left, right, (a, b) => a >= b, binary);
                if (operation == DdfVmExpressionOperations.BetweenExclusive)
                    return DdfConstantValue.Boolean(AsDouble(left, binary) > AsDouble(right, binary) && AsDouble(left, binary) < AsDouble(right, binary));
                if (operation == DdfVmExpressionOperations.OutsideExclusive)
                    return DdfConstantValue.Boolean(AsDouble(left, binary) < AsDouble(right, binary) || AsDouble(left, binary) > AsDouble(right, binary));
                if (operation == DdfVmExpressionOperations.Add &&
                    (left.Kind == DdfConstantKind.String || right.Kind == DdfConstantKind.String))
                    return DdfConstantValue.String(Format(left) + Format(right));
                return Numeric(binary, left, right);
            }

            private static DdfConstantValue Numeric(DdfIrBinary binary,
                DdfConstantValue left, DdfConstantValue right)
            {
                string operation = binary.OperationId;
                if (left.Kind == DdfConstantKind.Integer && right.Kind == DdfConstantKind.Integer &&
                    operation != DdfVmExpressionOperations.Root)
                {
                    int a = (int)left.Value;
                    int b = (int)right.Value;
                    if (operation == DdfVmExpressionOperations.Add) return DdfConstantValue.Integer(checked(a + b));
                    if (operation == DdfVmExpressionOperations.Subtract) return DdfConstantValue.Integer(checked(a - b));
                    if (operation == DdfVmExpressionOperations.Multiply) return DdfConstantValue.Integer(checked(a * b));
                    if (operation == DdfVmExpressionOperations.Divide)
                    {
                        if (b == 0) throw new ConstantFault(binary, "Divisione per zero in un inizializzatore fixed.");
                        return DdfConstantValue.Integer(checked(a / b));
                    }
                    if (operation == DdfVmExpressionOperations.Power)
                    {
                        if (b < 0) throw new ConstantFault(binary, "Una potenza intera fixed richiede un esponente non negativo.");
                        return DdfConstantValue.Integer(IntegerPower(a, b));
                    }
                }
                double x = AsDouble(left, binary);
                double y = AsDouble(right, binary);
                if (operation == DdfVmExpressionOperations.Divide && y == 0d)
                    throw new ConstantFault(binary, "Divisione per zero in un inizializzatore fixed.");
                if (operation == DdfVmExpressionOperations.Root && y == 0d)
                    throw new ConstantFault(binary, "Indice della radice uguale a zero in un inizializzatore fixed.");
                double result = operation == DdfVmExpressionOperations.Add ? x + y :
                    operation == DdfVmExpressionOperations.Subtract ? x - y :
                    operation == DdfVmExpressionOperations.Multiply ? x * y :
                    operation == DdfVmExpressionOperations.Divide ? x / y :
                    operation == DdfVmExpressionOperations.Power ? Math.Pow(x, y) :
                    operation == DdfVmExpressionOperations.Root ? Math.Pow(x, 1d / y) : double.NaN;
                return Finite(result, binary);
            }

            private static bool TryConvert(DdfConstantValue value, string typeName,
                out DdfConstantValue converted)
            {
                converted = null;
                if (value == null) return false;
                if (typeName == "int" && value.Kind == DdfConstantKind.Integer) converted = value;
                else if (typeName == "float" && value.Kind == DdfConstantKind.Integer)
                    converted = DdfConstantValue.Floating((int)value.Value);
                else if (typeName == "float" && value.Kind == DdfConstantKind.Floating) converted = value;
                else if (typeName == "bool" && value.Kind == DdfConstantKind.Boolean) converted = value;
                else if (typeName == "string" && value.Kind == DdfConstantKind.String) converted = value;
                else if (typeName == "char" && value.Kind == DdfConstantKind.Character) converted = value;
                else if (typeName == "char" && value.Kind == DdfConstantKind.String && ((string)value.Value).Length == 1)
                    converted = DdfConstantValue.Character(((string)value.Value)[0]);
                else if (value.Kind == DdfConstantKind.Enumeration &&
                    value.TypeName == typeName) converted = value;
                return converted != null;
            }

            private static DdfConstantValue Compare(DdfConstantValue left,
                DdfConstantValue right, Func<double, double, bool> comparison, DdfIrNode node) =>
                DdfConstantValue.Boolean(comparison(AsDouble(left, node), AsDouble(right, node)));

            private static bool AreEqual(DdfConstantValue left, DdfConstantValue right)
            {
                if (IsNumeric(left) && IsNumeric(right))
                    return AsDouble(left, null) == AsDouble(right, null);
                return Equals(left.Value, right.Value);
            }

            private static bool IsNumeric(DdfConstantValue value) =>
                value.Kind == DdfConstantKind.Integer || value.Kind == DdfConstantKind.Floating;

            private static bool AsBoolean(DdfConstantValue value, DdfIrNode node)
            {
                if (value.Kind == DdfConstantKind.Boolean) return (bool)value.Value;
                throw new ConstantFault(node, "Era atteso un valore bool costante.");
            }

            private static double AsDouble(DdfConstantValue value, DdfIrNode node)
            {
                if (value.Kind == DdfConstantKind.Integer) return (int)value.Value;
                if (value.Kind == DdfConstantKind.Floating) return (double)value.Value;
                throw new ConstantFault(node, "Era atteso un valore numerico costante.");
            }

            private static DdfConstantValue Finite(double value, DdfIrNode node)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ConstantFault(node, "Il risultato float di un inizializzatore fixed non è finito.");
                return DdfConstantValue.Floating(value);
            }

            private static int IntegerPower(int value, int exponent)
            {
                int result = 1;
                int factor = value;
                int remaining = exponent;
                while (remaining > 0)
                {
                    if ((remaining & 1) != 0) result = checked(result * factor);
                    remaining >>= 1;
                    if (remaining > 0) factor = checked(factor * factor);
                }
                return result;
            }

            private static string Format(DdfConstantValue value)
            {
                if (value.Kind == DdfConstantKind.Boolean) return (bool)value.Value ? "true" : "false";
                if (value.Kind == DdfConstantKind.Floating) return ((double)value.Value).ToString(CultureInfo.InvariantCulture);
                return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
            }

            private static string Unescape(string text) => text.Replace("\\\"", "\"")
                .Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t")
                .Replace("\\\\", "\\");

            private void Report(DdfIrModule module, DdfIrNode node, string code, string message)
            {
                int start = Math.Min(Math.Max(0, node?.Start ?? 0), Math.Max(0, module.Source.Length - 1));
                int line = 1;
                int column = 1;
                for (int index = 0; index < Math.Min(start, module.Source.Length); index++)
                {
                    if (module.Source[index] == '\n') { line++; column = 1; }
                    else if (module.Source[index] != '\r') column++;
                }
                issues.Add(new DdfConstantEvaluationIssue(module.DocumentPath,
                    new DdfDiagnostic(code, message, start, Math.Max(1, node?.Length ?? 1),
                        line, column, documentPath: module.DocumentPath)));
            }

            private static string Identity(DdfIrModule module) =>
                module.ModuleId + "@" + module.ModuleVersion;
        }

        private sealed class ConstantFault : Exception
        {
            internal ConstantFault(DdfIrNode node, string message) : base(message)
            { Node = node; }
            internal DdfIrNode Node { get; }
        }
    }
}
