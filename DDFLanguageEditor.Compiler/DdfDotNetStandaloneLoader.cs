using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetStandaloneLoadResult
    {
        internal DdfDotNetStandaloneLoadResult(
            DdfDotNetStandaloneSession session,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Session = session;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetStandaloneSession Session { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Session != null && Issues.Count == 0;
    }

    /// <summary>
    /// One-shot in-process session for verified standalone/CLI artifacts.
    /// Untrusted packages still require the future out-of-process runner.
    /// </summary>
    public sealed class DdfDotNetStandaloneSession : IDisposable
    {
        private const string ModuleInitializerName =
            "DdfGenerated_Helper_ModuleInitialize";
        private const string StateCaptureName =
            "DdfGenerated_Helper_StateCapture";
        private const string StateApplyName =
            "DdfGenerated_Helper_StateApply";
        private DdfStandaloneAssemblyLoadContext loadContext;
        private readonly Dictionary<string, Assembly> assemblies;
        private readonly string entryModuleIdentity;
        private readonly IReadOnlyList<string> moduleOrder;
        private readonly IReadOnlyDictionary<string, DdfDotNetSourceMap> maps;
        private readonly string languageId;
        private readonly string languagePackageVersion;
        private DdfPersistedStateSnapshot stateToRestore;
        private DdfPersistedStateSnapshot capturedState;
        private int executed;
        private int disposed;

        internal DdfDotNetStandaloneSession(DdfDotNetLinkedProgram program,
            IReadOnlyList<DdfDotNetEmittedArtifact> artifacts,
            IReadOnlyDictionary<string, DdfDotNetSourceMap> maps,
            DdfDotNetLinkedPackageManifest package,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences)
        {
            entryModuleIdentity = program.EntryModuleIdentity;
            moduleOrder = program.Artifacts.Select(item => item.Identity)
                .ToList().AsReadOnly();
            this.maps = maps;
            DdfDotNetSourceMap entryMap = maps[entryModuleIdentity];
            languageId = entryMap.LanguageProfileId;
            languagePackageVersion = entryMap.LanguageProfileVersion;
            loadContext = new DdfStandaloneAssemblyLoadContext(artifacts,
                package.ManagedReferences, packagedReferences);
            UnloadToken = new WeakReference(loadContext, trackResurrection: false);
            assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            try
            {
                foreach (DdfDotNetLinkedArtifact module in program.Artifacts)
                {
                    DdfDotNetEmittedArtifact artifact = artifacts.Single(item =>
                        item.ModuleIdentity == module.Identity);
                    Assembly assembly = loadContext.LoadArtifact(artifact);
                    string actualName = assembly.GetName().Name ?? string.Empty;
                    if (!string.Equals(actualName, module.AssemblyName,
                            StringComparison.Ordinal))
                        throw new FileLoadException(
                            "Identità assembly caricata inattesa: " + actualName + ".");
                    assemblies.Add(module.Identity, assembly);
                }
            }
            catch
            {
                assemblies.Clear();
                loadContext.Unload();
                loadContext = null;
                Volatile.Write(ref disposed, 1);
                throw;
            }
        }

        public string EntryModuleIdentity => entryModuleIdentity;
        public IReadOnlyList<string> ModuleOrder => moduleOrder;
        public bool IsDisposed => Volatile.Read(ref disposed) != 0;
        public bool HasExecuted => Volatile.Read(ref executed) != 0;
        public WeakReference UnloadToken { get; }

        public void RestoreState(DdfPersistedStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (HasExecuted)
                throw new InvalidOperationException(
                    "Lo stato deve essere ripristinato prima dell'esecuzione.");
            if (!string.Equals(snapshot.LanguageId, languageId,
                    StringComparison.Ordinal) ||
                !string.Equals(snapshot.LanguagePackageVersion,
                    languagePackageVersion, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Lo snapshot state appartiene a un pacchetto linguaggio differente.");
            stateToRestore = snapshot;
        }

        public DdfPersistedStateSnapshot CaptureState()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (capturedState == null)
                throw new InvalidOperationException(
                    "Nessuno stato completato è disponibile per questa sessione.");
            return capturedState;
        }

        public DdfDotNetCooperativeDebugger CreateDebugger()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            return new DdfDotNetCooperativeDebugger(moduleOrder.Select(identity =>
                maps[identity]));
        }

        public DdfRuntimeExecutionResult Execute(DdfRuntimeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (Interlocked.Exchange(ref executed, 1) != 0)
                throw new InvalidOperationException(
                    "La sessione standalone è monouso; creare una nuova sessione per una nuova esecuzione.");
            DdfRuntimeExecutionResult result = context.Execute(runtime =>
            {
                InitializeAndRestore(runtime);
                DdfDotNetSourceMap entryMap = maps[entryModuleIdentity];
                DdfDotNetDebugSymbol main = entryMap.Symbols.SingleOrDefault(
                    symbol => string.Equals(symbol.Name, "main",
                                  StringComparison.Ordinal) &&
                              string.Equals(symbol.Kind, "Function",
                                  StringComparison.OrdinalIgnoreCase)) ??
                    throw new MissingMethodException(
                        "Entry point DDF main non presente nella source map verificata.");
                MethodInfo entry = FindMethod(assemblies[entryModuleIdentity],
                    main.StorageName);
                return Invoke(entry, runtime);
            });
            capturedState = CaptureCurrentState();
            return result;
        }

        public ValueTask<DdfRuntimeExecutionResult> ExecuteAsync(
            DdfRuntimeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (Interlocked.Exchange(ref executed, 1) != 0)
                throw new InvalidOperationException(
                    "La sessione standalone è monouso; creare una nuova sessione per una nuova esecuzione.");
            return ExecuteAndCaptureAsync(context);
        }

        private async ValueTask<DdfRuntimeExecutionResult> ExecuteAndCaptureAsync(
            DdfRuntimeContext context)
        {
            DdfRuntimeExecutionResult result = await context.ExecuteAsync(
                async runtime =>
            {
                foreach (string identity in moduleOrder)
                    await InvokeAsync(FindMethod(assemblies[identity],
                        ModuleInitializerName), runtime).ConfigureAwait(false);
                ApplyRestoredState();
                DdfDotNetSourceMap entryMap = maps[entryModuleIdentity];
                DdfDotNetDebugSymbol main = entryMap.Symbols.SingleOrDefault(
                    symbol => string.Equals(symbol.Name, "main",
                                  StringComparison.Ordinal) &&
                              string.Equals(symbol.Kind, "Function",
                                  StringComparison.OrdinalIgnoreCase)) ??
                    throw new MissingMethodException(
                        "Entry point DDF main non presente nella source map verificata.");
                MethodInfo entry = FindMethod(assemblies[entryModuleIdentity],
                    main.StorageName);
                return await InvokeAsync(entry, runtime).ConfigureAwait(false);
            }).ConfigureAwait(false);
            capturedState = CaptureCurrentState();
            return result;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            assemblies.Clear();
            DdfStandaloneAssemblyLoadContext context = Interlocked.Exchange(
                ref loadContext, null);
            context?.Unload();
        }

        private static MethodInfo FindMethod(Assembly assembly, string name)
        {
            MethodInfo[] matches = assembly.GetTypes().SelectMany(type =>
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Static))
                .Where(method => method.Name == name &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                        typeof(DdfRuntimeContext))
                .ToArray();
            if (matches.Length != 1)
                throw new MissingMethodException("Metodo generato " + name +
                    " assente o ambiguo nell'assembly " +
                    assembly.GetName().Name + ".");
            return matches[0];
        }

        private void InitializeAndRestore(DdfRuntimeContext runtime)
        {
            foreach (string identity in moduleOrder)
                Invoke(FindMethod(assemblies[identity], ModuleInitializerName),
                    runtime);
            ApplyRestoredState();
        }

        private void ApplyRestoredState()
        {
            if (stateToRestore == null) return;
            HashSet<string> knownKeys = CaptureStateEntries()
                .Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            string unknown = stateToRestore.Entries.Select(item => item.Key)
                .FirstOrDefault(key => !knownKeys.Contains(key));
            if (unknown != null)
                throw new InvalidOperationException(
                    "Lo snapshot contiene una chiave state non presente nel programma: " +
                    unknown + ".");
            foreach (string identity in moduleOrder)
                InvokeStateApply(FindStateMethod(assemblies[identity],
                    StateApplyName, typeof(IReadOnlyDictionary<string,
                        DdfPersistedStateEntry>)), stateToRestore.ByKey);
        }

        private DdfPersistedStateSnapshot CaptureCurrentState() =>
            new DdfPersistedStateSnapshot(
                DdfPersistedStateSnapshot.CurrentSchemaVersion, languageId,
                languagePackageVersion, CaptureStateEntries());

        private IReadOnlyList<DdfPersistedStateEntry> CaptureStateEntries()
        {
            var result = new List<DdfPersistedStateEntry>();
            foreach (string identity in moduleOrder)
            {
                MethodInfo method = FindStateMethod(assemblies[identity],
                    StateCaptureName);
                object value;
                try { value = method.Invoke(null, Array.Empty<object>()); }
                catch (TargetInvocationException exception)
                    when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
                if (value is not IEnumerable<DdfPersistedStateEntry> entries)
                    throw new InvalidOperationException(
                        "Il metodo state generato ha restituito un valore non valido.");
                result.AddRange(entries);
            }
            return result.AsReadOnly();
        }

        private static MethodInfo FindStateMethod(Assembly assembly, string name,
            Type parameterType = null)
        {
            MethodInfo[] matches = assembly.GetTypes().SelectMany(type =>
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Static))
                .Where(method => method.Name == name &&
                    (parameterType == null
                        ? method.GetParameters().Length == 0
                        : method.GetParameters().Length == 1 &&
                          method.GetParameters()[0].ParameterType == parameterType))
                .ToArray();
            if (matches.Length != 1)
                throw new MissingMethodException("Metodo state generato " + name +
                    " assente o ambiguo nell'assembly " +
                    assembly.GetName().Name + ".");
            return matches[0];
        }

        private static void InvokeStateApply(MethodInfo method,
            IReadOnlyDictionary<string, DdfPersistedStateEntry> entries)
        {
            try { method.Invoke(null, new object[] { entries }); }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static object Invoke(MethodInfo method,
            DdfRuntimeContext context)
        {
            object invocation;
            try { invocation = method.Invoke(null, new object[] { context }); }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (invocation is not Task task) return invocation;
            task.GetAwaiter().GetResult();
            PropertyInfo result = task.GetType().GetProperty("Result",
                BindingFlags.Public | BindingFlags.Instance);
            return result?.GetValue(task);
        }

        private static async ValueTask<object> InvokeAsync(MethodInfo method,
            DdfRuntimeContext context)
        {
            object invocation;
            try { invocation = method.Invoke(null, new object[] { context }); }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (invocation is not Task task) return invocation;
            try { await task.ConfigureAwait(false); }
            catch (Exception exception)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }
            PropertyInfo result = task.GetType().GetProperty("Result",
                BindingFlags.Public | BindingFlags.Instance);
            return result?.GetValue(task);
        }
    }

    public static class DdfDotNetStandaloneLoader
    {
        public static DdfDotNetStandaloneLoadResult Load(
            DdfDotNetLinkedBundle bundle,
            DdfDotNetReferenceSet referenceSet,
            DdfDotNetRuntimeEnvironment environment,
            DdfDotNetPeVerificationLimits verificationLimits = null,
            DdfDotNetArtifactLoadLimits artifactLimits = null)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (bundle == null || referenceSet == null || environment == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC520",
                    "Bundle, reference set e ambiente standalone sono richiesti."));
                return new DdfDotNetStandaloneLoadResult(null, issues);
            }
            if (environment.HostKind == DdfDotNetArtifactHostKind.Embedded)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC520",
                    "Il loader compilato non è disponibile in modalità embedded."));
                return new DdfDotNetStandaloneLoadResult(null, issues);
            }
            DdfDotNetContractLoadResult<DdfDotNetLinkedPackageManifest> package =
                DdfDotNetLinkedPackageContract.Deserialize(Encoding.UTF8.GetString(
                    bundle.GetPackageManifestBytes()));
            issues.AddRange(package.Issues);
            if (!package.Success) return new DdfDotNetStandaloneLoadResult(null,
                issues);
            byte[] expectedPackage = Encoding.UTF8.GetBytes(
                DdfDotNetLinkedPackageContract.Serialize(
                    DdfDotNetLinkedPackageContract.Create(bundle, referenceSet)));
            if (!expectedPackage.SequenceEqual(bundle.GetPackageManifestBytes()) ||
                DdfDotNetArtifactContract.Hash(expectedPackage) !=
                    bundle.PackageManifestSha256 ||
                package.Value.BundleFingerprint != bundle.Fingerprint ||
                package.Value.ReferenceSetFingerprint != referenceSet.Fingerprint)
                issues.Add(new DdfDotNetContractIssue("DDFC520",
                    "Manifest package, bundle e reference set non coincidono."));
            VerifyReferences(referenceSet, bundle.PackagedReferences, issues);
            if (issues.Count != 0)
                return new DdfDotNetStandaloneLoadResult(null, issues);

            var artifacts = new List<DdfDotNetEmittedArtifact>();
            var maps = new Dictionary<string, DdfDotNetSourceMap>(
                StringComparer.Ordinal);
            foreach (DdfDotNetLinkedBinaryArtifact binary in bundle.Artifacts)
            {
                DdfDotNetContractLoadResult<DdfDotNetArtifactManifest> manifest =
                    DdfDotNetArtifactContract.DeserializeManifest(
                        Encoding.UTF8.GetString(binary.GetManifestBytes()));
                DdfDotNetContractLoadResult<DdfDotNetSourceMap> map =
                    DdfDotNetArtifactContract.DeserializeSourceMap(
                        Encoding.UTF8.GetString(binary.GetSourceMapBytes()));
                issues.AddRange(manifest.Issues);
                issues.AddRange(map.Issues);
                if (!manifest.Success || !map.Success) continue;
                maps.Add(binary.Identity, map.Value);
                artifacts.Add(new DdfDotNetEmittedArtifact(binary.Identity,
                    binary.AssemblyPath, binary.PortablePdbPath,
                    binary.SourceMapPath, binary.ManifestPath,
                    binary.GetAssemblyBytes(), binary.GetPortablePdbBytes(),
                    binary.GetSourceMapBytes(), binary.GetManifestBytes(),
                    manifest.Value, map.Value));
            }
            if (issues.Count != 0)
                return new DdfDotNetStandaloneLoadResult(null, issues);
            DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker.Link(
                bundle.EntryModuleIdentity,
                artifacts.Select(item => item.Manifest));
            issues.AddRange(linked.Issues);
            if (!linked.Success || linked.Program.Fingerprint !=
                    bundle.ProgramFingerprint)
            {
                if (linked.Success)
                    issues.Add(new DdfDotNetContractIssue("DDFC520",
                        "Fingerprint del programma ricollegato non valido."));
                return new DdfDotNetStandaloneLoadResult(null, issues);
            }
            var available = linked.Program.Artifacts.Select(item =>
                new DdfDotNetAvailableDependency(item.ModuleId,
                    item.ModuleVersion, item.CompilationFingerprint,
                    item.AssemblyName)).ToList();
            foreach (DdfDotNetEmittedArtifact artifact in artifacts)
                issues.AddRange(DdfDotNetArtifactCompatibility.Inspect(
                    artifact.Manifest, environment, available,
                    artifact.SourceMap, artifactLimits).Issues);
            DdfDotNetPeVerificationResult verified = DdfDotNetPeVerifier.Verify(
                linked.Program, artifacts, referenceSet, verificationLimits);
            issues.AddRange(verified.Issues);
            if (issues.Count != 0)
                return new DdfDotNetStandaloneLoadResult(null, issues);
            try
            {
                return new DdfDotNetStandaloneLoadResult(
                    new DdfDotNetStandaloneSession(linked.Program, artifacts,
                        maps, package.Value, bundle.PackagedReferences), null);
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is FileLoadException ||
                                               exception is TypeLoadException ||
                                               exception is MissingMethodException ||
                                               exception is ReflectionTypeLoadException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC521",
                    "Caricamento controllato fallito: " + exception.Message));
                return new DdfDotNetStandaloneLoadResult(null, issues);
            }
        }

        private static void VerifyReferences(
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var packaged = (packagedReferences ?? Array.Empty<
                    DdfDotNetPackagedReference>()).ToDictionary(item =>
                    item.AssemblyIdentity, StringComparer.Ordinal);
            foreach (DdfDotNetReferenceDescriptor reference in
                     referenceSet.References)
            {
                if (packaged.TryGetValue(reference.AssemblyName,
                        out DdfDotNetPackagedReference payload))
                {
                    byte[] bytes = payload.GetAssemblyBytes();
                    if (payload.Size != reference.Size ||
                        payload.Sha256 != reference.Sha256 ||
                        bytes.LongLength != reference.Size ||
                        DdfDotNetArtifactContract.Hash(bytes) !=
                            reference.Sha256)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC520",
                            "Reference inclusa non coerente col lock: " +
                            reference.AssemblyName + "."));
                        return;
                    }
                    continue;
                }
                try
                {
                    var info = new FileInfo(reference.Path);
                    if (!info.Exists || info.Length != reference.Size)
                        throw new IOException("dimensione o file non disponibile");
                    using FileStream stream = File.OpenRead(reference.Path);
                    using SHA256 sha = SHA256.Create();
                    string hash = Convert.ToHexString(sha.ComputeHash(stream))
                        .ToLowerInvariant();
                    if (hash != reference.Sha256)
                        throw new IOException("hash modificato");
                }
                catch (Exception exception) when (exception is IOException ||
                                                   exception is UnauthorizedAccessException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC520",
                        "Reference non più coerente col lock: " +
                        reference.AssemblyName + " (" + exception.Message + ")."));
                    return;
                }
            }
        }
    }

    internal sealed class DdfStandaloneAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, DdfDotNetEmittedArtifact>
            artifacts;
        private readonly IReadOnlySet<string> allowedReferences;
        private readonly IReadOnlyDictionary<string,
            DdfDotNetPackagedReference> packagedReferences;

        internal DdfStandaloneAssemblyLoadContext(
            IEnumerable<DdfDotNetEmittedArtifact> artifacts,
            IEnumerable<DdfDotNetLinkedPackageReference> references,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences)
            : base("DDF standalone " + Guid.NewGuid().ToString("N"),
                isCollectible: true)
        {
            this.artifacts = artifacts.ToDictionary(
                item => item.Manifest.AssemblyName,
                StringComparer.OrdinalIgnoreCase);
            allowedReferences = new HashSet<string>((references ??
                Array.Empty<DdfDotNetLinkedPackageReference>()).Select(item =>
                item.AssemblyIdentity), StringComparer.OrdinalIgnoreCase);
            this.packagedReferences = (packagedReferences ?? Array.Empty<
                    DdfDotNetPackagedReference>()).ToDictionary(item =>
                    new AssemblyName(item.AssemblyIdentity).Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
        }

        internal Assembly LoadArtifact(DdfDotNetEmittedArtifact artifact)
        {
            using var assembly = new MemoryStream(artifact.AssemblyBytes,
                writable: false);
            using var symbols = new MemoryStream(artifact.PortablePdbBytes,
                writable: false);
            return LoadFromStream(assembly, symbols);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (artifacts.TryGetValue(assemblyName.Name ?? string.Empty,
                    out DdfDotNetEmittedArtifact artifact))
                return LoadArtifact(artifact);
            if (packagedReferences.TryGetValue(
                    assemblyName.Name ?? string.Empty,
                    out DdfDotNetPackagedReference packaged))
            {
                if (!allowedReferences.Contains(packaged.AssemblyIdentity) ||
                    !AssemblyName.ReferenceMatchesDefinition(assemblyName,
                        new AssemblyName(packaged.AssemblyIdentity)))
                    throw new FileLoadException(
                        "Reference inclusa fuori dall'allowlist: " +
                        assemblyName.FullName + ".");
                using var stream = new MemoryStream(
                    packaged.GetAssemblyBytes(), writable: false);
                return LoadFromStream(stream);
            }
            Assembly runtime = typeof(DdfRuntimeAbi).Assembly;
            if (string.Equals(assemblyName.Name, runtime.GetName().Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!allowedReferences.Contains(assemblyName.FullName))
                    throw new FileLoadException(
                        "Runtime ABI fuori dall'allowlist: " +
                        assemblyName.FullName + ".");
                return runtime;
            }
            if (!allowedReferences.Contains(assemblyName.FullName))
                throw new FileLoadException("Assembly fuori dall'allowlist: " +
                    assemblyName.FullName + ".");
            return null;
        }
    }
}
