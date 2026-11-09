using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfProductUpdateStagingOptions
    {
        public const long DefaultMaximumAssetBytes = 2L * 1024 * 1024 * 1024;
        public const long DefaultMaximumExtractedBytes = 8L * 1024 * 1024 * 1024;

        public DdfProductUpdateStagingOptions(TimeSpan timeout,
            long maximumAssetBytes = DefaultMaximumAssetBytes,
            long maximumExtractedBytes = DefaultMaximumExtractedBytes)
        {
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(2))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (maximumAssetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumAssetBytes));
            if (maximumExtractedBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumExtractedBytes));
            Timeout = timeout;
            MaximumAssetBytes = maximumAssetBytes;
            MaximumExtractedBytes = maximumExtractedBytes;
        }

        public TimeSpan Timeout { get; }
        public long MaximumAssetBytes { get; }
        public long MaximumExtractedBytes { get; }

        public static DdfProductUpdateStagingOptions Default { get; } =
            new DdfProductUpdateStagingOptions(TimeSpan.FromMinutes(10));
    }

    public sealed class DdfProductUpdateStagingProgress
    {
        public DdfProductUpdateStagingProgress(long bytesReceived, long totalBytes)
        {
            if (bytesReceived < 0 || totalBytes <= 0 || bytesReceived > totalBytes)
                throw new ArgumentOutOfRangeException(nameof(bytesReceived));
            BytesReceived = bytesReceived;
            TotalBytes = totalBytes;
        }

        public long BytesReceived { get; }
        public long TotalBytes { get; }
        public double Fraction => (double)BytesReceived / TotalBytes;
    }

    public sealed class DdfProductUpdateStagingSeal
    {
        public const string SchemaVersionValue = "1.0";

        internal DdfProductUpdateStagingSeal(string planSha256,
            string inventorySha256, string assetSha256, int fileCount,
            long totalFileBytes, DateTimeOffset stagedUtc)
        {
            PlanSha256 = DdfProductUpdateInstallationFile.RequireHash(planSha256,
                nameof(planSha256));
            InventorySha256 = DdfProductUpdateInstallationFile.RequireHash(
                inventorySha256, nameof(inventorySha256));
            AssetSha256 = DdfProductUpdateInstallationFile.RequireHash(assetSha256,
                nameof(assetSha256));
            if (fileCount <= 0 || fileCount > 4096)
                throw new ArgumentOutOfRangeException(nameof(fileCount));
            if (totalFileBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(totalFileBytes));
            if (stagedUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("L'istante deve essere UTC.",
                    nameof(stagedUtc));
            FileCount = fileCount;
            TotalFileBytes = totalFileBytes;
            StagedUtc = stagedUtc;
        }

        public string SchemaVersion => SchemaVersionValue;
        public string PlanSha256 { get; }
        public string InventorySha256 { get; }
        public string AssetSha256 { get; }
        public int FileCount { get; }
        public long TotalFileBytes { get; }
        public DateTimeOffset StagedUtc { get; }
    }

    public static class DdfProductUpdateStagingSealCodec
    {
        private const int MaximumBytes = 64 * 1024;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static string SealPathFor(string stagingRoot) =>
            Path.GetFullPath(stagingRoot) + ".seal.json";

        public static string Serialize(DdfProductUpdateStagingSeal seal)
        {
            if (seal == null) throw new ArgumentNullException(nameof(seal));
            return JsonSerializer.Serialize(new SealDocument
            {
                SchemaVersion = seal.SchemaVersion,
                PlanSha256 = seal.PlanSha256,
                InventorySha256 = seal.InventorySha256,
                AssetSha256 = seal.AssetSha256,
                FileCount = seal.FileCount,
                TotalFileBytes = seal.TotalFileBytes,
                StagedUtc = seal.StagedUtc
            }, Options);
        }

        public static DdfProductUpdateStagingSeal Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("Sigillo staging vuoto o oltre quota.");
            try
            {
                SealDocument document = JsonSerializer.Deserialize<SealDocument>(json,
                    Options) ?? throw new InvalidDataException("Sigillo staging nullo.");
                if (document.SchemaVersion !=
                    DdfProductUpdateStagingSeal.SchemaVersionValue)
                    throw new InvalidDataException(
                        "Versione del sigillo staging non supportata.");
                return new DdfProductUpdateStagingSeal(document.PlanSha256,
                    document.InventorySha256, document.AssetSha256,
                    document.FileCount, document.TotalFileBytes,
                    document.StagedUtc);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Sigillo staging non valido.",
                    exception);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Sigillo staging non valido.",
                    exception);
            }
        }

        public static DdfProductUpdateStagingSeal Read(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaximumBytes)
                throw new InvalidDataException("Sigillo staging assente o oltre quota.");
            return Parse(File.ReadAllText(info.FullName, Encoding.UTF8));
        }

        public static string ComputeInventorySha256(
            IReadOnlyList<DdfProductUpdateInstallationFile> inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            var builder = new StringBuilder();
            foreach (DdfProductUpdateInstallationFile file in inventory
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
                builder.Append(file.RelativePath).Append('\0')
                    .Append(file.Size).Append('\0').Append(file.Sha256).Append('\n');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                builder.ToString()))).ToLowerInvariant();
        }

        public static void ValidateForPlan(DdfProductUpdateStagingSeal seal,
            DdfProductUpdateInstallationPlan plan)
        {
            if (seal == null) throw new ArgumentNullException(nameof(seal));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            long total = 0;
            foreach (DdfProductUpdateInstallationFile file in plan.Inventory)
                total = checked(total + file.Size);
            if (!string.Equals(seal.PlanSha256,
                    DdfProductUpdateInstallationJson.ComputePlanSha256(plan),
                    StringComparison.Ordinal) ||
                !string.Equals(seal.InventorySha256,
                    ComputeInventorySha256(plan.Inventory),
                    StringComparison.Ordinal) ||
                !string.Equals(seal.AssetSha256, plan.AssetSha256,
                    StringComparison.Ordinal) ||
                seal.FileCount != plan.Inventory.Count ||
                seal.TotalFileBytes != total)
                throw new InvalidDataException(
                    "Il sigillo staging non corrisponde al piano.");
        }

        internal static void WriteAtomic(string path,
            DdfProductUpdateStagingSeal seal)
        {
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            Directory.CreateDirectory(directory);
            string temporary = full + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(Serialize(seal));
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, full, false);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private sealed class SealDocument
        {
            public string SchemaVersion { get; set; }
            public string PlanSha256 { get; set; }
            public string InventorySha256 { get; set; }
            public string AssetSha256 { get; set; }
            public int FileCount { get; set; }
            public long TotalFileBytes { get; set; }
            public DateTimeOffset StagedUtc { get; set; }
        }
    }

    public sealed class DdfProductUpdateStager : IDisposable
    {
        private const int BufferSize = 64 * 1024;
        private static readonly HashSet<string> AllowedRedirectHosts = new(
            new[]
            {
                "release-assets.githubusercontent.com",
                "objects.githubusercontent.com",
                "github-releases.githubusercontent.com"
            }, StringComparer.OrdinalIgnoreCase);
        private readonly HttpClient client;
        private readonly DdfProductUpdateStagingOptions options;
        private bool disposed;

        public DdfProductUpdateStager(HttpMessageHandler handler = null,
            DdfProductUpdateStagingOptions options = null)
        {
            if (handler == null)
            {
                handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    UseCookies = false
                };
            }
            client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            this.options = options ?? DdfProductUpdateStagingOptions.Default;
        }

        public Task<DdfProductUpdateStagingSeal> StageAsync(
            DdfProductUpdateInstallationPlan plan, string signedManifestJson,
            DdfReleaseTrustStore trustStore,
            DdfProductUpdateInstallationJournalStore journalStore,
            DateTimeOffset stagedUtc, CancellationToken cancellationToken = default,
            IProgress<DdfProductUpdateStagingProgress> progress = null)
            => StageCoreAsync(plan, signedManifestJson, trustStore, journalStore,
                stagedUtc, null, cancellationToken, progress);

        public Task<DdfProductUpdateStagingSeal> StageLocalAsync(
            DdfProductUpdateInstallationPlan plan, string signedManifestJson,
            DdfReleaseTrustStore trustStore,
            DdfProductUpdateInstallationJournalStore journalStore,
            DateTimeOffset stagedUtc, string verifiedArchivePath,
            CancellationToken cancellationToken = default,
            IProgress<DdfProductUpdateStagingProgress> progress = null)
        {
            if (string.IsNullOrWhiteSpace(verifiedArchivePath))
                throw new ArgumentException("Archivio locale richiesto.",
                    nameof(verifiedArchivePath));
            return StageCoreAsync(plan, signedManifestJson, trustStore,
                journalStore, stagedUtc, verifiedArchivePath,
                cancellationToken, progress);
        }

        private async Task<DdfProductUpdateStagingSeal> StageCoreAsync(
            DdfProductUpdateInstallationPlan plan, string signedManifestJson,
            DdfReleaseTrustStore trustStore,
            DdfProductUpdateInstallationJournalStore journalStore,
            DateTimeOffset stagedUtc, string verifiedArchivePath,
            CancellationToken cancellationToken,
            IProgress<DdfProductUpdateStagingProgress> progress)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (trustStore == null) throw new ArgumentNullException(nameof(trustStore));
            if (journalStore == null) throw new ArgumentNullException(nameof(journalStore));
            if (stagedUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("L'istante deve essere UTC.",
                    nameof(stagedUtc));

            string planHash = DdfProductUpdateInstallationJson
                .ComputePlanSha256(plan);
            DdfProductUpdateInstallationJournal journal = journalStore.Read();
            if (!string.Equals(journal.PlanSha256, planHash,
                    StringComparison.Ordinal) ||
                journal.State != DdfProductUpdateInstallationState.DownloadAuthorized)
                throw new InvalidOperationException(
                    "Il journal non autorizza il download di questo piano.");
            if (stagedUtc < journal.UpdatedUtc)
                throw new ArgumentException(
                    "L'istante di staging precede il journal.", nameof(stagedUtc));
            long declaredExtractedBytes = 0;
            foreach (DdfProductUpdateInstallationFile file in plan.Inventory)
            {
                declaredExtractedBytes = checked(declaredExtractedBytes + file.Size);
                if (file.Size > options.MaximumExtractedBytes ||
                    declaredExtractedBytes > options.MaximumExtractedBytes)
                    throw new InvalidDataException(
                        "Inventario estratto oltre il limite configurato.");
            }

            string parent = Path.GetDirectoryName(plan.StagingRoot);
            string partial = Path.Combine(parent, "." +
                Path.GetFileName(plan.StagingRoot) + "." + plan.Nonce + ".partial");
            string extracting = plan.StagingRoot + ".extracting-" + plan.Nonce;
            string sealPath = DdfProductUpdateStagingSealCodec.SealPathFor(
                plan.StagingRoot);
            bool ownsPartial = false;
            bool ownsExtracting = false;
            bool ownsStaging = false;

            try
            {
                VerifyManifest(plan, signedManifestJson, trustStore);
                ValidateDestination(parent, plan.StagingRoot, extracting, partial,
                    sealPath);
                DateTimeOffset downloadStartedUtc = DateTimeOffset.UtcNow;
                if (downloadStartedUtc < journal.UpdatedUtc)
                    downloadStartedUtc = journal.UpdatedUtc;
                journal = journalStore.Transition(journal.Revision,
                    DdfProductUpdateInstallationState.Downloading,
                    downloadStartedUtc);
                ownsPartial = true;
                if (verifiedArchivePath == null)
                    await DownloadAsync(plan, partial, progress, cancellationToken)
                        .ConfigureAwait(false);
                else
                    await CopyVerifiedLocalArchiveAsync(plan,
                        verifiedArchivePath, partial, progress,
                        cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                VerifyManifest(plan, signedManifestJson, trustStore);
                Directory.CreateDirectory(extracting);
                ownsExtracting = true;
                ExtractionSummary summary = ExtractAndVerify(plan, partial,
                    extracting, options.MaximumExtractedBytes, cancellationToken);
                VerifyManifest(plan, signedManifestJson, trustStore);

                Directory.Move(extracting, plan.StagingRoot);
                ownsExtracting = false;
                ownsStaging = true;
                var seal = new DdfProductUpdateStagingSeal(planHash,
                    DdfProductUpdateStagingSealCodec.ComputeInventorySha256(
                        plan.Inventory), plan.AssetSha256,
                    summary.FileCount, summary.TotalBytes, stagedUtc);
                DdfProductUpdateStagingSealCodec.WriteAtomic(sealPath, seal);
                journalStore.Transition(journal.Revision,
                    DdfProductUpdateInstallationState.Staged, stagedUtc);
                File.Delete(partial);
                ownsPartial = false;
                ownsStaging = false;
                return seal;
            }
            catch (OperationCanceledException exception) when
                (!cancellationToken.IsCancellationRequested)
            {
                TryTransition(journalStore,
                    DdfProductUpdateInstallationState.Failed,
                    "Timeout durante download o staging.");
                throw new TimeoutException(
                    "Timeout durante download o staging dell'aggiornamento.",
                    exception);
            }
            catch (OperationCanceledException)
            {
                TryTransition(journalStore,
                    DdfProductUpdateInstallationState.Cancelled,
                    "Download annullato dall'utente.");
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException ||
                exception is IOException || exception is InvalidDataException ||
                exception is CryptographicException ||
                exception is OverflowException)
            {
                TryTransition(journalStore,
                    DdfProductUpdateInstallationState.Failed,
                    "Download o staging non riuscito.");
                throw;
            }
            finally
            {
                if (ownsPartial) TryDeleteFile(partial);
                if (ownsExtracting) TryDeleteDirectory(extracting);
                if (ownsStaging)
                {
                    TryDeleteFile(sealPath);
                    TryDeleteDirectory(plan.StagingRoot);
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            client.Dispose();
        }

        private async Task CopyVerifiedLocalArchiveAsync(
            DdfProductUpdateInstallationPlan plan, string sourcePath,
            string destination,
            IProgress<DdfProductUpdateStagingProgress> progress,
            CancellationToken cancellationToken)
        {
            if (!Path.IsPathFullyQualified(sourcePath))
                throw new InvalidDataException("Archivio locale non assoluto.");
            string source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source) ||
                (File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 ||
                IsInside(source, plan.TargetRoot) ||
                IsInside(source, plan.StagingRoot) ||
                IsInside(source, plan.BackupRoot))
                throw new InvalidDataException(
                    "Archivio locale assente o in una directory non ammessa.");
            if (plan.AssetSize > options.MaximumAssetBytes)
                throw new InvalidDataException("Asset oltre il limite configurato.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(options.Timeout);
            await using var input = new FileStream(source, FileMode.Open,
                FileAccess.Read, FileShare.Read, BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (input.Length != plan.AssetSize)
                throw new InvalidDataException("Dimensione archivio locale divergente.");
            await using var output = new FileStream(destination, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[BufferSize];
            long total = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(), timeout.Token)
                    .ConfigureAwait(false);
                if (read == 0) break;
                total = checked(total + read);
                if (total > plan.AssetSize)
                    throw new InvalidDataException("Archivio locale oltre quota.");
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token)
                    .ConfigureAwait(false);
                progress?.Report(new DdfProductUpdateStagingProgress(total,
                    plan.AssetSize));
            }
            await output.FlushAsync(timeout.Token).ConfigureAwait(false);
            output.Flush(true);
            if (total != plan.AssetSize ||
                !string.Equals(Convert.ToHexString(hash.GetHashAndReset())
                    .ToLowerInvariant(), plan.AssetSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Archivio locale incompleto o SHA-256 non valido.");
        }

        private static bool IsInside(string path, string directory)
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(directory));
            return path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private async Task DownloadAsync(DdfProductUpdateInstallationPlan plan,
            string destination, IProgress<DdfProductUpdateStagingProgress> progress,
            CancellationToken cancellationToken)
        {
            if (plan.AssetSize > options.MaximumAssetBytes)
                throw new InvalidDataException("Asset oltre il limite configurato.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(options.Timeout);
            using HttpResponseMessage first = await SendDownloadRequestAsync(
                plan.AssetUri, timeout.Token).ConfigureAwait(false);
            HttpResponseMessage response = first;
            HttpResponseMessage redirected = null;
            try
            {
                if (first.RequestMessage?.RequestUri == null ||
                    !Uri.Equals(first.RequestMessage.RequestUri, plan.AssetUri))
                    throw new HttpRequestException("Redirect dell'asset non autorizzato.");
                Uri expectedUri = plan.AssetUri;
                if (IsRedirect(first.StatusCode))
                {
                    Uri location = first.Headers.Location;
                    if (location != null && !location.IsAbsoluteUri)
                        location = new Uri(plan.AssetUri, location);
                    ValidateRedirectUri(location);
                    expectedUri = location;
                    redirected = await SendDownloadRequestAsync(location,
                        timeout.Token).ConfigureAwait(false);
                    response = redirected;
                    if (IsRedirect(response.StatusCode))
                        throw new HttpRequestException(
                            "Catena di redirect dell'asset non ammessa.");
                }
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new HttpRequestException("Risposta download non valida: " +
                        (int)response.StatusCode + ".");
                if (response.RequestMessage?.RequestUri == null ||
                    !Uri.Equals(response.RequestMessage.RequestUri, expectedUri))
                    throw new HttpRequestException("Redirect dell'asset non autorizzato.");
                if (response.Content.Headers.ContentEncoding.Count != 0)
                    throw new InvalidDataException("Content-Encoding non ammesso.");
                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength.Value != plan.AssetSize)
                    throw new InvalidDataException("Dimensione HTTP divergente dal piano.");

                await using Stream input = await response.Content.ReadAsStreamAsync(
                    timeout.Token).ConfigureAwait(false);
                await using var output = new FileStream(destination, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous |
                    FileOptions.WriteThrough);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[BufferSize];
                long total = 0;
                while (true)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length),
                        timeout.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    total = checked(total + read);
                    if (total > plan.AssetSize || total > options.MaximumAssetBytes)
                        throw new InvalidDataException("Asset oltre la dimensione attesa.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token)
                        .ConfigureAwait(false);
                    progress?.Report(new DdfProductUpdateStagingProgress(total,
                        plan.AssetSize));
                }
                await output.FlushAsync(timeout.Token).ConfigureAwait(false);
                output.Flush(true);
                if (total != plan.AssetSize)
                    throw new InvalidDataException("Asset tronco.");
                string actualHash = Convert.ToHexString(hash.GetHashAndReset())
                    .ToLowerInvariant();
                if (!string.Equals(actualHash, plan.AssetSha256,
                        StringComparison.Ordinal))
                    throw new InvalidDataException("SHA-256 asset non valido.");
            }
            finally
            {
                redirected?.Dispose();
            }
        }

        private async Task<HttpResponseMessage> SendDownloadRequestAsync(Uri uri,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.AcceptEncoding.Clear();
            return await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }

        private static void ValidateRedirectUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !AllowedRedirectHosts.Contains(uri.Host) ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new HttpRequestException(
                    "Redirect dell'asset non autorizzato.");
        }

        private static bool IsRedirect(HttpStatusCode status) =>
            status == HttpStatusCode.MovedPermanently ||
            status == HttpStatusCode.Redirect ||
            status == HttpStatusCode.RedirectMethod ||
            status == HttpStatusCode.TemporaryRedirect ||
            (int)status == 308;

        private static ExtractionSummary ExtractAndVerify(
            DdfProductUpdateInstallationPlan plan, string archivePath,
            string destination, long maximumExtractedBytes,
            CancellationToken cancellationToken)
        {
            var expected = plan.Inventory.ToDictionary(file => file.RelativePath,
                StringComparer.OrdinalIgnoreCase);
            var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                string raw = directory
                    ? entry.FullName.TrimEnd('/', '\\')
                    : entry.FullName;
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
                RejectLink(entry);
                if (directory)
                {
                    Directory.CreateDirectory(ResolveInside(destination, relative));
                    continue;
                }
                if (!extracted.Add(relative) || !expected.TryGetValue(relative,
                        out DdfProductUpdateInstallationFile expectedFile))
                    throw new InvalidDataException(
                        "L'archivio contiene file duplicati o non inventariati.");
                if (entry.Length != expectedFile.Size)
                    throw new InvalidDataException(
                        "Dimensione file divergente dall'inventario.");

                string outputPath = ResolveInside(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                using Stream input = entry.Open();
                using var output = new FileStream(outputPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, BufferSize,
                    FileOptions.WriteThrough);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[BufferSize];
                long written = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = input.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    written = checked(written + read);
                    if (written > expectedFile.Size)
                        throw new InvalidDataException("File estratto oltre quota.");
                    hash.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                }
                output.Flush(true);
                string actualHash = Convert.ToHexString(hash.GetHashAndReset())
                    .ToLowerInvariant();
                if (written != expectedFile.Size ||
                    !string.Equals(actualHash, expectedFile.Sha256,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Contenuto estratto divergente dall'inventario.");
                total = checked(total + written);
                if (total > maximumExtractedBytes)
                    throw new InvalidDataException(
                        "Contenuto estratto oltre il limite configurato.");
            }
            if (extracted.Count != expected.Count ||
                expected.Keys.Any(path => !extracted.Contains(path)))
                throw new InvalidDataException("Inventario estratto incompleto.");
            return new ExtractionSummary(extracted.Count, total);
        }

        private static void VerifyManifest(DdfProductUpdateInstallationPlan plan,
            string json, DdfReleaseTrustStore trustStore)
        {
            string manifestHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json ?? string.Empty))).ToLowerInvariant();
            if (!string.Equals(manifestHash, plan.ManifestSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Manifesto divergente dal piano.");
            DdfReleaseManifestVerification verification =
                DdfReleaseManifestVerifier.Verify(json, trustStore);
            if (!verification.Success)
                throw new InvalidDataException("Manifesto release non attendibile.");
            DdfReleaseManifest manifest = verification.Manifest;
            if (manifest.Signature == null ||
                !string.Equals(manifest.Signature.KeyId, plan.SigningKeyId,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.Version, plan.TargetVersion,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Manifesto non correlato al piano.");
            DdfReleaseManifestAsset asset = manifest.Assets.SingleOrDefault(item =>
                item.ComponentKind == DdfProductUpdateComponentKind.Standalone &&
                string.Equals(item.ComponentId, plan.ComponentId,
                    StringComparison.Ordinal) &&
                string.Equals(item.RuntimeIdentifier, plan.RuntimeIdentifier,
                    StringComparison.Ordinal) &&
                string.Equals(item.Architecture, plan.Architecture,
                    StringComparison.Ordinal));
            if (asset == null || !Uri.Equals(asset.Uri, plan.AssetUri) ||
                asset.Size != plan.AssetSize ||
                !string.Equals(asset.Sha256, plan.AssetSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Asset del piano non confermato.");
        }

        private static void ValidateDestination(string parent, string staging,
            string extracting, string partial, string seal)
        {
            EnsureExistingAncestorsAreRegular(parent);
            if (Directory.Exists(staging) || File.Exists(staging) ||
                Directory.Exists(extracting) || File.Exists(extracting) ||
                Directory.Exists(partial) || File.Exists(partial) ||
                Directory.Exists(seal) || File.Exists(seal))
                throw new IOException("Lo staging contiene residui non autorizzati.");
            Directory.CreateDirectory(parent);
        }

        private static void EnsureExistingAncestorsAreRegular(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "La radice staging attraversa un reparse point.");
                string parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    break;
                current = parent;
            }
        }

        private static string ResolveInside(string root, string relative)
        {
            string fullRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));
            string full = Path.GetFullPath(Path.Combine(fullRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Percorso estratto fuori staging.");
            return full;
        }

        private static void RejectLink(ZipArchiveEntry entry)
        {
            int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            int windowsAttributes = entry.ExternalAttributes & 0xFFFF;
            if (unixType == 0xA000 ||
                (windowsAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    "Link e reparse point non sono ammessi nell'archivio.");
        }

        private static void TryTransition(
            DdfProductUpdateInstallationJournalStore store,
            DdfProductUpdateInstallationState state, string detail)
        {
            try
            {
                DdfProductUpdateInstallationJournal current = store.Read();
                if (DdfProductUpdateInstallationTransitions.CanTransition(
                    current.State, state))
                {
                    DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                    if (nowUtc < current.UpdatedUtc) nowUtc = current.UpdatedUtc;
                    store.Transition(current.Revision, state, nowUtc, detail);
                }
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException) { }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class ExtractionSummary
        {
            public ExtractionSummary(int fileCount, long totalBytes)
            {
                FileCount = fileCount;
                TotalBytes = totalBytes;
            }

            public int FileCount { get; }
            public long TotalBytes { get; }
        }
    }
}
