using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    // An authenticated metadata snapshot. It is NOT admission, consent or a loader.
    public sealed class DdfVerifiedExtensionCatalog
    {
        private DdfVerifiedExtensionCatalog(DdfExtensionPluginManifest manifest,
            DdfExtensionPointCatalog points,
            DdfExtensionCallSignatureCatalog signatures,
            string sdkContract, string publisherKeyId)
        {
            Manifest = manifest;
            Points = points;
            CallSignatures = signatures;
            SdkContract = sdkContract;
            PublisherKeyId = publisherKeyId;
        }

        public DdfExtensionPluginManifest Manifest { get; }
        public DdfExtensionPointCatalog Points { get; }
        public DdfExtensionCallSignatureCatalog CallSignatures { get; }
        public string SdkContract { get; }
        public string PublisherKeyId { get; }
        public string AssemblySha256 => Manifest.AssemblySha256;

        public bool TryGetPoint(string id, out DdfExtensionPointDescriptor point) =>
            Points.TryGet(id, out point);

        public bool TryGetCallSignature(string pointId,
            out DdfExtensionCallSignature signature) =>
            CallSignatures.TryGet(pointId, out signature);

        public static DdfExtensionPluginCheck<DdfVerifiedExtensionCatalog> Verify(
            string manifestJson, string manifestSignatureJson,
            string pointsJson, string pointsSignatureJson,
            string callSignaturesJson, string callSignaturesSignatureJson,
            byte[] assemblyBytes, DdfExtensionPluginPublisherTrust trust,
            string sdkContract)
        {
            DdfExtensionPluginCheck<DdfExtensionPluginManifest> artifact =
                DdfExtensionPluginPreflight.VerifyArtifact(manifestJson,
                    manifestSignatureJson, assemblyBytes, trust, sdkContract);
            if (!artifact.Success) return Fail(artifact.Diagnostics);

            DdfExtensionPluginCheck<DdfExtensionPluginSignature> manifestSignature =
                DdfExtensionPluginPreflight.ParseSignature(manifestSignatureJson);
            if (!manifestSignature.Success) return Fail(manifestSignature.Diagnostics);

            DdfExtensionPluginCheck<DdfExtensionPointCatalog> points =
                DdfExtensionPluginPreflight.VerifyCatalog(pointsJson,
                    pointsSignatureJson, artifact.Value,
                    manifestSignature.Value, trust);
            if (!points.Success) return Fail(points.Diagnostics);

            DdfExtensionPluginCheck<DdfExtensionPluginSignature> pointsSignature =
                DdfExtensionPluginPreflight.ParseSignature(pointsSignatureJson);
            if (!pointsSignature.Success) return Fail(pointsSignature.Diagnostics);

            DdfExtensionPluginCheck<DdfExtensionCallSignatureCatalog> signatures =
                DdfExtensionPluginPreflight.VerifyCallSignatures(
                    callSignaturesJson, callSignaturesSignatureJson,
                    points.Value, pointsSignature.Value,
                    manifestSignature.Value, trust);
            if (!signatures.Success) return Fail(signatures.Diagnostics);

            return new DdfExtensionPluginCheck<DdfVerifiedExtensionCatalog>(
                new DdfVerifiedExtensionCatalog(artifact.Value, points.Value,
                    signatures.Value, sdkContract,
                    manifestSignature.Value.KeyId),
                Array.Empty<DdfExtensionPluginDiagnostic>());
        }

        private static DdfExtensionPluginCheck<DdfVerifiedExtensionCatalog> Fail(
            IEnumerable<DdfExtensionPluginDiagnostic> diagnostics) =>
            new DdfExtensionPluginCheck<DdfVerifiedExtensionCatalog>(null,
                diagnostics);
    }
}
