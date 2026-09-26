using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfHostEffectPolicy
    {
        Immediate,
        RollbackOnFailure
    }

    public enum DdfHostEffectKind
    {
        Function,
        ObjectWrite
    }

    public enum DdfHostEffectStatus
    {
        Started,
        Applied,
        Failed,
        Simulated
    }

    public enum DdfHostEffectDisposition
    {
        None,
        Simulated,
        Applied,
        PartiallyApplied,
        Committed,
        RolledBack,
        BeginFailed,
        CommitFailed,
        RollbackFailed
    }

    public sealed class DdfHostEffectRecord
    {
        internal DdfHostEffectRecord(int sequence, string operationId,
            DdfHostEffectKind kind, DdfHostEffectStatus status, string error)
        {
            Sequence = sequence;
            OperationId = operationId ?? string.Empty;
            Kind = kind;
            Status = status;
            Error = error ?? string.Empty;
        }

        public int Sequence { get; }
        public string OperationId { get; }
        public DdfHostEffectKind Kind { get; }
        public DdfHostEffectStatus Status { get; }
        public string Error { get; }
    }

    public sealed class DdfHostEffectTransactionContext
    {
        internal DdfHostEffectTransactionContext(Guid executionId, long generation,
            DateTimeOffset startedAt, CancellationToken cancellationToken)
        {
            ExecutionId = executionId;
            Generation = generation;
            StartedAt = startedAt;
            CancellationToken = cancellationToken;
        }

        public Guid ExecutionId { get; }
        public long Generation { get; }
        public DateTimeOffset StartedAt { get; }
        public CancellationToken CancellationToken { get; }
    }

    public interface IDdfHostEffectTransaction : IAsyncDisposable
    {
        string Id { get; }
        ValueTask CommitAsync();
        ValueTask RollbackAsync();
    }

    public interface IDdfHostEffectTransactionFactory
    {
        ValueTask<IDdfHostEffectTransaction> BeginAsync(
            DdfHostEffectTransactionContext context);
    }

    public sealed class DdfHostEffectSummary
    {
        internal DdfHostEffectSummary(DdfHostEffectPolicy policy,
            DdfHostEffectDisposition disposition,
            IEnumerable<DdfHostEffectRecord> operations,
            string transactionId = null, string error = null)
        {
            Policy = policy;
            Disposition = disposition;
            Operations = (operations ?? Array.Empty<DdfHostEffectRecord>())
                .ToList().AsReadOnly();
            TransactionId = transactionId ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public DdfHostEffectPolicy Policy { get; }
        public DdfHostEffectDisposition Disposition { get; }
        public IReadOnlyList<DdfHostEffectRecord> Operations { get; }
        public string TransactionId { get; }
        public string Error { get; }
        public bool HasEffects => Operations.Count > 0;
    }

    internal sealed class DdfHostEffectTracker
    {
        private sealed class Entry
        {
            public int Sequence { get; set; }
            public string OperationId { get; set; }
            public DdfHostEffectKind Kind { get; set; }
            public DdfHostEffectStatus Status { get; set; }
            public string Error { get; set; }
        }

        private readonly object syncRoot = new object();
        private readonly List<Entry> entries = new List<Entry>();

        public object Begin(string operationId, DdfHostEffectKind kind,
            bool simulated)
        {
            lock (syncRoot)
            {
                var entry = new Entry
                {
                    Sequence = entries.Count + 1,
                    OperationId = operationId ?? string.Empty,
                    Kind = kind,
                    Status = simulated ? DdfHostEffectStatus.Simulated :
                        DdfHostEffectStatus.Started,
                    Error = string.Empty
                };
                entries.Add(entry);
                return entry;
            }
        }

        public void Applied(object token)
        {
            if (!(token is Entry entry) ||
                entry.Status == DdfHostEffectStatus.Simulated) return;
            lock (syncRoot) entry.Status = DdfHostEffectStatus.Applied;
        }

        public void Failed(object token, Exception exception)
        {
            if (!(token is Entry entry)) return;
            lock (syncRoot)
            {
                if (entry.Status == DdfHostEffectStatus.Applied ||
                    entry.Status == DdfHostEffectStatus.Simulated) return;
                entry.Status = DdfHostEffectStatus.Failed;
                entry.Error = exception?.Message ?? string.Empty;
            }
        }

        public IReadOnlyList<DdfHostEffectRecord> Snapshot()
        {
            lock (syncRoot)
                return entries.Select(entry => new DdfHostEffectRecord(
                    entry.Sequence, entry.OperationId, entry.Kind,
                    entry.Status, entry.Error)).ToList().AsReadOnly();
        }
    }
}
