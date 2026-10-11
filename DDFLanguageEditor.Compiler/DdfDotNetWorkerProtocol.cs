using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    public enum DdfDotNetWorkerFileSystemAccess
    {
        None,
        BrokeredRead,
        BrokeredReadWrite
    }

    public enum DdfDotNetWorkerNetworkAccess
    {
        None,
        BrokeredClient
    }

    public enum DdfDotNetWorkerConsoleInputAccess
    {
        None,
        BrokeredInteractive
    }

    public sealed class DdfDotNetWorkerAccessPolicy
    {
        public const string FormatValue = "ddf-dotnet-worker-access-policy";
        public const string PolicyVersionValue = "1.3";

        [JsonRequired]
        public string Format { get; set; } = FormatValue;
        [JsonRequired]
        public string PolicyVersion { get; set; } = PolicyVersionValue;
        public DdfDotNetWorkerFileSystemAccess FileSystemAccess { get; set; }
        public DdfDotNetWorkerNetworkAccess NetworkAccess { get; set; }
        public DdfDotNetWorkerConsoleInputAccess ConsoleInputAccess {
            get; set;
        }
        public bool RequireReducedUserToken { get; set; }
        public List<string> AllowedBrokerCapabilities { get; set; } = new();

        public static DdfDotNetWorkerAccessPolicy DenyAll(
            bool requireReducedUserToken = false) => new()
        {
            RequireReducedUserToken = requireReducedUserToken
        };
    }

    public static class DdfDotNetWorkerBrokerCapabilities
    {
        public static readonly string ReadText =
            DdfDotNetHostCapabilities.ForFunction("brokerReadText");
        public static readonly string WriteText =
            DdfDotNetHostCapabilities.ForFunction("brokerWriteText");
        public static readonly string ReadNetworkResponse =
            DdfDotNetHostCapabilities.ForFunction("brokerReadResponse");
        public static readonly string FetchNetworkResponse =
            DdfDotNetHostCapabilities.ForFunction("brokerFetchResponse");
        public const string ReadConsoleInput = "worker.console.readLine";
        public const string NextDebugCommand = "worker.debug.nextCommand";
    }

    public sealed class DdfDotNetWorkerInteractiveInputOptions
    {
        public int MaxRequests { get; set; } = 64;
        public int MaxValueBytes { get; set; } = 64 * 1024;
    }

    public sealed class DdfDotNetWorkerFileSnapshot
    {
        public string LogicalName { get; set; }
        public string Content { get; set; }
        public string Sha256 { get; set; }
        public bool Writable { get; set; }
        public int MaxWriteBytes { get; set; }

        public static DdfDotNetWorkerFileSnapshot FromFile(string logicalName,
            string absolutePath, int maxBytes = 1024 * 1024,
            bool writable = false, int? maxWriteBytes = null)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !Path.IsPathRooted(absolutePath))
                throw new ArgumentException(
                    "Il broker richiede un percorso assoluto.",
                    nameof(absolutePath));
            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length > maxBytes)
                throw new ArgumentException(
                    "File broker oltre il limite consentito.",
                    nameof(absolutePath));
            string content = new UTF8Encoding(false, true).GetString(bytes);
            return new DdfDotNetWorkerFileSnapshot
            {
                LogicalName = logicalName,
                Content = content,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant(),
                Writable = writable,
                MaxWriteBytes = writable ? maxWriteBytes ?? maxBytes : 0
            };
        }
    }

    public sealed class DdfDotNetWorkerFileWrite
    {
        public string LogicalName { get; set; }
        public string OriginalSha256 { get; set; }
        public string Content { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class DdfDotNetWorkerNetworkSnapshot
    {
        public string LogicalName { get; set; }
        public string Content { get; set; }
        public string MediaType { get; set; }
        public string Sha256 { get; set; }

        public static DdfDotNetWorkerNetworkSnapshot FromContent(
            string logicalName, string content,
            string mediaType = "text/plain")
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            return new DdfDotNetWorkerNetworkSnapshot
            {
                LogicalName = logicalName,
                Content = content ?? string.Empty,
                MediaType = mediaType,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant()
            };
        }
    }

    public sealed class DdfDotNetWorkerArtifact
    {
        public string ModuleIdentity { get; set; }
        public string AssemblyPath { get; set; }
        public string PortablePdbPath { get; set; }
        public string SourceMapPath { get; set; }
        public string ManifestPath { get; set; }
        public byte[] AssemblyBytes { get; set; }
        public byte[] PortablePdbBytes { get; set; }
        public byte[] SourceMapBytes { get; set; }
        public byte[] ManifestBytes { get; set; }
    }

    public sealed class DdfDotNetWorkerPackagedReference
    {
        public string AssemblyIdentity { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
        public byte[] AssemblyBytes { get; set; }
    }

    public sealed class DdfDotNetWorkerDebugBreakpoint
    {
        public string SourcePath { get; set; }
        public int Start { get; set; }
        public int Length { get; set; } = 1;
    }

    public sealed class DdfDotNetWorkerDebugPlan
    {
        public List<DdfDotNetWorkerDebugBreakpoint> Breakpoints { get; set; } =
            new();
        public List<DdfDotNetDebugCommand> Commands { get; set; } = new();
        public int PauseTimeoutMilliseconds { get; set; } = 5000;
        public int MaxPauses { get; set; } = 64;
        public bool Interactive { get; set; }
    }

    public sealed class DdfDotNetWorkerDebugVariable
    {
        public string SymbolId { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public DdfRuntimeDebugVariableKind Kind { get; set; }
        public string DisplayValue { get; set; }
        public List<DdfDotNetWorkerDebugVariable> Children { get; set; } = new();
        public bool IsTruncated { get; set; }
    }

    public sealed class DdfDotNetWorkerSourceLocation
    {
        public string DocumentPath { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string SymbolId { get; set; }
    }

    public sealed class DdfDotNetWorkerDebugFrame
    {
        public string SymbolId { get; set; }
        public string Name { get; set; }
        public DdfDotNetWorkerSourceLocation Location { get; set; }
        public List<DdfDotNetWorkerDebugVariable> Variables { get; set; } = new();
    }

    public sealed class DdfDotNetWorkerDebugPause
    {
        public DdfDotNetWorkerSourceLocation Location { get; set; }
        public long Instructions { get; set; }
        public List<DdfDotNetWorkerDebugFrame> Frames { get; set; } = new();
    }

    public sealed class DdfDotNetWorkerRequest
    {
        public const string FormatValue = "ddf-dotnet-worker-request";
        public const string ProtocolVersionValue = "1.9";

        public string Format { get; set; } = FormatValue;
        public string ProtocolVersion { get; set; } = ProtocolVersionValue;
        public string CorrelationId { get; set; }
        public string EntryModuleIdentity { get; set; }
        public string PackageManifestJson { get; set; }
        public List<DdfDotNetWorkerArtifact> Artifacts { get; set; } = new();
        public List<string> ReferencePaths { get; set; } = new();
        public List<DdfDotNetWorkerPackagedReference> PackagedReferences {
            get; set;
        } = new();
        public string TargetFramework { get; set; }
        public string RuntimeAbiVersion { get; set; }
        public string RuntimeIdentifier { get; set; }
        public List<string> HostCapabilities { get; set; } = new();
        [JsonRequired]
        public DdfDotNetWorkerAccessPolicy AccessPolicy { get; set; } =
            DdfDotNetWorkerAccessPolicy.DenyAll();
        public List<DdfDotNetWorkerFileSnapshot> BrokerFiles { get; set; } =
            new();
        public List<DdfDotNetWorkerNetworkSnapshot> BrokerNetworkResponses {
            get; set;
        } = new();
        public List<string> LiveNetworkResources { get; set; } = new();
        public List<string> BufferedInput { get; set; } = new();
        public DdfDotNetWorkerInteractiveInputOptions InteractiveInput {
            get; set;
        }
        public DdfDotNetWorkerDebugPlan DebugPlan { get; set; }
        public int MaxInstructions { get; set; } = 100000;
    }

    public sealed class DdfDotNetWorkerResponse
    {
        public const string FormatValue = "ddf-dotnet-worker-response";
        public const string ProtocolVersionValue = "1.9";

        public string Format { get; set; } = FormatValue;
        public string ProtocolVersion { get; set; } = ProtocolVersionValue;
        public string CorrelationId { get; set; }
        public bool Success { get; set; }
        public string ResultJson { get; set; }
        public bool Ended { get; set; }
        public long Instructions { get; set; }
        public List<string> Output { get; set; } = new();
        public List<DdfDotNetWorkerFileWrite> BrokerFileWrites { get; set; } =
            new();
        public int ConsumedInputCount { get; set; }
        public List<DdfDotNetWorkerDebugPause> DebugTrace { get; set; } = new();
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool ReducedUserTokenApplied { get; set; }
    }

    public sealed class DdfDotNetWorkerPackage
    {
        internal DdfDotNetWorkerPackage(DdfDotNetLinkedBundle bundle,
            DdfDotNetReferenceSet referenceSet,
            DdfDotNetRuntimeEnvironment environment, int maxInstructions,
            DdfDotNetWorkerAccessPolicy accessPolicy,
            IEnumerable<DdfDotNetWorkerFileSnapshot> brokerFiles,
            IEnumerable<string> bufferedInput,
            DdfDotNetWorkerDebugPlan debugPlan,
            IEnumerable<DdfDotNetWorkerNetworkSnapshot> networkResponses,
            IEnumerable<string> liveNetworkResources,
            DdfDotNetWorkerInteractiveInputOptions interactiveInput)
        {
            Bundle = bundle;
            ReferenceSet = referenceSet;
            Environment = environment;
            MaxInstructions = maxInstructions;
            AccessPolicy = accessPolicy;
            BrokerFiles = (brokerFiles ??
                Array.Empty<DdfDotNetWorkerFileSnapshot>()).ToList().AsReadOnly();
            BufferedInput = (bufferedInput ?? Array.Empty<string>()).ToList()
                .AsReadOnly();
            DebugPlan = debugPlan;
            BrokerNetworkResponses = (networkResponses ??
                Array.Empty<DdfDotNetWorkerNetworkSnapshot>()).ToList()
                .AsReadOnly();
            LiveNetworkResources = (liveNetworkResources ??
                Array.Empty<string>()).ToList().AsReadOnly();
            InteractiveInput = interactiveInput;
        }

        public DdfDotNetLinkedBundle Bundle { get; }
        public DdfDotNetReferenceSet ReferenceSet { get; }
        public DdfDotNetRuntimeEnvironment Environment { get; }
        public int MaxInstructions { get; }
        public DdfDotNetWorkerAccessPolicy AccessPolicy { get; }
        public IReadOnlyList<DdfDotNetWorkerFileSnapshot> BrokerFiles { get; }
        public IReadOnlyList<string> BufferedInput { get; }
        public DdfDotNetWorkerDebugPlan DebugPlan { get; }
        public IReadOnlyList<DdfDotNetWorkerNetworkSnapshot>
            BrokerNetworkResponses { get; }
        public IReadOnlyList<string> LiveNetworkResources { get; }
        public DdfDotNetWorkerInteractiveInputOptions InteractiveInput {
            get;
        }
    }

    public sealed class DdfDotNetWorkerPackageResult
    {
        internal DdfDotNetWorkerPackageResult(DdfDotNetWorkerPackage package,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Package = package;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetWorkerPackage Package { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Package != null && Issues.Count == 0;
    }

    public static class DdfDotNetWorkerProtocol
    {
        public const int MaxRequestCharacters = 64 * 1024 * 1024;
        public const int MaxResponseCharacters = 4 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };

        public static DdfDotNetWorkerRequest CreateRequest(
            DdfDotNetLinkedBundle bundle, DdfDotNetReferenceSet referenceSet,
            DdfDotNetRuntimeEnvironment environment, int maxInstructions,
            string correlationId = null,
            DdfDotNetWorkerAccessPolicy accessPolicy = null,
            IEnumerable<DdfDotNetWorkerFileSnapshot> brokerFiles = null,
            IEnumerable<string> bufferedInput = null,
            DdfDotNetWorkerDebugPlan debugPlan = null,
            IEnumerable<DdfDotNetWorkerNetworkSnapshot> networkResponses = null,
            IEnumerable<string> liveNetworkResources = null,
            DdfDotNetWorkerInteractiveInputOptions interactiveInput = null)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (referenceSet == null)
                throw new ArgumentNullException(nameof(referenceSet));
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));
            if (environment.HostKind == DdfDotNetArtifactHostKind.Embedded)
                throw new ArgumentException(
                    "Il worker compilato non è disponibile in embedded.",
                    nameof(environment));
            if (maxInstructions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxInstructions));
            accessPolicy ??= DdfDotNetWorkerAccessPolicy.DenyAll(
                OperatingSystem.IsWindows());
            DdfDotNetContractIssue accessIssue = ValidateAccessPolicy(
                accessPolicy);
            if (accessIssue != null)
                throw new NotSupportedException(accessIssue.Message);
            List<DdfDotNetWorkerFileSnapshot> files = (brokerFiles ??
                Array.Empty<DdfDotNetWorkerFileSnapshot>()).ToList();
            DdfDotNetContractIssue brokerIssue = ValidateBrokerFiles(files,
                accessPolicy);
            if (brokerIssue != null)
                throw new NotSupportedException(brokerIssue.Message);
            List<DdfDotNetWorkerNetworkSnapshot> responses = (networkResponses ??
                Array.Empty<DdfDotNetWorkerNetworkSnapshot>()).ToList();
            DdfDotNetContractIssue networkIssue = ValidateNetworkResponses(
                responses, accessPolicy);
            if (networkIssue != null)
                throw new NotSupportedException(networkIssue.Message);
            List<string> liveResources = (liveNetworkResources ??
                Array.Empty<string>()).ToList();
            DdfDotNetContractIssue liveNetworkIssue =
                ValidateLiveNetworkResources(liveResources, accessPolicy);
            if (liveNetworkIssue != null)
                throw new NotSupportedException(liveNetworkIssue.Message);
            List<string> input = (bufferedInput ?? Array.Empty<string>())
                .ToList();
            DdfDotNetContractIssue inputIssue = ValidateBufferedInput(input);
            if (inputIssue != null)
                throw new NotSupportedException(inputIssue.Message);
            DdfDotNetContractIssue interactiveInputIssue =
                ValidateInteractiveInput(interactiveInput, accessPolicy);
            if (interactiveInputIssue != null)
                throw new NotSupportedException(interactiveInputIssue.Message);
            DdfDotNetContractIssue debugIssue = ValidateDebugPlan(debugPlan);
            if (debugIssue != null)
                throw new NotSupportedException(debugIssue.Message);
            string[] allowed = accessPolicy.AllowedBrokerCapabilities
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!bundle.RequiredHostCapabilities.SequenceEqual(allowed) ||
                !environment.HostCapabilities.SequenceEqual(allowed))
                throw new NotSupportedException(
                    "Capability del bundle, ambiente e broker non coincidono.");
            return new DdfDotNetWorkerRequest
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
                EntryModuleIdentity = bundle.EntryModuleIdentity,
                PackageManifestJson = System.Text.Encoding.UTF8.GetString(
                    bundle.GetPackageManifestBytes()),
                Artifacts = bundle.Artifacts.Select(item =>
                    new DdfDotNetWorkerArtifact
                    {
                        ModuleIdentity = item.Identity,
                        AssemblyPath = item.AssemblyPath,
                        PortablePdbPath = item.PortablePdbPath,
                        SourceMapPath = item.SourceMapPath,
                        ManifestPath = item.ManifestPath,
                        AssemblyBytes = item.GetAssemblyBytes(),
                        PortablePdbBytes = item.GetPortablePdbBytes(),
                        SourceMapBytes = item.GetSourceMapBytes(),
                        ManifestBytes = item.GetManifestBytes()
                    }).ToList(),
                ReferencePaths = referenceSet.References.Where(item =>
                        !bundle.PackagedReferences.Any(packaged =>
                            packaged.AssemblyIdentity == item.AssemblyName))
                    .Select(item => item.Path).ToList(),
                PackagedReferences = bundle.PackagedReferences.Select(item =>
                    new DdfDotNetWorkerPackagedReference
                    {
                        AssemblyIdentity = item.AssemblyIdentity,
                        Sha256 = item.Sha256,
                        Size = item.Size,
                        AssemblyBytes = item.GetAssemblyBytes()
                    }).ToList(),
                TargetFramework = environment.TargetFramework,
                RuntimeAbiVersion = environment.RuntimeAbiVersion,
                RuntimeIdentifier = environment.RuntimeIdentifier,
                HostCapabilities = environment.HostCapabilities.OrderBy(item =>
                    item, StringComparer.Ordinal).ToList(),
                AccessPolicy = accessPolicy,
                BrokerFiles = files,
                BrokerNetworkResponses = responses,
                LiveNetworkResources = liveResources,
                BufferedInput = input,
                InteractiveInput = interactiveInput,
                DebugPlan = debugPlan,
                MaxInstructions = maxInstructions
            };
        }

        public static string SerializeRequest(DdfDotNetWorkerRequest request)
        {
            string json = JsonSerializer.Serialize(request ??
                throw new ArgumentNullException(nameof(request)), JsonOptions);
            if (json.Length > MaxRequestCharacters)
                throw new ArgumentException(
                    "Richiesta worker oltre il limite IPC.", nameof(request));
            return json;
        }

        public static string SerializeResponse(DdfDotNetWorkerResponse response)
        {
            string json = JsonSerializer.Serialize(response ??
                throw new ArgumentNullException(nameof(response)), JsonOptions);
            if (json.Length > MaxResponseCharacters)
                throw new ArgumentException(
                    "Risposta worker oltre il limite IPC.", nameof(response));
            return json;
        }

        public static DdfDotNetWorkerRequest DeserializeRequest(string json) =>
            JsonSerializer.Deserialize<DdfDotNetWorkerRequest>(
                json ?? string.Empty, JsonOptions);

        public static DdfDotNetWorkerResponse DeserializeResponse(string json) =>
            JsonSerializer.Deserialize<DdfDotNetWorkerResponse>(
                json ?? string.Empty, JsonOptions);

        public static DdfDotNetWorkerPackageResult Import(
            DdfDotNetWorkerRequest request,
            bool reducedUserTokenApplied = false)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (request == null || request.Format !=
                    DdfDotNetWorkerRequest.FormatValue ||
                request.ProtocolVersion !=
                    DdfDotNetWorkerRequest.ProtocolVersionValue ||
                string.IsNullOrWhiteSpace(request.CorrelationId) ||
                string.IsNullOrWhiteSpace(request.EntryModuleIdentity) ||
                request.MaxInstructions <= 0)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC530",
                    "Richiesta worker assente o non supportata."));
                return new DdfDotNetWorkerPackageResult(null, issues);
            }
            try
            {
                DdfDotNetContractIssue accessIssue = ValidateAccessPolicy(
                    request.AccessPolicy);
                if (accessIssue != null)
                {
                    issues.Add(accessIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                if (request.AccessPolicy.RequireReducedUserToken &&
                    !reducedUserTokenApplied)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC537",
                        "La policy richiede un token utente ridotto non attestato."));
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue brokerIssue = ValidateBrokerFiles(
                    request.BrokerFiles, request.AccessPolicy);
                if (brokerIssue != null)
                {
                    issues.Add(brokerIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue inputIssue = ValidateBufferedInput(
                    request.BufferedInput);
                if (inputIssue != null)
                {
                    issues.Add(inputIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue interactiveInputIssue =
                    ValidateInteractiveInput(request.InteractiveInput,
                        request.AccessPolicy);
                if (interactiveInputIssue != null)
                {
                    issues.Add(interactiveInputIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue networkIssue = ValidateNetworkResponses(
                    request.BrokerNetworkResponses, request.AccessPolicy);
                if (networkIssue != null)
                {
                    issues.Add(networkIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue liveNetworkIssue =
                    ValidateLiveNetworkResources(request.LiveNetworkResources,
                        request.AccessPolicy);
                if (liveNetworkIssue != null)
                {
                    issues.Add(liveNetworkIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                DdfDotNetContractIssue debugIssue = ValidateDebugPlan(
                    request.DebugPlan);
                if (debugIssue != null)
                {
                    issues.Add(debugIssue);
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                string[] allowed = request.AccessPolicy
                    .AllowedBrokerCapabilities.OrderBy(item => item,
                        StringComparer.Ordinal).ToArray();
                if (!(request.HostCapabilities ?? new List<string>())
                        .SequenceEqual(allowed))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC539",
                        "Capability ambiente e broker non coincidono."));
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                List<DdfDotNetPackagedReference> packagedReferences =
                    ImportPackagedReferences(request.PackagedReferences,
                        issues);
                if (issues.Count != 0)
                    return new DdfDotNetWorkerPackageResult(null, issues);
                DdfDotNetReferenceSet references = DdfDotNetReferenceSet.Create(
                    request.ReferencePaths ?? new List<string>(),
                    packagedReferences.Select(item =>
                        DdfDotNetReferenceDescriptor.FromImage(
                            "bundle://" + item.Sha256 + "/reference.dll",
                            item.GetAssemblyBytes())));
                var emitted = new List<DdfDotNetEmittedArtifact>();
                foreach (DdfDotNetWorkerArtifact item in request.Artifacts ??
                         new List<DdfDotNetWorkerArtifact>())
                {
                    DdfDotNetContractLoadResult<DdfDotNetArtifactManifest>
                        manifest = DdfDotNetArtifactContract.DeserializeManifest(
                            System.Text.Encoding.UTF8.GetString(
                                item.ManifestBytes ?? Array.Empty<byte>()));
                    DdfDotNetContractLoadResult<DdfDotNetSourceMap> map =
                        DdfDotNetArtifactContract.DeserializeSourceMap(
                            System.Text.Encoding.UTF8.GetString(
                                item.SourceMapBytes ?? Array.Empty<byte>()));
                    issues.AddRange(manifest.Issues);
                    issues.AddRange(map.Issues);
                    if (!manifest.Success || !map.Success) continue;
                    emitted.Add(new DdfDotNetEmittedArtifact(
                        item.ModuleIdentity, item.AssemblyPath,
                        item.PortablePdbPath, item.SourceMapPath,
                        item.ManifestPath, item.AssemblyBytes,
                        item.PortablePdbBytes, item.SourceMapBytes,
                        item.ManifestBytes, manifest.Value, map.Value));
                }
                if (issues.Count != 0 || emitted.Count == 0)
                    return new DdfDotNetWorkerPackageResult(null, issues);
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker.Link(
                    request.EntryModuleIdentity,
                    emitted.Select(item => item.Manifest));
                issues.AddRange(linked.Issues);
                if (!linked.Success)
                    return new DdfDotNetWorkerPackageResult(null, issues);
                if (!linked.Program.RequiredHostCapabilities.SequenceEqual(
                        allowed))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC539",
                        "Capability del bundle e broker non coincidono."));
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                var emission = new DdfDotNetArtifactEmissionResult(emitted, null);
                DdfDotNetLinkedBundleResult bundle =
                    DdfDotNetLinkedBundleBuilder.CreateFromPackagedReferences(
                        linked.Program, emission, references,
                        packagedReferences);
                issues.AddRange(bundle.Issues);
                if (!bundle.Success || !string.Equals(
                        request.PackageManifestJson,
                        System.Text.Encoding.UTF8.GetString(bundle.Bundle
                            .GetPackageManifestBytes()), StringComparison.Ordinal))
                {
                    if (bundle.Success)
                        issues.Add(new DdfDotNetContractIssue("DDFC530",
                            "Package IPC diverso dal bundle ricostruito."));
                    return new DdfDotNetWorkerPackageResult(null, issues);
                }
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine,
                    request.TargetFramework, request.RuntimeAbiVersion,
                    request.RuntimeIdentifier, request.HostCapabilities);
                return new DdfDotNetWorkerPackageResult(
                    new DdfDotNetWorkerPackage(bundle.Bundle, references,
                        environment, request.MaxInstructions,
                        request.AccessPolicy, request.BrokerFiles,
                        request.BufferedInput, request.DebugPlan,
                        request.BrokerNetworkResponses,
                        request.LiveNetworkResources,
                        request.InteractiveInput), issues);
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is JsonException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC530",
                    "Richiesta worker non valida: " + exception.Message));
                return new DdfDotNetWorkerPackageResult(null, issues);
            }
        }

        private static List<DdfDotNetPackagedReference>
            ImportPackagedReferences(
                IEnumerable<DdfDotNetWorkerPackagedReference> values,
                ICollection<DdfDotNetContractIssue> issues)
        {
            var result = new List<DdfDotNetPackagedReference>();
            List<DdfDotNetWorkerPackagedReference> references = (values ??
                Array.Empty<DdfDotNetWorkerPackagedReference>()).ToList();
            if (references.Count > 64 || references.Sum(item =>
                    item?.AssemblyBytes?.LongLength ?? 0) > 32L * 1024 * 1024)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC552",
                    "Payload reference gestite oltre i limiti del worker."));
                return result;
            }
            foreach (DdfDotNetWorkerPackagedReference item in references)
            {
                byte[] bytes = item?.AssemblyBytes ?? Array.Empty<byte>();
                if (item == null || string.IsNullOrWhiteSpace(
                        item.AssemblyIdentity) || item.Size <= 0 ||
                    bytes.LongLength != item.Size || item.Sha256?.Length != 64 ||
                    DdfDotNetArtifactContract.Hash(bytes) != item.Sha256)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC552",
                        "Payload reference gestita non valido."));
                    continue;
                }
                DdfDotNetReferenceDescriptor descriptor =
                    DdfDotNetReferenceDescriptor.FromImage(
                        "bundle://" + item.Sha256 + "/reference.dll", bytes);
                if (descriptor.AssemblyName != item.AssemblyIdentity)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC552",
                        "Identità della reference gestita non corrispondente: " +
                        item.AssemblyIdentity + " != " +
                        descriptor.AssemblyName + "."));
                    continue;
                }
                result.Add(new DdfDotNetPackagedReference(
                    item.AssemblyIdentity, item.Sha256, item.Size, bytes));
            }
            if (result.Select(item => item.AssemblyIdentity).Distinct(
                    StringComparer.Ordinal).Count() != result.Count)
                issues.Add(new DdfDotNetContractIssue("DDFC552",
                    "Payload reference gestite duplicate."));
            return result;
        }

        private static DdfDotNetContractIssue ValidateAccessPolicy(
            DdfDotNetWorkerAccessPolicy policy)
        {
            if (policy == null || policy.Format !=
                    DdfDotNetWorkerAccessPolicy.FormatValue ||
                policy.PolicyVersion !=
                    DdfDotNetWorkerAccessPolicy.PolicyVersionValue)
                return new DdfDotNetContractIssue("DDFC536",
                    "Policy di accesso worker assente o non supportata.");
            if (!Enum.IsDefined(policy.FileSystemAccess) ||
                !Enum.IsDefined(policy.NetworkAccess) ||
                !Enum.IsDefined(policy.ConsoleInputAccess))
                return new DdfDotNetContractIssue("DDFC536",
                    "Il protocollo worker 1.9 richiede modalità di accesso " +
                    "filesystem e rete riconosciute.");
            string[] capabilities = (policy.AllowedBrokerCapabilities ??
                    new List<string>()).ToArray();
            if (capabilities.Distinct(StringComparer.Ordinal).Count() !=
                    capabilities.Length || capabilities.Any(item => item !=
                        DdfDotNetWorkerBrokerCapabilities.ReadText && item !=
                        DdfDotNetWorkerBrokerCapabilities.WriteText && item !=
                        DdfDotNetWorkerBrokerCapabilities.ReadNetworkResponse &&
                        item != DdfDotNetWorkerBrokerCapabilities
                            .FetchNetworkResponse))
                return new DdfDotNetContractIssue("DDFC536",
                    "Elenco capability broker non valido.");
            var granted = capabilities.ToHashSet(StringComparer.Ordinal);
            bool read = policy.FileSystemAccess ==
                DdfDotNetWorkerFileSystemAccess.BrokeredRead;
            bool readWrite = policy.FileSystemAccess ==
                DdfDotNetWorkerFileSystemAccess.BrokeredReadWrite;
            bool hasRead = granted.Contains(
                DdfDotNetWorkerBrokerCapabilities.ReadText);
            bool hasWrite = granted.Contains(
                DdfDotNetWorkerBrokerCapabilities.WriteText);
            bool hasNetwork = granted.Contains(
                    DdfDotNetWorkerBrokerCapabilities.ReadNetworkResponse) ||
                granted.Contains(DdfDotNetWorkerBrokerCapabilities
                    .FetchNetworkResponse);
            bool validFileCapabilities = policy.FileSystemAccess switch
            {
                DdfDotNetWorkerFileSystemAccess.None => !hasRead && !hasWrite,
                DdfDotNetWorkerFileSystemAccess.BrokeredRead =>
                    hasRead && !hasWrite,
                DdfDotNetWorkerFileSystemAccess.BrokeredReadWrite => hasWrite,
                _ => false
            };
            bool validNetworkCapabilities = policy.NetworkAccess switch
            {
                DdfDotNetWorkerNetworkAccess.None => !hasNetwork,
                DdfDotNetWorkerNetworkAccess.BrokeredClient => hasNetwork,
                _ => false
            };
            if (!validFileCapabilities || !validNetworkCapabilities)
                return new DdfDotNetContractIssue("DDFC536",
                    "Le capability non corrispondono alla policy broker.");
            return null;
        }

        private static DdfDotNetContractIssue ValidateBrokerFiles(
            IEnumerable<DdfDotNetWorkerFileSnapshot> snapshots,
            DdfDotNetWorkerAccessPolicy policy)
        {
            DdfDotNetWorkerFileSnapshot[] files = (snapshots ??
                Array.Empty<DdfDotNetWorkerFileSnapshot>()).ToArray();
            bool brokered = policy?.FileSystemAccess ==
                    DdfDotNetWorkerFileSystemAccess.BrokeredRead ||
                policy?.FileSystemAccess ==
                    DdfDotNetWorkerFileSystemAccess.BrokeredReadWrite;
            bool writable = policy?.FileSystemAccess ==
                DdfDotNetWorkerFileSystemAccess.BrokeredReadWrite;
            if ((!brokered && files.Length != 0) || files.Length > 32 ||
                files.Select(item => item?.LogicalName).Distinct(
                    StringComparer.Ordinal).Count() != files.Length)
                return new DdfDotNetContractIssue("DDFC539",
                    "Elenco snapshot file broker non valido.");
            long total = 0;
            foreach (DdfDotNetWorkerFileSnapshot file in files)
            {
                if (!ValidLogicalName(file?.LogicalName))
                    return new DdfDotNetContractIssue("DDFC539",
                        "Nome logico file broker non valido.");
                byte[] bytes = Encoding.UTF8.GetBytes(file.Content ?? string.Empty);
                total += bytes.Length;
                string hash = Convert.ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant();
                if (file.Writable && (!writable || file.MaxWriteBytes <= 0 ||
                        file.MaxWriteBytes > 1024 * 1024) ||
                    !file.Writable && file.MaxWriteBytes != 0 ||
                    bytes.Length > 1024 * 1024 || total > 4L * 1024 * 1024 ||
                    !string.Equals(hash, file.Sha256,
                        StringComparison.Ordinal))
                    return new DdfDotNetContractIssue("DDFC539",
                        "Contenuto, hash o budget file broker non valido.");
            }
            return null;
        }

        private static bool ValidLogicalName(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
            value != "." && value != ".." && !value.Contains("..",
                StringComparison.Ordinal) && value.All(character =>
                    char.IsLetterOrDigit(character) || character == '_' ||
                    character == '-' || character == '.');

        private static DdfDotNetContractIssue ValidateNetworkResponses(
            IEnumerable<DdfDotNetWorkerNetworkSnapshot> snapshots,
            DdfDotNetWorkerAccessPolicy policy)
        {
            DdfDotNetWorkerNetworkSnapshot[] values = (snapshots ??
                Array.Empty<DdfDotNetWorkerNetworkSnapshot>()).ToArray();
            bool brokered = policy?.NetworkAccess ==
                DdfDotNetWorkerNetworkAccess.BrokeredClient;
            if ((!brokered && values.Length != 0) || values.Length > 32 ||
                values.Select(item => item?.LogicalName).Distinct(
                    StringComparer.Ordinal).Count() != values.Length)
                return new DdfDotNetContractIssue("DDFC544",
                    "Elenco risposte di rete broker non valido.");
            long total = 0;
            foreach (DdfDotNetWorkerNetworkSnapshot value in values)
            {
                if (!ValidLogicalName(value?.LogicalName) ||
                    string.IsNullOrWhiteSpace(value.MediaType) ||
                    value.MediaType.Length > 128 ||
                    value.MediaType.Any(character => character < 0x20 ||
                        character > 0x7e))
                    return new DdfDotNetContractIssue("DDFC544",
                        "Metadati della risposta di rete broker non validi.");
                byte[] bytes = Encoding.UTF8.GetBytes(value.Content ??
                    string.Empty);
                total += bytes.Length;
                string hash = Convert.ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant();
                if (bytes.Length > 1024 * 1024 ||
                    total > 4L * 1024 * 1024 || !string.Equals(hash,
                        value.Sha256, StringComparison.Ordinal))
                    return new DdfDotNetContractIssue("DDFC544",
                        "Contenuto, hash o budget della risposta di rete " +
                        "broker non valido.");
            }
            return null;
        }

        private static DdfDotNetContractIssue ValidateBufferedInput(
            IEnumerable<string> values)
        {
            string[] input = (values ?? Array.Empty<string>()).ToArray();
            if (input.Length > 1024 || input.Any(item => item == null))
                return new DdfDotNetContractIssue("DDFC542",
                    "Coda input worker non valida.");
            long total = 0;
            foreach (string item in input)
            {
                int bytes = Encoding.UTF8.GetByteCount(item);
                total += bytes;
                if (bytes > 64 * 1024 || total > 1024 * 1024)
                    return new DdfDotNetContractIssue("DDFC542",
                        "Coda input worker oltre il budget consentito.");
            }
            return null;
        }

        private static DdfDotNetContractIssue ValidateInteractiveInput(
            DdfDotNetWorkerInteractiveInputOptions options,
            DdfDotNetWorkerAccessPolicy policy)
        {
            bool enabled = policy?.ConsoleInputAccess ==
                DdfDotNetWorkerConsoleInputAccess.BrokeredInteractive;
            if (!enabled && options != null || enabled && options == null ||
                options != null && (options.MaxRequests <= 0 ||
                    options.MaxRequests > 256 || options.MaxValueBytes <= 0 ||
                    options.MaxValueBytes > 256 * 1024))
                return new DdfDotNetContractIssue("DDFC549",
                    "Input console interattivo non valido o non autorizzato.");
            return null;
        }

        private static DdfDotNetContractIssue ValidateLiveNetworkResources(
            IEnumerable<string> resources,
            DdfDotNetWorkerAccessPolicy policy)
        {
            string[] values = (resources ?? Array.Empty<string>()).ToArray();
            bool capability = policy?.AllowedBrokerCapabilities?.Contains(
                DdfDotNetWorkerBrokerCapabilities.FetchNetworkResponse,
                StringComparer.Ordinal) == true;
            if ((!capability && values.Length != 0) ||
                capability && values.Length == 0 || values.Length > 32 ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length ||
                values.Any(value => !ValidLogicalName(value)))
                return new DdfDotNetContractIssue("DDFC547",
                    "Elenco risorse di rete live non valido o non autorizzato.");
            return null;
        }

        private static DdfDotNetContractIssue ValidateDebugPlan(
            DdfDotNetWorkerDebugPlan plan)
        {
            if (plan == null) return null;
            DdfDotNetWorkerDebugBreakpoint[] breakpoints = (plan.Breakpoints ??
                new List<DdfDotNetWorkerDebugBreakpoint>()).ToArray();
            DdfDotNetDebugCommand[] commands = (plan.Commands ??
                new List<DdfDotNetDebugCommand>()).ToArray();
            if (breakpoints.Length == 0 || breakpoints.Length > 128 ||
                commands.Length > 256 || plan.MaxPauses <= 0 ||
                plan.MaxPauses > 256 || commands.Length > plan.MaxPauses ||
                plan.PauseTimeoutMilliseconds < 1 ||
                plan.PauseTimeoutMilliseconds > 30000 ||
                plan.Interactive && commands.Length != 0 ||
                commands.Any(command => !Enum.IsDefined(command) ||
                    !plan.Interactive &&
                    command == DdfDotNetDebugCommand.Stop))
                return new DdfDotNetContractIssue("DDFC543",
                    "Piano debug worker non valido o oltre quota.");
            foreach (DdfDotNetWorkerDebugBreakpoint breakpoint in breakpoints)
            {
                string path = breakpoint?.SourcePath?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(path) || path.Length > 512 ||
                    Path.IsPathRooted(path) || path.Split('/').Any(segment =>
                        segment.Length == 0 || segment == "." ||
                        segment == "..") || breakpoint.Start < 0 ||
                    breakpoint.Length < 1)
                    return new DdfDotNetContractIssue("DDFC543",
                        "Breakpoint del piano debug worker non valido.");
            }
            return null;
        }
    }
}
