using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionPluginPublisherKey
    {
        public DdfExtensionPluginPublisherKey(string publisher, string keyId,
            byte[] subjectPublicKeyInfo, bool revoked = false)
        {
            if (string.IsNullOrWhiteSpace(publisher) || string.IsNullOrWhiteSpace(keyId))
                throw new ArgumentException("Editore e key ID sono obbligatori.");
            if (subjectPublicKeyInfo == null || subjectPublicKeyInfo.Length == 0 ||
                subjectPublicKeyInfo.Length > 1024)
                throw new ArgumentException("Chiave pubblica SPKI non valida.", nameof(subjectPublicKeyInfo));
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int read);
            if (read != subjectPublicKeyInfo.Length || verifier.KeySize != 256 ||
                verifier.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
                throw new ArgumentException("Chiave ECDSA P-256 richiesta.", nameof(subjectPublicKeyInfo));
            Publisher = publisher;
            KeyId = keyId;
            this.subjectPublicKeyInfo = (byte[])subjectPublicKeyInfo.Clone();
            Revoked = revoked;
        }

        public string Publisher { get; }
        public string KeyId { get; }
        private readonly byte[] subjectPublicKeyInfo;
        public byte[] SubjectPublicKeyInfo => (byte[])subjectPublicKeyInfo.Clone();
        public bool Revoked { get; }
    }

    public sealed class DdfExtensionPluginPublisherTrust
    {
        private readonly IReadOnlyList<DdfExtensionPluginPublisherKey> keys;

        public DdfExtensionPluginPublisherTrust(IEnumerable<DdfExtensionPluginPublisherKey> keys)
        {
            var copy = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();
            if (copy.Any(key => key == null) || copy.Length > 256 ||
                copy.Select(key => key.Publisher + "\0" + key.KeyId)
                    .Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Chiavi attendibili duplicate o non valide.", nameof(keys));
            this.keys = new ReadOnlyCollection<DdfExtensionPluginPublisherKey>(copy);
        }

        public DdfExtensionPluginPublisherKey Find(string publisher, string keyId) =>
            keys.FirstOrDefault(key => string.Equals(key.Publisher, publisher,
                StringComparison.Ordinal) && string.Equals(key.KeyId, keyId,
                StringComparison.Ordinal));
    }

    public sealed class DdfExtensionPluginSignature
    {
        public const string AlgorithmValue = "ECDSA-P256-SHA256";
        public DdfExtensionPluginSignature(string keyId, string value)
        {
            if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 128 ||
                keyId.Any(character => !(char.IsAsciiLetterOrDigit(character) ||
                    character == '-' || character == '_' || character == '.')))
                throw new ArgumentException("Key ID non valido.", nameof(keyId));
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
                throw new ArgumentException("Firma non valida.", nameof(value));
            KeyId = keyId;
            Value = value;
        }

        public string Algorithm => AlgorithmValue;
        public string KeyId { get; }
        public string Value { get; }
    }

    public static class DdfExtensionPluginSignatureCodec
    {
        public static string Serialize(DdfExtensionPluginSignature signature)
        {
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            return JsonSerializer.Serialize(new { algorithm = signature.Algorithm,
                keyId = signature.KeyId, value = signature.Value });
        }

        public static DdfExtensionPluginSignature Parse(string json)
        {
            if (json == null || json.Length > 2048)
                throw new ArgumentException("Firma assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 2, CommentHandling = JsonCommentHandling.Disallow
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("La firma deve essere un oggetto.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if ((property.Name != "algorithm" && property.Name != "keyId" &&
                    property.Name != "value") || !seen.Add(property.Name))
                    throw new FormatException("Campo firma sconosciuto o duplicato.");
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new FormatException("Campo firma non testuale.");
            }
            if (seen.Count != 3 || root.GetProperty("algorithm").GetString() !=
                DdfExtensionPluginSignature.AlgorithmValue)
                throw new FormatException("Algoritmo firma non supportato.");
            return new DdfExtensionPluginSignature(root.GetProperty("keyId").GetString(),
                root.GetProperty("value").GetString());
        }
    }

    public enum DdfExtensionPluginSignatureStatus
    {
        Missing,
        UntrustedPublisherOrKey,
        RevokedKey,
        InvalidSignature,
        Verified
    }

    public static class DdfExtensionPluginSignatureVerifier
    {
        private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
            "DDF-EXTENSION-PLUGIN-MANIFEST/1.0\n");

        public static byte[] CanonicalPayload(DdfExtensionPluginManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            using var stream = new MemoryStream();
            stream.Write(Domain);
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                SkipValidation = false
            }))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", manifest.SchemaVersion);
                writer.WriteString("id", manifest.Id);
                writer.WriteString("publisher", manifest.Publisher);
                writer.WriteString("version", manifest.Version);
                writer.WriteString("minimumSdkContract", manifest.MinimumSdkContract);
                writer.WriteString("maximumSdkContract", manifest.MaximumSdkContract);
                writer.WriteString("assemblyFile", manifest.AssemblyFile);
                writer.WriteString("assemblySha256", manifest.AssemblySha256);
                writer.WriteStartArray("dependencies");
                foreach (string item in manifest.Dependencies.OrderBy(item => item,
                    StringComparer.Ordinal)) writer.WriteStringValue(item);
                writer.WriteEndArray();
                writer.WriteStartArray("capabilities");
                foreach (string item in manifest.Capabilities.OrderBy(item => item,
                    StringComparer.Ordinal)) writer.WriteStringValue(item);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }

        public static DdfExtensionPluginSignature Sign(DdfExtensionPluginManifest manifest,
            string keyId, ECDsa privateKey)
        {
            if (privateKey == null || privateKey.KeySize != 256 ||
                privateKey.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
                throw new ArgumentException("Chiave privata ECDSA P-256 richiesta.", nameof(privateKey));
            byte[] signed = privateKey.SignData(CanonicalPayload(manifest),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            return new DdfExtensionPluginSignature(keyId, Convert.ToBase64String(signed));
        }

        public static DdfExtensionPluginSignatureStatus Verify(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (trust == null) throw new ArgumentNullException(nameof(trust));
            if (signature == null) return DdfExtensionPluginSignatureStatus.Missing;
            DdfExtensionPluginPublisherKey key = trust.Find(manifest.Publisher,
                signature.KeyId);
            if (key == null) return DdfExtensionPluginSignatureStatus.UntrustedPublisherOrKey;
            if (key.Revoked) return DdfExtensionPluginSignatureStatus.RevokedKey;
            try
            {
                byte[] bytes = Convert.FromBase64String(signature.Value);
                using ECDsa verifier = ECDsa.Create();
                verifier.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo,
                    out int read);
                return read == key.SubjectPublicKeyInfo.Length &&
                    verifier.VerifyData(CanonicalPayload(manifest), bytes,
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence)
                    ? DdfExtensionPluginSignatureStatus.Verified
                    : DdfExtensionPluginSignatureStatus.InvalidSignature;
            }
            catch (Exception exception) when (exception is FormatException ||
                exception is CryptographicException || exception is ArgumentException)
            {
                return DdfExtensionPluginSignatureStatus.InvalidSignature;
            }
        }
    }
}
