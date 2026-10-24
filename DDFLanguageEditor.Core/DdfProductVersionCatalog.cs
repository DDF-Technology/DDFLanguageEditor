using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfProductComponentVersion
    {
        internal DdfProductComponentVersion(string id, string version,
            string assemblyVersion, string fileVersion, string informationalVersion,
            string packageId, string packageVersion)
        {
            Id = id;
            Version = version;
            AssemblyVersion = assemblyVersion;
            FileVersion = fileVersion;
            InformationalVersion = informationalVersion;
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        public string Id { get; }
        public string Version { get; }
        public string AssemblyVersion { get; }
        public string FileVersion { get; }
        public string InformationalVersion { get; }
        public string PackageId { get; }
        public string PackageVersion { get; }
    }

    public sealed class DdfProductVersionIdentity
    {
        internal DdfProductVersionIdentity(string schemaVersion, string productId,
            string repository, string currentVersion, string stability,
            string displayVersion, string defaultChannel, string sdkContractVersion,
            string legacySdkContractVersion, DdfProductComponentVersion standalone,
            IReadOnlyList<DdfProductComponentVersion> embeddedComponents,
            IReadOnlyDictionary<string, string> persistenceVersions)
        {
            SchemaVersion = schemaVersion;
            ProductId = productId;
            Repository = repository;
            CurrentVersion = currentVersion;
            Stability = stability;
            DisplayVersion = displayVersion;
            DefaultChannel = defaultChannel;
            SdkContractVersion = sdkContractVersion;
            LegacySdkContractVersion = legacySdkContractVersion;
            Standalone = standalone;
            EmbeddedComponents = embeddedComponents;
            PersistenceVersions = persistenceVersions;
        }

        public string SchemaVersion { get; }
        public string ProductId { get; }
        public string Repository { get; }
        public string CurrentVersion { get; }
        public string Stability { get; }
        public string DisplayVersion { get; }
        public string DefaultChannel { get; }
        public string SdkContractVersion { get; }
        public string LegacySdkContractVersion { get; }
        public DdfProductComponentVersion Standalone { get; }
        public IReadOnlyList<DdfProductComponentVersion> EmbeddedComponents { get; }
        public IReadOnlyDictionary<string, string> PersistenceVersions { get; }
    }

    public static class DdfProductVersionCatalog
    {
        public const string EmbeddedResourceName =
            "DDFLanguageEditor.Core.Versioning.ddf-version-matrix.json";

        private static readonly Lazy<DdfProductVersionIdentity> current =
            new Lazy<DdfProductVersionIdentity>(LoadEmbedded,
                isThreadSafe: true);

        public static DdfProductVersionIdentity Current => current.Value;

        public static DdfProductVersionIdentity Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("La matrice versioni è vuota.");

            JsonObject root;
            try
            {
                root = JsonNode.Parse(json)?.AsObject() ??
                    throw new InvalidDataException("La matrice versioni non è un oggetto JSON.");
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                throw new InvalidDataException("La matrice versioni non è valida.", exception);
            }

            string schemaVersion = RequiredString(root, "schemaVersion");
            JsonObject product = RequiredObject(root, "product");
            JsonObject update = RequiredObject(root, "productUpdate");
            JsonObject editorSdk = RequiredObject(root, "editorSdk");
            JsonObject persistence = RequiredObject(root, "persistence");
            JsonArray components = root["components"]?.AsArray() ??
                throw new InvalidDataException("La matrice non dichiara components.");

            var componentMap = new Dictionary<string, DdfProductComponentVersion>(
                StringComparer.Ordinal);
            foreach (JsonNode node in components)
            {
                JsonObject item = node?.AsObject() ??
                    throw new InvalidDataException("Componente versione non valido.");
                string id = RequiredString(item, "id");
                if (!componentMap.TryAdd(id, new DdfProductComponentVersion(id,
                    RequiredString(item, "version"),
                    RequiredString(item, "assemblyVersion"),
                    RequiredString(item, "fileVersion"),
                    RequiredString(item, "informationalVersion"),
                    OptionalString(item, "packageId"),
                    OptionalString(item, "packageVersion"))))
                {
                    throw new InvalidDataException("Componente versione duplicato: " + id);
                }
            }

            string standaloneId = RequiredString(update, "standaloneComponentId");
            if (!componentMap.TryGetValue(standaloneId, out DdfProductComponentVersion standalone))
                throw new InvalidDataException("Componente standalone sconosciuto: " + standaloneId);

            JsonArray embeddedIds = update["embeddedComponentIds"]?.AsArray() ??
                throw new InvalidDataException("La matrice non dichiara embeddedComponentIds.");
            var embedded = new List<DdfProductComponentVersion>();
            foreach (JsonNode node in embeddedIds)
            {
                string id = node?.GetValue<string>() ?? string.Empty;
                if (!componentMap.TryGetValue(id, out DdfProductComponentVersion component))
                    throw new InvalidDataException("Componente embedded sconosciuto: " + id);
                if (embedded.Any(existing => string.Equals(existing.Id, id,
                    StringComparison.Ordinal)))
                {
                    throw new InvalidDataException("Componente embedded duplicato: " + id);
                }
                embedded.Add(component);
            }
            if (embedded.Count == 0)
                throw new InvalidDataException("La matrice non dichiara componenti embedded.");

            string currentVersion = RequiredString(product, "version");
            if (!string.Equals(standalone.Version, currentVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Versione prodotto e standalone divergenti.");

            var persistenceVersions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonNode> entry in persistence)
                persistenceVersions.Add(entry.Key, entry.Value?.GetValue<string>() ?? string.Empty);

            return new DdfProductVersionIdentity(schemaVersion,
                RequiredString(update, "productId"), RequiredString(update, "repository"),
                currentVersion, RequiredString(product, "stability"),
                RequiredString(product, "displayVersion"),
                RequiredString(update, "defaultChannel"),
                RequiredString(editorSdk, "contractVersion"),
                RequiredString(editorSdk, "legacyContractVersion"), standalone,
                new ReadOnlyCollection<DdfProductComponentVersion>(embedded),
                new ReadOnlyDictionary<string, string>(persistenceVersions));
        }

        private static DdfProductVersionIdentity LoadEmbedded()
        {
            Assembly assembly = typeof(DdfProductVersionCatalog).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(EmbeddedResourceName) ??
                throw new InvalidDataException("Matrice versioni incorporata non trovata.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }

        private static JsonObject RequiredObject(JsonObject parent, string name) =>
            parent[name]?.AsObject() ??
            throw new InvalidDataException("Sezione obbligatoria mancante: " + name);

        private static string RequiredString(JsonObject parent, string name)
        {
            string value = OptionalString(parent, name);
            if (value.Length == 0)
                throw new InvalidDataException("Valore obbligatorio mancante: " + name);
            return value;
        }

        private static string OptionalString(JsonObject parent, string name) =>
            parent[name]?.GetValue<string>() ?? string.Empty;
    }
}
