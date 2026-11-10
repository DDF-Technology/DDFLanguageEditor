using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Discards only a pre-swap update attempt. Never removes the installation
    /// target, a backup, or a rollback quarantine.
    /// </summary>
    public static class DdfProductUpdateRetryCleanup
    {
        public static bool CanDiscard(DdfProductUpdateStartupRecoveryStatus status) =>
            status != null && status.HasState && status.State.HasValue &&
            IsPreSwap(status.State.Value) &&
            (status.Action == DdfProductUpdateRecoveryAction.DiscardStaging ||
             status.Action == DdfProductUpdateRecoveryAction.None);

        public static bool CanFinalizeCompleted(
            DdfProductUpdateStartupRecoveryStatus status) =>
            status != null && status.HasState &&
            status.State == DdfProductUpdateInstallationState.Completed &&
            (status.Action == DdfProductUpdateRecoveryAction.CleanupDeferred ||
             status.Action == DdfProductUpdateRecoveryAction.None);

        public static void Discard(string stateDirectory,
            string currentExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(stateDirectory) ||
                !Path.IsPathFullyQualified(stateDirectory))
                throw new ArgumentException("Directory di stato assoluta richiesta.",
                    nameof(stateDirectory));
            string state = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(stateDirectory));
            if (state.Equals(Path.GetPathRoot(state),
                    StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(state))
                throw new InvalidDataException("Area di stato non valida.");
            RejectReparseAncestors(state);
            string planPath = Path.Combine(state, "plan.json");
            string journalPath = Path.Combine(state, "journal.json");
            RejectLink(planPath);
            RejectLink(journalPath);
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            (string targetParent, string expectedStem) = ValidateOwner(
                state, plan, currentExecutablePath);
            RejectLink(plan.BackupRoot);
            if (Directory.Exists(plan.BackupRoot) || File.Exists(plan.BackupRoot) ||
                !Directory.Exists(plan.TargetRoot))
                throw new InvalidDataException(
                    "Il backup o l'installazione richiedono recovery, non ritentativo.");
            string workerRoot = Path.Combine(state, "worker-" + plan.Nonce);
            string extracting = plan.StagingRoot + ".extracting-" + plan.Nonce;
            string partial = Path.Combine(targetParent, "." + expectedStem +
                "." + plan.Nonce + ".partial");
            string seal = DdfProductUpdateStagingSealCodec.SealPathFor(
                plan.StagingRoot);
            RejectLink(workerRoot);
            RejectLink(plan.StagingRoot);
            RejectLink(extracting);
            if (File.Exists(workerRoot))
                throw new InvalidDataException("Area worker occupata da un file.");
            if (Directory.Exists(workerRoot))
                VerifyWorkerDirectory(workerRoot);
            if (File.Exists(plan.StagingRoot))
                throw new InvalidDataException("Staging occupato da un file.");
            if (Directory.Exists(plan.StagingRoot))
                VerifyNoLinks(plan.StagingRoot);
            if (File.Exists(extracting) || Directory.Exists(partial) ||
                Directory.Exists(seal))
                throw new InvalidDataException("Residuo updater di tipo inatteso.");
            if (Directory.Exists(extracting)) VerifyNoLinks(extracting);
            RejectLink(partial);
            RejectLink(seal);

            string lockPath = journalPath + ".lock";
            RejectLink(lockPath);
            using (var installationLock = new FileStream(lockPath,
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var store = new DdfProductUpdateInstallationJournalStore(
                    journalPath);
                DdfProductUpdateInstallationJournal journal = store.Read();
                if (journal.PlanSha256 != DdfProductUpdateInstallationJson
                        .ComputePlanSha256(plan) ||
                    !IsPreSwap(journal.State) ||
                    Directory.Exists(plan.BackupRoot) ||
                    File.Exists(plan.BackupRoot))
                    throw new InvalidDataException(
                        "Stato non correlato o non ripulibile in sicurezza.");
                RejectLink(plan.BackupRoot);
                RejectLink(workerRoot);
                RejectLink(plan.StagingRoot);
                RejectLink(extracting);
                if (Directory.Exists(workerRoot)) VerifyWorkerDirectory(workerRoot);
                if (Directory.Exists(plan.StagingRoot))
                    VerifyNoLinks(plan.StagingRoot);
                if (Directory.Exists(extracting)) VerifyNoLinks(extracting);
                RejectLink(partial);
                RejectLink(seal);
                if (Directory.Exists(workerRoot))
                {
                    string worker = Path.Combine(workerRoot,
                        "DDFLanguageEditor.Updater.exe");
                    if (File.Exists(worker)) File.Delete(worker);
                    Directory.Delete(workerRoot, recursive: false);
                }
                if (Directory.Exists(plan.StagingRoot))
                    Directory.Delete(plan.StagingRoot, recursive: true);
                if (Directory.Exists(extracting))
                    Directory.Delete(extracting, recursive: true);
                if (File.Exists(partial)) File.Delete(partial);
                if (File.Exists(seal)) File.Delete(seal);
                File.Delete(planPath);
                File.Delete(journalPath);
            }
        }

        public static void FinalizeCompleted(string stateDirectory,
            string currentExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(stateDirectory) ||
                !Path.IsPathFullyQualified(stateDirectory))
                throw new ArgumentException("Directory di stato assoluta richiesta.",
                    nameof(stateDirectory));
            string state = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(stateDirectory));
            if (!Directory.Exists(state) ||
                state.Equals(Path.GetPathRoot(state),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Area di stato non valida.");
            RejectReparseAncestors(state);
            string planPath = Path.Combine(state, "plan.json");
            string journalPath = Path.Combine(state, "journal.json");
            RejectLink(planPath);
            RejectLink(journalPath);
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            ValidateOwner(state, plan, currentExecutablePath);
            string workerRoot = Path.Combine(state, "worker-" + plan.Nonce);
            string seal = DdfProductUpdateStagingSealCodec.SealPathFor(
                plan.StagingRoot);
            string quarantine = plan.StagingRoot + ".failed-" + plan.Nonce;
            if (Directory.Exists(plan.StagingRoot) ||
                File.Exists(plan.StagingRoot) ||
                Directory.Exists(quarantine) || File.Exists(quarantine) ||
                File.Exists(plan.BackupRoot) || File.Exists(workerRoot) ||
                Directory.Exists(seal))
                throw new InvalidDataException(
                    "Copie o residui non coerenti con un aggiornamento completato.");
            RejectLink(plan.StagingRoot);
            RejectLink(quarantine);
            RejectLink(plan.BackupRoot);
            RejectLink(workerRoot);
            RejectLink(seal);
            if (Directory.Exists(workerRoot)) VerifyWorkerDirectory(workerRoot);
            if (Directory.Exists(plan.BackupRoot)) VerifyNoLinks(plan.BackupRoot);
            string lockPath = journalPath + ".lock";
            RejectLink(lockPath);
            using (var installationLock = new FileStream(lockPath,
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var store = new DdfProductUpdateInstallationJournalStore(
                    journalPath);
                DdfProductUpdateInstallationJournal journal = store.Read();
                if (journal.PlanSha256 != DdfProductUpdateInstallationJson
                        .ComputePlanSha256(plan) ||
                    journal.State != DdfProductUpdateInstallationState.Completed ||
                    Directory.Exists(plan.StagingRoot) ||
                    Directory.Exists(quarantine))
                    throw new InvalidDataException(
                        "Journal non completato o stato filesystem cambiato.");
                DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                    plan.TargetRoot);
                RejectLink(plan.BackupRoot);
                RejectLink(workerRoot);
                RejectLink(seal);
                if (Directory.Exists(plan.BackupRoot))
                {
                    VerifyNoLinks(plan.BackupRoot);
                    Directory.Delete(plan.BackupRoot, recursive: true);
                }
                if (Directory.Exists(workerRoot))
                {
                    VerifyWorkerDirectory(workerRoot);
                    string worker = Path.Combine(workerRoot,
                        "DDFLanguageEditor.Updater.exe");
                    if (File.Exists(worker)) File.Delete(worker);
                    Directory.Delete(workerRoot, recursive: false);
                }
                if (File.Exists(seal)) File.Delete(seal);
                File.Delete(planPath);
                File.Delete(journalPath);
            }
        }

        private static (string TargetParent, string ExpectedStem) ValidateOwner(
            string state, DdfProductUpdateInstallationPlan plan,
            string currentExecutablePath)
        {
            DdfProductUpdateInstallationBinding binding =
                DdfProductUpdateInstallationBinding.Resolve(currentExecutablePath);
            string targetParent = Path.GetDirectoryName(binding.TargetRoot);
            string expectedStem = Path.GetFileName(plan.StagingRoot);
            if (!plan.TargetRoot.Equals(binding.TargetRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                plan.InstallationId != binding.InstallationId ||
                IsInside(state, plan.TargetRoot) ||
                IsInside(state, plan.StagingRoot) ||
                IsInside(state, plan.BackupRoot) ||
                !Path.GetDirectoryName(plan.StagingRoot).Equals(targetParent,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetDirectoryName(plan.BackupRoot).Equals(targetParent,
                    StringComparison.OrdinalIgnoreCase) ||
                !Regex.IsMatch(expectedStem,
                    "^\\.ddf-update-[0-9a-f]{24}\\.staging$",
                    RegexOptions.CultureInvariant) ||
                !Path.GetFileName(plan.BackupRoot).Equals(
                    expectedStem.Replace(".staging", ".backup"),
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Il piano non appartiene all'installazione corrente.");
            return (targetParent, expectedStem);
        }

        private static bool IsPreSwap(DdfProductUpdateInstallationState state) =>
            state == DdfProductUpdateInstallationState.Checked ||
            state == DdfProductUpdateInstallationState.DownloadAuthorized ||
            state == DdfProductUpdateInstallationState.Downloading ||
            state == DdfProductUpdateInstallationState.Staged ||
            state == DdfProductUpdateInstallationState.InstallAuthorized ||
            state == DdfProductUpdateInstallationState.WaitingForEditorExit ||
            state == DdfProductUpdateInstallationState.Cancelled ||
            state == DdfProductUpdateInstallationState.Failed;

        private static void VerifyWorkerDirectory(string root)
        {
            RejectLink(root);
            if (Directory.GetDirectories(root).Length != 0)
                throw new InvalidDataException(
                    "Area worker contenente directory non previste.");
            foreach (string file in Directory.GetFiles(root))
            {
                RejectLink(file);
                if (!Path.GetFileName(file).Equals(
                        "DDFLanguageEditor.Updater.exe",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Area worker contenente file estranei.");
            }
        }

        private static void VerifyNoLinks(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                string current = pending.Pop();
                RejectLink(current);
                foreach (string directory in Directory.GetDirectories(current))
                    pending.Push(directory);
                foreach (string file in Directory.GetFiles(current))
                    RejectLink(file);
            }
        }

        private static void RejectReparseAncestors(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current);
                current = Path.GetDirectoryName(current))
                if (Directory.Exists(current)) RejectLink(current);
        }

        private static void RejectLink(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "Reparse point non ammesso durante il ritentativo.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }

        private static bool IsInside(string path, string root) =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
