using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionPluginConsentAction
    {
        Granted,
        Revoked
    }

    public enum DdfExtensionPluginConsentChange
    {
        Granted,
        Revoked,
        AlreadyGranted,
        NotGranted,
        AdmissionDenied,
        CapabilityNotDeclared,
        AuditUnavailable
    }

    public sealed class DdfExtensionPluginConsentEvent
    {
        internal DdfExtensionPluginConsentEvent(long sequence, DateTimeOffset observedAtUtc,
            DdfExtensionPluginManifest manifest, DdfExtensionPluginConsentAction action,
            string capability, string resourceId, string actorId)
        {
            Sequence = sequence;
            ObservedAtUtc = observedAtUtc.ToUniversalTime();
            PluginId = manifest.Id;
            Publisher = manifest.Publisher;
            Version = manifest.Version;
            AssemblySha256 = manifest.AssemblySha256;
            Action = action;
            Capability = capability;
            ResourceId = resourceId;
            ActorId = actorId;
        }

        public long Sequence { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string PluginId { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string AssemblySha256 { get; }
        public DdfExtensionPluginConsentAction Action { get; }
        public string Capability { get; }
        public string ResourceId { get; }
        public string ActorId { get; }
    }

    // A per-plugin session. Consent is never replayed automatically from the audit journal.
    public sealed class DdfExtensionPluginConsentLedger
    {
        private static readonly Regex ActorName = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
        private readonly object gate = new object();
        private readonly DdfExtensionPluginManifest manifest;
        private readonly DdfExtensionPluginSignature signature;
        private readonly DdfExtensionPluginPublisherTrust trust;
        private readonly byte[] assemblyBytes;
        private readonly string sdkContract;
        private readonly DdfExtensionPluginGrant fullGrant;
        private readonly DdfExtensionPluginFileAuditStore auditStore;
        private readonly DdfExtensionPluginSecurityLedger decisions;
        private readonly Dictionary<string, DdfExtensionPluginResourceGrant> resources =
            new Dictionary<string, DdfExtensionPluginResourceGrant>(StringComparer.Ordinal);

        public DdfExtensionPluginConsentLedger(DdfExtensionPluginManifest manifest,
            DdfExtensionPluginSignature signature, DdfExtensionPluginPublisherTrust trust,
            byte[] assemblyBytes, string sdkContract,
            DdfExtensionPluginFileAuditStore auditStore)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            this.signature = signature;
            this.trust = trust ?? throw new ArgumentNullException(nameof(trust));
            if (assemblyBytes == null || assemblyBytes.Length > 256 * 1024 * 1024)
                throw new ArgumentException("Assembly assente o oltre quota.", nameof(assemblyBytes));
            this.assemblyBytes = (byte[])assemblyBytes.Clone();
            this.sdkContract = sdkContract ?? throw new ArgumentNullException(nameof(sdkContract));
            this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
            fullGrant = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                manifest.Version, manifest.AssemblySha256, manifest.Capabilities);
            decisions = new DdfExtensionPluginSecurityLedger(durableStore: auditStore);
        }

        public IReadOnlyList<DdfExtensionPluginResourceGrant> ActiveScopes()
        {
            lock (gate)
                return new ReadOnlyCollection<DdfExtensionPluginResourceGrant>(
                    resources.Values.ToArray());
        }

        public DdfExtensionPluginConsentChange Approve(string capability,
            string resourceId, string actorId, DateTimeOffset observedAtUtc)
        {
            ValidateActorAndTime(actorId, observedAtUtc);
            var scope = new DdfExtensionPluginResourceGrant(manifest.Id,
                manifest.Publisher, manifest.Version, manifest.AssemblySha256,
                capability, resourceId);
            if (!manifest.Capabilities.Contains(capability, StringComparer.Ordinal))
                return DdfExtensionPluginConsentChange.CapabilityNotDeclared;
            lock (gate)
            {
                if (DdfExtensionPluginPolicy.EvaluateAdmission(manifest, signature,
                        trust, assemblyBytes, sdkContract, fullGrant) !=
                    DdfExtensionPluginDecision.Authorized)
                    return DdfExtensionPluginConsentChange.AdmissionDenied;
                string key = ScopeKey(capability, resourceId);
                if (resources.ContainsKey(key))
                    return DdfExtensionPluginConsentChange.AlreadyGranted;
                try
                {
                    auditStore.AppendConsent(manifest, DdfExtensionPluginConsentAction.Granted,
                        capability, resourceId, actorId, observedAtUtc);
                }
                catch (Exception exception) when (IsAuditFailure(exception))
                {
                    return DdfExtensionPluginConsentChange.AuditUnavailable;
                }
                resources.Add(key, scope);
                return DdfExtensionPluginConsentChange.Granted;
            }
        }

        public DdfExtensionPluginConsentChange Revoke(string capability,
            string resourceId, string actorId, DateTimeOffset observedAtUtc)
        {
            ValidateActorAndTime(actorId, observedAtUtc);
            // Validate the exact host-owned logical resource even for absent grants.
            _ = new DdfExtensionPluginResourceGrant(manifest.Id, manifest.Publisher,
                manifest.Version, manifest.AssemblySha256, capability, resourceId);
            lock (gate)
            {
                if (!resources.Remove(ScopeKey(capability, resourceId)))
                    return DdfExtensionPluginConsentChange.NotGranted;
                try
                {
                    auditStore.AppendConsent(manifest, DdfExtensionPluginConsentAction.Revoked,
                        capability, resourceId, actorId, observedAtUtc);
                    return DdfExtensionPluginConsentChange.Revoked;
                }
                catch (Exception exception) when (IsAuditFailure(exception))
                {
                    // Never retain a grant merely because the audit is unavailable.
                    return DdfExtensionPluginConsentChange.AuditUnavailable;
                }
            }
        }

        public DdfExtensionPluginOperationResult Evaluate(string capability,
            string resourceId, DateTimeOffset observedAtUtc)
        {
            lock (gate)
                return decisions.EvaluateAndRecord(manifest, signature, trust,
                    assemblyBytes, sdkContract,
                    resources.Count == 0 ? null : fullGrant,
                    resources.Values.ToArray(), capability, resourceId,
                    observedAtUtc);
        }

        private static void ValidateActorAndTime(string actorId, DateTimeOffset at)
        {
            if (actorId == null || actorId.Length > 128 || !ActorName.IsMatch(actorId))
                throw new ArgumentException("ID attore host non valido.", nameof(actorId));
            if (at == default)
                throw new ArgumentException("Istante richiesto.", nameof(at));
        }

        private static string ScopeKey(string capability, string resourceId) =>
            capability + "\0" + resourceId;

        private static bool IsAuditFailure(Exception exception) =>
            exception is IOException || exception is InvalidDataException ||
            exception is UnauthorizedAccessException ||
            exception is CryptographicException ||
            exception is DecoderFallbackException ||
            exception is ObjectDisposedException;
    }
}
