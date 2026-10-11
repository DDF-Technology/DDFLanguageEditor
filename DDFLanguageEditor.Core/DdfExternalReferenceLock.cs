using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExternalReferenceLockDependency
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
    }

    public sealed class DdfExternalReferenceLockEntry
    {
        public string OwnerModule { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
        public string Origin { get; set; }
        public List<string> Capabilities { get; set; }
        public string Binding { get; set; }
        public string AssemblyPath { get; set; }
        public string AdapterId { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
        public string ApiFingerprint { get; set; }
        public List<DdfExternalReferenceLockDependency> Dependencies {
            get; set;
        }
    }

    public sealed class DdfExternalReferenceLockFile
    {
        public const string FormatValue = "ddf-external-reference-lock";
        public const string VersionValue = "1.1";
        public const string FileName = "ddf-references.lock.json";

        public string Format { get; set; }
        public string Version { get; set; }
        public string ProjectModule { get; set; }
        public string Context { get; set; }
        public string Fingerprint { get; set; }
        public List<DdfExternalReferenceLockEntry> References { get; set; }
    }

    internal sealed class DdfResolvedExternalReferenceRecord
    {
        internal DdfResolvedExternalReferenceRecord(string ownerModule,
            DdfResolvedExternalReference reference)
        {
            OwnerModule = ownerModule;
            Reference = reference;
        }

        internal string OwnerModule { get; }
        internal DdfResolvedExternalReference Reference { get; }
    }

    internal static class DdfExternalReferenceLock
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        internal static DdfBuildIssue VerifyOrWrite(string outputRoot,
            string projectModule, DdfExternalReferenceContext context,
            IEnumerable<DdfResolvedExternalReferenceRecord> records,
            bool writeIfMissing)
        {
            DdfExternalReferenceLockFile expected = Create(projectModule,
                context, records);
            string path = Path.Combine(outputRoot,
                DdfExternalReferenceLockFile.FileName);
            if (File.Exists(path))
            {
                try
                {
                    DdfExternalReferenceLockFile existing =
                        JsonSerializer.Deserialize<
                            DdfExternalReferenceLockFile>(File.ReadAllText(path,
                            Encoding.UTF8), JsonOptions);
                    if (existing == null ||
                        existing.Format !=
                            DdfExternalReferenceLockFile.FormatValue ||
                        existing.Version !=
                            DdfExternalReferenceLockFile.VersionValue ||
                        existing.ProjectModule != projectModule ||
                        existing.Context != ContextName(context) ||
                        existing.Fingerprint != expected.Fingerprint)
                        return DdfBuildIssue.Configuration("DDFR010",
                            "Il lockfile dei riferimenti non coincide con la " +
                            "risoluzione corrente. Rimuoverlo o aggiornarlo " +
                            "esplicitamente.", path,
                            DdfDiagnosticDomain.EngineCapability);
                    return null;
                }
                catch (Exception exception) when (exception is IOException ||
                                                   exception is UnauthorizedAccessException ||
                                                   exception is JsonException)
                {
                    return DdfBuildIssue.Configuration("DDFR010",
                        "Lockfile dei riferimenti non leggibile: " +
                        exception.Message, path,
                        DdfDiagnosticDomain.EngineCapability);
                }
            }
            if (!writeIfMissing) return null;
            try
            {
                Directory.CreateDirectory(outputRoot);
                string temporary = Path.Combine(outputRoot, "." +
                    DdfExternalReferenceLockFile.FileName + "." +
                    Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(temporary, JsonSerializer.Serialize(
                        expected, JsonOptions) + Environment.NewLine,
                        new UTF8Encoding(false));
                    File.Move(temporary, path);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                return null;
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                return DdfBuildIssue.Configuration("DDFR011",
                    "Impossibile scrivere atomicamente il lockfile: " +
                    exception.Message, path,
                    DdfDiagnosticDomain.EngineCapability);
            }
        }

        private static DdfExternalReferenceLockFile Create(
            string projectModule, DdfExternalReferenceContext context,
            IEnumerable<DdfResolvedExternalReferenceRecord> records)
        {
            List<DdfExternalReferenceLockEntry> entries = (records ??
                    Array.Empty<DdfResolvedExternalReferenceRecord>())
                .Select(record => Entry(record.OwnerModule,
                    record.Reference))
                .OrderBy(item => item.OwnerModule, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
            var result = new DdfExternalReferenceLockFile
            {
                Format = DdfExternalReferenceLockFile.FormatValue,
                Version = DdfExternalReferenceLockFile.VersionValue,
                ProjectModule = projectModule,
                Context = ContextName(context),
                References = entries
            };
            result.Fingerprint = Fingerprint(result);
            return result;
        }

        private static DdfExternalReferenceLockEntry Entry(string owner,
            DdfResolvedExternalReference reference) => new()
        {
            OwnerModule = owner,
            Id = reference.Id,
            Kind = reference.Kind,
            Version = reference.Version,
            Architecture = reference.Architecture,
            Origin = reference.Origin,
            Capabilities = reference.Capabilities.ToList(),
            Binding = reference.BindingKind ==
                DdfExternalReferenceBindingKind.ManagedAssembly
                ? "managed-assembly" : "typed-adapter",
            AssemblyPath = reference.AssemblyPath,
            AdapterId = reference.AdapterId,
            Sha256 = reference.Sha256,
            Size = reference.Size,
            ApiFingerprint = reference.ApiDefinition.Fingerprint,
            Dependencies = reference.Dependencies.Select(item =>
                    new DdfExternalReferenceLockDependency
                    {
                        Id = item.Id,
                        Version = item.Version,
                        Path = item.Path,
                        Sha256 = item.Sha256,
                        Size = item.Size
                    }).OrderBy(item => item.Id, StringComparer.Ordinal).ToList()
        };

        private static string Fingerprint(
            DdfExternalReferenceLockFile value)
        {
            var canonical = new StringBuilder();
            canonical.Append(value.Format).Append('\n')
                .Append(value.Version).Append('\n')
                .Append(value.ProjectModule).Append('\n')
                .Append(value.Context).Append('\n');
            foreach (DdfExternalReferenceLockEntry entry in value.References)
            {
                canonical.Append(entry.OwnerModule).Append(':')
                    .Append(entry.Id).Append(':').Append(entry.Kind).Append('@')
                    .Append(entry.Version).Append(':')
                    .Append(entry.Architecture).Append(':')
                    .Append(entry.Origin).Append(':')
                    .Append(string.Join(",", entry.Capabilities)).Append(':')
                    .Append(entry.Binding).Append(':')
                    .Append(entry.AssemblyPath).Append(':')
                    .Append(entry.AdapterId).Append(':')
                    .Append(entry.Sha256).Append(':').Append(entry.Size)
                    .Append(':').Append(entry.ApiFingerprint)
                    .Append('\n');
                foreach (DdfExternalReferenceLockDependency dependency in
                         entry.Dependencies)
                    canonical.Append("dep:").Append(dependency.Id).Append('@')
                        .Append(dependency.Version).Append(':')
                        .Append(dependency.Path).Append(':')
                        .Append(dependency.Sha256).Append(':')
                        .Append(dependency.Size).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                canonical.ToString()))).ToLowerInvariant();
        }

        private static string ContextName(
            DdfExternalReferenceContext context) => context ==
                DdfExternalReferenceContext.Standalone
                ? "standalone" : "embedded";
    }
}
