using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Identifies a persisted DDF artifact without coupling migration code to
    /// the editor UI or to a particular serialization format.
    /// </summary>
    public enum DdfPersistenceArtifactKind
    {
        EditorState,
        WorkspaceSession,
        HotExitSession,
        CrashRecoverySession,
        Breakpoints,
        WatchExpressions,
        PaletteLayout,
        ToolbarLayout,
        ScriptState,
        ProjectManifest
    }

    public enum DdfPersistenceInspectionStatus
    {
        Current,
        MigrationRequired,
        UnsupportedVersion,
        ArtifactKindMismatch
    }

    /// <summary>
    /// Immutable serialized artifact. Payload interpretation remains the
    /// responsibility of the domain-specific serializer or host migrator.
    /// </summary>
    public sealed class DdfPersistenceArtifact
    {
        public DdfPersistenceArtifact(DdfPersistenceArtifactKind kind,
            string schemaVersion, string payload)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion))
                throw new ArgumentException("La versione dello schema è obbligatoria.",
                    nameof(schemaVersion));
            Kind = kind;
            SchemaVersion = schemaVersion.Trim();
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public DdfPersistenceArtifactKind Kind { get; }
        public string SchemaVersion { get; }
        public string Payload { get; }
    }

    public sealed class DdfPersistenceInspection
    {
        internal DdfPersistenceInspection(DdfPersistenceArtifact artifact,
            string targetSchemaVersion, DdfPersistenceInspectionStatus status,
            string message)
        {
            Artifact = artifact;
            TargetSchemaVersion = targetSchemaVersion;
            Status = status;
            Message = message;
        }

        public DdfPersistenceArtifact Artifact { get; }
        public string SourceSchemaVersion => Artifact.SchemaVersion;
        public string TargetSchemaVersion { get; }
        public DdfPersistenceInspectionStatus Status { get; }
        public string Message { get; }
        public bool CanLoad => Status == DdfPersistenceInspectionStatus.Current;
        public bool CanMigrate => Status == DdfPersistenceInspectionStatus.MigrationRequired;
    }

    /// <summary>
    /// Optional host/domain adapter. It must build a new artifact and must not
    /// modify the supplied readable source snapshot.
    /// </summary>
    public interface IDdfPersistenceMigrationProvider
    {
        DdfPersistenceArtifact Migrate(DdfPersistenceArtifact source,
            DdfPersistenceSchemaContract targetContract);
    }

    public sealed class DdfPersistenceMigrationResult
    {
        internal DdfPersistenceMigrationResult(DdfPersistenceArtifact original,
            DdfPersistenceArtifact prepared, DdfPersistenceInspection inspection,
            string failure)
        {
            Original = original;
            Prepared = prepared;
            Inspection = inspection;
            Failure = failure;
        }

        /// <summary>The exact snapshot supplied by the caller.</summary>
        public DdfPersistenceArtifact Original { get; }

        /// <summary>
        /// Artifact safe to apply atomically. Null when migration was rejected.
        /// </summary>
        public DdfPersistenceArtifact Prepared { get; }
        public DdfPersistenceInspection Inspection { get; }
        public string Failure { get; }
        public bool Succeeded => Prepared != null;
    }

    /// <summary>
    /// Declares one current schema and the older readable versions that may be
    /// handed to an explicit migration provider. It never writes files or
    /// mutates the source artifact.
    /// </summary>
    public sealed class DdfPersistenceSchemaContract
    {
        private readonly HashSet<string> migratableVersions;

        public DdfPersistenceSchemaContract(DdfPersistenceArtifactKind kind,
            string currentSchemaVersion,
            IEnumerable<string> migratableVersions = null)
        {
            if (string.IsNullOrWhiteSpace(currentSchemaVersion))
                throw new ArgumentException("La versione corrente è obbligatoria.",
                    nameof(currentSchemaVersion));
            Kind = kind;
            CurrentSchemaVersion = currentSchemaVersion.Trim();
            this.migratableVersions = new HashSet<string>(
                (migratableVersions ?? Array.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Where(item => !string.Equals(item, CurrentSchemaVersion,
                        StringComparison.Ordinal)),
                StringComparer.Ordinal);
            MigratableVersions = new ReadOnlyCollection<string>(
                this.migratableVersions.OrderBy(item => item,
                    StringComparer.Ordinal).ToList());
        }

        public DdfPersistenceArtifactKind Kind { get; }
        public string CurrentSchemaVersion { get; }
        public IReadOnlyList<string> MigratableVersions { get; }

        public DdfPersistenceInspection Inspect(DdfPersistenceArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (artifact.Kind != Kind)
                return new DdfPersistenceInspection(artifact, CurrentSchemaVersion,
                    DdfPersistenceInspectionStatus.ArtifactKindMismatch,
                    "Il tipo di artefatto non corrisponde al contratto richiesto.");
            if (string.Equals(artifact.SchemaVersion, CurrentSchemaVersion,
                    StringComparison.Ordinal))
                return new DdfPersistenceInspection(artifact, CurrentSchemaVersion,
                    DdfPersistenceInspectionStatus.Current,
                    "Lo snapshot usa lo schema corrente.");
            if (migratableVersions.Contains(artifact.SchemaVersion))
                return new DdfPersistenceInspection(artifact, CurrentSchemaVersion,
                    DdfPersistenceInspectionStatus.MigrationRequired,
                    "Lo snapshot è leggibile ma richiede una migrazione esplicita.");
            return new DdfPersistenceInspection(artifact, CurrentSchemaVersion,
                DdfPersistenceInspectionStatus.UnsupportedVersion,
                "La versione dello snapshot non è supportata.");
        }

        public DdfPersistenceMigrationResult Prepare(
            DdfPersistenceArtifact artifact,
            IDdfPersistenceMigrationProvider migrationProvider = null)
        {
            DdfPersistenceInspection inspection = Inspect(artifact);
            if (inspection.Status == DdfPersistenceInspectionStatus.Current)
                return new DdfPersistenceMigrationResult(artifact, artifact,
                    inspection, null);
            if (!inspection.CanMigrate)
                return new DdfPersistenceMigrationResult(artifact, null,
                    inspection, inspection.Message);
            if (migrationProvider == null)
                return new DdfPersistenceMigrationResult(artifact, null,
                    inspection, "Nessun provider di migrazione è stato fornito.");

            try
            {
                DdfPersistenceArtifact migrated = migrationProvider.Migrate(
                    artifact, this);
                if (migrated == null)
                    return Failed(artifact, inspection,
                        "Il provider di migrazione non ha restituito uno snapshot.");
                if (migrated.Kind != Kind)
                    return Failed(artifact, inspection,
                        "Il provider ha restituito un tipo di artefatto differente.");
                if (!string.Equals(migrated.SchemaVersion,
                        CurrentSchemaVersion, StringComparison.Ordinal))
                    return Failed(artifact, inspection,
                        "Il provider non ha prodotto la versione schema richiesta.");
                return new DdfPersistenceMigrationResult(artifact, migrated,
                    inspection, null);
            }
            catch (Exception exception)
            {
                return Failed(artifact, inspection,
                    "Migrazione rifiutata: " + exception.Message);
            }
        }

        private static DdfPersistenceMigrationResult Failed(
            DdfPersistenceArtifact artifact,
            DdfPersistenceInspection inspection, string failure) =>
            new DdfPersistenceMigrationResult(artifact, null, inspection,
                failure);
    }

    /// <summary>
    /// Authoritative inventory for persisted domains. Declaring a version as
    /// readable does not install an implicit migration: a provider is still
    /// required before the snapshot can be applied.
    /// </summary>
    public static class DdfPersistenceSchemaCatalog
    {
        public const string EditorStateVersion = "1.0";
        public const string WorkspaceSessionVersion = "1.0";
        public const string HotExitSessionVersion = "1.0";
        public const string CrashRecoverySessionVersion = "1.0";
        public const string BreakpointsVersion = "1.0";
        public const string WatchExpressionsVersion = "1.0";
        public const string PaletteLayoutVersion = "1.0";
        public const string ToolbarLayoutVersion = "1.0";
        public const string ScriptStateVersion = "1.0";
        public const string ProjectManifestVersion = "1.2";

        private static readonly IReadOnlyDictionary<DdfPersistenceArtifactKind,
            DdfPersistenceSchemaContract> contracts =
            new ReadOnlyDictionary<DdfPersistenceArtifactKind,
                DdfPersistenceSchemaContract>(new Dictionary<
                    DdfPersistenceArtifactKind, DdfPersistenceSchemaContract>
                {
                    [DdfPersistenceArtifactKind.EditorState] = Contract(
                        DdfPersistenceArtifactKind.EditorState, EditorStateVersion, "legacy"),
                    [DdfPersistenceArtifactKind.WorkspaceSession] = Contract(
                        DdfPersistenceArtifactKind.WorkspaceSession, WorkspaceSessionVersion, "legacy"),
                    [DdfPersistenceArtifactKind.HotExitSession] = Contract(
                        DdfPersistenceArtifactKind.HotExitSession, HotExitSessionVersion),
                    [DdfPersistenceArtifactKind.CrashRecoverySession] = Contract(
                        DdfPersistenceArtifactKind.CrashRecoverySession, CrashRecoverySessionVersion),
                    [DdfPersistenceArtifactKind.Breakpoints] = Contract(
                        DdfPersistenceArtifactKind.Breakpoints, BreakpointsVersion, "legacy"),
                    [DdfPersistenceArtifactKind.WatchExpressions] = Contract(
                        DdfPersistenceArtifactKind.WatchExpressions, WatchExpressionsVersion, "legacy"),
                    [DdfPersistenceArtifactKind.PaletteLayout] = Contract(
                        DdfPersistenceArtifactKind.PaletteLayout, PaletteLayoutVersion, "legacy"),
                    [DdfPersistenceArtifactKind.ToolbarLayout] = Contract(
                        DdfPersistenceArtifactKind.ToolbarLayout, ToolbarLayoutVersion, "legacy"),
                    [DdfPersistenceArtifactKind.ScriptState] = Contract(
                        DdfPersistenceArtifactKind.ScriptState, ScriptStateVersion, "0.9"),
                    [DdfPersistenceArtifactKind.ProjectManifest] = Contract(
                        DdfPersistenceArtifactKind.ProjectManifest, ProjectManifestVersion, "1.1")
                });

        public static IReadOnlyDictionary<DdfPersistenceArtifactKind,
            DdfPersistenceSchemaContract> Contracts => contracts;

        public static DdfPersistenceSchemaContract Get(
            DdfPersistenceArtifactKind kind) => contracts[kind];

        private static DdfPersistenceSchemaContract Contract(
            DdfPersistenceArtifactKind kind, string current,
            params string[] migratable) =>
            new DdfPersistenceSchemaContract(kind, current, migratable);
    }
}
