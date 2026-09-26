using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfFlowchartSnapshotState
    {
        Current,
        Partial,
        Unavailable
    }

    public sealed class DdfFlowchartSnapshot
    {
        internal DdfFlowchartSnapshot(long requestId, long documentVersion, string source,
            string functionName, string documentPath, DdfFlowchartBuildResult buildResult,
            DateTimeOffset createdAtUtc)
        {
            RequestId = requestId;
            DocumentVersion = documentVersion;
            Source = source ?? string.Empty;
            FunctionName = functionName ?? string.Empty;
            DocumentPath = documentPath ?? string.Empty;
            BuildResult = buildResult ?? throw new ArgumentNullException(nameof(buildResult));
            CreatedAtUtc = createdAtUtc;
            Layout = buildResult.Flowchart == null ? null : DdfFlowchartLayouter.Layout(buildResult.Flowchart);
        }

        public long RequestId { get; }
        public long DocumentVersion { get; }
        public string Source { get; }
        public string FunctionName { get; }
        public string DocumentPath { get; }
        public DdfFlowchartBuildResult BuildResult { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public DdfFlowchart Flowchart => BuildResult.Flowchart;
        public DdfFlowchartLayout Layout { get; }
        public bool Success => BuildResult.Success;
        public DdfFlowchartSnapshotState State => Layout == null
            ? DdfFlowchartSnapshotState.Unavailable
            : BuildResult.Diagnostics.Any(item => item.Severity == DdfDiagnosticSeverity.Error)
                ? DdfFlowchartSnapshotState.Partial
                : DdfFlowchartSnapshotState.Current;
    }

    /// <summary>
    /// Serializes requests for flowchart snapshots and guarantees that only the
    /// newest request can complete successfully. Parsing and graph construction
    /// run away from the caller's synchronization context.
    /// </summary>
    public sealed class DdfFlowchartSnapshotService : IDisposable
    {
        private readonly object syncRoot = new object();
        private CancellationTokenSource currentCancellation;
        private long latestRequestId;
        private bool disposed;

        public long LatestRequestId
        {
            get { lock (syncRoot) return latestRequestId; }
        }

        public Task<DdfFlowchartSnapshot> RequestAsync(string source, string functionName,
            long documentVersion, DdfLanguageDefinition language = null, string documentPath = null,
            TimeSpan? debounceDelay = null, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function name is required.", nameof(functionName));
            TimeSpan delay = debounceDelay ?? TimeSpan.FromMilliseconds(120);
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(debounceDelay));

            CancellationTokenSource requestCancellation;
            long requestId;
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(nameof(DdfFlowchartSnapshotService));
                currentCancellation?.Cancel();
                requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                currentCancellation = requestCancellation;
                requestId = ++latestRequestId;
            }

            DdfLanguageDefinition capturedLanguage = language ?? DdfLanguageCatalog.Default;
            return BuildAsync(requestId, documentVersion, source, functionName, documentPath,
                () => DdfFlowchartBuilder.Build(source, functionName, capturedLanguage, documentPath),
                delay, requestCancellation);
        }

        public Task<DdfFlowchartSnapshot> RequestAsync(DdfIrModule module, string functionName,
            long documentVersion, IEnumerable<DdfDiagnostic> diagnostics = null,
            TimeSpan? debounceDelay = null,
            CancellationToken cancellationToken = default)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function name is required.", nameof(functionName));
            TimeSpan delay = debounceDelay ?? TimeSpan.FromMilliseconds(120);
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(debounceDelay));
            CancellationTokenSource requestCancellation;
            long requestId;
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(nameof(DdfFlowchartSnapshotService));
                currentCancellation?.Cancel();
                requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                currentCancellation = requestCancellation;
                requestId = ++latestRequestId;
            }
            return BuildAsync(requestId, documentVersion, module.Source, functionName, module.DocumentPath,
                () => DdfFlowchartBuilder.Build(module, functionName, diagnostics), delay, requestCancellation);
        }

        public void Cancel()
        {
            lock (syncRoot) currentCancellation?.Cancel();
        }

        public bool IsCurrent(DdfFlowchartSnapshot snapshot)
        {
            if (snapshot == null) return false;
            lock (syncRoot) return !disposed && snapshot.RequestId == latestRequestId;
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                cancellation = currentCancellation;
                currentCancellation = null;
            }
            cancellation?.Cancel();
        }

        private async Task<DdfFlowchartSnapshot> BuildAsync(long requestId, long documentVersion,
            string source, string functionName, string documentPath,
            Func<DdfFlowchartBuildResult> build, TimeSpan delay,
            CancellationTokenSource requestCancellation)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, requestCancellation.Token).ConfigureAwait(false);
                requestCancellation.Token.ThrowIfCancellationRequested();
                DdfFlowchartBuildResult result = await Task.Run(
                    build,
                    requestCancellation.Token).ConfigureAwait(false);
                requestCancellation.Token.ThrowIfCancellationRequested();

                lock (syncRoot)
                {
                    if (disposed || requestId != latestRequestId)
                        throw new OperationCanceledException(requestCancellation.Token);
                }

                return new DdfFlowchartSnapshot(requestId, documentVersion, source, functionName,
                    documentPath, result, DateTimeOffset.UtcNow);
            }
            finally
            {
                lock (syncRoot)
                {
                    if (ReferenceEquals(currentCancellation, requestCancellation))
                        currentCancellation = null;
                }
                requestCancellation.Dispose();
            }
        }
    }
}
