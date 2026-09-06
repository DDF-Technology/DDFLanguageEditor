using System;
using System.Collections.Generic;
using System.Globalization;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExecutionOptions
    {
        public string EntryPoint { get; set; }
        public int MaxInstructions { get; set; }
        public Action<string> Output { get; set; }
        public Func<string> Input { get; set; }
        public Func<bool> CancellationRequested { get; set; }
        public DdfDebuggerSession DebuggerSession { get; set; }
        public string SourcePath { get; set; }
    }

    public sealed class DdfRuntimeStackFrame
    {
        internal DdfRuntimeStackFrame(string functionName, int start, int length, int line, int column,
            string documentPath)
        {
            FunctionName = functionName;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            DocumentPath = documentPath;
        }

        public string FunctionName { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public string DocumentPath { get; }

        public override string ToString()
        {
            return "in " + FunctionName + "() — riga " + Line + ", colonna " + Column;
        }
    }

    public sealed class DdfExecutionResult
    {
        internal DdfExecutionResult(object returnValue, int instructions, bool cancelled, bool terminated, IReadOnlyList<DdfDiagnostic> diagnostics, IReadOnlyList<DdfRuntimeStackFrame> stackTrace)
        {
            ReturnValue = returnValue;
            Instructions = instructions;
            WasCancelled = cancelled;
            WasTerminated = terminated;
            Diagnostics = diagnostics;
            StackTrace = stackTrace;
        }

        public object ReturnValue { get; }
        public int Instructions { get; }
        public bool WasCancelled { get; }
        public bool WasTerminated { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DdfRuntimeStackFrame> StackTrace { get; }
        public bool Succeeded => !WasCancelled && Diagnostics.Count == 0;
    }

    public static class DdfVirtualMachine
    {
        public static DdfExecutionResult Execute(string source, DdfIrProgram root,
            DdfRuntimeDefinition runtime, DdfTypeSystemDefinition typeSystem,
            DdfStandardLibraryDefinition standardLibrary,
            DdfExecutionOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (typeSystem == null) throw new ArgumentNullException(nameof(typeSystem));
            if (standardLibrary == null) throw new ArgumentNullException(nameof(standardLibrary));
            options = options ?? new DdfExecutionOptions();
            if (string.IsNullOrWhiteSpace(options.EntryPoint)) options.EntryPoint = runtime.EntryPoint;
            if (options.MaxInstructions <= 0) options.MaxInstructions = runtime.DefaultInstructionLimit;
            return Execute(new[] { new DdfIrModule(options.SourcePath, source, root) }, runtime,
                typeSystem, standardLibrary, options);
        }

        public static DdfExecutionResult Execute(IReadOnlyList<DdfIrModule> modules,
            DdfRuntimeDefinition runtime, DdfTypeSystemDefinition typeSystem,
            DdfStandardLibraryDefinition standardLibrary, DdfExecutionOptions options = null)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            if (modules.Count == 0) throw new ArgumentException("È richiesto almeno un modulo runtime.", nameof(modules));
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (typeSystem == null) throw new ArgumentNullException(nameof(typeSystem));
            if (standardLibrary == null) throw new ArgumentNullException(nameof(standardLibrary));
            options = options ?? new DdfExecutionOptions();
            if (string.IsNullOrWhiteSpace(options.EntryPoint)) options.EntryPoint = runtime.EntryPoint;
            if (options.MaxInstructions <= 0) options.MaxInstructions = runtime.DefaultInstructionLimit;
            return new Runner(modules, runtime, typeSystem, standardLibrary, options).Run();
        }

        private sealed class Runner
        {
            private readonly IReadOnlyList<DdfIrModule> modules;
            private readonly DdfRuntimeDefinition runtime;
            private readonly DdfTypeSystemDefinition typeSystem;
            private readonly DdfStandardLibraryDefinition standardLibrary;
            private readonly DdfExecutionOptions options;
            private readonly Dictionary<string, DdfIrFunction> functions = new Dictionary<string, DdfIrFunction>(StringComparer.Ordinal);
            private readonly Dictionary<DdfIrFunction, DdfIrModule> functionModules = new Dictionary<DdfIrFunction, DdfIrModule>();
            private readonly Dictionary<string, DdfIrStructure> structures = new Dictionary<string, DdfIrStructure>(StringComparer.Ordinal);
            private readonly Dictionary<DdfIrStructure, DdfIrModule> structureModules = new Dictionary<DdfIrStructure, DdfIrModule>();
            private readonly List<Dictionary<string, Cell>> scopes = new List<Dictionary<string, Cell>>();
            private readonly Stack<int> debugFrameScopes = new Stack<int>();
            private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
            private readonly List<DdfRuntimeStackFrame> callStack = new List<DdfRuntimeStackFrame>();
            private int instructions;
            private DdfIrModule activeModule;

            public Runner(IReadOnlyList<DdfIrModule> modules, DdfRuntimeDefinition runtime,
                DdfTypeSystemDefinition typeSystem, DdfStandardLibraryDefinition standardLibrary,
                DdfExecutionOptions options)
            {
                this.modules = modules;
                activeModule = modules[0];
                this.runtime = runtime;
                this.typeSystem = typeSystem;
                this.standardLibrary = standardLibrary;
                this.options = options;
            }

            public DdfExecutionResult Run()
            {
                bool cancelled = false;
                bool terminated = false;
                object returnValue = null;
                IReadOnlyList<DdfRuntimeStackFrame> stackTrace = new DdfRuntimeStackFrame[0];
                try
                {
                    IndexDeclarations();
                    PushScope();
                    foreach (DdfIrModule module in modules)
                    {
                        activeModule = module;
                        foreach (DdfIrMember member in module.Program.Members)
                            if (member is DdfIrGlobal global) ExecuteStatement(global.Statement);
                    }

                    if (!string.IsNullOrWhiteSpace(options.EntryPoint))
                    {
                        if (!functions.TryGetValue(options.EntryPoint, out DdfIrFunction entry))
                            Fail("DDF401", "Funzione di ingresso '" + options.EntryPoint + "' non trovata.", activeModule.Program);
                        returnValue = Invoke(entry, new object[0], null);
                    }
                }
                catch (ReturnSignal signal) { returnValue = signal.Value; }
                catch (BreakSignal) { diagnostics.Add(CreateDiagnostic("DDF403", "'brk' può essere usato soltanto in un ciclo.", activeModule.Program,
                    DdfDiagnosticDomain.Source)); }
                catch (EndSignal) { terminated = true; }
                catch (CancelledSignal) { cancelled = true; }
                catch (RuntimeSignal signal)
                {
                    diagnostics.Add(signal.Diagnostic);
                    stackTrace = signal.DdfStackTrace;
                }
                catch (Exception exception) { diagnostics.Add(CreateDiagnostic("DDF499", "Errore runtime interno: " + exception.Message, activeModule.Program)); }
                finally
                {
                    scopes.Clear();
                }

                return new DdfExecutionResult(returnValue, instructions, cancelled, terminated, diagnostics.AsReadOnly(), stackTrace);
            }

            private void IndexDeclarations()
            {
                foreach (DdfIrModule module in modules)
                {
                    foreach (DdfIrMember member in module.Program.Members)
                    {
                        if (member is DdfIrFunction function && !string.IsNullOrEmpty(function.Name))
                        { functions[function.Name] = function; functionModules[function] = module; }
                        else if (member is DdfIrStructure structure && !string.IsNullOrEmpty(structure.Name))
                        { structures[structure.Name] = structure; structureModules[structure] = module; }
                    }
                }
            }

            private object Invoke(DdfIrFunction function, IList<object> arguments, DdfIrNode invocation)
            {
                DdfIrModule callerModule = activeModule;
                DdfIrModule functionModule = functionModules[function];
                debugFrameScopes.Push(scopes.Count);
                if (invocation == null) activeModule = functionModule;
                callStack.Add(CreateFrame(
                    function.Name,
                    invocation == null ? function.NameStart : invocation.Start,
                    invocation == null ? function.NameLength : invocation.Length));
                activeModule = functionModule;
                try
                {
                    Tick(function);
                    if (arguments.Count != function.Parameters.Count)
                        Fail("DDF402", "La funzione '" + function.Name + "' richiede " + function.Parameters.Count + " argomenti.", function);
                    PushScope();
                    try
                    {
                        for (int index = 0; index < function.Parameters.Count; index++)
                            Declare(function.Parameters[index].Name,
                                ConvertForType(arguments[index], function.Parameters[index].Type, function.Parameters[index]),
                                FormatType(function.Parameters[index].Type), DdfRuntimeVariableKind.Parameter);
                        try { ExecuteBlock(function.Body, false); }
                        catch (ReturnSignal signal) { return signal.Value; }
                        return null;
                    }
                    finally { PopScope(); }
                }
                finally
                {
                    activeModule = callerModule;
                    callStack.RemoveAt(callStack.Count - 1);
                    debugFrameScopes.Pop();
                }
            }

            private void ExecuteStatement(DdfIrStatement statement)
            {
                if (statement == null) return;
                if (!(statement is DdfIrBlock) && options.DebuggerSession != null &&
                    !options.DebuggerSession.BeforeStatement(activeModule.Source, statement, options.CancellationRequested,
                        () => CaptureStateSnapshot(statement), callStack.Count, activeModule.DocumentPath))
                    throw new CancelledSignal();
                Tick(statement);
                if (statement is DdfIrBlock block) ExecuteBlock(block, true);
                else if (statement is DdfIrVariableDeclaration variable)
                {
                    object value = variable.Initializer == null ? CreateDefault(variable.Type) : ConvertForType(Evaluate(variable.Initializer), variable.Type, variable);
                    Declare(variable.Name, value, FormatType(variable.Type), DdfRuntimeVariableKind.Local);
                }
                else if (statement is DdfIrExpressionStatement expression) Evaluate(expression.Expression);
                else if (statement is DdfIrIf conditional)
                {
                    if (AsBoolean(Evaluate(conditional.Condition), conditional.Condition)) ExecuteStatement(conditional.Body);
                    else if (conditional.ElseBody != null) ExecuteStatement(conditional.ElseBody);
                }
                else if (statement is DdfIrWhile loop)
                {
                    while (AsBoolean(Evaluate(loop.Condition), loop.Condition))
                    {
                        try { ExecuteStatement(loop.Body); } catch (BreakSignal) { break; }
                    }
                }
                else if (statement is DdfIrDoWhile doLoop)
                {
                    do { try { ExecuteStatement(doLoop.Body); } catch (BreakSignal) { break; } }
                    while (AsBoolean(Evaluate(doLoop.Condition), doLoop.Condition));
                }
                else if (statement is DdfIrFor forLoop)
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
                else if (statement is DdfIrReturn returned) throw new ReturnSignal(returned.Expression == null ? null : Evaluate(returned.Expression));
                else if (statement is DdfIrBreak) throw new BreakSignal();
                else if (statement is DdfIrEnd) throw new EndSignal();
            }

            private void ExecuteBlock(DdfIrBlock block, bool createScope)
            {
                if (createScope) PushScope();
                try { foreach (DdfIrStatement statement in block.Statements) ExecuteStatement(statement); }
                finally { if (createScope) PopScope(); }
            }

            private object Evaluate(DdfIrExpression expression)
            {
                if (expression == null || expression is DdfIrMissingExpression) return null;
                Tick(expression);
                if (expression is DdfIrLiteral literal) return ParseLiteral(literal.Text);
                if (expression is DdfIrName name) return FindCell(name.Name, name).Value;
                if (expression is DdfIrGroup parenthesized) return Evaluate(parenthesized.Expression);
                if (expression is DdfIrUnary unary)
                {
                    object operand = Evaluate(unary.Operand);
                    if (unary.OperationId == DdfVmExpressionOperations.LogicalNot) return !AsBoolean(operand, unary);
                    if (unary.OperationId == DdfVmExpressionOperations.Negate) return IsFloating(operand) ? (object)(-ToDouble(operand, unary)) : -ToInt(operand, unary);
                    if (unary.OperationId == DdfVmExpressionOperations.Positive) return operand;
                    Fail("DDFE004", "Operazione unaria IR non supportata: '" + unary.OperationId + "'.", unary,
                        DdfDiagnosticDomain.EngineCapability);
                }
                if (expression is DdfIrBinary binary) return EvaluateBinary(binary);
                if (expression is DdfIrPostfix postfix)
                {
                    Cell cell = ResolveCell(postfix.Operand);
                    object old = cell.Value;
                    int delta = postfix.OperationId == DdfVmExpressionOperations.Increment ? 1 :
                        postfix.OperationId == DdfVmExpressionOperations.Decrement ? -1 : 0;
                    if (delta == 0) Fail("DDFE004", "Operazione postfix IR non supportata: '" + postfix.OperationId + "'.", postfix,
                        DdfDiagnosticDomain.EngineCapability);
                    cell.Value = IsFloating(old) ? (object)(ToDouble(old, postfix) + delta) : ToInt(old, postfix) + delta;
                    return old;
                }
                if (expression is DdfIrCall call)
                {
                    var target = call.Target as DdfIrName;
                    if (target != null && functions.TryGetValue(target.Name, out DdfIrFunction function))
                    {
                        var userArguments = new List<object>();
                        foreach (DdfIrExpression argument in call.Arguments) userArguments.Add(Evaluate(argument));
                        return Invoke(function, userArguments, call.Target);
                    }
                    if (target != null && standardLibrary.TryGetFunction(target.Name, out DdfStandardFunction standard))
                    {
                        var standardArguments = new List<object>();
                        foreach (DdfIrExpression argument in call.Arguments) standardArguments.Add(Evaluate(argument));
                        return InvokeStandard(standard, standardArguments, call);
                    }
                    Fail("DDF402", "Funzione non trovata.", call.Target);
                    return null;
                }
                if (expression is DdfIrIndex || expression is DdfIrMemberAccess) return ResolveCell(expression).Value;
                Fail("DDFE001", "Espressione non supportata dal runtime.", expression,
                    DdfDiagnosticDomain.EngineCapability);
                return null;
            }

            private object InvokeStandard(DdfStandardFunction function, IList<object> arguments, DdfIrNode node)
            {
                var call = node as DdfIrCall;
                DdfIrNode invocation = call == null ? node : call.Target;
                callStack.Add(CreateFrame(function.Name, invocation.Start, invocation.Length));
                try
                {
                    if (arguments.Count != function.ParameterTypes.Count)
                        Fail("DDF402", "La funzione standard '" + function.Name + "' richiede " + function.ParameterTypes.Count + " argomenti.", node);
                    if (function.BindingId == DdfHostFunctionBindings.Output)
                    {
                        options.Output?.Invoke(arguments[0] as string ?? FormatValue(arguments[0]));
                        return null;
                    }
                    if (function.BindingId == DdfHostFunctionBindings.Input)
                    {
                        if (options.Input == null) Fail("DDF408", "Input standard non disponibile.", node);
                        return options.Input() ?? string.Empty;
                    }
                    if (function.BindingId == DdfHostFunctionBindings.StringLength) return ((string)arguments[0]).Length;
                    if (function.BindingId == DdfHostFunctionBindings.ToInt)
                    {
                        if (int.TryParse(arguments[0] as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) return integer;
                        Fail("DDF408", "Impossibile convertire il testo in int.", node);
                    }
                    if (function.BindingId == DdfHostFunctionBindings.ToFloat)
                    {
                        if (double.TryParse(arguments[0] as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double floating)) return floating;
                        Fail("DDF408", "Impossibile convertire il testo in float.", node);
                    }
                    Fail("DDFE002", "Funzione standard non implementata: '" + function.Name + "'.", node,
                        DdfDiagnosticDomain.EngineCapability);
                    return null;
                }
                finally { callStack.RemoveAt(callStack.Count - 1); }
            }

            private object EvaluateBinary(DdfIrBinary binary)
            {
                string op = binary.OperationId;
                if (op == DdfVmExpressionOperations.Assign)
                {
                    Cell target = ResolveCell(binary.Left);
                    object value = Evaluate(binary.Right);
                    target.Value = value;
                    return value;
                }
                if (op == DdfVmExpressionOperations.Output)
                {
                    var destination = binary.Right as DdfIrName;
                    if (destination == null || destination.Name != runtime.OutputTarget)
                        Fail("DDFE003", "Destinazione di output non supportata.", binary.Right,
                            DdfDiagnosticDomain.EngineCapability);
                    object value = Evaluate(binary.Left);
                    options.Output?.Invoke(FormatValue(value));
                    return value;
                }
                if (op == DdfVmExpressionOperations.LogicalOr)
                {
                    object leftShort = Evaluate(binary.Left);
                    return AsBoolean(leftShort, binary.Left) || AsBoolean(Evaluate(binary.Right), binary.Right);
                }
                if (op == DdfVmExpressionOperations.LogicalAnd)
                {
                    object leftShort = Evaluate(binary.Left);
                    return AsBoolean(leftShort, binary.Left) && AsBoolean(Evaluate(binary.Right), binary.Right);
                }
                object left = Evaluate(binary.Left);
                object right = Evaluate(binary.Right);
                if (op == DdfVmExpressionOperations.LogicalXor) return AsBoolean(left, binary.Left) ^ AsBoolean(right, binary.Right);
                if (op == DdfVmExpressionOperations.Equal) return EqualsValue(left, right);
                if (op == DdfVmExpressionOperations.NotEqual) return !EqualsValue(left, right);
                if (op == DdfVmExpressionOperations.BetweenExclusive) return ToDouble(left, binary) > ToDouble(right, binary) && ToDouble(left, binary) < ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.OutsideExclusive) return ToDouble(left, binary) < ToDouble(right, binary) || ToDouble(left, binary) > ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.Less) return ToDouble(left, binary) < ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.LessOrEqual) return ToDouble(left, binary) <= ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.Greater) return ToDouble(left, binary) > ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.GreaterOrEqual) return ToDouble(left, binary) >= ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.Add && (left is string || right is string)) return FormatValue(left) + FormatValue(right);
                double a = ToDouble(left, binary), b = ToDouble(right, binary);
                if (op == DdfVmExpressionOperations.Add) return NumericResult(left, right, a + b);
                if (op == DdfVmExpressionOperations.Subtract) return NumericResult(left, right, a - b);
                if (op == DdfVmExpressionOperations.Multiply) return NumericResult(left, right, a * b);
                if (op == DdfVmExpressionOperations.Divide) { if (b == 0) Fail("DDF404", "Divisione per zero.", binary); return NumericResult(left, right, a / b); }
                if (op == DdfVmExpressionOperations.Power) return NumericResult(left, right, Math.Pow(a, b));
                if (op == DdfVmExpressionOperations.Root) { if (b == 0) Fail("DDF404", "Indice della radice uguale a zero.", binary); return Math.Pow(a, 1d / b); }
                Fail("DDFE004", "Operatore runtime non supportato: '" + op + "'.", binary,
                    DdfDiagnosticDomain.EngineCapability);
                return null;
            }

            private Cell ResolveCell(DdfIrExpression expression)
            {
                if (expression is DdfIrName name) return FindCell(name.Name, name);
                if (expression is DdfIrIndex index)
                {
                    object target = Evaluate(index.Target);
                    int offset = ToInt(Evaluate(index.Index), index.Index);
                    var array = target as Cell[];
                    if (array == null) Fail("DDF405", "Il valore non è un array modificabile.", index.Target);
                    if (offset < 0 || offset >= array.Length) Fail("DDF405", "Indice array fuori intervallo: " + offset + ".", index.Index);
                    return array[offset];
                }
                if (expression is DdfIrMemberAccess member)
                {
                    var instance = Evaluate(member.Target) as Dictionary<string, Cell>;
                    Cell field = null;
                    if (instance == null || !instance.TryGetValue(member.Name, out field)) Fail("DDF405", "Membro non trovato: '" + member.Name + "'.", member);
                    return field;
                }
                Fail("DDF405", "La destinazione non è modificabile.", expression);
                return null;
            }

            private object CreateDefault(DdfIrType type)
            {
                if (type.ArrayLengths.Count > 0)
                {
                    int length = type.ArrayLengths[0] == null ? 0 : ToInt(Evaluate(type.ArrayLengths[0]), type.ArrayLengths[0]);
                    if (length < 0) Fail("DDF405", "La lunghezza di un array non può essere negativa.", type);
                    var cells = new Cell[length];
                    var elementType = new DdfIrType(type.Name, new DdfIrExpression[0], type.Start, type.Length);
                    for (int index = 0; index < cells.Length; index++)
                        cells[index] = new Cell(CreateDefault(elementType), FormatType(elementType), DdfRuntimeVariableKind.Element);
                    return cells;
                }
                if (structures.TryGetValue(type.Name, out DdfIrStructure structure))
                {
                    DdfIrModule previousModule = activeModule;
                    activeModule = structureModules[structure];
                    var fields = new Dictionary<string, Cell>(StringComparer.Ordinal);
                    try
                    {
                        foreach (DdfIrVariableDeclaration field in structure.Fields)
                            fields[field.Name] = new Cell(
                                field.Initializer == null ? CreateDefault(field.Type) : Evaluate(field.Initializer),
                                FormatType(field.Type), DdfRuntimeVariableKind.Field);
                        return fields;
                    }
                    finally { activeModule = previousModule; }
                }
                if (typeSystem.TryGetPrimitive(type.Name, out DdfPrimitiveTypeDefinition primitive))
                {
                    if (primitive.Category == DdfPrimitiveTypeCategory.Boolean) return false;
                    if (primitive.Category == DdfPrimitiveTypeCategory.Floating) return 0d;
                    if (primitive.Category == DdfPrimitiveTypeCategory.Character) return '\0';
                    if (primitive.Category == DdfPrimitiveTypeCategory.String) return string.Empty;
                    if (primitive.Category == DdfPrimitiveTypeCategory.Integer) return 0;
                }
                return null;
            }

            private object ConvertForType(object value, DdfIrType type, DdfIrNode node)
            {
                if (value == null) return CreateDefault(type);
                if (type.ArrayLengths.Count > 0 || structures.ContainsKey(type.Name)) return value;
                if (typeSystem.TryGetPrimitive(type.Name, out DdfPrimitiveTypeDefinition primitive))
                {
                    if (primitive.Category == DdfPrimitiveTypeCategory.Floating) return ToDouble(value, node);
                    if (primitive.Category == DdfPrimitiveTypeCategory.Integer) return ToInt(value, node);
                    if (primitive.Category == DdfPrimitiveTypeCategory.Character && value is string text && text.Length == 1) return text[0];
                }
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

            private Cell FindCell(string name, DdfIrNode node)
            {
                for (int index = scopes.Count - 1; index >= 0; index--)
                    if (scopes[index].TryGetValue(name, out Cell cell)) return cell;
                Fail("DDF406", "Variabile non definita: '" + name + "'.", node);
                return null;
            }

            private void Declare(string name, object value, string typeName, DdfRuntimeVariableKind kind)
            {
                if (!string.IsNullOrEmpty(name))
                    scopes[scopes.Count - 1][name] = new Cell(value, typeName, kind);
            }

            private static string FormatType(DdfIrType type) => type == null ? "?" :
                type.Name + string.Concat(System.Linq.Enumerable.Repeat("[]", type.ArrayLengths.Count));

            private void PushScope() => scopes.Add(new Dictionary<string, Cell>(StringComparer.Ordinal));
            private void PopScope() => scopes.RemoveAt(scopes.Count - 1);

            private void Tick(DdfIrNode node)
            {
                if (options.CancellationRequested != null && options.CancellationRequested()) throw new CancelledSignal();
                instructions++;
                if (options.MaxInstructions <= 0 || instructions > options.MaxInstructions)
                    Fail("DDF407", "Limite di " + options.MaxInstructions + " istruzioni superato.", node);
            }

            private bool AsBoolean(object value, DdfIrNode node)
            {
                if (value is bool boolean) return boolean;
                Fail("DDF403", "Era atteso un valore bool.", node);
                return false;
            }

            private double ToDouble(object value, DdfIrNode node)
            {
                if (value is int integer) return integer;
                if (value is double floating) return floating;
                if (value is char character) return character;
                Fail("DDF403", "Era atteso un valore numerico.", node);
                return 0;
            }

            private int ToInt(object value, DdfIrNode node)
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

            private DdfRuntimeStateSnapshot CaptureStateSnapshot(DdfIrStatement currentStatement)
            {
                int budget = 4096;
                var globals = CaptureScopeRange(0, Math.Min(1, scopes.Count), ref budget, true);
                var stack = new List<DdfRuntimeStackFrame>();
                for (int index = callStack.Count - 1; index >= 0; index--)
                {
                    DdfRuntimeStackFrame location = index == callStack.Count - 1
                        ? CreateFrame(callStack[index].FunctionName, currentStatement.Start, currentStatement.Length)
                        : CreateCallerFrame(callStack[index].FunctionName, callStack[index + 1]);
                    stack.Add(location);
                }
                int[] frameStartsInnerFirst = debugFrameScopes.ToArray();
                var frames = new List<DdfRuntimeFrameSnapshot>();
                for (int innerIndex = 0; innerIndex < stack.Count; innerIndex++)
                {
                    int start = frameStartsInnerFirst[innerIndex];
                    int end = innerIndex == 0 ? scopes.Count : frameStartsInnerFirst[innerIndex - 1];
                    frames.Add(new DdfRuntimeFrameSnapshot(stack[innerIndex],
                        CaptureScopeRange(start, end, ref budget, false).AsReadOnly()));
                }
                var variables = new List<DdfRuntimeVariableSnapshot>();
                if (frames.Count > 0) variables.AddRange(frames[0].Variables);
                variables.AddRange(globals);
                return new DdfRuntimeStateSnapshot(variables.AsReadOnly(), stack.AsReadOnly(), frames.AsReadOnly());
            }

            private List<DdfRuntimeVariableSnapshot> CaptureScopeRange(int start, int end,
                ref int budget, bool globals)
            {
                var variables = new List<DdfRuntimeVariableSnapshot>();
                for (int scopeIndex = end - 1; scopeIndex >= start && scopeIndex < scopes.Count; scopeIndex--)
                {
                    int depth = end - 1 - scopeIndex;
                    foreach (KeyValuePair<string, Cell> pair in scopes[scopeIndex])
                        variables.Add(CaptureDebugValue(pair.Key, pair.Value, depth, 0, ref budget,
                            globals ? DdfRuntimeVariableKind.Global : pair.Value.Kind));
                }
                return variables;
            }

            private DdfRuntimeVariableSnapshot CaptureDebugValue(string name, Cell cell, int scope, int level,
                ref int budget, DdfRuntimeVariableKind kind)
            {
                object value = cell.Value;
                string type = value is int ? "int" : value is double ? "float" : value is bool ? "bool" :
                    value is char ? "char" : value is string ? "string" : value is Cell[] ? "array" :
                    value is Dictionary<string, Cell> ? "struct" : "null";
                var result = new DdfRuntimeVariableSnapshot(name,
                    value is Dictionary<string, Cell> ? "{…}" : FormatValue(value), scope, kind)
                    { TypeName = string.IsNullOrEmpty(cell.TypeName) ? type : cell.TypeName };
                if (value is not Cell[] && value is not Dictionary<string, Cell>) return result;
                if (level >= 8 || budget <= 0) { result.IsTruncated = true; return result; }
                var children = new List<DdfRuntimeVariableSnapshot>();
                if (value is Cell[] array)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (budget-- <= 0) { result.IsTruncated = true; break; }
                        children.Add(CaptureDebugValue("[" + i + "]", array[i], scope, level + 1,
                            ref budget, DdfRuntimeVariableKind.Element));
                    }
                }
                else if (value is Dictionary<string, Cell> fields)
                    foreach (var field in fields)
                    {
                        if (budget-- <= 0) { result.IsTruncated = true; break; }
                        children.Add(CaptureDebugValue(field.Key, field.Value, scope, level + 1,
                            ref budget, DdfRuntimeVariableKind.Field));
                    }
                result.Children = children.AsReadOnly();
                return result;
            }

            private void Fail(string code, string message, DdfIrNode node,
                DdfDiagnosticDomain domain = DdfDiagnosticDomain.Runtime)
            {
                throw new RuntimeSignal(CreateDiagnostic(code, message, node, domain), node, CaptureStackTrace());
            }

            private IReadOnlyList<DdfRuntimeStackFrame> CaptureStackTrace()
            {
                var trace = new List<DdfRuntimeStackFrame>(callStack.Count);
                for (int index = callStack.Count - 1; index >= 0; index--) trace.Add(callStack[index]);
                return trace.AsReadOnly();
            }

            private DdfRuntimeStackFrame CreateFrame(string functionName, int start, int length)
            {
                string source = activeModule.Source;
                int safeStart = Math.Min(Math.Max(0, start), Math.Max(0, source.Length - 1));
                int line = 1, column = 1;
                for (int index = 0; index < Math.Min(safeStart, source.Length); index++)
                {
                    if (source[index] == '\n') { line++; column = 1; }
                    else if (source[index] != '\r') column++;
                }
                return new DdfRuntimeStackFrame(functionName, safeStart, Math.Max(1, length), line, column,
                    activeModule.DocumentPath);
            }

            private static DdfRuntimeStackFrame CreateCallerFrame(string functionName, DdfRuntimeStackFrame invocation) =>
                new DdfRuntimeStackFrame(functionName, invocation.Start, invocation.Length, invocation.Line,
                    invocation.Column, invocation.DocumentPath);

            private DdfDiagnostic CreateDiagnostic(string code, string message, DdfIrNode node,
                DdfDiagnosticDomain domain = DdfDiagnosticDomain.Runtime)
            {
                string source = activeModule.Source;
                int start = Math.Min(node?.Start ?? 0, Math.Max(0, source.Length - 1));
                int line = 1, column = 1;
                for (int index = 0; index < Math.Min(start, source.Length); index++) { if (source[index] == '\n') { line++; column = 1; } else if (source[index] != '\r') column++; }
                return new DdfDiagnostic(code, message, start, Math.Max(1, node?.Length ?? 1), line, column,
                    DdfDiagnosticSeverity.Error, null, null, domain, activeModule.DocumentPath);
            }

            private sealed class Cell
            {
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind)
                { Value = value; TypeName = typeName; Kind = kind; }
                public object Value { get; set; }
                public string TypeName { get; }
                public DdfRuntimeVariableKind Kind { get; }
            }
            private sealed class RuntimeSignal : Exception
            {
                public RuntimeSignal(DdfDiagnostic diagnostic, DdfIrNode node,
                    IReadOnlyList<DdfRuntimeStackFrame> stackTrace) : base(diagnostic.Message)
                {
                    Diagnostic = diagnostic;
                    Node = node;
                    DdfStackTrace = stackTrace;
                }
                public DdfDiagnostic Diagnostic { get; }
                public DdfIrNode Node { get; }
                public IReadOnlyList<DdfRuntimeStackFrame> DdfStackTrace { get; }
            }
            private sealed class ReturnSignal : Exception { public ReturnSignal(object value) { Value = value; } public object Value { get; } }
            private sealed class BreakSignal : Exception { }
            private sealed class EndSignal : Exception { }
            private sealed class CancelledSignal : Exception { }
        }
    }
}
