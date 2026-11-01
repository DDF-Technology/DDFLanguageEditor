using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    // A description and an authorization decision only. This file never loads plug-in code.
    public sealed class DdfExtensionPluginManifest
    {
        public const string SchemaVersionValue = "1.0";
        private static readonly Regex Identifier = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
        private static readonly Regex Capability = new Regex(
            "^(?:file|network|process|device|ui|native)\\.[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
            RegexOptions.CultureInvariant);
        private static readonly Regex AssemblyName = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9._-]*\\.dll$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public DdfExtensionPluginManifest(string id, string publisher, string version,
            string minimumSdkContract, string maximumSdkContract, string assemblyFile,
            string assemblySha256, IEnumerable<string> dependencies,
            IEnumerable<string> capabilities)
        {
            Id = RequireIdentifier(id, nameof(id));
            Publisher = RequireIdentifier(publisher, nameof(publisher));
            Version = DdfProductUpdateResult.RequireVersion(version, nameof(version));
            MinimumSdkContract = RequireContract(minimumSdkContract, nameof(minimumSdkContract));
            MaximumSdkContract = RequireContract(maximumSdkContract, nameof(maximumSdkContract));
            if (new Version(MinimumSdkContract).CompareTo(new Version(MaximumSdkContract)) > 0)
                throw new ArgumentException("Intervallo SDK invertito.");
            if (assemblyFile == null || assemblyFile.Length > 128 ||
                !AssemblyName.IsMatch(assemblyFile))
                throw new ArgumentException("Il file assembly deve essere un nome .dll locale.", nameof(assemblyFile));
            AssemblyFile = assemblyFile;
            AssemblySha256 = RequireSha256(assemblySha256);
            Dependencies = CopyDistinct(dependencies, "dipendenze", value =>
                RequireIdentifier(value, nameof(dependencies)));
            if (Dependencies.Contains(Id, StringComparer.Ordinal))
                throw new ArgumentException("Un plug-in non può dipendere da sé stesso.", nameof(dependencies));
            Capabilities = CopyDistinct(capabilities, "capability", value =>
                Capability.IsMatch(value ?? string.Empty) ? value :
                    throw new ArgumentException("Capability sconosciuta o non valida.", nameof(capabilities)));
        }

        public string SchemaVersion => SchemaVersionValue;
        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string MinimumSdkContract { get; }
        public string MaximumSdkContract { get; }
        public string AssemblyFile { get; }
        public string AssemblySha256 { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<string> Capabilities { get; }

        public bool SupportsSdkContract(string version)
        {
            var requested = new Version(RequireContract(version, nameof(version)));
            return requested.CompareTo(new Version(MinimumSdkContract)) >= 0 &&
                requested.CompareTo(new Version(MaximumSdkContract)) <= 0;
        }

        internal static string RequireSha256(string value)
        {
            if (value == null || value.Length != 64 || value.Any(character =>
                !Uri.IsHexDigit(character)))
                throw new ArgumentException("SHA-256 non valido.", nameof(value));
            return value.ToLowerInvariant();
        }

        private static string RequireIdentifier(string value, string name)
        {
            if (value == null || value.Length > 128 || !Identifier.IsMatch(value))
                throw new ArgumentException("Identificatore non valido.", name);
            return value;
        }

        private static string RequireContract(string value, string name)
        {
            string[] parts = (value ?? string.Empty).Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int major) ||
                !int.TryParse(parts[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int minor) ||
                parts[0] != major.ToString(CultureInfo.InvariantCulture) ||
                parts[1] != minor.ToString(CultureInfo.InvariantCulture))
                throw new ArgumentException("Versione contratto major.minor non valida.", name);
            return value;
        }

        private static IReadOnlyList<string> CopyDistinct(IEnumerable<string> source,
            string description, Func<string, string> validate)
        {
            if (source == null) throw new ArgumentNullException(description);
            var items = source.Select(validate).ToArray();
            if (items.Length > 64 || items.Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException(description + " duplicate o oltre quota.", description);
            return new ReadOnlyCollection<string>(items);
        }
    }

    public static class DdfExtensionPluginManifestCodec
    {
        private static readonly string[] Names = { "schemaVersion", "id", "publisher", "version",
            "minimumSdkContract", "maximumSdkContract", "assemblyFile", "assemblySha256",
            "dependencies", "capabilities" };

        public static DdfExtensionPluginManifest Parse(string json)
        {
            if (json == null || json.Length > 65536)
                throw new ArgumentException("Manifest assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Il manifest deve essere un oggetto.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!Names.Contains(property.Name, StringComparer.Ordinal) ||
                    !seen.Add(property.Name))
                    throw new FormatException("Campo sconosciuto o duplicato: " + property.Name);
            }
            if (seen.Count != Names.Length ||
                ReadString(root, "schemaVersion") != DdfExtensionPluginManifest.SchemaVersionValue)
                throw new FormatException("Schema manifest assente o non supportato.");
            return new DdfExtensionPluginManifest(ReadString(root, "id"),
                ReadString(root, "publisher"), ReadString(root, "version"),
                ReadString(root, "minimumSdkContract"),
                ReadString(root, "maximumSdkContract"),
                ReadString(root, "assemblyFile"), ReadString(root, "assemblySha256"),
                ReadArray(root, "dependencies"), ReadArray(root, "capabilities"));
        }

        private static string ReadString(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String)
                throw new FormatException("Campo stringa non valido: " + name);
            return value.GetString();
        }

        private static string[] ReadArray(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 64)
                throw new FormatException("Array non valido: " + name);
            return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() : throw new FormatException("Elemento non valido: " + name)).ToArray();
        }
    }

    public enum DdfExtensionPluginDecision
    {
        Denied,
        Incompatible,
        IdentityMismatch,
        CapabilityDenied,
        Revoked,
        Authorized,
        SignatureNotVerified,
        AssemblyHashMismatch
    }

    public sealed class DdfExtensionPluginGrant
    {
        public DdfExtensionPluginGrant(string id, string publisher, string version,
            string assemblySha256, IEnumerable<string> capabilities, bool revoked = false)
        {
            Id = id;
            Publisher = publisher;
            Version = version;
            AssemblySha256 = DdfExtensionPluginManifest.RequireSha256(assemblySha256);
            Capabilities = new ReadOnlyCollection<string>((capabilities ??
                throw new ArgumentNullException(nameof(capabilities))).ToArray());
            Revoked = revoked;
        }

        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string AssemblySha256 { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public bool Revoked { get; }
    }

    public static class DdfExtensionPluginPolicy
    {
        public static DdfExtensionPluginDecision EvaluateAdmission(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust, byte[] assemblyBytes,
            string sdkContract, DdfExtensionPluginGrant grant)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (assemblyBytes == null || assemblyBytes.Length > 256 * 1024 * 1024)
                throw new ArgumentException("Assembly assente o oltre quota.", nameof(assemblyBytes));
            if (DdfExtensionPluginSignatureVerifier.Verify(manifest, signature, trust) !=
                DdfExtensionPluginSignatureStatus.Verified)
                return DdfExtensionPluginDecision.SignatureNotVerified;
            string actualHash = Convert.ToHexString(SHA256.HashData(assemblyBytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, manifest.AssemblySha256, StringComparison.Ordinal))
                return DdfExtensionPluginDecision.AssemblyHashMismatch;
            return Evaluate(manifest, sdkContract, grant);
        }

        // Consent only: does not attest publisher identity or assembly bytes.
        public static DdfExtensionPluginDecision Evaluate(DdfExtensionPluginManifest manifest,
            string sdkContract, DdfExtensionPluginGrant grant)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (!manifest.SupportsSdkContract(sdkContract))
                return DdfExtensionPluginDecision.Incompatible;
            if (grant == null) return DdfExtensionPluginDecision.Denied;
            if (!string.Equals(manifest.Id, grant.Id, StringComparison.Ordinal) ||
                !string.Equals(manifest.Publisher, grant.Publisher, StringComparison.Ordinal) ||
                !string.Equals(manifest.Version, grant.Version, StringComparison.Ordinal) ||
                !string.Equals(manifest.AssemblySha256, grant.AssemblySha256, StringComparison.Ordinal))
                return DdfExtensionPluginDecision.IdentityMismatch;
            if (grant.Revoked) return DdfExtensionPluginDecision.Revoked;
            return grant.Capabilities.Count == manifest.Capabilities.Count &&
                manifest.Capabilities.All(item => grant.Capabilities.Contains(item,
                    StringComparer.Ordinal)) ? DdfExtensionPluginDecision.Authorized :
                DdfExtensionPluginDecision.CapabilityDenied;
        }
    }
}
