using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Copies the signed-inventory worker outside the directory being replaced.
    /// This does not authorize installation or start a process.
    /// </summary>
    public static class DdfProductUpdateWorkerDeployment
    {
        public const string WorkerRelativePath =
            "support/updater/DDFLanguageEditor.Updater.exe";
        private const long MaximumWorkerBytes = 256L * 1024 * 1024;

        public static string CopyVerified(DdfProductUpdateInstallationPlan plan,
            string stateDirectory)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(stateDirectory) ||
                !Path.IsPathFullyQualified(stateDirectory))
                throw new ArgumentException("Directory di stato assoluta richiesta.",
                    nameof(stateDirectory));
            string state = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(stateDirectory));
            if (state.Equals(Path.GetPathRoot(state),
                    StringComparison.OrdinalIgnoreCase) ||
                IsInside(state, plan.TargetRoot) ||
                IsInside(state, plan.StagingRoot) ||
                IsInside(state, plan.BackupRoot) ||
                !Directory.Exists(state))
                throw new InvalidDataException(
                    "Area del worker assente o interna all'installazione.");
            RejectReparseAncestors(state);

            DdfProductUpdateInstallationFile worker = plan.Inventory.SingleOrDefault(
                file => file.RelativePath.Equals(WorkerRelativePath,
                    StringComparison.OrdinalIgnoreCase));
            if (worker == null || worker.Size <= 0 ||
                worker.Size > MaximumWorkerBytes)
                throw new InvalidDataException(
                    "Worker updater assente o oltre quota nell'inventario firmato.");
            DdfProductUpdateInstallationVerifier.VerifyStaging(plan);

            string source = Path.Combine(plan.StagingRoot, "support", "updater",
                "DDFLanguageEditor.Updater.exe");
            RejectReparseAncestors(Path.GetDirectoryName(source));
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 ||
                !MatchesInventory(source, worker))
                throw new InvalidDataException(
                    "Worker nello staging divergente dall'inventario firmato.");

            string workerRoot = Path.Combine(state, "worker-" + plan.Nonce);
            if (Directory.Exists(workerRoot) || File.Exists(workerRoot))
                throw new InvalidDataException(
                    "Area del worker già occupata: nessuna sovrascrittura ammessa.");
            Directory.CreateDirectory(workerRoot);
            string temporary = Path.Combine(workerRoot, "worker.partial");
            string destination = Path.Combine(workerRoot,
                "DDFLanguageEditor.Updater.exe");
            try
            {
                File.Copy(source, temporary, overwrite: false);
                if (!MatchesInventory(temporary, worker))
                    throw new InvalidDataException(
                        "Copia del worker divergente dall'inventario firmato.");
                File.Move(temporary, destination);
                return destination;
            }
            catch
            {
                // Only remove files this method created; never recurse through
                // an area that another process could have changed meanwhile.
                if (File.Exists(temporary)) File.Delete(temporary);
                if (File.Exists(destination)) File.Delete(destination);
                if (Directory.Exists(workerRoot))
                    Directory.Delete(workerRoot, recursive: false);
                throw;
            }
        }

        /// <summary>Keeps write access denied while the verified worker starts.</summary>
        public static FileStream OpenVerifiedForLaunch(
            DdfProductUpdateInstallationPlan plan, string deployedPath)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(deployedPath) ||
                !Path.IsPathFullyQualified(deployedPath))
                throw new InvalidDataException("Percorso worker non valido.");
            string path = Path.GetFullPath(deployedPath);
            if (!Path.GetFileName(path).Equals(
                    "DDFLanguageEditor.Updater.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(Path.GetDirectoryName(path)).Equals(
                    "worker-" + plan.Nonce, StringComparison.Ordinal) ||
                IsInside(path, plan.TargetRoot) ||
                IsInside(path, plan.StagingRoot) ||
                IsInside(path, plan.BackupRoot))
                throw new InvalidDataException("Worker fuori dall'area attesa.");
            RejectReparseAncestors(Path.GetDirectoryName(path));
            DdfProductUpdateInstallationFile worker = plan.Inventory.SingleOrDefault(
                file => file.RelativePath.Equals(WorkerRelativePath,
                    StringComparison.OrdinalIgnoreCase));
            if (worker == null || worker.Size <= 0 ||
                worker.Size > MaximumWorkerBytes ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Worker non attendibile.");
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read);
            try
            {
                if (stream.Length != worker.Size ||
                    !Convert.ToHexString(SHA256.HashData(stream))
                        .Equals(worker.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Worker copiato divergente dall'inventario firmato.");
                stream.Position = 0;
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static bool MatchesInventory(string path,
            DdfProductUpdateInstallationFile worker)
        {
            var info = new FileInfo(path);
            if (info.Length != worker.Size) return false;
            using FileStream stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
            return hash.Equals(worker.Sha256, StringComparison.Ordinal);
        }

        private static void RejectReparseAncestors(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current);
                current = Path.GetDirectoryName(current))
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "Il percorso del worker attraversa un reparse point.");
        }

        private static bool IsInside(string path, string root) =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
