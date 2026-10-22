using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Compiler
{
    public enum DdfDotNetPayloadKind
    {
        Assembly,
        PortablePdb,
        SourceMap
    }

    public sealed class DdfDotNetPayloadDescriptor
    {
        public DdfDotNetPayloadKind Kind { get; set; }
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }

        public static DdfDotNetPayloadDescriptor FromBytes(DdfDotNetPayloadKind kind,
            string path, byte[] content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return new DdfDotNetPayloadDescriptor
            {
                Kind = kind,
                Path = path,
                Sha256 = DdfDotNetArtifactContract.Hash(content),
                Size = content.LongLength
            };
        }
    }

    public sealed class DdfDotNetDependencyDescriptor
    {
        public string ModuleId { get; set; }
        public string ModuleVersion { get; set; }
        public string CompilationFingerprint { get; set; }
        public string AssemblyName { get; set; }
    }

    public sealed class DdfDotNetExportDescriptor
    {
        public string Name { get; set; }
        public DdfCompilationExportKind Kind { get; set; }
        public string Signature { get; set; }
    }

    public sealed class DdfDotNetImportDescriptor
    {
        public string ModuleId { get; set; }
        public string ModuleVersion { get; set; }
        public string Name { get; set; }
        public DdfCompilationExportKind Kind { get; set; }
        public string Signature { get; set; }
        public string ModuleIdentity => ModuleId + "@" + ModuleVersion;
    }

    public sealed class DdfDotNetBuildReceipt
    {
        public string CompilationPlanVersion { get; set; }
        public string CompilationPlanFingerprint { get; set; }
        public string CompilerProfileVersion { get; set; }
        public string RoslynVersion { get; set; }
        public string CSharpLanguageVersion { get; set; }
        public string ReferenceSetFingerprint { get; set; }

        public static DdfDotNetBuildReceipt FromPlan(
            DdfDotNetCompilationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Options.ReferenceSet == null)
                throw new ArgumentException(
                    "Il piano richiede un reference set esplicito.", nameof(plan));
            return new DdfDotNetBuildReceipt
            {
                CompilationPlanVersion = DdfDotNetCompilationPlan.FormatVersion,
                CompilationPlanFingerprint = plan.Fingerprint,
                CompilerProfileVersion = DdfDotNetCompilerProfile.FormatVersion,
                RoslynVersion = DdfDotNetCompilerProfile.RoslynVersion,
                CSharpLanguageVersion = DdfDotNetCompilerProfile
                    .CSharpLanguageVersion.ToString(),
                ReferenceSetFingerprint = plan.Options.ReferenceSet.Fingerprint
            };
        }
    }

    public sealed class DdfDotNetArtifactManifest
    {
        public const string FormatValue = "ddf-dotnet-artifact";
        public const string SchemaVersionValue = "1.1";

        public string Format { get; set; } = FormatValue;
        public string SchemaVersion { get; set; } = SchemaVersionValue;
        public string ModuleId { get; set; }
        public string ModuleVersion { get; set; }
        public string CompilationUnitVersion { get; set; }
        public string CompilationFingerprint { get; set; }
        public string IrFormatVersion { get; set; }
        public string LanguageId { get; set; }
        public string LanguagePackageVersion { get; set; }
        public string BackendId { get; set; } = DdfRoslynCompilerBoundary.BackendId;
        public string TargetFramework { get; set; }
        public string RuntimeIdentifier { get; set; } = string.Empty;
        public string RuntimeAbiVersion { get; set; }
        public string AssemblyName { get; set; }
        public DdfDotNetBuildReceipt BuildReceipt { get; set; }
        public List<string> RequiredHostCapabilities { get; set; } = new();
        public List<DdfDotNetExportDescriptor> Exports { get; set; } = new();
        public List<DdfDotNetImportDescriptor> Imports { get; set; } = new();
        public List<DdfDotNetDependencyDescriptor> Dependencies { get; set; } = new();
        public List<DdfDotNetPayloadDescriptor> Payloads { get; set; } = new();

        public static DdfDotNetArtifactManifest Create(DdfCompilationUnit unit,
            string targetFramework, string runtimeAbiVersion, string assemblyName,
            IEnumerable<DdfDotNetPayloadDescriptor> payloads,
            IEnumerable<DdfDotNetDependencyDescriptor> dependencies = null,
            string runtimeIdentifier = null,
            IEnumerable<string> requiredHostCapabilities = null,
            DdfDotNetBuildReceipt buildReceipt = null,
            IEnumerable<DdfDotNetImportDescriptor> imports = null)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            List<DdfDotNetDependencyDescriptor> resolvedDependencies =
                (dependencies ?? Array.Empty<DdfDotNetDependencyDescriptor>())
                .OrderBy(item => item.ModuleId, StringComparer.Ordinal).ToList();
            string[] expectedDependencies = unit.Dependencies.Select(item => item.Identity)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] actualDependencies = resolvedDependencies.Select(item =>
                    (item?.ModuleId ?? string.Empty) + "@" +
                    (item?.ModuleVersion ?? string.Empty))
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!expectedDependencies.SequenceEqual(actualDependencies,
                    StringComparer.Ordinal))
                throw new ArgumentException(
                    "Le dipendenze compilate devono corrispondere alle dipendenze dirette dell'unità.",
                    nameof(dependencies));
            return new DdfDotNetArtifactManifest
            {
                ModuleId = unit.ModuleId,
                ModuleVersion = unit.ModuleVersion,
                CompilationUnitVersion = unit.Version,
                CompilationFingerprint = unit.Fingerprint,
                IrFormatVersion = unit.IrFormatVersion,
                LanguageId = unit.LanguageId,
                LanguagePackageVersion = unit.LanguagePackageVersion,
                TargetFramework = targetFramework,
                RuntimeIdentifier = runtimeIdentifier ?? string.Empty,
                RuntimeAbiVersion = runtimeAbiVersion,
                AssemblyName = assemblyName,
                BuildReceipt = buildReceipt,
                RequiredHostCapabilities = (requiredHostCapabilities ??
                        Array.Empty<string>()).OrderBy(item => item,
                        StringComparer.Ordinal).ToList(),
                Exports = unit.PublicInterface.Exports.Select(item =>
                    new DdfDotNetExportDescriptor
                    {
                        Name = item.Name,
                        Kind = item.Kind,
                        Signature = item.Signature
                    }).OrderBy(item => item.Name, StringComparer.Ordinal).ToList(),
                Imports = (imports ?? Array.Empty<DdfDotNetImportDescriptor>())
                    .OrderBy(item => item.ModuleId, StringComparer.Ordinal)
                    .ThenBy(item => item.ModuleVersion, StringComparer.Ordinal)
                    .ThenBy(item => item.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.Kind).ToList(),
                Dependencies = resolvedDependencies,
                Payloads = (payloads ?? Array.Empty<DdfDotNetPayloadDescriptor>())
                    .OrderBy(item => item.Kind).ThenBy(item => item.Path,
                        StringComparer.Ordinal).ToList()
            };
        }
    }

    public sealed class DdfDotNetSequencePoint
    {
        public int DdfStart { get; set; }
        public int DdfLength { get; set; }
        public int GeneratedStartLine { get; set; }
        public int GeneratedStartColumn { get; set; }
        public int GeneratedEndLine { get; set; }
        public int GeneratedEndColumn { get; set; }
    }

    public sealed class DdfDotNetSourceMapDocument
    {
        public string SourcePath { get; set; }
        public string SourceSha256 { get; set; }
        public string GeneratedPath { get; set; }
        public List<DdfDotNetSequencePoint> SequencePoints { get; set; } = new();
    }

    /// <summary>
    /// Maps one DDF symbol to its private CLR storage name. Debugger adapters
    /// use this record to present DDF names and types while keeping generated
    /// identifiers out of the user-facing experience.
    /// </summary>
    public sealed class DdfDotNetDebugSymbol
    {
        public string SymbolId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Type { get; set; }
        public string SourcePath { get; set; }
        public int DdfStart { get; set; }
        public int DdfLength { get; set; }
        public string StorageName { get; set; }
        public bool IsWritable { get; set; }
    }

    public sealed class DdfDotNetSourceMap
    {
        public const string FormatValue = "ddf-dotnet-source-map";
        public const string SchemaVersionValue = "1.1";

        public string Format { get; set; } = FormatValue;
        public string SchemaVersion { get; set; } = SchemaVersionValue;
        public string CompilationFingerprint { get; set; }
        public string LanguageProfileId { get; set; }
        public string LanguageProfileVersion { get; set; }
        public List<DdfDotNetSourceMapDocument> Documents { get; set; } = new();
        public List<DdfDotNetDebugSymbol> Symbols { get; set; } = new();
    }

    public sealed class DdfDotNetContractIssue
    {
        public DdfDotNetContractIssue(string code, string message,
            string sourcePath = null, int? startLine = null,
            int? startColumn = null)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SourcePath = sourcePath;
            StartLine = startLine;
            StartColumn = startColumn;
        }

        public string Code { get; }
        public string Message { get; }
        public string SourcePath { get; }
        public int? StartLine { get; }
        public int? StartColumn { get; }
        public override string ToString() => Code + ": " + Message;
    }

    public sealed class DdfDotNetContractLoadResult<T> where T : class
    {
        internal DdfDotNetContractLoadResult(T value,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Value = value;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>()).ToList().AsReadOnly();
        }

        public T Value { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Value != null && Issues.Count == 0;
    }

    public static class DdfDotNetArtifactContract
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string SerializeManifest(DdfDotNetArtifactManifest manifest)
        {
            IReadOnlyList<DdfDotNetContractIssue> issues = Validate(manifest);
            if (issues.Count != 0) throw new ArgumentException(
                string.Join(Environment.NewLine, issues), nameof(manifest));
            Normalize(manifest);
            return JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
        }

        public static string SerializeSourceMap(DdfDotNetSourceMap sourceMap)
        {
            IReadOnlyList<DdfDotNetContractIssue> issues = Validate(sourceMap);
            if (issues.Count != 0) throw new ArgumentException(
                string.Join(Environment.NewLine, issues), nameof(sourceMap));
            Normalize(sourceMap);
            return JsonSerializer.Serialize(sourceMap, JsonOptions) + "\n";
        }

        public static DdfDotNetContractLoadResult<DdfDotNetArtifactManifest>
            DeserializeManifest(string json)
        {
            try
            {
                DdfDotNetArtifactManifest value = JsonSerializer.Deserialize<
                    DdfDotNetArtifactManifest>(json ?? string.Empty, JsonOptions);
                IReadOnlyList<DdfDotNetContractIssue> issues = Validate(value);
                return new DdfDotNetContractLoadResult<DdfDotNetArtifactManifest>(
                    issues.Count == 0 ? value : null, issues);
            }
            catch (JsonException exception)
            {
                return Failure<DdfDotNetArtifactManifest>("DDFC001",
                    "Manifest .NET non leggibile: " + exception.Message);
            }
        }

        public static DdfDotNetContractLoadResult<DdfDotNetSourceMap>
            DeserializeSourceMap(string json)
        {
            try
            {
                DdfDotNetSourceMap value = JsonSerializer.Deserialize<
                    DdfDotNetSourceMap>(json ?? string.Empty, JsonOptions);
                IReadOnlyList<DdfDotNetContractIssue> issues = Validate(value);
                return new DdfDotNetContractLoadResult<DdfDotNetSourceMap>(
                    issues.Count == 0 ? value : null, issues);
            }
            catch (JsonException exception)
            {
                return Failure<DdfDotNetSourceMap>("DDFC101",
                    "Mappa sorgente .NET non leggibile: " + exception.Message);
            }
        }

        public static IReadOnlyList<DdfDotNetContractIssue> Validate(
            DdfDotNetArtifactManifest manifest)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (manifest == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC001",
                    "Manifest .NET richiesto."));
                return issues.AsReadOnly();
            }
            Require(issues, manifest.Format == DdfDotNetArtifactManifest.FormatValue,
                "DDFC002", "Formato manifest .NET non supportato.");
            Require(issues, manifest.SchemaVersion ==
                DdfDotNetArtifactManifest.SchemaVersionValue,
                "DDFC003", "Versione schema manifest .NET non supportata.");
            Require(issues, ValidId(manifest.ModuleId) &&
                Version.TryParse(manifest.ModuleVersion, out _), "DDFC004",
                "Identità o versione modulo non valida.");
            Require(issues, manifest.CompilationUnitVersion ==
                DdfCompilationUnit.ContractVersion && ValidHash(
                    manifest.CompilationFingerprint), "DDFC005",
                "Versione o fingerprint dell'unità di compilazione non valido.");
            Require(issues, manifest.IrFormatVersion == DdfIrProgram.FormatVersion,
                "DDFC006", "Versione IR non supportata.");
            Require(issues, ValidId(manifest.LanguageId) &&
                Version.TryParse(manifest.LanguagePackageVersion, out _),
                "DDFC007", "Pacchetto linguaggio non valido.");
            Require(issues, manifest.BackendId == DdfRoslynCompilerBoundary.BackendId,
                "DDFC008", "Backend artefatto non supportato.");
            Require(issues, !string.IsNullOrWhiteSpace(manifest.TargetFramework) &&
                !string.IsNullOrWhiteSpace(manifest.RuntimeAbiVersion) &&
                Version.TryParse(manifest.RuntimeAbiVersion, out _), "DDFC009",
                "Target framework o runtime ABI non valido.");
            Require(issues, string.IsNullOrEmpty(manifest.RuntimeIdentifier) ||
                Regex.IsMatch(manifest.RuntimeIdentifier,
                    "^[A-Za-z0-9][A-Za-z0-9.-]*$"), "DDFC018",
                "Runtime Identifier non valido.");
            Require(issues, ValidAssemblyName(manifest.AssemblyName), "DDFC010",
                "Nome assembly non valido.");
            Require(issues, ValidBuildReceipt(manifest.BuildReceipt), "DDFC020",
                "Ricevuta del compilatore mancante o non valida.");

            manifest.Exports ??= new();
            manifest.Imports ??= new();
            manifest.Dependencies ??= new();
            manifest.Payloads ??= new();
            manifest.RequiredHostCapabilities ??= new();
            Require(issues, manifest.RequiredHostCapabilities.All(ValidId) &&
                manifest.RequiredHostCapabilities.Distinct(StringComparer.Ordinal)
                    .Count() == manifest.RequiredHostCapabilities.Count,
                "DDFC019", "Capability host duplicate o non valide.");
            Require(issues, manifest.Exports.All(item => item != null &&
                    ValidName(item.Name) &&
                    Enum.IsDefined(typeof(DdfCompilationExportKind), item.Kind) &&
                    !string.IsNullOrWhiteSpace(item.Signature)) &&
                manifest.Exports.Select(item => item?.Name).Distinct(
                    StringComparer.Ordinal).Count() == manifest.Exports.Count,
                "DDFC011", "Export mancanti, duplicati o non validi.");
            var dependencyIdentities = new HashSet<string>(
                manifest.Dependencies.Where(item => item != null).Select(item =>
                    item.ModuleId + "@" + item.ModuleVersion),
                StringComparer.Ordinal);
            Require(issues, manifest.Imports.All(item => item != null &&
                    ValidId(item.ModuleId) &&
                    Version.TryParse(item.ModuleVersion, out _) &&
                    ValidName(item.Name) &&
                    Enum.IsDefined(typeof(DdfCompilationExportKind), item.Kind) &&
                    !string.IsNullOrWhiteSpace(item.Signature) &&
                    dependencyIdentities.Contains(item.ModuleIdentity)) &&
                manifest.Imports.Select(ImportKey).Distinct(
                    StringComparer.Ordinal).Count() == manifest.Imports.Count,
                "DDFC021", "Import mancanti, duplicati o estranei alle dipendenze.");
            Require(issues, manifest.Dependencies.All(item => item != null &&
                    ValidId(item.ModuleId) && Version.TryParse(item.ModuleVersion, out _) &&
                    ValidHash(item.CompilationFingerprint) &&
                    ValidAssemblyName(item.AssemblyName) &&
                    !string.Equals(item.ModuleId, manifest.ModuleId,
                        StringComparison.Ordinal)) &&
                manifest.Dependencies.Select(item => item?.ModuleId).Distinct(
                    StringComparer.Ordinal).Count() == manifest.Dependencies.Count,
                "DDFC012", "Dipendenze compilate mancanti, duplicate o non valide.");
            Require(issues, manifest.Payloads.All(item => item != null &&
                    SafeRelativePath(item.Path) && ValidHash(item.Sha256) && item.Size >= 0) &&
                manifest.Payloads.Select(item => item?.Path).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() == manifest.Payloads.Count,
                "DDFC013", "Payload mancanti, duplicati o non sicuri.");
            Require(issues, manifest.Payloads.Count(item => item != null &&
                    item.Kind == DdfDotNetPayloadKind.Assembly) == 1 &&
                manifest.Payloads.Count(item => item != null &&
                    item.Kind == DdfDotNetPayloadKind.PortablePdb) <= 1 &&
                manifest.Payloads.Count(item => item != null &&
                    item.Kind == DdfDotNetPayloadKind.SourceMap) <= 1,
                "DDFC014", "È richiesta una sola DLL e al massimo un PDB e una mappa sorgente.");
            return issues.AsReadOnly();
        }

        public static IReadOnlyList<DdfDotNetContractIssue> ValidateBundle(
            DdfDotNetArtifactManifest manifest, DdfDotNetSourceMap sourceMap = null)
        {
            var issues = Validate(manifest).ToList();
            if (sourceMap == null)
            {
                if (manifest != null && issues.Count == 0 && manifest.Payloads.Any(item =>
                        item.Kind == DdfDotNetPayloadKind.SourceMap))
                    issues.Add(new DdfDotNetContractIssue("DDFC016",
                        "Il manifest dichiara una mappa sorgente che non è stata fornita."));
                return issues.AsReadOnly();
            }
            issues.AddRange(Validate(sourceMap));
            if (manifest == null || issues.Count != 0) return issues.AsReadOnly();
            Require(issues, string.Equals(manifest.CompilationFingerprint,
                    sourceMap.CompilationFingerprint, StringComparison.Ordinal),
                "DDFC015", "Manifest e mappa sorgente appartengono a compilazioni diverse.");
            DdfDotNetPayloadDescriptor payload = manifest.Payloads.SingleOrDefault(item =>
                item.Kind == DdfDotNetPayloadKind.SourceMap);
            Require(issues, payload != null, "DDFC016",
                "La mappa fornita non è dichiarata tra i payload del manifest.");
            if (payload != null)
            {
                byte[] serialized = Encoding.UTF8.GetBytes(SerializeSourceMap(sourceMap));
                Require(issues, payload.Sha256 == Hash(serialized) &&
                        payload.Size == serialized.LongLength,
                    "DDFC017", "Hash o dimensione della mappa sorgente non corrisponde al manifest.");
            }
            return issues.AsReadOnly();
        }

        public static IReadOnlyList<DdfDotNetContractIssue> Validate(
            DdfDotNetSourceMap sourceMap)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (sourceMap == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC101",
                    "Mappa sorgente .NET richiesta."));
                return issues.AsReadOnly();
            }
            Require(issues, sourceMap.Format == DdfDotNetSourceMap.FormatValue,
                "DDFC102", "Formato mappa sorgente non supportato.");
            Require(issues, sourceMap.SchemaVersion ==
                DdfDotNetSourceMap.SchemaVersionValue, "DDFC103",
                "Versione schema mappa sorgente non supportata.");
            Require(issues, ValidHash(sourceMap.CompilationFingerprint),
                "DDFC104", "Fingerprint di compilazione non valido.");
            Require(issues, ValidId(sourceMap.LanguageProfileId) &&
                Version.TryParse(sourceMap.LanguageProfileVersion, out _),
                "DDFC105", "Profilo linguistico non valido.");
            sourceMap.Documents ??= new();
            Require(issues, sourceMap.Documents.Count > 0 &&
                sourceMap.Documents.All(ValidSourceMapDocument) &&
                sourceMap.Documents.Select(item => item?.SourcePath).Distinct(
                    StringComparer.Ordinal).Count() == sourceMap.Documents.Count,
                "DDFC106", "Documenti della mappa sorgente mancanti, duplicati o non validi.");
            sourceMap.Symbols ??= new();
            Require(issues, sourceMap.Symbols.All(item =>
                    ValidDebugSymbol(item, sourceMap.Documents)) &&
                sourceMap.Symbols.Select(item => item?.SymbolId).Distinct(
                    StringComparer.Ordinal).Count() == sourceMap.Symbols.Count &&
                sourceMap.Symbols.Select(item => item?.StorageName).Distinct(
                    StringComparer.Ordinal).Count() == sourceMap.Symbols.Count,
                "DDFC107", "Simboli debug mancanti, duplicati o non validi.");
            return issues.AsReadOnly();
        }

        internal static string Hash(byte[] value)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(value)).ToLowerInvariant();
        }

        private static bool ValidSourceMapDocument(DdfDotNetSourceMapDocument document)
        {
            if (document == null || !SafeRelativePath(document.SourcePath) ||
                !SafeRelativePath(document.GeneratedPath) ||
                !ValidHash(document.SourceSha256) || document.SequencePoints == null)
                return false;
            int previousEnd = -1;
            foreach (DdfDotNetSequencePoint point in document.SequencePoints)
            {
                if (point == null || point.DdfStart < 0 || point.DdfLength <= 0 ||
                    point.DdfStart < previousEnd || point.GeneratedStartLine < 1 ||
                    point.GeneratedStartColumn < 1 || point.GeneratedEndLine <
                    point.GeneratedStartLine || point.GeneratedEndColumn < 1)
                    return false;
                previousEnd = point.DdfStart + point.DdfLength;
            }
            return true;
        }

        private static bool ValidDebugSymbol(DdfDotNetDebugSymbol symbol,
            IReadOnlyCollection<DdfDotNetSourceMapDocument> documents)
        {
            bool supportedKind = Enum.TryParse(symbol?.Kind,
                out DdfBoundSymbolKind kind) && kind is
                DdfBoundSymbolKind.Structure or DdfBoundSymbolKind.Enumeration or
                DdfBoundSymbolKind.EnumerationMember or DdfBoundSymbolKind.Function or
                DdfBoundSymbolKind.Global or DdfBoundSymbolKind.Field or
                DdfBoundSymbolKind.Parameter or DdfBoundSymbolKind.Local;
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.SymbolId) ||
                string.IsNullOrWhiteSpace(symbol.Name) ||
                !supportedKind ||
                string.IsNullOrWhiteSpace(symbol.Type) ||
                !SafeRelativePath(symbol.SourcePath) || symbol.DdfStart < 0 ||
                symbol.DdfLength <= 0 || !ValidName(symbol.StorageName))
                return false;
            DdfDotNetSourceMapDocument document = documents.FirstOrDefault(item =>
                string.Equals(item.SourcePath, symbol.SourcePath,
                    StringComparison.Ordinal));
            return document != null;
        }

        private static void Normalize(DdfDotNetArtifactManifest manifest)
        {
            manifest.Exports = manifest.Exports.OrderBy(item => item.Name,
                StringComparer.Ordinal).ToList();
            manifest.Imports = manifest.Imports
                .OrderBy(item => item.ModuleId, StringComparer.Ordinal)
                .ThenBy(item => item.ModuleVersion, StringComparer.Ordinal)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.Kind).ToList();
            manifest.Dependencies = manifest.Dependencies.OrderBy(item => item.ModuleId,
                StringComparer.Ordinal).ToList();
            manifest.Payloads = manifest.Payloads.OrderBy(item => item.Kind)
                .ThenBy(item => item.Path, StringComparer.Ordinal).ToList();
            manifest.RequiredHostCapabilities = manifest.RequiredHostCapabilities
                .OrderBy(item => item, StringComparer.Ordinal).ToList();
        }

        private static void Normalize(DdfDotNetSourceMap sourceMap)
        {
            sourceMap.Documents = sourceMap.Documents.OrderBy(item => item.SourcePath,
                StringComparer.Ordinal).ToList();
            foreach (DdfDotNetSourceMapDocument document in sourceMap.Documents)
                document.SequencePoints = document.SequencePoints.OrderBy(item => item.DdfStart)
                    .ToList();
            sourceMap.Symbols = sourceMap.Symbols.OrderBy(item => item.SymbolId,
                StringComparer.Ordinal).ToList();
        }

        private static void Require(ICollection<DdfDotNetContractIssue> issues,
            bool condition, string code, string message)
        {
            if (!condition) issues.Add(new DdfDotNetContractIssue(code, message));
        }

        private static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(value, "^[a-z][a-z0-9.-]*$");
        private static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$");
        private static bool ValidAssemblyName(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(
                Path.GetInvalidFileNameChars()) < 0 && value.IndexOfAny(
                new[] { '/', '\\' }) < 0;
        private static bool ValidHash(string value) => value != null &&
            Regex.IsMatch(value, "^[a-f0-9]{64}$");
        private static bool ValidBuildReceipt(DdfDotNetBuildReceipt receipt) =>
            receipt != null &&
            receipt.CompilationPlanVersion == DdfDotNetCompilationPlan.FormatVersion &&
            ValidHash(receipt.CompilationPlanFingerprint) &&
            receipt.CompilerProfileVersion == DdfDotNetCompilerProfile.FormatVersion &&
            Version.TryParse(receipt.RoslynVersion, out _) &&
            receipt.CSharpLanguageVersion == DdfDotNetCompilerProfile
                .CSharpLanguageVersion.ToString() &&
            ValidHash(receipt.ReferenceSetFingerprint);
        private static string ImportKey(DdfDotNetImportDescriptor item) =>
            item == null ? string.Empty : item.ModuleIdentity + ":" +
                item.Kind + ":" + item.Name;
        private static bool SafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return false;
            string normalized = value.Replace('/', Path.DirectorySeparatorChar);
            return normalized.Split(Path.DirectorySeparatorChar).All(segment =>
                segment.Length > 0 && segment != "." && segment != "..") &&
                normalized.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }

        private static DdfDotNetContractLoadResult<T> Failure<T>(string code,
            string message) where T : class => new(null,
                new[] { new DdfDotNetContractIssue(code, message) });
    }
}
