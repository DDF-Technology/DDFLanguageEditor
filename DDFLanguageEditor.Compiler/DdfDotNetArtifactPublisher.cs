using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetArtifactPublicationResult
    {
        internal DdfDotNetArtifactPublicationResult(string outputRoot,
            IEnumerable<string> publishedPaths,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            OutputRoot = outputRoot ?? string.Empty;
            PublishedPaths = (publishedPaths ?? Array.Empty<string>())
                .ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public string OutputRoot { get; }
        public IReadOnlyList<string> PublishedPaths { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => !string.IsNullOrEmpty(OutputRoot) &&
            PublishedPaths.Count > 0 && Issues.Count == 0;
    }

    /// <summary>
    /// Publishes a complete in-memory emission through a sibling staging
    /// directory and a final atomic directory rename. Existing destinations are
    /// deliberately refused: replacement policy belongs to a later cache layer.
    /// </summary>
    public static class DdfDotNetArtifactPublisher
    {
        public static DdfDotNetArtifactPublicationResult Publish(
            DdfDotNetArtifactEmissionResult emission, string outputRoot)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (emission == null || !emission.Success)
                issues.Add(new DdfDotNetContractIssue("DDFC409",
                    "È richiesto un emit completo e valido."));
            string destination = NormalizeDestination(outputRoot, issues);
            if (issues.Count != 0)
                return new DdfDotNetArtifactPublicationResult(null, null, issues);
            string parent = Path.GetDirectoryName(destination);
            string leaf = Path.GetFileName(destination);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC409",
                    "La destinazione di pubblicazione esiste già: " +
                    destination + "."));
                return new DdfDotNetArtifactPublicationResult(null, null, issues);
            }

            IReadOnlyList<PreparedArtifact> prepared = Prepare(
                emission.Artifacts, issues);
            if (issues.Count != 0)
                return new DdfDotNetArtifactPublicationResult(null, null, issues);

            string staging = Path.Combine(parent, "." + leaf +
                ".ddf-tmp-" + Guid.NewGuid().ToString("N"));
            var publishedPaths = new List<string>();
            try
            {
                Directory.CreateDirectory(parent);
                Directory.CreateDirectory(staging);
                foreach (PreparedArtifact artifact in prepared)
                {
                    string assemblyPath = ResolveWithin(staging,
                        artifact.Artifact.AssemblyPath);
                    string portablePdbPath = ResolveWithin(staging,
                        artifact.Artifact.PortablePdbPath);
                    string sourceMapPath = ResolveWithin(staging,
                        artifact.Artifact.SourceMapPath);
                    string manifestPath = ResolveWithin(staging,
                        artifact.Artifact.ManifestPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
                    File.WriteAllBytes(assemblyPath,
                        artifact.Artifact.AssemblyBytes);
                    File.WriteAllBytes(portablePdbPath,
                        artifact.Artifact.PortablePdbBytes);
                    File.WriteAllBytes(sourceMapPath,
                        artifact.Artifact.SourceMapBytes);
                    File.WriteAllBytes(manifestPath,
                        artifact.Artifact.ManifestBytes);
                    string artifactRoot = Path.GetDirectoryName(manifestPath);
                    DdfDotNetCompatibilityResult verified =
                        DdfDotNetArtifactCompatibility.VerifyPayloads(
                            artifact.Artifact.Manifest, artifactRoot);
                    if (!verified.IsCompatible)
                    {
                        issues.AddRange(verified.Issues.Select(issue =>
                            new DdfDotNetContractIssue("DDFC410",
                                artifact.Artifact.ModuleIdentity + ": " +
                                issue.Code + " " + issue.Message)));
                        break;
                    }
                    publishedPaths.Add(artifact.Artifact.AssemblyPath);
                    publishedPaths.Add(artifact.Artifact.PortablePdbPath);
                    publishedPaths.Add(artifact.Artifact.SourceMapPath);
                    publishedPaths.Add(artifact.Artifact.ManifestPath);
                }
                if (issues.Count == 0)
                    Directory.Move(staging, destination);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC411",
                    "Pubblicazione atomica fallita: " + exception.Message));
            }
            finally
            {
                if (Directory.Exists(staging) && IsDirectChild(parent, staging))
                {
                    try { Directory.Delete(staging, true); }
                    catch (Exception exception) when (exception is IOException ||
                                                       exception is UnauthorizedAccessException)
                    {
                        if (issues.Count == 0)
                            issues.Add(new DdfDotNetContractIssue("DDFC411",
                                "Pulizia staging fallita: " + exception.Message));
                    }
                }
            }
            return new DdfDotNetArtifactPublicationResult(
                issues.Count == 0 ? destination : null,
                issues.Count == 0 ? publishedPaths : null, issues);
        }

        private static IReadOnlyList<PreparedArtifact> Prepare(
            IReadOnlyList<DdfDotNetEmittedArtifact> artifacts,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var result = new List<PreparedArtifact>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DdfDotNetEmittedArtifact artifact in artifacts)
            {
                if (artifact == null || artifact.Manifest == null ||
                    artifact.SourceMap == null ||
                    artifact.AssemblyBytes.Length == 0 ||
                    artifact.PortablePdbBytes.Length == 0 ||
                    artifact.SourceMapBytes.Length == 0 ||
                    artifact.ManifestBytes.Length == 0 ||
                    !SafeRelativePath(artifact.AssemblyPath) ||
                    !SafeRelativePath(artifact.PortablePdbPath) ||
                    !SafeRelativePath(artifact.SourceMapPath) ||
                    !SafeRelativePath(artifact.ManifestPath) ||
                    !paths.Add(NormalizedRelativePath(artifact.AssemblyPath)) ||
                    !paths.Add(NormalizedRelativePath(artifact.PortablePdbPath)) ||
                    !paths.Add(NormalizedRelativePath(artifact.SourceMapPath)) ||
                    !paths.Add(NormalizedRelativePath(artifact.ManifestPath)))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC410",
                        "Percorsi o contenuti dell'artefatto emesso non validi."));
                    continue;
                }
                byte[] expectedManifest;
                byte[] expectedSourceMap;
                try
                {
                    expectedManifest = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeManifest(
                            artifact.Manifest));
                    expectedSourceMap = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeSourceMap(
                            artifact.SourceMap));
                }
                catch (ArgumentException exception)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC410",
                        artifact.ModuleIdentity + ": " + exception.Message));
                    continue;
                }
                if (!expectedManifest.SequenceEqual(artifact.ManifestBytes) ||
                    !expectedSourceMap.SequenceEqual(artifact.SourceMapBytes) ||
                    !MatchesPayload(artifact, DdfDotNetPayloadKind.Assembly,
                        artifact.AssemblyPath, artifact.AssemblyBytes) ||
                    !MatchesPayload(artifact, DdfDotNetPayloadKind.PortablePdb,
                        artifact.PortablePdbPath, artifact.PortablePdbBytes) ||
                    !MatchesPayload(artifact, DdfDotNetPayloadKind.SourceMap,
                        artifact.SourceMapPath, artifact.SourceMapBytes) ||
                    !SameDirectory(artifact.AssemblyPath,
                        artifact.PortablePdbPath, artifact.SourceMapPath,
                        artifact.ManifestPath) ||
                    DdfDotNetArtifactContract.ValidateBundle(artifact.Manifest,
                        artifact.SourceMap).Count != 0)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC410",
                        artifact.ModuleIdentity +
                        ": byte, manifest o percorso non coerenti."));
                    continue;
                }
                result.Add(new PreparedArtifact(artifact));
            }
            return result.AsReadOnly();
        }

        private static bool MatchesPayload(DdfDotNetEmittedArtifact artifact,
            DdfDotNetPayloadKind kind, string path, byte[] bytes)
        {
            DdfDotNetPayloadDescriptor expected = artifact.Manifest.Payloads
                .SingleOrDefault(item => item.Kind == kind);
            DdfDotNetPayloadDescriptor actual =
                DdfDotNetPayloadDescriptor.FromBytes(kind,
                    Path.GetFileName(path), bytes);
            return expected != null && expected.Path == actual.Path &&
                expected.Sha256 == actual.Sha256 &&
                expected.Size == actual.Size;
        }

        private static bool SameDirectory(params string[] paths)
        {
            string first = Path.GetDirectoryName(paths[0].Replace('/',
                Path.DirectorySeparatorChar));
            return paths.Skip(1).All(path => string.Equals(first,
                Path.GetDirectoryName(path.Replace('/',
                    Path.DirectorySeparatorChar)),
                StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeDestination(string outputRoot,
            ICollection<DdfDotNetContractIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !Path.IsPathRooted(outputRoot))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC409",
                    "È richiesta una destinazione assoluta esplicita."));
                return null;
            }
            try
            {
                string fullPath = Path.GetFullPath(outputRoot).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(Path.GetFileName(fullPath)) ||
                    string.IsNullOrEmpty(Path.GetDirectoryName(fullPath)))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC409",
                        "La destinazione non può essere una radice filesystem."));
                    return null;
                }
                return fullPath;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException ||
                                               exception is PathTooLongException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC409",
                    "Destinazione non valida: " + exception.Message));
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
                throw new IOException("Percorso esterno alla directory di staging.");
            return resolved;
        }

        private static bool SafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
                return false;
            string normalized = value.Replace('/', Path.DirectorySeparatorChar);
            return normalized.Split(Path.DirectorySeparatorChar).All(segment =>
                segment.Length > 0 && segment != "." && segment != "..") &&
                normalized.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }

        private static string NormalizedRelativePath(string value) =>
            value.Replace('/', Path.DirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);

        private static bool IsDirectChild(string parent, string path) =>
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        private sealed class PreparedArtifact
        {
            internal PreparedArtifact(DdfDotNetEmittedArtifact artifact) =>
                Artifact = artifact;

            internal DdfDotNetEmittedArtifact Artifact { get; }
        }
    }
}
