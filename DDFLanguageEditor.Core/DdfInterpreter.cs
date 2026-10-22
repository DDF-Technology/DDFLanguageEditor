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
    public readonly struct DdfEnumValue : IEquatable<DdfEnumValue>
    {
        public DdfEnumValue(string typeName, string memberName)
        {
            TypeName = typeName ?? string.Empty;
            MemberName = memberName ?? string.Empty;
        }

        public string TypeName { get; }
        public string MemberName { get; }
        public bool Equals(DdfEnumValue other) => TypeName == other.TypeName &&
            MemberName == other.MemberName;
        public override bool Equals(object obj) => obj is DdfEnumValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TypeName, MemberName);
        public override string ToString() => TypeName + "." + MemberName;
    }

    /// <summary>
    /// Explicit, process-local state owned by one script instance. The store never
    /// persists itself to disk and deliberately rejects overlapping executions.
    /// </summary>
    public sealed class DdfStateStore
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> values =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private int executionActive;

        public int Count
        {
            get { lock (sync) return values.Count; }
        }

        public IReadOnlyDictionary<string, object> Snapshot()
        {
            lock (sync)
                return new ReadOnlyDictionary<string, object>(values.ToDictionary(
                    item => item.Key, item => item.Value.Value, StringComparer.Ordinal));
        }

        internal IReadOnlyList<DdfStateStoreEntry> SnapshotEntries()
        {
            lock (sync)
                return values.Select(item => new DdfStateStoreEntry(item.Key,
                    item.Value.TypeName, item.Value.Value)).ToList().AsReadOnly();
        }

        public void Reset()
        {
            lock (sync) values.Clear();
        }

        internal bool TryRead(string key, string typeName, out object value)
        {
            lock (sync)
            {
                if (values.TryGetValue(key, out Entry entry) && entry.TypeName == typeName)
                {
                    value = entry.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        internal void Write(string key, string typeName, object value)
        {
            lock (sync) values[key] = new Entry(typeName, value);
        }

        internal DdfStateStore Fork()
        {
            var copy = new DdfStateStore();
            lock (sync)
                foreach (KeyValuePair<string, Entry> item in values)
                    copy.values.Add(item.Key,
                        new Entry(item.Value.TypeName, item.Value.Value));
            return copy;
        }

        internal void ReplaceWith(DdfStateStore source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (ReferenceEquals(this, source)) return;
            Dictionary<string, Entry> replacement;
            lock (source.sync)
                replacement = source.values.ToDictionary(item => item.Key,
                    item => new Entry(item.Value.TypeName, item.Value.Value),
                    StringComparer.Ordinal);
            lock (sync)
            {
                values.Clear();
                foreach (KeyValuePair<string, Entry> item in replacement)
                    values.Add(item.Key, item.Value);
            }
        }

        internal void ReplaceEntries(IEnumerable<DdfStateStoreEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var replacement = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (DdfStateStoreEntry item in entries)
                replacement.Add(item.Key, new Entry(item.TypeName, item.Value));
            lock (sync)
            {
                values.Clear();
                foreach (KeyValuePair<string, Entry> item in replacement)
                    values.Add(item.Key, item.Value);
            }
        }

        internal IDisposable BeginExecution()
        {
            if (Interlocked.CompareExchange(ref executionActive, 1, 0) != 0)
                throw new DdfStateStoreBusyException(
                    "Lo stesso DdfStateStore non può essere usato da esecuzioni sovrapposte.");
            return new ExecutionLease(this);
        }

        private sealed class Entry
        {
            public Entry(string typeName, object value)
            {
                TypeName = typeName;
                Value = value;
            }
            public string TypeName { get; }
            public object Value { get; }
        }

        private sealed class ExecutionLease : IDisposable
        {
            private DdfStateStore owner;
            public ExecutionLease(DdfStateStore owner) { this.owner = owner; }
            public void Dispose()
            {
                DdfStateStore released = Interlocked.Exchange(ref owner, null);
                if (released != null) Volatile.Write(ref released.executionActive, 0);
            }
        }
    }

    internal sealed class DdfStateStoreEntry
    {
        public DdfStateStoreEntry(string key, string typeName, object value)
        {
            Key = key;
            TypeName = typeName;
            Value = value;
        }

        public string Key { get; }
        public string TypeName { get; }
        public object Value { get; }
    }

    internal sealed class DdfStateStoreBusyException : InvalidOperationException
    {
        public DdfStateStoreBusyException(string message) : base(message) { }
    }

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
        public IDdfEditorUiCapabilityInvoker UiCapabilityInvoker { get; set; }
        public DdfStateStore StateStore { get; set; }
        internal DdfHostEffectTracker HostEffectTracker { get; set; }
        internal long ExecutionGeneration { get; set; }
        internal Action<DdfCooperativeExecutionSnapshot> CooperativeStateChanged
        { get; set; }
    }

    public sealed class DdfRuntimeStackFrame
    {
        public DdfRuntimeStackFrame(string functionName, int start, int length, int line, int column,
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
        public DdfExecutionResult(object returnValue, int instructions, bool cancelled,
            bool terminated, IReadOnlyList<DdfDiagnostic> diagnostics,
            IReadOnlyList<DdfRuntimeStackFrame> stackTrace,
            IReadOnlyList<DdfHostValueResult> hostValues = null,
            string returnTypeName = null)
            : this(returnValue, instructions, cancelled, terminated,
                diagnostics, stackTrace, hostValues, returnTypeName, null)
        {
        }

        public DdfExecutionResult(object returnValue, int instructions,
            bool cancelled, bool terminated,
            IReadOnlyList<DdfDiagnostic> diagnostics,
            IReadOnlyList<DdfRuntimeStackFrame> stackTrace,
            IReadOnlyList<DdfHostValueResult> hostValues,
            string returnTypeName, DdfErrorValue error)
        {
            ReturnValue = returnValue;
            Instructions = instructions;
            WasCancelled = cancelled;
            WasTerminated = terminated;
            Diagnostics = diagnostics;
            StackTrace = stackTrace;
            HostValues = hostValues ?? Array.Empty<DdfHostValueResult>();
            ReturnTypeName = returnTypeName ?? string.Empty;
            Error = error;
        }

        public object ReturnValue { get; }
        public int Instructions { get; }
        public bool WasCancelled { get; }
        public bool WasTerminated { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DdfRuntimeStackFrame> StackTrace { get; }
        public IReadOnlyList<DdfHostValueResult> HostValues { get; }
        public string ReturnTypeName { get; }
        public DdfErrorValue Error { get; }
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
            => ExecuteAsync(source, root, runtime, typeSystem, standardLibrary,
                options).GetAwaiter().GetResult();

        public static Task<DdfExecutionResult> ExecuteAsync(string source,
            DdfIrProgram root, DdfRuntimeDefinition runtime,
            DdfTypeSystemDefinition typeSystem,
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
            return ExecuteAsync(new[] { new DdfIrModule(options.SourcePath, source, root) }, runtime,
                typeSystem, standardLibrary, options);
        }

        public static DdfExecutionResult Execute(IReadOnlyList<DdfIrModule> modules,
            DdfRuntimeDefinition runtime, DdfTypeSystemDefinition typeSystem,
            DdfStandardLibraryDefinition standardLibrary, DdfExecutionOptions options = null)
            => ExecuteAsync(modules, runtime, typeSystem, standardLibrary, options)
                .GetAwaiter().GetResult();

        public static Task<DdfExecutionResult> ExecuteAsync(
            IReadOnlyList<DdfIrModule> modules,
            DdfRuntimeDefinition runtime, DdfTypeSystemDefinition typeSystem,
            DdfStandardLibraryDefinition standardLibrary,
            DdfExecutionOptions options = null)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            if (modules.Count == 0) throw new ArgumentException("È richiesto almeno un modulo runtime.", nameof(modules));
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (typeSystem == null) throw new ArgumentNullException(nameof(typeSystem));
            if (standardLibrary == null) throw new ArgumentNullException(nameof(standardLibrary));
            options = options ?? new DdfExecutionOptions();
            if (string.IsNullOrWhiteSpace(options.EntryPoint)) options.EntryPoint = runtime.EntryPoint;
            if (options.MaxInstructions <= 0) options.MaxInstructions = runtime.DefaultInstructionLimit;
            return new Runner(modules, runtime, typeSystem, standardLibrary,
                options).RunAsync();
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
            private readonly Dictionary<string, Dictionary<string, DdfIrEnum>> enumsByModule =
                new Dictionary<string, Dictionary<string, DdfIrEnum>>(StringComparer.Ordinal);
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
            private readonly DdfStateStore stateStore;
            private readonly DdfCooperativeExecutionCoordinator cooperativeExecution =
                new DdfCooperativeExecutionCoordinator();
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
                stateStore = options.StateStore ?? new DdfStateStore();
                executionId = options.ExecutionId == Guid.Empty
                    ? Guid.NewGuid() : options.ExecutionId;
            }

            public async Task<DdfExecutionResult> RunAsync()
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
                DdfErrorValue error = null;
                IDisposable stateLease = null;
                DdfCooperativeExecutionSnapshot cooperativeSnapshot =
                    cooperativeExecution.Begin(executionId,
                        options.ExecutionGeneration > 0
                            ? options.ExecutionGeneration : null);
                NotifyCooperativeState(cooperativeSnapshot);
                try
                {
                    stateLease = stateStore.BeginExecution();
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
                                try { await ExecuteStatementAsync(global.Statement)
                                    .ConfigureAwait(false); }
                                finally { declaringGlobal = false; }
                            }
                    }

                    if (!string.IsNullOrWhiteSpace(options.EntryPoint))
                    {
                        if (!TryResolveFunction(activeModule, options.EntryPoint, false,
                            out DdfIrFunction entry))
                            Fail("DDF401", "Funzione di ingresso '" + options.EntryPoint + "' non trovata.", activeModule.Program);
                        returnTypeName = FormatType(entry.ReturnType);
                        returnValue = await InvokeAsync(entry,
                            BindEntryArguments(entry), null).ConfigureAwait(false);
                    }
                }
                catch (ReturnSignal signal) { returnValue = signal.Value; }
                catch (BreakSignal) { diagnostics.Add(CreateDiagnostic("DDF403", "'brk' può essere usato soltanto in un ciclo.", activeModule.Program,
                    DdfDiagnosticDomain.Source)); }
                catch (ContinueSignal) { diagnostics.Add(CreateDiagnostic("DDF403", "'continue' può essere usato soltanto in un ciclo.", activeModule.Program,
                    DdfDiagnosticDomain.Source)); }
                catch (EndSignal) { terminated = true; }
                catch (CancelledSignal signal)
                {
                    cancelled = true;
                    error = signal.Error;
                    stackTrace = signal.DdfStackTrace;
                }
                catch (RuntimeSignal signal)
                {
                    diagnostics.Add(signal.Diagnostic);
                    stackTrace = signal.DdfStackTrace;
                    error = signal.Error;
                }
                catch (DdfStateStoreBusyException exception)
                {
                    DdfDiagnostic diagnostic = CreateDiagnostic("DDF423",
                        exception.Message, activeModule.Program);
                    diagnostics.Add(diagnostic);
                    error = CreateErrorValue(DdfErrorCatalog.ConcurrentStateUse,
                        diagnostic, Array.Empty<DdfRuntimeStackFrame>());
                }
                catch (Exception exception)
                {
                    DdfDiagnostic diagnostic = CreateDiagnostic("DDF499",
                        "Errore runtime interno: " + exception.Message,
                        activeModule.Program);
                    diagnostics.Add(diagnostic);
                    error = CreateErrorValue(DdfErrorCatalog.InternalFailure,
                        diagnostic, Array.Empty<DdfRuntimeStackFrame>());
                }
                finally
                {
                    returnValue = FreezeHostValue(ExportHostValue(returnValue));
                    hostValues = CaptureHostValues();
                    DdfCooperativeExecutionSnapshot beforeCleanup =
                        cooperativeExecution.Snapshot;
                    if (beforeCleanup.State ==
                        DdfCooperativeExecutionState.AwaitingCapability)
                        cooperativeExecution.RequestCancellation(
                            beforeCleanup.Generation);
                    if (cooperativeExecution.Snapshot.State is
                        DdfCooperativeExecutionState.Running or
                        DdfCooperativeExecutionState.Cancelling)
                    {
                        cooperativeExecution.BeginCleanup(
                            cooperativeExecution.Snapshot.Generation);
                        NotifyCooperativeState(cooperativeExecution.Snapshot);
                    }
                    ReleaseHostResources();
                    stateLease?.Dispose();
                    scopes.Clear();
                    globalsByModule.Clear();
                    cooperativeExecution.Complete(
                        cooperativeExecution.Snapshot.Generation);
                    NotifyCooperativeState(cooperativeExecution.Snapshot);
                }

                return new DdfExecutionResult(returnValue, instructions, cancelled,
                    terminated, diagnostics.AsReadOnly(), stackTrace, hostValues,
                    returnTypeName, error);
            }

            private void NotifyCooperativeState(
                DdfCooperativeExecutionSnapshot snapshot) =>
                options.CooperativeStateChanged?.Invoke(snapshot);

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
                    if (!enumsByModule.TryGetValue(moduleKey,
                        out Dictionary<string, DdfIrEnum> enumerations))
                    {
                        enumerations = new Dictionary<string, DdfIrEnum>(StringComparer.Ordinal);
                        enumsByModule.Add(moduleKey, enumerations);
                    }
                    foreach (DdfIrMember member in module.Program.Members)
                    {
                        if (member is DdfIrFunction function && !string.IsNullOrEmpty(function.Name))
                        { functions[function.Name] = function; functionModules[function] = module; }
                        else if (member is DdfIrStructure structure && !string.IsNullOrEmpty(structure.Name))
                        { structures[structure.Name] = structure; structureModules[structure] = module; }
                        else if (member is DdfIrEnum enumeration && !string.IsNullOrEmpty(enumeration.Name))
                            enumerations[enumeration.Name] = enumeration;
                    }
                }
            }

            private async Task<object> InvokeAsync(DdfIrFunction function,
                IList<object> arguments, DdfIrNode invocation)
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
                                await ConvertForTypeAsync(arguments[index],
                                    function.Parameters[index].Type,
                                    function.Parameters[index]).ConfigureAwait(false),
                                FormatType(function.Parameters[index].Type), DdfRuntimeVariableKind.Parameter);
                        try { await ExecuteBlockAsync(function.Body, false)
                            .ConfigureAwait(false); }
                        catch (ReturnSignal signal)
                        {
                            return await ConvertForTypeAsync(signal.Value,
                                function.ReturnType,
                                signal.Node ?? (DdfIrNode)function)
                                .ConfigureAwait(false);
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

            private async Task ExecuteStatementAsync(DdfIrStatement statement)
            {
                if (statement == null) return;
                if (!(statement is DdfIrBlock) && options.DebuggerSession != null &&
                    !options.DebuggerSession.BeforeStatement(activeModule.Source, statement, options.CancellationRequested,
                        () => CaptureStateSnapshot(statement), callStack.Count, activeModule.DocumentPath))
                    Cancel(statement);
                Tick(statement);
                if (statement is DdfIrBlock block)
                    await ExecuteBlockAsync(block, true).ConfigureAwait(false);
                else if (statement is DdfIrVariableDeclaration variable)
                {
                    string typeName = FormatType(variable.Type);
                    string stateKey = variable.IsState && declaringGlobal
                        ? StateKey(activeModule, variable.Name) : null;
                    object value;
                    if (stateKey != null && stateStore.TryRead(stateKey, typeName, out object stored))
                        value = await ConvertForTypeAsync(stored, variable.Type,
                            variable).ConfigureAwait(false);
                    else
                    {
                        value = variable.ConstantValue != null ? variable.ConstantValue.Value :
                            variable.Initializer == null
                                ? await CreateDefaultAsync(variable.Type)
                                    .ConfigureAwait(false)
                                : await ConvertForTypeAsync(
                                    await EvaluateAsync(variable.Initializer)
                                        .ConfigureAwait(false),
                                    variable.Type, variable,
                                    variable.Initializer is DdfIrLiteral)
                                    .ConfigureAwait(false);
                        if (stateKey != null) stateStore.Write(stateKey, typeName, value);
                    }
                    DdfRuntimeVariableKind kind = stateKey == null
                        ? DdfRuntimeVariableKind.Local
                        : DdfRuntimeVariableKind.State;
                    Declare(variable.Name, value, typeName, kind,
                        !variable.IsFixed, stateKey == null ? null :
                            new Action<object>(updated => stateStore.Write(stateKey, typeName, updated)),
                        variable.IsFixed);
                }
                else if (statement is DdfIrExpressionStatement expression)
                    await EvaluateAsync(expression.Expression).ConfigureAwait(false);
                else if (statement is DdfIrIf conditional)
                {
                    if (AsBoolean(await EvaluateAsync(conditional.Condition)
                        .ConfigureAwait(false), conditional.Condition))
                        await ExecuteStatementAsync(conditional.Body)
                            .ConfigureAwait(false);
                    else if (conditional.ElseBody != null)
                        await ExecuteStatementAsync(conditional.ElseBody)
                            .ConfigureAwait(false);
                }
                else if (statement is DdfIrSelect selection)
                {
                    object selected = await EvaluateAsync(selection.Expression)
                        .ConfigureAwait(false);
                    DdfIrSelectClause fallback = null;
                    foreach (DdfIrSelectClause clause in selection.Clauses)
                    {
                        if (clause.IsOtherwise) { fallback = clause; continue; }
                        if (!EqualsValue(selected, await EvaluateAsync(clause.Value)
                            .ConfigureAwait(false))) continue;
                        await ExecuteStatementAsync(clause.Body).ConfigureAwait(false);
                        return;
                    }
                    if (fallback != null)
                        await ExecuteStatementAsync(fallback.Body)
                            .ConfigureAwait(false);
                }
                else if (statement is DdfIrWhile loop)
                {
                    while (AsBoolean(await EvaluateAsync(loop.Condition)
                        .ConfigureAwait(false), loop.Condition))
                    {
                        try { await ExecuteStatementAsync(loop.Body)
                            .ConfigureAwait(false); } catch (BreakSignal) { break; }
                        catch (ContinueSignal) { }
                    }
                }
                else if (statement is DdfIrDoWhile doLoop)
                {
                    do { try { await ExecuteStatementAsync(doLoop.Body)
                            .ConfigureAwait(false); } catch (BreakSignal) { break; }
                        catch (ContinueSignal) { } }
                    while (AsBoolean(await EvaluateAsync(doLoop.Condition)
                        .ConfigureAwait(false), doLoop.Condition));
                }
                else if (statement is DdfIrFor forLoop)
                {
                    PushScope();
                    try
                    {
                        await ExecuteStatementAsync(forLoop.Initializer)
                            .ConfigureAwait(false);
                        while (forLoop.Condition == null || AsBoolean(
                            await EvaluateAsync(forLoop.Condition)
                                .ConfigureAwait(false), forLoop.Condition))
                        {
                            try { await ExecuteStatementAsync(forLoop.Body)
                                .ConfigureAwait(false); } catch (BreakSignal) { break; }
                            catch (ContinueSignal) { }
                            if (forLoop.Increment != null)
                                await EvaluateAsync(forLoop.Increment)
                                    .ConfigureAwait(false);
                        }
                    }
                    finally { PopScope(); }
                }
                else if (statement is DdfIrReturn returned) throw new ReturnSignal(
                    returned.Expression == null ? null :
                        await EvaluateAsync(returned.Expression).ConfigureAwait(false),
                    returned);
                else if (statement is DdfIrBreak) throw new BreakSignal();
                else if (statement is DdfIrContinue) throw new ContinueSignal();
                else if (statement is DdfIrEnd) throw new EndSignal();
            }

            private async Task ExecuteBlockAsync(DdfIrBlock block, bool createScope)
            {
                if (createScope) PushScope();
                try
                {
                    foreach (DdfIrStatement statement in block.Statements)
                        await ExecuteStatementAsync(statement).ConfigureAwait(false);
                }
                finally { if (createScope) PopScope(); }
            }

            private async Task<object> EvaluateAsync(DdfIrExpression expression)
            {
                if (expression == null || expression is DdfIrMissingExpression) return null;
                Tick(expression);
                if (expression is DdfIrLiteral literal) return ParseLiteral(literal);
                if (expression is DdfIrName name) return ReadCell(FindCell(name.Name, name), name);
                if (expression is DdfIrGroup parenthesized)
                    return await EvaluateAsync(parenthesized.Expression)
                        .ConfigureAwait(false);
                if (expression is DdfIrUnary unary)
                {
                    object operand = await EvaluateAsync(unary.Operand)
                        .ConfigureAwait(false);
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
                if (expression is DdfIrBinary binary)
                    return await EvaluateBinaryAsync(binary).ConfigureAwait(false);
                if (expression is DdfIrPostfix postfix)
                {
                    Cell cell = await ResolveCellAsync(postfix.Operand)
                        .ConfigureAwait(false);
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
                        foreach (DdfIrExpression argument in call.Arguments)
                            userArguments.Add(await EvaluateAsync(argument)
                                .ConfigureAwait(false));
                        return await InvokeAsync(function, userArguments,
                            call.Target).ConfigureAwait(false);
                    }
                    if (target != null && standardLibrary.TryGetFunction(target.Name, out DdfStandardFunction standard))
                    {
                        var standardArguments = new List<object>();
                        foreach (DdfIrExpression argument in call.Arguments)
                            standardArguments.Add(await EvaluateAsync(argument)
                                .ConfigureAwait(false));
                        return InvokeStandard(standard, standardArguments, call);
                    }
                    if (target != null && options.HostCatalog?.TryGetFunction(
                        target.Name, out DdfHostFunctionDefinition hostFunction) == true)
                    {
                        var hostArguments = new List<object>();
                        foreach (DdfIrExpression argument in call.Arguments)
                            hostArguments.Add(await EvaluateAsync(argument)
                                .ConfigureAwait(false));
                        return await InvokeHostAsync(hostFunction, hostArguments,
                            call).ConfigureAwait(false);
                    }
                    Fail("DDF402", "Funzione non trovata.", call.Target);
                    return null;
                }
                if (expression is DdfIrMemberAccess enumMember &&
                    enumMember.Target is DdfIrName enumName &&
                    TryResolveEnum(activeModule, enumName.Name, true,
                        out DdfIrEnum enumeration))
                {
                    if (!enumeration.Members.Any(item => item.Name == enumMember.Name))
                        Fail("DDF405", "Valore enum non trovato: '" + enumMember.Name + "'.",
                            enumMember);
                    return new DdfEnumValue(enumeration.Name, enumMember.Name);
                }
                if (expression is DdfIrIndex index)
                    return await ReadIndexAsync(index).ConfigureAwait(false);
                if (expression is DdfIrMemberAccess)
                    return ReadCell(await ResolveCellAsync(expression)
                        .ConfigureAwait(false), expression);
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

            private async Task<object> InvokeHostAsync(
                DdfHostFunctionDefinition function,
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
                        object hostResult = await InvokeHostOperationAsync(operation,
                            operationAsync, convertedArguments.AsReadOnly(),
                            arguments, function, node).ConfigureAwait(false);
                        options.HostEffectTracker?.Applied(effectToken);
                        object scriptResult = function.ReturnValueAdapter.ToScriptValue(hostResult);
                        return ImportHostValue(scriptResult, function.ReturnType, node, true);
                    }
                    catch (CancelledSignal)
                    {
                        throw;
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
                    catch (DdfExternalApiUnavailableException exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        Fail("DDF409", exception.Message, node);
                        return null;
                    }
                    catch (DdfHostResourceLeaseDeniedException exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        Fail("DDF409", exception.Message, node);
                        return null;
                    }
                    catch (DdfHostErrorException exception)
                    {
                        options.HostEffectTracker?.Failed(effectToken, exception);
                        bool declared = function.RecoverableErrors.Any(error =>
                            string.Equals(error.TypeId, exception.Definition.TypeId,
                                StringComparison.Ordinal) &&
                            string.Equals(error.Code, exception.Definition.Code,
                                StringComparison.Ordinal));
                        if (!declared)
                            Fail("DDF410", "La funzione host '" + function.Name +
                                "' ha restituito l'errore DDF non dichiarato '" +
                                exception.Definition.TypeId + "' (" +
                                exception.Definition.Code + ").", node);
                        Fail(exception.Definition, exception.Message, node);
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

            private async Task<object> InvokeHostOperationAsync(
                Func<IReadOnlyList<object>, object> operation,
                Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> operationAsync,
                IReadOnlyList<object> arguments,
                IReadOnlyList<object> scriptArguments,
                DdfHostFunctionDefinition function, DdfIrNode node)
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
                DateTimeOffset callStartedAt = DateTimeOffset.UtcNow;
                Task<object> task;
                if (function.DispatchTarget == DdfHostFunctionDispatchTarget.Ui)
                {
                    IDdfEditorUiCapabilityInvoker invoker =
                        options.UiCapabilityInvoker ?? throw new InvalidOperationException(
                            "Nessun dispatcher capability UI è associato all'esecuzione.");
                    string uiCapability = function.RequiredPermissions.Single(
                        permission => permission.StartsWith("ui.",
                            StringComparison.Ordinal));
                    task = invoker.InvokeAsync(uiCapability, async token =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (operationAsync != null)
                            return await operationAsync(context, arguments);
                        return operation(arguments);
                    }, callCancellation.Token).AsTask();
                }
                else if (operationAsync != null)
                    task = operationAsync(context, arguments).AsTask();
                else
                    task = Task.Run(() => operation(arguments), callCancellation.Token);
                if (task.IsCompletedSuccessfully) return task.Result;

                DateTimeOffset suspendedAt = DateTimeOffset.UtcNow;
                DateTimeOffset? operationDeadline = options.HostCallTimeout > TimeSpan.Zero
                    ? callStartedAt + options.HostCallTimeout : null;
                DateTimeOffset? effectiveDeadline =
                    DdfCooperativeExecutionCoordinator.EarliestDeadline(
                        options.ExecutionDeadline, operationDeadline);
                if (effectiveDeadline.HasValue &&
                    effectiveDeadline.Value <= suspendedAt)
                {
                    callCancellation.Cancel();
                    ObserveLateHostFailure(task);
                    if (options.CancellationToken.IsCancellationRequested ||
                        options.CancellationRequested?.Invoke() == true)
                        Cancel(node);
                    Fail("DDF414", "Timeout della funzione host '" + function.Name +
                        "' dopo " + options.HostCallTimeout.TotalMilliseconds.ToString(
                            CultureInfo.InvariantCulture) + " ms.", node);
                }
                string capabilityId = function.RequiredPermissions.FirstOrDefault() ??
                    "host.function";
                var frozenArguments = scriptArguments.Select(ExportHostValue)
                    .ToList().AsReadOnly();
                DdfCooperativeSuspensionRequest suspension =
                    cooperativeExecution.Suspend(capabilityId, function.Name,
                        frozenArguments, activeModule.DocumentPath, node.Start,
                        node.Length, options.HostEffectTransactionId,
                        suspendedAt, effectiveDeadline,
                        callCancellation.Token,
                        options.DebuggerSession == null ? null :
                            CaptureStateSnapshot(node));
                NotifyCooperativeState(cooperativeExecution.Snapshot);
                try
                {
                    object result = await task.WaitAsync(callCancellation.Token)
                        .ConfigureAwait(false);
                    ResumeCooperative(suspension,
                        DdfCooperativeSuspensionResultKind.Succeeded, null);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    ObserveLateHostFailure(task);
                    if (options.CancellationToken.IsCancellationRequested ||
                        options.CancellationRequested?.Invoke() == true)
                    {
                        ResumeCooperative(suspension,
                            DdfCooperativeSuspensionResultKind.Cancelled, null,
                            "DDF421", "Esecuzione annullata.");
                        Cancel(node);
                    }
                    ResumeCooperative(suspension,
                        DdfCooperativeSuspensionResultKind.TimedOut, null,
                        "DDF414", "Timeout della funzione host.");
                    Fail("DDF414", "Timeout della funzione host '" + function.Name +
                        "' dopo " + options.HostCallTimeout.TotalMilliseconds.ToString(
                            CultureInfo.InvariantCulture) + " ms.", node);
                    return null;
                }
                catch
                {
                    ResumeCooperative(suspension,
                        DdfCooperativeSuspensionResultKind.Faulted, null,
                        "DDF410", "Errore della funzione host.");
                    throw;
                }
            }

            private void ResumeCooperative(
                DdfCooperativeSuspensionRequest suspension,
                DdfCooperativeSuspensionResultKind kind, object value,
                string errorCode = null, string errorMessage = null)
            {
                var result = new DdfCooperativeSuspensionResult(
                    suspension.ExecutionId, suspension.Generation,
                    suspension.CorrelationId, kind, value, errorCode,
                    errorMessage);
                if (cooperativeExecution.TryResume(result, out
                    DdfCooperativeExecutionSnapshot snapshot))
                    NotifyCooperativeState(snapshot);
            }

            private static void ObserveLateHostFailure(Task<object> task)
            {
                if (task.IsFaulted)
                {
                    _ = task.Exception;
                    return;
                }
                if (!task.IsCompleted)
                    _ = task.ContinueWith(completed => _ = completed.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted |
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
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

            private async Task<object> EvaluateBinaryAsync(DdfIrBinary binary)
            {
                string op = binary.OperationId;
                if (op == DdfVmExpressionOperations.Assign)
                {
                    Cell target = await ResolveCellAsync(binary.Left)
                        .ConfigureAwait(false);
                    object value = ConvertForTypeName(
                        await EvaluateAsync(binary.Right).ConfigureAwait(false),
                        target.TypeName,
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
                    object value = await EvaluateAsync(binary.Left)
                        .ConfigureAwait(false);
                    options.Output?.Invoke(FormatValue(value));
                    return value;
                }
                if (op == DdfVmExpressionOperations.LogicalOr)
                {
                    object leftShort = await EvaluateAsync(binary.Left)
                        .ConfigureAwait(false);
                    return AsBoolean(leftShort, binary.Left) || AsBoolean(
                        await EvaluateAsync(binary.Right).ConfigureAwait(false),
                        binary.Right);
                }
                if (op == DdfVmExpressionOperations.LogicalAnd)
                {
                    object leftShort = await EvaluateAsync(binary.Left)
                        .ConfigureAwait(false);
                    return AsBoolean(leftShort, binary.Left) && AsBoolean(
                        await EvaluateAsync(binary.Right).ConfigureAwait(false),
                        binary.Right);
                }
                object left = await EvaluateAsync(binary.Left).ConfigureAwait(false);
                object right = await EvaluateAsync(binary.Right).ConfigureAwait(false);
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

            private async Task<Cell> ResolveCellAsync(DdfIrExpression expression)
            {
                if (expression is DdfIrName name) return FindCell(name.Name, name);
                if (expression is DdfIrIndex index)
                {
                    object target = await EvaluateAsync(index.Target)
                        .ConfigureAwait(false);
                    int offset = ToInt(await EvaluateAsync(index.Index)
                        .ConfigureAwait(false), index.Index);
                    var array = target as Cell[];
                    if (array == null) Fail("DDF405", "Il valore non è un array modificabile.", index.Target);
                    if (offset < 0 || offset >= array.Length) Fail("DDF405", "Indice array fuori intervallo: " + offset + ".", index.Index);
                    return array[offset];
                }
                if (expression is DdfIrMemberAccess member)
                {
                    var instance = await EvaluateAsync(member.Target)
                        .ConfigureAwait(false) as Dictionary<string, Cell>;
                    Cell field = null;
                    if (instance == null || !instance.TryGetValue(member.Name, out field)) Fail("DDF405", "Membro non trovato: '" + member.Name + "'.", member);
                    return field;
                }
                Fail("DDF405", "La destinazione non è modificabile.", expression);
                return null;
            }

            private async Task<object> ReadIndexAsync(DdfIrIndex index)
            {
                object target = await EvaluateAsync(index.Target)
                    .ConfigureAwait(false);
                int offset = ToInt(await EvaluateAsync(index.Index)
                    .ConfigureAwait(false), index.Index);
                if (target is string text)
                {
                    if (offset < 0 || offset >= text.Length)
                        Fail("DDF405", "Indice stringa fuori intervallo: " +
                            offset + ".", index.Index);
                    return text[offset];
                }
                if (target is Cell[] array)
                {
                    if (offset < 0 || offset >= array.Length)
                        Fail("DDF405", "Indice array fuori intervallo: " +
                            offset + ".", index.Index);
                    return ReadCell(array[offset], index);
                }
                Fail("DDF405", "Il valore non è indicizzabile.", index.Target);
                return null;
            }

            private async Task<object> CreateDefaultAsync(DdfIrType type)
            {
                if (type.ArrayLengths.Count > 0)
                {
                    int length = type.ArrayLengths[0] == null ? 0 : ToInt(
                        await EvaluateAsync(type.ArrayLengths[0]).ConfigureAwait(false),
                        type.ArrayLengths[0]);
                    if (length < 0) Fail("DDF405", "La lunghezza di un array non può essere negativa.", type);
                    var cells = new Cell[length];
                    var elementType = new DdfIrType(type.Name, new DdfIrExpression[0], type.Start, type.Length);
                    for (int index = 0; index < cells.Length; index++)
                        cells[index] = new Cell(
                            await CreateDefaultAsync(elementType).ConfigureAwait(false),
                            FormatType(elementType), DdfRuntimeVariableKind.Element);
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
                                field.Initializer == null
                                    ? await CreateDefaultAsync(field.Type)
                                        .ConfigureAwait(false)
                                    : await ConvertForTypeAsync(
                                        await EvaluateAsync(field.Initializer)
                                            .ConfigureAwait(false),
                                        field.Type, field,
                                        field.Initializer is DdfIrLiteral)
                                        .ConfigureAwait(false),
                                FormatType(field.Type), DdfRuntimeVariableKind.Field);
                        return fields;
                    }
                    finally { activeModule = previousModule; }
                }
                if (TryResolveEnum(activeModule, type.Name, true, out DdfIrEnum enumeration))
                {
                    DdfIrEnumMember first = enumeration.Members.FirstOrDefault();
                    return first == null ? null : new DdfEnumValue(enumeration.Name, first.Name);
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

            private async Task<object> ConvertForTypeAsync(object value,
                DdfIrType type, DdfIrNode node,
                bool allowCharacterLiteral = false)
            {
                if (value == null)
                    return await CreateDefaultAsync(type).ConfigureAwait(false);
                if (type.ArrayLengths.Count > 0 ||
                    TryResolveStructure(activeModule, type.Name, true, out _)) return value;
                if (TryResolveEnum(activeModule, type.Name, true, out _))
                {
                    if (value is DdfEnumValue enumeration && enumeration.TypeName == type.Name)
                        return value;
                    Fail("DDF405", "Il valore non appartiene all'enum '" + type.Name + "'.", node);
                }
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

            private void Declare(string name, object value, string typeName, DdfRuntimeVariableKind kind,
                bool writable = true, Action<object> writer = null,
                bool isFixed = false)
            {
                if (string.IsNullOrEmpty(name)) return;
                DdfRuntimeVariableKind effectiveKind = declaringGlobal &&
                    kind != DdfRuntimeVariableKind.State
                        ? DdfRuntimeVariableKind.Global
                        : kind;
                var cell = writer == null
                    ? new Cell(value, typeName,
                        effectiveKind, writable, null, isFixed)
                    : new Cell(value, typeName,
                        effectiveKind,
                        null, writer, name, isFixed);
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

            private bool TryResolveEnum(DdfIrModule module, string name,
                bool includeDependencies, out DdfIrEnum enumeration)
            {
                if (enumsByModule.TryGetValue(ModuleKey(module),
                    out Dictionary<string, DdfIrEnum> ownEnums) &&
                    ownEnums.TryGetValue(name, out enumeration)) return true;
                if (includeDependencies)
                    foreach (DdfIrModule dependency in DirectDependencies(module))
                        if (dependency.Exports.Contains(name, StringComparer.Ordinal) &&
                            enumsByModule.TryGetValue(ModuleKey(dependency),
                                out Dictionary<string, DdfIrEnum> dependencyEnums) &&
                            dependencyEnums.TryGetValue(name, out enumeration)) return true;
                enumeration = null;
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

            private static string StateKey(DdfIrModule module, string name) =>
                ModuleKey(module) + "::" + (module.RelativePath ?? string.Empty) + "::" + name;

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
                    Cancel(node);
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

            private DdfRuntimeStateSnapshot CaptureStateSnapshot(DdfIrNode currentStatement)
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
                    int start = innerIndex < frameStartsInnerFirst.Length
                        ? frameStartsInnerFirst[innerIndex] : 0;
                    int end = innerIndex == 0 ? scopes.Count :
                        innerIndex - 1 < frameStartsInnerFirst.Length
                            ? frameStartsInnerFirst[innerIndex - 1] : scopes.Count;
                    start = Math.Max(0, Math.Min(start, scopes.Count));
                    end = Math.Max(start, Math.Min(end, scopes.Count));
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
                        ref budget, pair.Value.Kind));
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
                            globals && pair.Value.Kind != DdfRuntimeVariableKind.State
                                ? DdfRuntimeVariableKind.Global
                                : pair.Value.Kind));
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
                    value is Dictionary<string, Cell> ? "{…}" : FormatValue(value), scope, kind,
                    !cell.CanWrite, cell.IsFixed)
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
                if (!DdfErrorCatalog.TryGetByCode(code, out DdfErrorDefinition definition))
                    definition = DdfErrorCatalog.InternalFailure;
                Fail(definition, message, node, domain);
            }

            private void Fail(DdfErrorDefinition definition, string message,
                DdfIrNode node,
                DdfDiagnosticDomain domain = DdfDiagnosticDomain.Runtime)
            {
                DdfDiagnostic diagnostic = CreateDiagnostic(definition.Code,
                    message, node, domain);
                IReadOnlyList<DdfRuntimeStackFrame> stackTrace =
                    CaptureStackTrace(node);
                throw new RuntimeSignal(diagnostic, node, stackTrace,
                    CreateErrorValue(definition, diagnostic, stackTrace));
            }

            private void Cancel(DdfIrNode node)
            {
                DdfDiagnostic diagnostic = CreateDiagnostic(
                    DdfErrorCatalog.ExecutionCancelled.Code,
                    "Esecuzione annullata.", node);
                IReadOnlyList<DdfRuntimeStackFrame> stackTrace =
                    CaptureStackTrace(node);
                throw new CancelledSignal(stackTrace,
                    CreateErrorValue(DdfErrorCatalog.ExecutionCancelled,
                        diagnostic, stackTrace));
            }

            private IReadOnlyList<DdfRuntimeStackFrame> CaptureStackTrace(
                DdfIrNode currentNode)
            {
                var trace = new List<DdfRuntimeStackFrame>(callStack.Count);
                for (int index = callStack.Count - 1; index >= 0; index--)
                {
                    DdfRuntimeStackFrame location = index == callStack.Count - 1 &&
                        currentNode != null
                        ? CreateFrame(callStack[index].FunctionName,
                            currentNode.Start, currentNode.Length)
                        : index < callStack.Count - 1
                            ? CreateCallerFrame(callStack[index].FunctionName,
                                callStack[index + 1])
                            : callStack[index];
                    trace.Add(location);
                }
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

            private static DdfErrorValue CreateErrorValue(
                DdfErrorDefinition definition, DdfDiagnostic diagnostic,
                IReadOnlyList<DdfRuntimeStackFrame> stackTrace)
            {
                IEnumerable<DdfErrorStackFrame> frames = (stackTrace ??
                    Array.Empty<DdfRuntimeStackFrame>()).Select(frame =>
                        new DdfErrorStackFrame(frame.FunctionName,
                            frame.DocumentPath, frame.Start, frame.Length,
                            frame.Line, frame.Column, string.Empty));
                return new DdfErrorValue(definition, diagnostic.Message,
                    diagnostic.DocumentPath, diagnostic.Start,
                    diagnostic.Length, frames);
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
                    : this(value, typeName, kind, writable, null, false) { }
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind,
                    bool writable, string externalName, bool isFixed = false)
                    : this(value, typeName, kind, null,
                        writable ? new Action<object>(_ => { }) : null, externalName,
                        isFixed) { }
                public Cell(object value, string typeName, DdfRuntimeVariableKind kind,
                    Func<object> reader, Action<object> writer, string externalName,
                    bool isFixed = false)
                {
                    this.value = value;
                    this.reader = reader;
                    this.writer = writer;
                    TypeName = typeName;
                    Kind = kind;
                    ExternalName = externalName;
                    IsFixed = isFixed;
                }
                public bool CanWrite => writer != null;
                public bool WasWritten { get; private set; }
                public object SnapshotValue => value;
                public string ExternalName { get; }
                public bool IsFixed { get; }
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
                    IReadOnlyList<DdfRuntimeStackFrame> stackTrace,
                    DdfErrorValue error) : base(diagnostic.Message)
                {
                    Diagnostic = diagnostic;
                    Node = node;
                    DdfStackTrace = stackTrace;
                    Error = error;
                }
                public DdfDiagnostic Diagnostic { get; }
                public DdfIrNode Node { get; }
                public IReadOnlyList<DdfRuntimeStackFrame> DdfStackTrace { get; }
                public DdfErrorValue Error { get; }
            }
            private sealed class ReturnSignal : Exception
            {
                public ReturnSignal(object value, DdfIrNode node) { Value = value; Node = node; }
                public object Value { get; }
                public DdfIrNode Node { get; }
            }
            private sealed class BreakSignal : Exception { }
            private sealed class ContinueSignal : Exception { }
            private sealed class EndSignal : Exception { }
            private sealed class CancelledSignal : Exception
            {
                public CancelledSignal(
                    IReadOnlyList<DdfRuntimeStackFrame> stackTrace,
                    DdfErrorValue error)
                {
                    DdfStackTrace = stackTrace;
                    Error = error;
                }

                public IReadOnlyList<DdfRuntimeStackFrame> DdfStackTrace { get; }
                public DdfErrorValue Error { get; }
            }
            private sealed class HostResourceLimitException : Exception
            {
                public HostResourceLimitException(string message) : base(message) { }
            }
        }
    }
}
