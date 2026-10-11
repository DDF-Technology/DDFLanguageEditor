using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DDFLanguageEditor.Compiler
{
    public enum DdfDotNetArtifactHostKind
    {
        Standalone,
        CommandLine,
        Embedded
    }

    public sealed class DdfDotNetArtifactLoadLimits
    {
        public DdfDotNetArtifactLoadLimits(long maxPayloadBytes = 256 * 1024 * 1024,
            long maxTotalBytes = 512 * 1024 * 1024, int maxPayloads = 3,
            int maxDependencies = 256, int maxExports = 10000,
            int maxSourceDocuments = 10000, int maxSequencePoints = 2000000)
        {
            if (maxPayloadBytes <= 0 || maxTotalBytes <= 0 || maxPayloads <= 0 ||
                maxDependencies < 0 || maxExports < 0 || maxSourceDocuments < 0 ||
                maxSequencePoints < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
            MaxPayloadBytes = maxPayloadBytes;
            MaxTotalBytes = maxTotalBytes;
            MaxPayloads = maxPayloads;
            MaxDependencies = maxDependencies;
            MaxExports = maxExports;
            MaxSourceDocuments = maxSourceDocuments;
            MaxSequencePoints = maxSequencePoints;
        }

        public static DdfDotNetArtifactLoadLimits Default { get; } = new();
        public long MaxPayloadBytes { get; }
        public long MaxTotalBytes { get; }
        public int MaxPayloads { get; }
        public int MaxDependencies { get; }
        public int MaxExports { get; }
        public int MaxSourceDocuments { get; }
        public int MaxSequencePoints { get; }
    }

    public sealed class DdfDotNetAvailableDependency
    {
        public DdfDotNetAvailableDependency(string moduleId, string moduleVersion,
            string compilationFingerprint, string assemblyName)
        {
            ModuleId = moduleId ?? string.Empty;
            ModuleVersion = moduleVersion ?? string.Empty;
            CompilationFingerprint = compilationFingerprint ?? string.Empty;
            AssemblyName = assemblyName ?? string.Empty;
        }

        public string ModuleId { get; }
        public string ModuleVersion { get; }
        public string CompilationFingerprint { get; }
        public string AssemblyName { get; }
    }

    public sealed class DdfDotNetRuntimeEnvironment
    {
        public DdfDotNetRuntimeEnvironment(DdfDotNetArtifactHostKind hostKind,
            string targetFramework, string runtimeAbiVersion,
            string runtimeIdentifier = null,
            IEnumerable<string> hostCapabilities = null)
        {
            HostKind = hostKind;
            TargetFramework = targetFramework ?? string.Empty;
            RuntimeAbiVersion = runtimeAbiVersion ?? string.Empty;
            RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
            HostCapabilities = new HashSet<string>(hostCapabilities ??
                Array.Empty<string>(), StringComparer.Ordinal);
        }

        public DdfDotNetArtifactHostKind HostKind { get; }
        public string TargetFramework { get; }
        public string RuntimeAbiVersion { get; }
        public string RuntimeIdentifier { get; }
        public IReadOnlySet<string> HostCapabilities { get; }
    }

    public sealed class DdfDotNetCompatibilityResult
    {
        internal DdfDotNetCompatibilityResult(
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool IsCompatible => Issues.Count == 0;
    }

    /// <summary>
    /// Performs metadata and payload verification only. It never loads an
    /// assembly and therefore cannot execute module initializers or user code.
    /// </summary>
    public static class DdfDotNetArtifactCompatibility
    {
        public static DdfDotNetCompatibilityResult Inspect(
            DdfDotNetArtifactManifest manifest,
            DdfDotNetRuntimeEnvironment environment,
            IEnumerable<DdfDotNetAvailableDependency> availableDependencies = null,
            DdfDotNetSourceMap sourceMap = null,
            DdfDotNetArtifactLoadLimits limits = null)
        {
            var issues = DdfDotNetArtifactContract.ValidateBundle(manifest, sourceMap)
                .ToList();
            if (environment == null)
                issues.Add(new DdfDotNetContractIssue("DDFC200",
                    "Ambiente runtime richiesto."));
            if (manifest == null || environment == null || issues.Count != 0)
                return new DdfDotNetCompatibilityResult(issues);

            limits ??= DdfDotNetArtifactLoadLimits.Default;
            if (environment.HostKind == DdfDotNetArtifactHostKind.Embedded)
                issues.Add(new DdfDotNetContractIssue("DDFC201",
                    "Gli artefatti compilati non possono essere caricati dall'editor embedded."));
            if (!string.Equals(manifest.TargetFramework, environment.TargetFramework,
                    StringComparison.OrdinalIgnoreCase))
                issues.Add(new DdfDotNetContractIssue("DDFC202",
                    "Target Framework incompatibile: richiesto " +
                    manifest.TargetFramework + ", disponibile " +
                    environment.TargetFramework + "."));
            if (!string.Equals(manifest.RuntimeAbiVersion,
                    environment.RuntimeAbiVersion, StringComparison.Ordinal))
                issues.Add(new DdfDotNetContractIssue("DDFC203",
                    "Runtime ABI DDF incompatibile."));
            if (!string.IsNullOrEmpty(manifest.RuntimeIdentifier) &&
                !string.Equals(manifest.RuntimeIdentifier,
                    environment.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
                issues.Add(new DdfDotNetContractIssue("DDFC204",
                    "Runtime Identifier incompatibile."));
            foreach (string capability in manifest.RequiredHostCapabilities)
                if (!environment.HostCapabilities.Contains(capability))
                    issues.Add(new DdfDotNetContractIssue("DDFC205",
                        "Capability host richiesta ma non disponibile: " +
                        capability + "."));

            if (manifest.Payloads.Count > limits.MaxPayloads ||
                manifest.Payloads.Any(item => item.Size > limits.MaxPayloadBytes) ||
                SumWouldExceed(manifest.Payloads.Select(item => item.Size),
                    limits.MaxTotalBytes))
                issues.Add(new DdfDotNetContractIssue("DDFC206",
                    "Il bundle supera i limiti di numero o dimensione dei payload."));
            if (manifest.Dependencies.Count > limits.MaxDependencies ||
                manifest.Exports.Count > limits.MaxExports)
                issues.Add(new DdfDotNetContractIssue("DDFC207",
                    "Il manifest supera i limiti di dipendenze o simboli esportati."));
            if (sourceMap != null && (sourceMap.Documents.Count >
                    limits.MaxSourceDocuments || sourceMap.Documents.Sum(item =>
                        (long)item.SequencePoints.Count) > limits.MaxSequencePoints))
                issues.Add(new DdfDotNetContractIssue("DDFC208",
                    "La mappa sorgente supera i limiti configurati."));

            var available = (availableDependencies ??
                Array.Empty<DdfDotNetAvailableDependency>()).ToList();
            foreach (DdfDotNetDependencyDescriptor dependency in manifest.Dependencies)
            {
                bool found = available.Any(item => item.ModuleId == dependency.ModuleId &&
                    item.ModuleVersion == dependency.ModuleVersion &&
                    item.CompilationFingerprint == dependency.CompilationFingerprint &&
                    item.AssemblyName == dependency.AssemblyName);
                if (!found)
                    issues.Add(new DdfDotNetContractIssue("DDFC209",
                        "Dipendenza compilata assente o incompatibile: " +
                        dependency.ModuleId + "@" + dependency.ModuleVersion + "."));
            }
            return new DdfDotNetCompatibilityResult(issues);
        }

        public static DdfDotNetCompatibilityResult VerifyPayloads(
            DdfDotNetArtifactManifest manifest, string artifactRoot,
            DdfDotNetArtifactLoadLimits limits = null)
        {
            var issues = DdfDotNetArtifactContract.Validate(manifest).ToList();
            if (manifest == null || issues.Count != 0)
                return new DdfDotNetCompatibilityResult(issues);
            limits ??= DdfDotNetArtifactLoadLimits.Default;
            if (manifest.Payloads.Count > limits.MaxPayloads ||
                manifest.Payloads.Any(item => item.Size > limits.MaxPayloadBytes) ||
                SumWouldExceed(manifest.Payloads.Select(item => item.Size),
                    limits.MaxTotalBytes))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC206",
                    "Il bundle supera i limiti di numero o dimensione dei payload."));
                return new DdfDotNetCompatibilityResult(issues);
            }
            if (string.IsNullOrWhiteSpace(artifactRoot))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC210",
                    "Cartella artefatto richiesta."));
                return new DdfDotNetCompatibilityResult(issues);
            }
            string root;
            try
            {
                root = Path.GetFullPath(artifactRoot).TrimEnd(
                    Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is NotSupportedException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC210",
                    "Cartella artefatto non valida."));
                return new DdfDotNetCompatibilityResult(issues);
            }

            long total = 0;
            foreach (DdfDotNetPayloadDescriptor payload in manifest.Payloads)
            {
                string path = Path.GetFullPath(Path.Combine(root, payload.Path));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(path))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC211",
                        "Payload mancante o esterno alla cartella artefatto: " +
                        payload.Path + "."));
                    continue;
                }
                var info = new FileInfo(path);
                if (info.Length != payload.Size || info.Length > limits.MaxPayloadBytes ||
                    total > limits.MaxTotalBytes - info.Length)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC212",
                        "Dimensione payload non valida o oltre il limite: " +
                        payload.Path + "."));
                    continue;
                }
                total += info.Length;
                using FileStream stream = File.OpenRead(path);
                using SHA256 sha = SHA256.Create();
                string hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
                if (!string.Equals(hash, payload.Sha256, StringComparison.Ordinal))
                    issues.Add(new DdfDotNetContractIssue("DDFC213",
                        "Hash payload non corrispondente: " + payload.Path + "."));
            }
            return new DdfDotNetCompatibilityResult(issues);
        }

        private static bool SumWouldExceed(IEnumerable<long> values, long limit)
        {
            long total = 0;
            foreach (long value in values)
            {
                if (value < 0 || total > limit - value) return true;
                total += value;
            }
            return false;
        }
    }
}
