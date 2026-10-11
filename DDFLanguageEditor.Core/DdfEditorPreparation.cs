using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfEditorPreparationMode
    {
        Validate,
        PrepareScript
    }

    public enum DdfEditorPreparationState
    {
        Idle,
        Preparing
    }

    public sealed class DdfEditorPreparationRequest
    {
        public DdfEditorPreparationRequest(string entryPoint = null,
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

    public sealed class DdfEditorPreparationDocumentSnapshot
    {
        internal DdfEditorPreparationDocumentSnapshot(string id, string displayName,
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

    public sealed class DdfEditorPreparationSnapshot
    {
        internal DdfEditorPreparationSnapshot(long generation,
            DdfEditorPreparationMode mode, DateTimeOffset startedAt,
            DateTimeOffset completedAt, string languagePackageId,
            string languagePackageVersion,
            IReadOnlyList<DdfEditorPreparationDocumentSnapshot> documents,
            DdfBuildResult result)
        {
            Generation = generation;
            Mode = mode;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            LanguagePackageId = languagePackageId ?? string.Empty;
            LanguagePackageVersion = languagePackageVersion ?? string.Empty;
            Documents = documents ?? Array.Empty<DdfEditorPreparationDocumentSnapshot>();
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public long Generation { get; }
        public DdfEditorPreparationMode Mode { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset CompletedAt { get; }
        public string LanguagePackageId { get; }
        public string LanguagePackageVersion { get; }
        public IReadOnlyList<DdfEditorPreparationDocumentSnapshot> Documents { get; }
        public DdfBuildResult Result { get; }
        public IReadOnlyList<DdfBuildIssue> Issues => Result.Issues;
        public DdfBuildArtifact PreparedArtifact => Result.Artifact;
        public bool Succeeded => Result.Success;
    }

    public sealed class DdfEditorPreparationStateChangedEventArgs : EventArgs
    {
        public DdfEditorPreparationStateChangedEventArgs(long generation,
            DdfEditorPreparationState previousState,
            DdfEditorPreparationState state)
        {
            Generation = generation;
            PreviousState = previousState;
            State = state;
        }

        public long Generation { get; }
        public DdfEditorPreparationState PreviousState { get; }
        public DdfEditorPreparationState State { get; }
    }

    public sealed class DdfEditorPreparationCompletedEventArgs : EventArgs
    {
        public DdfEditorPreparationCompletedEventArgs(
            DdfEditorPreparationSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public DdfEditorPreparationSnapshot Snapshot { get; }
    }

    public interface IDdfEditorPreparationService
    {
        DdfEditorPreparationState State { get; }
        bool IsPreparing { get; }
        long Generation { get; }
        DdfEditorPreparationSnapshot LastSnapshot { get; }

        event EventHandler<DdfEditorPreparationStateChangedEventArgs> StateChanged;
        event EventHandler<DdfEditorPreparationCompletedEventArgs> PreparationCompleted;

        DdfEditorPreparationSnapshot Validate(
            DdfEditorPreparationRequest request = null);
        DdfEditorPreparationSnapshot PrepareScript(
            DdfEditorPreparationRequest request = null);
    }

    public delegate DdfBuildResult DdfEditorPreparationHandler(
        DdfEditorPreparationMode mode, DdfEditorPreparationRequest request,
        IReadOnlyList<DdfEditorPreparationDocumentSnapshot> documents,
        DdfLanguagePackage languagePackage,
        DdfHostCatalogSnapshot hostCatalog);

    /// <summary>
    /// Validates or prepares immutable in-memory script snapshots. This service
    /// never invokes Roslyn and never writes compiled assemblies.
    /// </summary>
    public sealed class DdfEditorPreparationService : IDdfEditorPreparationService
    {
        private readonly object syncRoot = new object();
        private readonly Func<IDdfEditorDocumentService> documentServiceProvider;
        private readonly Func<IDdfLanguagePackageSession> languageSessionProvider;
        private readonly Func<DdfHostCatalogSnapshot> hostCatalogProvider;
        private readonly DdfEditorPreparationHandler preparationHandler;
        private DdfEditorPreparationState state;
        private long generation;

        public DdfEditorPreparationService(IDdfEditorDocumentService documentService,
            IDdfLanguagePackageSession languageSession)
            : this(() => documentService, () => languageSession, null, null)
        {
            if (documentService == null) throw new ArgumentNullException(nameof(documentService));
            if (languageSession == null) throw new ArgumentNullException(nameof(languageSession));
        }

        public DdfEditorPreparationService(
            Func<IDdfEditorDocumentService> documentServiceProvider,
            Func<IDdfLanguagePackageSession> languageSessionProvider,
            Func<DdfHostCatalogSnapshot> hostCatalogProvider = null,
            DdfEditorPreparationHandler preparationHandler = null)
        {
            this.documentServiceProvider = documentServiceProvider ??
                throw new ArgumentNullException(nameof(documentServiceProvider));
            this.languageSessionProvider = languageSessionProvider ??
                throw new ArgumentNullException(nameof(languageSessionProvider));
            this.hostCatalogProvider = hostCatalogProvider;
            this.preparationHandler = preparationHandler;
        }

        public DdfEditorPreparationState State
        {
            get { lock (syncRoot) return state; }
        }

        public bool IsPreparing => State == DdfEditorPreparationState.Preparing;

        public long Generation
        {
            get { lock (syncRoot) return generation; }
        }

        public DdfEditorPreparationSnapshot LastSnapshot { get; private set; }

        public event EventHandler<DdfEditorPreparationStateChangedEventArgs> StateChanged;
        public event EventHandler<DdfEditorPreparationCompletedEventArgs> PreparationCompleted;

        public DdfEditorPreparationSnapshot Validate(
            DdfEditorPreparationRequest request = null) =>
            Execute(DdfEditorPreparationMode.Validate,
                request ?? new DdfEditorPreparationRequest());

        public DdfEditorPreparationSnapshot PrepareScript(
            DdfEditorPreparationRequest request = null) =>
            Execute(DdfEditorPreparationMode.PrepareScript,
                request ?? new DdfEditorPreparationRequest());

        private DdfEditorPreparationSnapshot Execute(DdfEditorPreparationMode mode,
            DdfEditorPreparationRequest request)
        {
            long currentGeneration;
            lock (syncRoot)
            {
                if (state != DdfEditorPreparationState.Idle)
                    throw new InvalidOperationException(
                        "Una preparazione script è già attiva.");
                currentGeneration = ++generation;
                state = DdfEditorPreparationState.Preparing;
            }
            StateChanged?.Invoke(this,
                new DdfEditorPreparationStateChangedEventArgs(currentGeneration,
                    DdfEditorPreparationState.Idle,
                    DdfEditorPreparationState.Preparing));

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            IReadOnlyList<DdfEditorPreparationDocumentSnapshot> documents =
                Array.Empty<DdfEditorPreparationDocumentSnapshot>();
            DdfLanguagePackage package = null;
            DdfBuildResult result;
            try
            {
                documents = CaptureDocuments();
                package = languageSessionProvider()?.ActivePackage;
                DdfHostCatalogSnapshot hostCatalog = hostCatalogProvider?.Invoke();
                if (preparationHandler != null)
                    result = preparationHandler(mode, request, documents, package,
                        hostCatalog) ?? DdfBuildResult.Failure(
                            DdfBuildIssue.Configuration("DDFH004",
                                "Il preparatore host non ha restituito un risultato."));
                else if (package == null)
                    result = DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                        "DDFH002", "Nessun pacchetto linguaggio associato alla sessione host."));
                else if (documents.Count == 0)
                    result = DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                        "DDFH001", "Nessun documento fornito dall'applicazione host."));
                else
                    result = PrepareDocuments(documents, package, request,
                        hostCatalog);
            }
            catch (Exception exception)
            {
                result = DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                    "DDFH003", "Errore interno della preparazione script: " +
                    exception.Message));
            }

            var snapshot = new DdfEditorPreparationSnapshot(currentGeneration,
                mode, startedAt, DateTimeOffset.UtcNow, package?.Id,
                package?.PackageVersion, documents, result);
            lock (syncRoot)
            {
                LastSnapshot = snapshot;
                state = DdfEditorPreparationState.Idle;
            }
            PreparationCompleted?.Invoke(this,
                new DdfEditorPreparationCompletedEventArgs(snapshot));
            StateChanged?.Invoke(this,
                new DdfEditorPreparationStateChangedEventArgs(currentGeneration,
                    DdfEditorPreparationState.Preparing,
                    DdfEditorPreparationState.Idle));
            return snapshot;
        }

        private IReadOnlyList<DdfEditorPreparationDocumentSnapshot> CaptureDocuments()
        {
            IReadOnlyList<IDdfEditorDocument> documents =
                documentServiceProvider()?.OpenDocuments;
            if (documents == null)
                return Array.Empty<DdfEditorPreparationDocumentSnapshot>();
            return documents.Select(document =>
                new DdfEditorPreparationDocumentSnapshot(document.Id,
                    document.DisplayName, document.FilePath,
                    document.SourceText, document.Version)).ToList().AsReadOnly();
        }

        private static DdfBuildResult PrepareDocuments(
            IReadOnlyList<DdfEditorPreparationDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfEditorPreparationRequest request,
            DdfHostCatalogSnapshot hostCatalog)
        {
            string logicalRoot = Path.Combine(Path.GetTempPath(),
                "DDFLanguageEditor.HostDocuments");
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namesById = new Dictionary<string, string>(StringComparer.Ordinal);
            var sources = new List<DdfBuildSource>();
            for (int index = 0; index < documents.Count; index++)
            {
                DdfEditorPreparationDocumentSnapshot document = documents[index];
                string name = CreateUniqueName(document.DisplayName, index,
                    package.FileExtensions.FirstOrDefault(), usedNames);
                namesById[document.Id] = name;
                string logicalPath = string.IsNullOrWhiteSpace(document.FilePath)
                    ? Path.Combine(logicalRoot, name)
                    : document.FilePath;
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
