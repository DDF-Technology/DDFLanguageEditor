using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguagePackageOrigin { Directory, Archive, Embedded }
    public enum DdfLanguagePackageDiagnosticSeverity { Warning, Error }

    public sealed class DdfLanguagePackageDiagnostic
    {
        public DdfLanguagePackageDiagnostic(string code, string message, string file = null,
            string jsonPath = null, DdfLanguagePackageDiagnosticSeverity severity = DdfLanguagePackageDiagnosticSeverity.Error,
            DdfDiagnosticDomain domain = DdfDiagnosticDomain.LanguageDefinition)
        {
            Code = code; Message = message; File = file ?? string.Empty;
            JsonPath = jsonPath ?? string.Empty; Severity = severity; Domain = domain;
        }
        public string Code { get; }
        public string Message { get; }
        public string File { get; }
        public string JsonPath { get; }
        public DdfLanguagePackageDiagnosticSeverity Severity { get; }
        public DdfDiagnosticDomain Domain { get; }
        public override string ToString() => "[" + DdfDiagnosticDomainNames.GetDisplayName(Domain) + "] " +
            Code + ": " + Message +
            (string.IsNullOrEmpty(File) ? string.Empty : " [" + File +
             (string.IsNullOrEmpty(JsonPath) ? string.Empty : ":" + JsonPath) + "]");
    }

    public sealed class DdfLanguagePackage
    {
        internal DdfLanguagePackage(string id, string name, string packageVersion, string languageVersion,
            IReadOnlyList<string> extensions, DdfLanguageDefinition language, DdfCompiledGrammar grammar,
            IReadOnlyList<DdfNativeExtensionRequirement> nativeExtensions,
            DdfLanguagePackageOrigin origin, string source)
        {
            Id = id; Name = name; PackageVersion = packageVersion; LanguageVersion = languageVersion;
            FileExtensions = extensions; Language = language; Grammar = grammar;
            NativeExtensions = nativeExtensions ?? Array.Empty<DdfNativeExtensionRequirement>();
            Origin = origin; Source = source;
        }
        public string Id { get; }
        public string Name { get; }
        public string PackageVersion { get; }
        public string LanguageVersion { get; }
        public IReadOnlyList<string> FileExtensions { get; }
        public DdfLanguageDefinition Language { get; }
        public DdfCompiledGrammar Grammar { get; }
        public DdfLanguagePackageTestSuite Tests { get; internal set; }
        public IReadOnlyList<DdfNativeExtensionRequirement> NativeExtensions { get; }
        public DdfLanguagePackageOrigin Origin { get; }
        public string Source { get; }
    }

    public sealed class DdfLanguagePackageLoadResult
    {
        internal DdfLanguagePackageLoadResult(DdfLanguagePackage package,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            Package = package;
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>()).ToList().AsReadOnly();
        }
        public DdfLanguagePackage Package { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => Package != null;
        public bool HasErrors => Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public static class DdfLanguagePackageLoader
    {
        public const string SupportedSchemaVersion = "1.0";
        public const string SupportedTypesSchemaVersion = "1.1";
        public const string SupportedStandardLibrarySchemaVersion = "1.1";
        public const string SupportedEngineApi = "1.0";
        private static readonly string[] RequiredSections =
        {
            "lexicon", "grammar", "types", "semantics", "formatting",
            "intellisense", "runtime", "build", "standardLibrary", "tests"
        };
        private static readonly IReadOnlyDictionary<string, string> SupportedAdapters =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lexicon"] = "ddf-lexicon-v1", ["grammar"] = "ddf-grammar-v2",
                ["types"] = "ddf-types-v1", ["semantics"] = "ddf-semantics-v1",
                ["formatting"] = "ddf-formatter-v1", ["intellisense"] = "ddf-intellisense-v1",
                ["runtime"] = "ddf-runtime-v2", ["build"] = "ddf-build-v2",
                ["standardLibrary"] = "ddf-standard-library-v3", ["tests"] = "ddf-tests-v1"
            };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static DdfLanguagePackageLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Failure("DDFL001", "Percorso del pacchetto non specificato.");
            try
            {
                if (Directory.Exists(path))
                    return load(new DirectoryReader(path), DdfLanguagePackageOrigin.Directory,
                        Path.GetFullPath(path), true);
                if (File.Exists(path))
                    return load(new ArchiveReader(path), DdfLanguagePackageOrigin.Archive,
                        Path.GetFullPath(path), true);
                return Failure("DDFL002", "Pacchetto non trovato.", path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is InvalidDataException || exception is JsonException ||
                                               exception is ArgumentException || exception is NotSupportedException)
            {
                return Failure("DDFL003", exception.Message, path);
            }
        }

        public static DdfLanguagePackageLoadResult LoadEmbeddedDefault()
        {
            try { return load(new EmbeddedReader(), DdfLanguagePackageOrigin.Embedded,
                "embedded://ddf", true); }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is JsonException)
            { return Failure("DDFL004", "Fallback DDF incorporato non valido: " + exception.Message, "embedded://ddf"); }
        }

        public static DdfLanguagePackageLoadResult LoadWithEmbeddedFallback(string path)
        {
            DdfLanguagePackageLoadResult external = Load(path);
            if (external.Success) return external;
            DdfLanguagePackageLoadResult embedded = LoadEmbeddedDefault();
            var diagnostics = external.Diagnostics.ToList();
            if (embedded.Success)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL005",
                    "Pacchetto esterno rifiutato; è attivo il fallback DDF incorporato.", path, null,
                    DdfLanguagePackageDiagnosticSeverity.Warning));
                return new DdfLanguagePackageLoadResult(embedded.Package, diagnostics);
            }
            diagnostics.AddRange(embedded.Diagnostics);
            return new DdfLanguagePackageLoadResult(null, diagnostics);
        }

        public static void ExportEmbeddedDefault(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory richiesta.", nameof(directory));
            Directory.CreateDirectory(directory);
            using (var reader = new EmbeddedReader())
            {
                foreach (string file in reader.Files)
                    File.WriteAllText(Path.Combine(directory, file), reader.Read(file), new UTF8Encoding(false));
            }
        }

        internal static DdfLanguagePackageLoadResult LoadAuthoringSnapshot(
            string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return Failure("DDFL001", "Percorso del pacchetto non specificato.");
            try
            {
                return load(new DirectoryReader(directory),
                    DdfLanguagePackageOrigin.Directory,
                    Path.GetFullPath(directory), false);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException || exception is JsonException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return Failure("DDFL003", exception.Message, directory);
            }
        }

        private static DdfLanguagePackageLoadResult load(IPackageReader reader,
            DdfLanguagePackageOrigin origin, string source,
            bool requirePassingTests)
        {
            using (reader)
            {
                var diagnostics = new List<DdfLanguagePackageDiagnostic>();
                ManifestDto manifest = read<ManifestDto>(reader, "manifest.json", diagnostics);
                if (manifest == null) return new DdfLanguagePackageLoadResult(null, diagnostics);
                validateManifest(manifest, diagnostics);
                if (hasErrors(diagnostics)) return new DdfLanguagePackageLoadResult(null, diagnostics);

                var sectionDocuments = new Dictionary<string, SectionDto>(StringComparer.Ordinal);
                foreach (string section in RequiredSections)
                {
                    string file = manifest.Sections[section];
                    SectionDto document = section == "lexicon"
                        ? read<LexiconDto>(reader, file, diagnostics)
                        : section == "grammar"
                            ? read<GrammarDto>(reader, file, diagnostics)
                            : section == "types"
                                ? read<TypesDto>(reader, file, diagnostics)
                            : section == "semantics"
                                ? read<SemanticsDto>(reader, file, diagnostics)
                            : section == "intellisense"
                                ? read<IntellisenseDto>(reader, file, diagnostics)
                                : section == "formatting"
                                    ? read<FormattingDto>(reader, file, diagnostics)
                                    : section == "runtime"
                                        ? read<RuntimeDto>(reader, file, diagnostics)
                                        : section == "build"
                                            ? read<BuildDto>(reader, file, diagnostics)
                                        : section == "standardLibrary"
                                            ? read<StandardLibraryDto>(reader, file, diagnostics)
                                        : section == "tests"
                                            ? read<TestsDto>(reader, file, diagnostics)
                                    : read<SectionDto>(reader, file, diagnostics);
                    if (document != null)
                    {
                        sectionDocuments[section] = document;
                        validateSection(section, file, document, manifest.Sections, diagnostics);
                    }
                }
                validateSectionGraph(sectionDocuments, manifest.Sections, diagnostics);
                if (hasErrors(diagnostics)) return new DdfLanguagePackageLoadResult(null, diagnostics);

                DdfLanguageDefinition language = createLanguage(
                    (LexiconDto)sectionDocuments["lexicon"], manifest.Sections["lexicon"], diagnostics);
                if (hasErrors(diagnostics) || language == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);

                DdfCompiledGrammar grammar = createGrammar(reader,
                    (GrammarDto)sectionDocuments["grammar"], manifest.Sections["grammar"], diagnostics);
                if (hasErrors(diagnostics) || grammar == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Grammar = grammar;
                language.Types = createTypes((TypesDto)sectionDocuments["types"],
                    manifest.Sections["types"], diagnostics);
                if (hasErrors(diagnostics) || language.Types == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Semantics = createSemantics((SemanticsDto)sectionDocuments["semantics"], grammar,
                    manifest.Sections["semantics"], diagnostics);
                if (hasErrors(diagnostics) || language.Semantics == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Formatting = createFormatting((FormattingDto)sectionDocuments["formatting"], language,
                    manifest.Sections["formatting"], diagnostics);
                if (hasErrors(diagnostics) || language.Formatting == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Intellisense = createIntellisense(
                    (IntellisenseDto)sectionDocuments["intellisense"],
                    manifest.Sections["intellisense"], diagnostics);
                if (hasErrors(diagnostics) || language.Intellisense == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Runtime = createRuntime((RuntimeDto)sectionDocuments["runtime"], language,
                    manifest.Sections["runtime"], diagnostics);
                if (hasErrors(diagnostics) || language.Runtime == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.StandardLibrary = createStandardLibrary(
                    (StandardLibraryDto)sectionDocuments["standardLibrary"], language.Types,
                    manifest.Sections["standardLibrary"], diagnostics);
                if (hasErrors(diagnostics) || language.StandardLibrary == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Build = createBuild((BuildDto)sectionDocuments["build"],
                    manifest.Sections["build"], diagnostics);
                if (hasErrors(diagnostics) || language.Build == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);

                var package = new DdfLanguagePackage(manifest.Id, manifest.Name,
                    manifest.PackageVersion, manifest.LanguageVersion,
                    manifest.FileExtensions.AsReadOnly(), language, grammar,
                    manifest.NativeExtensions.Select(item => new DdfNativeExtensionRequirement(
                        item.Id, item.Publisher, item.Version, item.Sha256,
                    item.Capabilities)).ToList().AsReadOnly(), origin, source);
                TestsDto tests = (TestsDto)sectionDocuments["tests"];
                var suite = new DdfLanguagePackageTestSuite
                {
                    SchemaVersion = tests.SchemaVersion,
                    Adapter = tests.Adapter,
                    DependsOn = tests.DependsOn,
                    Description = tests.Description,
                    Cases = (tests.Cases ?? new List<PackageTestCaseDto>()).Select(item =>
                        new DdfLanguagePackageTestCase
                        {
                            Name = item?.Name,
                            Source = item?.Source,
                            ExpectedTokenKinds = item?.ExpectedTokenKinds,
                            ExpectedDiagnosticCodes = item?.ExpectedDiagnosticCodes,
                            ExpectedFormatted = item?.ExpectedFormatted,
                            ExpectedReturn = item?.ExpectedReturn
                        }).ToList()
                };
                package.Tests = suite;
                foreach (DdfLanguagePackageTestResult test in DdfLanguagePackageTestRunner.Run(package, suite).Where(item => !item.Passed))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFT001",
                        "Test dichiarativo '" + test.Name + "' non riuscito: " + test.Message,
                        manifest.Sections["tests"], "$.cases"));
                if (requirePassingTests && hasErrors(diagnostics))
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                return new DdfLanguagePackageLoadResult(package, diagnostics);
            }
        }

        private static T read<T>(IPackageReader reader, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics) where T : class
        {
            if (!reader.Exists(file))
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL010", "File richiesto mancante.", file));
                return null;
            }
            try
            {
                T result = JsonSerializer.Deserialize<T>(reader.Read(file), JsonOptions);
                if (result == null) throw new JsonException("Documento JSON vuoto.");
                return result;
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL011", exception.Message, file));
                return null;
            }
        }

        private static void validateManifest(ManifestDto manifest, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            require(manifest.SchemaVersion == SupportedSchemaVersion, diagnostics, "DDFL020",
                "schemaVersion non supportata: " + manifest.SchemaVersion, "manifest.json", "$.schemaVersion",
                DdfDiagnosticDomain.EngineCapability);
            require(manifest.EngineApi == SupportedEngineApi, diagnostics, "DDFL021",
                "engineApi non compatibile: " + manifest.EngineApi, "manifest.json", "$.engineApi",
                DdfDiagnosticDomain.EngineCapability);
            require(!string.IsNullOrWhiteSpace(manifest.Id) && Regex.IsMatch(manifest.Id, "^[a-z][a-z0-9.-]*$"),
                diagnostics, "DDFL022", "Identificatore pacchetto non valido.", "manifest.json", "$.id");
            require(!string.IsNullOrWhiteSpace(manifest.Name), diagnostics, "DDFL023",
                "Nome pacchetto richiesto.", "manifest.json", "$.name");
            require(Version.TryParse(manifest.PackageVersion, out _), diagnostics, "DDFL024",
                "Versione pacchetto non valida.", "manifest.json", "$.packageVersion");
            require(Version.TryParse(manifest.LanguageVersion, out _), diagnostics, "DDFL025",
                "Versione linguaggio non valida.", "manifest.json", "$.languageVersion");
            manifest.FileExtensions = manifest.FileExtensions ?? new List<string>();
            require(manifest.FileExtensions.Count > 0 && unique(manifest.FileExtensions) &&
                    manifest.FileExtensions.All(item => Regex.IsMatch(item ?? string.Empty, "^\\.[A-Za-z0-9]+$")),
                diagnostics, "DDFL026", "Estensioni file mancanti, duplicate o non valide.", "manifest.json", "$.fileExtensions");
            manifest.Sections = manifest.Sections ?? new Dictionary<string, string>();
            foreach (string section in manifest.Sections.Keys.Except(RequiredSections, StringComparer.Ordinal))
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL029",
                    "Sezione manifest sconosciuta: " + section + ".", "manifest.json", "$.sections." + section));
            }
            foreach (string section in RequiredSections)
            {
                require(manifest.Sections.TryGetValue(section, out string file) && safeFile(file), diagnostics,
                    "DDFL027", "Riferimento sezione mancante o non sicuro: " + section + ".",
                    "manifest.json", "$.sections." + section);
            }
            require(manifest.Sections.Values.Where(value => value != null).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    manifest.Sections.Values.Count, diagnostics, "DDFL028", "Due sezioni riferiscono lo stesso file.",
                "manifest.json", "$.sections");
            manifest.NativeExtensions = manifest.NativeExtensions ?? new List<NativeExtensionDto>();
            validateNativeExtensions(manifest.NativeExtensions, diagnostics);
        }

        private static void validateNativeExtensions(IReadOnlyList<NativeExtensionDto> extensions,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < extensions.Count; index++)
            {
                NativeExtensionDto extension = extensions[index] ?? new NativeExtensionDto();
                string path = "$.nativeExtensions[" + index + "]";
                require(!string.IsNullOrWhiteSpace(extension.Id) &&
                        Regex.IsMatch(extension.Id, "^[a-z][a-z0-9.-]*$") && ids.Add(extension.Id),
                    diagnostics, "DDFL110", "Identificatore plug-in nativo mancante, non valido o duplicato.",
                    "manifest.json", path + ".id");
                require(!string.IsNullOrWhiteSpace(extension.Publisher) &&
                        Regex.IsMatch(extension.Publisher, "^[a-z][a-z0-9.-]*$"),
                    diagnostics, "DDFL111", "Identificatore dell'autore del plug-in non valido.",
                    "manifest.json", path + ".publisher");
                require(Version.TryParse(extension.Version, out _), diagnostics, "DDFL112",
                    "Versione del plug-in nativo non valida.", "manifest.json", path + ".version");
                require(!string.IsNullOrWhiteSpace(extension.Sha256) &&
                        Regex.IsMatch(extension.Sha256, "^[A-Fa-f0-9]{64}$"),
                    diagnostics, "DDFL113", "Impronta SHA-256 del plug-in nativo non valida.",
                    "manifest.json", path + ".sha256");
                extension.Capabilities = extension.Capabilities ?? new List<string>();
                require(extension.Capabilities.Count > 0 && uniqueExact(extension.Capabilities) &&
                        extension.Capabilities.All(item => !string.IsNullOrWhiteSpace(item) &&
                            Regex.IsMatch(item, "^[a-z][a-z0-9.-]*(?::[a-z][a-z0-9.-]*)*$")),
                    diagnostics, "DDFL114", "Capacità del plug-in mancanti, duplicate o non valide.",
                    "manifest.json", path + ".capabilities");
            }
        }

        private static void validateSection(string name, string file, SectionDto section,
            IReadOnlyDictionary<string, string> sectionFiles, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            string supportedSchemaVersion = name == "types"
                ? SupportedTypesSchemaVersion
                : name == "standardLibrary"
                    ? SupportedStandardLibrarySchemaVersion
                    : SupportedSchemaVersion;
            require(section.SchemaVersion == supportedSchemaVersion, diagnostics, "DDFL030",
                "Versione sezione non supportata.", file, "$.schemaVersion", DdfDiagnosticDomain.EngineCapability);
            require(SupportedAdapters[name] == section.Adapter, diagnostics, "DDFL031",
                "Adapter non supportato per " + name + ": " + section.Adapter, file, "$.adapter",
                DdfDiagnosticDomain.EngineCapability);
            foreach (string dependency in section.DependsOn ?? new List<string>())
                require(sectionFiles.ContainsKey(dependency), diagnostics, "DDFL032",
                    "Dipendenza di sezione sconosciuta: " + dependency, file, "$.dependsOn");
        }

        private static void validateSectionGraph(
            IReadOnlyDictionary<string, SectionDto> documents,
            IReadOnlyDictionary<string, string> files,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string section in documents.Keys)
            {
                var dependencies = documents[section].DependsOn ?? new List<string>();
                if (dependencies.Count != dependencies.Distinct(StringComparer.Ordinal).Count())
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL033",
                        "La lista delle dipendenze contiene duplicati.",
                        files[section], "$.dependsOn"));
                if (dependencies.Contains(section, StringComparer.Ordinal))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL034",
                        "Una sezione non può dipendere da se stessa: " + section + ".",
                        files[section], "$.dependsOn"));
            }
            foreach (string section in documents.Keys)
                Visit(section);

            void Visit(string section)
            {
                if (states.TryGetValue(section, out int state) && state == 2) return;
                states[section] = 1;
                foreach (string dependency in documents[section].DependsOn ?? new List<string>())
                {
                    if (dependency == section || !documents.ContainsKey(dependency)) continue;
                    if (states.TryGetValue(dependency, out state) && state == 1)
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL035",
                            "Dipendenza circolare tra sezioni: " + section + " e " + dependency + ".",
                            files[section], "$.dependsOn"));
                        continue;
                    }
                    if (state != 2) Visit(dependency);
                }
                states[section] = 2;
            }
        }

        private static DdfLanguageDefinition createLanguage(LexiconDto lexicon, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            lexicon.Keywords = lexicon.Keywords ?? new List<KeywordDto>();
            lexicon.BooleanLiterals = lexicon.BooleanLiterals ?? new List<string>();
            lexicon.BooleanLiteralValues = lexicon.BooleanLiteralValues ?? new Dictionary<string, bool>();
            lexicon.Operators = lexicon.Operators ?? new List<OperatorDto>();
            lexicon.Punctuation = lexicon.Punctuation ?? new List<string>();
            validateUnique(lexicon.Keywords.Select(item => item?.Text), "keyword", file, "$.keywords", diagnostics);
            validateUnique(lexicon.BooleanLiterals, "literal", file, "$.booleanLiterals", diagnostics);
            foreach (string literal in lexicon.BooleanLiterals)
                require(lexicon.BooleanLiteralValues.ContainsKey(literal) ||
                        string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase),
                    diagnostics, "DDFL047", "Il valore del letterale booleano non è dichiarato: " + literal + ".",
                    file, "$.booleanLiteralValues");
            foreach (string literal in lexicon.BooleanLiteralValues.Keys)
                require(lexicon.BooleanLiterals.Contains(literal, StringComparer.Ordinal), diagnostics, "DDFL048",
                    "Valore dichiarato per un letterale booleano sconosciuto: " + literal + ".",
                    file, "$.booleanLiteralValues");
            validateUnique(lexicon.Operators.Select(item => item?.Text), "operatore", file, "$.operators", diagnostics);
            validateUnique(lexicon.Punctuation, "punteggiatura", file, "$.punctuation", diagnostics);
            validateUniqueRoles(lexicon.Keywords.Where(item => item != null).Select(item => item.Role),
                "ruolo keyword", file, "$.keywords", diagnostics);
            validateUniqueRoles(lexicon.Operators.Where(item => item != null).Select(item => item.Role),
                "ruolo operatore", file, "$.operators", diagnostics);
            DdfLexicalDefinition lexicalRules = createLexicalRules(lexicon, file, diagnostics);
            var keywords = new List<DdfKeywordDefinition>();
            var operators = new List<DdfOperatorDefinition>();
            foreach (KeywordDto item in lexicon.Keywords)
            {
                if (item == null || !Enum.TryParse(item.TokenKind, true, out DdfTokenKind tokenKind) ||
                    !Enum.IsDefined(tokenKind) || !validRole(item.Role ?? "None"))
                { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL041", "Keyword con categoria o ruolo non valido.", file, "$.keywords")); continue; }
                try { keywords.Add(new DdfKeywordDefinition(item.Text, tokenKind, item.Role ?? "None")); }
                catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL042", exception.Message, file, "$.keywords")); }
            }
            foreach (OperatorDto item in lexicon.Operators)
            {
                if (item == null || !Enum.TryParse(item.Associativity ?? "Left", true, out DdfOperatorAssociativity associativity) ||
                    !Enum.IsDefined(associativity) || !Enum.TryParse(item.Role ?? "None", true, out DdfOperatorRole role) || !Enum.IsDefined(role))
                { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL043", "Operatore con associatività o ruolo non valido.", file, "$.operators")); continue; }
                try { operators.Add(new DdfOperatorDefinition(item.Text, item.BinaryPrecedence, associativity,
                    item.PrefixPrecedence, item.PostfixPrecedence, role)); }
                catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL044", exception.Message, file, "$.operators")); }
            }
            char[] punctuation = lexicon.Punctuation.Where(item => item?.Length == 1).Select(item => item[0]).ToArray();
            if (punctuation.Length != lexicon.Punctuation.Count)
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL045", "Ogni punteggiatura deve contenere un solo carattere.", file, "$.punctuation"));
            if (hasErrors(diagnostics)) return null;
            try { return new DdfLanguageDefinition(keywords, lexicon.BooleanLiterals, operators, punctuation,
                lexicalRules, lexicon.BooleanLiteralValues); }
            catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL046", exception.Message, file)); return null; }
        }

        private static DdfCompiledGrammar createGrammar(IPackageReader reader, GrammarDto grammar, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (grammar == null) return null;
            require(grammar.Notation == "ebnf-1.0", diagnostics, "DDFL060",
                "Notazione grammaticale non supportata: " + grammar.Notation + ".", file, "$.notation",
                DdfDiagnosticDomain.EngineCapability);
            require(grammar.ChoiceStrategy == "longest", diagnostics, "DDFL061",
                "Strategia delle alternative non supportata: " + grammar.ChoiceStrategy + ".", file, "$.choiceStrategy",
                DdfDiagnosticDomain.EngineCapability);
            require(safeResourceFile(grammar.EbnfFile) && reader.Exists(grammar.EbnfFile), diagnostics, "DDFL062",
                "File EBNF mancante o non sicuro.", file, "$.ebnfFile");
            require(!string.IsNullOrWhiteSpace(grammar.StartRule), diagnostics, "DDFL063",
                "Regola iniziale richiesta.", file, "$.startRule");
            if (hasErrors(diagnostics)) return null;
            try
            {
                var nodes = (grammar.Nodes ?? new List<GrammarNodeDto>()).Select(node =>
                    new DdfGrammarNodeDefinition(node.Rule, node.Kind,
                        (node.Fields ?? new List<GrammarFieldDto>()).Select(field =>
                            new DdfGrammarFieldDefinition(field.Name, field.SourceRule, field.Multiple)),
                        createIdeDefinition(node.Ide)));
                return DdfEbnfCompiler.Compile(reader.Read(grammar.EbnfFile), grammar.StartRule, nodes);
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL064", exception.Message,
                    grammar.EbnfFile, "$"));
                return null;
            }
        }

        private static DdfGrammarIdeDefinition createIdeDefinition(GrammarIdeDto ide)
        {
            if (ide == null) return null;
            return new DdfGrammarIdeDefinition(
                parseOptionalEnum<DdfSymbolKind>(ide.SymbolKind, "symbolKind"),
                ide.NameField, ide.NamePrefix, ide.NameSuffix, ide.Detail,
                parseOptionalEnum<DdfFoldingKind>(ide.FoldingKind, "foldingKind"), ide.FoldingField,
                parseOptionalEnum<DdfBreadcrumbKind>(ide.BreadcrumbKind, "breadcrumbKind"),
                ide.BreadcrumbLabel, ide.BreadcrumbSelectionField, ide.BreadcrumbSelectionLength,
                ide.Selectable, ide.TypeField, ide.ReturnTypeField, ide.ParametersField,
                parseOptionalEnum<DdfSymbolKind>(ide.ChildSymbolKind, "childSymbolKind"));
        }

        private static DdfIntellisenseDefinition createIntellisense(IntellisenseDto value, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.Completion != null, diagnostics, "DDFL070",
                    "Configurazione completion richiesta.", file, "$.completion");
                require(value.SignatureHelp != null, diagnostics, "DDFL071",
                    "Configurazione Signature Help richiesta.", file, "$.signatureHelp");
                require(value.SymbolKinds != null && value.SymbolKinds.Count > 0, diagnostics, "DDFL072",
                    "Presentazioni dei simboli richieste.", file, "$.symbolKinds");
                require(value.Snippets != null, diagnostics, "DDFL073",
                    "Catalogo snippet richiesto.", file, "$.snippets");
                if (hasErrors(diagnostics)) return null;

                CompletionDto completion = value.Completion;
                SignatureHelpDto signature = value.SignatureHelp;
                require(new[] { completion.KeywordOrigin, completion.BooleanOrigin, completion.SnippetOrigin,
                    completion.StandardLibraryOrigin, completion.CurrentDocumentOrigin, completion.WorkspaceOrigin,
                    completion.TypeDetail, completion.ControlFlowDetail, completion.KeywordDetail,
                    completion.BooleanDetail, completion.StandardFunctionDetailPrefix }.All(item => item != null),
                    diagnostics, "DDFL075", "Configurazione completion incompleta.", file, "$.completion");
                require(new[] { signature.CurrentDocumentOrigin, signature.WorkspaceOrigin,
                    signature.StandardLibraryOrigin }.All(item => item != null), diagnostics, "DDFL076",
                    "Configurazione Signature Help incompleta.", file, "$.signatureHelp");
                if (hasErrors(diagnostics)) return null;

                var presentations = value.SymbolKinds.Select(item => new DdfSymbolPresentation(
                    parseRequiredEnum<DdfSymbolKind>(item.Kind, "symbol kind"), item.Label, item.Documentation));
                var snippets = value.Snippets.Select(item => new DdfSnippetTemplate(
                    item.Prefix, item.DisplayText, item.Description, item.Body, item.RequiresKeyword));
                DdfFlowchartPresentation flowchart = value.Flowchart == null
                    ? DdfFlowchartPresentation.CreateLegacyDefault()
                    : new DdfFlowchartPresentation(value.Flowchart.TrueLabel, value.Flowchart.FalseLabel,
                        value.Flowchart.ExitLabelFormat, value.Flowchart.GuideTitle,
                        value.Flowchart.GuidePrompt, value.Flowchart.DebugTitle,
                        value.Flowchart.SelectedTitle, value.Flowchart.DebugPrefix,
                        value.Flowchart.ValuesPrefix, (value.Flowchart.Explanations ??
                            new Dictionary<string, string>()).Select(item =>
                                new KeyValuePair<DdfFlowchartNodeKind, string>(
                                    parseRequiredEnum<DdfFlowchartNodeKind>(item.Key, "flowchart node kind"),
                                    item.Value)));
                var diagnosticPresentation = new DdfDiagnosticPresentation(value.Diagnostics);
                return new DdfIntellisenseDefinition(snippets, presentations,
                    completion.KeywordOrigin, completion.BooleanOrigin, completion.SnippetOrigin,
                    completion.StandardLibraryOrigin, completion.CurrentDocumentOrigin, completion.WorkspaceOrigin,
                    completion.TypeDetail, completion.ControlFlowDetail, completion.KeywordDetail,
                    completion.BooleanDetail, completion.StandardFunctionDetailPrefix,
                    signature.CurrentDocumentOrigin, signature.WorkspaceOrigin, signature.StandardLibraryOrigin,
                    flowchart, diagnosticPresentation);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL074", exception.Message, file, "$"));
                return null;
            }
        }

        private static DdfTypeSystemDefinition createTypes(TypesDto value, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.Primitives != null && value.Primitives.Count > 0, diagnostics, "DDFL090",
                    "Almeno un tipo primitivo è richiesto.", file, "$.primitives");
                require(value.Conversions != null, diagnostics, "DDFL091",
                    "Elenco conversioni richiesto.", file, "$.conversions");
                require(value.Compatibility != null && value.Mutability != null && value.Scopes != null &&
                        value.Signatures != null && value.Literals != null && value.Numeric != null &&
                        value.Indexing != null && value.ClrMapping != null &&
                        value.ClrMapping.Primitives != null,
                    diagnostics, "DDFL092", "Configurazione del sistema di tipi incompleta.", file, "$");
                if (hasErrors(diagnostics)) return null;

                validateUnique(value.Primitives.Select(item => item?.Name), "tipo primitivo", file,
                    "$.primitives", diagnostics);
                validateUnique(value.Conversions.Select(item => item == null ? null :
                    item.From + "->" + item.To + "@" + item.Context), "conversione", file,
                    "$.conversions", diagnostics);
                validateUnique(value.ClrMapping.Primitives.Select(item => item?.Type),
                    "mapping primitivo CLR", file, "$.clrMapping.primitives", diagnostics);
                require(value.Mutability.AssignmentTargets != null && value.Mutability.AssignmentTargets.Count > 0,
                    diagnostics, "DDFL093", "Destinazioni di assegnazione richieste.", file,
                    "$.mutability.assignmentTargets");
                require(value.Mutability.AssignmentTargets != null &&
                        value.Mutability.AssignmentTargets.All(item => item == "Name" || item == "Index" || item == "Member") &&
                        value.Mutability.AssignmentTargets.Distinct(StringComparer.Ordinal).Count() == value.Mutability.AssignmentTargets.Count,
                    diagnostics, "DDFL093", "Destinazioni di assegnazione non supportate o duplicate.", file,
                    "$.mutability.assignmentTargets");
                require(allFlagsPresent(value), diagnostics, "DDFL094",
                    "I criteri booleani di compatibilità, mutabilità, scope e firme sono obbligatori.", file, "$");
                if (hasErrors(diagnostics)) return null;

                var primitives = value.Primitives.Select(item => new DdfPrimitiveTypeDefinition(
                    item.Name, parseRequiredEnum<DdfPrimitiveTypeCategory>(item.Category, "primitive category"),
                    item.CanDeclareVariable.Value, item.IndexedElementType));
                var conversions = value.Conversions.Select(item => new DdfTypeConversionDefinition(
                    item.From, item.To, parseRequiredEnum<DdfConversionContext>(item.Context, "conversion context")));
                var clrMapping = new DdfClrTypeMappingDefinition(value.ClrMapping.Backend,
                    value.ClrMapping.MappingVersion, value.ClrMapping.ArrayRepresentation,
                    value.ClrMapping.StructureRepresentation,
                    value.ClrMapping.Primitives.Select(item => new DdfClrPrimitiveTypeMapping(
                        item.Type, item.ClrType, item.CsharpType)));
                return new DdfTypeSystemDefinition(
                    primitives,
                    conversions,
                    new DdfTypeCompatibilityDefinition(value.Compatibility.ArraysInvariant.Value,
                        value.Compatibility.UserTypesNominal.Value, value.Compatibility.ErrorTypeCompatible.Value),
                    new DdfMutabilityDefinition(value.Mutability.VariablesMutable.Value,
                        value.Mutability.ParametersMutable.Value, value.Mutability.FieldsMutable.Value,
                        value.Mutability.AssignmentTargets),
                    new DdfScopeDefinition(value.Scopes.FunctionParameters.Value, value.Scopes.Blocks.Value,
                        value.Scopes.ForInitializers.Value, value.Scopes.StructureFields.Value,
                        value.Scopes.AllowShadowing.Value),
                    new DdfSignatureDefinition(value.Signatures.ExactArity.Value,
                        value.Signatures.RequireReturnForNonVoid.Value, value.Signatures.VoidType,
                        value.Signatures.ParameterOrder),
                    new DdfLiteralTypeDefinition(value.Literals.Integer, value.Literals.Decimal,
                        value.Literals.Boolean, value.Literals.String),
                    value.Numeric.ResultType, value.Numeric.DominantType, value.Indexing.IndexType,
                    clrMapping);
            }
            catch (DdfEngineCapabilityException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL096", exception.Message, file, "$",
                    DdfLanguagePackageDiagnosticSeverity.Error, DdfDiagnosticDomain.EngineCapability));
                return null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL095", exception.Message, file, "$"));
                return null;
            }
        }

        private static bool allFlagsPresent(TypesDto value)
        {
            return value.Primitives.All(item => item?.CanDeclareVariable.HasValue == true) &&
                   value.Compatibility.ArraysInvariant.HasValue && value.Compatibility.UserTypesNominal.HasValue &&
                   value.Compatibility.ErrorTypeCompatible.HasValue && value.Mutability.VariablesMutable.HasValue &&
                   value.Mutability.ParametersMutable.HasValue && value.Mutability.FieldsMutable.HasValue &&
                   value.Scopes.FunctionParameters.HasValue && value.Scopes.Blocks.HasValue &&
                   value.Scopes.ForInitializers.HasValue && value.Scopes.StructureFields.HasValue &&
                   value.Scopes.AllowShadowing.HasValue && value.Signatures.ExactArity.HasValue &&
                   value.Signatures.RequireReturnForNonVoid.HasValue;
        }

        private static DdfSemanticDefinition createSemantics(SemanticsDto value, DdfCompiledGrammar grammar,
            string file, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            require(!string.IsNullOrWhiteSpace(value.OperationSet) &&
                    !string.IsNullOrWhiteSpace(value.OperationSetVersion), diagnostics, "DDFL100",
                "Insieme e versione delle operazioni semantiche sono richiesti.", file, "$");
            require(value.Bindings != null, diagnostics, "DDFL101",
                "Elenco dei collegamenti semantici richiesto.", file, "$.bindings");
            if (hasErrors(diagnostics)) return null;

            validateUnique(value.Bindings.Select(item => item?.Rule), "regola semantica", file,
                "$.bindings", diagnostics);
            for (int bindingIndex = 0; bindingIndex < value.Bindings.Count; bindingIndex++)
            {
                SemanticBindingDto binding = value.Bindings[bindingIndex];
                string bindingPath = "$.bindings[" + bindingIndex + "]";
                if (binding == null) continue;
                if (!grammar.TryGetNodeDefinition(binding.Rule, out DdfGrammarNodeDefinition node))
                {
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL102",
                        "La regola semantica non produce un nodo dichiarato: " + binding.Rule + ".",
                        file, bindingPath + ".rule"));
                    continue;
                }
                require(binding.Operations != null && binding.Operations.Count > 0, diagnostics, "DDFL103",
                    "Almeno un'operazione semantica è richiesta.", file, bindingPath + ".operations");
                if (binding.Operations == null) continue;
                for (int operationIndex = 0; operationIndex < binding.Operations.Count; operationIndex++)
                {
                    SemanticOperationDto operation = binding.Operations[operationIndex];
                    string operationPath = bindingPath + ".operations[" + operationIndex + "]";
                    require(operation != null && !string.IsNullOrWhiteSpace(operation.Id) &&
                            operation.Arguments != null, diagnostics, "DDFL104",
                        "Identificatore e argomenti dell'operazione semantica sono richiesti.", file,
                        operationPath);
                    if (operation?.Arguments == null) continue;
                    foreach (KeyValuePair<string, string> argument in operation.Arguments)
                        require(node.Fields.Any(field => field.Name == argument.Value), diagnostics, "DDFL104",
                            "L'argomento '" + argument.Key + "' riferisce il campo grammaticale sconosciuto '" +
                            argument.Value + "'.", file, operationPath + ".arguments." + argument.Key);
                }
            }
            if (hasErrors(diagnostics)) return null;

            try
            {
                return new DdfSemanticDefinition(value.OperationSet, value.OperationSetVersion,
                    value.Bindings.Select(binding => new DdfSemanticBindingDefinition(binding.Rule,
                        binding.Operations.Select(operation => new DdfSemanticOperationDefinition(
                            operation.Id, operation.Arguments)))));
            }
            catch (DdfEngineCapabilityException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL106", exception.Message, file, "$",
                    DdfLanguagePackageDiagnosticSeverity.Error, DdfDiagnosticDomain.EngineCapability));
                return null;
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL105", exception.Message, file, "$"));
                return null;
            }
        }

        private static DdfRuntimeDefinition createRuntime(RuntimeDto value, DdfLanguageDefinition language,
            string file, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.ExpressionOperations != null && value.VmOperations != null,
                    diagnostics, "DDFL120", "Configurazione runtime incompleta.", file, "$.");
                if (hasErrors(diagnostics)) return null;
                value.ExpressionOperations.Unary = value.ExpressionOperations.Unary ?? new List<RuntimeOperationDto>();
                value.ExpressionOperations.Binary = value.ExpressionOperations.Binary ?? new List<RuntimeOperationDto>();
                value.ExpressionOperations.Postfix = value.ExpressionOperations.Postfix ?? new List<RuntimeOperationDto>();
                validateRuntimeBindings(value.ExpressionOperations.Unary, language, "unary", file,
                    "$.expressionOperations.unary", diagnostics);
                validateRuntimeBindings(value.ExpressionOperations.Binary, language, "binary", file,
                    "$.expressionOperations.binary", diagnostics);
                validateRuntimeBindings(value.ExpressionOperations.Postfix, language, "postfix", file,
                    "$.expressionOperations.postfix", diagnostics);
                foreach (DdfOperatorDefinition op in language.Operators)
                {
                    if (op.IsPrefix) require(value.ExpressionOperations.Unary.Any(item => item?.Token == op.Text),
                        diagnostics, "DDFL124", "Binding runtime unary mancante per '" + op.Text + "'.", file,
                        "$.expressionOperations.unary");
                    if (op.BinaryPrecedence > 0) require(value.ExpressionOperations.Binary.Any(item => item?.Token == op.Text),
                        diagnostics, "DDFL124", "Binding runtime binary mancante per '" + op.Text + "'.", file,
                        "$.expressionOperations.binary");
                    if (op.IsPostfix) require(value.ExpressionOperations.Postfix.Any(item => item?.Token == op.Text),
                        diagnostics, "DDFL124", "Binding runtime postfix mancante per '" + op.Text + "'.", file,
                        "$.expressionOperations.postfix");
                }
                if (hasErrors(diagnostics)) return null;
                return new DdfRuntimeDefinition(value.InstructionSet, value.EntryPoint,
                    value.DefaultInstructionLimit, value.OutputTarget, value.VmOperations,
                    value.ExpressionOperations.Unary.Select(item => Pair(item.Token, item.Operation)),
                    value.ExpressionOperations.Binary.Select(item => Pair(item.Token, item.Operation)),
                    value.ExpressionOperations.Postfix.Select(item => Pair(item.Token, item.Operation)));
            }
            catch (DdfEngineCapabilityException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL122", exception.Message, file, "$",
                    DdfLanguagePackageDiagnosticSeverity.Error, DdfDiagnosticDomain.EngineCapability));
                return null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL121", exception.Message, file, "$"));
                return null;
            }
        }

        private static void validateRuntimeBindings(IReadOnlyList<RuntimeOperationDto> bindings,
            DdfLanguageDefinition language, string category, string file, string path,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            validateUnique(bindings.Select(item => item?.Token), "binding runtime " + category, file, path, diagnostics);
            foreach (RuntimeOperationDto binding in bindings.Where(item => item != null))
            {
                bool declared = language.TryGetOperator(binding.Token, out DdfOperatorDefinition op) &&
                    (category == "unary" ? op.IsPrefix : category == "postfix" ? op.IsPostfix : op.BinaryPrecedence > 0);
                require(declared, diagnostics, "DDFL123",
                    "Il token runtime '" + binding.Token + "' non è un operatore " + category + " dichiarato.",
                    file, path);
            }
        }

        private static KeyValuePair<string, string> Pair(string key, string value) =>
            new KeyValuePair<string, string>(key, value);

        private static DdfStandardLibraryDefinition createStandardLibrary(StandardLibraryDto value,
            DdfTypeSystemDefinition types, string file, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(!string.IsNullOrWhiteSpace(value.Id) &&
                        Regex.IsMatch(value.Id, "^[a-z][a-z0-9.-]*$"), diagnostics, "DDFL133",
                    "Identità della libreria standard non valida.", file, "$.id");
                require(Version.TryParse(value.Version, out _) &&
                        value.Version.Split('.').Length == 3, diagnostics, "DDFL133",
                    "Versione della libreria standard non valida.", file, "$.version");
                value.Functions = value.Functions ?? new List<StandardFunctionDto>();
                validateUnique(value.Functions.Select(item => item?.Name), "funzione standard", file,
                    "$.functions", diagnostics);
                var functions = new List<DdfStandardFunction>();
                foreach (StandardFunctionDto function in value.Functions.Where(item => item != null))
                {
                    function.ParameterTypes = function.ParameterTypes ?? new List<string>();
                    require(types.IsKnownPrimitive(function.ReturnType) && function.ParameterTypes.All(types.IsKnownPrimitive),
                        diagnostics, "DDFL130", "La funzione standard '" + function.Name + "' usa tipi sconosciuti.",
                        file, "$.functions");
                    if (!DdfHostFunctionBindings.IsSupported(function.Binding))
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL132",
                            "Binding host non supportato: " + function.Binding + ".", file, "$.functions",
                            DdfLanguagePackageDiagnosticSeverity.Error, DdfDiagnosticDomain.EngineCapability));
                        continue;
                    }
                    if (!Enum.TryParse(function.Effect, true,
                            out DdfStandardFunctionEffect effect) || !Enum.IsDefined(effect))
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL133",
                            "Effetto non valido per la funzione standard '" + function.Name + "'.",
                            file, "$.functions"));
                        continue;
                    }
                    function.ErrorCodes = function.ErrorCodes ?? new List<string>();
                    bool validErrors = function.ErrorCodes.Distinct(StringComparer.Ordinal).Count() ==
                                       function.ErrorCodes.Count && function.ErrorCodes.All(code =>
                                           !string.IsNullOrWhiteSpace(code) && Regex.IsMatch(code, "^DDF[0-9]{3}$"));
                    DdfStandardFunctionEffect expectedEffect =
                        DdfHostFunctionBindings.GetEffect(function.Binding);
                    IReadOnlyList<string> expectedErrors =
                        DdfHostFunctionBindings.GetErrorCodes(function.Binding);
                    if (!validErrors || effect != expectedEffect ||
                        !function.ErrorCodes.OrderBy(code => code, StringComparer.Ordinal).SequenceEqual(
                            expectedErrors.OrderBy(code => code, StringComparer.Ordinal), StringComparer.Ordinal))
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL134",
                            "Effetto o diagnostiche non coerenti con il binding della funzione standard '" +
                            function.Name + "'.", file, "$.functions"));
                        continue;
                    }
                    functions.Add(new DdfStandardFunction(function.Name, function.ReturnType,
                        function.Documentation, function.Binding, function.ParameterTypes,
                        effect, function.ErrorCodes));
                }
                if (hasErrors(diagnostics)) return null;
                return new DdfStandardLibraryDefinition(value.Id, value.Version, functions);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL131", exception.Message, file, "$"));
                return null;
            }
        }

        private static DdfBuildDefinition createBuild(BuildDto value, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try { return new DdfBuildDefinition(value.DefaultBackend, value.ArtifactFormat, value.Backends); }
            catch (DdfEngineCapabilityException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL142", exception.Message, file, "$",
                    DdfLanguagePackageDiagnosticSeverity.Error, DdfDiagnosticDomain.EngineCapability));
                return null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL141", exception.Message, file, "$"));
                return null;
            }
        }

        private static DdfFormattingDefinition createFormatting(FormattingDto value,
            DdfLanguageDefinition language, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.Tokens != null && value.AutoPairs != null && value.IndentAfter != null &&
                    value.QuickFixes != null, diagnostics, "DDFL080",
                    "Configurazione formatter incompleta.", file, "$");
                if (hasErrors(diagnostics)) return null;
                string[] structuralTokens = { value.Tokens.BlockOpen, value.Tokens.BlockClose,
                    value.Tokens.GroupOpen, value.Tokens.GroupClose, value.Tokens.IndexOpen,
                    value.Tokens.IndexClose, value.Tokens.StatementTerminator, value.Tokens.Separator };
                require(structuralTokens.All(token => !string.IsNullOrEmpty(token) && token.Length == 1 &&
                    language.Punctuation.Contains(token[0])), diagnostics, "DDFL082",
                    "I delimitatori del formatter devono essere punteggiatura dichiarata nel lessico.",
                    file, "$.tokens");
                if (hasErrors(diagnostics)) return null;
                return new DdfFormattingDefinition(value.Tokens.BlockOpen, value.Tokens.BlockClose,
                    value.Tokens.GroupOpen, value.Tokens.GroupClose, value.Tokens.IndexOpen,
                    value.Tokens.IndexClose, value.Tokens.StatementTerminator, value.Tokens.Separator,
                    value.AutoPairs.Select(item => new KeyValuePair<char, char>(one(item.Open), one(item.Close))),
                    value.IndentAfter, value.BlankLineBetweenStatements,
                    value.QuickFixes.Select(item => new DdfQuickFixRule(item.DiagnosticCode, item.Title, item.Replacement)));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL081", exception.Message, file, "$"));
                return null;
            }
        }

        private static char one(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 1)
                throw new ArgumentException("Le coppie automatiche richiedono caratteri singoli.");
            return value[0];
        }

        private static T? parseOptionalEnum<T>(string value, string property) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;
            throw new ArgumentException("Valore IDE non supportato per " + property + ": " + value + ".");
        }

        private static T parseRequiredEnum<T>(string value, string property) where T : struct
        {
            T? parsed = parseOptionalEnum<T>(value, property);
            if (parsed.HasValue) return parsed.Value;
            throw new ArgumentException("Valore richiesto per " + property + ".");
        }

        private static DdfLexicalDefinition createLexicalRules(LexiconDto lexicon, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            LexicalDto lexical = lexicon.Lexical;
            if (lexical == null || lexical.Whitespace == null || lexical.Identifier == null ||
                lexical.Identifier.Start == null || lexical.Identifier.Part == null ||
                lexical.Number == null || lexical.Number.Digits == null || lexical.DelimitedTokens == null)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL050",
                    "Definizione delle regole lessicali incompleta.", file, "$.lexical"));
                return null;
            }

            try
            {
                DdfCharacterSetDefinition whitespace = createCharacterSet(lexical.Whitespace);
                DdfCharacterSetDefinition identifierStart = createCharacterSet(lexical.Identifier.Start);
                DdfCharacterSetDefinition identifierPart = createCharacterSet(lexical.Identifier.Part);
                DdfCharacterSetDefinition digits = createCharacterSet(lexical.Number.Digits);
                require(identifierStart.UnicodeLetters ? identifierPart.UnicodeLetters : true, diagnostics, "DDFL051",
                    "Le lettere ammesse all'inizio devono essere ammesse anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(identifierStart.UnicodeDigits ? identifierPart.UnicodeDigits : true, diagnostics, "DDFL051",
                    "Le cifre ammesse all'inizio devono essere ammesse anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(identifierStart.Characters.All(identifierPart.Contains), diagnostics, "DDFL051",
                    "I caratteri espliciti iniziali devono essere ammessi anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(digits.UnicodeDigits || digits.Characters.Length > 0, diagnostics, "DDFL052",
                    "La regola numerica non definisce alcuna cifra.", file, "$.lexical.number.digits");

                var rules = new List<DdfDelimitedTokenRule>();
                for (int index = 0; index < lexical.DelimitedTokens.Count; index++)
                {
                    DelimitedTokenDto item = lexical.DelimitedTokens[index];
                    string path = "$.lexical.delimitedTokens[" + index + "]";
                    if (item == null || !Enum.TryParse(item.TokenKind, true, out DdfTokenKind tokenKind) || !Enum.IsDefined(tokenKind))
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL053",
                            "Categoria del token delimitato non valida.", file, path + ".tokenKind"));
                        continue;
                    }
                    try
                    {
                        rules.Add(new DdfDelimitedTokenRule(tokenKind, item.Start, item.End,
                            item.EndsAtLineBreak, item.AllowLineBreak, item.EscapeCharacter,
                            item.UnterminatedCode, item.UnterminatedMessage, item.AllowedEscapeCharacters));
                    }
                    catch (ArgumentException exception)
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL054", exception.Message, file, path));
                    }
                }

                for (int earlier = 0; !lexical.LongestMatch && earlier < rules.Count; earlier++)
                    for (int later = earlier + 1; later < rules.Count; later++)
                        if (rules[later].Start.StartsWith(rules[earlier].Start, StringComparison.Ordinal))
                            diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL055",
                                "Regola irraggiungibile senza longest match: " + rules[later].Start + ".",
                                file, "$.lexical.delimitedTokens[" + later + "]"));
                for (int earlier = 0; !lexical.LongestMatch && earlier < lexicon.Operators.Count; earlier++)
                    for (int later = earlier + 1; later < lexicon.Operators.Count; later++)
                        if (lexicon.Operators[earlier] != null && lexicon.Operators[later] != null &&
                            !string.IsNullOrEmpty(lexicon.Operators[earlier].Text) &&
                            !string.IsNullOrEmpty(lexicon.Operators[later].Text) &&
                            lexicon.Operators[later].Text.StartsWith(lexicon.Operators[earlier].Text, StringComparison.Ordinal))
                            diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL066",
                                "Operatore irraggiungibile senza longest match: " + lexicon.Operators[later].Text + ".",
                                file, "$.operators[" + later + "]"));

                var number = new DdfNumberRuleDefinition(digits, lexical.Number.DecimalSeparator,
                    lexical.Number.RequireFractionDigits);
                if (number.DecimalSeparator.Length == 1)
                    require(!whitespace.Contains(number.DecimalSeparator[0]) &&
                            !digits.Contains(number.DecimalSeparator[0]) &&
                            !identifierPart.Contains(number.DecimalSeparator[0]), diagnostics, "DDFL067",
                        "Il separatore decimale è in conflitto con un'altra classe di caratteri.",
                        file, "$.lexical.number.decimalSeparator");
                var result = new DdfLexicalDefinition(whitespace, identifierStart, identifierPart,
                    number, rules, lexical.LongestMatch);
                require(!characterSetsOverlap(whitespace, identifierPart) && !characterSetsOverlap(whitespace, digits),
                    diagnostics, "DDFL058", "Spazi, identificatori e cifre contengono classi di caratteri in conflitto.",
                    file, "$.lexical");
                require(!characterSetsOverlap(identifierStart, digits), diagnostics, "DDFL059",
                    "L'inizio degli identificatori è in conflitto con la regola numerica.", file, "$.lexical");
                foreach (DdfDelimitedTokenRule rule in result.DelimitedTokens)
                {
                    require(!whitespace.Contains(rule.Start[0]), diagnostics, "DDFL056",
                        "Il marcatore lessicale inizia con uno spazio configurato: " + rule.Start + ".", file, "$.lexical.delimitedTokens");
                    require(!lexicon.Operators.Any(item => item != null && item.Text == rule.Start) &&
                            !lexicon.Punctuation.Any(item => item == rule.Start), diagnostics, "DDFL061",
                        "Il marcatore lessicale duplica un operatore o delimitatore: " + rule.Start + ".", file, "$.lexical.delimitedTokens");
                }
                foreach (KeywordDto keyword in lexicon.Keywords.Where(item => item != null))
                    require(result.IsValidIdentifier(keyword.Text), diagnostics, "DDFL062",
                        "Keyword non raggiungibile con le regole degli identificatori: " + keyword.Text + ".", file, "$.keywords");
                foreach (string literal in lexicon.BooleanLiterals)
                    require(result.IsValidIdentifier(literal), diagnostics, "DDFL062",
                        "Literal booleano non raggiungibile con le regole degli identificatori: " + literal + ".", file, "$.booleanLiterals");
                var keywordTexts = new HashSet<string>(lexicon.Keywords.Where(item => item != null).Select(item => item.Text), StringComparer.Ordinal);
                require(!lexicon.BooleanLiterals.Any(keywordTexts.Contains), diagnostics, "DDFL065",
                    "Keyword e letterali booleani non possono condividere la stessa definizione.", file, "$.booleanLiterals");
                var operatorTexts = new HashSet<string>(lexicon.Operators.Where(item => item != null).Select(item => item.Text), StringComparer.Ordinal);
                require(!lexicon.Punctuation.Any(operatorTexts.Contains), diagnostics, "DDFL065",
                    "Operatori e punteggiatura non possono condividere la stessa definizione.", file, "$.punctuation");
                foreach (OperatorDto item in lexicon.Operators.Where(item => item != null && !string.IsNullOrEmpty(item.Text)))
                    require(!whitespace.Contains(item.Text[0]) && !identifierStart.Contains(item.Text[0]) && !digits.Contains(item.Text[0]),
                        diagnostics, "DDFL063", "Operatore irraggiungibile con le regole lessicali: " + item.Text + ".", file, "$.operators");
                foreach (string item in lexicon.Punctuation.Where(item => !string.IsNullOrEmpty(item)))
                    require(!whitespace.Contains(item[0]) && !identifierStart.Contains(item[0]) && !digits.Contains(item[0]),
                        diagnostics, "DDFL064", "Punteggiatura irraggiungibile con le regole lessicali: " + item + ".", file, "$.punctuation");
                return hasErrors(diagnostics) ? null : result;
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL057", exception.Message, file, "$.lexical"));
                return null;
            }
        }

        private static DdfCharacterSetDefinition createCharacterSet(CharacterSetDto value)
        {
            return new DdfCharacterSetDefinition(value.UnicodeLetters, value.UnicodeDigits,
                value.UnicodeWhitespace, value.Characters);
        }

        private static bool characterSetsOverlap(DdfCharacterSetDefinition first, DdfCharacterSetDefinition second)
        {
            if ((first.UnicodeLetters && second.UnicodeLetters) ||
                (first.UnicodeDigits && second.UnicodeDigits) ||
                (first.UnicodeWhitespace && second.UnicodeWhitespace)) return true;
            return first.Characters.Any(second.Contains) || second.Characters.Any(first.Contains);
        }

        private static void validateUnique(IEnumerable<string> values, string label, string file, string path,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL040", "Definizione " + label + " vuota o duplicata: " + value, file, path));
        }
        private static void validateUniqueRoles(IEnumerable<string> values, string label, string file, string path,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values.Where(item => !string.IsNullOrWhiteSpace(item) &&
                                                          !string.Equals(item, "None", StringComparison.OrdinalIgnoreCase)))
                if (!seen.Add(value))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL047",
                        "Definizione " + label + " duplicata: " + value + ".", file, path));
        }
        private static bool unique(IEnumerable<string> values) => values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count();
        private static bool uniqueExact(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).Count() == values.Count();
        private static bool validRole(string role) => !string.IsNullOrWhiteSpace(role) && Regex.IsMatch(role, "^[A-Z][A-Za-z0-9.]*$");
        private static bool safeFile(string file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file && file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        private static bool safeResourceFile(string file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file &&
            (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ebnf", StringComparison.OrdinalIgnoreCase));
        private static bool hasErrors(IEnumerable<DdfLanguagePackageDiagnostic> diagnostics) => diagnostics.Any(item => item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
        private static void require(bool condition, ICollection<DdfLanguagePackageDiagnostic> diagnostics,
            string code, string message, string file, string path,
            DdfDiagnosticDomain domain = DdfDiagnosticDomain.LanguageDefinition)
        {
            if (!condition) diagnostics.Add(new DdfLanguagePackageDiagnostic(code, message, file, path,
                DdfLanguagePackageDiagnosticSeverity.Error, domain));
        }
        private static DdfLanguagePackageLoadResult Failure(string code, string message, string file = null) =>
            new DdfLanguagePackageLoadResult(null, new[] { new DdfLanguagePackageDiagnostic(code, message, file) });

        private interface IPackageReader : IDisposable
        {
            IEnumerable<string> Files { get; }
            bool Exists(string file);
            string Read(string file);
        }
        private sealed class DirectoryReader : IPackageReader
        {
            private readonly string root;
            public DirectoryReader(string rootPath) { root = Path.GetFullPath(rootPath); }
            public IEnumerable<string> Files => Directory.EnumerateFiles(root).Where(path => safeResourceFile(Path.GetFileName(path))).Select(Path.GetFileName);
            public bool Exists(string file) => safeResourceFile(file) && File.Exists(Path.Combine(root, file));
            public string Read(string file) => File.ReadAllText(Path.Combine(root, file), Encoding.UTF8);
            public void Dispose() { }
        }
        private sealed class ArchiveReader : IPackageReader
        {
            private readonly ZipArchive archive;
            public ArchiveReader(string path) { archive = ZipFile.OpenRead(path); }
            public IEnumerable<string> Files => archive.Entries.Where(entry => safeResourceFile(entry.FullName)).Select(entry => entry.FullName);
            public bool Exists(string file) => safeResourceFile(file) && archive.GetEntry(file) != null;
            public string Read(string file) { using (StreamReader reader = new StreamReader(archive.GetEntry(file).Open(), Encoding.UTF8)) return reader.ReadToEnd(); }
            public void Dispose() => archive.Dispose();
        }
        private sealed class EmbeddedReader : IPackageReader
        {
            private const string Prefix = "DDFLanguageEditor.Core.LanguagePackages.Ddf.";
            private readonly Assembly assembly = typeof(DdfLanguagePackageLoader).Assembly;
            public IEnumerable<string> Files => assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Select(name => name.Substring(Prefix.Length));
            public bool Exists(string file) => assembly.GetManifestResourceInfo(Prefix + file) != null;
            public string Read(string file) { using (Stream stream = assembly.GetManifestResourceStream(Prefix + file)) { if (stream == null) throw new FileNotFoundException(file); using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd(); } }
            public void Dispose() { }
        }

        private class SectionDto { public string SchemaVersion { get; set; } public string Adapter { get; set; } public List<string> DependsOn { get; set; } public string Description { get; set; } }
        private sealed class GrammarDto : SectionDto { public string Notation { get; set; } public string EbnfFile { get; set; } public string StartRule { get; set; } public string ChoiceStrategy { get; set; } public List<GrammarNodeDto> Nodes { get; set; } }
        private sealed class GrammarNodeDto { public string Rule { get; set; } public string Kind { get; set; } public List<GrammarFieldDto> Fields { get; set; } public GrammarIdeDto Ide { get; set; } }
        private sealed class GrammarFieldDto { public string Name { get; set; } public string SourceRule { get; set; } public bool Multiple { get; set; } }
        private sealed class GrammarIdeDto { public string SymbolKind { get; set; } public string NameField { get; set; } public string NamePrefix { get; set; } public string NameSuffix { get; set; } public string Detail { get; set; } public string FoldingKind { get; set; } public string FoldingField { get; set; } public string BreadcrumbKind { get; set; } public string BreadcrumbLabel { get; set; } public string BreadcrumbSelectionField { get; set; } public int BreadcrumbSelectionLength { get; set; } public bool Selectable { get; set; } public string TypeField { get; set; } public string ReturnTypeField { get; set; } public string ParametersField { get; set; } public string ChildSymbolKind { get; set; } }
        private sealed class TypesDto : SectionDto { public List<PrimitiveTypeDto> Primitives { get; set; } public List<TypeConversionDto> Conversions { get; set; } public TypeCompatibilityDto Compatibility { get; set; } public MutabilityDto Mutability { get; set; } public ScopesDto Scopes { get; set; } public SignaturesDto Signatures { get; set; } public LiteralTypesDto Literals { get; set; } public NumericTypesDto Numeric { get; set; } public IndexingDto Indexing { get; set; } public ClrMappingDto ClrMapping { get; set; } }
        private sealed class PrimitiveTypeDto { public string Name { get; set; } public string Category { get; set; } public bool? CanDeclareVariable { get; set; } public string IndexedElementType { get; set; } }
        private sealed class TypeConversionDto { public string From { get; set; } public string To { get; set; } public string Context { get; set; } }
        private sealed class TypeCompatibilityDto { public bool? ArraysInvariant { get; set; } public bool? UserTypesNominal { get; set; } public bool? ErrorTypeCompatible { get; set; } }
        private sealed class MutabilityDto { public bool? VariablesMutable { get; set; } public bool? ParametersMutable { get; set; } public bool? FieldsMutable { get; set; } public List<string> AssignmentTargets { get; set; } }
        private sealed class ScopesDto { public bool? FunctionParameters { get; set; } public bool? Blocks { get; set; } public bool? ForInitializers { get; set; } public bool? StructureFields { get; set; } public bool? AllowShadowing { get; set; } }
        private sealed class SignaturesDto { public bool? ExactArity { get; set; } public bool? RequireReturnForNonVoid { get; set; } public string VoidType { get; set; } public string ParameterOrder { get; set; } }
        private sealed class LiteralTypesDto { public string Integer { get; set; } public string Decimal { get; set; } public string Boolean { get; set; } public string String { get; set; } }
        private sealed class NumericTypesDto { public string ResultType { get; set; } public string DominantType { get; set; } }
        private sealed class IndexingDto { public string IndexType { get; set; } }
        private sealed class ClrMappingDto { public string Backend { get; set; } public string MappingVersion { get; set; } public string ArrayRepresentation { get; set; } public string StructureRepresentation { get; set; } public List<ClrPrimitiveMappingDto> Primitives { get; set; } }
        private sealed class ClrPrimitiveMappingDto { public string Type { get; set; } public string ClrType { get; set; } public string CsharpType { get; set; } }
        private sealed class SemanticsDto : SectionDto { public string OperationSet { get; set; } public string OperationSetVersion { get; set; } public List<SemanticBindingDto> Bindings { get; set; } }
        private sealed class SemanticBindingDto { public string Rule { get; set; } public List<SemanticOperationDto> Operations { get; set; } }
        private sealed class SemanticOperationDto { public string Id { get; set; } public Dictionary<string, string> Arguments { get; set; } }
        private sealed class IntellisenseDto : SectionDto { public CompletionDto Completion { get; set; } public SignatureHelpDto SignatureHelp { get; set; } public List<SymbolPresentationDto> SymbolKinds { get; set; } public List<SnippetDto> Snippets { get; set; } public FlowchartPresentationDto Flowchart { get; set; } public Dictionary<string, string> Diagnostics { get; set; } }
        private sealed class FlowchartPresentationDto { public string TrueLabel { get; set; } public string FalseLabel { get; set; } public string ExitLabelFormat { get; set; } public string GuideTitle { get; set; } public string GuidePrompt { get; set; } public string DebugTitle { get; set; } public string SelectedTitle { get; set; } public string DebugPrefix { get; set; } public string ValuesPrefix { get; set; } public Dictionary<string, string> Explanations { get; set; } }
        private sealed class CompletionDto { public string KeywordOrigin { get; set; } public string BooleanOrigin { get; set; } public string SnippetOrigin { get; set; } public string StandardLibraryOrigin { get; set; } public string CurrentDocumentOrigin { get; set; } public string WorkspaceOrigin { get; set; } public string TypeDetail { get; set; } public string ControlFlowDetail { get; set; } public string KeywordDetail { get; set; } public string BooleanDetail { get; set; } public string StandardFunctionDetailPrefix { get; set; } }
        private sealed class SignatureHelpDto { public string CurrentDocumentOrigin { get; set; } public string WorkspaceOrigin { get; set; } public string StandardLibraryOrigin { get; set; } }
        private sealed class SymbolPresentationDto { public string Kind { get; set; } public string Label { get; set; } public string Documentation { get; set; } }
        private sealed class SnippetDto { public string Prefix { get; set; } public string DisplayText { get; set; } public string Description { get; set; } public string Body { get; set; } public bool RequiresKeyword { get; set; } }
        private sealed class FormattingDto : SectionDto { public FormattingTokensDto Tokens { get; set; } public List<AutoPairDto> AutoPairs { get; set; } public List<string> IndentAfter { get; set; } public bool BlankLineBetweenStatements { get; set; } public List<QuickFixDto> QuickFixes { get; set; } }
        private sealed class RuntimeDto : SectionDto { public string InstructionSet { get; set; } public string EntryPoint { get; set; } public int DefaultInstructionLimit { get; set; } public string OutputTarget { get; set; } public List<string> VmOperations { get; set; } public RuntimeExpressionOperationsDto ExpressionOperations { get; set; } }
        private sealed class RuntimeExpressionOperationsDto { public List<RuntimeOperationDto> Unary { get; set; } public List<RuntimeOperationDto> Binary { get; set; } public List<RuntimeOperationDto> Postfix { get; set; } }
        private sealed class RuntimeOperationDto { public string Token { get; set; } public string Operation { get; set; } }
        private sealed class StandardLibraryDto : SectionDto { public string Id { get; set; } public string Version { get; set; } public List<StandardFunctionDto> Functions { get; set; } }
        private sealed class StandardFunctionDto { public string Name { get; set; } public string ReturnType { get; set; } public List<string> ParameterTypes { get; set; } public string Documentation { get; set; } public string Binding { get; set; } public string Effect { get; set; } public List<string> ErrorCodes { get; set; } }
        private sealed class BuildDto : SectionDto { public string DefaultBackend { get; set; } public string ArtifactFormat { get; set; } public List<string> Backends { get; set; } }
        private sealed class TestsDto : SectionDto { public List<PackageTestCaseDto> Cases { get; set; } }
        private sealed class PackageTestCaseDto { public string Name { get; set; } public string Source { get; set; } public List<string> ExpectedTokenKinds { get; set; } public List<string> ExpectedDiagnosticCodes { get; set; } public string ExpectedFormatted { get; set; } public string ExpectedReturn { get; set; } }
        private sealed class FormattingTokensDto { public string BlockOpen { get; set; } public string BlockClose { get; set; } public string GroupOpen { get; set; } public string GroupClose { get; set; } public string IndexOpen { get; set; } public string IndexClose { get; set; } public string StatementTerminator { get; set; } public string Separator { get; set; } }
        private sealed class AutoPairDto { public string Open { get; set; } public string Close { get; set; } }
        private sealed class QuickFixDto { public string DiagnosticCode { get; set; } public string Title { get; set; } public string Replacement { get; set; } }
        private sealed class LexiconDto : SectionDto { public LexicalDto Lexical { get; set; } public List<KeywordDto> Keywords { get; set; } public List<string> BooleanLiterals { get; set; } public Dictionary<string, bool> BooleanLiteralValues { get; set; } public List<OperatorDto> Operators { get; set; } public List<string> Punctuation { get; set; } }
        private sealed class LexicalDto { public CharacterSetDto Whitespace { get; set; } public IdentifierDto Identifier { get; set; } public NumberDto Number { get; set; } public List<DelimitedTokenDto> DelimitedTokens { get; set; } public bool LongestMatch { get; set; } }
        private sealed class CharacterSetDto { public bool UnicodeLetters { get; set; } public bool UnicodeDigits { get; set; } public bool UnicodeWhitespace { get; set; } public string Characters { get; set; } }
        private sealed class IdentifierDto { public CharacterSetDto Start { get; set; } public CharacterSetDto Part { get; set; } }
        private sealed class NumberDto { public CharacterSetDto Digits { get; set; } public string DecimalSeparator { get; set; } public bool RequireFractionDigits { get; set; } }
        private sealed class DelimitedTokenDto { public string TokenKind { get; set; } public string Start { get; set; } public string End { get; set; } public bool EndsAtLineBreak { get; set; } public bool AllowLineBreak { get; set; } = true; public string EscapeCharacter { get; set; } public string AllowedEscapeCharacters { get; set; } public string UnterminatedCode { get; set; } public string UnterminatedMessage { get; set; } }
        private sealed class KeywordDto { public string Text { get; set; } public string TokenKind { get; set; } public string Role { get; set; } }
        private sealed class OperatorDto { public string Text { get; set; } public int BinaryPrecedence { get; set; } public string Associativity { get; set; } public int PrefixPrecedence { get; set; } public int PostfixPrecedence { get; set; } public string Role { get; set; } }
        private sealed class NativeExtensionDto { public string Id { get; set; } public string Publisher { get; set; } public string Version { get; set; } public string Sha256 { get; set; } public List<string> Capabilities { get; set; } }
        private sealed class ManifestDto { public string SchemaVersion { get; set; } public string EngineApi { get; set; } public string Id { get; set; } public string Name { get; set; } public string PackageVersion { get; set; } public string LanguageVersion { get; set; } public List<string> FileExtensions { get; set; } public Dictionary<string, string> Sections { get; set; } public List<NativeExtensionDto> NativeExtensions { get; set; } }
    }
}
