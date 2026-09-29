using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public DdfHostCatalogSnapshot HostCatalog { get; set; }
        public DdfEditorExecutionMode Mode { get; set; } = DdfEditorExecutionMode.HostEffects;
        public CancellationToken CancellationToken { get; set; }
        public DateTimeOffset ExecutionStartedAt { get; set; }
        public DateTimeOffset? ExecutionDeadline { get; set; }
        public TimeSpan HostCallTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxHostCalls { get; set; } = 1024;
        public int MaxHostResources { get; set; } = 128;
        public IReadOnlyDictionary<string, object> EntryArguments { get; set; } =
            new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.Ordinal));
        public IReadOnlyDictionary<string, string> DatabaseTransactions { get; set; } =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal));
        public Guid ExecutionId { get; set; }
        public string HostEffectTransactionId { get; set; }
        internal DdfHostEffectTracker HostEffectTracker { get; set; }
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
        internal DdfExecutionResult(object returnValue, int instructions, bool cancelled,
            bool terminated, IReadOnlyList<DdfDiagnostic> diagnostics,
            IReadOnlyList<DdfRuntimeStackFrame> stackTrace,
            IReadOnlyList<DdfHostValueResult> hostValues = null,
            string returnTypeName = null)
        {
            ReturnValue = returnValue;
            Instructions = instructions;
            WasCancelled = cancelled;
            WasTerminated = terminated;
            Diagnostics = diagnostics;
            StackTrace = stackTrace;
            HostValues = hostValues ?? Array.Empty<DdfHostValueResult>();
            ReturnTypeName = returnTypeName ?? string.Empty;
        }

        public object ReturnValue { get; }
        public int Instructions { get; }
        public bool WasCancelled { get; }
        public bool WasTerminated { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DdfRuntimeStackFrame> StackTrace { get; }
        public IReadOnlyList<DdfHostValueResult> HostValues { get; }
        public string ReturnTypeName { get; }
        public bool Succeeded => !WasCancelled && Diagnostics.Count == 0;
    }

    /// <summary>
    /// Immutable host-facing snapshot of one value exposed to a completed run.
    /// Values are detached from VM cells and can be safely retained by the host.
    /// </summary>
    public sealed class DdfHostValueResult
    {
        internal DdfHostValueResult(string name, string typeName, object initialValue,
            object value, DdfHostValueAccess access, DdfHostObjectLifetime lifetime,
            bool wasWritten)
        {
            Name = name ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            InitialValue = initialValue;
            Value = value;
            Access = access;
            Lifetime = lifetime;
            WasWritten = wasWritten;
        }

        public string Name { get; }
        public string TypeName { get; }
        public object InitialValue { get; }
        public object Value { get; }
        public DdfHostValueAccess Access { get; }
        public DdfHostObjectLifetime Lifetime { get; }
        public bool WasWritten { get; }
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
            private readonly Dictionary<string, Dictionary<string, DdfIrFunction>> functionsByModule =
                new Dictionary<string, Dictionary<string, DdfIrFunction>>(StringComparer.Ordinal);
            private readonly Dictionary<DdfIrFunction, DdfIrModule> functionModules = new Dictionary<DdfIrFunction, DdfIrModule>();
            private readonly Dictionary<string, Dictionary<string, DdfIrStructure>> structuresByModule =
                new Dictionary<string, Dictionary<string, DdfIrStructure>>(StringComparer.Ordinal);
            private readonly Dictionary<DdfIrStructure, DdfIrModule> structureModules = new Dictionary<DdfIrStructure, DdfIrModule>();
            private readonly Dictionary<string, DdfIrModule> moduleDescriptors =
                new Dictionary<string, DdfIrModule>(StringComparer.Ordinal);
            private readonly Dictionary<string, Dictionary<string, Cell>> globalsByModule =
                new Dictionary<string, Dictionary<string, Cell>>(StringComparer.Ordinal);
            private readonly List<Dictionary<string, Cell>> scopes = new List<Dictionary<string, Cell>>();
            private readonly Stack<int> debugFrameScopes = new Stack<int>();
            private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
            private readonly List<DdfRuntimeStackFrame> callStack = new List<DdfRuntimeStackFrame>();
            private readonly List<object> hostResources = new List<object>();
            private readonly List<HostBinding> hostBindings = new List<HostBinding>();
            private readonly object hostResourcesSync = new object();
            private readonly Guid executionId;
            private int instructions;
            private int hostCalls;
            private DdfIrModule activeModule;
            private bool declaringGlobal;

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
                executionId = options.ExecutionId == Guid.Empty
                    ? Guid.NewGuid() : options.ExecutionId;
            }

            public DdfExecutionResult Run()
            {
                if (options.Mode == DdfEditorExecutionMode.AnalysisOnly)
                    return new DdfExecutionResult(null, 0, false, false,
                        Array.Empty<DdfDiagnostic>(), Array.Empty<DdfRuntimeStackFrame>());
                bool cancelled = false;
                bool terminated = false;
                object returnValue = null;
                string returnTypeName = string.Empty;
                IReadOnlyList<DdfHostValueResult> hostValues =
                    Array.Empty<DdfHostValueResult>();
                IReadOnlyList<DdfRuntimeStackFrame> stackTrace = new DdfRuntimeStackFrame[0];
                try
                {
                    IndexDeclarations();
                    PushScope();
                    RegisterHostObjects();
                    foreach (DdfIrModule module in modules)
                    {
                        activeModule = module;
                        foreach (DdfIrMember member in module.Program.Members)
                            if (member is DdfIrGlobal global)
                            {
                                declaringGlobal = true;
                                try { ExecuteStatement(global.Statement); }
                                finally { declaringGlobal = false; }
                            }
                    }

                    if (!string.IsNullOrWhiteSpace(options.EntryPoint))
                    {
                        if (!TryResolveFunction(activeModule, options.EntryPoint, false,
                            out DdfIrFunction entry))
                            Fail("DDF401", "Funzione di ingresso '" + options.EntryPoint + "' non trovata.", activeModule.Program);
                        returnTypeName = FormatType(entry.ReturnType);
                        returnValue = Invoke(entry, BindEntryArguments(entry), null);
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
                    returnValue = FreezeHostValue(ExportHostValue(returnValue));
                    hostValues = CaptureHostValues();
                    ReleaseHostResources();
                    scopes.Clear();
                    globalsByModule.Clear();
                }

                return new DdfExecutionResult(returnValue, instructions, cancelled,
                    terminated, diagnostics.AsReadOnly(), stackTrace, hostValues,
                    returnTypeName);
            }

            private IList<object> BindEntryArguments(DdfIrFunction entry)
            {
                IReadOnlyDictionary<string, object> supplied = options.EntryArguments ??
                    new ReadOnlyDictionary<string, object>(
                        new Dictionary<string, object>(StringComparer.Ordinal));
                string unexpected = supplied.Keys.FirstOrDefault(name =>
                    !entry.Parameters.Any(parameter => parameter.Name == name));
                if (unexpected != null)
                    Fail("DDF416", "Argomento non previsto per la procedura '" +
                        entry.Name + "': '" + unexpected + "'.", entry);

                var result = new List<object>(entry.Parameters.Count);
                foreach (DdfIrParameter parameter in entry.Parameters)
                {
                    if (!supplied.TryGetValue(parameter.Name, out object hostValue))
                        Fail("DDF416", "Argomento obbligatorio mancante per la procedura '" +
                            entry.Name + "': '" + parameter.Name + "'.", parameter);
                    try
                    {
                        result.Add(ImportHostValue(hostValue, FormatType(parameter.Type),
                            parameter, true, parameter.Name));
                    }
                    catch (RuntimeSignal) { throw; }
                    catch (Exception exception)
                    {
                        Fail("DDF416", "Argomento '" + parameter.Name +
                            "' non compatibile con " + FormatType(parameter.Type) +
                            ": " + exception.Message, parameter);
                    }
                }
                return result;
            }

            private void IndexDeclarations()
            {
                foreach (DdfIrModule module in modules)
                {
                    string moduleKey = ModuleKey(module);
                    moduleDescriptors[moduleKey] = module;
                    if (!functionsByModule.TryGetValue(moduleKey, out Dictionary<string, DdfIrFunction> functions))
                    {
                        functions = new Dictionary<string, DdfIrFunction>(StringComparer.Ordinal);
                        functionsByModule.Add(moduleKey, functions);
                    }
                    if (!structuresByModule.TryGetValue(moduleKey, out Dictionary<string, DdfIrStructure> structures))
                    {
                        structures = new Dictionary<string, DdfIrStructure>(StringComparer.Ordinal);
                        structuresByModule.Add(moduleKey, structures);
                    }
                    if (!globalsByModule.ContainsKey(moduleKey))
                        globalsByModule.Add(moduleKey, new Dictionary<string, Cell>(StringComparer.Ordinal));
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
                        catch (ReturnSignal signal)
                        {
                            return ConvertForType(signal.Value, function.ReturnType,
                                signal.Node ?? (DdfIrNode)function);
                        }
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
                    object value = variable.Initializer == null ? CreateDefault(variable.Type) :
                        ConvertForType(Evaluate(variable.Initializer), variable.Type, variable,
                            variable.Initializer is DdfIrLiteral);
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
                else if (statement is DdfIrReturn returned) throw new ReturnSignal(
                    returned.Expression == null ? null : Evaluate(returned.Expression), returned);
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
                if (expression is DdfIrLiteral literal) return ParseLiteral(literal);
                if (expression is DdfIrName name) return ReadCell(FindCell(name.Name, name), name);
                if (expression is DdfIrGroup parenthesized) return Evaluate(parenthesized.Expression);
                if (expression is DdfIrUnary unary)
                {
                    object operand = Evaluate(unary.Operand);
                    if (unary.OperationId == DdfVmExpressionOperations.LogicalNot) return !AsBoolean(operand, unary);
                    if (unary.OperationId == DdfVmExpressionOperations.Negate)
                    {
                        if (IsFloating(operand)) return EnsureFinite(-ToDouble(operand, unary), unary);
                        try { return checked(-ToInt(operand, unary)); }
                        catch (OverflowException) { FailIntegerOverflow(unary); }
                    }
                    if (unary.OperationId == DdfVmExpressionOperations.Positive) return operand;
                    Fail("DDFE004", "Operazione unaria IR non supportata: '" + unary.OperationId + "'.", unary,
                        DdfDiagnosticDomain.EngineCapability);
                }
                if (expression is DdfIrBinary binary) return EvaluateBinary(binary);
                if (expression is DdfIrPostfix postfix)
                {
                    Cell cell = ResolveCell(postfix.Operand);
                    object old = ReadCell(cell, postfix.Operand);
                    int delta = postfix.OperationId == DdfVmExpressionOperations.Increment ? 1 :
                        postfix.OperationId == DdfVmExpressionOperations.Decrement ? -1 : 0;
                    if (delta == 0) Fail("DDFE004", "Operazione postfix IR non supportata: '" + postfix.OperationId + "'.", postfix,
                        DdfDiagnosticDomain.EngineCapability);
                    object updated;
                    if (IsFloating(old)) updated = EnsureFinite(ToDouble(old, postfix) + delta, postfix);
                    else
                    {
                        try { updated = checked(ToInt(old, postfix) + delta); }
                        catch (OverflowException) { FailIntegerOverflow(postfix); return null; }
                    }
                    WriteCell(cell, updated, postfix.Operand);
                    return old;
                }
                if (expression is DdfIrCall call)
                {
                    var target = call.Target as DdfIrName;
                    if (target != null && TryResolveFunction(activeModule, target.Name, true,
                        out DdfIrFunction function))
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
                    if (target != null && options.HostCatalog?.TryGetFunction(
                        target.Name, out DdfHostFunctionDefinition hostFunction) == true)
                    {
                        var hostArguments = new List<object>();
                        foreach (DdfIrExpression argument in call.Arguments)
                            hostArguments.Add(Evaluate(argument));
                        return InvokeHost(hostFunction, hostArguments, call);
                    }
                    Fail("DDF402", "Funzione non trovata.", call.Target);
                    return null;
                }
                if (expression is DdfIrIndex || expression is DdfIrMemberAccess)
                    return ReadCell(ResolveCell(expression), expression);
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
                        if (options.Input == null) Fail(StandardError(function), "Input standard non disponibile.", node);
                        return options.Input() ?? string.Empty;
                    }
                    if (function.BindingId == DdfHostFunctionBindings.StringLength) return ((string)arguments[0]).Length;
                    if (function.BindingId == DdfHostFunctionBindings.ToInt)
                    {
                        if (int.TryParse(arguments[0] as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) return integer;
                        Fail(StandardError(function), "Impossibile convertire il testo in int.", node);
                    }
                    if (function.BindingId == DdfHostFunctionBindings.ToFloat)
                    {
                        if (double.TryParse(arguments[0] as string, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double floating) &&
                            !double.IsNaN(floating) && !double.IsInfinity(floating)) return floating;
                        Fail(StandardError(function), "Impossibile convertire il testo in float.", node);
                    }
                    Fail("DDFE002", "Funzione standard non implementata: '" + function.Name + "'.", node,
                        DdfDiagnosticDomain.EngineCapability);
                    return null;
                }
                finally { callStack.RemoveAt(callStack.Count - 1); }
            }

            private static string StandardError(DdfStandardFunction function) =>
                function.ErrorCodes.FirstOrDefault() ?? "DDF408";

            private object InvokeHost(DdfHostFunctionDefinition function,
                IReadOnlyList<object> arguments, DdfIrNode node)
            {
                callStack.Add(CreateFrame(function.Name, node.Start, node.Length));
                object effectToken = null;
                try
                {
                    if (arguments.Count != function.Parameters.Count)
                        Fail("DDF402", "La funzione host '" + function.Name +
                            "' richiede " + function.Parameters.Count + " argomenti.", node);
                    if (!options.HostCatalog.HasPermissions(function.RequiredPermissions))
                        Fail("DDF409", "Permessi host mancanti per la funzione '" +
                            function.Name + "': " + string.Join(", ",
                                function.RequiredPermissions.Where(permission =>
                                    !options.HostCatalog.IsGranted(permission))) + ".", node);
                    try
                    {
                        hostCalls++;
                        if (options.MaxHostCalls <= 0 || hostCalls > options.MaxHostCalls)
                            Fail("DDF413", "Limite di " + options.MaxHostCalls +
                                " chiamate host superato.", node);
                        var convertedArguments = new List<object>();
                        for (int index = 0; index < arguments.Count; index++)
                            convertedArguments.Add(function.Parameters[index].ValueAdapter.FromScriptValue(
                                ExportHostValue(arguments[index])));
                        Func<IReadOnlyList<object>, object> operation = function.Invoke;
                        Func<DdfHostInvocationContext, IReadOnlyList<object>,
                            ValueTask<object>> operationAsync = function.InvokeAsync;
                        if (options.Mode == DdfEditorExecutionMode.Simulation &&
                            function.Effect == DdfHostFunctionEffect.HostMutation)
                        {
                            if (function.Simulate == null && function.SimulateAsync == null)
                                Fail("DDF412", "La funzione host '" + function.Name +
                                    "' non dispone di una simulazione registrata.", node);
                            operation = function.Simulate;
                            operationAsync = function.SimulateAsync;
                        }
                        if (function.Effect == DdfHostFunctionEffect.HostMutation)
                            effectToken = options.HostEffectTracker?.Begin(function.Name,
                                DdfHostEffectKind.Function,
                                options.Mode == DdfEditorExecutionMode.Simulation);
                        object hostResult = InvokeHostOperation(operation, operationAsync,
                            convertedArguments.AsReadOnly(), function.Name, node);
                        options.HostEffectTracker?.Applied(effectToken);
                        object scriptResult = function.ReturnValueAdapter.ToScriptValue(hostResult);
                        return ImportHostValue(scriptResult, function.ReturnType, node, true);
                    }
                    catch (RuntimeSignal exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        throw;
                    }
                    catch (HostResourceLimitException exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        Fail("DDF413", exception.Message, node);
                        return null;
                    }
                    catch (Exception exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        Fail("DDF410", "La funzione host '" + function.Name +
                            "' ha restituito un errore: " + exception.Message, node);
                        return null;
                    }
                }
                finally { callStack.RemoveAt(callStack.Count - 1); }
            }

            private object InvokeHostOperation(
                Func<IReadOnlyList<object>, object> operation,
                Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> operationAsync,
                IReadOnlyList<object> arguments, string functionName, DdfIrNode node)
            {
                using var callCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    options.CancellationToken);
                if (options.HostCallTimeout > TimeSpan.Zero)
                    callCancellation.CancelAfter(options.HostCallTimeout);
                DateTimeOffset startedAt = options.ExecutionStartedAt == default
                    ? DateTimeOffset.UtcNow : options.ExecutionStartedAt;
                var context = new DdfHostInvocationContext(executionId, options.Mode,
                    callCancellation.Token, startedAt, options.ExecutionDeadline,
                    RegisterHostResource, options.DatabaseTransactions,
                    options.HostEffectTransactionId);
                Task<object> task;
                if (operationAsync != null)
                    task = operationAsync(context, arguments).AsTask();
                else
                    task = Task.Run(() => operation(arguments), callCancellation.Token);
                try
                {
                    return task.WaitAsync(callCancellation.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    if (options.CancellationToken.IsCancellationRequested ||
                        options.CancellationRequested?.Invoke() == true)
                        throw new CancelledSignal();
                    Fail("DDF414", "Timeout della funzione host '" + functionName +
                        "' dopo " + options.HostCallTimeout.TotalMilliseconds.ToString(
                            CultureInfo.InvariantCulture) + " ms.", node);
                    return null;
                }
            }

            private bool hostResourcesClosed;

            private void RegisterHostResource(object resource)
            {
                lock (hostResourcesSync)
                {
                    if (hostResourcesClosed)
                    {
                        DisposeHostResource(resource);
                        throw new ObjectDisposedException(nameof(DdfHostInvocationContext),
                            "La sessione host è già terminata.");
                    }
                    if (hostResources.Count >= options.MaxHostResources)
                    {
                        DisposeHostResource(resource);
                        throw new HostResourceLimitException("Limite di " +
                            options.MaxHostResources + " risorse host superato.");
                    }
                    if (!hostResources.Any(item => ReferenceEquals(item, resource)))
                        hostResources.Add(resource);
                }
            }

            private void ReleaseHostResources()
            {
                List<object> resources;
                lock (hostResourcesSync)
                {
                    hostResourcesClosed = true;
                    resources = hostResources.ToList();
                    hostResources.Clear();
                }
                resources.Reverse();
                foreach (object resource in resources)
                {
                    try { DisposeHostResource(resource); }
                    catch (Exception exception)
                    {
                        diagnostics.Add(CreateDiagnostic("DDF415",
                            "Errore durante il rilascio di una risorsa host: " +
                            exception.Message, activeModule.Program));
                    }
                }
            }

            private static void DisposeHostResource(object resource)
            {
                if (resource is IAsyncDisposable asyncResource)
                    asyncResource.DisposeAsync().AsTask().GetAwaiter().GetResult();
                else if (resource is IDisposable disposable) disposable.Dispose();
            }

            private void RegisterHostObjects()
            {
                if (options.HostCatalog == null) return;
                foreach (DdfHostObjectDefinition value in options.HostCatalog.Objects)
                {
                    if (!options.HostCatalog.HasPermissions(value.RequiredPermissions))
                        continue;
                    bool simulation = options.Mode == DdfEditorExecutionMode.Simulation;
                    Action<object> writer = null;
                    if (value.CanWrite)
                    {
                        writer = scriptValue =>
                        {
                            object effectToken = options.HostEffectTracker?.Begin(
                                value.Name, DdfHostEffectKind.ObjectWrite, simulation);
                            if (simulation) return;
                            try
                            {
                                value.WriteValue(value.ValueAdapter.FromScriptValue(
                                    ExportHostValue(scriptValue)));
                                options.HostEffectTracker?.Applied(effectToken);
                            }
                            catch (Exception exception)
                            {
                                options.HostEffectTracker?.Failed(effectToken, exception);
                                throw;
                            }
                        };
                    }
                    if (value.Lifetime == DdfHostObjectLifetime.Live && !simulation)
                    {
                        var binding = new HostBinding(value);
                        Func<object> reader = () =>
                        {
                            object hostValue = value.ReadValue();
                            binding.CaptureInitial(hostValue);
                            return ImportHostValue(value.ValueAdapter.ToScriptValue(hostValue),
                                value.TypeName, activeModule.Program, value.CanWrite,
                                value.Name, writer);
                        };
                        binding.Cell = DeclareHost(value, null, reader, writer);
                        hostBindings.Add(binding);
                    }
                    else
                    {
                        try
                        {
                            object hostValue = value.ReadValue();
                            object scriptValue = ImportHostValue(
                                value.ValueAdapter.ToScriptValue(hostValue), value.TypeName,
                                activeModule.Program, value.CanWrite, value.Name, writer);
                            var binding = new HostBinding(value);
                            binding.CaptureInitial(hostValue);
                            binding.Cell = DeclareHost(value, scriptValue, null, writer);
                            hostBindings.Add(binding);
                        }
                        catch (Exception exception)
                        {
                            Fail("DDF410", "Impossibile leggere l'oggetto host '" +
                                value.Name + "': " + exception.Message, activeModule.Program);
                            return;
                        }
                    }
                }
            }

            private IReadOnlyList<DdfHostValueResult> CaptureHostValues()
            {
                var result = new List<DdfHostValueResult>();
                foreach (HostBinding binding in hostBindings)
                {
                    object finalValue = null;
                    try
                    {
                        object scriptValue = binding.Cell.WasWritten ||
                            binding.Definition.Lifetime == DdfHostObjectLifetime.ExecutionSnapshot
                            ? binding.Cell.SnapshotValue
                            : binding.Cell.Read();
                        finalValue = FreezeHostValue(binding.Definition.ValueAdapter
                            .FromScriptValue(ExportHostValue(scriptValue)));
                    }
                    catch (Exception exception)
                    {
                        diagnostics.Add(CreateDiagnostic("DDF410",
                            "Impossibile acquisire il risultato host '" +
                            binding.Definition.Name + "': " + exception.Message,
                            activeModule.Program));
                    }
                    result.Add(new DdfHostValueResult(binding.Definition.Name,
                        binding.Definition.TypeName, binding.InitialValue, finalValue,
                        binding.Definition.Access, binding.Definition.Lifetime,
                        binding.Cell.WasWritten));
                }
                return result.AsReadOnly();
            }

            private object EvaluateBinary(DdfIrBinary binary)
            {
                string op = binary.OperationId;
                if (op == DdfVmExpressionOperations.Assign)
                {
                    Cell target = ResolveCell(binary.Left);
                    object value = ConvertForTypeName(Evaluate(binary.Right), target.TypeName,
                        binary.Right);
                    WriteCell(target, value, binary.Left);
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
                if (op == DdfVmExpressionOperations.Add ||
                    op == DdfVmExpressionOperations.Subtract ||
                    op == DdfVmExpressionOperations.Multiply ||
                    op == DdfVmExpressionOperations.Divide ||
                    op == DdfVmExpressionOperations.Power ||
                    op == DdfVmExpressionOperations.Root)
                    return EvaluateNumericBinary(op, left, right, binary);
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
                if (TryResolveStructure(activeModule, type.Name, true, out DdfIrStructure structure))
                {
                    DdfIrModule previousModule = activeModule;
                    activeModule = structureModules[structure];
                    var fields = new Dictionary<string, Cell>(StringComparer.Ordinal);
                    try
                    {
                        foreach (DdfIrVariableDeclaration field in structure.Fields)
                            fields[field.Name] = new Cell(
                                field.Initializer == null ? CreateDefault(field.Type) :
                                    ConvertForType(Evaluate(field.Initializer), field.Type, field,
                                        field.Initializer is DdfIrLiteral),
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

            private object ConvertForType(object value, DdfIrType type, DdfIrNode node,
                bool allowCharacterLiteral = false)
            {
                if (value == null) return CreateDefault(type);
                if (type.ArrayLengths.Count > 0 ||
                    TryResolveStructure(activeModule, type.Name, true, out _)) return value;
                return ConvertForTypeName(value, type.Name, node, allowCharacterLiteral);
            }

            private object ConvertForTypeName(object value, string typeName, DdfIrNode node,
                bool allowCharacterLiteral = false)
            {
                if (value == null || string.IsNullOrEmpty(typeName) ||
                    typeName.EndsWith("[]", StringComparison.Ordinal) ||
                    !typeSystem.TryGetPrimitive(typeName, out DdfPrimitiveTypeDefinition primitive))
                    return value;
                if (primitive.Category == DdfPrimitiveTypeCategory.Floating)
                    return EnsureFinite(ToDouble(value, node), node);
                if (primitive.Category == DdfPrimitiveTypeCategory.Integer) return ToInt(value, node);
                if (primitive.Category == DdfPrimitiveTypeCategory.Character &&
                    allowCharacterLiteral && value is string text && text.Length == 1) return text[0];
                return value;
            }

            private static object ParseLiteral(DdfIrLiteral literal)
            {
                string text = literal.Text;
                if (literal.Kind == DdfLiteralKind.Boolean)
                {
                    if (literal.BooleanValue.HasValue) return literal.BooleanValue.Value;
                    throw new InvalidOperationException("Boolean literal without a declared value: " + text + ".");
                }
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
                for (int index = scopes.Count - 1; index >= 1; index--)
                    if (scopes[index].TryGetValue(name, out Cell cell)) return cell;
                string activeKey = ModuleKey(activeModule);
                if (globalsByModule.TryGetValue(activeKey, out Dictionary<string, Cell> ownGlobals) &&
                    ownGlobals.TryGetValue(name, out Cell ownGlobal)) return ownGlobal;
                foreach (DdfIrModule dependency in DirectDependencies(activeModule))
                    if (dependency.Exports.Contains(name, StringComparer.Ordinal) &&
                        globalsByModule.TryGetValue(ModuleKey(dependency), out Dictionary<string, Cell> dependencyGlobals) &&
                        dependencyGlobals.TryGetValue(name, out Cell dependencyGlobal)) return dependencyGlobal;
                if (scopes.Count > 0 && scopes[0].TryGetValue(name, out Cell hostCell)) return hostCell;
                if (options.HostCatalog?.TryGetObject(name,
                    out DdfHostObjectDefinition deniedObject) == true &&
                    !options.HostCatalog.HasPermissions(deniedObject.RequiredPermissions))
                    Fail("DDF409", "Permessi host mancanti per l'oggetto '" +
                        name + "'.", node);
                Fail("DDF406", "Variabile non definita: '" + name + "'.", node);
                return null;
            }

            private void Declare(string name, object value, string typeName, DdfRuntimeVariableKind kind)
            {
                if (string.IsNullOrEmpty(name)) return;
                var cell = new Cell(value, typeName,
                    declaringGlobal ? DdfRuntimeVariableKind.Global : kind);
                if (declaringGlobal)
                    globalsByModule[ModuleKey(activeModule)][name] = cell;
                else
                    scopes[scopes.Count - 1][name] = cell;
            }

            private bool TryResolveFunction(DdfIrModule module, string name,
                bool includeDependencies, out DdfIrFunction function)
            {
                if (functionsByModule.TryGetValue(ModuleKey(module),
                    out Dictionary<string, DdfIrFunction> ownFunctions) &&
                    ownFunctions.TryGetValue(name, out function)) return true;
                if (includeDependencies)
                    foreach (DdfIrModule dependency in DirectDependencies(module))
                        if (dependency.Exports.Contains(name, StringComparer.Ordinal) &&
                            functionsByModule.TryGetValue(ModuleKey(dependency),
                                out Dictionary<string, DdfIrFunction> dependencyFunctions) &&
                            dependencyFunctions.TryGetValue(name, out function)) return true;
                function = null;
                return false;
            }

            private bool TryResolveStructure(DdfIrModule module, string name,
                bool includeDependencies, out DdfIrStructure structure)
            {
                if (structuresByModule.TryGetValue(ModuleKey(module),
                    out Dictionary<string, DdfIrStructure> ownStructures) &&
                    ownStructures.TryGetValue(name, out structure)) return true;
                if (includeDependencies)
                    foreach (DdfIrModule dependency in DirectDependencies(module))
                        if (dependency.Exports.Contains(name, StringComparer.Ordinal) &&
                            structuresByModule.TryGetValue(ModuleKey(dependency),
                                out Dictionary<string, DdfIrStructure> dependencyStructures) &&
                            dependencyStructures.TryGetValue(name, out structure)) return true;
                structure = null;
                return false;
            }

            private IEnumerable<DdfIrModule> DirectDependencies(DdfIrModule module)
            {
                foreach (DdfProjectDependency dependency in module.Dependencies)
                    if (moduleDescriptors.TryGetValue(ModuleKey(dependency.Id, dependency.Version),
                        out DdfIrModule resolved)) yield return resolved;
            }

            private static string ModuleKey(DdfIrModule module) =>
                ModuleKey(module.ModuleId, module.ModuleVersion);

            private static string ModuleKey(string id, string version) => id + "@" + version;

            private Cell DeclareHost(DdfHostObjectDefinition definition, object value,
                Func<object> reader, Action<object> writer)
            {
                var cell = new Cell(value,
                    definition.TypeName, DdfRuntimeVariableKind.Global, reader, writer,
                    definition.Name);
                scopes[scopes.Count - 1][definition.Name] = cell;
                return cell;
            }

            private object ReadCell(Cell cell, DdfIrNode node)
            {
                try { return cell.Read(); }
                catch (RuntimeSignal) { throw; }
                catch (Exception exception)
                {
                    Fail("DDF410", "Impossibile leggere l'oggetto host '" +
                        cell.ExternalName + "': " + exception.Message, node);
                    return null;
                }
            }

            private void WriteCell(Cell cell, object value, DdfIrNode node)
            {
                if (!cell.CanWrite)
                    Fail("DDF411", "L'oggetto host '" + cell.ExternalName +
                        "' è esposto in sola lettura.", node);
                try { cell.Write(value); }
                catch (RuntimeSignal) { throw; }
                catch (Exception exception)
                {
                    Fail("DDF410", "Impossibile scrivere l'oggetto host '" +
                        cell.ExternalName + "': " + exception.Message, node);
                }
            }

            private object ImportHostValue(object value, string typeName, DdfIrNode node,
                bool nestedValuesWritable, string externalName = null,
                Action<object> nestedWriter = null)
            {
                string elementType = typeName ?? string.Empty;
                if (elementType.EndsWith("[]", StringComparison.Ordinal))
                {
                    elementType = elementType.Substring(0, elementType.Length - 2);
                    if (value == null) return new Cell[0];
                    if (value is string || !(value is IEnumerable sequence))
                        throw new InvalidOperationException("Il valore host per '" + typeName +
                            "' deve essere una sequenza esplicita.");
                    var cells = new List<Cell>();
                    foreach (object item in sequence)
                    {
                        object imported = ImportHostValue(item, elementType, node,
                            nestedValuesWritable, externalName,
                            ignored => nestedWriter?.Invoke(cells.ToArray()));
                        Action<object> elementWriter = nestedValuesWritable && nestedWriter != null
                            ? new Action<object>(ignored => nestedWriter(cells.ToArray()))
                            : null;
                        cells.Add(new Cell(imported, elementType,
                            DdfRuntimeVariableKind.Element, null, elementWriter, externalName));
                    }
                    return cells.ToArray();
                }

                DdfHostTypeDefinition hostType = options.HostCatalog?.Types.FirstOrDefault(
                    item => item.Name == elementType);
                if (hostType != null)
                {
                    if (value == null) value = new Dictionary<string, object>(StringComparer.Ordinal);
                    var dictionary = value as IDictionary<string, object>;
                    var readOnlyDictionary = value as IReadOnlyDictionary<string, object>;
                    if (dictionary == null && readOnlyDictionary == null)
                        throw new InvalidOperationException("Il valore host per '" + elementType +
                            "' deve essere un dizionario di campi registrati.");
                    var fields = new Dictionary<string, Cell>(StringComparer.Ordinal);
                    foreach (DdfHostFieldDefinition field in hostType.Fields)
                    {
                        object fieldValue = null;
                        bool found = dictionary != null
                            ? dictionary.TryGetValue(field.Name, out fieldValue)
                            : readOnlyDictionary.TryGetValue(field.Name, out fieldValue);
                        if (!found) throw new InvalidOperationException("Campo host mancante: '" +
                            elementType + "." + field.Name + "'.");
                        bool fieldWritable = nestedValuesWritable && field.CanWrite &&
                            nestedWriter != null;
                        object imported = ImportHostValue(fieldValue,
                            field.TypeName, node, fieldWritable, externalName,
                            ignored => nestedWriter(fields));
                        Action<object> fieldWriter = fieldWritable
                            ? new Action<object>(ignored => nestedWriter(fields))
                            : null;
                        fields.Add(field.Name, new Cell(imported, field.TypeName,
                            DdfRuntimeVariableKind.Field, null, fieldWriter, externalName));
                    }
                    return fields;
                }

                if (value == null)
                {
                    if (typeSystem.TryGetPrimitive(elementType, out DdfPrimitiveTypeDefinition emptyPrimitive))
                    {
                        if (emptyPrimitive.Category == DdfPrimitiveTypeCategory.Boolean) return false;
                        if (emptyPrimitive.Category == DdfPrimitiveTypeCategory.Floating) return 0d;
                        if (emptyPrimitive.Category == DdfPrimitiveTypeCategory.Character) return '\0';
                        if (emptyPrimitive.Category == DdfPrimitiveTypeCategory.String) return string.Empty;
                        if (emptyPrimitive.Category == DdfPrimitiveTypeCategory.Integer) return 0;
                    }
                    return null;
                }
                if (!typeSystem.TryGetPrimitive(elementType, out DdfPrimitiveTypeDefinition primitive))
                    return value;
                if (primitive.Category == DdfPrimitiveTypeCategory.Boolean) return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                if (primitive.Category == DdfPrimitiveTypeCategory.Floating) return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (primitive.Category == DdfPrimitiveTypeCategory.Integer) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (primitive.Category == DdfPrimitiveTypeCategory.Character) return Convert.ToChar(value, CultureInfo.InvariantCulture);
                if (primitive.Category == DdfPrimitiveTypeCategory.String) return Convert.ToString(value, CultureInfo.InvariantCulture);
                return value;
            }

            private static object ExportHostValue(object value)
            {
                if (value is Cell[] array)
                    return array.Select(cell => ExportHostValue(cell.Read())).ToList().AsReadOnly();
                if (value is Dictionary<string, Cell> fields)
                    return fields.ToDictionary(pair => pair.Key,
                        pair => ExportHostValue(pair.Value.Read()), StringComparer.Ordinal);
                return value;
            }

            private static object FreezeHostValue(object value)
            {
                if (value == null || value is string || value.GetType().IsValueType)
                    return value;
                if (value is IDictionary<string, object> dictionary)
                    return new ReadOnlyDictionary<string, object>(dictionary.ToDictionary(
                        pair => pair.Key, pair => FreezeHostValue(pair.Value),
                        StringComparer.Ordinal));
                if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
                    return new ReadOnlyDictionary<string, object>(readOnlyDictionary.ToDictionary(
                        pair => pair.Key, pair => FreezeHostValue(pair.Value),
                        StringComparer.Ordinal));
                if (value is IEnumerable sequence)
                    return sequence.Cast<object>().Select(FreezeHostValue).ToList().AsReadOnly();
                return value;
            }

            private static string FormatType(DdfIrType type) => type == null ? "?" :
                type.Name + string.Concat(System.Linq.Enumerable.Repeat("[]", type.ArrayLengths.Count));

            private void PushScope() => scopes.Add(new Dictionary<string, Cell>(StringComparer.Ordinal));
            private void PopScope() => scopes.RemoveAt(scopes.Count - 1);

            private void Tick(DdfIrNode node)
            {
                if (options.CancellationToken.IsCancellationRequested ||
                    (options.CancellationRequested != null && options.CancellationRequested()))
                    throw new CancelledSignal();
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
                Fail("DDF403", "Era atteso un valore numerico.", node);
                return 0;
            }

            private int ToInt(object value, DdfIrNode node)
            {
                if (value is int integer) return integer;
                Fail("DDF403", "Era atteso un valore int.", node);
                return 0;
            }

            private static bool IsFloating(object value) => value is double;
            private object EvaluateNumericBinary(string operation, object left, object right,
                DdfIrNode node)
            {
                if (left is int leftInteger && right is int rightInteger &&
                    operation != DdfVmExpressionOperations.Root)
                {
                    try
                    {
                        if (operation == DdfVmExpressionOperations.Add)
                            return checked(leftInteger + rightInteger);
                        if (operation == DdfVmExpressionOperations.Subtract)
                            return checked(leftInteger - rightInteger);
                        if (operation == DdfVmExpressionOperations.Multiply)
                            return checked(leftInteger * rightInteger);
                        if (operation == DdfVmExpressionOperations.Divide)
                        {
                            if (rightInteger == 0) Fail("DDF404", "Divisione per zero.", node);
                            return checked(leftInteger / rightInteger);
                        }
                        if (operation == DdfVmExpressionOperations.Power)
                        {
                            if (rightInteger < 0)
                                Fail("DDF419", "Una potenza intera richiede un esponente non negativo.", node);
                            return IntegerPower(leftInteger, rightInteger);
                        }
                    }
                    catch (OverflowException) { FailIntegerOverflow(node); }
                }

                double a = ToDouble(left, node);
                double b = ToDouble(right, node);
                double result;
                if (operation == DdfVmExpressionOperations.Add) result = a + b;
                else if (operation == DdfVmExpressionOperations.Subtract) result = a - b;
                else if (operation == DdfVmExpressionOperations.Multiply) result = a * b;
                else if (operation == DdfVmExpressionOperations.Divide)
                {
                    if (b == 0d) Fail("DDF404", "Divisione per zero.", node);
                    result = a / b;
                }
                else if (operation == DdfVmExpressionOperations.Power) result = Math.Pow(a, b);
                else if (operation == DdfVmExpressionOperations.Root)
                {
                    if (b == 0d) Fail("DDF404", "Indice della radice uguale a zero.", node);
                    result = Math.Pow(a, 1d / b);
                }
                else
                {
                    Fail("DDFE004", "Operazione numerica runtime non supportata: '" +
                        operation + "'.", node, DdfDiagnosticDomain.EngineCapability);
                    return null;
                }
                return EnsureFinite(result, node);
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

            private double EnsureFinite(double value, DdfIrNode node)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    Fail("DDF418", "Il risultato float non è finito.", node);
                return value;
            }

            private void FailIntegerOverflow(DdfIrNode node)
            {
                Fail("DDF417", "Overflow dell'aritmetica int checked.", node);
            }

            private static bool EqualsValue(object left, object right) =>
                (left is int || left is double) && (right is int || right is double)
                    ? Convert.ToDouble(left, CultureInfo.InvariantCulture) ==
                        Convert.ToDouble(right, CultureInfo.InvariantCulture)
                    : Equals(left, right);
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
                globals.AddRange(CaptureVisibleModuleGlobals(ref budget));
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

            private IEnumerable<DdfRuntimeVariableSnapshot> CaptureVisibleModuleGlobals(ref int budget)
            {
                var variables = new List<DdfRuntimeVariableSnapshot>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                AppendModuleGlobals(activeModule, false, variables, names, ref budget);
                foreach (DdfIrModule dependency in DirectDependencies(activeModule))
                    AppendModuleGlobals(dependency, true, variables, names, ref budget);
                return variables;
            }

            private void AppendModuleGlobals(DdfIrModule module, bool exportsOnly,
                ICollection<DdfRuntimeVariableSnapshot> variables, ISet<string> names,
                ref int budget)
            {
                if (!globalsByModule.TryGetValue(ModuleKey(module),
                    out Dictionary<string, Cell> globals)) return;
                foreach (KeyValuePair<string, Cell> pair in globals)
                {
                    if (exportsOnly && !module.Exports.Contains(pair.Key, StringComparer.Ordinal)) continue;
                    if (!names.Add(pair.Key)) continue;
                    variables.Add(CaptureDebugValue(pair.Key, pair.Value, 0, 0,
                        ref budget, DdfRuntimeVariableKind.Global));
                }
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
                object value;
                try { value = cell.Read(); }
                catch (Exception exception) { value = "<errore host: " + exception.Message + ">"; }
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
                private object value;
                private readonly Func<object> reader;
                private readonly Action<object> writer;

                public Cell(object value, string typeName, DdfRuntimeVariableKind kind)
                    : this(value, typeName, kind, true) { }
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind,
                    bool writable)
                    : this(value, typeName, kind, writable, null) { }
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind,
                    bool writable, string externalName)
                    : this(value, typeName, kind, null,
                        writable ? new Action<object>(_ => { }) : null, externalName) { }
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind,
                    Func<object> reader, Action<object> writer, string externalName)
                {
                    this.value = value;
                    this.reader = reader;
                    this.writer = writer;
                    TypeName = typeName;
                    Kind = kind;
                    ExternalName = externalName;
                }
                public bool CanWrite => writer != null;
                public bool WasWritten { get; private set; }
                public object SnapshotValue => value;
                public string ExternalName { get; }
                public object Read() => reader == null ? value : reader();
                public void Write(object newValue)
                {
                    if (writer == null) throw new InvalidOperationException("Valore in sola lettura.");
                    object previous = value;
                    value = newValue;
                    try { writer(newValue); }
                    catch
                    {
                        value = previous;
                        throw;
                    }
                    WasWritten = true;
                }
                public string TypeName { get; }
                public DdfRuntimeVariableKind Kind { get; }
            }
            private sealed class HostBinding
            {
                public HostBinding(DdfHostObjectDefinition definition)
                {
                    Definition = definition;
                }

                public DdfHostObjectDefinition Definition { get; }
                public Cell Cell { get; set; }
                public object InitialValue { get; private set; }
                private bool HasInitialValue { get; set; }

                public void CaptureInitial(object value)
                {
                    if (HasInitialValue) return;
                    InitialValue = FreezeHostValue(value);
                    HasInitialValue = true;
                }
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
            private sealed class ReturnSignal : Exception
            {
                public ReturnSignal(object value, DdfIrNode node) { Value = value; Node = node; }
                public object Value { get; }
                public DdfIrNode Node { get; }
            }
            private sealed class BreakSignal : Exception { }
            private sealed class EndSignal : Exception { }
            private sealed class CancelledSignal : Exception { }
            private sealed class HostResourceLimitException : Exception
            {
                public HostResourceLimitException(string message) : base(message) { }
            }
        }
    }
}
