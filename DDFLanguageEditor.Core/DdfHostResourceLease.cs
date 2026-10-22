using System;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfHostResourceLeaseState
    {
        Active,
        Revoked,
        Released
    }

    public enum DdfHostResourceLeaseLifetime
    {
        Execution
    }

    public sealed class DdfHostResourceDescriptor
    {
        public DdfHostResourceDescriptor(string resourceType,
            string capabilityId, string displayName,
            string documentation = null,
            DdfHostResourceLeaseLifetime lifetime =
                DdfHostResourceLeaseLifetime.Execution)
        {
            if (!Enum.IsDefined(typeof(DdfHostResourceLeaseLifetime), lifetime))
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            ResourceType = DdfHostCatalogValidation.TypeName(resourceType,
                nameof(resourceType));
            CapabilityId = DdfHostCatalogValidation.Identifier(capabilityId,
                nameof(capabilityId));
            DisplayName = DdfHostCatalogValidation.Text(displayName,
                nameof(displayName));
            Documentation = documentation ?? string.Empty;
            Lifetime = lifetime;
        }

        public string ResourceType { get; }
        public string CapabilityId { get; }
        public string DisplayName { get; }
        public string Documentation { get; }
        public DdfHostResourceLeaseLifetime Lifetime { get; }
    }

    public sealed class DdfHostResourceLeaseRequest
    {
        internal DdfHostResourceLeaseRequest(Guid executionId,
            DdfHostResourceDescriptor descriptor, DateTimeOffset requestedAt,
            DateTimeOffset? executionDeadline)
        {
            ExecutionId = executionId;
            Descriptor = descriptor ?? throw new ArgumentNullException(
                nameof(descriptor));
            RequestedAt = requestedAt;
            ExecutionDeadline = executionDeadline;
        }

        public Guid ExecutionId { get; }
        public DdfHostResourceDescriptor Descriptor { get; }
        public DateTimeOffset RequestedAt { get; }
        public DateTimeOffset? ExecutionDeadline { get; }
    }

    public sealed class DdfHostResourceLeaseDecision
    {
        private DdfHostResourceLeaseDecision(bool granted, string reason)
        {
            Granted = granted;
            Reason = reason ?? string.Empty;
        }

        public bool Granted { get; }
        public string Reason { get; }

        public static DdfHostResourceLeaseDecision Grant() => new(true,
            string.Empty);

        public static DdfHostResourceLeaseDecision Deny(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(
                "Il motivo del diniego è obbligatorio.", nameof(reason));
            return new DdfHostResourceLeaseDecision(false, reason.Trim());
        }
    }

    public sealed class DdfHostResourceLeaseSnapshot
    {
        internal DdfHostResourceLeaseSnapshot(Guid leaseId, Guid executionId,
            DdfHostResourceDescriptor descriptor,
            DdfHostResourceLeaseState state, DateTimeOffset acquiredAt,
            DateTimeOffset? endedAt, string revocationReason)
        {
            LeaseId = leaseId;
            ExecutionId = executionId;
            Descriptor = descriptor;
            State = state;
            AcquiredAt = acquiredAt;
            EndedAt = endedAt;
            RevocationReason = revocationReason ?? string.Empty;
        }

        public Guid LeaseId { get; }
        public Guid ExecutionId { get; }
        public DdfHostResourceDescriptor Descriptor { get; }
        public DdfHostResourceLeaseState State { get; }
        public DateTimeOffset AcquiredAt { get; }
        public DateTimeOffset? EndedAt { get; }
        public string RevocationReason { get; }
        public bool IsAvailable => State == DdfHostResourceLeaseState.Active;
    }

    public interface IDdfHostResourceLeaseAuthority
    {
        ValueTask<DdfHostResourceLeaseDecision> AuthorizeAsync(
            DdfHostResourceLeaseRequest request,
            CancellationToken cancellationToken);

        void Observe(DdfHostResourceLeaseSnapshot snapshot);
    }

    public sealed class DdfHostResourceLease : IAsyncDisposable
    {
        private readonly object syncRoot = new object();
        private readonly object resource;
        private readonly IDdfHostResourceLeaseAuthority authority;
        private readonly Guid executionId;
        private readonly DateTimeOffset acquiredAt;
        private int state;
        private DateTimeOffset? endedAt;
        private string revocationReason = string.Empty;

        internal DdfHostResourceLease(Guid executionId,
            DdfHostResourceDescriptor descriptor, object resource,
            IDdfHostResourceLeaseAuthority authority)
        {
            LeaseId = Guid.NewGuid();
            this.executionId = executionId;
            Descriptor = descriptor ?? throw new ArgumentNullException(
                nameof(descriptor));
            this.resource = ValidateResource(resource);
            this.authority = authority ?? throw new ArgumentNullException(
                nameof(authority));
            acquiredAt = DateTimeOffset.UtcNow;
        }

        public Guid LeaseId { get; }
        public DdfHostResourceDescriptor Descriptor { get; }
        public DdfHostResourceLeaseSnapshot Snapshot => CreateSnapshot();

        public void EnsureAvailable()
        {
            DdfHostResourceLeaseSnapshot snapshot = Snapshot;
            if (!snapshot.IsAvailable)
                throw new InvalidOperationException("Il lease della risorsa '" +
                    Descriptor.ResourceType + "' non è più attivo.");
        }

        public ValueTask<bool> RevokeAsync(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(
                "Il motivo della revoca è obbligatorio.", nameof(reason));
            return TransitionAsync(DdfHostResourceLeaseState.Revoked,
                reason.Trim());
        }

        public ValueTask DisposeAsync() => new ValueTask(
            TransitionAsync(DdfHostResourceLeaseState.Released,
                string.Empty).AsTask());

        internal void NotifyActive() => Notify(CreateSnapshot());

        internal static async ValueTask DisposeUnregisteredAsync(object value)
        {
            object validated = ValidateResource(value);
            if (validated is IAsyncDisposable asynchronous)
                await asynchronous.DisposeAsync().ConfigureAwait(false);
            else
                ((IDisposable)validated).Dispose();
        }

        private async ValueTask<bool> TransitionAsync(
            DdfHostResourceLeaseState targetState, string reason)
        {
            int target = targetState == DdfHostResourceLeaseState.Revoked ? 1 : 2;
            lock (syncRoot)
            {
                if (state != 0) return false;
                state = target;
                endedAt = DateTimeOffset.UtcNow;
                revocationReason = reason ?? string.Empty;
            }
            try
            {
                if (resource is IAsyncDisposable asynchronous)
                    await asynchronous.DisposeAsync().ConfigureAwait(false);
                else
                    ((IDisposable)resource).Dispose();
            }
            finally
            {
                Notify(CreateSnapshot());
            }
            return true;
        }

        private DdfHostResourceLeaseSnapshot CreateSnapshot()
        {
            lock (syncRoot)
            {
                DdfHostResourceLeaseState leaseState = state switch
                {
                    1 => DdfHostResourceLeaseState.Revoked,
                    2 => DdfHostResourceLeaseState.Released,
                    _ => DdfHostResourceLeaseState.Active
                };
                return new DdfHostResourceLeaseSnapshot(LeaseId, executionId,
                    Descriptor, leaseState, acquiredAt, endedAt,
                    revocationReason);
            }
        }

        private void Notify(DdfHostResourceLeaseSnapshot snapshot)
        {
            try { authority.Observe(snapshot); }
            catch { }
        }

        private static object ValidateResource(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value is not IDisposable && value is not IAsyncDisposable)
                throw new ArgumentException(
                    "La risorsa nativa deve supportare un cleanup esplicito.",
                    nameof(value));
            return value;
        }
    }

    internal sealed class DdfHostResourceLeaseDeniedException : Exception
    {
        internal DdfHostResourceLeaseDeniedException(string resourceType)
            : base("L'host ha negato la risorsa '" + resourceType + "'.")
        {
        }
    }
}
