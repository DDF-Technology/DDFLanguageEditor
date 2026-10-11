using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetArtifactCacheResult
    {
        internal DdfDotNetArtifactCacheResult(string cacheKey,
            string entryPath, bool cacheHit, bool invalidated,
            DdfDotNetArtifactEmissionResult emission,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            CacheKey = cacheKey ?? string.Empty;
            EntryPath = entryPath ?? string.Empty;
            CacheHit = cacheHit;
            Invalidated = invalidated;
            Emission = emission;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public string CacheKey { get; }
        public string EntryPath { get; }
        public bool CacheHit { get; }
        public bool Invalidated { get; }
        public DdfDotNetArtifactEmissionResult Emission { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Emission?.Success == true && Issues.Count == 0;
    }

    /// <summary>
    /// Content-addressed cache for complete compiler bundles. A cached assembly
    /// is never loaded: every hit is reconstructed and verified against the
    /// exact compilation plan before its bytes are returned.
    /// </summary>
    public static class DdfDotNetArtifactCache
    {
        public const string FormatVersion = "1.0";
        private const long MaxManifestBytes = 8 * 1024 * 1024;
        private static readonly object CacheGate = new();

        public static string ComputeKey(DdfDotNetCompilationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            byte[] value = Encoding.UTF8.GetBytes(FormatVersion + "\n" +
                DdfDotNetArtifactManifest.SchemaVersionValue + "\n" +
                DdfDotNetSourceMap.SchemaVersionValue + "\n" +
                plan.Fingerprint + "\n");
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(value))
                .ToLowerInvariant();
        }

        public static DdfDotNetArtifactCacheResult GetOrEmit(
            DdfDotNetCompilationPlan plan, DdfClrTypeMappingDefinition mapping,
            string cacheRoot)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            var issues = new List<DdfDotNetContractIssue>();
            string key = ComputeKey(plan);
            string entryPath = ResolveEntryPath(cacheRoot, key, issues);
            if (issues.Count != 0)
                return new DdfDotNetArtifactCacheResult(key, null, false,
                    false, null, issues);

            lock (CacheGate)
            {
                DdfDotNetArtifactEmissionResult restored = Restore(plan,
                    entryPath, out bool existed);
                if (restored.Success)
                    return new DdfDotNetArtifactCacheResult(key, entryPath,
                        true, false, restored, null);

                DdfDotNetArtifactEmissionResult emitted =
                    DdfDotNetArtifactEmitter.Emit(plan, mapping);
                if (!emitted.Success)
                    return new DdfDotNetArtifactCacheResult(key, entryPath,
                        false, existed, emitted, emitted.Issues);

                DdfDotNetArtifactPublicationResult publication = Replace(
                    emitted, entryPath, existed);
                if (!publication.Success)
                    return new DdfDotNetArtifactCacheResult(key, entryPath,
                        false, existed, emitted, publication.Issues);

                DdfDotNetArtifactEmissionResult verified = Restore(plan,
                    entryPath, out _);
                if (!verified.Success)
                    return new DdfDotNetArtifactCacheResult(key, entryPath,
                        false, existed, emitted, verified.Issues);
                return new DdfDotNetArtifactCacheResult(key, entryPath,
                    false, existed, verified, null);
            }
        }

        private static DdfDotNetArtifactEmissionResult Restore(
            DdfDotNetCompilationPlan plan, string entryPath, out bool existed)
        {
            existed = Directory.Exists(entryPath) || File.Exists(entryPath);
            var issues = new List<DdfDotNetContractIssue>();
            var artifacts = new List<DdfDotNetEmittedArtifact>();
            if (!Directory.Exists(entryPath))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC412",
                    "Voce cache compilatore assente."));
                return new DdfDotNetArtifactEmissionResult(null, issues);
            }

            try
            {
                foreach (DdfDotNetCompilationWorkItem item in plan.WorkItems)
                {
                    string manifestPath = ResolveWithin(entryPath,
                        item.ManifestPath);
                    if (!File.Exists(manifestPath) ||
                        new FileInfo(manifestPath).Length > MaxManifestBytes)
                        throw new InvalidDataException(
                            item.Identity + ": manifest cache assente o eccessivo.");
                    byte[] manifestBytes = File.ReadAllBytes(manifestPath);
                    DdfDotNetContractLoadResult<DdfDotNetArtifactManifest>
                        loadedManifest = DdfDotNetArtifactContract
                            .DeserializeManifest(Encoding.UTF8.GetString(
                                manifestBytes));
                    if (!loadedManifest.Success)
                        throw new InvalidDataException(item.Identity +
                            ": manifest cache non valido.");
                    DdfDotNetArtifactManifest manifest = loadedManifest.Value;
                    if (!MatchesPlan(plan, item, manifest))
                        throw new InvalidDataException(item.Identity +
                            ": ricevuta cache incompatibile col piano.");
                    string artifactRoot = Path.GetDirectoryName(manifestPath);
                    DdfDotNetCompatibilityResult payloads =
                        DdfDotNetArtifactCompatibility.VerifyPayloads(manifest,
                            artifactRoot);
                    if (!payloads.IsCompatible)
                        throw new InvalidDataException(item.Identity +
                            ": payload cache non valido: " + string.Join(", ",
                                payloads.Issues.Select(issue => issue.Code)));

                    byte[] assemblyBytes = ReadPayload(artifactRoot, manifest,
                        DdfDotNetPayloadKind.Assembly);
                    byte[] pdbBytes = ReadPayload(artifactRoot, manifest,
                        DdfDotNetPayloadKind.PortablePdb);
                    byte[] mapBytes = ReadPayload(artifactRoot, manifest,
                        DdfDotNetPayloadKind.SourceMap);
                    DdfDotNetContractLoadResult<DdfDotNetSourceMap> loadedMap =
                        DdfDotNetArtifactContract.DeserializeSourceMap(
                            Encoding.UTF8.GetString(mapBytes));
                    if (!loadedMap.Success ||
                        DdfDotNetArtifactContract.ValidateBundle(manifest,
                            loadedMap.Value).Count != 0)
                        throw new InvalidDataException(item.Identity +
                            ": source map cache non valida.");
                    byte[] canonicalManifest = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeManifest(manifest));
                    if (!canonicalManifest.SequenceEqual(manifestBytes))
                        throw new InvalidDataException(item.Identity +
                            ": manifest cache non canonico.");

                    artifacts.Add(new DdfDotNetEmittedArtifact(item.Identity,
                        item.AssemblyPath, item.PortablePdbPath,
                        item.SourceMapPath, item.ManifestPath, assemblyBytes,
                        pdbBytes, mapBytes, manifestBytes, manifest,
                        loadedMap.Value));
                }
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is InvalidDataException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC413",
                    "Voce cache compilatore rifiutata: " + exception.Message));
            }
            return new DdfDotNetArtifactEmissionResult(
                issues.Count == 0 && artifacts.Count == plan.WorkItems.Count
                    ? artifacts : Array.Empty<DdfDotNetEmittedArtifact>(),
                issues);
        }

        private static bool MatchesPlan(DdfDotNetCompilationPlan plan,
            DdfDotNetCompilationWorkItem item,
            DdfDotNetArtifactManifest manifest)
        {
            DdfDotNetBuildReceipt receipt = manifest.BuildReceipt;
            return receipt != null &&
                receipt.CompilationPlanVersion ==
                    DdfDotNetCompilationPlan.FormatVersion &&
                receipt.CompilationPlanFingerprint == plan.Fingerprint &&
                receipt.CompilerProfileVersion ==
                    DdfDotNetCompilerProfile.FormatVersion &&
                receipt.RoslynVersion == DdfDotNetCompilerProfile.RoslynVersion &&
                receipt.CSharpLanguageVersion == DdfDotNetCompilerProfile
                    .CSharpLanguageVersion.ToString() &&
                receipt.ReferenceSetFingerprint ==
                    plan.Options.ReferenceSet?.Fingerprint &&
                manifest.ModuleId == item.Unit.ModuleId &&
                manifest.ModuleVersion == item.Unit.ModuleVersion &&
                manifest.CompilationFingerprint == item.Unit.Fingerprint &&
                manifest.AssemblyName == item.AssemblyName &&
                manifest.TargetFramework == plan.Options.TargetFramework &&
                manifest.RuntimeAbiVersion == plan.Options.RuntimeAbiVersion &&
                manifest.RuntimeIdentifier == plan.Options.RuntimeIdentifier;
        }

        private static byte[] ReadPayload(string root,
            DdfDotNetArtifactManifest manifest, DdfDotNetPayloadKind kind)
        {
            DdfDotNetPayloadDescriptor payload = manifest.Payloads
                .Single(item => item.Kind == kind);
            return File.ReadAllBytes(ResolveWithin(root, payload.Path));
        }

        private static DdfDotNetArtifactPublicationResult Replace(
            DdfDotNetArtifactEmissionResult emission, string entryPath,
            bool replaceExisting)
        {
            string parent = Path.GetDirectoryName(entryPath);
            Directory.CreateDirectory(parent);
            string stale = null;
            try
            {
                if (replaceExisting)
                {
                    if (!Directory.Exists(entryPath))
                        return new DdfDotNetArtifactPublicationResult(null, null,
                            new[] { new DdfDotNetContractIssue("DDFC414",
                                "La voce cache esistente non è una directory.") });
                    stale = Path.Combine(parent, "." +
                        Path.GetFileName(entryPath) + ".ddf-stale-" +
                        Guid.NewGuid().ToString("N"));
                    Directory.Move(entryPath, stale);
                }
                DdfDotNetArtifactPublicationResult result =
                    DdfDotNetArtifactPublisher.Publish(emission, entryPath);
                if (!result.Success && stale != null &&
                    !Directory.Exists(entryPath))
                    Directory.Move(stale, entryPath);
                if (result.Success && stale != null)
                    Directory.Delete(stale, true);
                return result;
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                if (stale != null && Directory.Exists(stale) &&
                    !Directory.Exists(entryPath))
                {
                    try { Directory.Move(stale, entryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return new DdfDotNetArtifactPublicationResult(null, null,
                    new[] { new DdfDotNetContractIssue("DDFC414",
                        "Sostituzione cache fallita: " + exception.Message) });
            }
        }

        private static string ResolveEntryPath(string cacheRoot, string key,
            ICollection<DdfDotNetContractIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot) ||
                !Path.IsPathRooted(cacheRoot))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC412",
                    "È richiesta una radice cache assoluta esplicita."));
                return null;
            }
            try
            {
                string root = Path.GetFullPath(cacheRoot).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(Path.GetFileName(root)) ||
                    string.IsNullOrEmpty(Path.GetDirectoryName(root)))
                    throw new ArgumentException(
                        "La cache non può coincidere con una radice filesystem.");
                return Path.Combine(root, "v" + FormatVersion, key.Substring(0, 2),
                    key);
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException ||
                                               exception is PathTooLongException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC412",
                    "Radice cache non valida: " + exception.Message));
                return null;
            }
        }

        private static string ResolveWithin(string root, string relativePath)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string resolved = Path.GetFullPath(Path.Combine(prefix,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Percorso cache esterno alla voce prevista.");
            return resolved;
        }
    }
}
