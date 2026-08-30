using System;
using System.Collections.Generic;
using System.Globalization;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExecutionOptions
    {
        public string EntryPoint { get; set; } = DdfRuntimeCatalog.DefaultEntryPoint;
        public int MaxInstructions { get; set; } = DdfRuntimeCatalog.DefaultInstructionLimit;
        public Action<string> Output { get; set; }
        public Func<string> Input { get; set; }
        public Func<bool> CancellationRequested { get; set; }
    }

    public sealed class DdfExecutionResult
    {
        internal DdfExecutionResult(object returnValue, int instructions, bool cancelled, bool terminated, IReadOnlyList<DdfDiagnostic> diagnostics)
        {
            ReturnValue = returnValue;
            Instructions = instructions;
            WasCancelled = cancelled;
            WasTerminated = terminated;
            Diagnostics = diagnostics;
        }

        public object ReturnValue { get; }
        public int Instructions { get; }
        public bool WasCancelled { get; }
        public bool WasTerminated { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public bool Succeeded => !WasCancelled && Diagnostics.Count == 0;
    }

    public static class DdfInterpreter
    {
        public static DdfExecutionResult Execute(string source, DdfExecutionOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DdfParseResult parse = DdfParser.Parse(source);
            if (parse.Diagnostics.Count > 0)
                return new DdfExecutionResult(null, 0, false, false, parse.Diagnostics);
            return Execute(source, parse.Root, options);
        }

        public static DdfExecutionResult Execute(string source, CompilationUnitSyntax root, DdfExecutionOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            return new Runner(source, root, options ?? new DdfExecutionOptions()).Run();
        }

        private sealed class Runner
        {
            private readonly string source;
            private readonly CompilationUnitSyntax root;
            private readonly DdfExecutionOptions options;
            private readonly Dictionary<string, FunctionDeclarationSyntax> functions = new Dictionary<string, FunctionDeclarationSyntax>(StringComparer.Ordinal);
            private readonly Dictionary<string, StructDeclarationSyntax> structures = new Dictionary<string, StructDeclarationSyntax>(StringComparer.Ordinal);
            private readonly List<Dictionary<string, Cell>> scopes = new List<Dictionary<string, Cell>>();
            private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
            private int instructions;

            public Runner(string source, CompilationUnitSyntax root, DdfExecutionOptions options)
            {
                this.source = source;
                this.root = root;
                this.options = options;
            }

            public DdfExecutionResult Run()
            {
                bool cancelled = false;
                bool terminated = false;
                object returnValue = null;
                try
                {
                    IndexDeclarations();
                    PushScope();
                    foreach (MemberSyntax member in root.Members)
                        if (member is GlobalStatementSyntax global) ExecuteStatement(global.Statement);

                    if (!string.IsNullOrWhiteSpace(options.EntryPoint))
                    {
                        if (!functions.TryGetValue(options.EntryPoint, out FunctionDeclarationSyntax entry))
                            Fail("DDF401", "Funzione di ingresso '" + options.EntryPoint + "' non trovata.", root);
                        returnValue = Invoke(entry, new object[0]);
                    }
                }
                catch (ReturnSignal signal) { returnValue = signal.Value; }
                catch (BreakSignal) { diagnostics.Add(CreateDiagnostic("DDF403", "'brk' può essere usato soltanto in un ciclo.", root)); }
                catch (EndSignal) { terminated = true; }
                catch (CancelledSignal) { cancelled = true; }
                catch (RuntimeSignal signal) { diagnostics.Add(CreateDiagnostic(signal.Code, signal.Message, signal.Node)); }
                catch (Exception exception) { diagnostics.Add(CreateDiagnostic("DDF499", "Errore runtime interno: " + exception.Message, root)); }
                finally
                {
                    scopes.Clear();
                }

                return new DdfExecutionResult(returnValue, instructions, cancelled, terminated, diagnostics.AsReadOnly());
            }

            private void IndexDeclarations()
            {
                foreach (MemberSyntax member in root.Members)
                {
                    if (member is FunctionDeclarationSyntax function && !string.IsNullOrEmpty(function.Name)) functions[function.Name] = function;
                    else if (member is StructDeclarationSyntax structure && !string.IsNullOrEmpty(structure.Name)) structures[structure.Name] = structure;
                }
            }

            private object Invoke(FunctionDeclarationSyntax function, IList<object> arguments)
            {
                Tick(function);
                if (arguments.Count != function.Parameters.Count)
                    Fail("DDF402", "La funzione '" + function.Name + "' richiede " + function.Parameters.Count + " argomenti.", function);
                PushScope();
                try
                {
                    for (int index = 0; index < function.Parameters.Count; index++)
                        Declare(function.Parameters[index].Name, ConvertForType(arguments[index], function.Parameters[index].Type, function.Parameters[index]));
                    try { ExecuteBlock(function.Body, false); }
                    catch (ReturnSignal signal) { return signal.Value; }
                    return null;
                }
                finally { PopScope(); }
            }

            private void ExecuteStatement(StatementSyntax statement)
            {
                if (statement == null) return;
                Tick(statement);
                if (statement is BlockStatementSyntax block) ExecuteBlock(block, true);
                else if (statement is VariableDeclarationStatementSyntax variable)
                {
                    object value = variable.Initializer == null ? CreateDefault(variable.Type) : ConvertForType(Evaluate(variable.Initializer), variable.Type, variable);
                    Declare(variable.Name, value);
                }
                else if (statement is ExpressionStatementSyntax expression) Evaluate(expression.Expression);
                else if (statement is IfStatementSyntax conditional)
                {
                    if (AsBoolean(Evaluate(conditional.Condition), conditional.Condition)) ExecuteStatement(conditional.Body);
                }
                else if (statement is WhileStatementSyntax loop)
                {
                    while (AsBoolean(Evaluate(loop.Condition), loop.Condition))
                    {
                        try { ExecuteStatement(loop.Body); } catch (BreakSignal) { break; }
                    }
                }
                else if (statement is DoWhileStatementSyntax doLoop)
                {
                    do { try { ExecuteStatement(doLoop.Body); } catch (BreakSignal) { break; } }
                    while (AsBoolean(Evaluate(doLoop.Condition), doLoop.Condition));
                }
                else if (statement is ForStatementSyntax forLoop)
                {
                    PushScope();
                    try
                    {
                        ExecuteStatement(forLoop.Initializer);
                        while (forLoop.Condition == null || AsBoolean(Evaluate(forLoop.Condition), forLoop.Condition))
                        {
                            try { ExecuteStatement(forLoop.Body); } catch (BreakSignal) { break; }
                            if (forLoop.Increment != null) Evaluate(forLoop.Increment);
                        }
                    }
                    finally { PopScope(); }
                }
                else if (statement is ReturnStatementSyntax returned) throw new ReturnSignal(returned.Expression == null ? null : Evaluate(returned.Expression));
                else if (statement is BreakStatementSyntax) throw new BreakSignal();
                else if (statement is EndStatementSyntax) throw new EndSignal();
            }

            private void ExecuteBlock(BlockStatementSyntax block, bool createScope)
            {
                if (createScope) PushScope();
                try { foreach (StatementSyntax statement in block.Statements) ExecuteStatement(statement); }
                finally { if (createScope) PopScope(); }
            }

            private object Evaluate(ExpressionSyntax expression)
            {
                if (expression == null || expression is MissingExpressionSyntax) return null;
                Tick(expression);
                if (expression is LiteralExpressionSyntax literal) return ParseLiteral(literal.Text);
                if (expression is NameExpressionSyntax name) return FindCell(name.Name, name).Value;
                if (expression is ParenthesizedExpressionSyntax parenthesized) return Evaluate(parenthesized.Expression);
                if (expression is UnaryExpressionSyntax unary)
                {
                    object operand = Evaluate(unary.Operand);
                    if (unary.OperatorText == "!") return !AsBoolean(operand, unary);
                    if (unary.OperatorText == "-") return IsFloating(operand) ? (object)(-ToDouble(operand, unary)) : -ToInt(operand, unary);
                    if (unary.OperatorText == "+") return operand;
                }
                if (expression is BinaryExpressionSyntax binary) return EvaluateBinary(binary);
                if (expression is PostfixExpressionSyntax postfix)
                {
                    Cell cell = ResolveCell(postfix.Operand);
                    object old = cell.Value;
                    cell.Value = IsFloating(old) ? (object)(ToDouble(old, postfix) + (postfix.OperatorText == "++" ? 1 : -1)) : ToInt(old, postfix) + (postfix.OperatorText == "++" ? 1 : -1);
                    return old;
                }
                if (expression is CallExpressionSyntax call)
                {
                    var target = call.Target as NameExpressionSyntax;
                    if (target != null && functions.TryGetValue(target.Name, out FunctionDeclarationSyntax function))
                    {
                        var userArguments = new List<object>();
                        foreach (ExpressionSyntax argument in call.Arguments) userArguments.Add(Evaluate(argument));
                        return Invoke(function, userArguments);
                    }
                    if (target != null && DdfRuntimeCatalog.TryGetStandardFunction(target.Name, out DdfStandardFunction standard))
                    {
                        var standardArguments = new List<object>();
                        foreach (ExpressionSyntax argument in call.Arguments) standardArguments.Add(Evaluate(argument));
                        return InvokeStandard(standard, standardArguments, call);
                    }
                    Fail("DDF402", "Funzione non trovata.", call.Target);
                    return null;
                }
                if (expression is IndexExpressionSyntax || expression is MemberAccessExpressionSyntax) return ResolveCell(expression).Value;
                Fail("DDF403", "Espressione non supportata dal runtime.", expression);
                return null;
            }

            private object InvokeStandard(DdfStandardFunction function, IList<object> arguments, DdfSyntaxNode node)
            {
                if (arguments.Count != function.ParameterTypes.Count)
                    Fail("DDF402", "La funzione standard '" + function.Name + "' richiede " + function.ParameterTypes.Count + " argomenti.", node);
                if (function.Name == "print")
                {
                    options.Output?.Invoke(arguments[0] as string ?? FormatValue(arguments[0]));
                    return null;
                }
                if (function.Name == "readLine")
                {
                    if (options.Input == null) Fail("DDF408", "Input standard non disponibile.", node);
                    return options.Input() ?? string.Empty;
                }
                if (function.Name == "length") return ((string)arguments[0]).Length;
                if (function.Name == "toInt")
                {
                    if (int.TryParse(arguments[0] as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) return integer;
                    Fail("DDF408", "Impossibile convertire il testo in int.", node);
                }
                if (function.Name == "toFloat")
                {
                    if (double.TryParse(arguments[0] as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double floating)) return floating;
                    Fail("DDF408", "Impossibile convertire il testo in float.", node);
                }
                Fail("DDF402", "Funzione standard non implementata: '" + function.Name + "'.", node);
                return null;
            }

            private object EvaluateBinary(BinaryExpressionSyntax binary)
            {
                string op = binary.OperatorText;
                if (op == "<<")
                {
                    Cell target = ResolveCell(binary.Left);
                    object value = Evaluate(binary.Right);
                    target.Value = value;
                    return value;
                }
                if (op == ">>")
                {
                    var destination = binary.Right as NameExpressionSyntax;
                    if (destination == null || destination.Name != DdfRuntimeCatalog.ConsoleOutput)
                        Fail("DDF403", "Destinazione di output non supportata.", binary.Right);
                    object value = Evaluate(binary.Left);
                    options.Output?.Invoke(FormatValue(value));
                    return value;
                }
                if (op == "||")
                {
                    object leftShort = Evaluate(binary.Left);
                    return AsBoolean(leftShort, binary.Left) || AsBoolean(Evaluate(binary.Right), binary.Right);
                }
                if (op == "&")
                {
                    object leftShort = Evaluate(binary.Left);
                    return AsBoolean(leftShort, binary.Left) && AsBoolean(Evaluate(binary.Right), binary.Right);
                }
                object left = Evaluate(binary.Left);
                object right = Evaluate(binary.Right);
                if (op == "|&|") return AsBoolean(left, binary.Left) ^ AsBoolean(right, binary.Right);
                if (op == "==") return EqualsValue(left, right);
                if (op == "!!") return !EqualsValue(left, right);
                if (op == ">><<") return ToDouble(left, binary) > ToDouble(right, binary) && ToDouble(left, binary) < ToDouble(right, binary);
                if (op == "<<>>") return ToDouble(left, binary) < ToDouble(right, binary) || ToDouble(left, binary) > ToDouble(right, binary);
                if (op == "<") return ToDouble(left, binary) < ToDouble(right, binary);
                if (op == "<=") return ToDouble(left, binary) <= ToDouble(right, binary);
                if (op == ">") return ToDouble(left, binary) > ToDouble(right, binary);
                if (op == ">=") return ToDouble(left, binary) >= ToDouble(right, binary);
                if (op == "+" && (left is string || right is string)) return FormatValue(left) + FormatValue(right);
                double a = ToDouble(left, binary), b = ToDouble(right, binary);
                if (op == "+") return NumericResult(left, right, a + b);
                if (op == "-") return NumericResult(left, right, a - b);
                if (op == "*") return NumericResult(left, right, a * b);
                if (op == "/") { if (b == 0) Fail("DDF404", "Divisione per zero.", binary); return NumericResult(left, right, a / b); }
                if (op == "^") return NumericResult(left, right, Math.Pow(a, b));
                if (op == "^/") { if (b == 0) Fail("DDF404", "Indice della radice uguale a zero.", binary); return Math.Pow(a, 1d / b); }
                Fail("DDF403", "Operatore runtime non supportato: '" + op + "'.", binary);
                return null;
            }

            private Cell ResolveCell(ExpressionSyntax expression)
            {
                if (expression is NameExpressionSyntax name) return FindCell(name.Name, name);
                if (expression is IndexExpressionSyntax index)
                {
                    object target = Evaluate(index.Target);
                    int offset = ToInt(Evaluate(index.Index), index.Index);
                    var array = target as Cell[];
                    if (array == null) Fail("DDF405", "Il valore non è un array modificabile.", index.Target);
                    if (offset < 0 || offset >= array.Length) Fail("DDF405", "Indice array fuori intervallo: " + offset + ".", index.Index);
                    return array[offset];
                }
                if (expression is MemberAccessExpressionSyntax member)
                {
                    var instance = Evaluate(member.Target) as Dictionary<string, Cell>;
                    Cell field = null;
                    if (instance == null || !instance.TryGetValue(member.Name, out field)) Fail("DDF405", "Membro non trovato: '" + member.Name + "'.", member);
                    return field;
                }
                Fail("DDF405", "La destinazione non è modificabile.", expression);
                return null;
            }

            private object CreateDefault(TypeReferenceSyntax type)
            {
                if (type.ArrayLengths.Count > 0)
                {
                    int length = type.ArrayLengths[0] == null ? 0 : ToInt(Evaluate(type.ArrayLengths[0]), type.ArrayLengths[0]);
                    if (length < 0) Fail("DDF405", "La lunghezza di un array non può essere negativa.", type);
                    var cells = new Cell[length];
                    var elementType = new TypeReferenceSyntax(type.Name, new ExpressionSyntax[0], type.Start, type.Length);
                    for (int index = 0; index < cells.Length; index++) cells[index] = new Cell(CreateDefault(elementType));
                    return cells;
                }
                if (structures.TryGetValue(type.Name, out StructDeclarationSyntax structure))
                {
                    var fields = new Dictionary<string, Cell>(StringComparer.Ordinal);
                    foreach (VariableDeclarationStatementSyntax field in structure.Fields) fields[field.Name] = new Cell(field.Initializer == null ? CreateDefault(field.Type) : Evaluate(field.Initializer));
                    return fields;
                }
                if (type.Name == "bool") return false;
                if (type.Name == "float") return 0d;
                if (type.Name == "char") return '\0';
                if (type.Name == "string") return string.Empty;
                if (type.Name == "int") return 0;
                return null;
            }

            private object ConvertForType(object value, TypeReferenceSyntax type, DdfSyntaxNode node)
            {
                if (value == null) return CreateDefault(type);
                if (type.ArrayLengths.Count > 0 || structures.ContainsKey(type.Name)) return value;
                if (type.Name == "float") return ToDouble(value, node);
                if (type.Name == "int") return ToInt(value, node);
                if (type.Name == "char" && value is string text && text.Length == 1) return text[0];
                return value;
            }

            private static object ParseLiteral(string text)
            {
                if (text == "true" || text == "True") return true;
                if (text == "false" || text == "False") return false;
                if (text.StartsWith("\"", StringComparison.Ordinal)) return Unescape(text.Substring(1, Math.Max(0, text.Length - 2)));
                if (text.IndexOf('.') >= 0) return double.Parse(text, CultureInfo.InvariantCulture);
                return int.Parse(text, CultureInfo.InvariantCulture);
            }

            private static string Unescape(string text)
            {
                return text.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\\", "\\");
            }

            private Cell FindCell(string name, DdfSyntaxNode node)
            {
                for (int index = scopes.Count - 1; index >= 0; index--)
                    if (scopes[index].TryGetValue(name, out Cell cell)) return cell;
                Fail("DDF406", "Variabile non definita: '" + name + "'.", node);
                return null;
            }

            private void Declare(string name, object value)
            {
                if (!string.IsNullOrEmpty(name)) scopes[scopes.Count - 1][name] = new Cell(value);
            }

            private void PushScope() => scopes.Add(new Dictionary<string, Cell>(StringComparer.Ordinal));
            private void PopScope() => scopes.RemoveAt(scopes.Count - 1);

            private void Tick(DdfSyntaxNode node)
            {
                if (options.CancellationRequested != null && options.CancellationRequested()) throw new CancelledSignal();
                instructions++;
                if (options.MaxInstructions <= 0 || instructions > options.MaxInstructions)
                    Fail("DDF407", "Limite di " + options.MaxInstructions + " istruzioni superato.", node);
            }

            private bool AsBoolean(object value, DdfSyntaxNode node)
            {
                if (value is bool boolean) return boolean;
                Fail("DDF403", "Era atteso un valore bool.", node);
                return false;
            }

            private double ToDouble(object value, DdfSyntaxNode node)
            {
                if (value is int integer) return integer;
                if (value is double floating) return floating;
                if (value is char character) return character;
                Fail("DDF403", "Era atteso un valore numerico.", node);
                return 0;
            }

            private int ToInt(object value, DdfSyntaxNode node)
            {
                if (value is int integer) return integer;
                if (value is char character) return character;
                if (value is double floating && floating >= int.MinValue && floating <= int.MaxValue) return (int)floating;
                Fail("DDF403", "Era atteso un valore int.", node);
                return 0;
            }

            private static bool IsFloating(object value) => value is double;
            private static object NumericResult(object left, object right, double result) => IsFloating(left) || IsFloating(right) ? (object)result : (int)result;
            private static bool EqualsValue(object left, object right) => left is int || left is double || left is char ? Convert.ToDouble(left, CultureInfo.InvariantCulture) == Convert.ToDouble(right, CultureInfo.InvariantCulture) : Equals(left, right);
            private static string FormatValue(object value)
            {
                if (value == null) return "null";
                if (value is bool boolean) return boolean ? "true" : "false";
                if (value is double floating) return floating.ToString(CultureInfo.InvariantCulture);
                if (value is Cell[] array) return "[" + array.Length + "]";
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            private void Fail(string code, string message, DdfSyntaxNode node) { throw new RuntimeSignal(code, message, node); }

            private DdfDiagnostic CreateDiagnostic(string code, string message, DdfSyntaxNode node)
            {
                int start = Math.Min(node?.Start ?? 0, Math.Max(0, source.Length - 1));
                int line = 1, column = 1;
                for (int index = 0; index < Math.Min(start, source.Length); index++) { if (source[index] == '\n') { line++; column = 1; } else if (source[index] != '\r') column++; }
                return new DdfDiagnostic(code, message, start, Math.Max(1, node?.Length ?? 1), line, column);
            }

            private sealed class Cell { public Cell(object value) { Value = value; } public object Value { get; set; } }
            private sealed class RuntimeSignal : Exception { public RuntimeSignal(string code, string message, DdfSyntaxNode node) : base(message) { Code = code; Node = node; } public string Code { get; } public DdfSyntaxNode Node { get; } }
            private sealed class ReturnSignal : Exception { public ReturnSignal(object value) { Value = value; } public object Value { get; } }
            private sealed class BreakSignal : Exception { }
            private sealed class EndSignal : Exception { }
            private sealed class CancelledSignal : Exception { }
        }
    }
}
