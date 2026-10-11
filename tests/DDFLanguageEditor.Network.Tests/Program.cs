using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Network;

namespace DDFLanguageEditor.Network.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("HTTP builds only declared route query and JSON requests",
                BuildsGovernedRequests);
            Run("HTTP keeps credentials outside the DDF contract",
                ProtectsCredentialsAndProjectsContract);
            Run("HTTP enforces origin method and payload limits",
                EnforcesNetworkBoundaries);
            Run("HTTP observes cancellation and host timeout",
                ObservesTimeout);
            Console.WriteLine(failures == 0
                ? "All 4 governed HTTP adapter tests passed."
                : failures + " governed HTTP adapter test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void BuildsGovernedRequests()
        {
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            using DdfHttpJsonAdapter adapter = CreateAdapter(client);
            using DdfEditorExecutionService execution = CreateExecution(adapter);
            Equal(42, ReturnInt(Invoke(execution, "loadRemoteScore",
                new Dictionary<string, object>
                {
                    ["id"] = 7,
                    ["scale"] = 2
                })));
            Equal("GET", handler.Requests[0].Method);
            Equal("https://api.example.test/users/7?scale=2",
                handler.Requests[0].Uri);
            Equal(string.Empty, handler.Requests[0].Body);

            Equal(201, ReturnInt(Invoke(execution, "saveRemoteUser",
                new Dictionary<string, object>
                {
                    ["id"] = 7,
                    ["name"] = "Ada \"DDF\"",
                    ["enabled"] = true
                })));
            Equal("POST", handler.Requests[1].Method);
            Equal("https://api.example.test/users/7",
                handler.Requests[1].Uri);
            using JsonDocument body = JsonDocument.Parse(
                handler.Requests[1].Body);
            Equal("Ada \"DDF\"", body.RootElement.GetProperty("name")
                .GetString());
            Equal(true, body.RootElement.GetProperty("enabled").GetBoolean());
        }

        private static void ProtectsCredentialsAndProjectsContract()
        {
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            using DdfHttpJsonAdapter adapter = CreateAdapter(client,
                token: "top-secret-token");
            DdfExternalApiDefinition api = adapter.CreateExternalApiDefinition(
                "ddf.dotnet.http.demo", "1.0.0", Permissions(),
                new[] { "net.access" });
            Equal(5, api.Functions.Count);
            Equal(true, api.Functions.All(item =>
                item.RequiredPermissions.Contains("net.access")));
            string contract = string.Join("|", api.Functions.Select(item =>
                item.Name + item.Documentation)) + api.Fingerprint;
            Equal(false, contract.Contains("top-secret-token",
                StringComparison.Ordinal));
            DdfHostCatalogSnapshot analysis =
                DdfExternalApiCatalogProjector.CreateAnalysisCatalog(api,
                    Permissions().Select(item => item.Id));
            Equal(true, analysis.Functions.Any(item =>
                item.Name == "remoteScore" && item.ReturnType == "int"));
            using DdfEditorExecutionService execution = CreateExecution(adapter);
            Equal("ready", ReturnString(Invoke(execution, "checkRemote")));
            Equal("Bearer top-secret-token", handler.Requests.Single()
                .Authorization);
        }

        private static void EnforcesNetworkBoundaries()
        {
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            using DdfHttpJsonAdapter adapter = CreateAdapter(client);
            using DdfEditorExecutionService execution = CreateExecution(adapter);
            DdfEditorExecutionCompletion large = Invoke(execution,
                "loadOversized");
            Equal(true, large.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF410" && item.Message.Contains("limite",
                    StringComparison.Ordinal)));
            Throws<ArgumentException>(() => new DdfHttpJsonAdapter("bad",
                new Uri("http://api.example.test"), client,
                BaseOperations()));
            Throws<ArgumentException>(() => new DdfHttpOperationDefinition(
                "bad", "bad.path", HttpMethod.Get,
                "https://evil.example/path", "string",
                responseKind: DdfHttpResponseKind.Text));
            Throws<ArgumentException>(() => new DdfHttpOperationDefinition(
                "trace", "bad.method", HttpMethod.Trace, "/trace", "int",
                responseKind: DdfHttpResponseKind.StatusCode));
            Throws<InvalidOperationException>(() => adapter.InvokeAsync(null,
                "raw.url", Array.Empty<object>()).AsTask().GetAwaiter()
                .GetResult());
        }

        private static void ObservesTimeout()
        {
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            using DdfHttpJsonAdapter adapter = CreateAdapter(client);
            using DdfEditorExecutionService execution = CreateExecution(adapter);
            DdfEditorExecutionCompletion timeout = Invoke(execution,
                "waitRemote", executionRequest: new DdfEditorExecutionRequest(
                    hostCallTimeout: TimeSpan.FromMilliseconds(20)));
            Equal(true, timeout.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF414"));
            Equal(true, handler.SlowRequestCancelled);
        }

        private static DdfHttpJsonAdapter CreateAdapter(HttpClient client,
            string token = "test-token") => new("http.demo",
                new Uri("https://api.example.test/"), client,
                BaseOperations(), (request, cancellationToken) =>
                {
                    request.Headers.Authorization = new(
                        "Bearer", token);
                    return ValueTask.CompletedTask;
                });

        private static IReadOnlyList<DdfHttpOperationDefinition>
            BaseOperations() => new[]
            {
                new DdfHttpOperationDefinition("remoteScore", "user.score",
                    HttpMethod.Get, "/users/{id}", "int", new[]
                    {
                        new DdfHttpParameterDefinition("id", "int",
                            DdfHttpParameterLocation.Route),
                        new DdfHttpParameterDefinition("scale", "int",
                            DdfHttpParameterLocation.Query)
                    }, jsonPath: "data.score",
                    requiredPermissions: new[] { "net.read" }),
                new DdfHttpOperationDefinition("remoteSave", "user.save",
                    HttpMethod.Post, "/users/{id}", "int", new[]
                    {
                        new DdfHttpParameterDefinition("id", "int",
                            DdfHttpParameterLocation.Route),
                        new DdfHttpParameterDefinition("name", "string",
                            DdfHttpParameterLocation.JsonBody),
                        new DdfHttpParameterDefinition("enabled", "bool",
                            DdfHttpParameterLocation.JsonBody)
                    }, DdfHttpResponseKind.StatusCode,
                    requiredPermissions: new[] { "net.write" }),
                new DdfHttpOperationDefinition("remoteHealth", "health.text",
                    HttpMethod.Get, "/health", "string",
                    responseKind: DdfHttpResponseKind.Text,
                    requiredPermissions: new[] { "net.read" }),
                new DdfHttpOperationDefinition("remoteLarge", "large.text",
                    HttpMethod.Get, "/large", "string",
                    responseKind: DdfHttpResponseKind.Text,
                    maxResponseBytes: 8,
                    requiredPermissions: new[] { "net.read" }),
                new DdfHttpOperationDefinition("remoteWait", "slow.text",
                    HttpMethod.Get, "/slow", "string",
                    responseKind: DdfHttpResponseKind.Text,
                    timeout: TimeSpan.FromSeconds(30),
                    requiredPermissions: new[] { "net.read" })
            };

        private static DdfEditorExecutionService CreateExecution(
            DdfHttpJsonAdapter adapter)
        {
            var catalog = new DdfEditorHostCatalog();
            foreach (DdfHostPermissionDefinition permission in Permissions())
            {
                catalog.RegisterPermission(permission);
                catalog.SetPermission(permission.Id, true);
            }
            catalog.RegisterFunctionLibrary(adapter.CreateHostLibrary(
                "demo.http", "HTTP demo", requiredPermissions:
                    new[] { "net.access" }));
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("http.ddf",
                "main() out int { ret 0; } " +
                "loadRemoteScore(int id, int scale) out int { " +
                "ret remoteScore(id, scale); } " +
                "saveRemoteUser(int id, string name, bool enabled) out int { " +
                "ret remoteSave(id, name, enabled); } " +
                "checkRemote() out string { ret remoteHealth(); } " +
                "loadOversized() out string { ret remoteLarge(); } " +
                "waitRemote() out string { ret remoteWait(); }");
            var language = new DdfLanguagePackageSession();
            return new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);
        }

        private static IReadOnlyList<DdfHostPermissionDefinition> Permissions()
            => new[]
            {
                new DdfHostPermissionDefinition("net.access", "Accesso rete"),
                new DdfHostPermissionDefinition("net.read", "Lettura HTTP"),
                new DdfHostPermissionDefinition("net.write", "Scrittura HTTP")
            };

        private static DdfEditorExecutionCompletion Invoke(
            DdfEditorExecutionService execution, string procedure,
            IReadOnlyDictionary<string, object> arguments = null,
            DdfEditorExecutionRequest executionRequest = null) => execution
                .InvokeAsync(new DdfEditorProcedureRequest(procedure, arguments,
                    execution: executionRequest)).GetAwaiter().GetResult();

        private static int ReturnInt(DdfEditorExecutionCompletion completion)
        {
            Equal(true, completion.Succeeded);
            return Convert.ToInt32(completion.ExecutionResult.ReturnValue);
        }

        private static string ReturnString(
            DdfEditorExecutionCompletion completion)
        {
            Equal(true, completion.Succeeded);
            return Convert.ToString(completion.ExecutionResult.ReturnValue);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": " + exception);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected '" + expected +
                    "', got '" + actual + "'.");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name +
                ".");
        }

        private sealed class RequestSnapshot
        {
            internal string Method { get; init; }
            internal string Uri { get; init; }
            internal string Body { get; init; }
            internal string Authorization { get; init; }
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            internal List<RequestSnapshot> Requests { get; } = new();
            internal bool SlowRequestCancelled { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content == null ? string.Empty :
                    await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(new RequestSnapshot
                {
                    Method = request.Method.Method,
                    Uri = request.RequestUri.AbsoluteUri,
                    Body = body,
                    Authorization = request.Headers.Authorization?.ToString() ??
                        string.Empty
                });
                string path = request.RequestUri.AbsolutePath;
                if (path == "/slow")
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        SlowRequestCancelled = true;
                        throw;
                    }
                }
                if (path == "/large")
                    return Response(HttpStatusCode.OK,
                        new string('x', 100), request);
                if (path == "/health")
                    return Response(HttpStatusCode.OK, "ready", request);
                if (request.Method == HttpMethod.Post)
                    return Response(HttpStatusCode.Created, string.Empty,
                        request);
                return Response(HttpStatusCode.OK,
                    "{\"data\":{\"score\":42}}", request,
                    "application/json");
            }

            private static HttpResponseMessage Response(HttpStatusCode status,
                string content, HttpRequestMessage request,
                string mediaType = "text/plain") => new(status)
                {
                    RequestMessage = request,
                    Content = new StringContent(content, Encoding.UTF8,
                        mediaType)
                };
        }
    }
}
