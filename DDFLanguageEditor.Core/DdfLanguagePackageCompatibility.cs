using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguageCompatibilityLevel
    {
        Information,
        Review,
        Breaking
    }

    public sealed class DdfLanguageCompatibilityFinding
    {
        public DdfLanguageCompatibilityFinding(DdfLanguageCompatibilityLevel level,
            string section, string message)
        {
            Level = level;
            Section = section ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DdfLanguageCompatibilityLevel Level { get; }
        public string Section { get; }
        public string Message { get; }
    }

    public sealed class DdfLanguagePackageCompatibilityReport
    {
        internal DdfLanguagePackageCompatibilityReport(string previousVersion,
            string currentVersion, IEnumerable<DdfLanguageCompatibilityFinding> findings)
        {
            PreviousVersion = previousVersion;
            CurrentVersion = currentVersion;
            Findings = Array.AsReadOnly(findings.ToArray());
        }

        public string PreviousVersion { get; }
        public string CurrentVersion { get; }
        public IReadOnlyList<DdfLanguageCompatibilityFinding> Findings { get; }
        public bool HasBreakingChanges => Findings.Any(item =>
            item.Level == DdfLanguageCompatibilityLevel.Breaking);
        public bool RequiresReview => Findings.Any(item =>
            item.Level == DdfLanguageCompatibilityLevel.Review);
    }

    public static class DdfLanguagePackageCompatibility
    {
        public static DdfLanguagePackageCompatibilityReport Compare(
            DdfLanguageAuthoringProject previous,
            DdfLanguageAuthoringProject current)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (current == null) throw new ArgumentNullException(nameof(current));
            DdfLanguagePackageLoadResult oldValidation =
                DdfLanguagePackageAuthoring.Validate(previous);
            DdfLanguagePackageLoadResult newValidation =
                DdfLanguagePackageAuthoring.Validate(current);
            if (!oldValidation.Success || !newValidation.Success)
                throw new InvalidOperationException(
                    "Entrambe le versioni devono superare la convalida prima del confronto. " +
                    string.Join(" | ", oldValidation.Diagnostics.Concat(newValidation.Diagnostics)
                        .Select(item => item.ToString())));

            JsonObject oldManifest = Read(previous, "manifest.json");
            JsonObject newManifest = Read(current, "manifest.json");
            string oldId = Value(oldManifest, "id");
            string newId = Value(newManifest, "id");
            if (!string.Equals(oldId, newId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il confronto richiede lo stesso ID di pacchetto: " + oldId +
                    " e " + newId + ".");

            string oldVersion = Value(oldManifest, "packageVersion");
            string newVersion = Value(newManifest, "packageVersion");
            var findings = new List<DdfLanguageCompatibilityFinding>();
            if (Version.Parse(newVersion).CompareTo(Version.Parse(oldVersion)) <= 0)
                Add(DdfLanguageCompatibilityLevel.Review, "manifest",
                    "La nuova packageVersion deve essere maggiore della precedente.");
            CompareScalar("manifest", "engineApi", oldManifest, newManifest,
                DdfLanguageCompatibilityLevel.Breaking);
            CompareScalar("manifest", "languageVersion", oldManifest, newManifest,
                DdfLanguageCompatibilityLevel.Review);
            CompareSet("manifest", "fileExtensions", oldManifest, newManifest);
            if (!JsonNode.DeepEquals(oldManifest["nativeExtensions"],
                    newManifest["nativeExtensions"]))
                Add(DdfLanguageCompatibilityLevel.Review, "manifest",
                    "I requisiti delle estensioni native sono cambiati.");

            JsonObject oldLexicon = Section(previous, oldManifest, "lexicon");
            JsonObject newLexicon = Section(current, newManifest, "lexicon");
            CompareNamedArray("lexicon", "keywords", "text", oldLexicon, newLexicon,
                DdfLanguageCompatibilityLevel.Review);
            CompareNamedArray("lexicon", "operators", "text", oldLexicon, newLexicon,
                DdfLanguageCompatibilityLevel.Review);
            CompareSet("lexicon", "punctuation", oldLexicon, newLexicon);
            CompareSet("lexicon", "booleanLiterals", oldLexicon, newLexicon);
            if (!JsonNode.DeepEquals(oldLexicon["booleanLiteralValues"],
                    newLexicon["booleanLiteralValues"]))
                Add(DdfLanguageCompatibilityLevel.Breaking, "lexicon",
                    "La mappatura dei letterali booleani è cambiata.");
            if (!JsonNode.DeepEquals(oldLexicon["lexical"], newLexicon["lexical"]))
                Add(DdfLanguageCompatibilityLevel.Review, "lexicon",
                    "Le regole lessicali sono cambiate.");

            JsonObject oldTypes = Section(previous, oldManifest, "types");
            JsonObject newTypes = Section(current, newManifest, "types");
            CompareNamedArray("types", "primitives", "name", oldTypes, newTypes,
                DdfLanguageCompatibilityLevel.Information);
            foreach (string key in new[] { "conversions", "compatibility", "mutability",
                "scopes", "signatures", "literals", "numeric", "indexing", "clrMapping" })
                if (!JsonNode.DeepEquals(oldTypes[key], newTypes[key]))
                    Add(DdfLanguageCompatibilityLevel.Review, "types",
                        key + " è cambiato: verificare le conversioni e la semantica dei tipi.");
            JsonObject oldRuntime = Section(previous, oldManifest, "runtime");
            JsonObject newRuntime = Section(current, newManifest, "runtime");
            foreach (string key in new[] { "instructionSet", "entryPoint", "outputTarget" })
                CompareScalar("runtime", key, oldRuntime, newRuntime,
                    DdfLanguageCompatibilityLevel.Breaking);
            CompareSet("runtime", "vmOperations", oldRuntime, newRuntime);
            CompareScalar("runtime", "defaultInstructionLimit", oldRuntime, newRuntime,
                DdfLanguageCompatibilityLevel.Review);
            if (!JsonNode.DeepEquals(oldRuntime["expressionOperations"],
                    newRuntime["expressionOperations"]))
                Add(DdfLanguageCompatibilityLevel.Breaking, "runtime",
                    "Le mappature delle operazioni di espressione sono cambiate.");

            JsonObject oldLibrary = Section(previous, oldManifest, "standardLibrary");
            JsonObject newLibrary = Section(current, newManifest, "standardLibrary");
            CompareScalar("standardLibrary", "id", oldLibrary, newLibrary,
                DdfLanguageCompatibilityLevel.Breaking);
            CompareNamedArray("standardLibrary", "functions", "name", oldLibrary,
                newLibrary, DdfLanguageCompatibilityLevel.Information);

            JsonObject oldBuild = Section(previous, oldManifest, "build");
            JsonObject newBuild = Section(current, newManifest, "build");
            CompareSet("build", "backends", oldBuild, newBuild);
            CompareScalar("build", "defaultBackend", oldBuild, newBuild,
                DdfLanguageCompatibilityLevel.Review);
            CompareScalar("build", "artifactFormat", oldBuild, newBuild,
                DdfLanguageCompatibilityLevel.Breaking);

            JsonObject oldGrammar = Section(previous, oldManifest, "grammar");
            JsonObject newGrammar = Section(current, newManifest, "grammar");
            foreach (string section in new[] { "grammar", "semantics" })
                if (!JsonNode.DeepEquals(Section(previous, oldManifest, section),
                        Section(current, newManifest, section)))
                    Add(DdfLanguageCompatibilityLevel.Review, section,
                        "La sezione è cambiata: verificare l'equivalenza semantica con test mirati.");
            string oldEbnfPath = Value(oldGrammar, "ebnfFile");
            string newEbnfPath = Value(newGrammar, "ebnfFile");
            if (!string.Equals(Document(previous, oldEbnfPath),
                    Document(current, newEbnfPath), StringComparison.Ordinal))
                Add(DdfLanguageCompatibilityLevel.Review, "grammar",
                    "La grammatica EBNF è cambiata: verificare le regole e i sorgenti esistenti.");
            foreach (string section in new[] { "formatting", "intellisense" })
                if (!JsonNode.DeepEquals(Section(previous, oldManifest, section),
                        Section(current, newManifest, section)))
                    Add(DdfLanguageCompatibilityLevel.Information, section,
                        "La configurazione editor è cambiata.");
            if (!JsonNode.DeepEquals(Section(previous, oldManifest, "tests"),
                    Section(current, newManifest, "tests")))
                Add(DdfLanguageCompatibilityLevel.Information, "tests",
                    "La suite dichiarativa è cambiata.");
            var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "manifest.json", oldEbnfPath, newEbnfPath
            };
            foreach (JsonObject manifest in new[] { oldManifest, newManifest })
                foreach (JsonNode path in manifest["sections"]?.AsObject().Select(item =>
                    item.Value) ?? Enumerable.Empty<JsonNode>())
                    if (path != null) knownPaths.Add(path.GetValue<string>());
            foreach (string path in previous.Documents.Keys.Union(current.Documents.Keys,
                StringComparer.OrdinalIgnoreCase).Where(path => !knownPaths.Contains(path)))
                if (!string.Equals(Document(previous, path), Document(current, path),
                        StringComparison.Ordinal))
                    Add(DdfLanguageCompatibilityLevel.Review, path,
                        "Documento non classificato modificato: revisione manuale necessaria.");

            return new DdfLanguagePackageCompatibilityReport(oldVersion, newVersion,
                findings);

            void Add(DdfLanguageCompatibilityLevel level, string section, string message) =>
                findings.Add(new DdfLanguageCompatibilityFinding(level, section, message));

            void CompareScalar(string section, string key, JsonObject before,
                JsonObject after, DdfLanguageCompatibilityLevel level)
            {
                if (!JsonNode.DeepEquals(before[key], after[key]))
                    Add(level, section, key + " è cambiato da '" + Value(before, key) +
                        "' a '" + Value(after, key) + "'.");
            }

            void CompareSet(string section, string key, JsonObject before, JsonObject after)
            {
                string[] oldItems = Strings(before[key]);
                string[] newItems = Strings(after[key]);
                foreach (string removed in oldItems.Except(newItems, StringComparer.Ordinal))
                    Add(DdfLanguageCompatibilityLevel.Breaking, section,
                        key + ": rimosso '" + removed + "'.");
                foreach (string added in newItems.Except(oldItems, StringComparer.Ordinal))
                    Add(DdfLanguageCompatibilityLevel.Information, section,
                        key + ": aggiunto '" + added + "'.");
            }

            void CompareNamedArray(string section, string key, string identity,
                JsonObject before, JsonObject after,
                DdfLanguageCompatibilityLevel additionLevel)
            {
                var oldItems = Named(before[key], identity);
                var newItems = Named(after[key], identity);
                foreach (KeyValuePair<string, JsonObject> item in oldItems)
                {
                    if (!newItems.TryGetValue(item.Key, out JsonObject replacement))
                        Add(DdfLanguageCompatibilityLevel.Breaking, section,
                            key + ": rimosso '" + item.Key + "'.");
                    else if (ContractChanged(section, key, item.Value, replacement))
                        Add(DdfLanguageCompatibilityLevel.Breaking, section,
                            key + ": contratto modificato per '" + item.Key + "'.");
                }
                foreach (string added in newItems.Keys.Except(oldItems.Keys,
                    StringComparer.Ordinal))
                    Add(additionLevel, section, key + ": aggiunto '" + added + "'.");
            }
        }

        private static JsonObject Section(DdfLanguageAuthoringProject project,
            JsonObject manifest, string name)
        {
            string path = manifest["sections"]?[name]?.GetValue<string>() ??
                throw new InvalidOperationException("Sezione mancante: " + name);
            return Read(project, path);
        }

        private static JsonObject Read(DdfLanguageAuthoringProject project, string path) =>
            JsonNode.Parse(Document(project, path)).AsObject();

        private static string Document(DdfLanguageAuthoringProject project,
            string path) => project.Documents.TryGetValue(path, out DdfLanguageAuthoringDocument document)
                ? document.Content : string.Empty;

        private static string Value(JsonObject value, string key) =>
            value[key]?.ToString() ?? string.Empty;

        private static string[] Strings(JsonNode node) =>
            (node as JsonArray ?? new JsonArray()).Select(item => item?.ToString() ?? "")
                .ToArray();

        private static Dictionary<string, JsonObject> Named(JsonNode node,
            string identity) => (node as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .ToDictionary(item => Value(item, identity), item => item,
                    StringComparer.Ordinal);

        private static bool ContractChanged(string section, string collection,
            JsonObject before, JsonObject after)
        {
            string[] keys = section == "standardLibrary"
                ? new[] { "returnType", "parameterTypes", "binding", "effect", "errorCodes" }
                : section == "lexicon" && collection == "keywords"
                    ? new[] { "tokenKind", "role" }
                    : section == "lexicon" && collection == "operators"
                        ? new[] { "binaryPrecedence", "associativity", "prefixPrecedence",
                            "postfixPrecedence", "role" }
                        : section == "types"
                            ? new[] { "category", "canDeclareVariable", "indexedElementType" }
                            : Array.Empty<string>();
            return keys.Any(key => !JsonNode.DeepEquals(before[key], after[key]));
        }
    }
}
