using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    // Bounded single-use correlation, not a worker, permission grant or dispatcher.
    public sealed class DdfExtensionSourceCorrelationLedger
    {
        private readonly object gate = new object();
        private readonly Dictionary<Guid, Pending> pending = new Dictionary<Guid, Pending>();
        private readonly TimeProvider clock;
        private readonly TimeSpan timeout;
        private readonly int maximumPending;

        public DdfExtensionSourceCorrelationLedger(TimeSpan timeout,
            int maximumPending = 128, TimeProvider clock = null)
        {
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (maximumPending < 1 || maximumPending > 128)
                throw new ArgumentOutOfRangeException(nameof(maximumPending));
            this.timeout = timeout;
            this.maximumPending = maximumPending;
            this.clock = clock ?? TimeProvider.System;
        }

        public int PendingCount
        {
            get
            {
                lock (gate)
                {
                    PruneExpired(clock.GetTimestamp());
                    return pending.Count;
                }
            }
        }

        public DdfExtensionSourceTicket Issue(DdfExtensionSemanticRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return IssueCore(DdfExtensionSourceOperationKind.Semantic, request, id =>
                DdfExtensionSourceFrameCodec.SerializeRequest(id, request));
        }

        public DdfExtensionSourceTicket Issue(DdfExtensionBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return IssueCore(DdfExtensionSourceOperationKind.Build, request, id =>
                DdfExtensionSourceFrameCodec.SerializeRequest(id, request));
        }

        public DdfExtensionSemanticResponse AcceptSemantic(string responseJson) =>
            (DdfExtensionSemanticResponse)AcceptCore(responseJson,
                DdfExtensionSourceOperationKind.Semantic);

        public DdfExtensionBuildResponse AcceptBuild(string responseJson) =>
            (DdfExtensionBuildResponse)AcceptCore(responseJson,
                DdfExtensionSourceOperationKind.Build);

        public bool Cancel(Guid callId)
        {
            lock (gate) return pending.Remove(callId);
        }

        private DdfExtensionSourceTicket IssueCore(DdfExtensionSourceOperationKind kind,
            object request, Func<Guid, string> serialize)
        {
            Guid id = Guid.NewGuid();
            Guid originalId = id;
            string json = serialize(id);
            lock (gate)
            {
                long now = clock.GetTimestamp();
                PruneExpired(now);
                if (pending.Count >= maximumPending)
                    throw new InvalidOperationException("Quota richieste sorgente pendenti raggiunta.");
                while (pending.ContainsKey(id)) id = Guid.NewGuid();
                if (id != originalId)
                    json = serialize(id);
                pending.Add(id, new Pending(kind, request, now));
            }
            return new DdfExtensionSourceTicket(id, kind, json);
        }

        private object AcceptCore(string json, DdfExtensionSourceOperationKind expected)
        {
            (Guid id, DdfExtensionSourceOperationKind kind, string payload) =
                DdfExtensionSourceFrameCodec.ParseResponse(json);
            Pending call;
            lock (gate)
            {
                if (!pending.TryGetValue(id, out call))
                    throw new DdfExtensionCallUnknownException();
                pending.Remove(id); // Consume before validating untrusted response content.
                if (Expired(call, clock.GetTimestamp()))
                    throw new DdfExtensionCallExpiredException();
            }
            if (kind != expected || kind != call.Kind)
                throw new FormatException("Tipo risposta sorgente non associato alla richiesta.");
            return kind == DdfExtensionSourceOperationKind.Semantic
                ? DdfExtensionSemanticResponseCodec.Parse(payload,
                    (DdfExtensionSemanticRequest)call.Request)
                : DdfExtensionBuildResponseCodec.Parse(payload,
                    (DdfExtensionBuildRequest)call.Request);
        }

        private void PruneExpired(long now)
        {
            foreach (Guid id in pending.Where(item => Expired(item.Value, now))
                .Select(item => item.Key).ToArray())
                pending.Remove(id);
        }

        private bool Expired(Pending call, long now) => now < call.StartTimestamp ||
            clock.GetElapsedTime(call.StartTimestamp, now) >= timeout;

        private sealed class Pending
        {
            internal Pending(DdfExtensionSourceOperationKind kind, object request,
                long startTimestamp)
            {
                Kind = kind;
                Request = request;
                StartTimestamp = startTimestamp;
            }

            internal DdfExtensionSourceOperationKind Kind { get; }
            internal object Request { get; }
            internal long StartTimestamp { get; }
        }
    }
}
