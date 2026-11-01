using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    // Host-side single-use correlation only; not authentication or a dispatcher.
    public sealed class DdfExtensionCallCorrelationLedger
    {
        private readonly object gate = new object();
        private readonly Dictionary<Guid, PendingCall> pending = new Dictionary<Guid, PendingCall>();
        private readonly TimeProvider clock;
        private readonly TimeSpan timeout;
        private readonly int maximumPending;

        public DdfExtensionCallCorrelationLedger(TimeSpan timeout,
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

        public DdfExtensionCallTicket Issue(DdfExtensionCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Guid id = Guid.NewGuid();
            Guid originalId = id;
            string json = DdfExtensionCallFrameCodec.SerializeRequest(id, request);
            lock (gate)
            {
                long now = clock.GetTimestamp();
                PruneExpired(now);
                if (pending.Count >= maximumPending)
                    throw new InvalidOperationException("Quota di chiamate pendenti raggiunta.");
                while (pending.ContainsKey(id)) id = Guid.NewGuid();
                if (id != originalId)
                    json = DdfExtensionCallFrameCodec.SerializeRequest(id, request);
                pending.Add(id, new PendingCall(request, now));
            }
            return new DdfExtensionCallTicket(id, json);
        }

        public DdfExtensionCallResponse Accept(string responseJson)
        {
            (Guid id, string payload) = DdfExtensionCallFrameCodec.ParseResponse(responseJson);
            PendingCall call;
            lock (gate)
            {
                if (!pending.TryGetValue(id, out call))
                    throw new InvalidOperationException("Call ID sconosciuto o già consumato.");
                pending.Remove(id); // Consume before validating untrusted response content.
                if (Expired(call, clock.GetTimestamp()))
                    throw new TimeoutException("Risposta arrivata dopo la scadenza.");
            }
            return DdfExtensionCallResponseCodec.Parse(payload, call.Request);
        }

        public bool Cancel(Guid callId)
        {
            lock (gate) return pending.Remove(callId);
        }

        private void PruneExpired(long now)
        {
            foreach (Guid id in pending.Where(item => Expired(item.Value, now))
                .Select(item => item.Key).ToArray())
                pending.Remove(id);
        }

        private bool Expired(PendingCall call, long now) => now < call.StartTimestamp ||
            clock.GetElapsedTime(call.StartTimestamp, now) >= timeout;

        private sealed class PendingCall
        {
            internal PendingCall(DdfExtensionCallRequest request, long startTimestamp)
            {
                Request = request;
                StartTimestamp = startTimestamp;
            }

            internal DdfExtensionCallRequest Request { get; }
            internal long StartTimestamp { get; }
        }
    }
}
