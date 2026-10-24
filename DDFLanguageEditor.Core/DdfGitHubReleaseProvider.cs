using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfProductUpdateRequest
    {
        public DdfProductUpdateRequest(DdfProductUpdateComponentKind componentKind,
            string componentId, string currentVersion,
            DdfProductUpdateChannel channel)
        {
            ComponentKind = componentKind;
            ComponentId = DdfProductUpdateResult.RequireComponentId(componentId);
            CurrentVersion = DdfProductUpdateResult.RequireVersion(currentVersion,
                nameof(currentVersion));
            Channel = channel;
        }

        public DdfProductUpdateComponentKind ComponentKind { get; }
        public string ComponentId { get; }
        public string CurrentVersion { get; }
        public DdfProductUpdateChannel Channel { get; }

        public static DdfProductUpdateRequest ForStandalone(
            DdfProductUpdateChannel? channel = null)
        {
            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            return new DdfProductUpdateRequest(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                identity.CurrentVersion, channel ??
                    (identity.DefaultChannel == "stable"
                        ? DdfProductUpdateChannel.Stable
                        : DdfProductUpdateChannel.Beta));
        }
    }

    public sealed class DdfRemoteReleaseAsset
    {
        internal DdfRemoteReleaseAsset(string name, Uri downloadUri,
            long size, string contentType)
        {
            Name = name;
            DownloadUri = downloadUri;
            Size = size;
            ContentType = contentType;
        }

        public string Name { get; }
        public Uri DownloadUri { get; }
        public long Size { get; }
        public string ContentType { get; }
    }

    public sealed class DdfRemoteRelease
    {
        internal DdfRemoteRelease(string tag, string name, string notes,
            DateTimeOffset? publishedAtUtc, Uri releaseUri, bool prerelease,
            IEnumerable<DdfRemoteReleaseAsset> assets)
        {
            Tag = tag;
            Name = name;
            Notes = notes;
            PublishedAtUtc = publishedAtUtc?.ToUniversalTime();
            ReleaseUri = releaseUri;
            Prerelease = prerelease;
            Assets = new ReadOnlyCollection<DdfRemoteReleaseAsset>(
                assets.ToList());
        }

        public string Tag { get; }
        public string Name { get; }
        public string Notes { get; }
        public DateTimeOffset? PublishedAtUtc { get; }
        public Uri ReleaseUri { get; }
        public bool Prerelease { get; }
        public IReadOnlyList<DdfRemoteReleaseAsset> Assets { get; }
    }

    public interface IDdfProductReleaseEvaluator
    {
        DdfProductUpdateResult Evaluate(DdfProductUpdateRequest request,
            IReadOnlyList<DdfRemoteRelease> releases,
            DateTimeOffset checkedAtUtc);
    }

    public interface IDdfProductUpdateProvider
    {
        Task<DdfProductUpdateResult> CheckAsync(DdfProductUpdateRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class DdfGitHubReleaseProviderOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public int MaximumResponseBytes { get; set; } = 1024 * 1024;
        public int MaximumReleases { get; set; } = 50;
        public int MaximumAssetsPerRelease { get; set; } = 50;
        public int MaximumReleaseNotesCharacters { get; set; } = 64 * 1024;

        internal DdfGitHubReleaseProviderOptions CopyAndValidate()
        {
            if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(2))
                throw new ArgumentOutOfRangeException(nameof(Timeout));
            if (MaximumResponseBytes < 1024 || MaximumResponseBytes > 8 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(MaximumResponseBytes));
            if (MaximumReleases < 1 || MaximumReleases > 100)
                throw new ArgumentOutOfRangeException(nameof(MaximumReleases));
            if (MaximumAssetsPerRelease < 1 || MaximumAssetsPerRelease > 100)
                throw new ArgumentOutOfRangeException(nameof(MaximumAssetsPerRelease));
            if (MaximumReleaseNotesCharacters < 0 ||
                MaximumReleaseNotesCharacters > 256 * 1024)
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumReleaseNotesCharacters));
            return new DdfGitHubReleaseProviderOptions
            {
                Timeout = Timeout,
                MaximumResponseBytes = MaximumResponseBytes,
                MaximumReleases = MaximumReleases,
                MaximumAssetsPerRelease = MaximumAssetsPerRelease,
                MaximumReleaseNotesCharacters = MaximumReleaseNotesCharacters
            };
        }
    }

    /// <summary>
    /// Reads only the public release feed for the fixed official repository.
    /// It never uploads workspace, source, host data or diagnostics. A separate
    /// evaluator must verify release manifests before returning an actionable
    /// result.
    /// </summary>
    public sealed class DdfGitHubReleaseProvider : IDdfProductUpdateProvider,
        IDisposable
    {
        public const string Repository = "DDF-Technology/DDFLanguageEditor";
        public static readonly Uri ReleasesEndpoint = new Uri(
            "https://api.github.com/repos/" + Repository + "/releases");

        private readonly HttpClient client;
        private readonly IDdfProductReleaseEvaluator evaluator;
        private readonly DdfGitHubReleaseProviderOptions options;
        private readonly Func<DateTimeOffset> clock;
        private readonly SemaphoreSlim fetchGate = new SemaphoreSlim(1, 1);
        private byte[] cachedPayload;
        private string cachedEntityTag = string.Empty;
        private DateTimeOffset? cachedLastModified;
        private bool disposed;

        public DdfGitHubReleaseProvider(IDdfProductReleaseEvaluator evaluator,
            DdfGitHubReleaseProviderOptions options = null)
            : this(evaluator, CreateDefaultHandler(), options, null)
        {
        }

        public DdfGitHubReleaseProvider(IDdfProductReleaseEvaluator evaluator,
            HttpMessageHandler handler,
            DdfGitHubReleaseProviderOptions options = null,
            Func<DateTimeOffset> clock = null)
        {
            this.evaluator = evaluator ?? throw new ArgumentNullException(
                nameof(evaluator));
            this.options = (options ?? new DdfGitHubReleaseProviderOptions())
                .CopyAndValidate();
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
            client = new HttpClient(handler ?? throw new ArgumentNullException(
                nameof(handler)), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task<DdfProductUpdateResult> CheckAsync(
            DdfProductUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ObjectDisposedException.ThrowIf(disposed, this);
            await fetchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeout.CancelAfter(options.Timeout);
                byte[] payload;
                try
                {
                    payload = await FetchAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Unknown(request, "Timeout durante il controllo aggiornamenti.");
                }
                catch (HttpRequestException exception)
                {
                    return Unknown(request, "Servizio aggiornamenti non raggiungibile: " +
                        exception.Message);
                }
                catch (DdfUpdateProviderException exception)
                {
                    return Unknown(request, exception.Message);
                }

                IReadOnlyList<DdfRemoteRelease> releases;
                try
                {
                    releases = ParseReleases(payload);
                }
                catch (Exception exception) when (exception is JsonException ||
                    exception is InvalidDataException ||
                    exception is InvalidOperationException ||
                    exception is UriFormatException)
                {
                    return Unknown(request, "Risposta GitHub non valida: " +
                        exception.Message);
                }

                DdfProductUpdateResult result;
                try
                {
                    result = evaluator.Evaluate(request, releases,
                        clock().ToUniversalTime());
                }
                catch (Exception exception) when (exception is not
                    OperationCanceledException)
                {
                    return Unknown(request, "Valutazione release non riuscita: " +
                        exception.Message);
                }
                return MatchesRequest(result, request)
                    ? result
                    : Unknown(request,
                        "Il valutatore ha restituito un risultato incoerente.");
            }
            finally
            {
                fetchGate.Release();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            client.Dispose();
            fetchGate.Dispose();
        }

        private async Task<byte[]> FetchAsync(CancellationToken cancellationToken)
        {
            var endpoint = new UriBuilder(ReleasesEndpoint)
            {
                Query = "per_page=" + options.MaximumReleases
            }.Uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("DDFLanguageEditor-UpdateCheck/1.0");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version",
                "2022-11-28");
            if (cachedEntityTag.Length != 0)
                request.Headers.TryAddWithoutValidation("If-None-Match",
                    cachedEntityTag);
            if (cachedLastModified.HasValue)
                request.Headers.IfModifiedSince = cachedLastModified;

            using HttpResponseMessage response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            ValidateFinalEndpoint(response.RequestMessage?.RequestUri);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (cachedPayload == null)
                    throw new DdfUpdateProviderException(
                        "GitHub ha restituito 304 senza una cache disponibile.");
                return cachedPayload.ToArray();
            }
            if ((int)response.StatusCode == 429 ||
                response.StatusCode == HttpStatusCode.Forbidden)
                throw new DdfUpdateProviderException(
                    "Limite richieste GitHub raggiunto; nessun retry automatico.");
            if (!response.IsSuccessStatusCode)
                throw new DdfUpdateProviderException("GitHub ha restituito HTTP " +
                    (int)response.StatusCode + ".");
            if (response.Content == null)
                throw new DdfUpdateProviderException("Risposta GitHub vuota.");
            if (response.Content.Headers.ContentLength.HasValue &&
                response.Content.Headers.ContentLength.Value >
                    options.MaximumResponseBytes)
                throw new DdfUpdateProviderException(
                    "Risposta GitHub oltre il limite consentito.");

            byte[] payload = await ReadLimitedAsync(response.Content,
                options.MaximumResponseBytes, cancellationToken)
                .ConfigureAwait(false);
            cachedPayload = payload.ToArray();
            cachedEntityTag = response.Headers.ETag?.ToString() ?? string.Empty;
            cachedLastModified = response.Content.Headers.LastModified ??
                response.Headers.Date;
            return payload;
        }

        private IReadOnlyList<DdfRemoteRelease> ParseReleases(byte[] payload)
        {
            using JsonDocument document = JsonDocument.Parse(payload,
                new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Il feed release non è un array.");
            if (document.RootElement.GetArrayLength() > options.MaximumReleases)
                throw new InvalidDataException("Il feed contiene troppe release.");

            var releases = new List<DdfRemoteRelease>();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Release GitHub non valida.");
                if (ReadBoolean(item, "draft")) continue;
                string tag = ReadRequiredString(item, "tag_name", 128);
                string name = ReadOptionalString(item, "name", 256);
                string notes = ReadOptionalString(item, "body",
                    options.MaximumReleaseNotesCharacters);
                DateTimeOffset? published = ReadOptionalDate(item,
                    "published_at");
                Uri releaseUri = ReadReleaseUri(item, "html_url");
                bool prerelease = ReadBoolean(item, "prerelease");
                JsonElement assetsElement;
                var assets = new List<DdfRemoteReleaseAsset>();
                if (item.TryGetProperty("assets", out assetsElement) &&
                    assetsElement.ValueKind != JsonValueKind.Null)
                {
                    if (assetsElement.ValueKind != JsonValueKind.Array ||
                        assetsElement.GetArrayLength() >
                            options.MaximumAssetsPerRelease)
                        throw new InvalidDataException("Asset release non validi.");
                    foreach (JsonElement asset in assetsElement.EnumerateArray())
                    {
                        string assetName = ReadRequiredString(asset, "name", 256);
                        Uri downloadUri = ReadHttpsUri(asset,
                            "browser_download_url");
                        long size = ReadNonNegativeInt64(asset, "size");
                        string contentType = ReadOptionalString(asset,
                            "content_type", 256);
                        assets.Add(new DdfRemoteReleaseAsset(assetName,
                            downloadUri, size, contentType));
                    }
                }
                releases.Add(new DdfRemoteRelease(tag, name, notes, published,
                    releaseUri, prerelease, assets));
            }
            return new ReadOnlyCollection<DdfRemoteRelease>(releases);
        }

        private DdfProductUpdateResult Unknown(DdfProductUpdateRequest request,
            string detail) => DdfProductUpdateResult.Unknown(
                request.ComponentKind, request.ComponentId,
                request.CurrentVersion, request.Channel,
                clock().ToUniversalTime(), detail);

        private static bool MatchesRequest(DdfProductUpdateResult result,
            DdfProductUpdateRequest request) => result != null &&
            result.ComponentKind == request.ComponentKind &&
            string.Equals(result.ComponentId, request.ComponentId,
                StringComparison.Ordinal) &&
            string.Equals(result.CurrentVersion, request.CurrentVersion,
                StringComparison.Ordinal) && result.Channel == request.Channel;

        private static async Task<byte[]> ReadLimitedAsync(HttpContent content,
            int maximumBytes, CancellationToken cancellationToken)
        {
            await using Stream input = await content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[16 * 1024];
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0,
                    Math.Min(buffer.Length, maximumBytes + 1 -
                        (int)output.Length)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                if (output.Length > maximumBytes)
                    throw new DdfUpdateProviderException(
                        "Risposta GitHub oltre il limite consentito.");
            }
            return output.ToArray();
        }

        private static HttpMessageHandler CreateDefaultHandler() =>
            new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip |
                    DecompressionMethods.Deflate,
                UseProxy = true
            };

        private static void ValidateFinalEndpoint(Uri value)
        {
            if (value == null || value.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(value.Host, "api.github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !value.AbsolutePath.Equals(ReleasesEndpoint.AbsolutePath,
                    StringComparison.Ordinal))
                throw new DdfUpdateProviderException(
                    "Endpoint GitHub inatteso o redirect non autorizzato.");
        }

        private static string ReadRequiredString(JsonElement item, string name,
            int maximumLength)
        {
            string value = ReadOptionalString(item, name, maximumLength);
            if (value.Length == 0)
                throw new InvalidDataException("Campo release mancante: " + name);
            return value;
        }

        private static string ReadOptionalString(JsonElement item, string name,
            int maximumLength)
        {
            if (!item.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null) return string.Empty;
            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Campo release non testuale: " + name);
            string result = value.GetString() ?? string.Empty;
            if (result.Length > maximumLength)
                throw new InvalidDataException("Campo release oltre limite: " + name);
            return result;
        }

        private static bool ReadBoolean(JsonElement item, string name)
        {
            if (!item.TryGetProperty(name, out JsonElement value) ||
                (value.ValueKind != JsonValueKind.True &&
                 value.ValueKind != JsonValueKind.False))
                throw new InvalidDataException("Campo release booleano mancante: " + name);
            return value.GetBoolean();
        }

        private static long ReadNonNegativeInt64(JsonElement item, string name)
        {
            if (!item.TryGetProperty(name, out JsonElement value) ||
                !value.TryGetInt64(out long result) || result < 0)
                throw new InvalidDataException("Campo release numerico non valido: " + name);
            return result;
        }

        private static DateTimeOffset? ReadOptionalDate(JsonElement item,
            string name)
        {
            string text = ReadOptionalString(item, name, 64);
            if (text.Length == 0) return null;
            if (!DateTimeOffset.TryParse(text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset result))
                throw new InvalidDataException("Data release non valida: " + name);
            return result;
        }

        private static Uri ReadReleaseUri(JsonElement item, string name)
        {
            Uri uri = ReadHttpsUri(item, name);
            string expectedPrefix = "/" + Repository + "/releases/";
            if (!string.Equals(uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith(expectedPrefix,
                    StringComparison.Ordinal))
                throw new InvalidDataException("URI release fuori repository.");
            return uri;
        }

        private static Uri ReadHttpsUri(JsonElement item, string name)
        {
            string text = ReadRequiredString(item, name, 2048);
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
                throw new InvalidDataException("URI release non valida: " + name);
            return uri;
        }

        private sealed class DdfUpdateProviderException : Exception
        {
            public DdfUpdateProviderException(string message) : base(message) { }
        }
    }
}
