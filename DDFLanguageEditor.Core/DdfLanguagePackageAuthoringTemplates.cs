using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguageAuthoringTemplateKind
    {
        Blank,
        EmbeddedDdf,
        DirectoryPackage,
        ArchivePackage,
        InstalledPackage
    }

    public sealed class DdfLanguageAuthoringIdentity
    {
        public DdfLanguageAuthoringIdentity(string id, string name,
            string packageVersion, string languageVersion,
            IEnumerable<string> fileExtensions)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !Regex.IsMatch(id, "^[a-z][a-z0-9.-]*$"))
                throw new ArgumentException("Identificatore pacchetto non valido.",
                    nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome pacchetto richiesto.", nameof(name));
            if (!Version.TryParse(packageVersion, out _))
                throw new ArgumentException("Versione pacchetto non valida.",
                    nameof(packageVersion));
            if (!Version.TryParse(languageVersion, out _))
                throw new ArgumentException("Versione linguaggio non valida.",
                    nameof(languageVersion));
            var extensions = (fileExtensions ??
                throw new ArgumentNullException(nameof(fileExtensions))).ToList();
            if (extensions.Count == 0 || extensions.Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() != extensions.Count ||
                extensions.Any(item => item == null ||
                    !Regex.IsMatch(item, "^\\.[A-Za-z0-9]+$")))
                throw new ArgumentException("Estensioni file non valide o duplicate.",
                    nameof(fileExtensions));

            Id = id;
            Name = name.Trim();
            PackageVersion = packageVersion;
            LanguageVersion = languageVersion;
            FileExtensions = extensions.AsReadOnly();
        }

        public string Id { get; }
        public string Name { get; }
        public string PackageVersion { get; }
        public string LanguageVersion { get; }
        public IReadOnlyList<string> FileExtensions { get; }
    }

    public sealed class DdfLanguageAuthoringTemplateResult
    {
        internal DdfLanguageAuthoringTemplateResult(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringTemplateKind templateKind, string source,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            Project = project;
            TemplateKind = templateKind;
            Source = source ?? string.Empty;
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>())
                .ToList().AsReadOnly();
        }

        public DdfLanguageAuthoringProject Project { get; }
        public DdfLanguageAuthoringTemplateKind TemplateKind { get; }
        public string Source { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => Project != null && !Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public static class DdfLanguageAuthoringProjectFactory
    {
        private const int MaximumArchiveFiles = 64;
        private const long MaximumArchiveFileBytes = 16L * 1024 * 1024;
        private const long MaximumArchiveBytes = 64L * 1024 * 1024;
        private static readonly JsonSerializerOptions IndentedJson =
            new JsonSerializerOptions { WriteIndented = true };

        public static DdfLanguageAuthoringTemplateResult CreateBlank(
            DdfLanguageAuthoringIdentity identity)
        {
            DdfLanguageAuthoringTemplateResult embedded = LoadEmbeddedSnapshot();
            if (!embedded.Success) return embedded;
            DdfLanguageAuthoringProject project = RewriteIdentity(
                embedded.Project, identity, "template://blank", out var diagnostics);
            if (project == null)
                return Result(null, DdfLanguageAuthoringTemplateKind.Blank,
                    "template://blank", diagnostics);

            project = KeepMinimumDeclarativeTest(project, out diagnostics);
            if (project == null)
                return Result(null, DdfLanguageAuthoringTemplateKind.Blank,
                    "template://blank", diagnostics);
            return ValidateResult(project, DdfLanguageAuthoringTemplateKind.Blank,
                "template://blank");
        }

        public static DdfLanguageAuthoringTemplateResult CloneEmbeddedDdf(
            DdfLanguageAuthoringIdentity identity)
        {
            DdfLanguageAuthoringTemplateResult embedded = LoadEmbeddedSnapshot();
            if (!embedded.Success) return embedded;
            DdfLanguageAuthoringProject project = RewriteIdentity(
                embedded.Project, identity, "embedded://ddf", out var diagnostics);
            return project == null
                ? Result(null, DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                    "embedded://ddf", diagnostics)
                : ValidateResult(project,
                    DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                    "embedded://ddf");
        }

        public static DdfLanguageAuthoringTemplateResult ClonePackage(
            string path, DdfLanguageAuthoringIdentity identity)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Failure(DdfLanguageAuthoringTemplateKind.DirectoryPackage,
                    path, "DDFPAT001", "Percorso del pacchetto non specificato.");
            DdfLanguagePackageLoadResult package =
                DdfLanguagePackageLoader.Load(path);
            if (!package.Success)
                return Result(null, Directory.Exists(path)
                    ? DdfLanguageAuthoringTemplateKind.DirectoryPackage
                    : DdfLanguageAuthoringTemplateKind.ArchivePackage,
                    path, package.Diagnostics);

            bool directory = Directory.Exists(path);
            DdfLanguageAuthoringTemplateKind kind = directory
                ? DdfLanguageAuthoringTemplateKind.DirectoryPackage
                : DdfLanguageAuthoringTemplateKind.ArchivePackage;
            string temporary = null;
            try
            {
                string source = Path.GetFullPath(path);
                DdfLanguageAuthoringResult loaded;
                if (directory)
                    loaded = DdfLanguagePackageAuthoring.LoadDirectory(source);
                else
                {
                    temporary = Path.Combine(Path.GetTempPath(),
                        "ddf-authoring-clone-" + Guid.NewGuid().ToString("N"));
                    ExtractArchive(source, temporary);
                    loaded = DdfLanguagePackageAuthoring.LoadDirectory(temporary);
                }
                if (!loaded.Success)
                    return Result(null, kind, source, loaded.Diagnostics);
                DdfLanguageAuthoringProject clone = RewriteIdentity(loaded.Project,
                    identity, source, out var diagnostics);
                return clone == null
                    ? Result(null, kind, source, diagnostics)
                    : ValidateResult(clone, kind, source);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return Failure(kind, path, "DDFPAT002", exception.Message);
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        public static DdfLanguageAuthoringTemplateResult CloneInstalledPackage(
            DdfInstalledLanguagePackage installed,
            DdfLanguageAuthoringIdentity identity)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            DdfLanguagePackageLoadResult source =
                DdfLanguagePackageLoader.Load(installed.Path);
            if (!source.Success)
                return Result(null, DdfLanguageAuthoringTemplateKind.InstalledPackage,
                    installed.Path, source.Diagnostics);
            if (!string.Equals(source.Package.Id, installed.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(source.Package.PackageVersion, installed.Version,
                    StringComparison.Ordinal))
                return Failure(DdfLanguageAuthoringTemplateKind.InstalledPackage,
                    installed.Path, "DDFPAT003",
                    "Il descrittore installato non coincide con il pacchetto su disco.");

            DdfLanguageAuthoringTemplateResult clone = ClonePackage(installed.Path,
                identity);
            return new DdfLanguageAuthoringTemplateResult(clone.Project,
                DdfLanguageAuthoringTemplateKind.InstalledPackage,
                installed.Path, clone.Diagnostics);
        }

        private static DdfLanguageAuthoringTemplateResult LoadEmbeddedSnapshot()
        {
            string temporary = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-embedded-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(temporary);
                DdfLanguageAuthoringResult loaded =
                    DdfLanguagePackageAuthoring.LoadDirectory(temporary);
                if (!loaded.Success)
                    return Result(null,
                        DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                        "embedded://ddf", loaded.Diagnostics);
                return Result(new DdfLanguageAuthoringProject("embedded://ddf", 0,
                        loaded.Project.Documents.Values),
                    DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                    "embedded://ddf", Array.Empty<DdfLanguagePackageDiagnostic>());
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return Failure(DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                    "embedded://ddf", "DDFPAT004", exception.Message);
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        private static DdfLanguageAuthoringProject RewriteIdentity(
            DdfLanguageAuthoringProject source,
            DdfLanguageAuthoringIdentity identity, string sourceRoot,
            out IReadOnlyList<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            try
            {
                if (!source.TryGetDocument("manifest.json", out var manifest))
                {
                    diagnostics = new[] { Diagnostic("DDFPAT005",
                        "manifest.json non presente nel modello.", "manifest.json") };
                    return null;
                }
                JsonObject json = JsonNode.Parse(manifest.Content,
                    documentOptions: new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    })?.AsObject();
                if (json == null)
                    throw new JsonException("Manifest non valido.");
                json["id"] = identity.Id;
                json["name"] = identity.Name;
                json["packageVersion"] = identity.PackageVersion;
                json["languageVersion"] = identity.LanguageVersion;
                json["fileExtensions"] = new JsonArray(identity.FileExtensions
                    .Select(item => JsonValue.Create(item)).ToArray());

                var documents = source.Documents.Values
                    .Where(item => !string.Equals(item.RelativePath, "manifest.json",
                        StringComparison.OrdinalIgnoreCase)).ToList();
                documents.Add(new DdfLanguageAuthoringDocument("manifest.json",
                    json.ToJsonString(IndentedJson) + Environment.NewLine));
                diagnostics = Array.Empty<DdfLanguagePackageDiagnostic>();
                return new DdfLanguageAuthoringProject(sourceRoot, 0, documents);
            }
            catch (JsonException exception)
            {
                diagnostics = new[] { Diagnostic("DDFPAT006", exception.Message,
                    "manifest.json") };
                return null;
            }
        }

        private static DdfLanguageAuthoringProject KeepMinimumDeclarativeTest(
            DdfLanguageAuthoringProject source,
            out IReadOnlyList<DdfLanguagePackageDiagnostic> diagnostics)
        {
            try
            {
                DdfLanguageAuthoringDocument tests = source.Documents["tests.json"];
                JsonObject json = JsonNode.Parse(tests.Content,
                    documentOptions: new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    }).AsObject();
                JsonArray cases = json["cases"]?.AsArray();
                if (cases == null || cases.Count == 0)
                    throw new JsonException(
                        "Il modello non contiene il test minimo obbligatorio.");
                json["cases"] = new JsonArray(cases[0].DeepClone());
                var documents = source.Documents.Values
                    .Where(item => !string.Equals(item.RelativePath, "tests.json",
                        StringComparison.OrdinalIgnoreCase)).ToList();
                documents.Add(new DdfLanguageAuthoringDocument("tests.json",
                    json.ToJsonString(IndentedJson) + Environment.NewLine));
                diagnostics = Array.Empty<DdfLanguagePackageDiagnostic>();
                return new DdfLanguageAuthoringProject(source.SourceRoot, 0,
                    documents);
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is KeyNotFoundException)
            {
                diagnostics = new[] { Diagnostic("DDFPAT007", exception.Message,
                    "tests.json") };
                return null;
            }
        }

        private static DdfLanguageAuthoringTemplateResult ValidateResult(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringTemplateKind kind, string source)
        {
            DdfLanguagePackageLoadResult validation =
                DdfLanguagePackageAuthoring.Validate(project);
            return validation.Success
                ? Result(project, kind, source, validation.Diagnostics)
                : Result(null, kind, source, validation.Diagnostics);
        }

        private static void ExtractArchive(string archivePath, string destination)
        {
            Directory.CreateDirectory(destination);
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaximumArchiveFiles)
                throw new InvalidDataException("Archivio oltre il limite di file.");
            long total = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) ||
                    entry.FullName != entry.Name ||
                    !DdfLanguageAuthoringPaths.TryNormalize(entry.Name,
                        out string normalized) || normalized.Contains('/'))
                    throw new InvalidDataException(
                        "L'archivio contiene un percorso non sicuro.");
                if (!names.Add(normalized))
                    throw new InvalidDataException(
                        "L'archivio contiene file duplicati.");
                if (entry.Length < 0 || entry.Length > MaximumArchiveFileBytes ||
                    total + entry.Length > MaximumArchiveBytes)
                    throw new InvalidDataException("Archivio oltre quota.");
                total += entry.Length;
                string output = Path.Combine(destination, normalized);
                using Stream input = entry.Open();
                using FileStream file = new FileStream(output, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                input.CopyTo(file);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static DdfLanguageAuthoringTemplateResult Failure(
            DdfLanguageAuthoringTemplateKind kind, string source, string code,
            string message) => Result(null, kind, source,
                new[] { Diagnostic(code, message, source) });

        private static DdfLanguageAuthoringTemplateResult Result(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringTemplateKind kind, string source,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics) =>
            new DdfLanguageAuthoringTemplateResult(project, kind, source,
                diagnostics);

        private static DdfLanguagePackageDiagnostic Diagnostic(string code,
            string message, string file = null) =>
            new DdfLanguagePackageDiagnostic(code, message, file, severity:
                DdfLanguagePackageDiagnosticSeverity.Error,
                domain: DdfDiagnosticDomain.LanguageDefinition);
    }
}
