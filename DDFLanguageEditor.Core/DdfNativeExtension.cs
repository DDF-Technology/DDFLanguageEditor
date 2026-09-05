using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfNativeExtensionRequirement
    {
        public DdfNativeExtensionRequirement(string id, string publisher, string version, string sha256,
            IEnumerable<string> capabilities)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Sha256 = (sha256 ?? throw new ArgumentNullException(nameof(sha256))).ToLowerInvariant();
            Capabilities = (capabilities ?? throw new ArgumentNullException(nameof(capabilities)))
                .ToList().AsReadOnly();
        }

        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string Sha256 { get; }
        public IReadOnlyList<string> Capabilities { get; }
    }

    public sealed class DdfNativeExtensionAuthorization
    {
        public DdfNativeExtensionAuthorization(string id, string publisher, string version, string sha256,
            IEnumerable<string> capabilities)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Sha256 = (sha256 ?? throw new ArgumentNullException(nameof(sha256))).ToLowerInvariant();
            Capabilities = (capabilities ?? throw new ArgumentNullException(nameof(capabilities)))
                .ToList().AsReadOnly();
        }

        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string Sha256 { get; }
        public IReadOnlyList<string> Capabilities { get; }
    }

    public enum DdfNativeExtensionAuthorizationStatus
    {
        AuthorizationRequired,
        IdentityMismatch,
        CapabilityNotAuthorized,
        Authorized
    }

    public sealed class DdfNativeExtensionAuthorizationResult
    {
        internal DdfNativeExtensionAuthorizationResult(DdfNativeExtensionRequirement requirement,
            DdfNativeExtensionAuthorizationStatus status, IEnumerable<string> missingCapabilities)
        {
            Requirement = requirement;
            Status = status;
            MissingCapabilities = (missingCapabilities ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public DdfNativeExtensionRequirement Requirement { get; }
        public DdfNativeExtensionAuthorizationStatus Status { get; }
        public IReadOnlyList<string> MissingCapabilities { get; }
        public bool IsAuthorized => Status == DdfNativeExtensionAuthorizationStatus.Authorized;
    }

    public static class DdfNativeExtensionAuthorizationPolicy
    {
        public static IReadOnlyList<DdfNativeExtensionAuthorizationResult> Evaluate(
            IEnumerable<DdfNativeExtensionRequirement> requirements,
            IEnumerable<DdfNativeExtensionAuthorization> authorizations)
        {
            var grants = (authorizations ?? Array.Empty<DdfNativeExtensionAuthorization>()).ToList();
            var results = new List<DdfNativeExtensionAuthorizationResult>();
            foreach (DdfNativeExtensionRequirement requirement in
                     requirements ?? Array.Empty<DdfNativeExtensionRequirement>())
            {
                DdfNativeExtensionAuthorization[] candidates = grants.Where(item =>
                    string.Equals(item.Id, requirement.Id, StringComparison.Ordinal)).ToArray();
                if (candidates.Length == 0)
                {
                    results.Add(new DdfNativeExtensionAuthorizationResult(requirement,
                        DdfNativeExtensionAuthorizationStatus.AuthorizationRequired, requirement.Capabilities));
                    continue;
                }

                DdfNativeExtensionAuthorization grant = candidates.FirstOrDefault(item =>
                    string.Equals(item.Publisher, requirement.Publisher, StringComparison.Ordinal) &&
                    string.Equals(item.Version, requirement.Version, StringComparison.Ordinal) &&
                    string.Equals(item.Sha256, requirement.Sha256, StringComparison.OrdinalIgnoreCase));
                if (grant == null)
                {
                    results.Add(new DdfNativeExtensionAuthorizationResult(requirement,
                        DdfNativeExtensionAuthorizationStatus.IdentityMismatch, requirement.Capabilities));
                    continue;
                }

                var authorizedCapabilities = new HashSet<string>(grant.Capabilities, StringComparer.Ordinal);
                string[] missing = requirement.Capabilities
                    .Where(item => !authorizedCapabilities.Contains(item)).ToArray();
                results.Add(new DdfNativeExtensionAuthorizationResult(requirement,
                    missing.Length == 0
                        ? DdfNativeExtensionAuthorizationStatus.Authorized
                        : DdfNativeExtensionAuthorizationStatus.CapabilityNotAuthorized,
                    missing));
            }
            return results.AsReadOnly();
        }
    }
}
