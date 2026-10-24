using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfReleaseManifestAsset
    {
        public DdfReleaseManifestAsset(DdfProductUpdateComponentKind componentKind,
            string componentId, string version, string runtimeIdentifier,
            string architecture, Uri uri, long size, string sha256)
        {
            ComponentKind = componentKind;
            ComponentId = DdfProductUpdateResult.RequireComponentId(componentId);
            Version = DdfProductUpdateResult.RequireVersion(version, nameof(version));
            RuntimeIdentifier = RequireToken(runtimeIdentifier, nameof(runtimeIdentifier));
            Architecture = RequireToken(architecture, nameof(architecture));
            Uri = ValidateUri(uri);
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            Sha256 = NormalizeSha256(sha256);
        }

        public DdfProductUpdateComponentKind ComponentKind { get; }
        public string ComponentId { get; }
        public string Version { get; }
        public string RuntimeIdentifier { get; }
        public string Architecture { get; }
        public Uri Uri { get; }
        public long Size { get; }
        public string Sha256 { get; }

        internal string Identity => ComponentKind + "|" + ComponentId + "|" +
            RuntimeIdentifier + "|" + Architecture;

        private static string RequireToken(string value, string parameterName)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0 || normalized.Any(character =>
                !(char.IsLetterOrDigit(character) || character == '-' ||
                  character == '_' || character == '.')))
                throw new ArgumentException("Token asset non valido.", parameterName);
            return normalized;
        }

        private static Uri ValidateUri(Uri value)
        {
            if (value == null || !value.IsAbsoluteUri ||
                value.Scheme != Uri.UriSchemeHttps || value.UserInfo.Length != 0)
                throw new ArgumentException("URI asset HTTPS richiesta.", nameof(value));
            return value;
        }

        private static string NormalizeSha256(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length != 64 || normalized.Any(character =>
                !Uri.IsHexDigit(character)))
                throw new ArgumentException("SHA-256 asset non valido.", nameof(value));
            return normalized;
        }
    }

    public sealed class DdfReleaseCompatibility
    {
        public DdfReleaseCompatibility(string minimumSdkContractVersion,
            string maximumSdkContractVersion,
            IReadOnlyDictionary<string, string> persistenceVersions)
        {
            MinimumSdkContractVersion = RequireMajorMinor(
                minimumSdkContractVersion, nameof(minimumSdkContractVersion));
            MaximumSdkContractVersion = RequireMajorMinor(
                maximumSdkContractVersion, nameof(maximumSdkContractVersion));
            if (CompareMajorMinor(MinimumSdkContractVersion,
                    MaximumSdkContractVersion) > 0)
                throw new ArgumentException("Intervallo SDK invertito.");
            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in
                persistenceVersions ?? throw new ArgumentNullException(
                    nameof(persistenceVersions)))
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    throw new ArgumentException("Schema persistenza privo di nome.");
                copy.Add(entry.Key, RequireMajorMinor(entry.Value, entry.Key));
            }
            if (copy.Count == 0)
                throw new ArgumentException("Compatibilità persistenza richiesta.");
            PersistenceVersions = new ReadOnlyDictionary<string, string>(copy);
        }

        public string MinimumSdkContractVersion { get; }
        public string MaximumSdkContractVersion { get; }
        public IReadOnlyDictionary<string, string> PersistenceVersions { get; }

        internal static int CompareMajorMinor(string left, string right)
        {
            Version leftVersion = Version.Parse(left);
            Version rightVersion = Version.Parse(right);
            return leftVersion.CompareTo(rightVersion);
        }

        private static string RequireMajorMinor(string value, string parameterName)
        {
            string normalized = (value ?? string.Empty).Trim();
            string[] parts = normalized.Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor) || major < 0 || minor < 0 ||
                parts[0] != major.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                parts[1] != minor.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ArgumentException("Versione major.minor non valida.",
                    parameterName);
            return normalized;
        }
    }

    public sealed class DdfReleaseManifestSignature
    {
        public const string AlgorithmValue = "ECDSA-P256-SHA256";

        internal DdfReleaseManifestSignature(string keyId, string value)
        {
            KeyId = string.IsNullOrWhiteSpace(keyId)
                ? throw new ArgumentException("Key ID richiesto.", nameof(keyId))
                : keyId.Trim();
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Firma richiesta.", nameof(value))
                : value.Trim();
        }

        public string Algorithm => AlgorithmValue;
        public string KeyId { get; }
        public string Value { get; }
    }

    public sealed class DdfReleaseManifest
    {
        public const string SchemaVersionValue = "1.0";

        internal DdfReleaseManifest(string productId, string repository,
            string version, DdfProductUpdateChannel channel,
            DateTimeOffset publishedAtUtc, string releaseNotes,
            DdfReleaseCompatibility compatibility,
            IEnumerable<DdfReleaseManifestAsset> assets,
            DdfReleaseManifestSignature signature)
        {
            ProductId = DdfProductUpdateResult.RequireComponentId(productId);
            Repository = string.Equals(repository,
                    DdfGitHubReleaseProvider.Repository, StringComparison.Ordinal)
                ? repository
                : throw new ArgumentException("Repository release non autorizzato.",
                    nameof(repository));
            Version = DdfProductUpdateResult.RequireVersion(version,
                nameof(version));
            Channel = channel;
            PublishedAtUtc = publishedAtUtc.ToUniversalTime();
            ReleaseNotes = releaseNotes ?? string.Empty;
            if (ReleaseNotes.Length > 64 * 1024)
                throw new ArgumentException("Note release oltre limite.",
                    nameof(releaseNotes));
            Compatibility = compatibility ?? throw new ArgumentNullException(
                nameof(compatibility));
            List<DdfReleaseManifestAsset> ordered = (assets ??
                throw new ArgumentNullException(nameof(assets)))
                .OrderBy(asset => asset.Identity, StringComparer.Ordinal).ToList();
            if (ordered.Count == 0)
                throw new ArgumentException("Almeno un asset è richiesto.");
            if (ordered.Select(asset => asset.Identity).Distinct(
                    StringComparer.Ordinal).Count() != ordered.Count)
                throw new ArgumentException("Asset release ambiguo o duplicato.");
            if (ordered.Any(asset => !string.Equals(asset.Version, Version,
                    StringComparison.Ordinal)))
                throw new ArgumentException("Versione asset divergente.");
            Assets = new ReadOnlyCollection<DdfReleaseManifestAsset>(ordered);
            Signature = signature;
        }

        public string SchemaVersion => SchemaVersionValue;
        public string ProductId { get; }
        public string Repository { get; }
        public string Version { get; }
        public DdfProductUpdateChannel Channel { get; }
        public DateTimeOffset PublishedAtUtc { get; }
        public string ReleaseNotes { get; }
        public DdfReleaseCompatibility Compatibility { get; }
        public IReadOnlyList<DdfReleaseManifestAsset> Assets { get; }
        public DdfReleaseManifestSignature Signature { get; }
        public bool IsSigned => Signature != null;

        public static DdfReleaseManifest CreateUnsigned(string productId,
            string version, DdfProductUpdateChannel channel,
            DateTimeOffset publishedAtUtc, string releaseNotes,
            DdfReleaseCompatibility compatibility,
            IEnumerable<DdfReleaseManifestAsset> assets) =>
            new DdfReleaseManifest(productId,
                DdfGitHubReleaseProvider.Repository, version, channel,
                publishedAtUtc, releaseNotes, compatibility, assets, null);
    }

    public static class DdfReleaseManifestCodec
    {
        private static readonly JsonSerializerOptions ReadOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };

        public static byte[] CanonicalPayload(DdfReleaseManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
                WriteManifest(writer, manifest, includeSignature: false);
            return buffer.WrittenSpan.ToArray();
        }

        public static string Serialize(DdfReleaseManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer,
                new JsonWriterOptions { Indented = true }))
                WriteManifest(writer, manifest, includeSignature: true);
            return Encoding.UTF8.GetString(buffer.WrittenSpan) + Environment.NewLine;
        }

        public static DdfReleaseManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Manifesto release vuoto.");
            try
            {
                ManifestDocument document = JsonSerializer.Deserialize<ManifestDocument>(
                    json, ReadOptions) ?? throw new InvalidDataException(
                        "Manifesto release vuoto.");
                if (document.SchemaVersion != DdfReleaseManifest.SchemaVersionValue)
                    throw new InvalidDataException(
                        "Versione manifesto release non supportata.");
                var compatibility = new DdfReleaseCompatibility(
                    document.Compatibility?.MinimumSdkContractVersion,
                    document.Compatibility?.MaximumSdkContractVersion,
                    document.Compatibility?.PersistenceVersions);
                var assets = (document.Assets ?? Array.Empty<AssetDocument>())
                    .Select(asset => new DdfReleaseManifestAsset(
                        asset.ComponentKind, asset.ComponentId, asset.Version,
                        asset.RuntimeIdentifier, asset.Architecture,
                        new Uri(asset.Uri, UriKind.Absolute), asset.Size,
                        asset.Sha256));
                DdfReleaseManifestSignature signature = document.Signature == null
                    ? null
                    : document.Signature.Algorithm !=
                        DdfReleaseManifestSignature.AlgorithmValue
                        ? throw new InvalidDataException(
                            "Algoritmo firma release non supportato.")
                        : new DdfReleaseManifestSignature(
                            document.Signature.KeyId, document.Signature.Value);
                return new DdfReleaseManifest(document.ProductId,
                    document.Repository, document.Version, document.Channel,
                    document.PublishedAtUtc, document.ReleaseNotes,
                    compatibility, assets, signature);
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is JsonException ||
                exception is ArgumentException || exception is UriFormatException ||
                exception is InvalidOperationException)
            {
                throw new InvalidDataException("Manifesto release non valido.",
                    exception);
            }
        }

        internal static DdfReleaseManifest WithSignature(
            DdfReleaseManifest manifest, DdfReleaseManifestSignature signature) =>
            new DdfReleaseManifest(manifest.ProductId, manifest.Repository,
                manifest.Version, manifest.Channel, manifest.PublishedAtUtc,
                manifest.ReleaseNotes, manifest.Compatibility, manifest.Assets,
                signature);

        private static void WriteManifest(Utf8JsonWriter writer,
            DdfReleaseManifest manifest, bool includeSignature)
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("productId", manifest.ProductId);
            writer.WriteString("repository", manifest.Repository);
            writer.WriteString("version", manifest.Version);
            writer.WriteString("channel", manifest.Channel ==
                DdfProductUpdateChannel.Stable ? "stable" : "beta");
            writer.WriteString("publishedAtUtc", manifest.PublishedAtUtc);
            writer.WriteString("releaseNotes", manifest.ReleaseNotes);
            writer.WritePropertyName("compatibility");
            writer.WriteStartObject();
            writer.WriteString("minimumSdkContractVersion",
                manifest.Compatibility.MinimumSdkContractVersion);
            writer.WriteString("maximumSdkContractVersion",
                manifest.Compatibility.MaximumSdkContractVersion);
            writer.WritePropertyName("persistenceVersions");
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> entry in
                manifest.Compatibility.PersistenceVersions)
                writer.WriteString(entry.Key, entry.Value);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("assets");
            writer.WriteStartArray();
            foreach (DdfReleaseManifestAsset asset in manifest.Assets)
            {
                writer.WriteStartObject();
                writer.WriteString("componentKind", JsonNamingPolicy.CamelCase
                    .ConvertName(asset.ComponentKind.ToString()));
                writer.WriteString("componentId", asset.ComponentId);
                writer.WriteString("version", asset.Version);
                writer.WriteString("runtimeIdentifier", asset.RuntimeIdentifier);
                writer.WriteString("architecture", asset.Architecture);
                writer.WriteString("uri", asset.Uri.AbsoluteUri);
                writer.WriteNumber("size", asset.Size);
                writer.WriteString("sha256", asset.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (includeSignature && manifest.Signature != null)
            {
                writer.WritePropertyName("signature");
                writer.WriteStartObject();
                writer.WriteString("algorithm", manifest.Signature.Algorithm);
                writer.WriteString("keyId", manifest.Signature.KeyId);
                writer.WriteString("value", manifest.Signature.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.Flush();
        }

        private sealed class ManifestDocument
        {
            public string SchemaVersion { get; set; }
            public string ProductId { get; set; }
            public string Repository { get; set; }
            public string Version { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter<DdfProductUpdateChannel>))]
            public DdfProductUpdateChannel Channel { get; set; }
            public DateTimeOffset PublishedAtUtc { get; set; }
            public string ReleaseNotes { get; set; }
            public CompatibilityDocument Compatibility { get; set; }
            public AssetDocument[] Assets { get; set; }
            public SignatureDocument Signature { get; set; }
        }

        private sealed class CompatibilityDocument
        {
            public string MinimumSdkContractVersion { get; set; }
            public string MaximumSdkContractVersion { get; set; }
            public Dictionary<string, string> PersistenceVersions { get; set; }
        }

        private sealed class AssetDocument
        {
            [JsonConverter(typeof(JsonStringEnumConverter<DdfProductUpdateComponentKind>))]
            public DdfProductUpdateComponentKind ComponentKind { get; set; }
            public string ComponentId { get; set; }
            public string Version { get; set; }
            public string RuntimeIdentifier { get; set; }
            public string Architecture { get; set; }
            public string Uri { get; set; }
            public long Size { get; set; }
            public string Sha256 { get; set; }
        }

        private sealed class SignatureDocument
        {
            public string Algorithm { get; set; }
            public string KeyId { get; set; }
            public string Value { get; set; }
        }
    }

    public sealed class DdfReleaseTrustStore
    {
        private readonly IReadOnlyDictionary<string, byte[]> keys;

        public DdfReleaseTrustStore(IReadOnlyDictionary<string, byte[]> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            var copy = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, byte[]> entry in keys)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                    entry.Value.Length == 0)
                    throw new ArgumentException("Chiave pubblica release non valida.");
                copy.Add(entry.Key.Trim(), entry.Value.ToArray());
            }
            this.keys = new ReadOnlyDictionary<string, byte[]>(copy);
        }

        internal bool TryGetKey(string keyId, out byte[] key)
        {
            if (keys.TryGetValue(keyId ?? string.Empty, out byte[] stored))
            {
                key = stored.ToArray();
                return true;
            }
            key = null;
            return false;
        }
    }

    public sealed class DdfReleaseManifestVerification
    {
        internal DdfReleaseManifestVerification(DdfReleaseManifest manifest,
            IEnumerable<string> issues)
        {
            Manifest = manifest;
            Issues = new ReadOnlyCollection<string>((issues ??
                Array.Empty<string>()).ToList());
        }

        public DdfReleaseManifest Manifest { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool Success => Manifest != null && Issues.Count == 0;
    }

    public static class DdfReleaseManifestSigner
    {
        public static DdfReleaseManifest Sign(DdfReleaseManifest manifest,
            string keyId, ECDsa privateKey)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (manifest.IsSigned)
                throw new ArgumentException("Il manifesto è già firmato.",
                    nameof(manifest));
            if (privateKey == null || privateKey.KeySize != 256)
                throw new ArgumentException("Chiave privata ECDSA P-256 richiesta.",
                    nameof(privateKey));
            byte[] signature = privateKey.SignData(
                DdfReleaseManifestCodec.CanonicalPayload(manifest),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            return DdfReleaseManifestCodec.WithSignature(manifest,
                new DdfReleaseManifestSignature(keyId,
                    Convert.ToBase64String(signature)));
        }
    }

    public static class DdfReleaseManifestVerifier
    {
        public static DdfReleaseManifestVerification Verify(string json,
            DdfReleaseTrustStore trustStore)
        {
            if (trustStore == null) throw new ArgumentNullException(nameof(trustStore));
            DdfReleaseManifest manifest;
            try { manifest = DdfReleaseManifestCodec.Parse(json); }
            catch (InvalidDataException exception)
            {
                return new DdfReleaseManifestVerification(null,
                    new[] { exception.Message });
            }
            var issues = new List<string>();
            if (manifest.Signature == null)
                issues.Add("Manifesto release privo di firma.");
            else if (!trustStore.TryGetKey(manifest.Signature.KeyId,
                    out byte[] publicKey))
                issues.Add("Chiave firma release non attendibile.");
            else
            {
                try
                {
                    byte[] signature = Convert.FromBase64String(
                        manifest.Signature.Value);
                    using ECDsa verifier = ECDsa.Create();
                    verifier.ImportSubjectPublicKeyInfo(publicKey, out int read);
                    if (read != publicKey.Length || verifier.KeySize != 256 ||
                        !verifier.VerifyData(
                            DdfReleaseManifestCodec.CanonicalPayload(manifest),
                            signature, HashAlgorithmName.SHA256,
                            DSASignatureFormat.Rfc3279DerSequence))
                        issues.Add("Firma manifesto release non valida.");
                }
                catch (Exception exception) when (exception is CryptographicException ||
                    exception is FormatException || exception is ArgumentException)
                {
                    issues.Add("Firma manifesto release non valida: " +
                        exception.Message);
                }
            }
            return new DdfReleaseManifestVerification(
                issues.Count == 0 ? manifest : null, issues);
        }
    }
}
