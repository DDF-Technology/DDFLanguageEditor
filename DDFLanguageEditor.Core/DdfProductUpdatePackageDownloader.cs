using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfProductUpdatePreparedPackage
    {
        internal DdfProductUpdatePreparedPackage(string archivePath,
            string signedManifestJson,
            IReadOnlyList<DdfProductUpdateInstallationFile> inventory)
        {
            ArchivePath = archivePath;
            SignedManifestJson = signedManifestJson;
            Inventory = new ReadOnlyCollection<DdfProductUpdateInstallationFile>(
                new List<DdfProductUpdateInstallationFile>(inventory));
        }

        public string ArchivePath { get; }
        public string SignedManifestJson { get; }
        public IReadOnlyList<DdfProductUpdateInstallationFile> Inventory { get; }
    }

    public sealed class DdfProductUpdateVerifiedOffer
    {
        internal DdfProductUpdateVerifiedOffer(DdfReleaseManifestAsset asset,
            string signedManifestJson)
        {
            Asset = asset;
            SignedManifestJson = signedManifestJson;
        }

        internal DdfReleaseManifestAsset Asset { get; }
        internal string SignedManifestJson { get; }
        public string Version => Asset.Version;
        public Uri AssetUri => Asset.Uri;
        public long AssetSize => Asset.Size;
    }

    /// <summary>
    /// Downloads the signed manifest and matching ZIP only after the caller
    /// has obtained explicit download consent. It does not install anything.
    /// </summary>
    public sealed class DdfProductUpdatePackageDownloader : IDisposable
    {
        private const int MaximumManifestBytes = 1024 * 1024;
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
        private readonly DdfReleaseTrustStore trustStore;
        private bool disposed;

        public DdfProductUpdatePackageDownloader(DdfReleaseTrustStore trustStore,
            HttpMessageHandler handler = null,
            DdfProductUpdateStagingOptions options = null)
        {
            this.trustStore = trustStore ?? throw new ArgumentNullException(
                nameof(trustStore));
            this.options = options ?? DdfProductUpdateStagingOptions.Default;
            handler ??= new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            };
            client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task<DdfProductUpdateVerifiedOffer> InspectOfferAsync(
            DdfProductUpdateResult checkedResult,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Uri assetUri = ValidateCheckedResult(checkedResult, request);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(options.Timeout);
            Uri manifestUri = new Uri(assetUri,
                DdfSignedGitHubReleaseEvaluator.ManifestAssetName);
            string manifestJson;
            using (HttpResponseMessage response = await GetResponseAsync(
                manifestUri, timeout.Token).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength > MaximumManifestBytes)
                    throw new InvalidDataException("Manifesto oltre quota.");
                await using Stream input = await response.Content
                    .ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                byte[] chunk = new byte[16 * 1024];
                while (true)
                {
                    int read = await input.ReadAsync(chunk.AsMemory(),
                        timeout.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    if (buffer.Length + read > MaximumManifestBytes)
                        throw new InvalidDataException("Manifesto oltre quota.");
                    buffer.Write(chunk, 0, read);
                }
                manifestJson = new UTF8Encoding(false, true).GetString(
                    buffer.ToArray());
            }
            DdfReleaseManifestVerification verification =
                DdfReleaseManifestVerifier.Verify(manifestJson, trustStore);
            DdfProductUpdateResult verified = DdfReleaseUpdatePolicy.Evaluate(
                verification, request, runtimeIdentifier, architecture,
                DateTimeOffset.UtcNow);
            if (!verified.IsActionable ||
                verified.AvailableVersion != checkedResult.AvailableVersion ||
                !Uri.Equals(verified.VerifiedReleaseUri, assetUri))
                throw new InvalidDataException(
                    "Manifesto firmato divergente dal controllo precedente.");
            DdfReleaseManifestAsset[] matches = verification.Manifest.Assets
                .Where(item => item.ComponentKind == request.ComponentKind &&
                    item.ComponentId == request.ComponentId &&
                    Uri.Equals(item.Uri, assetUri)).ToArray();
            if (matches.Length != 1 || matches[0].Size > options.MaximumAssetBytes)
                throw new InvalidDataException("Asset firmato ambiguo o oltre quota.");
            return new DdfProductUpdateVerifiedOffer(matches[0], manifestJson);
        }

        public async Task<DdfProductUpdatePreparedPackage> DownloadAuthorizedAsync(
            DdfProductUpdateResult checkedResult,
            DdfProductUpdateRequest request, string runtimeIdentifier,
            string architecture, string destinationDirectory,
            CancellationToken cancellationToken = default,
            IProgress<DdfProductUpdateStagingProgress> progress = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Uri assetUri = ValidateCheckedResult(checkedResult, request);
            if (string.IsNullOrWhiteSpace(destinationDirectory) ||
                !Path.IsPathFullyQualified(destinationDirectory))
                throw new ArgumentException("Directory assoluta richiesta.",
                    nameof(destinationDirectory));
            string destination = Path.GetFullPath(destinationDirectory);
            string partial = Path.Combine(destination,
                Guid.NewGuid().ToString("N") + ".partial");
            string archivePath = Path.ChangeExtension(partial, ".zip");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(options.Timeout);
            try
            {
                DdfProductUpdateVerifiedOffer offer = await InspectOfferAsync(
                    checkedResult, request, runtimeIdentifier, architecture,
                    timeout.Token).ConfigureAwait(false);
                string manifestJson = offer.SignedManifestJson;
                DdfReleaseManifestAsset asset = offer.Asset;

                ValidateDestination(destination);
                Directory.CreateDirectory(destination);
                using (HttpResponseMessage response = await GetResponseAsync(
                    assetUri, timeout.Token).ConfigureAwait(false))
                {
                    if (response.Content.Headers.ContentLength.HasValue &&
                        response.Content.Headers.ContentLength.Value != asset.Size)
                        throw new InvalidDataException("Dimensione HTTP divergente.");
                    await using Stream input = await response.Content
                        .ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    await using var output = new FileStream(partial,
                        FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        BufferSize, FileOptions.Asynchronous |
                        FileOptions.WriteThrough);
                    using var hash = IncrementalHash.CreateHash(
                        HashAlgorithmName.SHA256);
                    byte[] chunk = new byte[BufferSize];
                    long received = 0;
                    while (true)
                    {
                        int read = await input.ReadAsync(chunk.AsMemory(),
                            timeout.Token).ConfigureAwait(false);
                        if (read == 0) break;
                        received = checked(received + read);
                        if (received > asset.Size)
                            throw new InvalidDataException("Asset oltre quota.");
                        hash.AppendData(chunk, 0, read);
                        await output.WriteAsync(chunk.AsMemory(0, read),
                            timeout.Token).ConfigureAwait(false);
                        progress?.Report(new DdfProductUpdateStagingProgress(
                            received, asset.Size));
                    }
                    await output.FlushAsync(timeout.Token).ConfigureAwait(false);
                    output.Flush(true);
                    if (received != asset.Size ||
                        !string.Equals(Convert.ToHexString(hash.GetHashAndReset())
                            .ToLowerInvariant(), asset.Sha256,
                            StringComparison.Ordinal))
                        throw new InvalidDataException("Asset incompleto o alterato.");
                }
                File.Move(partial, archivePath);
                IReadOnlyList<DdfProductUpdateInstallationFile> inventory =
                    DdfProductUpdateArchiveInspector.Inspect(archivePath,
                        manifestJson, trustStore, request, runtimeIdentifier,
                        architecture, options, timeout.Token);
                return new DdfProductUpdatePreparedPackage(archivePath,
                    manifestJson, inventory);
            }
            catch
            {
                if (File.Exists(partial)) File.Delete(partial);
                if (File.Exists(archivePath)) File.Delete(archivePath);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            client.Dispose();
        }

        private static Uri ValidateCheckedResult(
            DdfProductUpdateResult checkedResult,
            DdfProductUpdateRequest request)
        {
            if (checkedResult == null) throw new ArgumentNullException(
                nameof(checkedResult));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!checkedResult.IsActionable ||
                checkedResult.ComponentKind != DdfProductUpdateComponentKind.Standalone ||
                checkedResult.ComponentKind != request.ComponentKind ||
                checkedResult.ComponentId != request.ComponentId ||
                checkedResult.CurrentVersion != request.CurrentVersion ||
                checkedResult.Channel != request.Channel)
                throw new InvalidDataException(
                    "Controllo aggiornamenti precedente non azionabile.");
            Uri assetUri = checkedResult.VerifiedReleaseUri;
            string prefix = "/" + DdfGitHubReleaseProvider.Repository +
                "/releases/download/v" + checkedResult.AvailableVersion + "/";
            if (assetUri == null || assetUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(assetUri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !assetUri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) ||
                !assetUri.AbsolutePath.EndsWith(".zip", StringComparison.Ordinal) ||
                assetUri.UserInfo.Length != 0 || assetUri.Query.Length != 0 ||
                assetUri.Fragment.Length != 0)
                throw new InvalidDataException("URI asset non autorizzata.");
            return assetUri;
        }

        private async Task<HttpResponseMessage> GetResponseAsync(Uri uri,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage first = await SendAsync(uri, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (first.RequestMessage?.RequestUri == null ||
                    !Uri.Equals(first.RequestMessage.RequestUri, uri))
                    throw new HttpRequestException("Redirect non autorizzato.");
                if (IsRedirect(first.StatusCode))
                {
                    Uri location = first.Headers.Location;
                    if (location != null && !location.IsAbsoluteUri)
                        location = new Uri(uri, location);
                    if (location == null || location.Scheme != Uri.UriSchemeHttps ||
                        !AllowedRedirectHosts.Contains(location.Host) ||
                        location.UserInfo.Length != 0 || location.Fragment.Length != 0)
                        throw new HttpRequestException("Redirect non autorizzato.");
                    using (first)
                    {
                        HttpResponseMessage second = await SendAsync(location,
                            cancellationToken).ConfigureAwait(false);
                        if (IsRedirect(second.StatusCode) ||
                            second.RequestMessage?.RequestUri == null ||
                            !Uri.Equals(second.RequestMessage.RequestUri, location))
                        {
                            second.Dispose();
                            throw new HttpRequestException(
                                "Catena di redirect non autorizzata.");
                        }
                        if (second.StatusCode != HttpStatusCode.OK ||
                            second.Content.Headers.ContentEncoding.Count != 0)
                        {
                            second.Dispose();
                            throw new HttpRequestException("Risposta HTTP non valida.");
                        }
                        return second;
                    }
                }
                if (first.StatusCode != HttpStatusCode.OK ||
                    first.Content.Headers.ContentEncoding.Count != 0)
                    throw new HttpRequestException("Risposta HTTP non valida.");
                return first;
            }
            catch
            {
                first.Dispose();
                throw;
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Uri uri,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.AcceptEncoding.Clear();
            request.Headers.UserAgent.ParseAdd("DDFLanguageEditor-Update/1.0");
            return await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }

        private static void ValidateDestination(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current);
                current = Path.GetDirectoryName(current))
            {
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "La destinazione attraversa un reparse point.");
                if (Path.GetDirectoryName(current) == current) break;
            }
        }

        private static bool IsRedirect(HttpStatusCode status) =>
            status == HttpStatusCode.MovedPermanently ||
            status == HttpStatusCode.Redirect ||
            status == HttpStatusCode.RedirectMethod ||
            status == HttpStatusCode.TemporaryRedirect ||
            (int)status == 308;
    }
}
