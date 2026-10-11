using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Network
{
    public enum DdfHttpParameterLocation
    {
        Route,
        Query,
        JsonBody
    }

    public enum DdfHttpResponseKind
    {
        JsonScalar,
        Text,
        StatusCode
    }

    public sealed class DdfHttpParameterDefinition
    {
        public DdfHttpParameterDefinition(string name, string typeName,
            DdfHttpParameterLocation location, string wireName = null,
            string documentation = null)
        {
            if (!Enum.IsDefined(typeof(DdfHttpParameterLocation), location))
                throw new ArgumentOutOfRangeException(nameof(location));
            Name = Identifier(name, nameof(name));
            TypeName = Type(typeName, nameof(typeName));
            Location = location;
            WireName = Identifier(wireName ?? name, nameof(wireName));
            Documentation = documentation ?? string.Empty;
        }

        public string Name { get; }
        public string TypeName { get; }
        public DdfHttpParameterLocation Location { get; }
        public string WireName { get; }
        public string Documentation { get; }

        internal DdfHostParameterDefinition ToHostParameter() => new(Name,
            TypeName, Documentation);

        internal DdfExternalApiParameterDefinition ToExternalParameter() => new(
            Name, TypeName, Documentation);

        internal static string Identifier(string value, string parameterName)
        {
            string result = Required(value, parameterName);
            if (!(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character => !(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '.')))
                throw new ArgumentException(
                    "Identificatore HTTP non valido: " + result + ".",
                    parameterName);
            return result;
        }

        private static string Type(string value, string parameterName)
        {
            string result = Required(value, parameterName);
            if (result != "bool" && result != "int" && result != "float" &&
                result != "string")
                throw new ArgumentException(
                    "L'adapter HTTP iniziale supporta soltanto tipi scalari DDF.",
                    parameterName);
            return result;
        }

        private static string Required(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0)
                throw new ArgumentException("Il valore è obbligatorio.",
                    parameterName);
            return result;
        }
    }

    public sealed class DdfHttpOperationDefinition
    {
        private static readonly Regex RouteParameterPattern = new(
            "\\{([A-Za-z_][A-Za-z0-9_]*)\\}",
            RegexOptions.CultureInvariant);

        public DdfHttpOperationDefinition(string functionName, string bindingId,
            HttpMethod method, string relativePath, string returnType,
            IEnumerable<DdfHttpParameterDefinition> parameters = null,
            DdfHttpResponseKind responseKind = DdfHttpResponseKind.JsonScalar,
            string jsonPath = null, TimeSpan? timeout = null,
            int maxRequestBytes = 64 * 1024,
            int maxResponseBytes = 1024 * 1024,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null,
            bool requireSuccessStatusCode = true)
        {
            FunctionName = DdfHttpParameterDefinition.Identifier(functionName,
                nameof(functionName));
            BindingId = DdfHttpParameterDefinition.Identifier(bindingId,
                nameof(bindingId));
            Method = ValidateMethod(method);
            RelativePath = ValidateRelativePath(relativePath);
            ReturnType = ValidateReturnType(returnType);
            Parameters = UniqueParameters(parameters);
            ResponseKind = responseKind;
            if (!Enum.IsDefined(typeof(DdfHttpResponseKind), responseKind))
                throw new ArgumentOutOfRangeException(nameof(responseKind));
            JsonPath = (jsonPath ?? string.Empty).Trim();
            Timeout = timeout ?? TimeSpan.FromSeconds(15);
            if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (maxRequestBytes < 0 || maxRequestBytes > 16 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxRequestBytes));
            if (maxResponseBytes <= 0 || maxResponseBytes > 16 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
            MaxRequestBytes = maxRequestBytes;
            MaxResponseBytes = maxResponseBytes;
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = (requiredPermissions ?? Array.Empty<string>())
                .Select(item => DdfHttpParameterDefinition.Identifier(item,
                    nameof(requiredPermissions))).Distinct(StringComparer.Ordinal)
                .ToList().AsReadOnly();
            RequireSuccessStatusCode = requireSuccessStatusCode;
            ValidateContract();
        }

        public string FunctionName { get; }
        public string BindingId { get; }
        public HttpMethod Method { get; }
        public string RelativePath { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfHttpParameterDefinition> Parameters { get; }
        public DdfHttpResponseKind ResponseKind { get; }
        public string JsonPath { get; }
        public TimeSpan Timeout { get; }
        public int MaxRequestBytes { get; }
        public int MaxResponseBytes { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public bool RequireSuccessStatusCode { get; }
        public bool IsReadOnly => Method == HttpMethod.Get;

        internal DdfHostFunctionDefinition ToHostFunction(
            DdfHttpJsonAdapter adapter) => new(FunctionName, ReturnType,
                Parameters.Select(item => item.ToHostParameter()), null,
                Documentation, RequiredPermissions,
                effect: IsReadOnly ? DdfHostFunctionEffect.Pure :
                    DdfHostFunctionEffect.HostMutation,
                invokeAsync: (context, arguments) => adapter.InvokeAsync(context,
                    BindingId, arguments));

        internal DdfExternalApiFunctionDefinition ToExternalFunction(
            IEnumerable<string> inheritedPermissions) => new(FunctionName,
                BindingId, ReturnType,
                Parameters.Select(item => item.ToExternalParameter()),
                Documentation, (inheritedPermissions ?? Array.Empty<string>())
                    .Concat(RequiredPermissions).Distinct(StringComparer.Ordinal),
                IsReadOnly ? DdfHostFunctionEffect.Pure :
                    DdfHostFunctionEffect.HostMutation);

        private void ValidateContract()
        {
            string[] placeholders = RouteParameterPattern.Matches(RelativePath)
                .Select(item => item.Groups[1].Value)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] routeNames = Parameters.Where(item =>
                    item.Location == DdfHttpParameterLocation.Route)
                .Select(item => item.WireName).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray();
            if (!placeholders.SequenceEqual(routeNames, StringComparer.Ordinal))
                throw new ArgumentException(
                    "I placeholder del percorso devono coincidere con i parametri route.");
            if (Method == HttpMethod.Get && Parameters.Any(item =>
                    item.Location == DdfHttpParameterLocation.JsonBody))
                throw new ArgumentException(
                    "GET non può contenere parametri nel body JSON.");
            if (ResponseKind == DdfHttpResponseKind.Text && ReturnType != "string")
                throw new ArgumentException(
                    "Una risposta testuale richiede il tipo DDF string.");
            if (ResponseKind == DdfHttpResponseKind.StatusCode && ReturnType != "int")
                throw new ArgumentException(
                    "Lo status HTTP richiede il tipo DDF int.");
        }

        private static IReadOnlyList<DdfHttpParameterDefinition>
            UniqueParameters(IEnumerable<DdfHttpParameterDefinition> values)
        {
            var result = new List<DdfHttpParameterDefinition>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DdfHttpParameterDefinition value in values ??
                     Array.Empty<DdfHttpParameterDefinition>())
            {
                if (value == null || !names.Add(value.Name))
                    throw new ArgumentException(
                        "I parametri HTTP non possono essere nulli o duplicati.",
                        nameof(values));
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        private static HttpMethod ValidateMethod(HttpMethod method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            string value = method.Method.ToUpperInvariant();
            if (value != "GET" && value != "POST" && value != "PUT" &&
                value != "PATCH" && value != "DELETE")
                throw new ArgumentException(
                    "Metodo HTTP non autorizzabile dall'adapter iniziale.",
                    nameof(method));
            return new HttpMethod(value);
        }

        private static string ValidateRelativePath(string value)
        {
            string result = (value ?? string.Empty).Trim();
            if (!result.StartsWith("/", StringComparison.Ordinal) ||
                result.StartsWith("//", StringComparison.Ordinal) ||
                result.Contains("..", StringComparison.Ordinal) ||
                Uri.TryCreate(result, UriKind.Absolute, out _))
                throw new ArgumentException(
                    "Il percorso HTTP deve essere relativo all'origine autorizzata.",
                    nameof(value));
            return result;
        }

        private static string ValidateReturnType(string value)
        {
            string result = (value ?? string.Empty).Trim();
            if (result != "bool" && result != "int" && result != "float" &&
                result != "string")
                throw new ArgumentException(
                    "Tipo risposta HTTP DDF non supportato.", nameof(value));
            return result;
        }
    }

    public delegate ValueTask DdfHttpRequestAuthenticator(
        HttpRequestMessage request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends only host-declared operations to one exact HTTPS origin. Scripts
    /// never provide URLs, methods, headers, credentials or raw JSON.
    /// </summary>
    public sealed class DdfHttpJsonAdapter : IDdfExternalApiRuntimeAdapter,
        IDisposable
    {
        private readonly HttpClient client;
        private readonly bool ownsClient;
        private readonly Uri origin;
        private readonly IReadOnlyDictionary<string, DdfHttpOperationDefinition>
            operations;
        private readonly DdfHttpRequestAuthenticator authenticator;
        private int disposed;

        public DdfHttpJsonAdapter(string adapterId, Uri baseAddress,
            HttpClient client,
            IEnumerable<DdfHttpOperationDefinition> operations,
            DdfHttpRequestAuthenticator authenticator = null,
            bool ownsClient = false)
        {
            AdapterId = DdfHttpParameterDefinition.Identifier(adapterId,
                nameof(adapterId));
            origin = ValidateOrigin(baseAddress);
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.ownsClient = ownsClient;
            this.authenticator = authenticator;
            DdfHttpOperationDefinition[] source = (operations ??
                Array.Empty<DdfHttpOperationDefinition>()).ToArray();
            if (source.Length == 0 || source.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesta almeno un'operazione HTTP valida.",
                    nameof(operations));
            if (source.Select(item => item.BindingId).Distinct(
                    StringComparer.Ordinal).Count() != source.Length ||
                source.Select(item => item.FunctionName).Distinct(
                    StringComparer.Ordinal).Count() != source.Length)
                throw new ArgumentException(
                    "Binding e nomi funzione HTTP devono essere univoci.",
                    nameof(operations));
            this.operations = new ReadOnlyDictionary<string,
                DdfHttpOperationDefinition>(source.ToDictionary(item =>
                    item.BindingId, StringComparer.Ordinal));
        }

        public string AdapterId { get; }
        public Uri BaseAddress => origin;
        public IReadOnlyCollection<DdfHttpOperationDefinition> Operations =>
            operations.Values.ToList().AsReadOnly();

        public DdfHostFunctionLibraryDefinition CreateHostLibrary(string id,
            string displayName, string documentation = null,
            IEnumerable<string> requiredPermissions = null) => new(id,
                displayName, operations.Values.Select(item =>
                    item.ToHostFunction(this)), documentation,
                requiredPermissions);

        public DdfExternalApiDefinition CreateExternalApiDefinition(
            string referenceId, string referenceVersion,
            IEnumerable<DdfHostPermissionDefinition> permissions = null,
            IEnumerable<string> requiredPermissions = null) => new(referenceId,
                referenceVersion, permissions: permissions,
                functions: operations.Values.Select(item =>
                    item.ToExternalFunction(requiredPermissions)));

        object IDdfExternalApiRuntimeAdapter.ReadObject(string bindingId) =>
            throw new NotSupportedException(
                "L'adapter HTTP non espone oggetti globali.");

        void IDdfExternalApiRuntimeAdapter.WriteObject(string bindingId,
            object value) => throw new NotSupportedException(
                "L'adapter HTTP non espone oggetti globali.");

        public async ValueTask<object> InvokeAsync(DdfHostInvocationContext context,
            string bindingId, IReadOnlyList<object> arguments)
        {
            ThrowIfDisposed();
            if (!operations.TryGetValue(bindingId ?? string.Empty,
                    out DdfHttpOperationDefinition operation))
                throw new InvalidOperationException(
                    "Operazione HTTP non autorizzata: " + bindingId + ".");
            if (context == null) throw new ArgumentNullException(nameof(context));
            if ((arguments?.Count ?? 0) != operation.Parameters.Count)
                throw new ArgumentException(
                    "Numero di argomenti HTTP non valido.", nameof(arguments));
            using CancellationTokenSource timeout = CreateTimeout(context,
                operation.Timeout);
            using HttpRequestMessage request = BuildRequest(operation, arguments);
            HttpMethod declaredMethod = request.Method;
            Uri declaredUri = request.RequestUri;
            HttpContent declaredContent = request.Content;
            if (authenticator != null)
                await authenticator(request, timeout.Token);
            if (request.Method != declaredMethod || request.RequestUri !=
                    declaredUri || !ReferenceEquals(request.Content,
                        declaredContent))
                throw new InvalidOperationException(
                    "L'autenticatore HTTP può modificare soltanto gli header.");
            ValidateUri(request.RequestUri);
            using HttpResponseMessage response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            ValidateUri(response.RequestMessage?.RequestUri ?? request.RequestUri);
            if (operation.RequireSuccessStatusCode)
                response.EnsureSuccessStatusCode();
            if (operation.ResponseKind == DdfHttpResponseKind.StatusCode)
                return (int)response.StatusCode;
            byte[] payload = await ReadBoundedAsync(response.Content,
                operation.MaxResponseBytes, timeout.Token);
            if (operation.ResponseKind == DdfHttpResponseKind.Text)
                return Encoding.UTF8.GetString(payload);
            using JsonDocument json = JsonDocument.Parse(payload,
                new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement value = SelectJsonValue(json.RootElement,
                operation.JsonPath);
            return ConvertJsonValue(value, operation.ReturnType);
        }

        private HttpRequestMessage BuildRequest(
            DdfHttpOperationDefinition operation,
            IReadOnlyList<object> arguments)
        {
            string path = operation.RelativePath;
            var query = new List<string>();
            var body = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < operation.Parameters.Count; index++)
            {
                DdfHttpParameterDefinition parameter =
                    operation.Parameters[index];
                object value = arguments[index];
                string encoded = Uri.EscapeDataString(FormatScalar(value));
                if (parameter.Location == DdfHttpParameterLocation.Route)
                    path = path.Replace("{" + parameter.WireName + "}",
                        encoded, StringComparison.Ordinal);
                else if (parameter.Location == DdfHttpParameterLocation.Query)
                    query.Add(Uri.EscapeDataString(parameter.WireName) + "=" +
                        encoded);
                else
                    body.Add(parameter.WireName, value);
            }
            if (query.Count != 0)
                path += (path.Contains('?', StringComparison.Ordinal) ? "&" :
                    "?") + string.Join("&", query);
            var request = new HttpRequestMessage(operation.Method,
                new Uri(origin, path));
            if (body.Count != 0)
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body);
                if (bytes.Length > operation.MaxRequestBytes)
                {
                    request.Dispose();
                    throw new InvalidOperationException(
                        "Payload HTTP oltre il limite autorizzato.");
                }
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new(
                    "application/json") { CharSet = "utf-8" };
            }
            return request;
        }

        private static async Task<byte[]> ReadBoundedAsync(HttpContent content,
            int maximum, CancellationToken cancellationToken)
        {
            if (content.Headers.ContentLength > maximum)
                throw new InvalidOperationException(
                    "Risposta HTTP oltre il limite autorizzato.");
            await using Stream input = await content.ReadAsStreamAsync(
                cancellationToken);
            using var output = new MemoryStream(Math.Min(maximum, 81920));
            byte[] buffer = new byte[81920];
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0,
                    Math.Min(buffer.Length, maximum - checked((int)output.Length) +
                        1)), cancellationToken);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                if (output.Length > maximum)
                    throw new InvalidOperationException(
                        "Risposta HTTP oltre il limite autorizzato.");
            }
            return output.ToArray();
        }

        private static JsonElement SelectJsonValue(JsonElement root,
            string jsonPath)
        {
            JsonElement current = root;
            foreach (string segment in (jsonPath ?? string.Empty).Split('.',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(segment, out current))
                    throw new InvalidOperationException(
                        "Percorso JSON autorizzato non presente: " + jsonPath +
                        ".");
            }
            return current;
        }

        private static object ConvertJsonValue(JsonElement value,
            string typeName) => typeName switch
        {
            "bool" when value.ValueKind == JsonValueKind.True ||
                        value.ValueKind == JsonValueKind.False =>
                value.GetBoolean(),
            "int" when value.TryGetInt32(out int integer) => integer,
            "float" when value.TryGetDouble(out double number) &&
                         double.IsFinite(number) => number,
            "string" when value.ValueKind == JsonValueKind.String =>
                value.GetString(),
            _ => throw new InvalidOperationException(
                "La risposta JSON non coincide con il tipo DDF " + typeName +
                ".")
        };

        private static string FormatScalar(object value) => value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null,
                CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        private static CancellationTokenSource CreateTimeout(
            DdfHostInvocationContext context, TimeSpan operationTimeout)
        {
            var result = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken);
            TimeSpan remaining = operationTimeout;
            if (context.Deadline.HasValue)
            {
                TimeSpan deadline = context.Deadline.Value - DateTimeOffset.UtcNow;
                if (deadline <= TimeSpan.Zero)
                {
                    result.Cancel();
                    return result;
                }
                if (deadline < remaining) remaining = deadline;
            }
            result.CancelAfter(remaining);
            return result;
        }

        private static Uri ValidateOrigin(Uri baseAddress)
        {
            if (baseAddress == null || !baseAddress.IsAbsoluteUri ||
                baseAddress.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(baseAddress.UserInfo) ||
                !string.IsNullOrEmpty(baseAddress.Query) ||
                !string.IsNullOrEmpty(baseAddress.Fragment))
                throw new ArgumentException(
                    "L'adapter HTTP richiede un'origine HTTPS assoluta e pulita.",
                    nameof(baseAddress));
            var builder = new UriBuilder(baseAddress.Scheme, baseAddress.Host,
                baseAddress.IsDefaultPort ? -1 : baseAddress.Port, "/");
            return builder.Uri;
        }

        private void ValidateUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                uri.Scheme != origin.Scheme || uri.Host != origin.Host ||
                uri.Port != origin.Port || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(
                    "La richiesta o risposta HTTP è uscita dall'origine autorizzata.");
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(DdfHttpJsonAdapter));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsClient)
                client.Dispose();
        }
    }
}
