using System;
using System.Linq;
using System.Text.Json;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class DdfPersistenceReadResult<T> where T : class
    {
        internal DdfPersistenceReadResult(string originalPayload,
            DdfPersistenceArtifact originalArtifact,
            DdfPersistenceInspection inspection, T value, string failure)
        {
            OriginalPayload = originalPayload ?? string.Empty;
            OriginalArtifact = originalArtifact;
            Inspection = inspection;
            Value = value;
            Failure = failure;
        }

        public string OriginalPayload { get; }
        public DdfPersistenceArtifact OriginalArtifact { get; }
        public DdfPersistenceInspection Inspection { get; }
        public T Value { get; }
        public string Failure { get; }
        public bool Succeeded => Value != null;
        public bool WasMigrated => Succeeded && Inspection?.CanMigrate == true;

        internal static DdfPersistenceReadResult<T> Success(string payload,
            DdfPersistencePreparation preparation, T value) =>
            new DdfPersistenceReadResult<T>(payload,
                preparation?.OriginalArtifact, preparation?.Inspection,
                value ?? throw new ArgumentNullException(nameof(value)), null);

        internal static DdfPersistenceReadResult<T> Failed(string payload,
            DdfPersistencePreparation preparation, string failure) =>
            new DdfPersistenceReadResult<T>(payload,
                preparation?.OriginalArtifact, preparation?.Inspection, null,
                string.IsNullOrWhiteSpace(failure)
                    ? "Snapshot persistito non valido."
                    : failure);
    }

    internal sealed class DdfPersistencePreparation
    {
        public DdfPersistenceArtifact OriginalArtifact { get; init; }
        public DdfPersistenceInspection Inspection { get; init; }
        public DdfPersistenceArtifact PreparedArtifact { get; init; }
        public string Failure { get; init; }
        public bool Succeeded => PreparedArtifact != null;
    }

    internal static class DdfPersistenceSerialization
    {
        public static DdfPersistencePreparation PrepareJson(
            DdfPersistenceArtifactKind kind, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return Failed("Lo snapshot persistito è vuoto.");
            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return Failed("Lo snapshot persistito non contiene un oggetto JSON.");
                string version = ReadJsonVersion(document.RootElement);
                return Prepare(kind, version, payload);
            }
            catch (JsonException exception)
            {
                return Failed("JSON persistito non valido: " + exception.Message);
            }
        }

        public static DdfPersistencePreparation PrepareKeyValue(
            DdfPersistenceArtifactKind kind, string payload)
        {
            string version = (payload ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2 && string.Equals(parts[0].Trim(),
                    "schemaVersion", StringComparison.OrdinalIgnoreCase))
                .Select(parts => parts[1].Trim())
                .FirstOrDefault();
            return Prepare(kind, string.IsNullOrWhiteSpace(version)
                ? "legacy"
                : version, payload ?? string.Empty);
        }

        private static DdfPersistencePreparation Prepare(
            DdfPersistenceArtifactKind kind, string version, string payload)
        {
            var artifact = new DdfPersistenceArtifact(kind, version, payload);
            DdfPersistenceSchemaContract contract =
                DdfPersistenceSchemaCatalog.Get(kind);
            DdfPersistenceMigrationResult result = contract.Prepare(artifact,
                IdentityMigrationProvider.Instance);
            return new DdfPersistencePreparation
            {
                OriginalArtifact = artifact,
                Inspection = result.Inspection,
                PreparedArtifact = result.Prepared,
                Failure = result.Failure
            };
        }

        private static string ReadJsonVersion(JsonElement root)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, "schemaVersion",
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                if (!string.Equals(property.Name, "formatVersion",
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetInt32(out int numeric))
                    return numeric + ".0";
                if (property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
            }
            return "legacy";
        }

        private static DdfPersistencePreparation Failed(string failure) =>
            new DdfPersistencePreparation { Failure = failure };

        private sealed class IdentityMigrationProvider :
            IDdfPersistenceMigrationProvider
        {
            public static readonly IdentityMigrationProvider Instance = new();

            public DdfPersistenceArtifact Migrate(DdfPersistenceArtifact source,
                DdfPersistenceSchemaContract targetContract) =>
                new DdfPersistenceArtifact(source.Kind,
                    targetContract.CurrentSchemaVersion, source.Payload);
        }
    }
}
