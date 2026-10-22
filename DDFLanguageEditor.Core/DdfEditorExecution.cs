using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfEditorExecutionMode
    {
        AnalysisOnly,
        Simulation,
        HostEffects
    }

    public enum DdfEditorExecutionState
    {
        Idle,
        Running,
        Stopping
    }

    /// <summary>
    /// Backend-neutral lifecycle of one cooperatively executed DDF script.
    /// It deliberately exposes no CLR task, thread or cancellation primitive.
    /// </summary>
    public enum DdfEditorCooperativeExecutionState
    {
        Idle,
        Running,
        AwaitingCapability,
        Cancelling,
        CleaningUp
    }

    public sealed class DdfEditorCooperativeSuspensionSnapshot
    {
        internal DdfEditorCooperativeSuspensionSnapshot(
            DdfCooperativeSuspensionRequest request,
            string sourceDocumentId = null) : this(request?.CorrelationId ??
                throw new ArgumentNullException(nameof(request)),
                request.CapabilityId, request.OperationId, request.Arguments,
                string.IsNullOrWhiteSpace(sourceDocumentId)
                    ? request.SourceDocumentId : sourceDocumentId,
                request.SourceStart, request.SourceLength,
                request.TransactionId, request.CreatedAt, request.Deadline)
        {
            RuntimeState = request.RuntimeState;
        }

        public DdfEditorCooperativeSuspensionSnapshot(Guid correlationId,
            string capabilityId, string operationId,
            IReadOnlyList<object> arguments, string sourceDocumentId,
            int sourceStart, int sourceLength, string transactionId = null,
            DateTimeOffset? createdAt = null, DateTimeOffset? deadline = null)
        {
            if (correlationId == Guid.Empty) throw new ArgumentException(
                "Correlation id is required.", nameof(correlationId));
            if (string.IsNullOrWhiteSpace(capabilityId)) throw new
                ArgumentException("Capability id is required.",
                    nameof(capabilityId));
            CorrelationId = correlationId;
            CapabilityId = capabilityId;
            OperationId = operationId ?? string.Empty;
            Arguments = (arguments ?? Array.Empty<object>()).Select(
                FreezeArgument).ToList().AsReadOnly();
            SourceDocumentId = sourceDocumentId ?? string.Empty;
            SourceStart = Math.Max(0, sourceStart);
            SourceLength = Math.Max(0, sourceLength);
            TransactionId = transactionId ?? string.Empty;
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
            Deadline = deadline;
        }

        private static object FreezeArgument(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType)
                return value;
            if (value is IReadOnlyDictionary<string, object> readOnly)
                return new ReadOnlyDictionary<string, object>(readOnly
                    .ToDictionary(item => item.Key,
                        item => FreezeArgument(item.Value),
                        StringComparer.Ordinal));
            if (value is IDictionary<string, object> dictionary)
                return new ReadOnlyDictionary<string, object>(dictionary
                    .ToDictionary(item => item.Key,
                        item => FreezeArgument(item.Value),
                        StringComparer.Ordinal));
            if (value is IEnumerable sequence)
                return sequence.Cast<object>().Select(FreezeArgument).ToList()
                    .AsReadOnly();
            return value.ToString() ?? string.Empty;
        }

        public Guid CorrelationId { get; }
        public string CapabilityId { get; }
        public string OperationId { get; }
        public IReadOnlyList<object> Arguments { get; }
        public string SourceDocumentId { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public string TransactionId { get; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? Deadline { get; }
        internal DdfRuntimeStateSnapshot RuntimeState { get; }
    }

    public sealed class DdfEditorCooperativeExecutionSnapshot
    {
        public DdfEditorCooperativeExecutionSnapshot(Guid executionId,
            long generation, DdfEditorCooperativeExecutionState state,
            DdfEditorCooperativeSuspensionSnapshot pendingSuspension = null)
        {
            ExecutionId = executionId;
            Generation = generation;
            State = state;
            PendingSuspension = pendingSuspension;
        }

        public Guid ExecutionId { get; }
        public long Generation { get; }
        public DdfEditorCooperativeExecutionState State { get; }
        public DdfEditorCooperativeSuspensionSnapshot PendingSuspension { get; }
        public bool IsSuspended =>
            State == DdfEditorCooperativeExecutionState.AwaitingCapability;
    }

    public sealed class DdfEditorCooperativeStateChangedEventArgs : EventArgs
    {
        public DdfEditorCooperativeStateChangedEventArgs(
            DdfEditorCooperativeExecutionSnapshot previousSnapshot,
            DdfEditorCooperativeExecutionSnapshot snapshot)
        {
            PreviousSnapshot = previousSnapshot ??
                throw new ArgumentNullException(nameof(previousSnapshot));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public DdfEditorCooperativeExecutionSnapshot PreviousSnapshot { get; }
        public DdfEditorCooperativeExecutionSnapshot Snapshot { get; }
    }

    public enum DdfEditorProcedureTrigger
    {
        Manual,
        HostEvent
    }

    public sealed class DdfEditorProcedureInvocation
    {
        internal DdfEditorProcedureInvocation(string procedureName,
            IReadOnlyDictionary<string, object> arguments,
            DdfEditorProcedureTrigger trigger, string triggerId)
        {
            ProcedureName = procedureName;
            Arguments = arguments;
            Trigger = trigger;
            TriggerId = triggerId ?? string.Empty;
        }

        public string ProcedureName { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }
        public DdfEditorProcedureTrigger Trigger { get; }
        public string TriggerId { get; }
    }

    public sealed class DdfEditorProcedureRequest
    {
        public DdfEditorProcedureRequest(string procedureName,
            IReadOnlyDictionary<string, object> arguments = null,
            DdfEditorProcedureTrigger trigger = DdfEditorProcedureTrigger.Manual,
            string triggerId = null, DdfEditorExecutionRequest execution = null)
        {
            ProcedureName = DdfHostCatalogValidation.Identifier(procedureName,
                nameof(procedureName));
            if (!Enum.IsDefined(typeof(DdfEditorProcedureTrigger), trigger))
                throw new ArgumentOutOfRangeException(nameof(trigger));
            if (trigger == DdfEditorProcedureTrigger.HostEvent &&
                string.IsNullOrWhiteSpace(triggerId))
                throw new ArgumentException(
                    "Un'invocazione da evento richiede un identificatore esplicito.",
                    nameof(triggerId));
            Trigger = trigger;
            TriggerId = (triggerId ?? string.Empty).Trim();
            Arguments = FreezeArguments(arguments);
            Execution = execution ?? new DdfEditorExecutionRequest();
        }

        public string ProcedureName { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }
        public DdfEditorProcedureTrigger Trigger { get; }
        public string TriggerId { get; }
        public DdfEditorExecutionRequest Execution { get; }

        internal static IReadOnlyDictionary<string, object> FreezeArguments(
            IReadOnlyDictionary<string, object> arguments)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> argument in arguments ??
                new Dictionary<string, object>())
            {
                string name = DdfHostCatalogValidation.Identifier(argument.Key,
                    nameof(arguments));
                if (result.ContainsKey(name)) throw new ArgumentException(
                    "Argomento procedura duplicato: " + name + ".", nameof(arguments));
                result.Add(name, FreezeValue(argument.Value));
            }
            return new ReadOnlyDictionary<string, object>(result);
        }

        private static object FreezeValue(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType)
                return value;
            if (value is IReadOnlyDictionary<string, object> readOnly)
                return new ReadOnlyDictionary<string, object>(readOnly.ToDictionary(
                    item => item.Key, item => FreezeValue(item.Value),
                    StringComparer.Ordinal));
            if (value is IDictionary<string, object> dictionary)
                return new ReadOnlyDictionary<string, object>(dictionary.ToDictionary(
                    item => item.Key, item => FreezeValue(item.Value),
                    StringComparer.Ordinal));
            if (value is IEnumerable sequence)
                return sequence.Cast<object>().Select(FreezeValue).ToList().AsReadOnly();
            return value;
        }
    }

    public sealed class DdfEditorExecutionRequest
    {
        public DdfEditorExecutionRequest(string entryPoint = null,
            string entryDocumentId = null, int maxInstructions = 0,
            Func<string> input = null,
            DdfEditorExecutionMode mode = DdfEditorExecutionMode.HostEffects,
            TimeSpan? timeout = null, TimeSpan? hostCallTimeout = null,
            int maxHostCalls = 1024, int maxHostResources = 128,
            IReadOnlyDictionary<string, object> entryArguments = null,
            IReadOnlyDictionary<string, string> databaseTransactions = null,
            DdfHostEffectPolicy hostEffectPolicy = DdfHostEffectPolicy.Immediate,
            IDdfHostEffectTransactionFactory hostEffectTransactionFactory = null)
        {
            if (!Enum.IsDefined(typeof(DdfEditorExecutionMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (hostCallTimeout.HasValue && hostCallTimeout.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(hostCallTimeout));
            if (maxHostCalls <= 0) throw new ArgumentOutOfRangeException(nameof(maxHostCalls));
            if (maxHostResources <= 0) throw new ArgumentOutOfRangeException(nameof(maxHostResources));
            if (!Enum.IsDefined(typeof(DdfHostEffectPolicy), hostEffectPolicy))
                throw new ArgumentOutOfRangeException(nameof(hostEffectPolicy));
            if (hostEffectPolicy == DdfHostEffectPolicy.RollbackOnFailure &&
                hostEffectTransactionFactory == null)
                throw new ArgumentException(
                    "RollbackOnFailure richiede una factory transazionale dell'host.",
                    nameof(hostEffectTransactionFactory));
            EntryPoint = entryPoint;
            EntryDocumentId = entryDocumentId;
            MaxInstructions = maxInstructions;
            Input = input;
            Mode = mode;
            Timeout = timeout;
            HostCallTimeout = hostCallTimeout ?? TimeSpan.FromSeconds(30);
            MaxHostCalls = maxHostCalls;
            MaxHostResources = maxHostResources;
            EntryArguments = DdfEditorProcedureRequest.FreezeArguments(entryArguments);
            DatabaseTransactions = FreezeTransactions(databaseTransactions);
            HostEffectPolicy = hostEffectPolicy;
            HostEffectTransactionFactory = hostEffectTransactionFactory;
        }

        public string EntryPoint { get; }
        public string EntryDocumentId { get; }
        public int MaxInstructions { get; }
        public Func<string> Input { get; }
        public DdfEditorExecutionMode Mode { get; }
        public TimeSpan? Timeout { get; }
        public TimeSpan HostCallTimeout { get; }
        public int MaxHostCalls { get; }
        public int MaxHostResources { get; }
        public IReadOnlyDictionary<string, object> EntryArguments { get; }
        public IReadOnlyDictionary<string, string> DatabaseTransactions { get; }
        public DdfHostEffectPolicy HostEffectPolicy { get; }
        public IDdfHostEffectTransactionFactory HostEffectTransactionFactory { get; }

        internal DdfEditorDebugBinding DebugBinding { get; set; }
        internal DdfEditorProcedureInvocation ProcedureInvocation { get; set; }

        private static IReadOnlyDictionary<string, string> FreezeTransactions(
            IReadOnlyDictionary<string, string> transactions)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> transaction in transactions ??
                new Dictionary<string, string>())
            {
                string databaseId = DdfHostCatalogValidation.Identifier(
                    transaction.Key, nameof(transactions));
                string transactionId = (transaction.Value ?? string.Empty).Trim();
                if (transactionId.Length == 0) throw new ArgumentException(
                    "L'identificatore transazione è obbligatorio.",
                    nameof(transactions));
                result.Add(databaseId, transactionId);
            }
            return new ReadOnlyDictionary<string, string>(result);
        }
    }

    public sealed class DdfEditorExecutionBuildContext
    {
        public DdfEditorExecutionBuildContext(DdfBuildResult result,
            DdfLanguagePackage languagePackage,
            IReadOnlyDictionary<string, string> runtimePathsByDocumentId = null)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            LanguagePackage = languagePackage;
            RuntimePathsByDocumentId = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(runtimePathsByDocumentId ??
                    new Dictionary<string, string>(), StringComparer.Ordinal));
        }

        public DdfBuildResult Result { get; }
        public DdfLanguagePackage LanguagePackage { get; }
        public IReadOnlyDictionary<string, string> RuntimePathsByDocumentId { get; }
    }

    /// <summary>
    /// Collega al runtime pubblico un modello di progetto posseduto dall'host.
    /// Viene invocato sul thread chiamante prima dell'avvio del worker.
    /// </summary>
    public delegate DdfEditorExecutionBuildContext DdfEditorExecutionBuildHandler(
        DdfEditorExecutionRequest request,
        IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
        DdfLanguagePackage languagePackage,
        DdfHostCatalogSnapshot hostCatalog);

    internal sealed class DdfEditorDebugBinding
    {
        public DdfDebuggerSession Session { get; set; }
        public IReadOnlyList<DdfEditorBreakpoint> Breakpoints { get; set; } =
            Array.Empty<DdfEditorBreakpoint>();
        public Action<DdfDebugPauseInfo, string,
            IReadOnlyDictionary<string, string>> Paused { get; set; }
    }

    public sealed class DdfEditorExecutionOutputEventArgs : EventArgs
    {
        public DdfEditorExecutionOutputEventArgs(long generation, string text)
        {
            Generation = generation;
            Text = text ?? string.Empty;
        }

        public long Generation { get; }
        public string Text { get; }
    }

    public sealed class DdfEditorExecutionStateChangedEventArgs : EventArgs
    {
        public DdfEditorExecutionStateChangedEventArgs(long generation,
            DdfEditorExecutionState previousState, DdfEditorExecutionState state)
        {
            Generation = generation;
            PreviousState = previousState;
            State = state;
        }

        public long Generation { get; }
        public DdfEditorExecutionState PreviousState { get; }
        public DdfEditorExecutionState State { get; }
    }

    public sealed class DdfEditorExecutionCompletion
    {
        public DdfEditorExecutionCompletion(long generation, DateTimeOffset startedAt,
            DateTimeOffset completedAt, DdfExecutionResult executionResult,
            IReadOnlyList<DdfBuildIssue> buildIssues, string internalError,
            DdfEditorExecutionMode mode, DdfBuildArtifact artifact = null)
        {
            Generation = generation;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            ExecutionResult = executionResult;
            BuildIssues = buildIssues ?? Array.Empty<DdfBuildIssue>();
            InternalError = internalError ?? string.Empty;
            Mode = mode;
            Artifact = artifact;
            HostEffects = new DdfHostEffectSummary(DdfHostEffectPolicy.Immediate,
                DdfHostEffectDisposition.None, Array.Empty<DdfHostEffectRecord>());
        }

        public long Generation { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset CompletedAt { get; }
        public DdfExecutionResult ExecutionResult { get; }
        public IReadOnlyList<DdfBuildIssue> BuildIssues { get; }
        public string InternalError { get; }
        public DdfEditorExecutionMode Mode { get; }
        public DdfBuildArtifact Artifact { get; }
        public DdfEditorProcedureInvocation ProcedureInvocation { get; internal set; }
        public DdfHostEffectSummary HostEffects { get; internal set; }
        public IReadOnlyList<DdfHostValueResult> HostValues =>
            ExecutionResult?.HostValues ?? Array.Empty<DdfHostValueResult>();
        public bool WasCancelled => ExecutionResult?.WasCancelled == true;
        public bool Succeeded => (Mode == DdfEditorExecutionMode.AnalysisOnly ||
                                  ExecutionResult?.Succeeded == true) &&
                                 BuildIssues.All(item => item.Severity != DdfDiagnosticSeverity.Error) &&
                                 string.IsNullOrEmpty(InternalError);
    }

    public sealed class DdfEditorExecutionCompletedEventArgs : EventArgs
    {
        public DdfEditorExecutionCompletedEventArgs(DdfEditorExecutionCompletion completion)
        {
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        }

        public DdfEditorExecutionCompletion Completion { get; }
    }

    public enum DdfEditorLifecycleOutcome
    {
        Completed,
        AlreadyIdle,
        TimedOut
    }

    public sealed class DdfEditorLifecycleResult
    {
        public DdfEditorLifecycleResult(long generation,
            DdfEditorLifecycleOutcome outcome,
            DdfEditorExecutionCompletion completion = null,
            string errorCode = null, string message = null)
        {
            Generation = generation;
            Outcome = outcome;
            Completion = completion;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public long Generation { get; }
        public DdfEditorLifecycleOutcome Outcome { get; }
        public DdfEditorExecutionCompletion Completion { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public bool Succeeded => Outcome is DdfEditorLifecycleOutcome.Completed or
            DdfEditorLifecycleOutcome.AlreadyIdle;
    }

    public interface IDdfEditorExecutionService : IDisposable
    {
        DdfEditorExecutionState State { get; }
        bool IsRunning { get; }
        long Generation { get; }
        DdfEditorExecutionCompletion LastCompletion { get; }

        event EventHandler<DdfEditorExecutionStateChangedEventArgs> StateChanged;
        event EventHandler<DdfEditorExecutionOutputEventArgs> OutputReceived;
        event EventHandler<DdfEditorExecutionCompletedEventArgs> ExecutionCompleted;

        Task<DdfEditorExecutionCompletion> RunAsync(
            DdfEditorExecutionRequest request = null);
        Task<DdfEditorExecutionCompletion> InvokeAsync(
            DdfEditorProcedureRequest request);
        [DdfEditorDeprecatedApi(DdfEditorApiLifecycleCatalog.ExecutionStopId,
            "0.41", DdfEditorApiLifecycleCatalog.EarliestBreakingContractVersion,
            "DDFLanguageEditor.Core.IDdfEditorAsyncLifecycleService.StopAsync(TimeSpan,CancellationToken)",
            "Negoziare editor.execution.async-lifecycle e attendere StopAsync; usare Stop soltanto come fallback legacy.")]
        bool Stop();
    }

    /// <summary>
    /// Optional SDK 0.41 capability. Hosts can discover it with a type check;
    /// existing implementations of IDdfEditorExecutionService remain valid.
    /// </summary>
    public interface IDdfEditorCooperativeExecutionService
    {
        DdfEditorCooperativeExecutionSnapshot CooperativeSnapshot { get; }
        event EventHandler<DdfEditorCooperativeStateChangedEventArgs>
            CooperativeStateChanged;
    }

    /// <summary>
    /// Optional SDK 0.41 lifecycle capability. It observes cooperative stop and
    /// shutdown without adding members to the legacy execution interfaces.
    /// </summary>
    public interface IDdfEditorAsyncLifecycleService
    {
        DdfEditorLifecycleResult LastLifecycleResult { get; }
        Task<DdfEditorLifecycleResult> StopAsync(TimeSpan timeout,
            CancellationToken cancellationToken = default);
        Task<DdfEditorLifecycleResult> DisposeAsync(TimeSpan timeout,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional additive SDK capability for the committed state owned by one
    /// editor/host instance. Existing execution-service implementations remain valid.
    /// </summary>
    public interface IDdfEditorScriptStateService
    {
        IReadOnlyDictionary<string, object> CaptureScriptState();
        void ResetScriptState();
    }

    public sealed class DdfEditorScriptStateEntry
    {
        public DdfEditorScriptStateEntry(string key, string typeName, object value)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 2048)
                throw new ArgumentException("La chiave state non è valida.", nameof(key));
            if (typeName is not ("int" or "float" or "char" or "bool" or "string"))
                throw new ArgumentException("Il tipo state persistito non è supportato.",
                    nameof(typeName));
            bool valid = typeName switch
            {
                "int" => value is int,
                "float" => value is double number && !double.IsNaN(number) &&
                    !double.IsInfinity(number),
                "char" => value is char,
                "bool" => value is bool,
                "string" => value is string text && text.Length <= 1048576,
                _ => false
            };
            if (!valid) throw new ArgumentException(
                "Il valore state non corrisponde al tipo dichiarato.", nameof(value));
            Key = key;
            TypeName = typeName;
            Value = value;
        }

        public string Key { get; }
        public string TypeName { get; }
        public object Value { get; }
    }

    public sealed class DdfEditorScriptStateSnapshot
    {
        public const string CurrentSchemaVersion = "1.0";

        public DdfEditorScriptStateSnapshot(string schemaVersion, string languageId,
            string languagePackageVersion,
            IEnumerable<DdfEditorScriptStateEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion))
                throw new ArgumentException("La versione schema state è obbligatoria.",
                    nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(languageId))
                throw new ArgumentException("L'identità del linguaggio è obbligatoria.",
                    nameof(languageId));
            if (string.IsNullOrWhiteSpace(languagePackageVersion))
                throw new ArgumentException("La versione del pacchetto è obbligatoria.",
                    nameof(languagePackageVersion));
            DdfEditorScriptStateEntry[] materialized = (entries ??
                    Enumerable.Empty<DdfEditorScriptStateEntry>()).ToArray();
            long payloadSize = materialized.Sum(item => item == null ? 0L :
                item.Key.Length + item.TypeName.Length +
                (item.Value is string text ? text.Length : 16));
            if (materialized.Length > 4096 || materialized.Any(item => item == null) ||
                materialized.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() !=
                    materialized.Length || payloadSize > 4L * 1024 * 1024)
                throw new ArgumentException(
                    "Lo snapshot state supera i budget o contiene voci nulle o duplicate.",
                    nameof(entries));
            SchemaVersion = schemaVersion;
            LanguageId = languageId;
            LanguagePackageVersion = languagePackageVersion;
            Entries = Array.AsReadOnly(materialized);
        }

        public string SchemaVersion { get; }
        public string LanguageId { get; }
        public string LanguagePackageVersion { get; }
        public IReadOnlyList<DdfEditorScriptStateEntry> Entries { get; }
    }

    /// <summary>Host-owned storage. Save and Clear must be atomic.</summary>
    public interface IDdfEditorScriptStatePersistenceProvider
    {
        DdfEditorScriptStateSnapshot Load();
        void Save(DdfEditorScriptStateSnapshot snapshot);
        void Clear();
    }

    /// <summary>Optional host migration for an older schema or language package.</summary>
    public interface IDdfEditorScriptStateMigrationProvider
    {
        DdfEditorScriptStateSnapshot Migrate(DdfEditorScriptStateSnapshot snapshot,
            string targetSchemaVersion, string targetLanguageId,
            string targetLanguagePackageVersion);
    }

    public sealed class DdfInMemoryEditorScriptStatePersistenceProvider :
        IDdfEditorScriptStatePersistenceProvider
    {
        private readonly object syncRoot = new object();
        private DdfEditorScriptStateSnapshot state;

        public DdfEditorScriptStateSnapshot Load()
        {
            lock (syncRoot) return state;
        }

        public void Save(DdfEditorScriptStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (syncRoot) state = snapshot;
        }

        public void Clear()
        {
            lock (syncRoot) state = null;
        }
    }

    public interface IDdfEditorScriptStatePersistenceService
    {
        IDdfEditorScriptStatePersistenceProvider ScriptStatePersistenceProvider { get; }
        void AttachScriptStatePersistenceProvider(
            IDdfEditorScriptStatePersistenceProvider provider);
        bool DetachScriptStatePersistenceProvider();
        DdfEditorScriptStateSnapshot CapturePersistableScriptState();
        void SavePersistedScriptState();
        bool RestorePersistedScriptState();
        void ResetPersistedScriptState();
    }

    /// <summary>
    /// Per-host execution service. It snapshots the supplied in-memory documents,
    /// builds them without reading or writing source files and runs the resulting IR
    /// on a dedicated worker. Observable events are delivered through the
    /// host-selected dispatcher; the compatibility default delivers them inline.
    /// </summary>
    public sealed class DdfEditorExecutionService : IDdfEditorExecutionService,
        IDdfEditorCooperativeExecutionService,
        IDdfEditorScriptStateService, IDdfEditorScriptStatePersistenceService,
        IDdfEditorAsyncLifecycleService, IAsyncDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Func<IDdfEditorDocumentService> documentServiceProvider;
        private readonly Func<IDdfLanguagePackageSession> languageSessionProvider;
        private readonly Func<DdfHostCatalogSnapshot> hostCatalogProvider;
        private readonly DdfEditorExecutionBuildHandler buildHandler;
        private readonly DdfStateStore scriptStateStore;
        private readonly IDdfEditorEventDispatcher eventDispatcher;
        private readonly IDdfEditorExecutionScheduler executionScheduler;
        private readonly IDdfEditorUiCapabilityInvoker uiCapabilityInvoker;
        private readonly bool ownsUiCapabilityInvoker;
        private IDdfEditorScriptStatePersistenceProvider scriptStatePersistenceProvider;
        private bool statePersistenceBusy;
        private CancellationTokenSource cancellation;
        private TaskCompletionSource<DdfEditorExecutionCompletion> activeCompletion;
        private DdfEditorExecutionState state;
        private DdfEditorCooperativeExecutionSnapshot cooperativeSnapshot =
            new(Guid.Empty, 0, DdfEditorCooperativeExecutionState.Idle);
        private long generation;
        private bool disposed;
        private bool callbacksSuppressed;

        public DdfEditorExecutionService(IDdfEditorDocumentService documentService,
            IDdfLanguagePackageSession languageSession)
            : this(() => documentService, () => languageSession, null, null, null,
                null)
        {
            if (documentService == null) throw new ArgumentNullException(nameof(documentService));
            if (languageSession == null) throw new ArgumentNullException(nameof(languageSession));
        }

        public DdfEditorExecutionService(
            Func<IDdfEditorDocumentService> documentServiceProvider,
            Func<IDdfLanguagePackageSession> languageSessionProvider,
            Func<DdfHostCatalogSnapshot> hostCatalogProvider = null,
            DdfEditorExecutionBuildHandler buildHandler = null,
            DdfStateStore scriptStateStore = null,
            IDdfEditorEventDispatcher eventDispatcher = null,
            IDdfEditorExecutionScheduler executionScheduler = null,
            IDdfEditorUiCapabilityInvoker uiCapabilityInvoker = null)
        {
            this.documentServiceProvider = documentServiceProvider ??
                throw new ArgumentNullException(nameof(documentServiceProvider));
            this.languageSessionProvider = languageSessionProvider ??
                throw new ArgumentNullException(nameof(languageSessionProvider));
            this.hostCatalogProvider = hostCatalogProvider;
            this.buildHandler = buildHandler;
            this.scriptStateStore = scriptStateStore ?? new DdfStateStore();
            this.eventDispatcher = eventDispatcher ??
                DdfEditorInlineEventDispatcher.Instance;
            this.executionScheduler = executionScheduler ??
                DdfEditorDedicatedExecutionScheduler.Instance;
            this.uiCapabilityInvoker = uiCapabilityInvoker ??
                new DdfEditorUiCapabilityInvoker(this.eventDispatcher);
            ownsUiCapabilityInvoker = uiCapabilityInvoker == null;
        }

        public IDdfEditorEventDispatcher EventDispatcher => eventDispatcher;
        public IDdfEditorExecutionScheduler ExecutionScheduler => executionScheduler;
        public IDdfEditorUiCapabilityInvoker UiCapabilityInvoker => uiCapabilityInvoker;

        public DdfEditorExecutionState State
        {
            get { lock (syncRoot) return state; }
        }

        public bool IsRunning => State != DdfEditorExecutionState.Idle;

        public DdfEditorCooperativeExecutionSnapshot CooperativeSnapshot
        {
            get { lock (syncRoot) return cooperativeSnapshot; }
        }

        public long Generation
        {
            get { lock (syncRoot) return generation; }
        }

        public DdfEditorExecutionCompletion LastCompletion { get; private set; }
        public DdfEditorLifecycleResult LastLifecycleResult { get; private set; }

        public IDdfEditorScriptStatePersistenceProvider ScriptStatePersistenceProvider
        {
            get { lock (syncRoot) return scriptStatePersistenceProvider; }
        }

        public event EventHandler<DdfEditorExecutionStateChangedEventArgs> StateChanged;
        public event EventHandler<DdfEditorCooperativeStateChangedEventArgs>
            CooperativeStateChanged;
        public event EventHandler<DdfEditorExecutionOutputEventArgs> OutputReceived;
        public event EventHandler<DdfEditorExecutionCompletedEventArgs> ExecutionCompleted;

        public Task<DdfEditorExecutionCompletion> RunAsync(
            DdfEditorExecutionRequest request = null)
        {
            request = request ?? new DdfEditorExecutionRequest();
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents = CaptureDocuments();
            DdfLanguagePackage package = languageSessionProvider()?.ActivePackage;
            DdfHostCatalogSnapshot hostCatalog = hostCatalogProvider?.Invoke();
            DdfEditorExecutionBuildContext preparedBuild = null;
            if (buildHandler != null)
            {
                try
                {
                    preparedBuild = buildHandler(request, documents, package,
                        hostCatalog) ?? new DdfEditorExecutionBuildContext(
                            DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                                "DDFH004", "L'esecutore host non ha restituito un contesto di build.")),
                            package);
                }
                catch (Exception exception)
                {
                    preparedBuild = new DdfEditorExecutionBuildContext(
                        DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                            "DDFH003", "Errore interno della preparazione host: " +
                            exception.Message)), package);
                }
            }
            long currentGeneration;
            Guid executionId = Guid.NewGuid();
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            CancellationTokenSource currentCancellation;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (statePersistenceBusy)
                    throw new InvalidOperationException(
                        "La persistenza dello stato dello script è in corso.");
                if (state != DdfEditorExecutionState.Idle)
                    throw new InvalidOperationException("Una sessione di esecuzione è già attiva.");
                currentGeneration = ++generation;
                currentCancellation = new CancellationTokenSource();
                if (request.Timeout.HasValue)
                    currentCancellation.CancelAfter(request.Timeout.Value);
                cancellation = currentCancellation;
                activeCompletion = new TaskCompletionSource<DdfEditorExecutionCompletion>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                state = DdfEditorExecutionState.Running;
            }
            RaiseStateChanged(currentGeneration, DdfEditorExecutionState.Idle,
                DdfEditorExecutionState.Running);
            UpdateCooperativeSnapshot(new DdfEditorCooperativeExecutionSnapshot(
                executionId, currentGeneration,
                DdfEditorCooperativeExecutionState.Running));

            int callingThreadId = Environment.CurrentManagedThreadId;
            SynchronizationContext callingContext = SynchronizationContext.Current;
            Func<string, Task<DdfEditorExecutionCompletion>> operation =
                schedulingFailure => ExecuteAsync(currentGeneration, startedAt,
                    documents, package, request, currentCancellation, hostCatalog,
                    preparedBuild, executionId, callingThreadId, callingContext,
                    schedulingFailure);
            try
            {
                Task<DdfEditorExecutionCompletion> scheduled =
                    executionScheduler.ScheduleAsync(() => operation(null));
                return scheduled == null
                    ? RecoverSchedulingFailureAsync(operation,
                        "Lo scheduler host ha restituito un task nullo.")
                    : ObserveScheduledExecutionAsync(scheduled, operation);
            }
            catch (Exception exception)
            {
                return RecoverSchedulingFailureAsync(operation,
                    "Lo scheduler host ha rifiutato l'esecuzione: " +
                    exception.Message);
            }
        }

        public Task<DdfEditorExecutionCompletion> InvokeAsync(
            DdfEditorProcedureRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            DdfEditorExecutionRequest options = request.Execution;
            var invocation = new DdfEditorProcedureInvocation(request.ProcedureName,
                request.Arguments, request.Trigger, request.TriggerId);
            var executionRequest = new DdfEditorExecutionRequest(
                request.ProcedureName, options.EntryDocumentId,
                options.MaxInstructions, options.Input, options.Mode,
                options.Timeout, options.HostCallTimeout, options.MaxHostCalls,
                options.MaxHostResources, request.Arguments,
                options.DatabaseTransactions, options.HostEffectPolicy,
                options.HostEffectTransactionFactory)
            {
                ProcedureInvocation = invocation
            };
            return RunAsync(executionRequest);
        }

        public bool Stop()
        {
            long currentGeneration;
            DdfEditorExecutionState previousState;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Running) return false;
                previousState = state;
                state = DdfEditorExecutionState.Stopping;
                currentGeneration = generation;
                cancellation?.Cancel();
            }
            RaiseStateChanged(currentGeneration, previousState,
                DdfEditorExecutionState.Stopping);
            return true;
        }

        public Task<DdfEditorLifecycleResult> StopAsync(TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            StopOrDisposeAsync(timeout, cancellationToken, dispose: false);

        public Task<DdfEditorLifecycleResult> DisposeAsync(TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            StopOrDisposeAsync(timeout, cancellationToken, dispose: true);

        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        public IReadOnlyDictionary<string, object> CaptureScriptState() =>
            scriptStateStore.Snapshot();

        public void ResetScriptState()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle || statePersistenceBusy)
                    throw new InvalidOperationException(
                        "Lo stato dello script può essere azzerato soltanto a esecuzione ferma.");
                scriptStateStore.Reset();
            }
        }

        public void AttachScriptStatePersistenceProvider(
            IDdfEditorScriptStatePersistenceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle || statePersistenceBusy)
                    throw new InvalidOperationException(
                        "Il provider state può essere sostituito soltanto a esecuzione ferma.");
                scriptStatePersistenceProvider = provider;
            }
        }

        public bool DetachScriptStatePersistenceProvider()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle || statePersistenceBusy)
                    throw new InvalidOperationException(
                        "Il provider state può essere revocato soltanto a esecuzione ferma.");
                if (scriptStatePersistenceProvider == null) return false;
                scriptStatePersistenceProvider = null;
                return true;
            }
        }

        public DdfEditorScriptStateSnapshot CapturePersistableScriptState()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle || statePersistenceBusy)
                    throw new InvalidOperationException(
                        "Lo snapshot persistibile dello state richiede un'esecuzione ferma.");
                DdfLanguagePackage package = languageSessionProvider()?.ActivePackage ??
                    throw new InvalidOperationException(
                        "Nessun pacchetto linguaggio attivo per lo snapshot state.");
                return CreatePersistableState(package);
            }
        }

        public void SavePersistedScriptState()
        {
            IDdfEditorScriptStatePersistenceProvider provider = BeginStatePersistence();
            try
            {
                DdfLanguagePackage package = languageSessionProvider()?.ActivePackage ??
                    throw new InvalidOperationException(
                        "Nessun pacchetto linguaggio attivo per il salvataggio state.");
                provider.Save(CreatePersistableState(package));
            }
            finally { EndStatePersistence(); }
        }

        public bool RestorePersistedScriptState()
        {
            IDdfEditorScriptStatePersistenceProvider provider = BeginStatePersistence();
            try
            {
                DdfLanguagePackage package = languageSessionProvider()?.ActivePackage ??
                    throw new InvalidOperationException(
                        "Nessun pacchetto linguaggio attivo per il ripristino state.");
                DdfEditorScriptStateSnapshot snapshot = provider.Load();
                if (snapshot == null) return false;
                if (!MatchesCurrentStateContract(snapshot, package))
                {
                    if (provider is not IDdfEditorScriptStateMigrationProvider migrator)
                        throw new InvalidOperationException(
                            "Lo snapshot state richiede una migrazione esplicita dell'host.");
                    snapshot = migrator.Migrate(snapshot,
                        DdfEditorScriptStateSnapshot.CurrentSchemaVersion,
                        package.Id, package.PackageVersion) ??
                        throw new InvalidOperationException(
                            "Il provider di migrazione non ha restituito uno snapshot state.");
                }
                if (!MatchesCurrentStateContract(snapshot, package))
                    throw new InvalidOperationException(
                        "Lo snapshot state migrato non corrisponde al contratto corrente.");
                var entries = snapshot.Entries.Select(item => new DdfStateStoreEntry(
                    item.Key, item.TypeName, item.Value)).ToList();
                scriptStateStore.ReplaceEntries(entries);
                return true;
            }
            finally { EndStatePersistence(); }
        }

        public void ResetPersistedScriptState()
        {
            IDdfEditorScriptStatePersistenceProvider provider = BeginStatePersistence();
            try
            {
                provider.Clear();
                scriptStateStore.Reset();
            }
            finally { EndStatePersistence(); }
        }

        private DdfEditorScriptStateSnapshot CreatePersistableState(
            DdfLanguagePackage package)
        {
            IReadOnlyList<DdfEditorScriptStateEntry> entries = scriptStateStore
                .SnapshotEntries().Select(item => new DdfEditorScriptStateEntry(
                    item.Key, item.TypeName, item.Value)).ToList().AsReadOnly();
            return new DdfEditorScriptStateSnapshot(
                DdfEditorScriptStateSnapshot.CurrentSchemaVersion,
                package.Id, package.PackageVersion, entries);
        }

        private static bool MatchesCurrentStateContract(
            DdfEditorScriptStateSnapshot snapshot, DdfLanguagePackage package) =>
            snapshot.SchemaVersion == DdfEditorScriptStateSnapshot.CurrentSchemaVersion &&
            snapshot.LanguageId == package.Id &&
            snapshot.LanguagePackageVersion == package.PackageVersion;

        private IDdfEditorScriptStatePersistenceProvider BeginStatePersistence()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle || statePersistenceBusy)
                    throw new InvalidOperationException(
                        "La persistenza state richiede un'esecuzione ferma.");
                IDdfEditorScriptStatePersistenceProvider provider =
                    scriptStatePersistenceProvider ?? throw new InvalidOperationException(
                        "Nessun provider di persistenza state è associato all'istanza.");
                statePersistenceBusy = true;
                return provider;
            }
        }

        private void EndStatePersistence()
        {
            lock (syncRoot) statePersistenceBusy = false;
        }

        public void Dispose()
        {
            BeginDisposal();
        }

        private async Task<DdfEditorLifecycleResult> StopOrDisposeAsync(
            TimeSpan timeout, CancellationToken cancellationToken,
            bool dispose)
        {
            if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            Task<DdfEditorExecutionCompletion> pending;
            long currentGeneration;
            DdfEditorExecutionCompletion completed;
            bool stateChanged = false;
            DdfEditorExecutionState previousState = DdfEditorExecutionState.Idle;
            lock (syncRoot)
            {
                if (!dispose) ThrowIfDisposed();
                currentGeneration = generation;
                completed = LastCompletion;
                pending = activeCompletion?.Task;
                if (state == DdfEditorExecutionState.Running)
                {
                    previousState = state;
                    state = DdfEditorExecutionState.Stopping;
                    stateChanged = true;
                }
                if (dispose)
                {
                    disposed = true;
                    callbacksSuppressed = true;
                }
                cancellation?.Cancel();
            }
            if (dispose && ownsUiCapabilityInvoker &&
                uiCapabilityInvoker is IDisposable disposable)
                disposable.Dispose();
            if (stateChanged && !dispose)
                RaiseStateChanged(currentGeneration, previousState,
                    DdfEditorExecutionState.Stopping);
            if (pending == null)
                return SetLifecycleResult(new DdfEditorLifecycleResult(
                    currentGeneration, DdfEditorLifecycleOutcome.AlreadyIdle,
                    completed));
            try
            {
                DdfEditorExecutionCompletion completion = timeout ==
                    Timeout.InfiniteTimeSpan
                    ? await pending.WaitAsync(cancellationToken).ConfigureAwait(false)
                    : await pending.WaitAsync(timeout, cancellationToken)
                        .ConfigureAwait(false);
                return SetLifecycleResult(new DdfEditorLifecycleResult(
                    currentGeneration, DdfEditorLifecycleOutcome.Completed,
                    completion));
            }
            catch (TimeoutException)
            {
                return SetLifecycleResult(new DdfEditorLifecycleResult(
                    currentGeneration, DdfEditorLifecycleOutcome.TimedOut,
                    errorCode: "DDFH005",
                    message: "Il cleanup dell'esecuzione non è terminato entro la scadenza. Usare un worker fuori processo per provider non cooperativi."));
            }
        }

        private void BeginDisposal()
        {
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                callbacksSuppressed = true;
                if (state == DdfEditorExecutionState.Running)
                    state = DdfEditorExecutionState.Stopping;
                cancellation?.Cancel();
            }
            if (ownsUiCapabilityInvoker && uiCapabilityInvoker is IDisposable disposable)
                disposable.Dispose();
        }

        private DdfEditorLifecycleResult SetLifecycleResult(
            DdfEditorLifecycleResult result)
        {
            lock (syncRoot) LastLifecycleResult = result;
            return result;
        }

        private async Task<DdfEditorExecutionCompletion> ExecuteAsync(
            long currentGeneration,
            DateTimeOffset startedAt, IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfEditorExecutionRequest request,
            CancellationTokenSource currentCancellation,
            DdfHostCatalogSnapshot hostCatalog,
            DdfEditorExecutionBuildContext preparedBuild, Guid executionId,
            int callingThreadId, SynchronizationContext callingContext,
            string schedulingFailure = null)
        {
            DdfExecutionResult executionResult = null;
            IReadOnlyList<DdfBuildIssue> issues = Array.Empty<DdfBuildIssue>();
            string internalError = string.Empty;
            DdfBuildArtifact artifact = null;
            var effectTracker = new DdfHostEffectTracker();
            var attempt = new ExecutionAttemptState();
            DdfStateStore workingState = scriptStateStore.Fork();
            try
            {
                if (!string.IsNullOrEmpty(schedulingFailure))
                    throw new InvalidOperationException(schedulingFailure);
                if (Environment.CurrentManagedThreadId == callingThreadId ||
                    (callingContext != null && ReferenceEquals(
                        SynchronizationContext.Current, callingContext)))
                    throw new InvalidOperationException(
                        "Lo scheduler host ha tentato di eseguire codice DDF sul thread chiamante/UI.");
                if (preparedBuild != null)
                {
                    package = preparedBuild.LanguagePackage ?? package;
                    DdfBuildResult build = preparedBuild.Result;
                    issues = build.Issues;
                    artifact = build.Artifact;
                    if (build.Success && request.Mode != DdfEditorExecutionMode.AnalysisOnly)
                        executionResult = await ExecuteArtifactAsync(
                            currentGeneration, startedAt,
                            request, currentCancellation, hostCatalog, executionId,
                            effectTracker, artifact, package,
                            preparedBuild.RuntimePathsByDocumentId,
                            attempt, workingState)
                            .ConfigureAwait(false);
                }
                else if (package == null)
                    issues = new[] { DdfBuildIssue.Configuration("DDFH002",
                        "Nessun pacchetto linguaggio associato alla sessione host.") };
                else if (documents.Count == 0)
                    issues = new[] { DdfBuildIssue.Configuration("DDFH001",
                        "Nessun documento fornito dall'applicazione host.") };
                else
                {
                    var buildRequest = new DdfEditorBuildRequest(request.EntryPoint,
                        request.EntryDocumentId, request.MaxInstructions);
                    DdfBuildResult build = DdfEditorHostBuildPipeline.Build(
                        documents, package, buildRequest,
                        out IReadOnlyDictionary<string, string> runtimePathsById,
                        hostCatalog);
                    issues = build.Issues;
                    artifact = build.Artifact;
                    if (build.Success && request.Mode != DdfEditorExecutionMode.AnalysisOnly)
                    {
                        executionResult = await ExecuteArtifactAsync(
                            currentGeneration, startedAt,
                            request, currentCancellation, hostCatalog, executionId,
                            effectTracker, artifact, package,
                            runtimePathsById, attempt, workingState)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                internalError = exception.Message;
            }

            DdfHostEffectSummary hostEffects = FinalizeHostEffects(request,
                executionResult, effectTracker, attempt.EffectTransaction,
                attempt.TransactionBeginAttempted, attempt.RuntimeAttempted,
                ref internalError);

            if (request.Mode == DdfEditorExecutionMode.HostEffects &&
                executionResult?.Succeeded == true && string.IsNullOrEmpty(internalError))
                scriptStateStore.ReplaceWith(workingState);

            var completion = new DdfEditorExecutionCompletion(currentGeneration,
                startedAt, DateTimeOffset.UtcNow, executionResult, issues, internalError,
                request.Mode, artifact)
            {
                ProcedureInvocation = request.ProcedureInvocation,
                HostEffects = hostEffects
            };
            DdfEditorExecutionState previousState;
            TaskCompletionSource<DdfEditorExecutionCompletion> lifecycleCompletion;
            lock (syncRoot)
            {
                previousState = state;
                if (ReferenceEquals(cancellation, currentCancellation))
                    cancellation = null;
                state = DdfEditorExecutionState.Idle;
                LastCompletion = completion;
                lifecycleCompletion = activeCompletion;
                activeCompletion = null;
            }
            lifecycleCompletion?.TrySetResult(completion);
            currentCancellation.Dispose();
            UpdateCooperativeSnapshot(new DdfEditorCooperativeExecutionSnapshot(
                Guid.Empty, currentGeneration,
                DdfEditorCooperativeExecutionState.Idle));
            Dispatch(() => ExecutionCompleted?.Invoke(this,
                new DdfEditorExecutionCompletedEventArgs(completion)));
            RaiseStateChanged(currentGeneration, previousState,
                DdfEditorExecutionState.Idle);
            return completion;
        }

        private Task<DdfEditorExecutionCompletion> RecoverSchedulingFailureAsync(
            Func<string, Task<DdfEditorExecutionCompletion>> operation,
            string message)
        {
            return DdfEditorDedicatedExecutionScheduler.Instance.ScheduleAsync(
                () => operation(message));
        }

        private async Task<DdfEditorExecutionCompletion>
            ObserveScheduledExecutionAsync(
                Task<DdfEditorExecutionCompletion> scheduled,
                Func<string, Task<DdfEditorExecutionCompletion>> operation)
        {
            try
            {
                return await scheduled.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return await RecoverSchedulingFailureAsync(operation,
                    "Lo scheduler host ha interrotto l'esecuzione: " +
                    exception.Message).ConfigureAwait(false);
            }
        }

        private async Task<DdfExecutionResult> ExecuteArtifactAsync(
            long currentGeneration,
            DateTimeOffset startedAt, DdfEditorExecutionRequest request,
            CancellationTokenSource currentCancellation,
            DdfHostCatalogSnapshot hostCatalog, Guid executionId,
            DdfHostEffectTracker effectTracker, DdfBuildArtifact artifact,
            DdfLanguagePackage package,
            IReadOnlyDictionary<string, string> runtimePathsById,
            ExecutionAttemptState attempt, DdfStateStore workingState)
        {
            attempt.RuntimeAttempted = true;
            if (request.Mode == DdfEditorExecutionMode.HostEffects &&
                request.HostEffectPolicy == DdfHostEffectPolicy.RollbackOnFailure)
            {
                attempt.TransactionBeginAttempted = true;
                attempt.EffectTransaction = WaitForEffectTransaction(
                    request.HostEffectTransactionFactory.BeginAsync(
                        new DdfHostEffectTransactionContext(executionId,
                            currentGeneration, startedAt,
                            currentCancellation.Token)),
                    request.HostCallTimeout);
                if (attempt.EffectTransaction == null)
                    throw new InvalidOperationException(
                        "La factory host ha restituito una transazione nulla.");
                if (string.IsNullOrWhiteSpace(attempt.EffectTransaction.Id))
                    throw new InvalidOperationException(
                        "La transazione host deve avere un identificatore.");
            }

            runtimePathsById = runtimePathsById ??
                new Dictionary<string, string>();
            DdfDebuggerSession debugger = request.DebugBinding?.Session;
            if (debugger != null)
            {
                var documentIdsByPath = runtimePathsById
                    .GroupBy(item => NormalizePath(item.Value),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Last().Key,
                        StringComparer.OrdinalIgnoreCase);
                debugger.SetBreakpoints((request.DebugBinding.Breakpoints ??
                    Array.Empty<DdfEditorBreakpoint>()).Where(item =>
                        item != null && item.Enabled &&
                        runtimePathsById.ContainsKey(item.DocumentId)).Select(item =>
                            new DdfSourceBreakpoint(runtimePathsById[item.DocumentId],
                                item.Line)));
                debugger.Paused = pause =>
                {
                    documentIdsByPath.TryGetValue(
                        NormalizePath(pause.DocumentPath), out string documentId);
                    request.DebugBinding.Paused?.Invoke(pause, documentId,
                        documentIdsByPath);
                };
            }

            return await DdfInterpreter.ExecuteAsync(artifact.RuntimeModules,
                package.Language, new DdfExecutionOptions
                {
                    EntryPoint = string.IsNullOrWhiteSpace(request.EntryPoint)
                        ? artifact.EntryPoint : request.EntryPoint,
                    MaxInstructions = request.MaxInstructions > 0
                        ? request.MaxInstructions : artifact.MaxInstructions,
                    CancellationRequested = () =>
                        currentCancellation.IsCancellationRequested,
                    Input = request.Input,
                    Output = line => RaiseOutput(currentGeneration, line),
                    DebuggerSession = debugger,
                    HostCatalog = hostCatalog,
                    Mode = request.Mode,
                    CancellationToken = currentCancellation.Token,
                    ExecutionStartedAt = startedAt,
                    ExecutionDeadline = request.Timeout.HasValue
                        ? startedAt + request.Timeout.Value : null,
                    HostCallTimeout = request.HostCallTimeout,
                    MaxHostCalls = request.MaxHostCalls,
                    MaxHostResources = request.MaxHostResources,
                    EntryArguments = request.EntryArguments,
                    DatabaseTransactions = request.DatabaseTransactions,
                    ExecutionId = executionId,
                    HostEffectTransactionId = attempt.EffectTransaction?.Id,
                    UiCapabilityInvoker = uiCapabilityInvoker,
                    StateStore = workingState,
                    HostEffectTracker = effectTracker,
                    ExecutionGeneration = currentGeneration,
                    CooperativeStateChanged = snapshot =>
                        UpdateCooperativeState(currentGeneration, snapshot,
                            runtimePathsById)
                }).ConfigureAwait(false);
        }

        private void UpdateCooperativeState(long currentGeneration,
            DdfCooperativeExecutionSnapshot snapshot,
            IReadOnlyDictionary<string, string> runtimePathsById)
        {
            if (snapshot == null || snapshot.Generation != currentGeneration)
                return;
            DdfEditorCooperativeSuspensionSnapshot pending =
                snapshot.PendingSuspension == null ? null :
                new DdfEditorCooperativeSuspensionSnapshot(
                    snapshot.PendingSuspension,
                    ResolveSourceDocumentId(snapshot.PendingSuspension,
                        runtimePathsById));
            UpdateCooperativeSnapshot(new DdfEditorCooperativeExecutionSnapshot(
                snapshot.ExecutionId, snapshot.Generation,
                MapCooperativeState(snapshot.State), pending));
        }

        private static string ResolveSourceDocumentId(
            DdfCooperativeSuspensionRequest suspension,
            IReadOnlyDictionary<string, string> runtimePathsById)
        {
            if (suspension == null || runtimePathsById == null)
                return null;
            string path = NormalizePath(suspension.SourceDocumentId);
            return runtimePathsById.FirstOrDefault(item => string.Equals(
                NormalizePath(item.Value), path,
                StringComparison.OrdinalIgnoreCase)).Key;
        }

        private void UpdateCooperativeSnapshot(
            DdfEditorCooperativeExecutionSnapshot snapshot)
        {
            DdfEditorCooperativeExecutionSnapshot previous;
            lock (syncRoot)
            {
                if (snapshot.Generation != generation) return;
                previous = cooperativeSnapshot;
                if (SameCooperativeSnapshot(previous, snapshot)) return;
                cooperativeSnapshot = snapshot;
            }
            Dispatch(() => CooperativeStateChanged?.Invoke(this,
                new DdfEditorCooperativeStateChangedEventArgs(previous, snapshot)));
        }

        private static bool SameCooperativeSnapshot(
            DdfEditorCooperativeExecutionSnapshot left,
            DdfEditorCooperativeExecutionSnapshot right) =>
            left.ExecutionId == right.ExecutionId &&
            left.Generation == right.Generation && left.State == right.State &&
            left.PendingSuspension?.CorrelationId ==
                right.PendingSuspension?.CorrelationId;

        private static DdfEditorCooperativeExecutionState MapCooperativeState(
            DdfCooperativeExecutionState state) => state switch
            {
                DdfCooperativeExecutionState.Running =>
                    DdfEditorCooperativeExecutionState.Running,
                DdfCooperativeExecutionState.AwaitingCapability =>
                    DdfEditorCooperativeExecutionState.AwaitingCapability,
                DdfCooperativeExecutionState.Cancelling =>
                    DdfEditorCooperativeExecutionState.Cancelling,
                DdfCooperativeExecutionState.CleaningUp =>
                    DdfEditorCooperativeExecutionState.CleaningUp,
                _ => DdfEditorCooperativeExecutionState.Idle
            };

        private static DdfHostEffectSummary FinalizeHostEffects(
            DdfEditorExecutionRequest request, DdfExecutionResult executionResult,
            DdfHostEffectTracker tracker,
            IDdfHostEffectTransaction transaction,
            bool transactionBeginAttempted, bool runtimeAttempted,
            ref string internalError)
        {
            IReadOnlyList<DdfHostEffectRecord> operations = tracker.Snapshot();
            if (request.Mode == DdfEditorExecutionMode.AnalysisOnly || !runtimeAttempted)
                return new DdfHostEffectSummary(request.HostEffectPolicy,
                    transactionBeginAttempted ? DdfHostEffectDisposition.BeginFailed :
                        DdfHostEffectDisposition.None,
                    operations, error: internalError);
            if (request.Mode == DdfEditorExecutionMode.Simulation)
                return new DdfHostEffectSummary(request.HostEffectPolicy,
                    operations.Count == 0 ? DdfHostEffectDisposition.None :
                        DdfHostEffectDisposition.Simulated, operations);
            if (request.HostEffectPolicy == DdfHostEffectPolicy.Immediate)
                return new DdfHostEffectSummary(request.HostEffectPolicy,
                    operations.Count == 0 ? DdfHostEffectDisposition.None :
                    executionResult?.Succeeded == true && string.IsNullOrEmpty(internalError)
                        ? DdfHostEffectDisposition.Applied
                        : DdfHostEffectDisposition.PartiallyApplied,
                    operations, error: internalError);
            if (transaction == null)
                return new DdfHostEffectSummary(request.HostEffectPolicy,
                    DdfHostEffectDisposition.BeginFailed, operations,
                    error: internalError);

            string transactionId = transaction.Id;
            bool commit = executionResult?.Succeeded == true &&
                string.IsNullOrEmpty(internalError);
            DdfHostEffectDisposition disposition;
            string effectError = string.Empty;
            try
            {
                if (commit)
                {
                    WaitForEffectOperation(transaction.CommitAsync(),
                        request.HostCallTimeout);
                    disposition = DdfHostEffectDisposition.Committed;
                }
                else
                {
                    WaitForEffectOperation(transaction.RollbackAsync(),
                        request.HostCallTimeout);
                    disposition = DdfHostEffectDisposition.RolledBack;
                }
            }
            catch (Exception exception)
            {
                effectError = exception.Message;
                internalError = AppendError(internalError,
                    (commit ? "Commit" : "Rollback") +
                    " della transazione host non riuscito: " + exception.Message);
                disposition = commit ? DdfHostEffectDisposition.CommitFailed :
                    DdfHostEffectDisposition.RollbackFailed;
            }
            finally
            {
                try
                {
                    WaitForEffectOperation(transaction.DisposeAsync(),
                        request.HostCallTimeout);
                }
                catch (Exception exception)
                {
                    effectError = AppendError(effectError, exception.Message);
                    internalError = AppendError(internalError,
                        "Rilascio della transazione host non riuscito: " +
                        exception.Message);
                }
            }
            return new DdfHostEffectSummary(request.HostEffectPolicy, disposition,
                operations, transactionId, effectError);
        }

        private static IDdfHostEffectTransaction WaitForEffectTransaction(
            ValueTask<IDdfHostEffectTransaction> operation, TimeSpan timeout) =>
            operation.AsTask().WaitAsync(timeout).GetAwaiter().GetResult();

        private static void WaitForEffectOperation(ValueTask operation,
            TimeSpan timeout) => operation.AsTask().WaitAsync(timeout)
                .GetAwaiter().GetResult();

        private static string AppendError(string current, string next) =>
            string.IsNullOrEmpty(current) ? next ?? string.Empty :
            current + " | " + (next ?? string.Empty);

        private static string NormalizePath(string path) =>
            (path ?? string.Empty).Replace('/', '\\');

        private sealed class ExecutionAttemptState
        {
            public bool TransactionBeginAttempted { get; set; }
            public bool RuntimeAttempted { get; set; }
            public IDdfHostEffectTransaction EffectTransaction { get; set; }
        }

        private IReadOnlyList<DdfEditorBuildDocumentSnapshot> CaptureDocuments()
        {
            IReadOnlyList<IDdfEditorDocument> documents =
                documentServiceProvider()?.OpenDocuments;
            if (documents == null) return Array.Empty<DdfEditorBuildDocumentSnapshot>();
            return documents.Select(document => new DdfEditorBuildDocumentSnapshot(
                document.Id, document.DisplayName, document.FilePath,
                document.SourceText, document.Version))
                .ToList().AsReadOnly();
        }

        private void RaiseOutput(long currentGeneration, string text)
        {
            Dispatch(() => OutputReceived?.Invoke(this,
                new DdfEditorExecutionOutputEventArgs(currentGeneration, text)));
        }

        private void RaiseStateChanged(long currentGeneration,
            DdfEditorExecutionState previousState, DdfEditorExecutionState nextState)
        {
            Dispatch(() => StateChanged?.Invoke(this,
                new DdfEditorExecutionStateChangedEventArgs(
                    currentGeneration, previousState, nextState)));
        }

        private void Dispatch(Action callback)
        {
            if (callback == null) return;
            lock (syncRoot)
            {
                if (callbacksSuppressed) return;
            }
            if (eventDispatcher.HasAccess)
            {
                callback();
                return;
            }
            eventDispatcher.Post(() =>
            {
                lock (syncRoot)
                {
                    if (callbacksSuppressed) return;
                }
                callback();
            });
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DdfEditorExecutionService));
        }
    }
}
