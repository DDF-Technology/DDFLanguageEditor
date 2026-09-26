using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfEditorDebugState
    {
        Idle,
        Running,
        Paused,
        Stopping
    }

    public sealed class DdfEditorBreakpoint
    {
        public DdfEditorBreakpoint(string documentId, int line, bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("L'identità del documento è obbligatoria.", nameof(documentId));
            if (line <= 0) throw new ArgumentOutOfRangeException(nameof(line));
            DocumentId = documentId;
            Line = line;
            Enabled = enabled;
        }

        public string DocumentId { get; }
        public int Line { get; }
        public bool Enabled { get; }
    }

    public sealed class DdfEditorDebugLocation
    {
        internal DdfEditorDebugLocation(string documentId, string displayName,
            string filePath, int line, int column, int start, int length)
        {
            DocumentId = documentId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Line = line;
            Column = column;
            Start = start;
            Length = length;
        }

        public string DocumentId { get; }
        public string DisplayName { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Column { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfEditorDebugStateChangedEventArgs : EventArgs
    {
        public DdfEditorDebugStateChangedEventArgs(long generation,
            DdfEditorDebugState previousState, DdfEditorDebugState state)
        {
            Generation = generation;
            PreviousState = previousState;
            State = state;
        }

        public long Generation { get; }
        public DdfEditorDebugState PreviousState { get; }
        public DdfEditorDebugState State { get; }
    }

    public sealed class DdfEditorDebugPausedEventArgs : EventArgs
    {
        public DdfEditorDebugPausedEventArgs(long generation,
            DdfEditorDebugSnapshot snapshot)
        {
            Generation = generation;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public long Generation { get; }
        public DdfEditorDebugSnapshot Snapshot { get; }
        public DdfEditorDebugLocation Location => Snapshot.Location;
    }

    public sealed class DdfEditorDebugVariableSnapshot
    {
        internal DdfEditorDebugVariableSnapshot(string name, string displayValue,
            string typeName, int scopeDepth, DdfRuntimeVariableKind kind,
            IReadOnlyList<DdfEditorDebugVariableSnapshot> children, bool isTruncated)
        {
            Name = name ?? string.Empty;
            DisplayValue = displayValue ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            ScopeDepth = scopeDepth;
            Kind = kind;
            Children = children ?? Array.Empty<DdfEditorDebugVariableSnapshot>();
            IsTruncated = isTruncated;
        }

        public string Name { get; }
        public string DisplayValue { get; }
        public string TypeName { get; }
        public int ScopeDepth { get; }
        public DdfRuntimeVariableKind Kind { get; }
        public IReadOnlyList<DdfEditorDebugVariableSnapshot> Children { get; }
        public bool IsTruncated { get; }
    }

    public sealed class DdfEditorDebugStackFrameSnapshot
    {
        internal DdfEditorDebugStackFrameSnapshot(int index, string functionName,
            DdfEditorDebugLocation location,
            IReadOnlyList<DdfEditorDebugVariableSnapshot> variables)
        {
            Index = index;
            FunctionName = functionName ?? string.Empty;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Variables = variables ?? Array.Empty<DdfEditorDebugVariableSnapshot>();
        }

        public int Index { get; }
        public string FunctionName { get; }
        public DdfEditorDebugLocation Location { get; }
        public IReadOnlyList<DdfEditorDebugVariableSnapshot> Variables { get; }
    }

    public sealed class DdfEditorDebugSnapshot
    {
        internal DdfEditorDebugSnapshot(DdfEditorDebugLocation location,
            IReadOnlyList<DdfEditorDebugStackFrameSnapshot> callStack,
            IReadOnlyList<DdfEditorDebugVariableSnapshot> variables,
            IReadOnlyList<DdfEditorDebugVariableSnapshot> globals)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            CallStack = callStack ?? Array.Empty<DdfEditorDebugStackFrameSnapshot>();
            Variables = variables ?? Array.Empty<DdfEditorDebugVariableSnapshot>();
            Globals = globals ?? Array.Empty<DdfEditorDebugVariableSnapshot>();
        }

        public DdfEditorDebugLocation Location { get; }
        public IReadOnlyList<DdfEditorDebugStackFrameSnapshot> CallStack { get; }
        public IReadOnlyList<DdfEditorDebugVariableSnapshot> Variables { get; }
        public IReadOnlyList<DdfEditorDebugVariableSnapshot> Globals { get; }
    }

    public sealed class DdfEditorWatchResult
    {
        internal DdfEditorWatchResult(string expression, int frameIndex,
            DdfEditorDebugVariableSnapshot value, string message)
        {
            Expression = expression ?? string.Empty;
            FrameIndex = frameIndex;
            Value = value;
            Message = message ?? string.Empty;
        }

        public string Expression { get; }
        public int FrameIndex { get; }
        public DdfEditorDebugVariableSnapshot Value { get; }
        public string Message { get; }
        public bool Available => Value != null;
    }

    public interface IDdfEditorDebugService : IDisposable
    {
        DdfEditorDebugState State { get; }
        bool IsActive { get; }
        bool IsPaused { get; }
        long Generation { get; }
        IReadOnlyList<DdfEditorBreakpoint> Breakpoints { get; }
        IReadOnlyList<string> WatchExpressions { get; }
        DdfEditorDebugLocation CurrentLocation { get; }
        DdfEditorDebugSnapshot CurrentSnapshot { get; }
        DdfEditorExecutionCompletion LastCompletion { get; }

        event EventHandler<DdfEditorDebugStateChangedEventArgs> StateChanged;
        event EventHandler<DdfEditorDebugPausedEventArgs> Paused;
        event EventHandler<DdfEditorExecutionOutputEventArgs> OutputReceived;
        event EventHandler<DdfEditorExecutionCompletedEventArgs> DebugCompleted;

        void SetBreakpoints(IEnumerable<DdfEditorBreakpoint> breakpoints);
        void SetWatchExpressions(IEnumerable<string> expressions);
        Task<DdfEditorExecutionCompletion> RunAsync(
            DdfEditorExecutionRequest request = null);
        bool Continue();
        bool StepInto();
        bool StepOver();
        bool StepOut();
        DdfEditorWatchResult EvaluateWatch(string expression, int frameIndex = 0);
        bool Stop();
    }

    /// <summary>
    /// Host-facing debug lifecycle. Breakpoints refer to document identities and
    /// are mapped to the immutable in-memory build snapshot for each run.
    /// Event handlers run on the debugger worker; graphical hosts must marshal them.
    /// </summary>
    public sealed class DdfEditorDebugService : IDdfEditorDebugService
    {
        private sealed class DocumentInfo
        {
            public DocumentInfo(IDdfEditorDocument document)
            {
                Id = document.Id;
                DisplayName = document.DisplayName;
                FilePath = document.FilePath;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string FilePath { get; }
        }

        private readonly object syncRoot = new object();
        private readonly Func<IDdfEditorDocumentService> documentServiceProvider;
        private readonly Func<IDdfLanguagePackageSession> languageSessionProvider;
        private readonly DdfEditorExecutionService executionService;
        private IReadOnlyList<DdfEditorBreakpoint> breakpoints =
            Array.Empty<DdfEditorBreakpoint>();
        private IReadOnlyList<string> watchExpressions = Array.Empty<string>();
        private DdfDebuggerSession debuggerSession;
        private DdfEditorDebugState state;
        private long generation;
        private bool disposed;
        private DdfRuntimeStateSnapshot currentRuntimeState;
        private DdfLanguageDefinition currentLanguage;

        public DdfEditorDebugService(IDdfEditorDocumentService documentService,
            IDdfLanguagePackageSession languageSession)
            : this(() => documentService, () => languageSession)
        {
            if (documentService == null) throw new ArgumentNullException(nameof(documentService));
            if (languageSession == null) throw new ArgumentNullException(nameof(languageSession));
        }

        public DdfEditorDebugService(
            Func<IDdfEditorDocumentService> documentServiceProvider,
            Func<IDdfLanguagePackageSession> languageSessionProvider,
            Func<DdfHostCatalogSnapshot> hostCatalogProvider = null,
            DdfEditorExecutionBuildHandler buildHandler = null)
        {
            this.documentServiceProvider = documentServiceProvider ??
                throw new ArgumentNullException(nameof(documentServiceProvider));
            this.languageSessionProvider = languageSessionProvider ??
                throw new ArgumentNullException(nameof(languageSessionProvider));
            executionService = new DdfEditorExecutionService(documentServiceProvider,
                languageSessionProvider, hostCatalogProvider, buildHandler);
            executionService.OutputReceived += executionService_OutputReceived;
        }

        public DdfEditorDebugState State
        {
            get { lock (syncRoot) return state; }
        }

        public bool IsActive => State != DdfEditorDebugState.Idle;
        public bool IsPaused => State == DdfEditorDebugState.Paused;

        public long Generation
        {
            get { lock (syncRoot) return generation; }
        }

        public IReadOnlyList<DdfEditorBreakpoint> Breakpoints
        {
            get { lock (syncRoot) return breakpoints; }
        }

        public IReadOnlyList<string> WatchExpressions
        {
            get { lock (syncRoot) return watchExpressions; }
        }

        public DdfEditorDebugLocation CurrentLocation { get; private set; }
        public DdfEditorDebugSnapshot CurrentSnapshot { get; private set; }
        public DdfEditorExecutionCompletion LastCompletion { get; private set; }

        public event EventHandler<DdfEditorDebugStateChangedEventArgs> StateChanged;
        public event EventHandler<DdfEditorDebugPausedEventArgs> Paused;
        public event EventHandler<DdfEditorExecutionOutputEventArgs> OutputReceived;
        public event EventHandler<DdfEditorExecutionCompletedEventArgs> DebugCompleted;

        public void SetBreakpoints(IEnumerable<DdfEditorBreakpoint> items)
        {
            var replacement = (items ?? Array.Empty<DdfEditorBreakpoint>())
                .Where(item => item != null)
                .GroupBy(item => item.DocumentId + "\n" + item.Line,
                    StringComparer.Ordinal)
                .Select(group => group.Last()).ToList().AsReadOnly();
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Idle)
                    throw new InvalidOperationException(
                        "I breakpoint non possono essere sostituiti durante una sessione di debug.");
                breakpoints = replacement;
            }
        }

        public void SetWatchExpressions(IEnumerable<string> expressions)
        {
            var replacement = new List<string>();
            foreach (string expression in expressions ?? Array.Empty<string>())
            {
                string value = (expression ?? string.Empty).Trim();
                if (value.Length == 0) continue;
                if (value.Length > 512)
                    throw new ArgumentException(
                        "Ogni espressione Watch può contenere al massimo 512 caratteri.",
                        nameof(expressions));
                if (!replacement.Contains(value, StringComparer.Ordinal))
                    replacement.Add(value);
            }
            lock (syncRoot)
            {
                ThrowIfDisposed();
                watchExpressions = replacement.AsReadOnly();
            }
        }

        public Task<DdfEditorExecutionCompletion> RunAsync(
            DdfEditorExecutionRequest request = null)
        {
            request = request ?? new DdfEditorExecutionRequest();
            if (request.Mode == DdfEditorExecutionMode.AnalysisOnly)
                throw new ArgumentException(
                    "Il debug richiede Simulation oppure HostEffects.", nameof(request));
            Dictionary<string, DocumentInfo> documents = CaptureDocuments();
            DdfDebuggerSession session;
            IReadOnlyList<DdfEditorBreakpoint> currentBreakpoints;
            long currentGeneration;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Idle)
                    throw new InvalidOperationException("Una sessione di debug è già attiva.");
                currentGeneration = ++generation;
                session = new DdfDebuggerSession();
                debuggerSession = session;
                currentBreakpoints = breakpoints;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                currentLanguage = languageSessionProvider()?.ActivePackage?.Language;
                state = DdfEditorDebugState.Running;
            }
            RaiseStateChanged(currentGeneration, DdfEditorDebugState.Idle,
                DdfEditorDebugState.Running);

            request.DebugBinding = new DdfEditorDebugBinding
            {
                Session = session,
                Breakpoints = currentBreakpoints,
                Paused = (pause, documentId, documentIdsByPath) => OnPaused(
                    currentGeneration, session, pause, documentId, documents,
                    documentIdsByPath)
            };

            try
            {
                return CompleteRunAsync(currentGeneration, session,
                    executionService.RunAsync(request));
            }
            catch
            {
                FinishFailedStart(currentGeneration, session);
                throw;
            }
        }

        public bool Continue()
        {
            return Resume(session => session.Continue());
        }

        public bool StepInto()
        {
            return Resume(session => session.StepInto());
        }

        public bool StepOver()
        {
            return Resume(session => session.StepOver());
        }

        public bool StepOut()
        {
            return Resume(session => session.StepOut());
        }

        private bool Resume(Action<DdfDebuggerSession> resume)
        {
            DdfDebuggerSession session;
            long currentGeneration;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Paused) return false;
                currentGeneration = generation;
                session = debuggerSession;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                state = DdfEditorDebugState.Running;
            }
            if (session != null) resume(session);
            RaiseStateChanged(currentGeneration, DdfEditorDebugState.Paused,
                DdfEditorDebugState.Running);
            return true;
        }

        public bool Stop()
        {
            DdfDebuggerSession session;
            long currentGeneration;
            DdfEditorDebugState previousState;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Running &&
                    state != DdfEditorDebugState.Paused) return false;
                previousState = state;
                currentGeneration = generation;
                session = debuggerSession;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                state = DdfEditorDebugState.Stopping;
            }
            executionService.Stop();
            session?.Continue();
            RaiseStateChanged(currentGeneration, previousState,
                DdfEditorDebugState.Stopping);
            return true;
        }

        public void Dispose()
        {
            DdfDebuggerSession session;
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                session = debuggerSession;
                debuggerSession = null;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                currentLanguage = null;
            }
            executionService.OutputReceived -= executionService_OutputReceived;
            executionService.Dispose();
            session?.Dispose();
        }

        private async Task<DdfEditorExecutionCompletion> CompleteRunAsync(
            long currentGeneration, DdfDebuggerSession session,
            Task<DdfEditorExecutionCompletion> execution)
        {
            DdfEditorExecutionCompletion completion = await execution.ConfigureAwait(false);
            DdfEditorDebugState previousState;
            lock (syncRoot)
            {
                previousState = state;
                if (ReferenceEquals(debuggerSession, session)) debuggerSession = null;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                currentLanguage = null;
                state = DdfEditorDebugState.Idle;
                LastCompletion = completion;
            }
            session.Dispose();
            DebugCompleted?.Invoke(this,
                new DdfEditorExecutionCompletedEventArgs(completion));
            RaiseStateChanged(currentGeneration, previousState,
                DdfEditorDebugState.Idle);
            return completion;
        }

        private void FinishFailedStart(long currentGeneration,
            DdfDebuggerSession session)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(debuggerSession, session)) debuggerSession = null;
                CurrentLocation = null;
                CurrentSnapshot = null;
                currentRuntimeState = null;
                currentLanguage = null;
                state = DdfEditorDebugState.Idle;
            }
            session.Dispose();
            RaiseStateChanged(currentGeneration, DdfEditorDebugState.Running,
                DdfEditorDebugState.Idle);
        }

        private void OnPaused(long currentGeneration, DdfDebuggerSession session,
            DdfDebugPauseInfo pause, string documentId,
            IReadOnlyDictionary<string, DocumentInfo> documents,
            IReadOnlyDictionary<string, string> documentIdsByPath)
        {
            DdfEditorDebugSnapshot snapshot = CreateSnapshot(pause, documentId,
                documents, documentIdsByPath);
            lock (syncRoot)
            {
                if (disposed || generation != currentGeneration ||
                    !ReferenceEquals(debuggerSession, session) ||
                    state != DdfEditorDebugState.Running) return;
                CurrentSnapshot = snapshot;
                CurrentLocation = snapshot.Location;
                currentRuntimeState = pause.State;
                state = DdfEditorDebugState.Paused;
            }
            RaiseStateChanged(currentGeneration, DdfEditorDebugState.Running,
                DdfEditorDebugState.Paused);
            Paused?.Invoke(this,
                new DdfEditorDebugPausedEventArgs(currentGeneration, snapshot));
        }

        public DdfEditorWatchResult EvaluateWatch(string expression, int frameIndex = 0)
        {
            DdfRuntimeStateSnapshot runtimeState;
            DdfLanguageDefinition language;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (state != DdfEditorDebugState.Paused || currentRuntimeState == null)
                    return new DdfEditorWatchResult(expression, frameIndex, null,
                        "In attesa di una pausa del debugger.");
                if (frameIndex < 0 || frameIndex >= currentRuntimeState.Frames.Count)
                    return new DdfEditorWatchResult(expression, frameIndex, null,
                        "Frame del Call Stack non disponibile.");
                runtimeState = CreateFrameWatchState(currentRuntimeState, frameIndex);
                language = currentLanguage;
            }

            DdfWatchResult result = DdfWatch.Evaluate(expression, runtimeState, language);
            return new DdfEditorWatchResult(expression, frameIndex,
                result.Value == null ? null : CloneVariable(result.Value), result.Message);
        }

        private static DdfRuntimeStateSnapshot CreateFrameWatchState(
            DdfRuntimeStateSnapshot state, int frameIndex)
        {
            if (frameIndex == 0) return state;
            var variables = new List<DdfRuntimeVariableSnapshot>();
            variables.AddRange(state.Frames[frameIndex].Variables);
            variables.AddRange(state.Variables.Where(variable =>
                variable.Kind == DdfRuntimeVariableKind.Global));
            return new DdfRuntimeStateSnapshot(variables.AsReadOnly(),
                state.CallStack, state.Frames);
        }

        private static DdfEditorDebugSnapshot CreateSnapshot(DdfDebugPauseInfo pause,
            string currentDocumentId, IReadOnlyDictionary<string, DocumentInfo> documents,
            IReadOnlyDictionary<string, string> documentIdsByPath)
        {
            string resolvedCurrentId = ResolveDocumentId(currentDocumentId,
                pause.DocumentPath, documentIdsByPath);
            DdfEditorDebugLocation location = CreateLocation(resolvedCurrentId,
                pause.DocumentPath, pause.Line, pause.Column, pause.Start, pause.Length,
                documents);
            IReadOnlyList<DdfEditorDebugVariableSnapshot> variables =
                CloneVariables(pause.State?.Variables);
            IReadOnlyList<DdfEditorDebugVariableSnapshot> globals = variables
                .Where(item => item.Kind == DdfRuntimeVariableKind.Global)
                .ToList().AsReadOnly();
            var frames = new List<DdfEditorDebugStackFrameSnapshot>();
            IReadOnlyList<DdfRuntimeFrameSnapshot> runtimeFrames =
                pause.State?.Frames ?? Array.Empty<DdfRuntimeFrameSnapshot>();
            for (int index = 0; index < runtimeFrames.Count; index++)
            {
                DdfRuntimeFrameSnapshot runtimeFrame = runtimeFrames[index];
                DdfRuntimeStackFrame frame = runtimeFrame.Frame;
                if (frame == null) continue;
                string frameDocumentId = ResolveDocumentId(null, frame.DocumentPath,
                    documentIdsByPath);
                DdfEditorDebugLocation frameLocation = CreateLocation(frameDocumentId,
                    frame.DocumentPath, frame.Line, frame.Column, frame.Start, frame.Length,
                    documents);
                frames.Add(new DdfEditorDebugStackFrameSnapshot(index,
                    frame.FunctionName, frameLocation,
                    CloneVariables(runtimeFrame.Variables)));
            }
            return new DdfEditorDebugSnapshot(location, frames.AsReadOnly(),
                variables, globals);
        }

        private static DdfEditorDebugLocation CreateLocation(string documentId,
            string runtimePath, int line, int column, int start, int length,
            IReadOnlyDictionary<string, DocumentInfo> documents)
        {
            documents.TryGetValue(documentId ?? string.Empty, out DocumentInfo document);
            return new DdfEditorDebugLocation(documentId, document?.DisplayName,
                document?.FilePath ?? runtimePath, line, column, start, length);
        }

        private static string ResolveDocumentId(string documentId, string runtimePath,
            IReadOnlyDictionary<string, string> documentIdsByPath)
        {
            if (!string.IsNullOrEmpty(documentId)) return documentId;
            return documentIdsByPath != null && documentIdsByPath.TryGetValue(
                NormalizePath(runtimePath), out string resolved) ? resolved : string.Empty;
        }

        private static IReadOnlyList<DdfEditorDebugVariableSnapshot> CloneVariables(
            IReadOnlyList<DdfRuntimeVariableSnapshot> variables)
        {
            if (variables == null || variables.Count == 0)
                return Array.Empty<DdfEditorDebugVariableSnapshot>();
            return variables.Select(CloneVariable)
                .ToList().AsReadOnly();
        }

        private static DdfEditorDebugVariableSnapshot CloneVariable(
            DdfRuntimeVariableSnapshot variable) =>
            new DdfEditorDebugVariableSnapshot(variable.Name, variable.DisplayValue,
                variable.TypeName, variable.ScopeDepth, variable.Kind,
                CloneVariables(variable.Children), variable.IsTruncated);

        private static string NormalizePath(string path) =>
            (path ?? string.Empty).Replace('/', '\\');

        private Dictionary<string, DocumentInfo> CaptureDocuments()
        {
            IReadOnlyList<IDdfEditorDocument> documents =
                documentServiceProvider()?.OpenDocuments;
            return (documents ?? Array.Empty<IDdfEditorDocument>()).ToDictionary(
                document => document.Id, document => new DocumentInfo(document),
                StringComparer.Ordinal);
        }

        private void executionService_OutputReceived(object sender,
            DdfEditorExecutionOutputEventArgs e)
        {
            long currentGeneration;
            lock (syncRoot)
            {
                if (disposed || state == DdfEditorDebugState.Idle) return;
                currentGeneration = generation;
            }
            OutputReceived?.Invoke(this,
                new DdfEditorExecutionOutputEventArgs(currentGeneration, e.Text));
        }

        private void RaiseStateChanged(long currentGeneration,
            DdfEditorDebugState previousState, DdfEditorDebugState nextState)
        {
            StateChanged?.Invoke(this, new DdfEditorDebugStateChangedEventArgs(
                currentGeneration, previousState, nextState));
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DdfEditorDebugService));
        }
    }
}
