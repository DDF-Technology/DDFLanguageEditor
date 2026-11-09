using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Crosses the installation-consent boundary. It does not start a worker.
    /// </summary>
    public static class DdfProductUpdateInstallAuthorization
    {
        public static bool TryAuthorize(string planPath, string journalPath,
            string archivePath, DdfReleaseTrustStore trustStore,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, DdfProductUpdateInstallationBinding binding,
            string currentExecutablePath, int currentProcessId,
            DateTimeOffset currentProcessStartedUtc, bool explicitConsent)
        {
            if (!explicitConsent) return false;
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            DdfProductUpdateInstallationJournal journal = store.Read();
            if (journal.State != DdfProductUpdateInstallationState.Staged ||
                journal.PlanSha256 !=
                    DdfProductUpdateInstallationJson.ComputePlanSha256(plan))
                throw new InvalidOperationException(
                    "Piano e journal non sono pronti all'autorizzazione.");
            DdfProductUpdateInstallationBinding currentBinding =
                DdfProductUpdateInstallationBinding.Resolve(currentExecutablePath);
            if (plan.ParentProcessId != currentProcessId ||
                plan.ParentProcessStartedUtc != currentProcessStartedUtc ||
                plan.InstallationId != binding.InstallationId ||
                plan.InstallationId != currentBinding.InstallationId ||
                !string.Equals(plan.TargetRoot, binding.TargetRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(plan.TargetRoot, currentBinding.TargetRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(plan.StagingRoot, binding.StagingRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(plan.BackupRoot, binding.BackupRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                plan.ApplicationExecutable != binding.ApplicationExecutable)
                throw new InvalidOperationException(
                    "La sessione o la destinazione non corrisponde al piano.");
            if (request == null ||
                request.ComponentKind != DdfProductUpdateComponentKind.Standalone ||
                plan.ComponentId != request.ComponentId ||
                plan.CurrentVersion != request.CurrentVersion ||
                plan.RuntimeIdentifier != runtimeIdentifier ||
                plan.Architecture != architecture)
                throw new InvalidDataException(
                    "Il piano non corrisponde al prodotto corrente.");

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
            if (!verification.Success || !update.IsActionable ||
                verification.Manifest.Signature?.KeyId != plan.SigningKeyId ||
                update.AvailableVersion != plan.TargetVersion ||
                !string.Equals(plan.ManifestSha256, Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(
                        package.SignedManifestJson))).ToLowerInvariant(),
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Il manifesto firmato è divergente dal piano.");
            DdfReleaseManifestAsset[] assets = verification.Manifest.Assets
                .Where(item => item.ComponentKind == request.ComponentKind &&
                    item.ComponentId == request.ComponentId &&
                    item.RuntimeIdentifier == runtimeIdentifier &&
                    item.Architecture == architecture &&
                    Uri.Equals(item.Uri, update.VerifiedReleaseUri)).ToArray();
            if (assets.Length != 1 ||
                !Uri.Equals(assets[0].Uri, plan.AssetUri) ||
                assets[0].Size != plan.AssetSize ||
                assets[0].Sha256 != plan.AssetSha256 ||
                package.Inventory.Count != plan.Inventory.Count ||
                package.Inventory.Where((file, index) =>
                    file.RelativePath != plan.Inventory[index].RelativePath ||
                    file.Size != plan.Inventory[index].Size ||
                    file.Sha256 != plan.Inventory[index].Sha256).Any())
                throw new InvalidDataException(
                    "L'archivio non corrisponde al piano sigillato.");
            DdfProductUpdateInstallationVerifier.VerifyStaging(plan);
            store.Transition(journal.Revision,
                DdfProductUpdateInstallationState.InstallAuthorized,
                DateTimeOffset.UtcNow);
            return true;
        }
    }
}
