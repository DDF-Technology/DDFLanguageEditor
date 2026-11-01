using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionPointAttestationStatus
    {
        ManifestNotVerified,
        Missing,
        UntrustedPublisherOrKey,
        RevokedKey,
        InvalidSignature,
        Verified
    }

    // A detached signature over the canonical catalog and the signed manifest identity.
    // No assembly is loaded, and a verified catalog is not an execution grant.
    public static class DdfExtensionPointAttestation
    {
        private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
            "DDF-EXTENSION-POINT-CATALOG/1.0\n");

        public static byte[] CanonicalPayload(DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            byte[] manifestHash = SHA256.HashData(
                DdfExtensionPluginSignatureVerifier.CanonicalPayload(catalog.Manifest));
            byte[] catalogBytes = Encoding.UTF8.GetBytes(catalog.ToJson());
            using var stream = new MemoryStream(Domain.Length + manifestHash.Length +
                catalogBytes.Length);
            stream.Write(Domain);
            stream.Write(manifestHash);
            stream.Write(catalogBytes);
            return stream.ToArray();
        }

        public static DdfExtensionPluginSignature Sign(DdfExtensionPointCatalog catalog,
            string keyId, ECDsa privateKey)
        {
            if (privateKey == null || privateKey.KeySize != 256 ||
                privateKey.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
                throw new ArgumentException("Chiave privata ECDSA P-256 richiesta.", nameof(privateKey));
            byte[] signed = privateKey.SignData(CanonicalPayload(catalog),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            return new DdfExtensionPluginSignature(keyId, Convert.ToBase64String(signed));
        }

        public static DdfExtensionPointAttestationStatus Verify(
            DdfExtensionPointCatalog catalog, DdfExtensionPluginSignature catalogSignature,
            DdfExtensionPluginSignature manifestSignature,
            DdfExtensionPluginPublisherTrust trust)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (trust == null) throw new ArgumentNullException(nameof(trust));
            if (DdfExtensionPluginSignatureVerifier.Verify(catalog.Manifest,
                    manifestSignature, trust) != DdfExtensionPluginSignatureStatus.Verified)
                return DdfExtensionPointAttestationStatus.ManifestNotVerified;
            if (catalogSignature == null)
                return DdfExtensionPointAttestationStatus.Missing;
            DdfExtensionPluginPublisherKey key = trust.Find(catalog.Manifest.Publisher,
                catalogSignature.KeyId);
            if (key == null)
                return DdfExtensionPointAttestationStatus.UntrustedPublisherOrKey;
            if (key.Revoked)
                return DdfExtensionPointAttestationStatus.RevokedKey;
            try
            {
                byte[] signatureBytes = Convert.FromBase64String(catalogSignature.Value);
                byte[] publicKey = key.SubjectPublicKeyInfo;
                using ECDsa verifier = ECDsa.Create();
                verifier.ImportSubjectPublicKeyInfo(publicKey, out int read);
                return read == publicKey.Length && verifier.VerifyData(
                    CanonicalPayload(catalog), signatureBytes,
                    HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
                    ? DdfExtensionPointAttestationStatus.Verified
                    : DdfExtensionPointAttestationStatus.InvalidSignature;
            }
            catch (Exception exception) when (exception is FormatException ||
                exception is CryptographicException || exception is ArgumentException)
            {
                return DdfExtensionPointAttestationStatus.InvalidSignature;
            }
        }
    }
}
