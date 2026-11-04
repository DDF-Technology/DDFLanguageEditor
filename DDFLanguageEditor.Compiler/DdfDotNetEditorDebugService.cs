using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Standalone-only adapter between the public editor debug contract and the
    /// compiled out-of-process worker. The reusable WinForms surface never
    /// references this assembly.
    /// </summary>
    public sealed class DdfDotNetEditorDebugService : IDdfEditorDebugService,
        IDdfEditorCooperativeExecutionService,
        IDdfDotNetWorkerCooperativeExecutionObserver
    {
        // Interactive pauses are user think-time, not execution time. Keep a
        // bounded wall clock for abandoned sessions and a separate CPU quota.
        internal static readonly TimeSpan InteractiveSessionWallLimit =
            TimeSpan.FromHours(12);
        internal static readonly TimeSpan InteractiveWorkerCpuLimit =
            TimeSpan.FromMinutes(5);
        internal const int InteractiveProgressLimitMilliseconds = 300000;
        private readonly object sync = new();
        private readonly Func<IDdfEditorDocumentService> documentsProvider;
        private readonly Func<IDdfLanguagePackageSession> languageProvider;
        private readonly Func<DdfEditorExecutionRequest,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot>, DdfLanguagePackage,
            DdfEditorExecutionBuildContext> build;
        private readonly string workerPath;
        private readonly IDdfEditorEventDispatcher eventDispatcher;
        private IReadOnlyList<DdfEditorBreakpoint> breakpoints =
            Array.Empty<DdfEditorBreakpoint>();
        private IReadOnlyList<string> watches = Array.Empty<string>();
        private CancellationTokenSource cancellation;
        private TaskCompletionSource<DdfDotNetDebugCommand> pendingCommand;
        private DdfEditorDebugState state;
        private bool disposed;
        private long generation;
        private Guid executionId;
        private IReadOnlyList<DdfEditorBuildDocumentSnapshot> activeDocuments =
            Array.Empty<DdfEditorBuildDocumentSnapshot>();
        private DdfEditorCooperativeExecutionSnapshot cooperativeSnapshot =
            new(Guid.Empty, 0, DdfEditorCooperativeExecutionState.Idle);

        public DdfDotNetEditorDebugService(
            Func<IDdfEditorDocumentService> documentsProvider,
            Func<IDdfLanguagePackageSession> languageProvider,
            Func<DdfEditorExecutionRequest,
                IReadOnlyList<DdfEditorBuildDocumentSnapshot>,
                DdfLanguagePackage, DdfEditorExecutionBuildContext> build,
            string workerPath = null,
            IDdfEditorEventDispatcher eventDispatcher = null)
        {
            this.documentsProvider = documentsProvider ?? throw new
                ArgumentNullException(nameof(documentsProvider));
            this.languageProvider = languageProvider ?? throw new
                ArgumentNullException(nameof(languageProvider));
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.workerPath = ResolveWorkerPath(workerPath);
            this.eventDispatcher = eventDispatcher ??
                DdfEditorInlineEventDispatcher.Instance;
        }

        public DdfEditorDebugState State { get { lock (sync) return state; } }
        public bool IsActive => State != DdfEditorDebugState.Idle;
        public bool IsPaused => State == DdfEditorDebugState.Paused;
        public long Generation { get { lock (sync) return generation; } }
        public IReadOnlyList<DdfEditorBreakpoint> Breakpoints { get { lock (sync) return breakpoints; } }
        public IReadOnlyList<string> WatchExpressions { get { lock (sync) return watches; } }
        public DdfEditorDebugLocation CurrentLocation { get; private set; }
        public DdfEditorDebugSnapshot CurrentSnapshot { get; private set; }
        public DdfEditorExecutionCompletion LastCompletion { get; private set; }
        public DdfEditorCooperativeExecutionSnapshot CooperativeSnapshot
        {
            get { lock (sync) return cooperativeSnapshot; }
        }

        public event EventHandler<DdfEditorDebugStateChangedEventArgs> StateChanged;
        public event EventHandler<DdfEditorDebugPausedEventArgs> Paused;
        public event EventHandler<DdfEditorExecutionOutputEventArgs> OutputReceived;
        public event EventHandler<DdfEditorExecutionCompletedEventArgs> DebugCompleted;
        public event EventHandler<DdfEditorCooperativeStateChangedEventArgs>
            CooperativeStateChanged;

        public void SetBreakpoints(IEnumerable<DdfEditorBreakpoint> values)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Idle &&
                    state != DdfEditorDebugState.Paused) throw new
                    InvalidOperationException("I breakpoint possono cambiare solo a debug fermo o in pausa.");
                breakpoints = (values ?? Array.Empty<DdfEditorBreakpoint>())
                    .Where(value => value != null).GroupBy(value =>
                        value.DocumentId + "\n" + value.Line, StringComparer.Ordinal)
                    .Select(group => group.Last()).ToList().AsReadOnly();
            }
        }

        public void SetWatchExpressions(IEnumerable<string> values)
        {
            var result = (values ?? Array.Empty<string>()).Select(value =>
                    (value ?? string.Empty).Trim()).Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal).ToList();
            if (result.Any(value => value.Length > 512)) throw new
                ArgumentException("Espressione Watch oltre il limite.", nameof(values));
            lock (sync) { ThrowIfDisposed(); watches = result.AsReadOnly(); }
        }

        public Task<DdfEditorExecutionCompletion> RunAsync(
            DdfEditorExecutionRequest request = null)
        {
            request ??= new DdfEditorExecutionRequest();
            if (request.Mode == DdfEditorExecutionMode.AnalysisOnly) throw new
                ArgumentException("Il debug compilato richiede una modalità eseguibile.", nameof(request));
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents = CaptureDocuments();
            DdfLanguagePackage package = languageProvider()?.ActivePackage;
            long current;
            CancellationTokenSource source;
            DdfEditorCooperativeExecutionSnapshot previousCooperative;
            DdfEditorCooperativeExecutionSnapshot nextCooperative;
            lock (sync)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Idle) throw new
                    InvalidOperationException("Una sessione di debug è già attiva.");
                current = ++generation;
                executionId = Guid.NewGuid();
                source = new CancellationTokenSource();
                if (request.Timeout.HasValue) source.CancelAfter(request.Timeout.Value);
                cancellation = source;
                CurrentLocation = null;
                CurrentSnapshot = null;
                activeDocuments = documents;
                state = DdfEditorDebugState.Running;
                previousCooperative = cooperativeSnapshot;
                cooperativeSnapshot = nextCooperative = new(
                    executionId, current,
                    DdfEditorCooperativeExecutionState.Running);
            }
            RaiseState(current, DdfEditorDebugState.Idle,
                DdfEditorDebugState.Running);
            RaiseCooperative(previousCooperative, nextCooperative);
            return RunCoreAsync(current, request, documents, package, source);
        }

        public bool Continue() => Submit(DdfDotNetDebugCommand.Continue);
        public bool StepInto() => Submit(DdfDotNetDebugCommand.StepInto);
        public bool StepOver() => Submit(DdfDotNetDebugCommand.StepOver);
        public bool StepOut() => Submit(DdfDotNetDebugCommand.StepOut);

        public bool Stop()
        {
            DdfEditorDebugState previous;
            TaskCompletionSource<DdfDotNetDebugCommand> command;
            lock (sync)
            {
                ThrowIfDisposed();
                if (state is not (DdfEditorDebugState.Running or
                    DdfEditorDebugState.Paused or
                    DdfEditorDebugState.AwaitingCapability)) return false;
                previous = state;
                state = DdfEditorDebugState.Stopping;
                command = pendingCommand;
                CurrentLocation = null;
                CurrentSnapshot = null;
            }
            command?.TrySetResult(DdfDotNetDebugCommand.Stop);
            cancellation?.Cancel();
            RaiseState(Generation, previous, DdfEditorDebugState.Stopping);
            SetCooperativeState(DdfEditorCooperativeExecutionState.Cancelling);
            return true;
        }

        public DdfEditorWatchResult EvaluateWatch(string expression,
            int frameIndex = 0)
        {
            DdfEditorDebugSnapshot snapshot;
            lock (sync) snapshot = CurrentSnapshot;
            if (snapshot == null) return new DdfEditorWatchResult(expression,
                frameIndex, null, "In attesa di una pausa del debugger.");
            IReadOnlyList<DdfEditorDebugVariableSnapshot> variables = frameIndex == 0
                ? snapshot.Variables
                : frameIndex >= 0 && frameIndex < snapshot.CallStack.Count
                    ? snapshot.CallStack[frameIndex].Variables
                    : null;
            if (variables == null) return new DdfEditorWatchResult(expression,
                frameIndex, null, "Frame del Call Stack non disponibile.");
            DdfEditorDebugVariableSnapshot value = FindVariable(variables,
                (expression ?? string.Empty).Trim());
            return new DdfEditorWatchResult(expression, frameIndex, value,
                value == null ? "Simbolo non disponibile nello scope corrente." : string.Empty);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
            }
            cancellation?.Cancel();
            pendingCommand?.TrySetResult(DdfDotNetDebugCommand.Stop);
        }

        private async Task<DdfEditorExecutionCompletion> RunCoreAsync(long current,
            DdfEditorExecutionRequest request,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package, CancellationTokenSource source)
        {
            DateTimeOffset started = DateTimeOffset.UtcNow;
            DdfExecutionResult runtimeResult = null;
            IReadOnlyList<DdfBuildIssue> buildIssues = Array.Empty<DdfBuildIssue>();
            string internalError = string.Empty;
            DdfBuildArtifact artifact = null;
            try
            {
                DdfEditorExecutionBuildContext prepared = build(request,
                    documents, package);
                package = prepared.LanguagePackage ?? package;
                buildIssues = prepared.Result.Issues;
                artifact = prepared.Result.Artifact;
                if (prepared.Result.Success)
                {
                    DdfDotNetResolvedReferenceSetResult resolvedReferences =
                        DdfDotNetResolvedReferenceSetBuilder.Create(
                            FindNet10ReferencePack(),
                            typeof(DdfRuntimeAbi).Assembly.Location,
                            prepared.Result);
                    if (!resolvedReferences.Success)
                        throw ContractFailure(resolvedReferences.Issues);
                    DdfDotNetReferenceSet references =
                        resolvedReferences.ReferenceSet;
                    DdfDotNetCompilationPlanResult planned =
                        DdfDotNetCompilationPlanner.Create(prepared.Result,
                            new DdfDotNetCompilationOptions("net10.0",
                                DdfRuntimeAbi.Version, referenceSet: references));
                    if (!planned.Success) throw ContractFailure(planned.Issues);
                    DdfDotNetArtifactEmissionResult emitted =
                        DdfDotNetArtifactEmitter.Emit(planned.Plan,
                            package.Language.Types.ClrMapping);
                    if (!emitted.Success) throw ContractFailure(emitted.Issues);
                    DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                        .Link(planned.Plan.EntryModuleIdentity,
                            emitted.Artifacts.Select(value => value.Manifest));
                    if (!linked.Success) throw ContractFailure(linked.Issues);
                    DdfDotNetLinkedBundleResult bundled =
                        DdfDotNetLinkedBundleBuilder
                            .CreateWithPackagedReferences(linked.Program,
                                emitted, references, resolvedReferences
                                    .ManagedReferencePaths);
                    if (!bundled.Success) throw ContractFailure(bundled.Issues);

                    var environment = new DdfDotNetRuntimeEnvironment(
                        DdfDotNetArtifactHostKind.Standalone, "net10.0",
                        DdfRuntimeAbi.Version);
                    List<DdfDotNetWorkerDebugBreakpoint> workerBreakpoints =
                        CreateBreakpoints(documents, artifact);
                    bool hasRequestedBreakpoints = Breakpoints.Any(value =>
                        value.Enabled);
                    if (hasRequestedBreakpoints && workerBreakpoints.Count == 0)
                        throw new InvalidOperationException(
                            "Nessun breakpoint attivo può essere associato ai sorgenti compilati. " +
                            "Aprire il relativo documento e riposizionare il breakpoint su una riga eseguibile.");
                    DdfDotNetWorkerDebugPlan debugPlan =
                        workerBreakpoints.Count == 0 ? null : new()
                    {
                        Breakpoints = workerBreakpoints,
                        Interactive = true,
                        MaxPauses = 256,
                        PauseTimeoutMilliseconds =
                            InteractiveProgressLimitMilliseconds
                    };
                    DdfDotNetWorkerRequest workerRequest =
                        DdfDotNetWorkerProtocol.CreateRequest(bundled.Bundle,
                            references, environment, artifact.MaxInstructions,
                            correlationId: executionId.ToString("D"),
                            debugPlan: debugPlan);
                    var handler = new DdfDotNetWorkerInteractiveDebugHandler(
                        (pause, token) => WaitForCommandAsync(current, pause,
                            documents, token),
                        () => CreateBreakpoints(documents, artifact));
                    DdfDotNetWorkerExecutionResult executed = await
                        DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                            workerRequest, new DdfDotNetWorkerResourceLimits(
                                request.Timeout ?? InteractiveSessionWallLimit,
                                maxProcessCpuTime: InteractiveWorkerCpuLimit),
                            handler, this, source.Token).ConfigureAwait(false);
                    foreach (string output in executed.Response?.Output ??
                             new List<string>())
                        Dispatch(() => OutputReceived?.Invoke(this,
                            new DdfEditorExecutionOutputEventArgs(current, output)));
                    bool cancelled = source.IsCancellationRequested ||
                        executed.Response?.ErrorCode == "DDFC550";
                    if (!executed.Success && !cancelled)
                        internalError = string.Join("; ", executed.Issues.Select(
                            issue => issue.Code + ": " + issue.Message).Concat(
                            executed.Response == null ? Array.Empty<string>() :
                            new[] { executed.Response.ErrorCode + ": " +
                                executed.Response.ErrorMessage }));
                    object returnValue = executed.Response?.ResultJson == null
                        ? null : JsonSerializer.Deserialize<JsonElement>(
                            executed.Response.ResultJson);
                    IReadOnlyList<DdfDiagnostic> diagnostics =
                        Array.Empty<DdfDiagnostic>();
                    IReadOnlyList<DdfRuntimeStackFrame> stackTrace =
                        Array.Empty<DdfRuntimeStackFrame>();
                    DdfErrorValue error = null;
                    if (executed.Response != null &&
                        DdfErrorCatalog.TryGetByCode(
                            executed.Response.ErrorCode,
                            out DdfErrorDefinition definition))
                    {
                        DdfEditorDebugLocation errorLocation = Location(
                            executed.Response.ErrorLocation, documents);
                        string message = string.IsNullOrWhiteSpace(
                            executed.Response.ErrorMessage)
                            ? definition.Documentation
                            : executed.Response.ErrorMessage;
                        DdfDiagnostic diagnostic = new DdfDiagnostic(
                            definition.Code, message, errorLocation.Start,
                            Math.Max(1, errorLocation.Length),
                            Math.Max(1, errorLocation.Line),
                            Math.Max(1, errorLocation.Column),
                            domain: DdfDiagnosticDomain.Runtime,
                            documentPath: errorLocation.FilePath);
                        diagnostics = new[] { diagnostic };
                        var runtimeFrames = new List<DdfRuntimeStackFrame>();
                        var errorFrames = new List<DdfErrorStackFrame>();
                        foreach (DdfDotNetWorkerErrorFrame frame in
                                 executed.Response.ErrorStack ??
                                 new List<DdfDotNetWorkerErrorFrame>())
                        {
                            DdfEditorDebugLocation frameLocation = Location(
                                frame.Location ?? frame.Declaration, documents);
                            string functionName = string.IsNullOrWhiteSpace(
                                frame.SymbolId) ? "<funzione>" : frame.SymbolId;
                            runtimeFrames.Add(new DdfRuntimeStackFrame(
                                functionName, frameLocation.Start,
                                Math.Max(1, frameLocation.Length),
                                Math.Max(1, frameLocation.Line),
                                Math.Max(1, frameLocation.Column),
                                frameLocation.FilePath));
                            errorFrames.Add(new DdfErrorStackFrame(functionName,
                                frameLocation.FilePath, frameLocation.Start,
                                Math.Max(1, frameLocation.Length),
                                Math.Max(1, frameLocation.Line),
                                Math.Max(1, frameLocation.Column),
                                frame.SymbolId));
                        }
                        stackTrace = runtimeFrames.AsReadOnly();
                        error = new DdfErrorValue(definition, message,
                            errorLocation.FilePath, errorLocation.Start,
                            Math.Max(1, errorLocation.Length), errorFrames);
                    }
                    runtimeResult = new DdfExecutionResult(returnValue,
                        checked((int)Math.Min(int.MaxValue,
                            executed.Response?.Instructions ?? 0)), cancelled,
                        executed.Response?.Ended == true,
                        diagnostics, stackTrace, Array.Empty<DdfHostValueResult>(),
                        null, error);
                }
            }
            catch (OperationCanceledException)
            {
                runtimeResult = new DdfExecutionResult(null, 0, true, false,
                    Array.Empty<DdfDiagnostic>(),
                    Array.Empty<DdfRuntimeStackFrame>());
            }
            catch (Exception exception)
            {
                internalError = exception.Message;
            }
            SetCooperativeState(
                DdfEditorCooperativeExecutionState.CleaningUp);
            var completion = new DdfEditorExecutionCompletion(current, started,
                DateTimeOffset.UtcNow, runtimeResult, buildIssues, internalError,
                request.Mode, artifact);
            DdfEditorDebugState previous;
            DdfEditorCooperativeExecutionSnapshot previousCooperative;
            DdfEditorCooperativeExecutionSnapshot nextCooperative;
            lock (sync)
            {
                previous = state;
                if (ReferenceEquals(cancellation, source)) cancellation = null;
                pendingCommand = null;
                CurrentLocation = null;
                CurrentSnapshot = null;
                state = DdfEditorDebugState.Idle;
                activeDocuments = Array.Empty<
                    DdfEditorBuildDocumentSnapshot>();
                previousCooperative = cooperativeSnapshot;
                cooperativeSnapshot = nextCooperative = new(executionId,
                    current, DdfEditorCooperativeExecutionState.Idle);
                LastCompletion = completion;
            }
            source.Dispose();
            Dispatch(() => DebugCompleted?.Invoke(this,
                new DdfEditorExecutionCompletedEventArgs(completion)));
            RaiseState(current, previous, DdfEditorDebugState.Idle);
            RaiseCooperative(previousCooperative, nextCooperative);
            return completion;
        }

        public void OnAwaitingCapability(
            DdfDotNetWorkerCooperativeSuspension suspension)
        {
            if (suspension?.DebugSnapshot == null) return;
            DdfEditorDebugSnapshot debugSnapshot;
            DdfEditorDebugState previousState;
            DdfEditorCooperativeExecutionSnapshot previousCooperative;
            DdfEditorCooperativeExecutionSnapshot nextCooperative;
            long current;
            lock (sync)
            {
                if (disposed || state != DdfEditorDebugState.Running) return;
                debugSnapshot = CreateSnapshot(suspension.DebugSnapshot,
                    activeDocuments);
                previousState = state;
                state = DdfEditorDebugState.AwaitingCapability;
                CurrentSnapshot = debugSnapshot;
                CurrentLocation = debugSnapshot.Location;
                current = generation;
                Guid correlation = Guid.TryParse(suspension.CorrelationId,
                    out Guid parsed) ? parsed : executionId;
                var pending = new DdfEditorCooperativeSuspensionSnapshot(
                    correlation, suspension.CapabilityId,
                    suspension.CapabilityId,
                    suspension.ArgumentsJson.Select(DeserializeArgument)
                        .ToList().AsReadOnly(),
                    debugSnapshot.Location.DocumentId,
                    debugSnapshot.Location.Start,
                    debugSnapshot.Location.Length);
                previousCooperative = cooperativeSnapshot;
                cooperativeSnapshot = nextCooperative = new(executionId,
                    current,
                    DdfEditorCooperativeExecutionState.AwaitingCapability,
                    pending);
            }
            RaiseState(current, previousState,
                DdfEditorDebugState.AwaitingCapability);
            RaiseCooperative(previousCooperative, nextCooperative);
        }

        public void OnCapabilityResumed(
            DdfDotNetWorkerCooperativeSuspension suspension)
        {
            DdfEditorCooperativeExecutionSnapshot previousCooperative;
            DdfEditorCooperativeExecutionSnapshot nextCooperative;
            long current;
            lock (sync)
            {
                if (disposed || state !=
                    DdfEditorDebugState.AwaitingCapability) return;
                state = DdfEditorDebugState.Running;
                CurrentSnapshot = null;
                CurrentLocation = null;
                current = generation;
                previousCooperative = cooperativeSnapshot;
                cooperativeSnapshot = nextCooperative = new(executionId,
                    current, DdfEditorCooperativeExecutionState.Running);
            }
            RaiseState(current, DdfEditorDebugState.AwaitingCapability,
                DdfEditorDebugState.Running);
            RaiseCooperative(previousCooperative, nextCooperative);
        }

        private ValueTask<DdfDotNetDebugCommand> WaitForCommandAsync(long current,
            DdfDotNetWorkerDebugPause pause,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            CancellationToken token)
        {
            DdfEditorDebugSnapshot snapshot = CreateSnapshot(pause, documents);
            var command = new TaskCompletionSource<DdfDotNetDebugCommand>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (sync)
            {
                if (disposed || generation != current ||
                    state != DdfEditorDebugState.Running)
                    return ValueTask.FromResult(DdfDotNetDebugCommand.Stop);
                pendingCommand = command;
                CurrentSnapshot = snapshot;
                CurrentLocation = snapshot.Location;
                state = DdfEditorDebugState.Paused;
            }
            RaiseState(current, DdfEditorDebugState.Running,
                DdfEditorDebugState.Paused);
            Dispatch(() => Paused?.Invoke(this,
                new DdfEditorDebugPausedEventArgs(current, snapshot)));
            token.Register(() => command.TrySetCanceled(token));
            return new ValueTask<DdfDotNetDebugCommand>(command.Task);
        }

        private bool Submit(DdfDotNetDebugCommand value)
        {
            TaskCompletionSource<DdfDotNetDebugCommand> command;
            long current;
            lock (sync)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Paused) return false;
                command = pendingCommand;
                pendingCommand = null;
                CurrentLocation = null;
                CurrentSnapshot = null;
                current = generation;
                state = DdfEditorDebugState.Running;
            }
            bool submitted = command?.TrySetResult(value) == true;
            if (submitted) RaiseState(current, DdfEditorDebugState.Paused,
                DdfEditorDebugState.Running);
            return submitted;
        }

        private static DdfEditorDebugSnapshot CreateSnapshot(
            DdfDotNetWorkerDebugPause pause,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents)
        {
            DdfEditorDebugLocation location = Location(pause.Location, documents);
            var frames = (pause.Frames ?? new List<DdfDotNetWorkerDebugFrame>())
                .Select((frame, index) => new DdfEditorDebugStackFrameSnapshot(
                    index, frame.Name, Location(frame.Location, documents),
                    Variables(frame.Variables))).ToList().AsReadOnly();
            IReadOnlyList<DdfEditorDebugVariableSnapshot> variables = frames
                .FirstOrDefault()?.Variables ?? Array.Empty<
                    DdfEditorDebugVariableSnapshot>();
            IReadOnlyList<DdfEditorDebugVariableSnapshot> globals = frames
                .SelectMany(frame => frame.Variables).Where(variable =>
                    variable.Kind == DdfRuntimeVariableKind.Global)
                .GroupBy(variable => variable.Name, StringComparer.Ordinal)
                .Select(group => group.First()).ToList().AsReadOnly();
            IReadOnlyList<DdfEditorDebugVariableSnapshot> states = frames
                .SelectMany(frame => frame.Variables).Where(variable =>
                    variable.Kind == DdfRuntimeVariableKind.State)
                .GroupBy(variable => variable.Name, StringComparer.Ordinal)
                .Select(group => group.First()).ToList().AsReadOnly();
            return new DdfEditorDebugSnapshot(location, frames, variables,
                globals, states);
        }

        private static object DeserializeArgument(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json ?? "null");
                return document.RootElement.ValueKind switch
                {
                    JsonValueKind.String => document.RootElement.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when document.RootElement.TryGetInt64(
                        out long integer) => integer,
                    JsonValueKind.Number => document.RootElement.GetDouble(),
                    JsonValueKind.Null => null,
                    _ => document.RootElement.GetRawText()
                };
            }
            catch (JsonException) { return json ?? string.Empty; }
        }

        private static DdfEditorDebugLocation Location(
            DdfDotNetWorkerSourceLocation source,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents)
        {
            source ??= new DdfDotNetWorkerSourceLocation();
            DdfEditorBuildDocumentSnapshot document = documents.FirstOrDefault(
                value => PathMatches(value.FilePath, source.DocumentPath) ||
                    string.Equals(value.DisplayName, Path.GetFileName(
                        source.DocumentPath), StringComparison.OrdinalIgnoreCase));
            string text = document?.SourceText ?? string.Empty;
            (int line, int column) = LineColumn(text, source.Start);
            return new DdfEditorDebugLocation(document?.Id,
                document?.DisplayName ?? Path.GetFileName(source.DocumentPath),
                document?.FilePath ?? source.DocumentPath, line, column,
                source.Start, source.Length);
        }

        private static IReadOnlyList<DdfEditorDebugVariableSnapshot> Variables(
            IEnumerable<DdfDotNetWorkerDebugVariable> values) =>
            (values ?? Array.Empty<DdfDotNetWorkerDebugVariable>()).Select(value =>
                new DdfEditorDebugVariableSnapshot(value.Name,
                    value.DisplayValue, value.TypeName, 0, MapKind(value.Kind),
                    Variables(value.Children), value.IsTruncated,
                    value.IsReadOnly, value.IsFixed)).ToList()
                .AsReadOnly();

        private static DdfRuntimeVariableKind MapKind(
            DdfRuntimeDebugVariableKind value) => value switch
            {
                DdfRuntimeDebugVariableKind.Parameter => DdfRuntimeVariableKind.Parameter,
                DdfRuntimeDebugVariableKind.Global => DdfRuntimeVariableKind.Global,
                DdfRuntimeDebugVariableKind.State => DdfRuntimeVariableKind.State,
                DdfRuntimeDebugVariableKind.Field => DdfRuntimeVariableKind.Field,
                DdfRuntimeDebugVariableKind.Element => DdfRuntimeVariableKind.Element,
                _ => DdfRuntimeVariableKind.Local
            };

        private static DdfEditorDebugVariableSnapshot FindVariable(
            IEnumerable<DdfEditorDebugVariableSnapshot> values, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;
            string[] parts = expression.Replace("]", string.Empty)
                .Split(new[] { '.', '[' }, StringSplitOptions.RemoveEmptyEntries);
            DdfEditorDebugVariableSnapshot current = values.FirstOrDefault(value =>
                string.Equals(value.Name, parts[0], StringComparison.Ordinal));
            for (int index = 1; current != null && index < parts.Length; index++)
                current = current.Children.FirstOrDefault(value =>
                    string.Equals(value.Name, parts[index], StringComparison.Ordinal) ||
                    string.Equals(value.Name, "[" + parts[index] + "]",
                        StringComparison.Ordinal));
            return current;
        }

        private List<DdfDotNetWorkerDebugBreakpoint> CreateBreakpoints(
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfBuildArtifact artifact)
        {
            var sources = artifact.CompilationUnits.SelectMany(unit => unit.Sources)
                .ToList();
            var result = new List<DdfDotNetWorkerDebugBreakpoint>();
            foreach (DdfEditorBreakpoint breakpoint in Breakpoints.Where(value =>
                         value.Enabled))
            {
                DdfEditorBuildDocumentSnapshot document = documents.FirstOrDefault(
                    value => value.Id == breakpoint.DocumentId);
                if (document == null) continue;
                DdfCompilationSourceSnapshot source = sources.FirstOrDefault(value =>
                    PathMatches(document.FilePath, value.RelativePath) ||
                    string.Equals(document.DisplayName,
                        Path.GetFileName(value.RelativePath),
                        StringComparison.OrdinalIgnoreCase));
                if (source == null) continue;
                int start = LineStart(document.SourceText, breakpoint.Line);
                result.Add(new DdfDotNetWorkerDebugBreakpoint
                {
                    SourcePath = source.RelativePath,
                    Start = start,
                    Length = Math.Max(1, LineLength(document.SourceText, start))
                });
            }
            return result;
        }

        private IReadOnlyList<DdfEditorBuildDocumentSnapshot> CaptureDocuments() =>
            (documentsProvider()?.OpenDocuments ?? Array.Empty<IDdfEditorDocument>())
            .Select(value => new DdfEditorBuildDocumentSnapshot(value.Id,
                value.DisplayName, value.FilePath, value.SourceText,
                value.Version)).ToList().AsReadOnly();

        private static Exception ContractFailure(
            IEnumerable<DdfDotNetContractIssue> issues) => new
            InvalidOperationException(string.Join("; ", issues.Select(issue =>
                issue.Code + ": " + issue.Message)));

        private static string ResolveWorkerPath(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(Path.GetFullPath(configured)))
                return Path.GetFullPath(configured);
            string environment = Environment.GetEnvironmentVariable("DDF_RUNNER_PATH");
            if (!string.IsNullOrWhiteSpace(environment) &&
                File.Exists(Path.GetFullPath(environment)))
                return Path.GetFullPath(environment);
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 7;
                 depth++, directory = directory.Parent)
            foreach (string configuration in new[] { "Release", "Debug" })
            {
                string candidate = Path.Combine(directory.FullName,
                    "DDFLanguageEditor.Runner", "bin", configuration,
                    "DDFLanguageEditor.Runner.dll");
                if (File.Exists(candidate)) return candidate;
            }
            string beside = Path.Combine(AppContext.BaseDirectory,
                "DDFLanguageEditor.Runner.dll");
            if (File.Exists(beside)) return beside;
            throw new FileNotFoundException("Worker DDF standalone non trovato.");
        }

        private static string FindNet10ReferencePack()
        {
            DirectoryInfo runtime = new(RuntimeEnvironment.GetRuntimeDirectory()
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            string packs = Path.Combine(runtime.Parent?.Parent?.Parent?.FullName ??
                string.Empty, "packs", "Microsoft.NETCore.App.Ref");
            string selected = Directory.Exists(packs) ? Directory.GetDirectories(packs)
                .Select(path => Path.Combine(path, "ref", "net10.0"))
                .Where(Directory.Exists).OrderByDescending(path => path,
                    StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;
            return selected ?? throw new DirectoryNotFoundException(
                "Reference pack Microsoft.NETCore.App.Ref net10.0 non trovato.");
        }

        private static bool PathMatches(string fullPath, string relativePath) =>
            !string.IsNullOrWhiteSpace(fullPath) &&
            !string.IsNullOrWhiteSpace(relativePath) &&
            fullPath.Replace('\\', '/').EndsWith(
                relativePath.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);

        private static int LineStart(string text, int line)
        {
            int current = 1;
            for (int index = 0; index < (text?.Length ?? 0); index++)
            {
                if (current == line) return index;
                if (text[index] == '\n') current++;
            }
            return current == line ? text?.Length ?? 0 : 0;
        }

        private static int LineLength(string text, int start)
        {
            if (string.IsNullOrEmpty(text) || start >= text.Length) return 1;
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            return Math.Max(1, end - start);
        }

        private static (int Line, int Column) LineColumn(string text, int start)
        {
            int line = 1, column = 1;
            for (int index = 0; index < Math.Min(start, text?.Length ?? 0); index++)
            {
                if (text[index] == '\n') { line++; column = 1; }
                else column++;
            }
            return (line, column);
        }

        private void RaiseState(long current, DdfEditorDebugState previous,
            DdfEditorDebugState next) => Dispatch(() => StateChanged?.Invoke(this,
                new DdfEditorDebugStateChangedEventArgs(current, previous, next)));

        private void SetCooperativeState(
            DdfEditorCooperativeExecutionState nextState)
        {
            DdfEditorCooperativeExecutionSnapshot previous;
            DdfEditorCooperativeExecutionSnapshot next;
            lock (sync)
            {
                if (cooperativeSnapshot.State == nextState) return;
                previous = cooperativeSnapshot;
                cooperativeSnapshot = next = new(executionId, generation,
                    nextState);
            }
            RaiseCooperative(previous, next);
        }

        private void RaiseCooperative(
            DdfEditorCooperativeExecutionSnapshot previous,
            DdfEditorCooperativeExecutionSnapshot next) =>
            Dispatch(() => CooperativeStateChanged?.Invoke(this,
                new DdfEditorCooperativeStateChangedEventArgs(previous, next)));

        private void Dispatch(Action callback)
        {
            if (callback == null) return;
            lock (sync)
            {
                if (disposed) return;
            }
            if (eventDispatcher.HasAccess)
            {
                callback();
                return;
            }
            eventDispatcher.Post(() =>
            {
                lock (sync)
                {
                    if (disposed) return;
                }
                callback();
            });
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(
                nameof(DdfDotNetEditorDebugService));
        }
    }
}
