using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionPluginSecurityDomain
    {
        PublisherTrust,
        Compatibility,
        Authorization,
        Resource,
        Audit
    }

    public sealed class DdfExtensionPluginSecurityEvent
    {
        internal DdfExtensionPluginSecurityEvent(long sequence, DateTimeOffset observedAtUtc,
            DdfExtensionPluginManifest manifest, DdfExtensionPluginOperationResult result)
        {
            Sequence = sequence;
            ObservedAtUtc = observedAtUtc.ToUniversalTime();
            PluginId = manifest.Id;
            Publisher = manifest.Publisher;
            Version = manifest.Version;
            AssemblySha256 = manifest.AssemblySha256;
            Capability = result.Capability;
            ResourceId = result.ResourceId;
            Decision = result.Decision;
            AdmissionDecision = result.AdmissionDecision;
            (Code, Domain) = Classify(result);
        }

        public long Sequence { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string PluginId { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string AssemblySha256 { get; }
        public string Capability { get; }
        public string ResourceId { get; }
        public DdfExtensionPluginOperationDecision Decision { get; }
        public DdfExtensionPluginDecision AdmissionDecision { get; }
        public string Code { get; }
        public DdfExtensionPluginSecurityDomain Domain { get; }

        internal static (string, DdfExtensionPluginSecurityDomain) Classify(
            DdfExtensionPluginOperationResult result)
        {
            switch (result.Decision)
            {
                case DdfExtensionPluginOperationDecision.Authorized:
                    return ("DDFP000", DdfExtensionPluginSecurityDomain.Authorization);
                case DdfExtensionPluginOperationDecision.CapabilityNotDeclared:
                    return ("DDFP201", DdfExtensionPluginSecurityDomain.Authorization);
                case DdfExtensionPluginOperationDecision.ResourceNotGranted:
                    return ("DDFP202", DdfExtensionPluginSecurityDomain.Resource);
                case DdfExtensionPluginOperationDecision.ResourceRevoked:
                    return ("DDFP203", DdfExtensionPluginSecurityDomain.Resource);
                case DdfExtensionPluginOperationDecision.AuditUnavailable:
                    return ("DDFP900", DdfExtensionPluginSecurityDomain.Audit);
                default:
                    switch (result.AdmissionDecision)
                    {
                        case DdfExtensionPluginDecision.SignatureNotVerified:
                            return ("DDFP101", DdfExtensionPluginSecurityDomain.PublisherTrust);
                        case DdfExtensionPluginDecision.AssemblyHashMismatch:
                            return ("DDFP102", DdfExtensionPluginSecurityDomain.PublisherTrust);
                        case DdfExtensionPluginDecision.Incompatible:
                            return ("DDFP103", DdfExtensionPluginSecurityDomain.Compatibility);
                        case DdfExtensionPluginDecision.Revoked:
                            return ("DDFP104", DdfExtensionPluginSecurityDomain.Authorization);
                        case DdfExtensionPluginDecision.IdentityMismatch:
                            return ("DDFP105", DdfExtensionPluginSecurityDomain.Authorization);
                        case DdfExtensionPluginDecision.CapabilityDenied:
                            return ("DDFP106", DdfExtensionPluginSecurityDomain.Authorization);
                        default:
                            return ("DDFP107", DdfExtensionPluginSecurityDomain.Authorization);
                    }
            }
        }
    }

    // Volatile, bounded ledger. An owner must provide durable audit before enabling plug-ins.
    public sealed class DdfExtensionPluginSecurityLedger
    {
        private readonly object gate = new object();
        private readonly List<DdfExtensionPluginSecurityEvent> events =
            new List<DdfExtensionPluginSecurityEvent>();
        private readonly int capacity;

        public DdfExtensionPluginSecurityLedger(int capacity = 4096)
        {
            if (capacity < 1 || capacity > 100000)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public IReadOnlyList<DdfExtensionPluginSecurityEvent> Snapshot()
        {
            lock (gate)
                return new ReadOnlyCollection<DdfExtensionPluginSecurityEvent>(
                    events.ToArray());
        }

        public DdfExtensionPluginOperationResult EvaluateAndRecord(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust, byte[] assemblyBytes,
            string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string capability, string resourceId, DateTimeOffset observedAtUtc)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (observedAtUtc == default)
                throw new ArgumentException("Istante audit richiesto.", nameof(observedAtUtc));
            // Do not authorize first and discover afterward that the audit is full.
            lock (gate)
            {
                if (events.Count == capacity)
                    return new DdfExtensionPluginOperationResult(
                        DdfExtensionPluginOperationDecision.AuditUnavailable,
                        DdfExtensionPluginDecision.Denied, capability, resourceId);
                DdfExtensionPluginOperationResult result =
                    DdfExtensionPluginResourcePolicy.Evaluate(manifest, signature,
                        trust, assemblyBytes, sdkContract, grant, resourceGrants,
                        capability, resourceId);
                events.Add(new DdfExtensionPluginSecurityEvent(events.Count + 1,
                    observedAtUtc, manifest, result));
                return result;
            }
        }
    }
}
