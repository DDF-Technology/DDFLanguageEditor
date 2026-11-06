using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Builds a bounded installation inventory from a locally downloaded,
    /// signed release archive. It never extracts or executes archive entries.
    /// </summary>
    public static class DdfProductUpdateArchiveInspector
    {
        private const int MaximumFiles = 4096;
        private const int BufferSize = 64 * 1024;

        public static IReadOnlyList<DdfProductUpdateInstallationFile> Inspect(
            string archivePath, string signedManifestJson,
            DdfReleaseTrustStore trustStore, DdfProductUpdateRequest request,
            string runtimeIdentifier, string architecture,
            DdfProductUpdateStagingOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (archivePath == null) throw new ArgumentNullException(nameof(archivePath));
            if (trustStore == null) throw new ArgumentNullException(nameof(trustStore));
            if (request == null) throw new ArgumentNullException(nameof(request));
            options ??= DdfProductUpdateStagingOptions.Default;
            string path = Path.GetFullPath(archivePath);
            if (!File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Archivio aggiornamento assente o non regolare.");

            DdfReleaseManifestVerification verification =
                DdfReleaseManifestVerifier.Verify(signedManifestJson, trustStore);
            DdfProductUpdateResult update = DdfReleaseUpdatePolicy.Evaluate(
                verification, request, runtimeIdentifier, architecture,
                DateTimeOffset.UtcNow);
            if (!update.IsActionable)
                throw new InvalidDataException(
                    "Release non verificata o incompatibile con questa installazione.");
            DdfReleaseManifestAsset[] candidates = verification.Manifest.Assets.Where(
                item => item.ComponentKind == request.ComponentKind &&
                    string.Equals(item.ComponentId, request.ComponentId,
                        StringComparison.Ordinal) &&
                    Uri.Equals(item.Uri, update.VerifiedReleaseUri)).ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException("Asset firmato ambiguo.");
            DdfReleaseManifestAsset asset = candidates[0];
            var info = new FileInfo(path);
            if (info.Length != asset.Size || info.Length > options.MaximumAssetBytes)
                throw new InvalidDataException("Dimensione archivio divergente.");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, BufferSize, FileOptions.SequentialScan);
            if (stream.Length != asset.Size)
                throw new InvalidDataException("Dimensione archivio divergente.");
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[BufferSize];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    hash.AppendData(buffer, 0, read);
                }
                string actualHash = Convert.ToHexString(hash.GetHashAndReset())
                    .ToLowerInvariant();
                if (!string.Equals(actualHash, asset.Sha256,
                        StringComparison.Ordinal))
                    throw new InvalidDataException("SHA-256 archivio non valido.");
            }
            stream.Position = 0;

            var files = new List<DdfProductUpdateInstallationFile>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read,
                leaveOpen: true);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                string raw = directory
                    ? entry.FullName.TrimEnd('/', '\\') : entry.FullName;
                string relative;
                try
                {
                    relative = DdfProductUpdateInstallationFile
                        .RequireSafeRelativePath(raw);
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException(
                        "L'archivio contiene un percorso non sicuro.", exception);
                }
                int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                int windowsAttributes = entry.ExternalAttributes & 0xFFFF;
                if (unixType == 0xA000 ||
                    (windowsAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "Link e reparse point non ammessi nell'archivio.");
                if (directory) continue;
                if (!names.Add(relative) || files.Count >= MaximumFiles)
                    throw new InvalidDataException(
                        "File duplicati o inventario oltre quota.");
                total = checked(total + entry.Length);
                if (entry.Length > options.MaximumExtractedBytes ||
                    total > options.MaximumExtractedBytes)
                    throw new InvalidDataException("Archivio estratto oltre quota.");

                using Stream input = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[BufferSize];
                long actual = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = input.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    actual = checked(actual + read);
                    if (actual > entry.Length)
                        throw new InvalidDataException("File estratto oltre quota.");
                    hash.AppendData(buffer, 0, read);
                }
                if (actual != entry.Length)
                    throw new InvalidDataException("File estratto tronco.");
                files.Add(new DdfProductUpdateInstallationFile(relative, actual,
                    Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
            }
            foreach (string required in new[]
            {
                "DDFLanguageEditor.exe", "DDFLanguageEditor.dll",
                "DDFLanguageEditor.Core.dll", "DDFLanguageEditor.runtimeconfig.json",
                "coreclr.dll", "hostfxr.dll", "hostpolicy.dll"
            })
                if (!names.Contains(required))
                    throw new InvalidDataException(
                        "Archivio aggiornamento privo di file essenziali: " +
                        required + ".");
            return new ReadOnlyCollection<DdfProductUpdateInstallationFile>(files
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList());
        }
    }
}
