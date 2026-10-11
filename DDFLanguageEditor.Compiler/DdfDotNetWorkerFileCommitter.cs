using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerFileCommitTarget
    {
        private DdfDotNetWorkerFileCommitTarget(string logicalName,
            string absolutePath, string originalSha256, int maxWriteBytes,
            string fileIdentity)
        {
            LogicalName = logicalName;
            AbsolutePath = absolutePath;
            OriginalSha256 = originalSha256;
            MaxWriteBytes = maxWriteBytes;
            FileIdentity = fileIdentity;
        }

        public string LogicalName { get; }
        public string AbsolutePath { get; }
        public string OriginalSha256 { get; }
        public int MaxWriteBytes { get; }
        internal string FileIdentity { get; }

        public static DdfDotNetWorkerFileCommitTarget FromFile(
            string logicalName, string absolutePath,
            int maxWriteBytes = 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
                throw new ArgumentException("Nome logico richiesto.",
                    nameof(logicalName));
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !Path.IsPathRooted(absolutePath) || !File.Exists(absolutePath))
                throw new ArgumentException(
                    "Il target broker deve essere un file assoluto esistente.",
                    nameof(absolutePath));
            if (maxWriteBytes <= 0 || maxWriteBytes > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxWriteBytes));
            string path = Path.GetFullPath(absolutePath);
            DdfDotNetWorkerFileProtection.ValidatePath(path,
                requireWriteAccess: true);
            string identityBefore = DdfDotNetWorkerFileProtection.GetIdentity(
                path);
            byte[] bytes = File.ReadAllBytes(path);
            string identityAfter = DdfDotNetWorkerFileProtection.GetIdentity(
                path);
            if (!string.Equals(identityBefore, identityAfter,
                    StringComparison.Ordinal))
                throw new IOException(
                    "Il target broker è cambiato durante l'autorizzazione.");
            return new DdfDotNetWorkerFileCommitTarget(logicalName, path,
                Hash(bytes), maxWriteBytes, identityAfter);
        }

        internal static string Hash(byte[] bytes) => Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public sealed class DdfDotNetWorkerFileProtectionReceipt
    {
        internal DdfDotNetWorkerFileProtectionReceipt(string logicalName,
            bool reparsePointsRejected, bool readWriteAccessVerified,
            bool identityVerified)
        {
            LogicalName = logicalName;
            ReparsePointsRejected = reparsePointsRejected;
            ReadWriteAccessVerified = readWriteAccessVerified;
            IdentityVerified = identityVerified;
        }

        public string LogicalName { get; }
        public bool ReparsePointsRejected { get; }
        public bool ReadWriteAccessVerified { get; }
        public bool IdentityVerified { get; }
    }

    internal sealed class DdfDotNetWorkerFileCommitResult
    {
        internal DdfDotNetWorkerFileCommitResult(
            IEnumerable<string> committedLogicalNames,
            IEnumerable<DdfDotNetWorkerFileProtectionReceipt> protections,
            DdfDotNetContractIssue issue = null)
        {
            CommittedLogicalNames = (committedLogicalNames ??
                Array.Empty<string>()).ToList().AsReadOnly();
            Issue = issue;
            Protections = (protections ?? Array.Empty<
                DdfDotNetWorkerFileProtectionReceipt>()).ToList().AsReadOnly();
        }

        internal IReadOnlyList<string> CommittedLogicalNames { get; }
        internal DdfDotNetContractIssue Issue { get; }
        internal IReadOnlyList<DdfDotNetWorkerFileProtectionReceipt>
            Protections { get; }
        internal bool Success => Issue == null;
    }

    internal static class DdfDotNetWorkerFileCommitter
    {
        internal static DdfDotNetContractIssue ValidateTargets(
            DdfDotNetWorkerRequest request,
            IReadOnlyList<DdfDotNetWorkerFileCommitTarget> targets)
        {
            DdfDotNetWorkerFileSnapshot[] writable = (request.BrokerFiles ??
                    new List<DdfDotNetWorkerFileSnapshot>())
                .Where(item => item?.Writable == true).ToArray();
            if (targets == null || targets.Count != writable.Length ||
                targets.Select(item => item?.LogicalName).Distinct(
                    StringComparer.Ordinal).Count() != targets.Count)
                return Issue("I target di commit non corrispondono agli " +
                    "snapshot broker scrivibili.");
            foreach (DdfDotNetWorkerFileSnapshot snapshot in writable)
            {
                DdfDotNetWorkerFileCommitTarget target = targets.SingleOrDefault(
                    item => string.Equals(item?.LogicalName,
                        snapshot.LogicalName, StringComparison.Ordinal));
                if (target == null || !Path.IsPathRooted(target.AbsolutePath) ||
                    target.MaxWriteBytes != snapshot.MaxWriteBytes ||
                    !string.Equals(target.OriginalSha256, snapshot.Sha256,
                        StringComparison.Ordinal))
                    return Issue("Target di commit non coerente con lo " +
                        "snapshot " + snapshot.LogicalName + ".");
                try
                {
                    DdfDotNetWorkerFileProtection.ValidatePath(
                        target.AbsolutePath, requireWriteAccess: true);
                    if (!string.Equals(
                            DdfDotNetWorkerFileProtection.GetIdentity(
                                target.AbsolutePath), target.FileIdentity,
                            StringComparison.Ordinal))
                        return ProtectionIssue("L'identità fisica del target " +
                            snapshot.LogicalName + " è cambiata.");
                }
                catch (Exception exception) when (exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is PlatformNotSupportedException)
                {
                    return ProtectionIssue("Protezione del target " +
                        snapshot.LogicalName + " non verificabile: " +
                        exception.Message);
                }
            }
            return null;
        }

        internal static DdfDotNetWorkerFileCommitResult Commit(
            DdfDotNetWorkerRequest request, DdfDotNetWorkerResponse response,
            IReadOnlyList<DdfDotNetWorkerFileCommitTarget> targets)
        {
            DdfDotNetContractIssue targetIssue = ValidateTargets(request,
                targets);
            if (targetIssue != null)
                return new DdfDotNetWorkerFileCommitResult(null, null,
                    targetIssue);
            DdfDotNetWorkerFileWrite[] writes = (response.BrokerFileWrites ??
                new List<DdfDotNetWorkerFileWrite>()).ToArray();
            if (writes.Select(item => item?.LogicalName).Distinct(
                    StringComparer.Ordinal).Count() != writes.Length)
                return Failed("La risposta contiene scritture duplicate.");
            var prepared = new List<(DdfDotNetWorkerFileCommitTarget Target,
                string Temporary, string Backup)>();
            try
            {
                foreach (DdfDotNetWorkerFileWrite write in writes)
                {
                    DdfDotNetWorkerFileCommitTarget target = targets
                        .SingleOrDefault(item => string.Equals(item.LogicalName,
                            write?.LogicalName, StringComparison.Ordinal));
                    if (target == null || !File.Exists(target.AbsolutePath))
                        return Failed("Scrittura broker senza target autorizzato.");
                    byte[] current = File.ReadAllBytes(target.AbsolutePath);
                    byte[] content = Encoding.UTF8.GetBytes(
                        write.Content ?? string.Empty);
                    string contentHash = DdfDotNetWorkerFileCommitTarget.Hash(
                        content);
                    if (!string.Equals(
                            DdfDotNetWorkerFileCommitTarget.Hash(current),
                            target.OriginalSha256, StringComparison.Ordinal) ||
                        !string.Equals(write.OriginalSha256,
                            target.OriginalSha256, StringComparison.Ordinal) ||
                        !string.Equals(write.Sha256, contentHash,
                            StringComparison.Ordinal) ||
                        content.Length > target.MaxWriteBytes)
                        return Failed("Concorrenza, hash o budget non valido " +
                            "per " + target.LogicalName + ".");
                    string directory = Path.GetDirectoryName(
                        target.AbsolutePath) ?? throw new IOException(
                        "Directory target non disponibile.");
                    string suffix = ".ddf-" + Guid.NewGuid().ToString("N");
                    string temporary = target.AbsolutePath + suffix + ".tmp";
                    string backup = target.AbsolutePath + suffix + ".bak";
                    File.WriteAllBytes(temporary, content);
                    File.Copy(target.AbsolutePath, backup, overwrite: false);
                    prepared.Add((target, temporary, backup));
                }
                foreach (var item in prepared)
                {
                    DdfDotNetWorkerFileProtection.ValidatePath(
                        item.Target.AbsolutePath, requireWriteAccess: true);
                    if (!string.Equals(
                            DdfDotNetWorkerFileProtection.GetIdentity(
                                item.Target.AbsolutePath),
                            item.Target.FileIdentity,
                            StringComparison.Ordinal) ||
                        !string.Equals(DdfDotNetWorkerFileCommitTarget.Hash(
                                File.ReadAllBytes(item.Target.AbsolutePath)),
                            item.Target.OriginalSha256,
                            StringComparison.Ordinal))
                        throw new IOException(
                            "Target broker sostituito durante il commit: " +
                            item.Target.LogicalName + ".");
                }
                int committed = 0;
                try
                {
                    foreach (var item in prepared)
                    {
                        File.Move(item.Temporary, item.Target.AbsolutePath,
                            overwrite: true);
                        committed++;
                    }
                }
                catch
                {
                    for (int index = committed - 1; index >= 0; index--)
                        File.Move(prepared[index].Backup,
                            prepared[index].Target.AbsolutePath,
                            overwrite: true);
                    throw;
                }
                return new DdfDotNetWorkerFileCommitResult(prepared.Select(
                    item => item.Target.LogicalName), prepared.Select(item =>
                        new DdfDotNetWorkerFileProtectionReceipt(
                            item.Target.LogicalName,
                            reparsePointsRejected: true,
                            readWriteAccessVerified: true,
                            identityVerified: OperatingSystem.IsWindows())));
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException)
            {
                return Failed("Commit transazionale file broker fallito: " +
                    exception.Message);
            }
            finally
            {
                foreach (var item in prepared)
                {
                    TryDelete(item.Temporary);
                    TryDelete(item.Backup);
                }
            }
        }

        private static DdfDotNetWorkerFileCommitResult Failed(string message) =>
            new(null, null, Issue(message));

        private static DdfDotNetContractIssue Issue(string message) => new(
            "DDFC540", message);

        private static DdfDotNetContractIssue ProtectionIssue(string message) =>
            new("DDFC541", message);

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class DdfDotNetWorkerFileProtection
    {
        internal static void ValidatePath(string path, bool requireWriteAccess)
        {
            string fullPath = Path.GetFullPath(path);
            FileSystemInfo current = new FileInfo(fullPath);
            while (current != null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(
                        "Reparse point non consentito nel percorso broker: " +
                        current.FullName);
                current = current is FileInfo file
                    ? file.Directory
                    : ((DirectoryInfo)current).Parent;
            }
            using SafeFileHandle handle = File.OpenHandle(fullPath,
                FileMode.Open, requireWriteAccess ? FileAccess.ReadWrite :
                    FileAccess.Read, FileShare.Read);
        }

        internal static string GetIdentity(string path)
        {
            if (!OperatingSystem.IsWindows()) return string.Empty;
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            if (!GetFileInformationByHandle(handle, out FileInformation info))
                throw new IOException("Identità file Windows non leggibile.",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            return info.VolumeSerialNumber.ToString("x8") + ":" +
                info.FileIndexHigh.ToString("x8") +
                info.FileIndexLow.ToString("x8");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME
                CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME
                LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME
                LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file, out FileInformation information);
    }
}
