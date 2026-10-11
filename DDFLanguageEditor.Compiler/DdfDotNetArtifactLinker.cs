using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetLinkedArtifact
    {
        internal DdfDotNetLinkedArtifact(DdfDotNetArtifactManifest manifest)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            ModuleId = manifest.ModuleId;
            ModuleVersion = manifest.ModuleVersion;
            Identity = ModuleId + "@" + ModuleVersion;
            CompilationFingerprint = manifest.CompilationFingerprint;
            AssemblyName = manifest.AssemblyName;
            AssemblySha256 = manifest.Payloads.Single(payload => payload.Kind ==
                DdfDotNetPayloadKind.Assembly).Sha256;
            DependencyIdentities = manifest.Dependencies.Select(dependency =>
                    dependency.ModuleId + "@" + dependency.ModuleVersion)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        internal DdfDotNetArtifactManifest Manifest { get; }
        public string ModuleId { get; }
        public string ModuleVersion { get; }
        public string Identity { get; }
        public string CompilationFingerprint { get; }
        public string AssemblyName { get; }
        public string AssemblySha256 { get; }
        public IReadOnlyList<string> DependencyIdentities { get; }
    }

    public sealed class DdfDotNetLinkedProgram
    {
        internal DdfDotNetLinkedProgram(string entryModuleIdentity,
            IEnumerable<DdfDotNetArtifactManifest> artifacts,
            IEnumerable<string> requiredHostCapabilities)
        {
            EntryModuleIdentity = entryModuleIdentity ?? string.Empty;
            Artifacts = (artifacts ?? Array.Empty<DdfDotNetArtifactManifest>())
                .Select(manifest => new DdfDotNetLinkedArtifact(manifest))
                .ToList().AsReadOnly();
            RequiredHostCapabilities = (requiredHostCapabilities ??
                    Array.Empty<string>()).OrderBy(value => value,
                    StringComparer.Ordinal).ToList().AsReadOnly();
            Fingerprint = ComputeFingerprint();
        }

        public const string FormatVersion = "1.0";
        public string EntryModuleIdentity { get; }
        public IReadOnlyList<DdfDotNetLinkedArtifact> Artifacts { get; }
        public IReadOnlyList<string> RequiredHostCapabilities { get; }
        public string Fingerprint { get; }

        private string ComputeFingerprint()
        {
            var value = new StringBuilder();
            value.Append(FormatVersion).Append('\n')
                .Append(EntryModuleIdentity).Append('\n');
            if (Artifacts.Count > 0)
            {
                DdfDotNetArtifactManifest environment = Artifacts[^1].Manifest;
                value.Append(environment.BackendId).Append('\n')
                    .Append(environment.TargetFramework).Append('\n')
                    .Append(environment.RuntimeIdentifier).Append('\n')
                    .Append(environment.RuntimeAbiVersion).Append('\n')
                    .Append(environment.LanguageId).Append('@')
                    .Append(environment.LanguagePackageVersion).Append('\n');
            }
            foreach (DdfDotNetLinkedArtifact linkedArtifact in Artifacts)
            {
                DdfDotNetArtifactManifest artifact = linkedArtifact.Manifest;
                value.Append("module:").Append(linkedArtifact.Identity).Append(':')
                    .Append(linkedArtifact.CompilationFingerprint).Append(':')
                    .Append(linkedArtifact.AssemblyName).Append(':')
                    .Append(linkedArtifact.AssemblySha256).Append('\n');
                foreach (DdfDotNetDependencyDescriptor dependency in
                         artifact.Dependencies.OrderBy(item => item.ModuleId,
                             StringComparer.Ordinal).ThenBy(item =>
                             item.ModuleVersion, StringComparer.Ordinal))
                    value.Append("dependency:").Append(dependency.ModuleId)
                        .Append('@').Append(dependency.ModuleVersion).Append(':')
                        .Append(dependency.CompilationFingerprint).Append(':')
                        .Append(dependency.AssemblyName).Append('\n');
                foreach (DdfDotNetImportDescriptor imported in artifact.Imports
                             .OrderBy(item => item.ModuleId, StringComparer.Ordinal)
                             .ThenBy(item => item.ModuleVersion,
                                 StringComparer.Ordinal)
                             .ThenBy(item => item.Name, StringComparer.Ordinal)
                             .ThenBy(item => item.Kind))
                    value.Append("import:").Append(imported.ModuleIdentity)
                        .Append(':').Append(imported.Kind).Append(':')
                        .Append(imported.Name).Append(':')
                        .Append(imported.Signature).Append('\n');
                foreach (DdfDotNetExportDescriptor exported in artifact.Exports
                             .OrderBy(item => item.Name, StringComparer.Ordinal)
                             .ThenBy(item => item.Kind))
                    value.Append("export:").Append(exported.Kind).Append(':')
                        .Append(exported.Name).Append(':')
                        .Append(exported.Signature).Append('\n');
            }
            foreach (string capability in RequiredHostCapabilities)
                value.Append("capability:").Append(capability).Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }
    }

    public sealed class DdfDotNetArtifactLinkResult
    {
        internal DdfDotNetArtifactLinkResult(DdfDotNetLinkedProgram program,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Program = program;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetLinkedProgram Program { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Program != null && Issues.Count == 0;
    }

    /// <summary>
    /// Resolves a closed graph of compiled DDF manifests without loading PE
    /// images. Dependencies and imported interfaces must match exactly.
    /// </summary>
    public static class DdfDotNetArtifactLinker
    {
        public static DdfDotNetArtifactLinkResult Link(
            string entryModuleIdentity,
            IEnumerable<DdfDotNetArtifactManifest> artifacts)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (string.IsNullOrWhiteSpace(entryModuleIdentity))
                issues.Add(new DdfDotNetContractIssue("DDFC501",
                    "È richiesta l'identità del modulo di ingresso."));
            List<DdfDotNetArtifactManifest> supplied = (artifacts ??
                Array.Empty<DdfDotNetArtifactManifest>()).ToList();
            foreach (DdfDotNetArtifactManifest artifact in supplied)
                issues.AddRange(DdfDotNetArtifactContract.Validate(artifact)
                    .Select(issue => new DdfDotNetContractIssue("DDFC500",
                        (artifact == null ? "<null>" : Identity(artifact)) +
                        ": " + issue.Code + " " + issue.Message)));
            if (issues.Count != 0)
                return new DdfDotNetArtifactLinkResult(null, issues);

            var byIdentity = new Dictionary<string,
                DdfDotNetArtifactManifest>(StringComparer.Ordinal);
            var assemblyNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (DdfDotNetArtifactManifest artifact in supplied)
            {
                if (!byIdentity.TryAdd(Identity(artifact), artifact))
                    issues.Add(new DdfDotNetContractIssue("DDFC500",
                        "Artefatto modulo duplicato: " + Identity(artifact) + "."));
                if (!assemblyNames.Add(artifact.AssemblyName))
                    issues.Add(new DdfDotNetContractIssue("DDFC500",
                        "Nome assembly duplicato: " + artifact.AssemblyName + "."));
            }
            if (!byIdentity.TryGetValue(entryModuleIdentity ?? string.Empty,
                    out DdfDotNetArtifactManifest entry))
                issues.Add(new DdfDotNetContractIssue("DDFC501",
                    "Modulo di ingresso non disponibile: " +
                    (entryModuleIdentity ?? string.Empty) + "."));
            if (issues.Count != 0)
                return new DdfDotNetArtifactLinkResult(null, issues);

            var ordered = new List<DdfDotNetArtifactManifest>();
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            Visit(entry, entry, byIdentity, visiting, visited, ordered, issues);
            if (issues.Count != 0)
                return new DdfDotNetArtifactLinkResult(null, issues);

            var capabilities = ordered.SelectMany(artifact =>
                    artifact.RequiredHostCapabilities)
                .Distinct(StringComparer.Ordinal);
            return new DdfDotNetArtifactLinkResult(new DdfDotNetLinkedProgram(
                entryModuleIdentity, ordered, capabilities), null);
        }

        private static void Visit(DdfDotNetArtifactManifest artifact,
            DdfDotNetArtifactManifest entry,
            IReadOnlyDictionary<string, DdfDotNetArtifactManifest> byIdentity,
            ISet<string> visiting, ISet<string> visited,
            ICollection<DdfDotNetArtifactManifest> ordered,
            ICollection<DdfDotNetContractIssue> issues)
        {
            string identity = Identity(artifact);
            if (visited.Contains(identity)) return;
            if (!visiting.Add(identity))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC504",
                    "Ciclo tra artefatti compilati rilevato in " + identity + "."));
                return;
            }
            if (!SameEnvironment(entry, artifact))
                issues.Add(new DdfDotNetContractIssue("DDFC505",
                    identity + ": ambiente di compilazione incompatibile col modulo di ingresso."));

            foreach (DdfDotNetDependencyDescriptor dependency in
                     artifact.Dependencies.OrderBy(item => item.ModuleId,
                         StringComparer.Ordinal).ThenBy(item => item.ModuleVersion,
                         StringComparer.Ordinal))
            {
                string dependencyIdentity = dependency.ModuleId + "@" +
                    dependency.ModuleVersion;
                if (!byIdentity.TryGetValue(dependencyIdentity,
                        out DdfDotNetArtifactManifest resolved))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC502",
                        identity + ": dipendenza compilata mancante: " +
                        dependencyIdentity + "."));
                    continue;
                }
                if (resolved.CompilationFingerprint !=
                        dependency.CompilationFingerprint ||
                    resolved.AssemblyName != dependency.AssemblyName)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC502",
                        identity + ": dipendenza compilata incompatibile: " +
                        dependencyIdentity + "."));
                    continue;
                }
                Visit(resolved, entry, byIdentity, visiting, visited, ordered,
                    issues);
            }
            ValidateImports(artifact, byIdentity, issues);
            visiting.Remove(identity);
            if (visited.Add(identity)) ordered.Add(artifact);
        }

        private static void ValidateImports(DdfDotNetArtifactManifest importer,
            IReadOnlyDictionary<string, DdfDotNetArtifactManifest> byIdentity,
            ICollection<DdfDotNetContractIssue> issues)
        {
            foreach (DdfDotNetImportDescriptor imported in importer.Imports)
            {
                if (!byIdentity.TryGetValue(imported.ModuleIdentity,
                        out DdfDotNetArtifactManifest provider))
                    continue;
                DdfDotNetExportDescriptor exported = provider.Exports
                    .SingleOrDefault(item => item.Name == imported.Name);
                if (exported == null)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC503",
                        Identity(importer) + ": simbolo importato mancante: " +
                        imported.ModuleIdentity + "." + imported.Name + "."));
                }
                else if (exported.Kind != imported.Kind ||
                         exported.Signature != imported.Signature)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC503",
                        Identity(importer) + ": simbolo importato incompatibile: " +
                        imported.ModuleIdentity + "." + imported.Name + "."));
                }
            }
        }

        private static bool SameEnvironment(DdfDotNetArtifactManifest left,
            DdfDotNetArtifactManifest right) =>
            left.BackendId == right.BackendId &&
            string.Equals(left.TargetFramework, right.TargetFramework,
                StringComparison.OrdinalIgnoreCase) &&
            left.RuntimeAbiVersion == right.RuntimeAbiVersion &&
            string.Equals(left.RuntimeIdentifier, right.RuntimeIdentifier,
                StringComparison.OrdinalIgnoreCase) &&
            left.LanguageId == right.LanguageId &&
            left.LanguagePackageVersion == right.LanguagePackageVersion;

        private static string Identity(DdfDotNetArtifactManifest artifact) =>
            artifact.ModuleId + "@" + artifact.ModuleVersion;
    }
}
