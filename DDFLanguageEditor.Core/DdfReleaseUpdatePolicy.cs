using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public static class DdfSemanticVersionOrder
    {
        public static int Compare(string left, string right)
        {
            ParsedVersion leftVersion = Parse(left);
            ParsedVersion rightVersion = Parse(right);
            int result = leftVersion.Major.CompareTo(rightVersion.Major);
            if (result != 0) return result;
            result = leftVersion.Minor.CompareTo(rightVersion.Minor);
            if (result != 0) return result;
            result = leftVersion.Patch.CompareTo(rightVersion.Patch);
            if (result != 0) return result;
            if (leftVersion.Prerelease.Length == 0)
                return rightVersion.Prerelease.Length == 0 ? 0 : 1;
            if (rightVersion.Prerelease.Length == 0) return -1;
            int count = Math.Max(leftVersion.Prerelease.Length,
                rightVersion.Prerelease.Length);
            for (int index = 0; index < count; index++)
            {
                if (index >= leftVersion.Prerelease.Length) return -1;
                if (index >= rightVersion.Prerelease.Length) return 1;
                string leftPart = leftVersion.Prerelease[index];
                string rightPart = rightVersion.Prerelease[index];
                bool leftNumeric = int.TryParse(leftPart, out int leftNumber);
                bool rightNumeric = int.TryParse(rightPart, out int rightNumber);
                if (leftNumeric && rightNumeric)
                    result = leftNumber.CompareTo(rightNumber);
                else if (leftNumeric != rightNumeric)
                    result = leftNumeric ? -1 : 1;
                else
                    result = string.CompareOrdinal(leftPart, rightPart);
                if (result != 0) return result;
            }
            return 0;
        }

        public static bool IsPrerelease(string version) =>
            Parse(version).Prerelease.Length != 0;

        private static ParsedVersion Parse(string value)
        {
            string normalized = DdfProductUpdateResult.RequireVersion(value,
                nameof(value));
            string withoutBuild = normalized.Split('+')[0];
            string[] versionAndPrerelease = withoutBuild.Split(new[] { '-' }, 2);
            string[] numbers = versionAndPrerelease[0].Split('.');
            return new ParsedVersion(int.Parse(numbers[0]),
                int.Parse(numbers[1]), int.Parse(numbers[2]),
                versionAndPrerelease.Length == 1
                    ? Array.Empty<string>()
                    : versionAndPrerelease[1].Split('.'));
        }

        private sealed class ParsedVersion
        {
            public ParsedVersion(int major, int minor, int patch,
                string[] prerelease)
            {
                Major = major;
                Minor = minor;
                Patch = patch;
                Prerelease = prerelease;
            }
            public int Major { get; }
            public int Minor { get; }
            public int Patch { get; }
            public string[] Prerelease { get; }
        }
    }

    public static class DdfReleaseUpdatePolicy
    {
        public static DdfProductUpdateResult Evaluate(
            DdfReleaseManifestVerification verification,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, DateTimeOffset checkedAtUtc)
        {
            if (verification == null) throw new ArgumentNullException(
                nameof(verification));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!verification.Success)
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    checkedAtUtc, verification.Issues.Count == 0
                        ? "Manifesto release non verificato."
                        : string.Join(" ", verification.Issues));

            DdfReleaseManifest manifest = verification.Manifest;
            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            if (!string.Equals(manifest.ProductId, identity.ProductId,
                    StringComparison.Ordinal))
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    checkedAtUtc, "Identità prodotto del manifesto non autorizzata.");
            int versionOrder = DdfSemanticVersionOrder.Compare(manifest.Version,
                request.CurrentVersion);
            if (versionOrder < 0)
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    checkedAtUtc, "Downgrade rifiutato: " + manifest.Version + ".");
            if (versionOrder == 0)
                return DdfProductUpdateResult.UpToDate(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, manifest.Version,
                    request.Channel, checkedAtUtc);
            if (request.Channel == DdfProductUpdateChannel.Stable &&
                (manifest.Channel != DdfProductUpdateChannel.Stable ||
                 DdfSemanticVersionOrder.IsPrerelease(manifest.Version)))
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    checkedAtUtc, "Release prerelease esclusa dal canale stable.");

            string rid = NormalizeToken(runtimeIdentifier);
            string architectureValue = NormalizeToken(architecture);
            DdfReleaseManifestAsset[] matchingAssets = manifest.Assets.Where(item =>
                item.ComponentKind == request.ComponentKind &&
                string.Equals(item.ComponentId, request.ComponentId,
                    StringComparison.Ordinal) &&
                (item.RuntimeIdentifier == "any" || item.RuntimeIdentifier == rid) &&
                (item.Architecture == "any" ||
                 item.Architecture == architectureValue)).ToArray();
            if (matchingAssets.Length != 1)
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    checkedAtUtc, matchingAssets.Length == 0
                        ? "Nessun asset compatibile con RID e architettura."
                        : "Asset ambiguo per RID e architettura.");
            DdfReleaseManifestAsset asset = matchingAssets[0];

            var reasons = new List<string>();
            if (DdfReleaseCompatibility.CompareMajorMinor(
                    identity.SdkContractVersion,
                    manifest.Compatibility.MinimumSdkContractVersion) < 0 ||
                DdfReleaseCompatibility.CompareMajorMinor(
                    identity.SdkContractVersion,
                    manifest.Compatibility.MaximumSdkContractVersion) > 0)
                reasons.Add("Contratto SDK corrente fuori intervallo.");
            foreach (KeyValuePair<string, string> current in
                identity.PersistenceVersions)
            {
                if (!manifest.Compatibility.PersistenceVersions.TryGetValue(
                        current.Key, out string accepted) ||
                    !string.Equals(current.Value, accepted,
                        StringComparison.Ordinal))
                    reasons.Add("Schema persistito incompatibile: " + current.Key + ".");
            }

            return DdfProductUpdateResult.Available(request.ComponentKind,
                request.ComponentId, request.CurrentVersion, manifest.Version,
                request.Channel, reasons.Count == 0
                    ? DdfProductUpdateCompatibility.Compatible
                    : DdfProductUpdateCompatibility.Incompatible,
                checkedAtUtc, manifest.PublishedAtUtc, manifest.ReleaseNotes,
                asset.Uri, reasons);
        }

        private static string NormalizeToken(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0)
                throw new ArgumentException("RID o architettura richiesti.");
            return normalized;
        }
    }
}
