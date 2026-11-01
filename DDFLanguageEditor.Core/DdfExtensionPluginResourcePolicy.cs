using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    // A resource is an opaque host-owned ID, never a filesystem path or endpoint.
    public sealed class DdfExtensionPluginResourceGrant
    {
        private static readonly Regex ResourceName = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public DdfExtensionPluginResourceGrant(string id, string publisher,
            string version, string assemblySha256, string capability,
            string resourceId, bool revoked = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(publisher) ||
                string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(capability))
                throw new ArgumentException("Identità e capability richieste.");
            if (!DdfExtensionPluginCapabilityCatalog.TryGet(capability, out _))
                throw new ArgumentException("Capability sconosciuta.", nameof(capability));
            if (resourceId == null || resourceId.Length > 128 ||
                !ResourceName.IsMatch(resourceId))
                throw new ArgumentException("ID risorsa logica non valido.", nameof(resourceId));
            Id = id;
            Publisher = publisher;
            Version = version;
            AssemblySha256 = DdfExtensionPluginManifest.RequireSha256(assemblySha256);
            Capability = capability;
            ResourceId = resourceId;
            Revoked = revoked;
        }

        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string AssemblySha256 { get; }
        public string Capability { get; }
        public string ResourceId { get; }
        public bool Revoked { get; }

        internal bool Matches(DdfExtensionPluginManifest manifest, string capability,
            string resourceId) =>
            string.Equals(Id, manifest.Id, StringComparison.Ordinal) &&
            string.Equals(Publisher, manifest.Publisher, StringComparison.Ordinal) &&
            string.Equals(Version, manifest.Version, StringComparison.Ordinal) &&
            string.Equals(AssemblySha256, manifest.AssemblySha256, StringComparison.Ordinal) &&
            string.Equals(Capability, capability, StringComparison.Ordinal) &&
            string.Equals(ResourceId, resourceId, StringComparison.Ordinal);
    }

    public enum DdfExtensionPluginOperationDecision
    {
        AdmissionDenied,
        CapabilityNotDeclared,
        ResourceNotGranted,
        ResourceRevoked,
        Authorized,
        AuditUnavailable
    }

    public sealed class DdfExtensionPluginOperationResult
    {
        internal DdfExtensionPluginOperationResult(DdfExtensionPluginOperationDecision decision,
            DdfExtensionPluginDecision admissionDecision, string capability,
            string resourceId)
        {
            Decision = decision;
            AdmissionDecision = admissionDecision;
            Capability = capability;
            ResourceId = resourceId;
        }

        public DdfExtensionPluginOperationDecision Decision { get; }
        public DdfExtensionPluginDecision AdmissionDecision { get; }
        public string Capability { get; }
        public string ResourceId { get; }
        public bool IsAuthorized => Decision == DdfExtensionPluginOperationDecision.Authorized;
        public string SecurityCode => DdfExtensionPluginSecurityEvent.Classify(this).Item1;
        public DdfExtensionPluginSecurityDomain SecurityDomain =>
            DdfExtensionPluginSecurityEvent.Classify(this).Item2;
    }

    public static class DdfExtensionPluginResourcePolicy
    {
        // The caller must still enforce the decision at the host resource boundary.
        public static DdfExtensionPluginOperationResult Evaluate(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust, byte[] assemblyBytes,
            string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string capability, string resourceId)
        {
            if (string.IsNullOrWhiteSpace(capability))
                throw new ArgumentException("Capability richiesta.", nameof(capability));
            if (string.IsNullOrWhiteSpace(resourceId))
                throw new ArgumentException("Risorsa logica richiesta.", nameof(resourceId));
            DdfExtensionPluginDecision admission =
                DdfExtensionPluginPolicy.EvaluateAdmission(manifest, signature,
                    trust, assemblyBytes, sdkContract, grant);
            if (admission != DdfExtensionPluginDecision.Authorized)
                return new DdfExtensionPluginOperationResult(
                    DdfExtensionPluginOperationDecision.AdmissionDenied,
                    admission, capability, resourceId);
            if (!manifest.Capabilities.Contains(capability, StringComparer.Ordinal))
                return new DdfExtensionPluginOperationResult(
                    DdfExtensionPluginOperationDecision.CapabilityNotDeclared,
                    admission, capability, resourceId);
            DdfExtensionPluginResourceGrant[] matches =
                (resourceGrants ?? Array.Empty<DdfExtensionPluginResourceGrant>())
                .Where(item => item != null && item.Matches(manifest,
                    capability, resourceId)).Take(2).ToArray();
            // Duplicate records are ambiguous and cannot grant access.
            DdfExtensionPluginOperationDecision decision = matches.Length == 0 ||
                matches.Length > 1
                ? DdfExtensionPluginOperationDecision.ResourceNotGranted
                : matches[0].Revoked
                    ? DdfExtensionPluginOperationDecision.ResourceRevoked
                    : DdfExtensionPluginOperationDecision.Authorized;
            return new DdfExtensionPluginOperationResult(decision, admission,
                capability, resourceId);
        }
    }
}
