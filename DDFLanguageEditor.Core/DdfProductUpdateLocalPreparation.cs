using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfProductUpdateLocalPreparationResult
    {
        internal DdfProductUpdateLocalPreparationResult(
            DdfProductUpdateInstallationPlan plan,
            DdfProductUpdateStagingSeal seal, string planPath,
            string journalPath)
        {
            Plan = plan;
            Seal = seal;
            PlanPath = planPath;
            JournalPath = journalPath;
        }

        public DdfProductUpdateInstallationPlan Plan { get; }
        public DdfProductUpdateStagingSeal Seal { get; }
        public string PlanPath { get; }
        public string JournalPath { get; }
    }

    /// <summary>
    /// Persists a verified local plan and stages it without granting installation.
    /// The caller must still obtain a separate install consent before using a worker.
    /// </summary>
    public static class DdfProductUpdateLocalPreparation
    {
        public static async Task<DdfProductUpdateLocalPreparationResult> PrepareAsync(
            string archivePath, DdfReleaseTrustStore trustStore,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, DdfProductUpdateInstallationBinding binding,
            string stateDirectory, int parentProcessId,
            DateTimeOffset parentProcessStartedUtc,
            CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (string.IsNullOrWhiteSpace(stateDirectory) ||
                !Path.IsPathFullyQualified(stateDirectory))
                throw new ArgumentException("Directory di stato assoluta richiesta.",
                    nameof(stateDirectory));
            string state = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(stateDirectory));
            if (string.Equals(state, Path.GetPathRoot(state),
                    StringComparison.OrdinalIgnoreCase) ||
                IsInside(state, binding.TargetRoot) ||
                IsInside(binding.TargetRoot, state) ||
                IsInside(state, binding.StagingRoot) ||
                IsInside(state, binding.BackupRoot))
                throw new InvalidDataException(
                    "Il journal non può risiedere nella directory sostituita.");
            for (string current = state; !string.IsNullOrEmpty(current);
                current = Path.GetDirectoryName(current))
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "La directory di stato attraversa un reparse point.");
            string planPath = Path.Combine(state, "plan.json");
            string journalPath = Path.Combine(state, "journal.json");
            if (File.Exists(planPath) || File.Exists(journalPath))
                throw new InvalidOperationException(
                    "Esiste già uno stato di aggiornamento da esaminare.");
            DdfProductUpdateInstallationBinding currentBinding =
                DdfProductUpdateInstallationBinding.Resolve(Path.Combine(
                    binding.TargetRoot, binding.ApplicationExecutable));
            if (!string.Equals(currentBinding.TargetRoot, binding.TargetRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                currentBinding.InstallationId != binding.InstallationId ||
                Directory.Exists(binding.StagingRoot) ||
                File.Exists(binding.StagingRoot) ||
                Directory.Exists(binding.BackupRoot) ||
                File.Exists(binding.BackupRoot))
                throw new InvalidDataException(
                    "La destinazione dell'aggiornamento è cambiata.");

            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateLocalPlanFactory.Create(archivePath,
                    trustStore, request, runtimeIdentifier, architecture,
                    binding.InstallationId, binding.TargetRoot,
                    binding.StagingRoot, binding.BackupRoot,
                    binding.ApplicationExecutable, parentProcessId,
                    parentProcessStartedUtc, now);
            DdfProductUpdatePreparedPackage package =
                DdfProductUpdatePackageDownloader.ReopenVerifiedLocal(
                    archivePath, trustStore, request, runtimeIdentifier,
                    architecture, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            DdfProductUpdateInstallationJournalStore.WritePlan(planPath, plan);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            DdfProductUpdateInstallationJournal journal = store.Create(
                DdfProductUpdateInstallationJson.ComputePlanSha256(plan), now);
            journal = store.Transition(journal.Revision,
                DdfProductUpdateInstallationState.DownloadAuthorized,
                DateTimeOffset.UtcNow);
            using var stager = new DdfProductUpdateStager();
            DdfProductUpdateStagingSeal seal = await stager.StageLocalAsync(
                plan, package.SignedManifestJson, trustStore, store,
                DateTimeOffset.UtcNow, package.ArchivePath,
                cancellationToken).ConfigureAwait(false);
            return new DdfProductUpdateLocalPreparationResult(plan, seal,
                planPath, journalPath);
        }

        private static bool IsInside(string path, string root) =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
