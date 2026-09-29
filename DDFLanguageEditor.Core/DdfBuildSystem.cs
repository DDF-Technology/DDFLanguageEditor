using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfBuildDefinition
    {
        private readonly IReadOnlyCollection<string> backends;

        public DdfBuildDefinition(string defaultBackend, string artifactFormat, IEnumerable<string> backends)
        {
            if (defaultBackend != DdfVmBuildBackend.BackendId)
                throw new DdfEngineCapabilityException("Backend predefinito non supportato: " + defaultBackend + ".");
            if (artifactFormat != DdfBuildArtifact.ReceiptFormat)
                throw new DdfEngineCapabilityException("Formato artefatto non supportato: " + artifactFormat + ".");
            var values = (backends ?? throw new ArgumentNullException(nameof(backends))).ToList();
            if (values.Count == 0 || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new ArgumentException("I backend devono essere presenti e univoci.", nameof(backends));
            if (values.Any(item => item != DdfVmBuildBackend.BackendId))
                throw new DdfEngineCapabilityException("Il pacchetto richiede un backend non supportato.");
            if (!values.Contains(defaultBackend, StringComparer.Ordinal))
                throw new ArgumentException("Il backend predefinito deve essere incluso nell'elenco.", nameof(defaultBackend));
            DefaultBackend = defaultBackend;
            ArtifactFormat = artifactFormat;
            this.backends = values.AsReadOnly();
        }

        public string DefaultBackend { get; }
        public string ArtifactFormat { get; }
        public IReadOnlyCollection<string> Backends => backends;

        public static DdfBuildDefinition CreateDdfCompatibleDefault() =>
            new DdfBuildDefinition(DdfVmBuildBackend.BackendId, DdfBuildArtifact.ReceiptFormat,
                new[] { DdfVmBuildBackend.BackendId });
    }

    public sealed class DdfProjectDependency
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public sealed class DdfProjectModule
    {
        public string Id { get; set; }
        public string Version { get; set; } = "0.0.0";
        public List<string> Exports { get; set; } = new List<string>();
    }

    public sealed class DdfProjectOutput
    {
        public string Directory { get; set; } = ".ddf-build";
        public string Artifact { get; set; } = "project.ddfbuild.json";
    }

    public sealed class DdfProjectOptions
    {
        public bool UseUnsavedBuffers { get; set; } = true;
        public int? MaxInstructions { get; set; }
    }

    public sealed class DdfProjectManifest
    {
        public const string FileName = "ddf-project.json";
        public const string SchemaVersionValue = "1.1";

        [JsonPropertyName("$schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Schema { get; set; }
        public string SchemaVersion { get; set; } = SchemaVersionValue;
        public string Name { get; set; }
        public DdfProjectModule Module { get; set; } = new DdfProjectModule();
        public string LanguageId { get; set; }
        public string LanguagePackageVersion { get; set; }
        public List<string> Sources { get; set; } = new List<string>();
        public string EntrySource { get; set; }
        public string EntryPoint { get; set; }
        public List<DdfProjectDependency> Dependencies { get; set; } = new List<DdfProjectDependency>();
        public string Backend { get; set; }
        public DdfProjectOutput Output { get; set; } = new DdfProjectOutput();
        public DdfProjectOptions Options { get; set; } = new DdfProjectOptions();

        public static DdfProjectManifest CreateImplicit(string name, DdfLanguagePackage package,
            string entrySource = null)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            return new DdfProjectManifest
            {
                Name = string.IsNullOrWhiteSpace(name) ? "DDF project" : name,
                Module = new DdfProjectModule
                {
                    Id = CreateImplicitModuleId(name),
                    Version = "0.0.0"
                },
                LanguageId = package.Id,
                LanguagePackageVersion = package.PackageVersion,
                Sources = package.FileExtensions.Select(extension => "**/*" + extension).ToList(),
                EntrySource = entrySource,
                EntryPoint = package.Language.Runtime.EntryPoint,
                Backend = package.Language.Build.DefaultBackend
            };
        }

        private static string CreateImplicitModuleId(string name)
        {
            string value = Regex.Replace((name ?? "project").Trim().ToLowerInvariant(),
                "[^a-z0-9]+", "-").Trim('-');
            if (value.Length == 0 || value[0] < 'a' || value[0] > 'z') value = "project-" + value;
            return "local." + value.TrimEnd('-');
        }
    }

    public sealed class DdfProjectManifestLoadResult
    {
        internal DdfProjectManifestLoadResult(DdfProjectManifest manifest, IEnumerable<DdfBuildIssue> issues)
        { Manifest = manifest; Issues = issues.ToList().AsReadOnly(); }
        public DdfProjectManifest Manifest { get; }
        public IReadOnlyList<DdfBuildIssue> Issues { get; }
        public bool Success => Manifest != null && Issues.All(item => item.Severity != DdfDiagnosticSeverity.Error);
    }

    public static class DdfProjectManifestLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static DdfProjectManifestLoadResult Load(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot)) return Failure("DDFP001", "Cartella progetto richiesta.");
            string root;
            try { root = Path.GetFullPath(workspaceRoot); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            { return Failure("DDFP001", exception.Message); }
            string path = Path.Combine(root, DdfProjectManifest.FileName);
            if (!File.Exists(path)) return Failure("DDFP002", "Manifest progetto non trovato.", path);
            try
            {
                DdfProjectManifest manifest = JsonSerializer.Deserialize<DdfProjectManifest>(
                    File.ReadAllText(path, Encoding.UTF8), Options);
                if (manifest == null) return Failure("DDFP003", "Manifest progetto vuoto.", path);
                var issues = Validate(manifest, root, path);
                return new DdfProjectManifestLoadResult(issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error)
                    ? null : manifest, issues);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            { return Failure("DDFP003", exception.Message, path); }
        }

        public static IReadOnlyList<DdfBuildIssue> Validate(DdfProjectManifest manifest, string workspaceRoot,
            string manifestPath = null)
        {
            var issues = new List<DdfBuildIssue>();
            string path = manifestPath ?? Path.Combine(workspaceRoot ?? string.Empty, DdfProjectManifest.FileName);
            void Require(bool condition, string code, string message)
            {
                if (!condition) issues.Add(DdfBuildIssue.Configuration(code, message, path));
            }
            Require(manifest != null, "DDFP003", "Manifest progetto richiesto.");
            if (manifest == null) return issues.AsReadOnly();
            Require(manifest.SchemaVersion == DdfProjectManifest.SchemaVersionValue, "DDFP004", "schemaVersion progetto non supportata.");
            Require(!string.IsNullOrWhiteSpace(manifest.Name), "DDFP005", "Nome progetto richiesto.");
            Require(manifest.Module != null && ValidId(manifest.Module.Id) &&
                    Version.TryParse(manifest.Module.Version, out _),
                "DDFP015", "Identità o versione del modulo non valida.");
            if (manifest.Module != null)
            {
                manifest.Module.Exports = manifest.Module.Exports ?? new List<string>();
                Require(manifest.Module.Exports.All(ValidName) &&
                        manifest.Module.Exports.Distinct(StringComparer.Ordinal).Count() ==
                        manifest.Module.Exports.Count,
                    "DDFP016", "Esportazioni del modulo duplicate o non valide.");
            }
            Require(ValidId(manifest.LanguageId), "DDFP006", "ID del linguaggio non valido.");
            Require(Version.TryParse(manifest.LanguagePackageVersion, out _), "DDFP007", "Versione esatta del pacchetto linguaggio richiesta.");
            Require(manifest.Sources != null && manifest.Sources.Count > 0 &&
                    manifest.Sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() == manifest.Sources.Count &&
                    manifest.Sources.All(SafeSourcePattern), "DDFP008", "Sorgenti mancanti, duplicati o non sicuri.");
            Require(string.IsNullOrWhiteSpace(manifest.EntrySource) || SafeRelativePath(manifest.EntrySource),
                "DDFP009", "Sorgente di ingresso non sicura.");
            Require(ValidName(manifest.EntryPoint), "DDFP010", "Funzione di ingresso non valida.");
            Require(ValidId(manifest.Backend), "DDFP011", "Backend non valido.");
            manifest.Dependencies = manifest.Dependencies ?? new List<DdfProjectDependency>();
            Require(manifest.Dependencies.All(item => item != null && ValidId(item.Id) && Version.TryParse(item.Version, out _)) &&
                    manifest.Dependencies.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == manifest.Dependencies.Count,
                "DDFP012", "Dipendenze duplicate o non valide.");
            Require(manifest.Module == null || manifest.Dependencies.All(item => item == null ||
                    !string.Equals(item.Id, manifest.Module.Id, StringComparison.Ordinal)),
                "DDFP017", "Un modulo non può dipendere da se stesso.");
            manifest.Output = manifest.Output ?? new DdfProjectOutput();
            Require(SafeRelativePath(manifest.Output.Directory) && SafeFileName(manifest.Output.Artifact),
                "DDFP013", "Percorso di output non sicuro.");
            manifest.Options = manifest.Options ?? new DdfProjectOptions();
            Require(!manifest.Options.MaxInstructions.HasValue || manifest.Options.MaxInstructions.Value > 0,
                "DDFP014", "Il limite istruzioni deve essere positivo.");
            return issues.AsReadOnly();
        }

        private static DdfProjectManifestLoadResult Failure(string code, string message, string path = null) =>
            new DdfProjectManifestLoadResult(null, new[] { DdfBuildIssue.Configuration(code, message, path) });
        private static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[a-z][a-z0-9.-]*$");
        private static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$");
        private static bool SafeFileName(string value) => !string.IsNullOrWhiteSpace(value) && value == Path.GetFileName(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        private static bool SafeSourcePattern(string value) => !string.IsNullOrWhiteSpace(value) &&
            (Regex.IsMatch(value, "^\\*\\*/\\*\\.[A-Za-z0-9]+$") || SafeRelativePath(value));
        internal static bool SafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return false;
            string normalized = value.Replace('/', Path.DirectorySeparatorChar);
            return normalized.Split(Path.DirectorySeparatorChar).All(segment => segment.Length > 0 && segment != "." && segment != "..") &&
                   normalized.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }
    }

    public sealed class DdfBuildSource
    {
        public DdfBuildSource(string path, string relativePath, string source, bool fromUnsavedBuffer)
        { Path = path; RelativePath = relativePath; Source = source ?? string.Empty; FromUnsavedBuffer = fromUnsavedBuffer; }
        public string Path { get; }
        public string RelativePath { get; }
        public string Source { get; }
        public bool FromUnsavedBuffer { get; }
    }

    public sealed class DdfBuildIssue
    {
        private DdfBuildIssue(string code, string message, DdfDiagnosticSeverity severity,
            DdfDiagnosticDomain domain, string documentPath, int start, int length, int line, int column)
        { Code = code; Message = message; Severity = severity; Domain = domain; DocumentPath = documentPath ?? string.Empty; Start = start; Length = length; Line = line; Column = column; }
        public string Code { get; }
        public string Message { get; }
        public DdfDiagnosticSeverity Severity { get; }
        public DdfDiagnosticDomain Domain { get; }
        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public static DdfBuildIssue FromDiagnostic(string path, DdfDiagnostic diagnostic) =>
            new DdfBuildIssue(diagnostic.Code, diagnostic.Message, diagnostic.Severity, diagnostic.Domain,
                path, diagnostic.Start, diagnostic.Length, diagnostic.Line, diagnostic.Column);
        public static DdfBuildIssue Configuration(string code, string message, string path = null,
            DdfDiagnosticDomain domain = DdfDiagnosticDomain.Source) =>
            new DdfBuildIssue(code, message, DdfDiagnosticSeverity.Error, domain, path, 0, 1, 1, 1);
        public override string ToString() => "[" + DdfDiagnosticDomainNames.GetDisplayName(Domain) + "] " + Code + ": " + Message +
            (string.IsNullOrEmpty(DocumentPath) ? string.Empty : " (" + Path.GetFileName(DocumentPath) + ":" + Line + ":" + Column + ")");
    }

    public sealed class DdfBuildArtifactSource
    {
        internal DdfBuildArtifactSource(string relativePath, string sha256, bool fromUnsavedBuffer)
        { RelativePath = relativePath; Sha256 = sha256; FromUnsavedBuffer = fromUnsavedBuffer; }
        public string RelativePath { get; }
        public string Sha256 { get; }
        public bool FromUnsavedBuffer { get; }
    }

    public sealed class DdfBuildArtifact
    {
        public const string ReceiptFormat = "ddf-build-receipt-1.0";
        internal DdfBuildArtifact(string projectName, string moduleId, string moduleVersion,
            IReadOnlyList<string> exports, string languageId, string languagePackageVersion,
            string backend, string entrySource, string entryPoint, string fingerprint,
            IReadOnlyList<DdfBuildArtifactSource> sources, IReadOnlyList<DdfProjectDependency> dependencies,
            DdfIrProgram entryProgram, IReadOnlyList<DdfIrModule> runtimeModules,
            int maxInstructions, bool useUnsavedBuffers)
        { ProjectName = projectName; ModuleId = moduleId; ModuleVersion = moduleVersion; Exports = exports; LanguageId = languageId; LanguagePackageVersion = languagePackageVersion; Backend = backend; EntrySource = entrySource; EntryPoint = entryPoint; Fingerprint = fingerprint; Sources = sources; Dependencies = dependencies; EntryProgram = entryProgram; RuntimeModules = runtimeModules; MaxInstructions = maxInstructions; UseUnsavedBuffers = useUnsavedBuffers; }
        public string Format => ReceiptFormat;
        public string ProjectName { get; }
        public string ModuleId { get; }
        public string ModuleVersion { get; }
        public IReadOnlyList<string> Exports { get; }
        public string LanguageId { get; }
        public string LanguagePackageVersion { get; }
        public string Backend { get; }
        public string EntrySource { get; }
        public string EntryPoint { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<DdfBuildArtifactSource> Sources { get; }
        public IReadOnlyList<DdfProjectDependency> Dependencies { get; }
        [JsonIgnore] public DdfIrProgram EntryProgram { get; }
        [JsonIgnore] public IReadOnlyList<DdfIrModule> RuntimeModules { get; }
        public int MaxInstructions { get; }
        public bool UseUnsavedBuffers { get; }
    }

    public sealed class DdfBuildResult
    {
        internal DdfBuildResult(DdfBuildArtifact artifact, IEnumerable<DdfBuildIssue> issues, string artifactPath = null)
        { Artifact = artifact; Issues = issues.ToList().AsReadOnly(); ArtifactPath = artifactPath ?? string.Empty; }

        public static DdfBuildResult Failure(params DdfBuildIssue[] issues) =>
            new DdfBuildResult(null, issues ?? Array.Empty<DdfBuildIssue>());

        public static DdfBuildResult Failure(IEnumerable<DdfBuildIssue> issues) =>
            new DdfBuildResult(null, issues ?? Array.Empty<DdfBuildIssue>());

        public DdfBuildArtifact Artifact { get; }
        public IReadOnlyList<DdfBuildIssue> Issues { get; }
        public string ArtifactPath { get; }
        public bool Success => Artifact != null && Issues.All(item => item.Severity != DdfDiagnosticSeverity.Error);
    }

    public interface IDdfBuildBackend
    {
        string Id { get; }
        DdfBuildResult Build(string workspaceRoot, DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyList<DdfBuildSource> sources);
    }

    public sealed class DdfVmBuildBackend : IDdfBuildBackend
    {
        public const string BackendId = "ddf-vm-ir-v1";
        public string Id => BackendId;

        public DdfBuildResult Build(string workspaceRoot, DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyList<DdfBuildSource> sources)
        {
            return Build(workspaceRoot, manifest, package, sources, null);
        }

        public DdfBuildResult Build(string workspaceRoot, DdfProjectManifest manifest,
            DdfLanguagePackage package, IReadOnlyList<DdfBuildSource> sources,
            DdfHostCatalogSnapshot hostCatalog)
        {
            var issues = new List<DdfBuildIssue>();
            var parsed = sources.Select(item => new { Source = item, Parse = DdfParser.Parse(item.Source, package.Language) }).ToList();
            foreach (var item in parsed)
                issues.AddRange(item.Parse.Diagnostics.Select(diagnostic => DdfBuildIssue.FromDiagnostic(item.Source.Path, diagnostic)));
            var programs = new Dictionary<string, DdfIrProgram>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in parsed)
            {
                var externalRoots = parsed.Where(other => !ReferenceEquals(other, item)).Select(other => other.Parse.Root).ToList();
                DdfTypeCheckResult types = DdfTypeChecker.Check(item.Source.Source, item.Parse.Root, externalRoots,
                    package.Language.Types, package.Language.StandardLibrary, hostCatalog,
                    package.Language.Runtime);
                issues.AddRange(types.Diagnostics.Select(diagnostic => DdfBuildIssue.FromDiagnostic(item.Source.Path, diagnostic)));
                DdfIrBuildResult ir = DdfIrLowerer.Lower(item.Source.Source, item.Parse, package.Language);
                issues.AddRange(ir.Diagnostics.Select(diagnostic => DdfBuildIssue.FromDiagnostic(item.Source.Path, diagnostic)));
                if (ir.Success) programs[item.Source.RelativePath] = ir.Program;
            }
            if (issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error)) return new DdfBuildResult(null, issues);

            DdfBuildSource[] entryCandidates = sources.Where(item =>
                programs.TryGetValue(item.RelativePath, out DdfIrProgram candidateProgram) &&
                candidateProgram.Members.OfType<DdfIrFunction>().Any(function => function.Name == manifest.EntryPoint))
                .ToArray();
            if (entryCandidates.Length > 1)
            {
                issues.Add(DdfBuildIssue.Configuration("DDFB009",
                    "La funzione di ingresso '" + manifest.EntryPoint + "' deve essere unica nel progetto. Trovata in: " +
                    string.Join(", ", entryCandidates.Select(item => item.RelativePath)) + "."));
                return new DdfBuildResult(null, issues);
            }
            DdfBuildSource entry = ResolveEntrySource(manifest, sources, programs);
            if (entry == null)
            {
                issues.Add(DdfBuildIssue.Configuration("DDFB001", "Sorgente o funzione di ingresso non trovata."));
                return new DdfBuildResult(null, issues);
            }
            var declarations = programs.SelectMany(pair => pair.Value.Members.Select(member => new
                {
                    Path = pair.Key,
                    Kind = member is DdfIrFunction ? "funzione" : member is DdfIrStructure ? "struttura" :
                        member is DdfIrGlobal global && global.Statement is DdfIrVariableDeclaration ? "globale" : null,
                    Name = member is DdfIrFunction function ? function.Name : member is DdfIrStructure structure ? structure.Name :
                        member is DdfIrGlobal globalMember && globalMember.Statement is DdfIrVariableDeclaration variable ? variable.Name : null
                }))
                .Where(item => item.Kind != null && !string.IsNullOrEmpty(item.Name))
                .ToList();
            var duplicateDeclarations = declarations.GroupBy(item => item.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList();
            foreach (var duplicate in duplicateDeclarations)
                issues.Add(DdfBuildIssue.Configuration("DDFB010",
                    "Dichiarazione runtime duplicata '" + duplicate.Key + "': " +
                    string.Join(", ", duplicate.Select(item => item.Path)) + "."));
            foreach (string export in manifest.Module.Exports)
                if (!declarations.Any(item => item.Name == export))
                    issues.Add(DdfBuildIssue.Configuration("DDFB012",
                        "L'esportazione '" + export + "' non corrisponde a una dichiarazione del modulo '" +
                        manifest.Module.Id + "'."));
            if (issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error))
                return new DdfBuildResult(null, issues);

            var orderedGlobals = sources.SelectMany(source => programs[source.RelativePath].Members
                .OfType<DdfIrGlobal>()
                .Where(global => global.Statement is DdfIrVariableDeclaration)
                .Select(global => new { Source = source, Variable = (DdfIrVariableDeclaration)global.Statement }))
                .ToList();
            var globalOrder = orderedGlobals.Select((item, index) => new { item.Variable.Name, Index = index })
                .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
            for (int index = 0; index < orderedGlobals.Count; index++)
            {
                var current = orderedGlobals[index];
                foreach (string dependency in EnumerateNames(current.Variable.Initializer).Distinct(StringComparer.Ordinal))
                    if (globalOrder.TryGetValue(dependency, out int dependencyIndex) && dependencyIndex >= index)
                        issues.Add(DdfBuildIssue.Configuration("DDFB011",
                            "La globale '" + current.Variable.Name + "' dipende da '" + dependency +
                            "', dichiarata successivamente o coinvolta in un ciclo. L'inizializzazione segue l'ordine dei file e delle dichiarazioni.",
                            current.Source.Path));
            }
            if (issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error))
                return new DdfBuildResult(null, issues);

            int maxInstructions = manifest.Options.MaxInstructions ?? package.Language.Runtime.DefaultInstructionLimit;
            IReadOnlyList<DdfBuildArtifactSource> artifactSources = sources.Select(item =>
                new DdfBuildArtifactSource(item.RelativePath, Hash(item.Source), item.FromUnsavedBuffer)).ToList().AsReadOnly();
            string fingerprint = Fingerprint(manifest, package, artifactSources, maxInstructions);
            IReadOnlyList<DdfIrModule> runtimeModules = sources
                .Where(item => programs.ContainsKey(item.RelativePath))
                .Select(item => new DdfIrModule(item.Path, item.Source, programs[item.RelativePath]))
                .ToList().AsReadOnly();
            var artifact = new DdfBuildArtifact(manifest.Name, manifest.Module.Id,
                manifest.Module.Version, manifest.Module.Exports.OrderBy(item => item, StringComparer.Ordinal)
                    .ToList().AsReadOnly(), package.Id, package.PackageVersion, Id,
                entry.RelativePath, manifest.EntryPoint, fingerprint, artifactSources,
                manifest.Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal)
                    .Select(item => new DdfProjectDependency { Id = item.Id, Version = item.Version }).ToList().AsReadOnly(),
                programs[entry.RelativePath], runtimeModules, maxInstructions, manifest.Options.UseUnsavedBuffers);
            return new DdfBuildResult(artifact, issues);
        }

        private static DdfBuildSource ResolveEntrySource(DdfProjectManifest manifest, IReadOnlyList<DdfBuildSource> sources,
            IReadOnlyDictionary<string, DdfIrProgram> programs)
        {
            if (!string.IsNullOrWhiteSpace(manifest.EntrySource))
                return sources.FirstOrDefault(item => string.Equals(Normalize(item.RelativePath), Normalize(manifest.EntrySource), StringComparison.OrdinalIgnoreCase) &&
                    programs.TryGetValue(item.RelativePath, out DdfIrProgram program) && program.Members.OfType<DdfIrFunction>().Any(function => function.Name == manifest.EntryPoint));
            return sources.FirstOrDefault(item => programs.TryGetValue(item.RelativePath, out DdfIrProgram program) &&
                program.Members.OfType<DdfIrFunction>().Any(function => function.Name == manifest.EntryPoint));
        }

        private static string Fingerprint(DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyList<DdfBuildArtifactSource> sources, int maxInstructions)
        {
            var value = new StringBuilder();
            value.Append(DdfBuildArtifact.ReceiptFormat).Append('\n').Append(manifest.Name).Append('\n')
                .Append(manifest.Module.Id).Append('@').Append(manifest.Module.Version).Append('\n')
                .Append(string.Join(",", manifest.Module.Exports.OrderBy(item => item,
                    StringComparer.Ordinal))).Append('\n')
                .Append(package.Id).Append('@').Append(package.PackageVersion).Append('\n')
                .Append(manifest.Backend).Append('\n').Append(manifest.EntrySource).Append('\n')
                .Append(manifest.EntryPoint).Append('\n').Append(maxInstructions).Append('\n')
                .Append("useUnsavedBuffers:").Append(manifest.Options.UseUnsavedBuffers).Append('\n');
            foreach (DdfProjectDependency dependency in manifest.Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal))
                value.Append("dep:").Append(dependency.Id).Append('@').Append(dependency.Version).Append('\n');
            foreach (DdfBuildArtifactSource source in sources)
                value.Append("src:").Append(Normalize(source.RelativePath)).Append(':').Append(source.Sha256).Append('\n');
            return Hash(value.ToString());
        }

        internal static string Hash(string value)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
        }
        private static IEnumerable<string> EnumerateNames(DdfIrExpression expression)
        {
            if (expression == null) yield break;
            if (expression is DdfIrName name) { yield return name.Name; yield break; }
            IEnumerable<DdfIrExpression> children = expression switch
            {
                DdfIrGroup group => new[] { group.Expression },
                DdfIrUnary unary => new[] { unary.Operand },
                DdfIrBinary binary => new[] { binary.Left, binary.Right },
                DdfIrPostfix postfix => new[] { postfix.Operand },
                DdfIrCall call => new[] { call.Target }.Concat(call.Arguments),
                DdfIrIndex index => new[] { index.Target, index.Index },
                DdfIrMemberAccess member => new[] { member.Target },
                _ => Array.Empty<DdfIrExpression>()
            };
            foreach (DdfIrExpression child in children)
                foreach (string childName in EnumerateNames(child)) yield return childName;
        }
        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');
    }

    public sealed class DdfBuildService
    {
        private readonly IReadOnlyDictionary<string, IDdfBuildBackend> backends;
        private static readonly JsonSerializerOptions ReceiptOptions = new JsonSerializerOptions { WriteIndented = true };

        public DdfBuildService(IEnumerable<IDdfBuildBackend> backends = null)
        {
            var values = (backends ?? new IDdfBuildBackend[] { new DdfVmBuildBackend() }).ToList();
            this.backends = values.ToDictionary(item => item.Id, StringComparer.Ordinal);
        }

        public DdfBuildResult Validate(string workspaceRoot, DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyDictionary<string, string> unsavedBuffers = null) =>
            BuildInternal(workspaceRoot, manifest, package, unsavedBuffers, false, null);

        public DdfBuildResult Validate(string workspaceRoot, DdfProjectManifest manifest,
            DdfLanguagePackage package, IReadOnlyDictionary<string, string> unsavedBuffers,
            DdfHostCatalogSnapshot hostCatalog) =>
            BuildInternal(workspaceRoot, manifest, package, unsavedBuffers, false, hostCatalog);

        public DdfBuildResult Build(string workspaceRoot, DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyDictionary<string, string> unsavedBuffers = null) =>
            BuildInternal(workspaceRoot, manifest, package, unsavedBuffers, true, null);

        public DdfBuildResult Build(string workspaceRoot, DdfProjectManifest manifest,
            DdfLanguagePackage package, IReadOnlyDictionary<string, string> unsavedBuffers,
            DdfHostCatalogSnapshot hostCatalog) =>
            BuildInternal(workspaceRoot, manifest, package, unsavedBuffers, true, hostCatalog);

        private DdfBuildResult BuildInternal(string workspaceRoot, DdfProjectManifest manifest, DdfLanguagePackage package,
            IReadOnlyDictionary<string, string> unsavedBuffers, bool writeArtifact,
            DdfHostCatalogSnapshot hostCatalog)
        {
            string root = Path.GetFullPath(workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot)));
            var issues = DdfProjectManifestLoader.Validate(manifest, root).ToList();
            if (package == null) issues.Add(DdfBuildIssue.Configuration("DDFB002", "Pacchetto linguaggio richiesto."));
            else
            {
                if (manifest.LanguageId != package.Id || manifest.LanguagePackageVersion != package.PackageVersion)
                    issues.Add(DdfBuildIssue.Configuration("DDFB003", "Il progetto richiede " + manifest.LanguageId + " " + manifest.LanguagePackageVersion +
                        ", ma è attivo " + package.Id + " " + package.PackageVersion + ".", null, DdfDiagnosticDomain.LanguageDefinition));
                if (!package.Language.Build.Backends.Contains(manifest.Backend))
                    issues.Add(DdfBuildIssue.Configuration("DDFB004", "Backend non consentito dal pacchetto: " + manifest.Backend + ".", null,
                        DdfDiagnosticDomain.EngineCapability));
            }
            if (issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error)) return new DdfBuildResult(null, issues);
            if (!backends.TryGetValue(manifest.Backend, out IDdfBuildBackend backend))
                return new DdfBuildResult(null, new[] { DdfBuildIssue.Configuration("DDFB005", "Backend non installato: " + manifest.Backend + ".", null, DdfDiagnosticDomain.EngineCapability) });

            IReadOnlyList<DdfBuildSource> sources = ResolveSources(root, manifest, unsavedBuffers, issues);
            if (sources.Count == 0) issues.Add(DdfBuildIssue.Configuration("DDFB006", "Nessun sorgente corrisponde al manifest."));
            if (issues.Any(item => item.Severity == DdfDiagnosticSeverity.Error)) return new DdfBuildResult(null, issues);
            DdfBuildResult compiled = backend is DdfVmBuildBackend vmBackend
                ? vmBackend.Build(root, manifest, package, sources, hostCatalog)
                : backend.Build(root, manifest, package, sources);
            if (!compiled.Success || !writeArtifact) return compiled;

            string outputDirectory = Path.GetFullPath(Path.Combine(root, manifest.Output.Directory));
            EnsureInside(root, outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            string artifactPath = Path.Combine(outputDirectory, manifest.Output.Artifact);
            File.WriteAllText(artifactPath, JsonSerializer.Serialize(compiled.Artifact, ReceiptOptions) + Environment.NewLine,
                new UTF8Encoding(false));
            return new DdfBuildResult(compiled.Artifact, compiled.Issues, artifactPath);
        }

        private static IReadOnlyList<DdfBuildSource> ResolveSources(string root, DdfProjectManifest manifest,
            IReadOnlyDictionary<string, string> buffers, ICollection<DdfBuildIssue> issues)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pattern in manifest.Sources)
            {
                if (pattern.StartsWith("**/*.", StringComparison.Ordinal))
                {
                    string extension = Path.GetExtension(pattern.Substring(3));
                    foreach (string path in Directory.EnumerateFiles(root, "*" + extension, SearchOption.AllDirectories))
                        if (!IsOutputPath(root, manifest, path)) paths.Add(Path.GetFullPath(path));
                }
                else
                {
                    string path = Path.GetFullPath(Path.Combine(root, pattern));
                    EnsureInside(root, path);
                    if (File.Exists(path) || (buffers != null && buffers.ContainsKey(path))) paths.Add(path);
                    else issues.Add(DdfBuildIssue.Configuration("DDFB007", "Sorgente non trovato: " + pattern + ".", path));
                }
            }
            var result = new List<DdfBuildSource>();
            foreach (string path in paths.OrderBy(item => Relative(root, item), StringComparer.Ordinal))
            {
                string source = null;
                bool useBuffer = manifest.Options.UseUnsavedBuffers && buffers != null && buffers.TryGetValue(path, out source);
                if (!useBuffer && !File.Exists(path))
                {
                    issues.Add(DdfBuildIssue.Configuration("DDFB007", "Sorgente non trovato sul disco e uso dei buffer disabilitato.", path));
                    continue;
                }
                result.Add(new DdfBuildSource(path, Relative(root, path), useBuffer ? source : File.ReadAllText(path, Encoding.UTF8), useBuffer));
            }
            return result.AsReadOnly();
        }

        private static bool IsOutputPath(string root, DdfProjectManifest manifest, string path)
        {
            string output = Path.GetFullPath(Path.Combine(root, manifest.Output.Directory));
            string full = Path.GetFullPath(path);
            return full.StartsWith(output.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
        private static void EnsureInside(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Il percorso esce dalla cartella progetto: " + path);
        }
    }
}
