using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Portable metadata for a library/provider. Signatures, effects and errors
    /// live in the existing DdfExternalApiDefinition identified by its digest.
    /// This descriptor is not a trust decision or a loader.
    /// </summary>
    public sealed class DdfExternalLibraryDescriptor
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumBytes = 65536;
        private static readonly Regex IdPattern = new(
            "^[a-z][a-z0-9.-]*$", RegexOptions.CultureInvariant);
        private static readonly Regex NamespacePattern = new(
            "^[A-Za-z][A-Za-z0-9]*(\\.[A-Za-z][A-Za-z0-9]*)*$",
            RegexOptions.CultureInvariant);
        private static readonly Regex HashPattern = new(
            "^[a-f0-9]{64}$", RegexOptions.CultureInvariant);
        private static readonly string[] PortabilityValues =
            { "portable", "dotnet", "platform", "host" };
        private static readonly string[] ArchitectureValues =
            { "any", "x86", "x64", "arm64" };

        public DdfExternalLibraryDescriptor(string id, string version,
            string ddfNamespace, string apiFingerprint, string portability,
            string license, string documentation,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> dependencies = null,
            IEnumerable<string> runtimeIdentifiers = null,
            IEnumerable<string> architectures = null)
        {
            Id = Require(id, IdPattern, nameof(id));
            Version = RequireVersion(version);
            Namespace = Require(ddfNamespace, NamespacePattern,
                nameof(ddfNamespace));
            ApiFingerprint = Require(apiFingerprint, HashPattern,
                nameof(apiFingerprint));
            Portability = RequireChoice(portability, PortabilityValues,
                nameof(portability));
            License = RequireText(license, nameof(license), 256);
            Documentation = RequireText(documentation, nameof(documentation),
                2048);
            Capabilities = Copy(capabilities, value => Require(value, IdPattern,
                nameof(capabilities)), nameof(capabilities));
            Dependencies = Copy(dependencies, value => Require(value, IdPattern,
                nameof(dependencies)), nameof(dependencies));
            RuntimeIdentifiers = Copy(runtimeIdentifiers, value =>
            {
                if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
                    !Regex.IsMatch(value, "^[a-z0-9][a-z0-9.-]*$"))
                    throw new ArgumentException("RID non valido.",
                        nameof(runtimeIdentifiers));
                return value;
            }, nameof(runtimeIdentifiers));
            Architectures = Copy(architectures, value => RequireChoice(value,
                ArchitectureValues, nameof(architectures)),
                nameof(architectures));
            if (Dependencies.Contains(Id, StringComparer.Ordinal) ||
                (Architectures.Contains("any", StringComparer.Ordinal) &&
                 Architectures.Count > 1))
                throw new ArgumentException("Dipendenze o architetture incoerenti.");
            if (Portability == "portable" &&
                (RuntimeIdentifiers.Count != 0 || Architectures.Count != 0))
                throw new ArgumentException("Una libreria portable non vincola RID o architettura.");
            if (Portability == "platform" &&
                RuntimeIdentifiers.Count == 0 && Architectures.Count == 0)
                throw new ArgumentException("Una libreria platform richiede RID o architettura.");
        }

        public string Id { get; }
        public string Version { get; }
        public string Namespace { get; }
        public string ApiFingerprint { get; }
        public string Portability { get; }
        public string License { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<string> RuntimeIdentifiers { get; }
        public IReadOnlyList<string> Architectures { get; }

        public bool Matches(DdfExternalApiDefinition api) => api != null &&
            api.ReferenceId == Id && api.ReferenceVersion == Version &&
            api.Fingerprint == ApiFingerprint;

        public string ToJson()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("id", Id);
                writer.WriteString("version", Version);
                writer.WriteString("namespace", Namespace);
                writer.WriteString("apiFingerprint", ApiFingerprint);
                writer.WriteString("portability", Portability);
                writer.WriteString("license", License);
                writer.WriteString("documentation", Documentation);
                WriteArray(writer, "capabilities", Capabilities);
                WriteArray(writer, "dependencies", Dependencies);
                WriteArray(writer, "runtimeIdentifiers", RuntimeIdentifiers);
                WriteArray(writer, "architectures", Architectures);
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumBytes)
                throw new ArgumentException("Descrittore oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static DdfExternalLibraryDescriptor Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new FormatException("Descrittore assente o oltre quota.");
            using JsonDocument document = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = 8,
                    CommentHandling = JsonCommentHandling.Disallow,
                    AllowTrailingCommas = false });
            JsonElement root = document.RootElement;
            string[] fields = { "schemaVersion", "id", "version", "namespace",
                "apiFingerprint", "portability", "license", "documentation",
                "capabilities", "dependencies", "runtimeIdentifiers",
                "architectures" };
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != fields.Length ||
                root.EnumerateObject().Select(p => p.Name)
                    .Distinct(StringComparer.Ordinal).Count() != fields.Length ||
                fields.Any(field => !root.TryGetProperty(field, out _)) ||
                Read(root, "schemaVersion") != SchemaVersion)
                throw new FormatException("Schema del descrittore non valido.");
            try
            {
                return new DdfExternalLibraryDescriptor(Read(root, "id"),
                    Read(root, "version"), Read(root, "namespace"),
                    Read(root, "apiFingerprint"), Read(root, "portability"),
                    Read(root, "license"), Read(root, "documentation"),
                    ReadArray(root, "capabilities"),
                    ReadArray(root, "dependencies"),
                    ReadArray(root, "runtimeIdentifiers"),
                    ReadArray(root, "architectures"));
            }
            catch (ArgumentException ex)
            {
                throw new FormatException("Descrittore non valido.", ex);
            }
        }

        private static string Read(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String)
                throw new FormatException("Campo non testuale: " + name + ".");
            return value.GetString();
        }

        private static IReadOnlyList<string> ReadArray(JsonElement root,
            string name)
        {
            JsonElement array = root.GetProperty(name);
            if (array.ValueKind != JsonValueKind.Array ||
                array.GetArrayLength() > 64)
                throw new FormatException("Elenco non valido: " + name + ".");
            return array.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.String)
                    throw new FormatException("Elemento non testuale: " + name + ".");
                return item.GetString();
            }).ToArray();
        }

        private static void WriteArray(Utf8JsonWriter writer, string name,
            IEnumerable<string> values)
        {
            writer.WriteStartArray(name);
            foreach (string value in values) writer.WriteStringValue(value);
            writer.WriteEndArray();
        }

        private static IReadOnlyList<string> Copy(IEnumerable<string> values,
            Func<string, string> validate, string name)
        {
            string[] items = (values ?? Array.Empty<string>()).Select(validate)
                .ToArray();
            if (items.Length > 64 ||
                items.Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("Elenco non valido o duplicato.", name);
            return new ReadOnlyCollection<string>(items.OrderBy(value => value,
                StringComparer.Ordinal).ToArray());
        }

        private static string RequireVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
                !System.Version.TryParse(value, out _))
                throw new ArgumentException("Versione non valida.",
                    nameof(value));
            return value;
        }

        private static string RequireChoice(string value, string[] choices,
            string name)
        {
            if (!choices.Contains(value, StringComparer.Ordinal))
                throw new ArgumentException("Valore non valido.", name);
            return value;
        }

        private static string Require(string value, Regex pattern, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
                !pattern.IsMatch(value))
                throw new ArgumentException("Identificatore non valido.", name);
            return value;
        }

        private static string RequireText(string value, string name, int limit)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > limit ||
                value.Any(char.IsControl))
                throw new ArgumentException("Testo non valido.", name);
            return value;
        }
    }
}
