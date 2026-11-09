using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Builds an immutable installation plan from a locally reverified release.
    /// This does not persist a journal, authorize installation, or start a worker.
    /// </summary>
    public static class DdfProductUpdateLocalPlanFactory
    {
        public static DdfProductUpdateInstallationPlan Create(
            string archivePath, DdfReleaseTrustStore trustStore,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, string installationId, string targetRoot,
            string stagingRoot, string backupRoot,
            string applicationExecutable, int parentProcessId,
            DateTimeOffset parentProcessStartedUtc, DateTimeOffset createdUtc,
            bool restartRequested = false)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ComponentKind != DdfProductUpdateComponentKind.Standalone)
                throw new InvalidDataException(
                    "Un piano d'installazione richiede il componente standalone.");
            DdfProductUpdatePreparedPackage package =
                DdfProductUpdatePackageDownloader.ReopenVerifiedLocal(
                    archivePath, trustStore, request, runtimeIdentifier,
                    architecture);
            DdfReleaseManifestVerification verification =
                DdfReleaseManifestVerifier.Verify(package.SignedManifestJson,
                    trustStore);
            DdfProductUpdateResult update = DdfReleaseUpdatePolicy.Evaluate(
                verification, request, runtimeIdentifier, architecture,
                DateTimeOffset.UtcNow);
            if (!update.IsActionable || !verification.Success ||
                verification.Manifest.Signature == null)
                throw new InvalidDataException("Release locale non azionabile.");
            DdfReleaseManifestAsset[] assets = verification.Manifest.Assets
                .Where(item => item.ComponentKind == request.ComponentKind &&
                    item.ComponentId == request.ComponentId &&
                    item.RuntimeIdentifier == runtimeIdentifier &&
                    item.Architecture == architecture &&
                    Uri.Equals(item.Uri, update.VerifiedReleaseUri)).ToArray();
            if (assets.Length != 1)
                throw new InvalidDataException("Asset locale ambiguo.");
            DdfReleaseManifestAsset asset = assets[0];
            string manifestHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(package.SignedManifestJson)))
                .ToLowerInvariant();
            string nonce = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            return new DdfProductUpdateInstallationPlan(installationId,
                request.ComponentId, request.CurrentVersion, asset.Version,
                runtimeIdentifier, architecture, manifestHash,
                verification.Manifest.Signature.KeyId, asset.Uri, asset.Size,
                asset.Sha256, targetRoot, stagingRoot, backupRoot,
                applicationExecutable, parentProcessId, parentProcessStartedUtc,
                nonce, restartRequested, package.Inventory, createdUtc);
        }
    }
}
