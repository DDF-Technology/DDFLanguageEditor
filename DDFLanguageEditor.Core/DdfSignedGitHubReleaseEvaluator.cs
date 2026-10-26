using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSignedGitHubReleaseEvaluatorOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public int MaximumManifestBytes { get; set; } = 1024 * 1024;

        internal DdfSignedGitHubReleaseEvaluatorOptions CopyAndValidate()
        {
            if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(2))
                throw new ArgumentOutOfRangeException(nameof(Timeout));
            if (MaximumManifestBytes < 1024 ||
                MaximumManifestBytes > 4 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(MaximumManifestBytes));
            return new DdfSignedGitHubReleaseEvaluatorOptions
            {
                Timeout = Timeout,
                MaximumManifestBytes = MaximumManifestBytes
            };
        }
    }

    public sealed class DdfSignedGitHubReleaseEvaluator :
        IDdfAsyncProductReleaseEvaluator, IDisposable
    {
        public const string ManifestAssetName = "ddf-release-manifest.json";
        private static readonly HashSet<string> AllowedRedirectHosts = new(
            new[]
            {
                "release-assets.githubusercontent.com",
                "objects.githubusercontent.com",
                "github-releases.githubusercontent.com"
            }, StringComparer.OrdinalIgnoreCase);

        private readonly DdfReleaseTrustStore trustStore;
        private readonly DdfSignedGitHubReleaseEvaluatorOptions options;
        private readonly HttpClient client;
        private readonly string runtimeIdentifier;
        private readonly string architecture;
        private bool disposed;

        public DdfSignedGitHubReleaseEvaluator(DdfReleaseTrustStore trustStore,
            DdfSignedGitHubReleaseEvaluatorOptions options = null)
            : this(trustStore, CreateDefaultHandler(), options,
                RuntimeInformation.RuntimeIdentifier,
                RuntimeInformation.ProcessArchitecture.ToString()
                    .ToLowerInvariant())
        {
        }

        public DdfSignedGitHubReleaseEvaluator(DdfReleaseTrustStore trustStore,
            HttpMessageHandler handler,
            DdfSignedGitHubReleaseEvaluatorOptions options = null,
            string runtimeIdentifier = "win-x64", string architecture = "x64")
        {
            this.trustStore = trustStore ?? throw new ArgumentNullException(
                nameof(trustStore));
            this.options = (options ??
                new DdfSignedGitHubReleaseEvaluatorOptions()).CopyAndValidate();
            this.runtimeIdentifier = RequireToken(runtimeIdentifier,
                nameof(runtimeIdentifier));
            this.architecture = RequireToken(architecture, nameof(architecture));
            client = new HttpClient(handler ?? throw new ArgumentNullException(
                nameof(handler)), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task<DdfProductUpdateResult> EvaluateAsync(
            DdfProductUpdateRequest request,
            IReadOnlyList<DdfRemoteRelease> releases,
            DateTimeOffset checkedAtUtc,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (releases == null) throw new ArgumentNullException(nameof(releases));
            ObjectDisposedException.ThrowIf(disposed, this);

            Candidate candidate = releases.Select(TryCreateCandidate)
                .Where(item => item != null &&
                    DdfSemanticVersionOrder.Compare(item.Version,
                        request.CurrentVersion) > 0 &&
                    (request.Channel != DdfProductUpdateChannel.Stable ||
                     (!item.Release.Prerelease &&
                      !DdfSemanticVersionOrder.IsPrerelease(item.Version))))
                .OrderByDescending(item => item.Version,
                    SemanticVersionComparer.Instance)
                .FirstOrDefault();
            if (candidate == null)
                return DdfProductUpdateResult.UpToDate(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion,
                    request.CurrentVersion, request.Channel, checkedAtUtc);

            DdfRemoteReleaseAsset[] manifests = candidate.Release.Assets.Where(
                item => string.Equals(item.Name, ManifestAssetName,
                    StringComparison.Ordinal)).ToArray();
            if (manifests.Length != 1)
                return Unknown(request, checkedAtUtc,
                    manifests.Length == 0
                        ? "Manifesto firmato assente dalla release più recente."
                        : "Manifesto firmato ambiguo nella release più recente.");

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeout.CancelAfter(options.Timeout);
                string json = await DownloadManifestAsync(manifests[0].DownloadUri,
                    timeout.Token).ConfigureAwait(false);
                DdfReleaseManifestVerification verification =
                    DdfReleaseManifestVerifier.Verify(json, trustStore);
                if (!verification.Success)
                    return Unknown(request, checkedAtUtc,
                        verification.Issues.Count == 0
                            ? "Manifesto release non verificato."
                            : string.Join(" ", verification.Issues));
                DdfReleaseManifest manifest = verification.Manifest;
                if (!string.Equals(manifest.Version, candidate.Version,
                        StringComparison.Ordinal))
                    return Unknown(request, checkedAtUtc,
                        "Versione del manifesto non correlata al tag GitHub.");
                foreach (DdfReleaseManifestAsset asset in manifest.Assets)
                {
                    int matches = candidate.Release.Assets.Count(remote =>
                        remote.Size == asset.Size &&
                        remote.DownloadUri == asset.Uri);
                    if (matches != 1)
                        return Unknown(request, checkedAtUtc,
                            "Inventario firmato non correlato agli asset GitHub.");
                }
                return DdfReleaseUpdatePolicy.Evaluate(verification, request,
                    runtimeIdentifier, architecture, checkedAtUtc);
            }
            catch (OperationCanceledException) when
                (!cancellationToken.IsCancellationRequested)
            {
                return Unknown(request, checkedAtUtc,
                    "Timeout durante la verifica del manifesto firmato.");
            }
            catch (Exception exception) when (exception is HttpRequestException ||
                exception is IOException || exception is InvalidDataException ||
                exception is InvalidOperationException)
            {
                return Unknown(request, checkedAtUtc,
                    "Verifica del manifesto non riuscita: " + exception.Message);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            client.Dispose();
        }

        private async Task<string> DownloadManifestAsync(Uri uri,
            CancellationToken cancellationToken)
        {
            ValidateInitialUri(uri);
            using HttpResponseMessage first = await SendAsync(uri,
                cancellationToken).ConfigureAwait(false);
            HttpResponseMessage response = first;
            HttpResponseMessage redirected = null;
            try
            {
                if (IsRedirect(first.StatusCode))
                {
                    Uri location = first.Headers.Location;
                    if (location != null && !location.IsAbsoluteUri)
                        location = new Uri(uri, location);
                    ValidateRedirectUri(location);
                    redirected = await SendAsync(location, cancellationToken)
                        .ConfigureAwait(false);
                    response = redirected;
                    if (IsRedirect(response.StatusCode))
                        throw new InvalidDataException(
                            "Catena di redirect del manifesto non ammessa.");
                }
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new HttpRequestException(
                        "HTTP manifesto inatteso: " +
                        (int)response.StatusCode + ".");
                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength.Value >
                        options.MaximumManifestBytes)
                    throw new InvalidDataException(
                        "Manifesto oltre il limite consentito.");
                byte[] bytes = await ReadLimitedAsync(response.Content,
                    options.MaximumManifestBytes, cancellationToken)
                    .ConfigureAwait(false);
                return new System.Text.UTF8Encoding(false, true).GetString(bytes);
            }
            finally
            {
                redirected?.Dispose();
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Uri uri,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(
                "DDFLanguageEditor-SignedManifest/1.0");
            request.Headers.Accept.ParseAdd("application/json");
            try
            {
                return await client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                request.Dispose();
            }
        }

        private static Candidate TryCreateCandidate(DdfRemoteRelease release)
        {
            if (release == null || string.IsNullOrWhiteSpace(release.Tag) ||
                release.Tag[0] != 'v') return null;
            string version = release.Tag.Substring(1);
            try
            {
                DdfSemanticVersionOrder.Compare(version, version);
                return new Candidate(release, version);
            }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
            catch (OverflowException) { return null; }
        }

        private static void ValidateInitialUri(Uri uri)
        {
            string prefix = "/" + DdfGitHubReleaseProvider.Repository +
                "/releases/download/";
            if (uri == null || !uri.IsAbsoluteUri ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) ||
                !uri.AbsolutePath.EndsWith("/" + ManifestAssetName,
                    StringComparison.Ordinal) || uri.UserInfo.Length != 0 ||
                uri.Fragment.Length != 0)
                throw new InvalidDataException(
                    "URI del manifesto release non autorizzata.");
        }

        private static void ValidateRedirectUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !AllowedRedirectHosts.Contains(uri.Host) ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new InvalidDataException(
                    "Redirect del manifesto release non autorizzato.");
        }

        private static bool IsRedirect(HttpStatusCode status) =>
            status == HttpStatusCode.MovedPermanently ||
            status == HttpStatusCode.Redirect ||
            status == HttpStatusCode.RedirectMethod ||
            status == HttpStatusCode.TemporaryRedirect ||
            (int)status == 308;

        private static async Task<byte[]> ReadLimitedAsync(HttpContent content,
            int maximumBytes, CancellationToken cancellationToken)
        {
            await using Stream input = await content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[16 * 1024];
            while (true)
            {
                int remaining = maximumBytes + 1 - (int)output.Length;
                int read = await input.ReadAsync(buffer.AsMemory(0,
                    Math.Min(buffer.Length, remaining)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                if (output.Length > maximumBytes)
                    throw new InvalidDataException(
                        "Manifesto oltre il limite consentito.");
            }
            return output.ToArray();
        }

        private static HttpMessageHandler CreateDefaultHandler() =>
            new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = true
            };

        private static string RequireToken(string value, string name)
        {
            string token = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (token.Length == 0) throw new ArgumentException(
                "Token piattaforma richiesto.", name);
            return token;
        }

        private static DdfProductUpdateResult Unknown(
            DdfProductUpdateRequest request, DateTimeOffset checkedAtUtc,
            string detail) => DdfProductUpdateResult.Unknown(
                request.ComponentKind, request.ComponentId,
                request.CurrentVersion, request.Channel, checkedAtUtc, detail);

        private sealed class Candidate
        {
            public Candidate(DdfRemoteRelease release, string version)
            {
                Release = release;
                Version = version;
            }
            public DdfRemoteRelease Release { get; }
            public string Version { get; }
        }

        private sealed class SemanticVersionComparer : IComparer<string>
        {
            public static readonly SemanticVersionComparer Instance = new();
            public int Compare(string left, string right) =>
                DdfSemanticVersionOrder.Compare(left, right);
        }
    }
}
