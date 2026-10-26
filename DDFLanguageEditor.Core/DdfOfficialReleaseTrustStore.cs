using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDFLanguageEditor.Core
{
    public static class DdfOfficialReleaseTrustStore
    {
        public const string SchemaVersion = "1.0";
        public const string ActiveKeyId = "official-2026";
        public const string ActiveKeySha256 =
            "cef7cb71ca0c95d06a4cf8b56d1996fe4e2dea0fd94ee490fa7184753465e204";
        private const string ResourceName =
            "DDFLanguageEditor.Core.ProductUpdates.ddf-release-trust-store.json";

        public static DdfReleaseTrustStore Create()
        {
            using Stream stream = typeof(DdfOfficialReleaseTrustStore).Assembly
                .GetManifestResourceStream(ResourceName) ?? throw new
                InvalidDataException("Trust store release incorporato assente.");
            TrustStoreDocument document;
            try
            {
                document = JsonSerializer.Deserialize<TrustStoreDocument>(stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = false,
                        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                    });
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "Trust store release incorporato non valido.", exception);
            }
            if (document == null || document.Schema !=
                    "./ddf-release-trust-store.schema.json" ||
                document.SchemaVersion != SchemaVersion || document.Keys == null ||
                document.Keys.Count == 0 || document.Keys.Count > 8)
                throw new InvalidDataException(
                    "Contratto trust store release non valido.");

            var trusted = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrustStoreKeyDocument entry in document.Keys)
            {
                if (entry == null || !identifiers.Add(entry.KeyId ?? string.Empty) ||
                    entry.Algorithm != "ECDSA-P256-SHA256" ||
                    (entry.Status != "active" && entry.Status != "revoked"))
                    throw new InvalidDataException(
                        "Voce trust store release non valida o duplicata.");
                byte[] publicKey;
                try { publicKey = Convert.FromBase64String(
                    entry.PublicKeySpkiBase64 ?? string.Empty); }
                catch (FormatException exception)
                {
                    throw new InvalidDataException(
                        "Chiave pubblica release non codificata correttamente.",
                        exception);
                }
                string fingerprint = Convert.ToHexString(SHA256.HashData(publicKey))
                    .ToLowerInvariant();
                if (!string.Equals(fingerprint, entry.Sha256,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Fingerprint della chiave release non corrispondente.");
                try
                {
                    using ECDsa verifier = ECDsa.Create();
                    verifier.ImportSubjectPublicKeyInfo(publicKey, out int read);
                    if (read != publicKey.Length || verifier.KeySize != 256)
                        throw new InvalidDataException(
                            "La chiave release non è ECDSA P-256 SPKI.");
                }
                catch (CryptographicException exception)
                {
                    throw new InvalidDataException(
                        "Chiave pubblica release non valida.", exception);
                }
                if (entry.Status == "active") trusted.Add(entry.KeyId, publicKey);
            }
            if (trusted.Count == 0 || !trusted.ContainsKey(ActiveKeyId))
                throw new InvalidDataException(
                    "Nessuna chiave release ufficiale attiva.");
            return new DdfReleaseTrustStore(trusted);
        }

        private sealed class TrustStoreDocument
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; }
            [JsonPropertyName("schemaVersion")]
            public string SchemaVersion { get; set; }
            [JsonPropertyName("keys")]
            public List<TrustStoreKeyDocument> Keys { get; set; }
        }

        private sealed class TrustStoreKeyDocument
        {
            [JsonPropertyName("keyId")]
            public string KeyId { get; set; }
            [JsonPropertyName("algorithm")]
            public string Algorithm { get; set; }
            [JsonPropertyName("publicKeySpkiBase64")]
            public string PublicKeySpkiBase64 { get; set; }
            [JsonPropertyName("sha256")]
            public string Sha256 { get; set; }
            [JsonPropertyName("status")]
            public string Status { get; set; }
        }
    }
}
