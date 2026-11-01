using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionCallSignatureAttestationStatus
    {
        PointCatalogNotVerified,
        Missing,
        UntrustedPublisherOrKey,
        RevokedKey,
        InvalidSignature,
        Verified
    }

    // Signed metadata only; no plug-in assembly, delegate, editor or VM is loaded.
    public static class DdfExtensionCallSignatureAttestation
    {
        private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
            "DDF-EXTENSION-CALL-SIGNATURES/1.0\n");

        public static byte[] CanonicalPayload(DdfExtensionCallSignatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            byte[] pointDigest = SHA256.HashData(
                DdfExtensionPointAttestation.CanonicalPayload(catalog.Points));
            byte[] signatureBytes = Encoding.UTF8.GetBytes(catalog.ToJson());
            using var stream = new MemoryStream(Domain.Length + pointDigest.Length +
                signatureBytes.Length);
            stream.Write(Domain);
            stream.Write(pointDigest);
            stream.Write(signatureBytes);
            return stream.ToArray();
        }

        public static DdfExtensionPluginSignature Sign(
            DdfExtensionCallSignatureCatalog catalog, string keyId, ECDsa privateKey)
        {
            if (privateKey == null || privateKey.KeySize != 256 ||
                privateKey.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
                throw new ArgumentException("Chiave privata ECDSA P-256 richiesta.", nameof(privateKey));
            byte[] signed = privateKey.SignData(CanonicalPayload(catalog),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            return new DdfExtensionPluginSignature(keyId, Convert.ToBase64String(signed));
        }

        public static DdfExtensionCallSignatureAttestationStatus Verify(
            DdfExtensionCallSignatureCatalog catalog,
            DdfExtensionPluginSignature signaturesSignature,
            DdfExtensionPluginSignature pointsSignature,
            DdfExtensionPluginSignature manifestSignature,
            DdfExtensionPluginPublisherTrust trust)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (trust == null) throw new ArgumentNullException(nameof(trust));
            if (DdfExtensionPointAttestation.Verify(catalog.Points, pointsSignature,
                    manifestSignature, trust) != DdfExtensionPointAttestationStatus.Verified)
                return DdfExtensionCallSignatureAttestationStatus.PointCatalogNotVerified;
            if (signaturesSignature == null)
                return DdfExtensionCallSignatureAttestationStatus.Missing;
            DdfExtensionPluginPublisherKey key = trust.Find(
                catalog.Points.Manifest.Publisher, signaturesSignature.KeyId);
            if (key == null)
                return DdfExtensionCallSignatureAttestationStatus.UntrustedPublisherOrKey;
            if (key.Revoked)
                return DdfExtensionCallSignatureAttestationStatus.RevokedKey;
            try
            {
                byte[] signed = Convert.FromBase64String(signaturesSignature.Value);
                byte[] publicKey = key.SubjectPublicKeyInfo;
                using ECDsa verifier = ECDsa.Create();
                verifier.ImportSubjectPublicKeyInfo(publicKey, out int read);
                return read == publicKey.Length && verifier.VerifyData(
                    CanonicalPayload(catalog), signed, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence)
                    ? DdfExtensionCallSignatureAttestationStatus.Verified
                    : DdfExtensionCallSignatureAttestationStatus.InvalidSignature;
            }
            catch (Exception exception) when (exception is FormatException ||
                exception is CryptographicException || exception is ArgumentException)
            {
                return DdfExtensionCallSignatureAttestationStatus.InvalidSignature;
            }
        }
    }
}
