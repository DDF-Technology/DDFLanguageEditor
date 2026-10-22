using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Per-execution context supplied only to explicitly registered host operations.
    /// Resources registered here are released in reverse order when the VM ends.
    /// </summary>
    public sealed class DdfHostInvocationContext
    {
        private readonly Action<object> registerResource;

        internal DdfHostInvocationContext(Guid executionId,
            DdfEditorExecutionMode mode, CancellationToken cancellationToken,
            DateTimeOffset startedAt, DateTimeOffset? deadline,
            Action<object> registerResource,
            IReadOnlyDictionary<string, string> databaseTransactions = null,
            string hostEffectTransactionId = null)
        {
            ExecutionId = executionId;
            Mode = mode;
            CancellationToken = cancellationToken;
            StartedAt = startedAt;
            Deadline = deadline;
            this.registerResource = registerResource ??
                throw new ArgumentNullException(nameof(registerResource));
            DatabaseTransactions = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(databaseTransactions ??
                    new Dictionary<string, string>(), StringComparer.Ordinal));
            HostEffectTransactionId = hostEffectTransactionId ?? string.Empty;
        }

        public Guid ExecutionId { get; }
        public DdfEditorExecutionMode Mode { get; }
        public CancellationToken CancellationToken { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? Deadline { get; }
        public IReadOnlyDictionary<string, string> DatabaseTransactions { get; }
        public string HostEffectTransactionId { get; }
        public bool HasHostEffectTransaction => HostEffectTransactionId.Length > 0;

        public bool TryGetDatabaseTransaction(string databaseId,
            out string transactionId) => DatabaseTransactions.TryGetValue(
                databaseId ?? string.Empty, out transactionId);

        public void RegisterResource(IDisposable resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            registerResource(resource);
        }

        public void RegisterAsyncResource(IAsyncDisposable resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            registerResource(resource);
        }

        public async ValueTask<DdfHostResourceLease> RegisterResourceLeaseAsync(
            DdfHostResourceDescriptor descriptor, object resource,
            IDdfHostResourceLeaseAuthority authority)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (authority == null) throw new ArgumentNullException(nameof(authority));

            var request = new DdfHostResourceLeaseRequest(ExecutionId, descriptor,
                DateTimeOffset.UtcNow, Deadline);
            DdfHostResourceLeaseDecision decision;
            try
            {
                decision = await authority.AuthorizeAsync(request,
                    CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await DdfHostResourceLease.DisposeUnregisteredAsync(resource)
                    .ConfigureAwait(false);
                throw;
            }
            if (decision == null)
            {
                await DdfHostResourceLease.DisposeUnregisteredAsync(resource)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    "L'autorità lease ha restituito una decisione nulla.");
            }
            if (!decision.Granted)
            {
                await DdfHostResourceLease.DisposeUnregisteredAsync(resource)
                    .ConfigureAwait(false);
                throw new DdfHostResourceLeaseDeniedException(
                    descriptor.ResourceType);
            }

            var lease = new DdfHostResourceLease(ExecutionId, descriptor,
                resource, authority);
            try
            {
                registerResource(lease);
                lease.NotifyActive();
                return lease;
            }
            catch
            {
                await lease.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
