using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionPluginLockEntry
    {
        private static readonly Regex Identifier = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public DdfExtensionPluginLockEntry(string id, string publisher, string version,
            string assemblySha256, string manifestSha256)
        {
            if (id == null || id.Length > 128 || !Identifier.IsMatch(id) ||
                publisher == null || publisher.Length > 128 || !Identifier.IsMatch(publisher))
                throw new ArgumentException("Identità plug-in nel lock non valida.");
            Id = id;
            Publisher = publisher;
            Version = DdfProductUpdateResult.RequireVersion(version, nameof(version));
            AssemblySha256 = DdfExtensionPluginManifest.RequireSha256(assemblySha256);
            ManifestSha256 = DdfExtensionPluginManifest.RequireSha256(manifestSha256);
        }

        public string Id { get; }
        public string Publisher { get; }
        public string Version { get; }
        public string AssemblySha256 { get; }
        public string ManifestSha256 { get; }

        public static DdfExtensionPluginLockEntry FromManifest(DdfExtensionPluginManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            string digest = Convert.ToHexString(SHA256.HashData(
                DdfExtensionPluginSignatureVerifier.CanonicalPayload(manifest)))
                .ToLowerInvariant();
            return new DdfExtensionPluginLockEntry(manifest.Id, manifest.Publisher,
                manifest.Version, manifest.AssemblySha256, digest);
        }

        internal bool Matches(DdfExtensionPluginManifest manifest)
        {
            DdfExtensionPluginLockEntry actual = FromManifest(manifest);
            return Id == actual.Id && Publisher == actual.Publisher &&
                Version == actual.Version && AssemblySha256 == actual.AssemblySha256 &&
                ManifestSha256 == actual.ManifestSha256;
        }
    }

    public sealed class DdfExtensionPluginDependencyLockFile
    {
        public const string SchemaVersionValue = "1.0";
        public DdfExtensionPluginDependencyLockFile(
            IEnumerable<DdfExtensionPluginLockEntry> entries)
        {
            var copy = (entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
            if (copy.Length == 0 || copy.Length > 128 || copy.Any(item => item == null) ||
                copy.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Lock plug-in vuoto, duplicato o oltre quota.", nameof(entries));
            Entries = new ReadOnlyCollection<DdfExtensionPluginLockEntry>(
                copy.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());
        }

        public string SchemaVersion => SchemaVersionValue;
        public IReadOnlyList<DdfExtensionPluginLockEntry> Entries { get; }
    }

    public static class DdfExtensionPluginDependencyLockCodec
    {
        public static string Serialize(DdfExtensionPluginDependencyLockFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return JsonSerializer.Serialize(new
            {
                schemaVersion = file.SchemaVersion,
                entries = file.Entries.Select(item => new
                {
                    id = item.Id, publisher = item.Publisher, version = item.Version,
                    assemblySha256 = item.AssemblySha256,
                    manifestSha256 = item.ManifestSha256
                })
            });
        }

        public static DdfExtensionPluginDependencyLockFile Parse(string json)
        {
            if (json == null || json.Length > 131072)
                throw new ArgumentException("Lock plug-in assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 5, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            RequireProperties(root, "schemaVersion", "entries");
            if (root.GetProperty("schemaVersion").GetString() !=
                DdfExtensionPluginDependencyLockFile.SchemaVersionValue)
                throw new FormatException("Schema lock plug-in non supportato.");
            JsonElement entries = root.GetProperty("entries");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 128)
                throw new FormatException("Voci lock non valide.");
            return new DdfExtensionPluginDependencyLockFile(entries.EnumerateArray()
                .Select(item =>
                {
                    RequireProperties(item, "id", "publisher", "version",
                        "assemblySha256", "manifestSha256");
                    return new DdfExtensionPluginLockEntry(
                        Read(item, "id"), Read(item, "publisher"),
                        Read(item, "version"), Read(item, "assemblySha256"),
                        Read(item, "manifestSha256"));
                }));
        }

        private static string Read(JsonElement item, string name)
        {
            JsonElement value = item.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String)
                throw new FormatException("Campo lock non testuale: " + name);
            return value.GetString();
        }

        private static void RequireProperties(JsonElement item, params string[] names)
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new FormatException("Oggetto lock richiesto.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in item.EnumerateObject())
                if (!names.Contains(property.Name, StringComparer.Ordinal) ||
                    !seen.Add(property.Name))
                    throw new FormatException("Campo lock sconosciuto o duplicato.");
            if (seen.Count != names.Length)
                throw new FormatException("Campo lock mancante.");
        }
    }

    public sealed class DdfExtensionPluginArtifact
    {
        private readonly byte[] assemblyBytes;
        public DdfExtensionPluginArtifact(DdfExtensionPluginManifest manifest,
            DdfExtensionPluginSignature signature, byte[] assemblyBytes)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Signature = signature;
            if (assemblyBytes == null || assemblyBytes.Length > 256 * 1024 * 1024)
                throw new ArgumentException("Assembly assente o oltre quota.", nameof(assemblyBytes));
            this.assemblyBytes = (byte[])assemblyBytes.Clone();
        }

        public DdfExtensionPluginManifest Manifest { get; }
        public DdfExtensionPluginSignature Signature { get; }
        public byte[] AssemblyBytes => (byte[])assemblyBytes.Clone();
        internal byte[] BytesForVerification => assemblyBytes;
    }

    public sealed class DdfExtensionPluginDependencyResolution
    {
        internal DdfExtensionPluginDependencyResolution(IEnumerable<string> orderedIds,
            IEnumerable<string> issues)
        {
            OrderedIds = new ReadOnlyCollection<string>(orderedIds.ToArray());
            Issues = new ReadOnlyCollection<string>(issues.ToArray());
        }

        public IReadOnlyList<string> OrderedIds { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool Success => Issues.Count == 0;
    }

    public static class DdfExtensionPluginDependencyResolver
    {
        // Verification only: no assembly is loaded and no capability is granted.
        public static DdfExtensionPluginDependencyResolution Resolve(string rootId,
            DdfExtensionPluginDependencyLockFile lockFile,
            IEnumerable<DdfExtensionPluginArtifact> artifacts,
            DdfExtensionPluginPublisherTrust trust, string sdkContract)
        {
            if (lockFile == null) throw new ArgumentNullException(nameof(lockFile));
            if (trust == null) throw new ArgumentNullException(nameof(trust));
            if (string.IsNullOrWhiteSpace(rootId))
                throw new ArgumentException("Plug-in radice richiesto.", nameof(rootId));
            if (string.IsNullOrWhiteSpace(sdkContract))
                throw new ArgumentException("Contratto SDK richiesto.", nameof(sdkContract));
            DdfExtensionPluginArtifact[] available = (artifacts ??
                throw new ArgumentNullException(nameof(artifacts))).ToArray();
            if (available.Length > 128 || available.Any(item => item == null) ||
                available.Select(item => item.Manifest.Id).Distinct(StringComparer.Ordinal).Count() !=
                available.Length)
                return Fail("DDFP301: Catalogo plug-in duplicato o oltre quota.");
            var byId = available.ToDictionary(item => item.Manifest.Id,
                StringComparer.Ordinal);
            var locked = lockFile.Entries.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var ordered = new List<string>();
            var issues = new List<string>();

            void Visit(string id)
            {
                if (issues.Count != 0) return;
                if (states.TryGetValue(id, out int state))
                {
                    if (state == 1) issues.Add("DDFP306: Ciclo dipendenze: " + id);
                    return;
                }
                if (!locked.TryGetValue(id, out DdfExtensionPluginLockEntry pin) ||
                    !byId.TryGetValue(id, out DdfExtensionPluginArtifact artifact))
                {
                    issues.Add("DDFP302: Dipendenza assente dal lock o dal catalogo: " + id);
                    return;
                }
                DdfExtensionPluginManifest manifest = artifact.Manifest;
                if (!pin.Matches(manifest))
                {
                    issues.Add("DDFP303: Identità o hash lock divergente: " + id);
                    return;
                }
                if (!manifest.SupportsSdkContract(sdkContract))
                {
                    issues.Add("DDFP304: Contratto SDK incompatibile: " + id);
                    return;
                }
                if (DdfExtensionPluginSignatureVerifier.Verify(manifest, artifact.Signature,
                        trust) != DdfExtensionPluginSignatureStatus.Verified ||
                    !string.Equals(Convert.ToHexString(SHA256.HashData(artifact.BytesForVerification)),
                        manifest.AssemblySha256, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("DDFP305: Firma o assembly non verificato: " + id);
                    return;
                }
                states[id] = 1;
                foreach (string dependency in manifest.Dependencies.OrderBy(item => item,
                    StringComparer.Ordinal)) Visit(dependency);
                states[id] = 2;
                ordered.Add(id);
            }

            Visit(rootId);
            if (issues.Count == 0 && ordered.Count != lockFile.Entries.Count)
                issues.Add("DDFP307: Il lock contiene plug-in non raggiungibili dalla radice.");
            return new DdfExtensionPluginDependencyResolution(
                issues.Count == 0 ? ordered : Array.Empty<string>(), issues);
        }

        private static DdfExtensionPluginDependencyResolution Fail(string issue) =>
            new DdfExtensionPluginDependencyResolution(Array.Empty<string>(),
                new[] { issue });
    }
}
