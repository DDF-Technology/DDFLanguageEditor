using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfEditorBuildMode
    {
        Validate,
        Build
    }

    public enum DdfEditorBuildState
    {
        Idle,
        Building
    }

    public sealed class DdfEditorBuildRequest
    {
        public DdfEditorBuildRequest(string entryPoint = null,
            string entryDocumentId = null, int maxInstructions = 0)
        {
            EntryPoint = entryPoint;
            EntryDocumentId = entryDocumentId;
            MaxInstructions = maxInstructions;
        }

        public string EntryPoint { get; }
        public string EntryDocumentId { get; }
        public int MaxInstructions { get; }
    }

    public sealed class DdfEditorBuildDocumentSnapshot
    {
        internal DdfEditorBuildDocumentSnapshot(string id, string displayName,
            string filePath, string sourceText, long version)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            Version = version;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string FilePath { get; }
        public string SourceText { get; }
        public long Version { get; }
    }

    public sealed class DdfEditorBuildSnapshot
    {
        internal DdfEditorBuildSnapshot(long generation, DdfEditorBuildMode mode,
            DateTimeOffset startedAt, DateTimeOffset completedAt,
            string languagePackageId, string languagePackageVersion,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfBuildResult result)
        {
            Generation = generation;
            Mode = mode;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            LanguagePackageId = languagePackageId ?? string.Empty;
            LanguagePackageVersion = languagePackageVersion ?? string.Empty;
            Documents = documents ?? Array.Empty<DdfEditorBuildDocumentSnapshot>();
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public long Generation { get; }
        public DdfEditorBuildMode Mode { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset CompletedAt { get; }
        public string LanguagePackageId { get; }
        public string LanguagePackageVersion { get; }
        public IReadOnlyList<DdfEditorBuildDocumentSnapshot> Documents { get; }
        public DdfBuildResult Result { get; }
        public IReadOnlyList<DdfBuildIssue> Issues => Result.Issues;
        public DdfBuildArtifact Artifact => Result.Artifact;
        public bool Succeeded => Result.Success;
    }

    public sealed class DdfEditorBuildStateChangedEventArgs : EventArgs
    {
        public DdfEditorBuildStateChangedEventArgs(long generation,
            DdfEditorBuildState previousState, DdfEditorBuildState state)
        {
            Generation = generation;
            PreviousState = previousState;
            State = state;
        }

        public long Generation { get; }
        public DdfEditorBuildState PreviousState { get; }
        public DdfEditorBuildState State { get; }
    }

    public sealed class DdfEditorBuildCompletedEventArgs : EventArgs
    {
        public DdfEditorBuildCompletedEventArgs(DdfEditorBuildSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public DdfEditorBuildSnapshot Snapshot { get; }
    }

    public interface IDdfEditorBuildService
    {
        DdfEditorBuildState State { get; }
        bool IsBuilding { get; }
        long Generation { get; }
        DdfEditorBuildSnapshot LastSnapshot { get; }

        event EventHandler<DdfEditorBuildStateChangedEventArgs> StateChanged;
        event EventHandler<DdfEditorBuildCompletedEventArgs> BuildCompleted;

        DdfEditorBuildSnapshot Validate(DdfEditorBuildRequest request = null);
        DdfEditorBuildSnapshot Build(DdfEditorBuildRequest request = null);
    }

    /// <summary>
    /// Permette a un host di collegare al servizio editor un proprio percorso di
    /// build (per esempio manifest, sorgenti su disco e artefatti), conservando
    /// snapshot, stato ed eventi del contratto pubblico.
    /// </summary>
    public delegate DdfBuildResult DdfEditorBuildHandler(
        DdfEditorBuildMode mode, DdfEditorBuildRequest request,
        IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
        DdfLanguagePackage languagePackage,
        DdfHostCatalogSnapshot hostCatalog);

    /// <summary>
    /// Builds immutable in-memory document snapshots and never reads or writes
    /// source files or artifacts on behalf of the embedding host.
    /// </summary>
    public sealed class DdfEditorBuildService : IDdfEditorBuildService
    {
        private readonly object syncRoot = new object();
        private readonly Func<IDdfEditorDocumentService> documentServiceProvider;
        private readonly Func<IDdfLanguagePackageSession> languageSessionProvider;
        private readonly Func<DdfHostCatalogSnapshot> hostCatalogProvider;
        private readonly DdfEditorBuildHandler buildHandler;
        private DdfEditorBuildState state;
        private long generation;

        public DdfEditorBuildService(IDdfEditorDocumentService documentService,
            IDdfLanguagePackageSession languageSession)
            : this(() => documentService, () => languageSession, null, null)
        {
            if (documentService == null) throw new ArgumentNullException(nameof(documentService));
            if (languageSession == null) throw new ArgumentNullException(nameof(languageSession));
        }

        public DdfEditorBuildService(
            Func<IDdfEditorDocumentService> documentServiceProvider,
            Func<IDdfLanguagePackageSession> languageSessionProvider,
            Func<DdfHostCatalogSnapshot> hostCatalogProvider = null,
            DdfEditorBuildHandler buildHandler = null)
        {
            this.documentServiceProvider = documentServiceProvider ??
                throw new ArgumentNullException(nameof(documentServiceProvider));
            this.languageSessionProvider = languageSessionProvider ??
                throw new ArgumentNullException(nameof(languageSessionProvider));
            this.hostCatalogProvider = hostCatalogProvider;
            this.buildHandler = buildHandler;
        }

        public DdfEditorBuildState State
        {
            get { lock (syncRoot) return state; }
        }

        public bool IsBuilding => State == DdfEditorBuildState.Building;

        public long Generation
        {
            get { lock (syncRoot) return generation; }
        }

        public DdfEditorBuildSnapshot LastSnapshot { get; private set; }

        public event EventHandler<DdfEditorBuildStateChangedEventArgs> StateChanged;
        public event EventHandler<DdfEditorBuildCompletedEventArgs> BuildCompleted;

        public DdfEditorBuildSnapshot Validate(DdfEditorBuildRequest request = null) =>
            Execute(DdfEditorBuildMode.Validate, request ?? new DdfEditorBuildRequest());

        public DdfEditorBuildSnapshot Build(DdfEditorBuildRequest request = null) =>
            Execute(DdfEditorBuildMode.Build, request ?? new DdfEditorBuildRequest());

        private DdfEditorBuildSnapshot Execute(DdfEditorBuildMode mode,
            DdfEditorBuildRequest request)
        {
            long currentGeneration;
            lock (syncRoot)
            {
                if (state != DdfEditorBuildState.Idle)
                    throw new InvalidOperationException("Una build host è già attiva.");
                currentGeneration = ++generation;
                state = DdfEditorBuildState.Building;
            }
            StateChanged?.Invoke(this, new DdfEditorBuildStateChangedEventArgs(
                currentGeneration, DdfEditorBuildState.Idle,
                DdfEditorBuildState.Building));

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents =
                Array.Empty<DdfEditorBuildDocumentSnapshot>();
            DdfLanguagePackage package = null;
            DdfBuildResult result;
            try
            {
                documents = CaptureDocuments();
                package = languageSessionProvider()?.ActivePackage;
                DdfHostCatalogSnapshot hostCatalog = hostCatalogProvider?.Invoke();
                if (buildHandler != null)
                    result = buildHandler(mode, request, documents, package,
                        hostCatalog) ?? DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                            "DDFH004", "L'esecutore di build host non ha restituito un risultato."));
                else if (package == null)
                    result = new DdfBuildResult(null, new[] { DdfBuildIssue.Configuration(
                        "DDFH002", "Nessun pacchetto linguaggio associato alla sessione host.") });
                else if (documents.Count == 0)
                    result = new DdfBuildResult(null, new[] { DdfBuildIssue.Configuration(
                        "DDFH001", "Nessun documento fornito dall'applicazione host.") });
                else
                    result = DdfEditorHostBuildPipeline.Build(documents, package,
                        request, out _, hostCatalog);
            }
            catch (Exception exception)
            {
                result = new DdfBuildResult(null, new[] { DdfBuildIssue.Configuration(
                    "DDFH003", "Errore interno della build host: " + exception.Message) });
            }

            var snapshot = new DdfEditorBuildSnapshot(currentGeneration, mode,
                startedAt, DateTimeOffset.UtcNow, package?.Id,
                package?.PackageVersion, documents, result);
            lock (syncRoot)
            {
                LastSnapshot = snapshot;
                state = DdfEditorBuildState.Idle;
            }
            BuildCompleted?.Invoke(this,
                new DdfEditorBuildCompletedEventArgs(snapshot));
            StateChanged?.Invoke(this, new DdfEditorBuildStateChangedEventArgs(
                currentGeneration, DdfEditorBuildState.Building,
                DdfEditorBuildState.Idle));
            return snapshot;
        }

        private IReadOnlyList<DdfEditorBuildDocumentSnapshot> CaptureDocuments()
        {
            IReadOnlyList<IDdfEditorDocument> documents =
                documentServiceProvider()?.OpenDocuments;
            if (documents == null) return Array.Empty<DdfEditorBuildDocumentSnapshot>();
            return documents.Select(document => new DdfEditorBuildDocumentSnapshot(
                document.Id, document.DisplayName, document.FilePath,
                document.SourceText, document.Version)).ToList().AsReadOnly();
        }
    }

    internal static class DdfEditorHostBuildPipeline
    {
        internal static DdfBuildResult Build(
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfEditorBuildRequest request,
            out IReadOnlyDictionary<string, string> runtimePathsById,
            DdfHostCatalogSnapshot hostCatalog = null)
        {
            string logicalRoot = Path.Combine(Path.GetTempPath(),
                "DDFLanguageEditor.HostDocuments");
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namesById = new Dictionary<string, string>(StringComparer.Ordinal);
            var pathsById = new Dictionary<string, string>(StringComparer.Ordinal);
            var sources = new List<DdfBuildSource>();
            for (int index = 0; index < documents.Count; index++)
            {
                DdfEditorBuildDocumentSnapshot document = documents[index];
                string name = CreateUniqueName(document.DisplayName, index,
                    package.FileExtensions.FirstOrDefault(), usedNames);
                namesById[document.Id] = name;
                string logicalPath = string.IsNullOrWhiteSpace(document.FilePath)
                    ? Path.Combine(logicalRoot, name)
                    : document.FilePath;
                pathsById[document.Id] = logicalPath;
                sources.Add(new DdfBuildSource(logicalPath, name,
                    document.SourceText, true));
            }

            string entrySource = null;
            if (!string.IsNullOrWhiteSpace(request.EntryDocumentId))
                namesById.TryGetValue(request.EntryDocumentId, out entrySource);
            var manifest = DdfProjectManifest.CreateImplicit("DDF host session",
                package, entrySource);
            manifest.Sources = sources.Select(item => item.RelativePath).ToList();
            if (!string.IsNullOrWhiteSpace(request.EntryPoint))
                manifest.EntryPoint = request.EntryPoint;
            if (request.MaxInstructions > 0)
                manifest.Options.MaxInstructions = request.MaxInstructions;
            runtimePathsById = pathsById;
            return new DdfVmBuildBackend().Build(logicalRoot, manifest, package,
                sources.AsReadOnly(), hostCatalog);
        }

        private static string CreateUniqueName(string displayName, int index,
            string preferredExtension, ISet<string> usedNames)
        {
            string name = Path.GetFileName(displayName ?? string.Empty);
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            if (string.IsNullOrWhiteSpace(name)) name = "document-" + (index + 1);
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
                name += string.IsNullOrWhiteSpace(preferredExtension)
                    ? ".ddf" : preferredExtension;
            string candidate = name;
            int suffix = 2;
            while (!usedNames.Add(candidate))
                candidate = Path.GetFileNameWithoutExtension(name) + "-" +
                            suffix++ + Path.GetExtension(name);
            return candidate;
        }
    }
}
