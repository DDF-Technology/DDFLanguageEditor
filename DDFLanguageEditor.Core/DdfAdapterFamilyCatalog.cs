using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDFLanguageEditor.Core
{
    public enum DdfAdapterFamilyStage
    {
        Available,
        Candidate,
        Deferred
    }

    public enum DdfAdapterFamilyClassification
    {
        DotNet,
        Platform,
        Host
    }

    public enum DdfAdapterPortability
    {
        Portable,
        ManagedProvider,
        PlatformSpecific,
        HostDefined,
        ExternalService
    }

    public enum DdfAdapterGateStatus
    {
        Complete,
        Required,
        NotApplicable
    }

    public sealed class DdfAdapterFamilyGate
    {
        internal DdfAdapterFamilyGate(DdfAdapterGateStatus status,
            string evidence)
        {
            Status = status;
            Evidence = evidence ?? string.Empty;
        }

        public DdfAdapterGateStatus Status { get; }
        public string Evidence { get; }
        public bool IsSatisfied => Status == DdfAdapterGateStatus.Complete ||
            Status == DdfAdapterGateStatus.NotApplicable;
    }

    public sealed class DdfAdapterFamilyGates
    {
        internal DdfAdapterFamilyGates(DdfAdapterFamilyGate security,
            DdfAdapterFamilyGate portability, DdfAdapterFamilyGate licensing,
            DdfAdapterFamilyGate testing)
        {
            Security = security;
            Portability = portability;
            Licensing = licensing;
            Testing = testing;
        }

        public DdfAdapterFamilyGate Security { get; }
        public DdfAdapterFamilyGate Portability { get; }
        public DdfAdapterFamilyGate Licensing { get; }
        public DdfAdapterFamilyGate Testing { get; }

        public IEnumerable<DdfAdapterFamilyGate> All
        {
            get
            {
                yield return Security;
                yield return Portability;
                yield return Licensing;
                yield return Testing;
            }
        }
    }

    public sealed class DdfAdapterFamilyDefinition
    {
        internal DdfAdapterFamilyDefinition(string id, string displayName,
            DdfAdapterFamilyClassification classification,
            DdfAdapterFamilyStage stage, string maintainer,
            IEnumerable<string> useCases, DdfAdapterPortability portability,
            IEnumerable<string> dependencies,
            IEnumerable<string> capabilities, DdfAdapterFamilyGates gates,
            IEnumerable<string> blockers)
        {
            Id = id;
            DisplayName = displayName;
            Classification = classification;
            Stage = stage;
            Maintainer = maintainer ?? string.Empty;
            UseCases = ReadOnly(useCases);
            Portability = portability;
            Dependencies = ReadOnly(dependencies);
            Capabilities = ReadOnly(capabilities);
            Gates = gates;
            Blockers = ReadOnly(blockers);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public DdfAdapterFamilyClassification Classification { get; }
        public DdfAdapterFamilyStage Stage { get; }
        public bool StandardLibrary => false;
        public string Maintainer { get; }
        public IReadOnlyList<string> UseCases { get; }
        public DdfAdapterPortability Portability { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public DdfAdapterFamilyGates Gates { get; }
        public IReadOnlyList<string> Blockers { get; }
        public bool IsAvailable => Stage == DdfAdapterFamilyStage.Available;

        private static IReadOnlyList<string> ReadOnly(
            IEnumerable<string> values) => new ReadOnlyCollection<string>(
                (values ?? Array.Empty<string>()).ToList());
    }

    public sealed class DdfAdapterFamilyCatalog
    {
        internal DdfAdapterFamilyCatalog(string formatVersion,
            IEnumerable<DdfAdapterFamilyDefinition> families,
            string fingerprint)
        {
            FormatVersion = formatVersion;
            Families = new ReadOnlyCollection<DdfAdapterFamilyDefinition>(
                families.OrderBy(item => item.Id, StringComparer.Ordinal)
                    .ToList());
            Fingerprint = fingerprint;
        }

        public const string FormatVersionValue = "1.0";
        public string FormatVersion { get; }
        public IReadOnlyList<DdfAdapterFamilyDefinition> Families { get; }
        public string Fingerprint { get; }

        public DdfAdapterFamilyDefinition Find(string id) => Families
            .FirstOrDefault(item => string.Equals(item.Id, id,
                StringComparison.Ordinal));
    }

    public sealed class DdfAdapterFamilyCatalogIssue
    {
        internal DdfAdapterFamilyCatalogIssue(string code, string message,
            string jsonPath)
        {
            Code = code;
            Message = message;
            JsonPath = jsonPath ?? "$";
        }

        public string Code { get; }
        public string Message { get; }
        public string JsonPath { get; }
        public override string ToString() => Code + " " + JsonPath + ": " +
            Message;
    }

    public sealed class DdfAdapterFamilyCatalogLoadResult
    {
        internal DdfAdapterFamilyCatalogLoadResult(
            DdfAdapterFamilyCatalog catalog,
            IEnumerable<DdfAdapterFamilyCatalogIssue> issues)
        {
            Catalog = catalog;
            Issues = new ReadOnlyCollection<DdfAdapterFamilyCatalogIssue>(
                issues.ToList());
        }

        public DdfAdapterFamilyCatalog Catalog { get; }
        public IReadOnlyList<DdfAdapterFamilyCatalogIssue> Issues { get; }
        public bool Success => Catalog != null && Issues.Count == 0;
    }

    /// <summary>
    /// Loads the governance catalog only. It never discovers, installs or
    /// activates an adapter and therefore cannot enlarge runtime authority.
    /// </summary>
    public static class DdfAdapterFamilyCatalogLoader
    {
        private static readonly JsonSerializerOptions ReadOptions = CreateOptions();
        private static readonly JsonSerializerOptions WriteOptions = CreateOptions(true);

        public static DdfAdapterFamilyCatalogLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Failure("DDFAC001", "Percorso catalogo richiesto.");
            try
            {
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                return Failure("DDFAC001", exception.Message);
            }
        }

        public static DdfAdapterFamilyCatalogLoadResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Failure("DDFAC002", "Catalogo JSON vuoto.");
            CatalogDocument document;
            try
            {
                document = JsonSerializer.Deserialize<CatalogDocument>(json,
                    ReadOptions);
            }
            catch (JsonException exception)
            {
                return Failure("DDFAC002", exception.Message,
                    string.IsNullOrWhiteSpace(exception.Path) ? "$" :
                    exception.Path);
            }
            if (document == null)
                return Failure("DDFAC002", "Catalogo JSON vuoto.");

            List<DdfAdapterFamilyCatalogIssue> issues = Validate(document);
            if (issues.Count != 0)
                return new DdfAdapterFamilyCatalogLoadResult(null, issues);

            List<FamilyDocument> ordered = document.Families
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
            string canonical = JsonSerializer.Serialize(new CatalogDocument
            {
                FormatVersion = document.FormatVersion,
                Families = ordered
            }, WriteOptions);
            string fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            var families = ordered.Select(CreateDefinition).ToList();
            return new DdfAdapterFamilyCatalogLoadResult(
                new DdfAdapterFamilyCatalog(document.FormatVersion, families,
                    fingerprint), Array.Empty<DdfAdapterFamilyCatalogIssue>());
        }

        private static List<DdfAdapterFamilyCatalogIssue> Validate(
            CatalogDocument document)
        {
            var issues = new List<DdfAdapterFamilyCatalogIssue>();
            void Add(string code, string message, string path) => issues.Add(
                new DdfAdapterFamilyCatalogIssue(code, message, path));

            if (document.FormatVersion !=
                DdfAdapterFamilyCatalog.FormatVersionValue)
                Add("DDFAC003", "Versione formato non supportata.",
                    "$.formatVersion");
            if (document.Families == null || document.Families.Count == 0)
            {
                Add("DDFAC004", "Il catalogo deve contenere almeno una famiglia.",
                    "$.families");
                return issues;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var capabilities = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.Families.Count; index++)
            {
                FamilyDocument family = document.Families[index];
                string path = "$.families[" + index + "]";
                if (family == null)
                {
                    Add("DDFAC005", "Definizione famiglia mancante.", path);
                    continue;
                }
                if (!family.Classification.HasValue)
                    Add("DDFAC005", "Classificazione obbligatoria mancante.",
                        path + ".classification");
                if (!family.Stage.HasValue)
                    Add("DDFAC005", "Stadio obbligatorio mancante.",
                        path + ".stage");
                if (!family.Portability.HasValue)
                    Add("DDFAC005", "Portabilità obbligatoria mancante.",
                        path + ".portability");
                if (string.IsNullOrWhiteSpace(family.Id) ||
                    !family.Classification.HasValue ||
                    !HasExpectedPrefix(family.Id,
                        family.Classification.Value))
                    Add("DDFAC005", "ID assente o incoerente con la classificazione.",
                        path + ".id");
                else if (!ids.Add(family.Id))
                    Add("DDFAC006", "ID famiglia duplicato.", path + ".id");
                RequireText(family.DisplayName, "displayName", path, issues);
                if (!family.StandardLibrary.HasValue)
                    Add("DDFAC005", "Il confine dalla libreria standard deve essere esplicito.",
                        path + ".standardLibrary");
                else if (family.StandardLibrary.Value)
                    Add("DDFAC007", "Una famiglia adapter non può diventare implicitamente libreria standard.",
                        path + ".standardLibrary");
                if (family.UseCases == null || family.UseCases.Count == 0 ||
                    family.UseCases.Any(string.IsNullOrWhiteSpace))
                    Add("DDFAC008", "È richiesto almeno un caso d'uso concreto.",
                        path + ".useCases");
                if (family.Capabilities == null ||
                    family.Capabilities.Count == 0)
                    Add("DDFAC009", "È richiesta almeno una capability.",
                        path + ".capabilities");
                else
                {
                    foreach (string capability in family.Capabilities)
                    {
                        if (string.IsNullOrWhiteSpace(capability) ||
                            !capabilities.Add(capability))
                            Add("DDFAC009", "Capability assente o duplicata nel catalogo.",
                                path + ".capabilities");
                    }
                }
                if (family.Dependencies == null)
                    Add("DDFAC005", "L'elenco dipendenze deve essere esplicito.",
                        path + ".dependencies");
                if (family.Blockers == null)
                    Add("DDFAC005", "L'elenco blocker deve essere esplicito.",
                        path + ".blockers");
                ValidateGate(family.Gates?.Security, "security", path, issues);
                ValidateGate(family.Gates?.Portability, "portability", path, issues);
                ValidateGate(family.Gates?.Licensing, "licensing", path, issues);
                ValidateGate(family.Gates?.Testing, "testing", path, issues);

                if (family.Stage == DdfAdapterFamilyStage.Available)
                {
                    if (string.IsNullOrWhiteSpace(family.Maintainer))
                        Add("DDFAC010", "Una famiglia disponibile richiede un maintainer.",
                            path + ".maintainer");
                    if (family.Gates == null || family.Gates.All
                        .Any(gate => gate == null || !gate.IsSatisfied))
                        Add("DDFAC011", "Una famiglia disponibile richiede tutti i gate soddisfatti.",
                            path + ".gates");
                    if (family.Blockers != null && family.Blockers.Count != 0)
                        Add("DDFAC012", "Una famiglia disponibile non può avere blocker aperti.",
                            path + ".blockers");
                }
            }
            return issues;
        }

        private static void RequireText(string value, string property,
            string path, ICollection<DdfAdapterFamilyCatalogIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(value)) issues.Add(
                new DdfAdapterFamilyCatalogIssue("DDFAC005",
                    "Valore obbligatorio mancante.", path + "." + property));
        }

        private static void ValidateGate(GateDocument gate, string name,
            string path, ICollection<DdfAdapterFamilyCatalogIssue> issues)
        {
            string gatePath = path + ".gates." + name;
            if (gate == null)
            {
                issues.Add(new DdfAdapterFamilyCatalogIssue("DDFAC013",
                    "Gate obbligatorio mancante.", gatePath));
                return;
            }
            if (!gate.Status.HasValue)
                issues.Add(new DdfAdapterFamilyCatalogIssue("DDFAC013",
                    "Lo stato del gate è obbligatorio.", gatePath +
                    ".status"));
            if (string.IsNullOrWhiteSpace(gate.Evidence))
                issues.Add(new DdfAdapterFamilyCatalogIssue("DDFAC013",
                    "Il gate richiede evidenza o motivazione.", gatePath +
                    ".evidence"));
        }

        private static bool HasExpectedPrefix(string id,
            DdfAdapterFamilyClassification classification)
        {
            string prefix = classification switch
            {
                DdfAdapterFamilyClassification.DotNet => "ddf.dotnet.",
                DdfAdapterFamilyClassification.Platform => "ddf.platform.",
                DdfAdapterFamilyClassification.Host => "host.",
                _ => string.Empty
            };
            return prefix.Length != 0 && id.StartsWith(prefix,
                StringComparison.Ordinal) && id.Length > prefix.Length;
        }

        private static DdfAdapterFamilyDefinition CreateDefinition(
            FamilyDocument family) => new(family.Id, family.DisplayName,
                family.Classification.Value, family.Stage.Value, family.Maintainer,
                family.UseCases, family.Portability.Value, family.Dependencies,
                family.Capabilities, new DdfAdapterFamilyGates(
                    CreateGate(family.Gates.Security),
                    CreateGate(family.Gates.Portability),
                    CreateGate(family.Gates.Licensing),
                    CreateGate(family.Gates.Testing)), family.Blockers);

        private static DdfAdapterFamilyGate CreateGate(GateDocument gate) =>
            new(gate.Status.Value, gate.Evidence);

        private static DdfAdapterFamilyCatalogLoadResult Failure(string code,
            string message, string path = "$") => new(null, new[]
            {
                new DdfAdapterFamilyCatalogIssue(code, message, path)
            });

        private static JsonSerializerOptions CreateOptions(
            bool writeIndented = false)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = writeIndented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase, false));
            return options;
        }

        private sealed class CatalogDocument
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; }
            public string FormatVersion { get; set; }
            public List<FamilyDocument> Families { get; set; }
        }

        private sealed class FamilyDocument
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public DdfAdapterFamilyClassification? Classification { get; set; }
            public DdfAdapterFamilyStage? Stage { get; set; }
            public bool? StandardLibrary { get; set; }
            public string Maintainer { get; set; }
            public List<string> UseCases { get; set; }
            public DdfAdapterPortability? Portability { get; set; }
            public List<string> Dependencies { get; set; }
            public List<string> Capabilities { get; set; }
            public GatesDocument Gates { get; set; }
            public List<string> Blockers { get; set; }
        }

        private sealed class GatesDocument
        {
            public GateDocument Security { get; set; }
            public GateDocument Portability { get; set; }
            public GateDocument Licensing { get; set; }
            public GateDocument Testing { get; set; }

            [JsonIgnore]
            public IEnumerable<GateDocument> All
            {
                get
                {
                    yield return Security;
                    yield return Portability;
                    yield return Licensing;
                    yield return Testing;
                }
            }
        }

        private sealed class GateDocument
        {
            public DdfAdapterGateStatus? Status { get; set; }
            public string Evidence { get; set; }
            [JsonIgnore]
            public bool IsSatisfied => Status ==
                DdfAdapterGateStatus.Complete || Status ==
                DdfAdapterGateStatus.NotApplicable;
        }
    }
}
