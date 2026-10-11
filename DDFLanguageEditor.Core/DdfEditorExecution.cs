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
        bool Stop();
    }

    /// <summary>
    /// Per-host execution service. It snapshots the supplied in-memory documents,
    /// builds them without reading or writing source files and runs the resulting IR
    /// on a dedicated worker. Event handlers are invoked on that worker thread.
    /// </summary>
    public sealed class DdfEditorExecutionService : IDdfEditorExecutionService
    {
        private readonly object syncRoot = new object();
        private readonly Func<IDdfEditorDocumentService> documentServiceProvider;
        private readonly Func<IDdfLanguagePackageSession> languageSessionProvider;
        private readonly Func<DdfHostCatalogSnapshot> hostCatalogProvider;
        private readonly DdfEditorExecutionBuildHandler buildHandler;
        private CancellationTokenSource cancellation;
        private DdfEditorExecutionState state;
        private long generation;
        private bool disposed;

        public DdfEditorExecutionService(IDdfEditorDocumentService documentService,
            IDdfLanguagePackageSession languageSession)
            : this(() => documentService, () => languageSession, null, null)
        {
            if (documentService == null) throw new ArgumentNullException(nameof(documentService));
            if (languageSession == null) throw new ArgumentNullException(nameof(languageSession));
        }

        public DdfEditorExecutionService(
            Func<IDdfEditorDocumentService> documentServiceProvider,
            Func<IDdfLanguagePackageSession> languageSessionProvider,
            Func<DdfHostCatalogSnapshot> hostCatalogProvider = null,
            DdfEditorExecutionBuildHandler buildHandler = null)
        {
            this.documentServiceProvider = documentServiceProvider ??
                throw new ArgumentNullException(nameof(documentServiceProvider));
            this.languageSessionProvider = languageSessionProvider ??
                throw new ArgumentNullException(nameof(languageSessionProvider));
            this.hostCatalogProvider = hostCatalogProvider;
            this.buildHandler = buildHandler;
        }

        public DdfEditorExecutionState State
        {
            get { lock (syncRoot) return state; }
        }

        public bool IsRunning => State != DdfEditorExecutionState.Idle;

        public long Generation
        {
            get { lock (syncRoot) return generation; }
        }

        public DdfEditorExecutionCompletion LastCompletion { get; private set; }

        public event EventHandler<DdfEditorExecutionStateChangedEventArgs> StateChanged;
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
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            CancellationTokenSource currentCancellation;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Idle)
                    throw new InvalidOperationException("Una sessione di esecuzione è già attiva.");
                currentGeneration = ++generation;
                currentCancellation = new CancellationTokenSource();
                if (request.Timeout.HasValue)
                    currentCancellation.CancelAfter(request.Timeout.Value);
                cancellation = currentCancellation;
                state = DdfEditorExecutionState.Running;
            }
            RaiseStateChanged(currentGeneration, DdfEditorExecutionState.Idle,
                DdfEditorExecutionState.Running);

            return Task.Factory.StartNew(
                () => Execute(currentGeneration, startedAt, documents, package,
                    request, currentCancellation, hostCatalog, preparedBuild),
                CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
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
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorExecutionState.Running) return false;
                state = DdfEditorExecutionState.Stopping;
                currentGeneration = generation;
                cancellation?.Cancel();
            }
            RaiseStateChanged(currentGeneration, DdfEditorExecutionState.Running,
                DdfEditorExecutionState.Stopping);
            return true;
        }

        public void Dispose()
        {
            CancellationTokenSource pending;
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                pending = cancellation;
                cancellation = null;
            }
            pending?.Cancel();
        }

        private DdfEditorExecutionCompletion Execute(long currentGeneration,
            DateTimeOffset startedAt, IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfEditorExecutionRequest request,
            CancellationTokenSource currentCancellation,
            DdfHostCatalogSnapshot hostCatalog,
            DdfEditorExecutionBuildContext preparedBuild)
        {
            DdfExecutionResult executionResult = null;
            IReadOnlyList<DdfBuildIssue> issues = Array.Empty<DdfBuildIssue>();
            string internalError = string.Empty;
            DdfBuildArtifact artifact = null;
            Guid executionId = Guid.NewGuid();
            var effectTracker = new DdfHostEffectTracker();
            IDdfHostEffectTransaction effectTransaction = null;
            bool transactionBeginAttempted = false;
            bool runtimeAttempted = false;
            try
            {
                if (preparedBuild != null)
                {
                    package = preparedBuild.LanguagePackage ?? package;
                    DdfBuildResult build = preparedBuild.Result;
                    issues = build.Issues;
                    artifact = build.Artifact;
                    if (build.Success && request.Mode != DdfEditorExecutionMode.AnalysisOnly)
                        executionResult = ExecuteArtifact(currentGeneration, startedAt,
                            request, currentCancellation, hostCatalog, executionId,
                            effectTracker, artifact, package,
                            preparedBuild.RuntimePathsByDocumentId,
                            ref transactionBeginAttempted, ref runtimeAttempted,
                            ref effectTransaction);
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
                        executionResult = ExecuteArtifact(currentGeneration, startedAt,
                            request, currentCancellation, hostCatalog, executionId,
                            effectTracker, artifact, package,
                            runtimePathsById, ref transactionBeginAttempted,
                            ref runtimeAttempted, ref effectTransaction);
                    }
                }
            }
            catch (Exception exception)
            {
                internalError = exception.Message;
            }

            DdfHostEffectSummary hostEffects = FinalizeHostEffects(request,
                executionResult, effectTracker, effectTransaction,
                transactionBeginAttempted, runtimeAttempted, ref internalError);

            var completion = new DdfEditorExecutionCompletion(currentGeneration,
                startedAt, DateTimeOffset.UtcNow, executionResult, issues, internalError,
                request.Mode, artifact)
            {
                ProcedureInvocation = request.ProcedureInvocation,
                HostEffects = hostEffects
            };
            DdfEditorExecutionState previousState;
            lock (syncRoot)
            {
                previousState = state;
                if (ReferenceEquals(cancellation, currentCancellation))
                    cancellation = null;
                state = DdfEditorExecutionState.Idle;
                LastCompletion = completion;
            }
            currentCancellation.Dispose();
            ExecutionCompleted?.Invoke(this,
                new DdfEditorExecutionCompletedEventArgs(completion));
            RaiseStateChanged(currentGeneration, previousState,
                DdfEditorExecutionState.Idle);
            return completion;
        }

        private DdfExecutionResult ExecuteArtifact(long currentGeneration,
            DateTimeOffset startedAt, DdfEditorExecutionRequest request,
            CancellationTokenSource currentCancellation,
            DdfHostCatalogSnapshot hostCatalog, Guid executionId,
            DdfHostEffectTracker effectTracker, DdfBuildArtifact artifact,
            DdfLanguagePackage package,
            IReadOnlyDictionary<string, string> runtimePathsById,
            ref bool transactionBeginAttempted, ref bool runtimeAttempted,
            ref IDdfHostEffectTransaction effectTransaction)
        {
            runtimeAttempted = true;
            if (request.Mode == DdfEditorExecutionMode.HostEffects &&
                request.HostEffectPolicy == DdfHostEffectPolicy.RollbackOnFailure)
            {
                transactionBeginAttempted = true;
                effectTransaction = WaitForEffectTransaction(
                    request.HostEffectTransactionFactory.BeginAsync(
                        new DdfHostEffectTransactionContext(executionId,
                            currentGeneration, startedAt,
                            currentCancellation.Token)),
                    request.HostCallTimeout);
                if (effectTransaction == null)
                    throw new InvalidOperationException(
                        "La factory host ha restituito una transazione nulla.");
                if (string.IsNullOrWhiteSpace(effectTransaction.Id))
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

            return DdfInterpreter.Execute(artifact.RuntimeModules,
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
                    HostEffectTransactionId = effectTransaction?.Id,
                    HostEffectTracker = effectTracker
                });
        }

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
            OutputReceived?.Invoke(this,
                new DdfEditorExecutionOutputEventArgs(currentGeneration, text));
        }

        private void RaiseStateChanged(long currentGeneration,
            DdfEditorExecutionState previousState, DdfEditorExecutionState nextState)
        {
            StateChanged?.Invoke(this, new DdfEditorExecutionStateChangedEventArgs(
                currentGeneration, previousState, nextState));
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DdfEditorExecutionService));
        }
    }
}
