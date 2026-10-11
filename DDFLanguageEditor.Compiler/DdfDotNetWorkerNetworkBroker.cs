using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerNetworkGrant
    {
        public DdfDotNetWorkerNetworkGrant(string logicalName, Uri endpoint,
            IEnumerable<string> allowedMediaTypes = null,
            int maxResponseBytes = 1024 * 1024,
            TimeSpan? timeout = null, bool allowPrivateNetwork = false,
            bool allowInsecureHttp = false)
        {
            LogicalName = logicalName;
            Endpoint = endpoint;
            AllowedMediaTypes = (allowedMediaTypes ?? new[]
                {
                    "application/json", "text/plain"
                }).Select(value => value?.Trim().ToLowerInvariant()).ToArray();
            MaxResponseBytes = maxResponseBytes;
            Timeout = timeout ?? TimeSpan.FromSeconds(15);
            AllowPrivateNetwork = allowPrivateNetwork;
            AllowInsecureHttp = allowInsecureHttp;
        }

        public string LogicalName { get; }
        public Uri Endpoint { get; }
        public IReadOnlyList<string> AllowedMediaTypes { get; }
        public int MaxResponseBytes { get; }
        public TimeSpan Timeout { get; }
        public bool AllowPrivateNetwork { get; }
        public bool AllowInsecureHttp { get; }
    }

    public sealed class DdfDotNetWorkerNetworkReceipt
    {
        internal DdfDotNetWorkerNetworkReceipt(string logicalName,
            string scheme, int port, string hostSha256, int statusCode,
            string mediaType, int responseBytes, string responseSha256)
        {
            LogicalName = logicalName;
            Scheme = scheme;
            Port = port;
            HostSha256 = hostSha256;
            StatusCode = statusCode;
            MediaType = mediaType;
            ResponseBytes = responseBytes;
            ResponseSha256 = responseSha256;
        }

        public string LogicalName { get; }
        public string Scheme { get; }
        public int Port { get; }
        public string HostSha256 { get; }
        public int StatusCode { get; }
        public string MediaType { get; }
        public int ResponseBytes { get; }
        public string ResponseSha256 { get; }
    }

    public sealed class DdfDotNetWorkerNetworkAcquisitionResult
    {
        internal DdfDotNetWorkerNetworkAcquisitionResult(
            DdfDotNetWorkerNetworkSnapshot snapshot,
            DdfDotNetWorkerNetworkReceipt receipt,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Snapshot = snapshot;
            Receipt = receipt;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>()).ToList()
                .AsReadOnly();
        }

        public DdfDotNetWorkerNetworkSnapshot Snapshot { get; }
        public DdfDotNetWorkerNetworkReceipt Receipt { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Snapshot != null && Receipt != null &&
            Issues.Count == 0;
    }

    public sealed class DdfDotNetWorkerNetworkTransportResponse
    {
        public DdfDotNetWorkerNetworkTransportResponse(int statusCode,
            string mediaType, byte[] content)
        {
            StatusCode = statusCode;
            MediaType = mediaType ?? string.Empty;
            Content = (content ?? Array.Empty<byte>()).ToArray();
        }

        public int StatusCode { get; }
        public string MediaType { get; }
        public byte[] Content { get; }
    }

    public interface IDdfDotNetWorkerHostResolver
    {
        ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host,
            CancellationToken cancellationToken);
    }

    public interface IDdfDotNetWorkerNetworkTransport
    {
        ValueTask<DdfDotNetWorkerNetworkTransportResponse> SendAsync(
            Uri endpoint, IReadOnlyList<IPAddress> approvedAddresses,
            int maxResponseBytes, Action<HttpRequestMessage> authorize,
            CancellationToken cancellationToken);
    }

    public sealed class DdfDotNetWorkerNetworkBroker
    {
        private sealed class SystemResolver : IDdfDotNetWorkerHostResolver
        {
            public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
                string host, CancellationToken cancellationToken) =>
                await Dns.GetHostAddressesAsync(host, cancellationToken);
        }

        private sealed class PinnedHttpTransport :
            IDdfDotNetWorkerNetworkTransport
        {
            public async ValueTask<DdfDotNetWorkerNetworkTransportResponse>
                SendAsync(Uri endpoint,
                    IReadOnlyList<IPAddress> approvedAddresses,
                    int maxResponseBytes, Action<HttpRequestMessage> authorize,
                    CancellationToken cancellationToken)
            {
                int port = endpoint.IsDefaultPort
                    ? endpoint.Scheme == Uri.UriSchemeHttps ? 443 : 80
                    : endpoint.Port;
                var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseProxy = false,
                    Credentials = null,
                    ConnectCallback = async (_, token) =>
                    {
                        Exception last = null;
                        foreach (IPAddress address in approvedAddresses)
                        {
                            var socket = new Socket(address.AddressFamily,
                                SocketType.Stream, ProtocolType.Tcp)
                            {
                                NoDelay = true
                            };
                            try
                            {
                                await socket.ConnectAsync(new IPEndPoint(
                                    address, port), token);
                                return new NetworkStream(socket,
                                    ownsSocket: true);
                            }
                            catch (Exception exception) when (exception is
                                SocketException or IOException)
                            {
                                last = exception;
                                socket.Dispose();
                            }
                        }
                        throw new HttpRequestException(
                            "Nessun indirizzo IP autorizzato raggiungibile.",
                            last);
                    }
                };
                using (handler)
                using (var client = new HttpClient(handler, true)
                       {
                           Timeout = Timeout.InfiniteTimeSpan
                       })
                using (var request = new HttpRequestMessage(HttpMethod.Get,
                           endpoint))
                {
                    authorize?.Invoke(request);
                    if (request.Method != HttpMethod.Get ||
                        request.RequestUri != endpoint)
                        throw new InvalidOperationException(
                            "Il provider credenziali ha modificato metodo o " +
                            "destinazione del grant HTTP.");
                    using HttpResponseMessage response = await client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (response.Content.Headers.ContentLength is long length &&
                        length > maxResponseBytes)
                        throw new InvalidDataException(
                            "Risposta HTTP oltre la quota autorizzata.");
                    await using Stream stream = await response.Content
                        .ReadAsStreamAsync(cancellationToken);
                    using var buffer = new MemoryStream();
                    byte[] chunk = new byte[8192];
                    while (true)
                    {
                        int read = await stream.ReadAsync(chunk,
                            cancellationToken);
                        if (read == 0) break;
                        if (buffer.Length > maxResponseBytes - read)
                            throw new InvalidDataException(
                                "Risposta HTTP oltre la quota autorizzata.");
                        buffer.Write(chunk, 0, read);
                    }
                    return new DdfDotNetWorkerNetworkTransportResponse(
                        (int)response.StatusCode,
                        response.Content.Headers.ContentType?.MediaType,
                        buffer.ToArray());
                }
            }
        }

        private readonly IDdfDotNetWorkerHostResolver resolver;
        private readonly IDdfDotNetWorkerNetworkTransport transport;

        public DdfDotNetWorkerNetworkBroker() : this(new SystemResolver(),
            new PinnedHttpTransport()) { }

        public DdfDotNetWorkerNetworkBroker(
            IDdfDotNetWorkerHostResolver resolver,
            IDdfDotNetWorkerNetworkTransport transport)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(
                nameof(resolver));
            this.transport = transport ?? throw new ArgumentNullException(
                nameof(transport));
        }

        public async Task<DdfDotNetWorkerNetworkAcquisitionResult> AcquireAsync(
            DdfDotNetWorkerNetworkGrant grant,
            Action<HttpRequestMessage> authorize = null,
            CancellationToken cancellationToken = default)
        {
            DdfDotNetContractIssue validation = ValidateGrant(grant);
            if (validation != null) return Failure(validation.Message);
            using var deadline = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(grant.Timeout);
            try
            {
                IReadOnlyList<IPAddress> resolved = await resolver.ResolveAsync(
                    grant.Endpoint.IdnHost, deadline.Token);
                IPAddress[] addresses = (resolved ?? Array.Empty<IPAddress>())
                    .Distinct().ToArray();
                if (addresses.Length == 0 || addresses.Any(address =>
                        !grant.AllowPrivateNetwork && !IsPublic(address)))
                    return Failure("La risoluzione DNS contiene indirizzi " +
                        "assenti, privati, locali o non instradabili.");
                Action<HttpRequestMessage> guardedAuthorization = request =>
                {
                    authorize?.Invoke(request);
                    if (request.Method != HttpMethod.Get ||
                        request.RequestUri != grant.Endpoint)
                        throw new InvalidOperationException(
                            "Il provider credenziali ha modificato metodo o " +
                            "destinazione del grant HTTP.");
                };
                DdfDotNetWorkerNetworkTransportResponse response =
                    await transport.SendAsync(grant.Endpoint, addresses,
                        grant.MaxResponseBytes, guardedAuthorization,
                        deadline.Token);
                if (response == null || response.StatusCode < 200 ||
                    response.StatusCode > 299)
                    return Failure("La risposta HTTP non è un successo 2xx; " +
                        "redirect automatici non consentiti.");
                string mediaType = (response.MediaType ?? string.Empty).Trim()
                    .ToLowerInvariant();
                if (!grant.AllowedMediaTypes.Contains(mediaType,
                        StringComparer.Ordinal))
                    return Failure("Media type HTTP non autorizzato.");
                byte[] bytes = response.Content ?? Array.Empty<byte>();
                if (bytes.Length > grant.MaxResponseBytes ||
                    bytes.Length > 1024 * 1024)
                    return Failure("Risposta HTTP oltre la quota autorizzata.");
                string content = new UTF8Encoding(false, true)
                    .GetString(bytes);
                DdfDotNetWorkerNetworkSnapshot snapshot =
                    DdfDotNetWorkerNetworkSnapshot.FromContent(
                        grant.LogicalName, content, mediaType);
                string hostHash = Sha256(Encoding.UTF8.GetBytes(
                    grant.Endpoint.IdnHost.ToLowerInvariant()));
                var receipt = new DdfDotNetWorkerNetworkReceipt(
                    grant.LogicalName, grant.Endpoint.Scheme.ToLowerInvariant(),
                    grant.Endpoint.Port, hostHash, response.StatusCode,
                    mediaType, bytes.Length, snapshot.Sha256);
                return new DdfDotNetWorkerNetworkAcquisitionResult(snapshot,
                    receipt, Array.Empty<DdfDotNetContractIssue>());
            }
            catch (Exception exception) when (exception is HttpRequestException ||
                                               exception is SocketException ||
                                               exception is IOException ||
                                               exception is DecoderFallbackException ||
                                               exception is ArgumentException ||
                                               exception is InvalidOperationException ||
                                               exception is OperationCanceledException)
            {
                return Failure(exception is OperationCanceledException
                    ? "Timeout o cancellazione durante l'acquisizione HTTP."
                    : "Acquisizione HTTP governata fallita: " +
                        exception.Message);
            }
        }

        private static DdfDotNetContractIssue ValidateGrant(
            DdfDotNetWorkerNetworkGrant grant)
        {
            if (grant == null || string.IsNullOrWhiteSpace(grant.LogicalName) ||
                grant.LogicalName.Length > 128 || grant.LogicalName == "." ||
                grant.LogicalName == ".." || grant.LogicalName.Contains("..",
                    StringComparison.Ordinal) || grant.LogicalName.Any(character =>
                    !char.IsLetterOrDigit(character) && character != '_' &&
                    character != '-' && character != '.'))
                return Issue("Nome logico del grant HTTP non valido.");
            Uri endpoint = grant.Endpoint;
            if (endpoint == null || !endpoint.IsAbsoluteUri ||
                endpoint.Scheme != Uri.UriSchemeHttps &&
                endpoint.Scheme != Uri.UriSchemeHttp ||
                endpoint.Scheme == Uri.UriSchemeHttp &&
                !grant.AllowInsecureHttp || !string.IsNullOrEmpty(
                    endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment))
                return Issue("Endpoint HTTP non valido o non sicuro.");
            if (grant.MaxResponseBytes <= 0 ||
                grant.MaxResponseBytes > 1024 * 1024 ||
                grant.Timeout < TimeSpan.FromMilliseconds(100) ||
                grant.Timeout > TimeSpan.FromSeconds(60) ||
                grant.AllowedMediaTypes.Count == 0 ||
                grant.AllowedMediaTypes.Count > 16 ||
                grant.AllowedMediaTypes.Distinct(StringComparer.Ordinal).Count()
                    != grant.AllowedMediaTypes.Count ||
                grant.AllowedMediaTypes.Any(value =>
                    string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
                    value.Any(character => character < 0x20 ||
                        character > 0x7e)))
                return Issue("Quote, timeout o media type del grant HTTP non validi.");
            return null;
        }

        private static bool IsPublic(IPAddress address)
        {
            if (address == null) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.None) ||
                address.Equals(IPAddress.IPv6None) || address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast || address.IsIPv6SiteLocal)
                return false;
            byte[] bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return bytes[0] != 0 && bytes[0] != 10 &&
                    bytes[0] != 127 &&
                    !(bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) &&
                    !(bytes[0] == 169 && bytes[1] == 254) &&
                    !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) &&
                    !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) &&
                    !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) &&
                    !(bytes[0] == 192 && bytes[1] == 168) &&
                    !(bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) &&
                    !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                    !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) &&
                    !(bytes[0] >= 224);
            return address.AddressFamily == AddressFamily.InterNetworkV6 &&
                (bytes[0] & 0xe0) == 0x20;
        }

        private static string Sha256(byte[] bytes) => Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();

        private static DdfDotNetContractIssue Issue(string message) =>
            new("DDFC545", message);

        private static DdfDotNetWorkerNetworkAcquisitionResult Failure(
            string message) => new(null, null, new[] { Issue(message) });
    }
}
