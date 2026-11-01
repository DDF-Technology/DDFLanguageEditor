using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionPluginGraphAdmissionResult
    {
        internal DdfExtensionPluginGraphAdmissionResult(IEnumerable<string> orderedIds,
            IEnumerable<string> issues)
        {
            OrderedIds = new ReadOnlyCollection<string>(orderedIds.ToArray());
            Issues = new ReadOnlyCollection<string>(issues.ToArray());
        }

        public IReadOnlyList<string> OrderedIds { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool Success => Issues.Count == 0;
    }

    public static class DdfExtensionPluginGraphAdmission
    {
        // A fresh all-or-nothing decision. It neither loads code nor grants resource scopes.
        public static DdfExtensionPluginGraphAdmissionResult Evaluate(string rootId,
            DdfExtensionPluginDependencyLockFile lockFile,
            IEnumerable<DdfExtensionPluginArtifact> artifacts,
            DdfExtensionPluginPublisherTrust trust, string sdkContract,
            IEnumerable<DdfExtensionPluginGrant> grants)
        {
            DdfExtensionPluginArtifact[] available = (artifacts ??
                throw new ArgumentNullException(nameof(artifacts))).ToArray();
            DdfExtensionPluginGrant[] consent = (grants ??
                Array.Empty<DdfExtensionPluginGrant>()).ToArray();
            if (consent.Length > 128 || consent.Any(item => item == null) ||
                consent.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
                consent.Length)
                return Fail("DDFX400: Consensi duplicati o oltre quota.");
            DdfExtensionPluginDependencyResolution resolved =
                DdfExtensionPluginDependencyResolver.Resolve(rootId, lockFile,
                    available, trust, sdkContract);
            if (!resolved.Success)
                return new DdfExtensionPluginGraphAdmissionResult(
                    Array.Empty<string>(), resolved.Issues);
            var byId = available.ToDictionary(item => item.Manifest.Id,
                StringComparer.Ordinal);
            var byGrant = consent.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            var issues = new List<string>();
            foreach (string id in resolved.OrderedIds)
            {
                if (!byGrant.TryGetValue(id, out DdfExtensionPluginGrant grant))
                {
                    issues.Add("DDFX401: Consenso specifico mancante: " + id);
                    continue;
                }
                DdfExtensionPluginArtifact artifact = byId[id];
                DdfExtensionPluginDecision decision =
                    DdfExtensionPluginPolicy.EvaluateAdmission(
                        artifact.Manifest, artifact.Signature, trust,
                        artifact.BytesForVerification, sdkContract, grant);
                if (decision != DdfExtensionPluginDecision.Authorized)
                    issues.Add("DDFX402: Consenso non valido per " + id + ": " + decision);
            }
            foreach (DdfExtensionPluginGrant grant in consent)
                if (!resolved.OrderedIds.Contains(grant.Id, StringComparer.Ordinal))
                    issues.Add("DDFX403: Consenso estraneo al grafo: " + grant.Id);
            return new DdfExtensionPluginGraphAdmissionResult(
                issues.Count == 0 ? resolved.OrderedIds : Array.Empty<string>(), issues);
        }

        private static DdfExtensionPluginGraphAdmissionResult Fail(string issue) =>
            new DdfExtensionPluginGraphAdmissionResult(Array.Empty<string>(),
                new[] { issue });
    }
}
