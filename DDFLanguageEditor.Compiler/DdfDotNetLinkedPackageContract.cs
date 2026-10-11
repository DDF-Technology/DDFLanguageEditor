using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetLinkedPackageArtifact
    {
        public string ModuleIdentity { get; set; }
        public string AssemblyName { get; set; }
        public string AssemblySha256 { get; set; }
        public string PortablePdbSha256 { get; set; }
        public string SourceMapSha256 { get; set; }
        public string ManifestSha256 { get; set; }
    }

    public sealed class DdfDotNetLinkedPackageReference
    {
        public string AssemblyIdentity { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
        public bool Packaged { get; set; }
    }

    public sealed class DdfDotNetLinkedPackageManifest
    {
        public const string FormatValue = "ddf-dotnet-linked-package";
        public const string SchemaVersionValue = "1.1";

        public string Format { get; set; } = FormatValue;
        public string SchemaVersion { get; set; } = SchemaVersionValue;
        public string BundleFormatVersion { get; set; }
        public string BundleFingerprint { get; set; }
        public string ProgramFingerprint { get; set; }
        public string EntryModuleIdentity { get; set; }
        public string ReferenceSetFingerprint { get; set; }
        public List<string> RequiredHostCapabilities { get; set; } = new();
        public List<DdfDotNetLinkedPackageArtifact> Artifacts { get; set; } = new();
        public List<DdfDotNetLinkedPackageReference> ManagedReferences { get; set; }
            = new();
    }

    public static class DdfDotNetLinkedPackageContract
    {
        private static readonly Regex HashPattern = new("^[a-f0-9]{64}$",
            RegexOptions.CultureInvariant);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };

        internal static DdfDotNetLinkedPackageManifest Create(
            DdfDotNetLinkedBundle bundle, DdfDotNetReferenceSet referenceSet)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (referenceSet == null)
                throw new ArgumentNullException(nameof(referenceSet));
            return new DdfDotNetLinkedPackageManifest
            {
                BundleFormatVersion = DdfDotNetLinkedBundle.FormatVersion,
                BundleFingerprint = bundle.Fingerprint,
                ProgramFingerprint = bundle.ProgramFingerprint,
                EntryModuleIdentity = bundle.EntryModuleIdentity,
                ReferenceSetFingerprint = referenceSet.Fingerprint,
                RequiredHostCapabilities = bundle.RequiredHostCapabilities
                    .OrderBy(item => item, StringComparer.Ordinal).ToList(),
                Artifacts = bundle.Artifacts.Select(item =>
                    new DdfDotNetLinkedPackageArtifact
                    {
                        ModuleIdentity = item.Identity,
                        AssemblyName = item.AssemblyName,
                        AssemblySha256 = item.AssemblySha256,
                        PortablePdbSha256 = item.PortablePdbSha256,
                        SourceMapSha256 = item.SourceMapSha256,
                        ManifestSha256 = item.ManifestSha256
                    }).ToList(),
                ManagedReferences = bundle.ManagedReferences.Select(item =>
                    new DdfDotNetLinkedPackageReference
                    {
                        AssemblyIdentity = item.AssemblyIdentity,
                        Sha256 = item.Sha256,
                        Size = item.Size,
                        Packaged = item.Packaged
                    }).ToList()
            };
        }

        public static string Serialize(DdfDotNetLinkedPackageManifest manifest)
        {
            IReadOnlyList<DdfDotNetContractIssue> issues = Validate(manifest);
            if (issues.Count != 0)
                throw new ArgumentException(string.Join(Environment.NewLine,
                    issues), nameof(manifest));
            Normalize(manifest);
            return JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
        }

        public static DdfDotNetContractLoadResult<
            DdfDotNetLinkedPackageManifest> Deserialize(string json)
        {
            try
            {
                DdfDotNetLinkedPackageManifest value = JsonSerializer
                    .Deserialize<DdfDotNetLinkedPackageManifest>(
                        json ?? string.Empty, JsonOptions);
                IReadOnlyList<DdfDotNetContractIssue> issues = Validate(value);
                return new DdfDotNetContractLoadResult<
                    DdfDotNetLinkedPackageManifest>(
                    issues.Count == 0 ? value : null, issues);
            }
            catch (JsonException exception)
            {
                return new DdfDotNetContractLoadResult<
                    DdfDotNetLinkedPackageManifest>(null, new[]
                    {
                        new DdfDotNetContractIssue("DDFC514",
                            "Manifest package collegato non leggibile: " +
                            exception.Message)
                    });
            }
        }

        public static IReadOnlyList<DdfDotNetContractIssue> Validate(
            DdfDotNetLinkedPackageManifest manifest)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (manifest == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC514",
                    "Manifest package collegato richiesto."));
                return issues.AsReadOnly();
            }
            bool hashes = ValidHash(manifest.BundleFingerprint) &&
                ValidHash(manifest.ProgramFingerprint) &&
                ValidHash(manifest.ReferenceSetFingerprint);
            bool header = manifest.Format ==
                    DdfDotNetLinkedPackageManifest.FormatValue &&
                manifest.SchemaVersion ==
                    DdfDotNetLinkedPackageManifest.SchemaVersionValue &&
                manifest.BundleFormatVersion ==
                    DdfDotNetLinkedBundle.FormatVersion && hashes &&
                !string.IsNullOrWhiteSpace(manifest.EntryModuleIdentity);
            if (!header)
                issues.Add(new DdfDotNetContractIssue("DDFC514",
                    "Intestazione del package collegato non valida."));
            List<DdfDotNetLinkedPackageArtifact> artifacts =
                manifest.Artifacts ?? new();
            bool artifactLockValid = artifacts.Count != 0 &&
                !artifacts.Any(item => item == null ||
                    string.IsNullOrWhiteSpace(item.ModuleIdentity) ||
                    string.IsNullOrWhiteSpace(item.AssemblyName) ||
                    !ValidHash(item.AssemblySha256) ||
                    !ValidHash(item.PortablePdbSha256) ||
                    !ValidHash(item.SourceMapSha256) ||
                    !ValidHash(item.ManifestSha256)) &&
                artifacts.Select(item => item.ModuleIdentity).Distinct(
                    StringComparer.Ordinal).Count() == artifacts.Count &&
                artifacts.Select(item => item.AssemblyName).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() == artifacts.Count;
            if (!artifactLockValid)
                issues.Add(new DdfDotNetContractIssue("DDFC515",
                    "Lock degli artefatti del package non valido."));
            List<DdfDotNetLinkedPackageReference> references =
                manifest.ManagedReferences ?? new();
            bool referenceLockValid = references.Count != 0 &&
                !references.Any(item => item == null ||
                    string.IsNullOrWhiteSpace(item.AssemblyIdentity) ||
                    !ValidHash(item.Sha256) || item.Size <= 0) &&
                references.Select(item => item.AssemblyIdentity).Distinct(
                    StringComparer.Ordinal).Count() == references.Count;
            referenceLockValid = referenceLockValid &&
                ComputeReferenceSetFingerprint(references) ==
                    manifest.ReferenceSetFingerprint;
            if (!referenceLockValid)
                issues.Add(new DdfDotNetContractIssue("DDFC516",
                    "Allowlist gestita persistita non valida o non coerente col fingerprint."));
            if (header && artifactLockValid && referenceLockValid &&
                ComputeBundleFingerprint(manifest) !=
                    manifest.BundleFingerprint)
                issues.Add(new DdfDotNetContractIssue("DDFC515",
                    "Fingerprint del bundle non coerente con il lock persistito."));
            return issues.AsReadOnly();
        }

        internal static string ComputeReferenceSetFingerprint(
            IEnumerable<DdfDotNetLinkedPackageReference> references)
        {
            var value = new StringBuilder();
            foreach (DdfDotNetLinkedPackageReference reference in
                     (references ?? Array.Empty<
                         DdfDotNetLinkedPackageReference>()).OrderBy(
                         item => item.AssemblyIdentity, StringComparer.Ordinal))
                value.Append(reference.AssemblyIdentity).Append(':')
                    .Append(reference.Sha256).Append(':')
                    .Append(reference.Size).Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }

        private static string ComputeBundleFingerprint(
            DdfDotNetLinkedPackageManifest manifest)
        {
            var value = new StringBuilder();
            value.Append(manifest.BundleFormatVersion).Append('\n')
                .Append(manifest.ProgramFingerprint).Append('\n')
                .Append(manifest.EntryModuleIdentity).Append('\n');
            foreach (string capability in (manifest.RequiredHostCapabilities ??
                         new List<string>()).OrderBy(item => item,
                         StringComparer.Ordinal))
                value.Append("capability:").Append(capability).Append('\n');
            foreach (DdfDotNetLinkedPackageArtifact artifact in
                     manifest.Artifacts ?? new List<
                         DdfDotNetLinkedPackageArtifact>())
                value.Append(artifact.ModuleIdentity).Append(':')
                    .Append(artifact.AssemblySha256).Append(':')
                    .Append(artifact.PortablePdbSha256).Append(':')
                    .Append(artifact.SourceMapSha256).Append(':')
                    .Append(artifact.ManifestSha256).Append('\n');
            value.Append("references:")
                .Append(manifest.ReferenceSetFingerprint).Append('\n');
            foreach (DdfDotNetLinkedPackageReference reference in
                     (manifest.ManagedReferences ?? new List<
                         DdfDotNetLinkedPackageReference>()).OrderBy(
                         item => item.AssemblyIdentity, StringComparer.Ordinal))
                value.Append(reference.AssemblyIdentity).Append(':')
                    .Append(reference.Sha256).Append(':')
                    .Append(reference.Size).Append(':')
                    .Append(reference.Packaged ? "packaged" : "platform")
                    .Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }

        private static void Normalize(DdfDotNetLinkedPackageManifest manifest)
        {
            manifest.RequiredHostCapabilities = (manifest
                    .RequiredHostCapabilities ?? new List<string>())
                .OrderBy(item => item, StringComparer.Ordinal).ToList();
            manifest.ManagedReferences = (manifest.ManagedReferences ?? new())
                .OrderBy(item => item.AssemblyIdentity, StringComparer.Ordinal)
                .ToList();
        }

        private static bool ValidHash(string value) => value != null &&
            HashPattern.IsMatch(value);
    }
}
