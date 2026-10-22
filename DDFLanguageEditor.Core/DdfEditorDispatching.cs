using System;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Host-owned destination for observable editor events. Implementations
    /// must enqueue callbacks in FIFO order and must not execute Post inline
    /// when HasAccess is false.
    /// </summary>
    public interface IDdfEditorEventDispatcher
    {
        bool HasAccess { get; }
        void Post(Action callback);
    }

    /// <summary>
    /// Compatibility dispatcher used when a host does not opt into marshaling.
    /// Events remain on the thread that produced them.
    /// </summary>
    public sealed class DdfEditorInlineEventDispatcher :
        IDdfEditorEventDispatcher
    {
        public static DdfEditorInlineEventDispatcher Instance { get; } = new();

        private DdfEditorInlineEventDispatcher() { }

        public bool HasAccess => true;

        public void Post(Action callback) =>
            (callback ?? throw new ArgumentNullException(nameof(callback)))();
    }

    /// <summary>
    /// Backend-neutral adapter for UI frameworks and host event loops that
    /// expose a SynchronizationContext.
    /// </summary>
    public sealed class DdfEditorSynchronizationContextDispatcher :
        IDdfEditorEventDispatcher
    {
        private readonly SynchronizationContext context;

        public DdfEditorSynchronizationContextDispatcher(
            SynchronizationContext context)
        {
            this.context = context ?? throw new ArgumentNullException(
                nameof(context));
        }

        public bool HasAccess => ReferenceEquals(
            SynchronizationContext.Current, context);

        public void Post(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            context.Post(static state => ((Action)state)(), callback);
        }
    }

    /// <summary>
    /// Host-selected scheduler for DDF work. Implementations must execute the
    /// operation outside the calling/UI thread and must invoke it exactly once.
    /// </summary>
    public interface IDdfEditorExecutionScheduler
    {
        Task<TResult> ScheduleAsync<TResult>(Func<Task<TResult>> operation);
    }

    /// <summary>Default scheduler backed by a dedicated long-running worker.</summary>
    public sealed class DdfEditorDedicatedExecutionScheduler :
        IDdfEditorExecutionScheduler
    {
        public static DdfEditorDedicatedExecutionScheduler Instance { get; } = new();

        private DdfEditorDedicatedExecutionScheduler() { }

        public Task<TResult> ScheduleAsync<TResult>(Func<Task<TResult>> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            return Task.Factory.StartNew(operation, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>
    /// Executes the presentation portion of a UI capability on the host event
    /// dispatcher while the DDF worker remains asynchronously suspended.
    /// </summary>
    public interface IDdfEditorUiCapabilityInvoker
    {
        ValueTask<TResult> InvokeAsync<TResult>(string capabilityId,
            Func<CancellationToken, ValueTask<TResult>> presentation,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Framework-neutral, single-pending-request UI capability boundary.
    /// Capability identifiers must belong to the reserved <c>ui.</c> namespace.
    /// </summary>
    public sealed class DdfEditorUiCapabilityInvoker :
        IDdfEditorUiCapabilityInvoker, IDisposable
    {
        private readonly object syncRoot = new();
        private readonly IDdfEditorEventDispatcher dispatcher;
        private object pending;
        private bool disposed;

        public DdfEditorUiCapabilityInvoker(IDdfEditorEventDispatcher dispatcher)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(
                nameof(dispatcher));
        }

        public ValueTask<TResult> InvokeAsync<TResult>(string capabilityId,
            Func<CancellationToken, ValueTask<TResult>> presentation,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(capabilityId) ||
                !capabilityId.StartsWith("ui.", StringComparison.Ordinal))
                throw new ArgumentException(
                    "Una capability grafica deve usare il namespace 'ui.'.",
                    nameof(capabilityId));
            if (presentation == null) throw new ArgumentNullException(
                nameof(presentation));
            cancellationToken.ThrowIfCancellationRequested();

            var request = new PendingRequest<TResult>(this, presentation,
                cancellationToken);
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(
                    nameof(DdfEditorUiCapabilityInvoker));
                if (pending != null) throw new InvalidOperationException(
                    "È già attiva una richiesta capability UI per questo host.");
                pending = request;
            }
            request.RegisterCancellation();
            try
            {
                dispatcher.Post(request.Start);
            }
            catch
            {
                Complete(request);
                request.DisposeRegistration();
                throw;
            }
            return new ValueTask<TResult>(request.Task);
        }

        public void Dispose()
        {
            IPendingRequest request;
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                request = pending as IPendingRequest;
                pending = null;
            }
            request?.CancelForDisposal();
        }

        private bool IsCurrent(object request)
        {
            lock (syncRoot) return !disposed && ReferenceEquals(pending, request);
        }

        private void Complete(object request)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(pending, request)) pending = null;
            }
        }

        private interface IPendingRequest
        {
            void CancelForDisposal();
        }

        private sealed class PendingRequest<TResult> : IPendingRequest
        {
            private readonly DdfEditorUiCapabilityInvoker owner;
            private readonly Func<CancellationToken, ValueTask<TResult>> presentation;
            private readonly CancellationToken cancellationToken;
            private readonly TaskCompletionSource<TResult> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration cancellationRegistration;

            public PendingRequest(DdfEditorUiCapabilityInvoker owner,
                Func<CancellationToken, ValueTask<TResult>> presentation,
                CancellationToken cancellationToken)
            {
                this.owner = owner;
                this.presentation = presentation;
                this.cancellationToken = cancellationToken;
            }

            public Task<TResult> Task => completion.Task;

            public void RegisterCancellation()
            {
                if (cancellationToken.CanBeCanceled)
                    cancellationRegistration = cancellationToken.Register(
                        static state => ((PendingRequest<TResult>)state)
                            .Cancel(), this);
            }

            public void DisposeRegistration() => cancellationRegistration.Dispose();

            public void Start()
            {
                if (!owner.IsCurrent(this) || cancellationToken.IsCancellationRequested)
                {
                    Cancel();
                    return;
                }
                _ = PresentAsync();
            }

            private async Task PresentAsync()
            {
                try
                {
                    TResult result = await presentation(cancellationToken);
                    completion.TrySetResult(result);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    owner.Complete(this);
                    cancellationRegistration.Dispose();
                }
            }

            private void Cancel()
            {
                completion.TrySetCanceled(cancellationToken);
                owner.Complete(this);
                cancellationRegistration.Dispose();
            }

            public void CancelForDisposal()
            {
                completion.TrySetException(new ObjectDisposedException(
                    nameof(DdfEditorUiCapabilityInvoker)));
                cancellationRegistration.Dispose();
            }
        }
    }
}
