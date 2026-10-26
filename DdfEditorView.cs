using System;
using System.ComponentModel;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Reusable host-facing editing surface. The standalone shell binds its active
    /// document editor to this control; an embedding host can also use it directly.
    /// </summary>
    public sealed class DdfEditorView : UserControl, IDdfEditorSurface,
        IDdfEditorCapabilityProvider
    {
        private RichTextBox activeEditor;
        private bool settingSourceText;
        private IDdfEditorDocumentService documentService;
        private bool synchronizeDocumentText;
        private IDdfEditorPaletteService paletteService;
        private IDdfEditorHostConfiguration hostConfiguration;
        private bool applyHostVisualOptions;
        private readonly bool manageHostEditingPreferences;
        private IDdfLanguagePackageSession languageSession;
        private IDdfHostCatalog hostCatalog;
        private IDdfEditorExperienceState experienceState;
        private IDdfEditorAnalysisService analysisService;
        private bool ownsAnalysisService;
        private IDdfEditorCommandService commandService;
        private DdfEditorAnalysisSnapshot lastAnalysisSnapshot;
        private long standaloneDocumentVersion;
        private IDdfEditorPersistenceProvider persistenceProvider;
        private IDdfEditorProjectContainer projectContainer;
        private DdfEditorProjectState currentProject;
        private IDdfEditorPreparationService preparationService;
        private IDdfEditorExecutionService executionService;
        private bool ownsExecutionService;
        private IDdfEditorDebugService debugService;
        private bool ownsDebugService;
        private IDdfEditorProductUpdateService productUpdateService;
        private readonly IDdfEditorEventDispatcher eventDispatcher;

        public DdfEditorView() : this(true)
        {
        }

        internal DdfEditorView(bool createDefaultEditor)
        {
            Name = "ddfEditorView";
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.FromArgb(212, 212, 212);
            Padding = new Padding(3);
            AccessibleName = "Editor di codice DDF";
            AccessibleDescription = "Superficie editor incorporabile con testo e selezione controllabili dall'applicazione host.";
            manageHostEditingPreferences = createDefaultEditor;
            eventDispatcher = new DdfWinFormsEventDispatcher(this);

            if (createDefaultEditor)
            {
                var editor = new MultiCursorRichTextBox
                {
                    AcceptsTab = true,
                    BackColor = BackColor,
                    BorderStyle = BorderStyle.None,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10F),
                    ForeColor = ForeColor,
                    HideSelection = false,
                    Name = "richTextBoxMainEditor",
                    WordWrap = false
                };
                Controls.Add(editor);
                BindEditor(editor);
            }
            AttachHostConfiguration(new DdfEditorHostConfiguration(), createDefaultEditor);
            AttachLanguageSession(new DdfLanguagePackageSession());
            AttachHostCatalog(new DdfEditorHostCatalog());
            AttachExperienceState(new DdfEditorExperienceState());
            AttachAnalysisService(new DdfEditorAnalysisService(), true);
            AttachPreparationService(new DdfEditorPreparationService(
                () => documentService, () => languageSession,
                () => hostCatalog?.Snapshot));
            var scriptStateStore = new DdfStateStore();
            AttachExecutionService(new DdfEditorExecutionService(
                () => documentService, () => languageSession,
                () => hostCatalog?.Snapshot, scriptStateStore: scriptStateStore,
                eventDispatcher: eventDispatcher), true);
            AttachDebugService(new DdfEditorDebugService(
                () => documentService, () => languageSession,
                () => hostCatalog?.Snapshot, scriptStateStore: scriptStateStore,
                eventDispatcher: eventDispatcher), true);
            AttachCommandService(new DdfEditorCommandCatalog());
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ContractVersion => DdfEditorSdkContract.CurrentVersion;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorEventDispatcher EventDispatcher => eventDispatcher;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SourceText
        {
            get => activeEditor?.Text ?? string.Empty;
            set
            {
                EnsureEditor();
                string next = value ?? string.Empty;
                if (string.Equals(activeEditor.Text, next, StringComparison.Ordinal))
                {
                    UpdateAttachedDocument(next);
                    return;
                }
                settingSourceText = true;
                try
                {
                    activeEditor.Text = next;
                }
                finally
                {
                    settingSourceText = false;
                }
                if (documentService?.ActiveDocumentInfo == null) standaloneDocumentVersion++;
                UpdateAttachedDocument(next);
                InvalidateAnalysis();
                SourceTextChanged?.Invoke(this, EventArgs.Empty);
                RefreshCommands();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionStart => activeEditor?.SelectionStart ?? 0;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionLength => activeEditor?.SelectionLength ?? 0;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorDocumentService DocumentService => documentService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorPaletteService PaletteService => paletteService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorHostConfiguration HostConfiguration => hostConfiguration;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfLanguagePackageSession LanguageSession => languageSession;

        /// <summary>
        /// Inspects every supplied editor buffer before a package switch. The
        /// returned plan is read-only and never changes text or active language.
        /// </summary>
        public DdfLanguageMigrationPlan AnalyzeLanguageMigration(
            DdfLanguagePackage targetPackage)
        {
            if (targetPackage == null)
                throw new ArgumentNullException(nameof(targetPackage));
            DdfLanguagePackage sourcePackage = languageSession?.ActivePackage ??
                throw new InvalidOperationException(
                    "Nessun pacchetto linguaggio è associato alla superficie.");
            IReadOnlyList<IDdfEditorDocument> available = GetAvailableDocuments();
            IEnumerable<DdfLanguageMigrationDocument> documents =
                available.Count == 0
                    ? new[]
                    {
                        new DdfLanguageMigrationDocument(string.Empty,
                            SourceText)
                    }
                    : available.Select(document =>
                        new DdfLanguageMigrationDocument(
                            string.IsNullOrWhiteSpace(document.FilePath)
                                ? document.Id
                                : document.FilePath,
                            document.SourceText));
            return DdfLanguageMigrationAnalyzer.Analyze(documents,
                sourcePackage, targetPackage);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfHostCatalog HostCatalog => hostCatalog;

        public event EventHandler<DdfHostCatalogChangedEventArgs> HostCatalogChanged;
        public event EventHandler LanguageSessionChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorExperienceState ExperienceState => experienceState;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorAnalysisService AnalysisService => analysisService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DdfEditorAnalysisSnapshot LastAnalysisSnapshot => lastAnalysisSnapshot;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorCommandService CommandService => commandService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorPersistenceProvider PersistenceProvider => persistenceProvider;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorProjectContainer ProjectContainer => projectContainer;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DdfEditorProjectState CurrentProject => currentProject;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorPreparationService PreparationService => preparationService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorExecutionService ExecutionService => executionService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorScriptStateService ScriptStateService =>
            executionService as IDdfEditorScriptStateService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorScriptStatePersistenceService ScriptStatePersistenceService =>
            executionService as IDdfEditorScriptStatePersistenceService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorDebugService DebugService => debugService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDdfEditorProductUpdateService ProductUpdateService =>
            productUpdateService;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<DdfEditorCapabilityDescriptor> KnownCapabilities =>
            DdfEditorCapabilityCatalog.Known;

        public DdfEditorCapabilityResult InspectCapability(string capabilityId)
        {
            object service = capabilityId switch
            {
                DdfEditorCapabilityIds.ScriptState =>
                    executionService as IDdfEditorScriptStateService,
                DdfEditorCapabilityIds.ScriptStatePersistence =>
                    executionService as IDdfEditorScriptStatePersistenceService,
                DdfEditorCapabilityIds.CooperativeExecution =>
                    executionService as IDdfEditorCooperativeExecutionService,
                DdfEditorCapabilityIds.AsyncLifecycle =>
                    executionService as IDdfEditorAsyncLifecycleService,
                DdfEditorCapabilityIds.ProductUpdates => productUpdateService,
                _ => null
            };
            return DdfEditorCapabilityCatalog.Find(capabilityId) == null
                ? DdfEditorCapabilityCatalog.Unknown(capabilityId)
                : service == null
                    ? DdfEditorCapabilityCatalog.Unavailable(capabilityId)
                    : DdfEditorCapabilityCatalog.Available(capabilityId, service);
        }

        [DefaultValue(false)]
        public bool EditorReadOnly
        {
            get => activeEditor?.ReadOnly ?? false;
            set
            {
                EnsureEditor();
                activeEditor.ReadOnly = value;
                RefreshCommands();
            }
        }

        [DefaultValue(false)]
        public bool WordWrap
        {
            get => activeEditor?.WordWrap ?? false;
            set
            {
                EnsureEditor();
                activeEditor.WordWrap = value;
                RefreshCommands();
            }
        }

        public event EventHandler SourceTextChanged;
        public event EventHandler EditorSelectionChanged;
        public event EventHandler<DdfEditorSelectionChangedEventArgs> SelectionSnapshotChanged;
        public event EventHandler ActiveEditorChanged;
        public event EventHandler<DdfEditorDocumentEventArgs> DocumentAdded;
        public event EventHandler<DdfEditorDocumentEventArgs> ActiveDocumentChanged;
        public event EventHandler<DdfEditorDocumentChangedEventArgs> DocumentChanged;
        public event EventHandler<DdfEditorDocumentEventArgs> DocumentClosed;
        public event EventHandler<DdfEditorAnalysisCompletedEventArgs> AnalysisCompleted;
        public event EventHandler<DdfEditorAnalysisCompletedEventArgs> DiagnosticsUpdated;
        public event EventHandler<DdfEditorDefinitionNavigatedEventArgs> DefinitionNavigated;
        public event EventHandler<DdfEditorProjectEventArgs> ProjectSaved;
        public event EventHandler<DdfEditorProjectEventArgs> ProjectOpened;
        public event EventHandler<DdfEditorPreparationStateChangedEventArgs> PreparationStateChanged;
        public event EventHandler<DdfEditorPreparationCompletedEventArgs> PreparationCompleted;
        public event EventHandler<DdfEditorExecutionStateChangedEventArgs> ExecutionStateChanged;
        public event EventHandler<DdfEditorCooperativeStateChangedEventArgs>
            CooperativeExecutionStateChanged;
        public event EventHandler<DdfEditorExecutionOutputEventArgs> ExecutionOutputReceived;
        public event EventHandler<DdfEditorExecutionCompletedEventArgs> ExecutionCompleted;
        public event EventHandler<DdfEditorDebugStateChangedEventArgs> DebugStateChanged;
        public event EventHandler<DdfEditorCooperativeStateChangedEventArgs>
            CooperativeDebugStateChanged;
        public event EventHandler<DdfEditorDebugPausedEventArgs> DebugPaused;
        public event EventHandler<DdfEditorExecutionOutputEventArgs> DebugOutputReceived;
        public event EventHandler<DdfEditorExecutionCompletedEventArgs> DebugCompleted;

        public void SetSelection(int start, int length)
        {
            EnsureEditor();
            int safeStart = Math.Max(0, Math.Min(start, activeEditor.TextLength));
            int safeLength = Math.Max(0, Math.Min(length, activeEditor.TextLength - safeStart));
            activeEditor.Select(safeStart, safeLength);
            RefreshCommands();
        }

        public bool FocusEditor()
        {
            return activeEditor != null && !activeEditor.IsDisposed && activeEditor.Focus();
        }

        public DdfEditorAnalysisSnapshot AnalyzeDocument()
        {
            EnsureEditor();
            DdfEditorAnalysisRequest request = CreateAnalysisRequest();
            DdfEditorAnalysisSnapshot snapshot = analysisService.Analyze(request);
            lastAnalysisSnapshot = snapshot;
            var eventArgs = new DdfEditorAnalysisCompletedEventArgs(snapshot);
            AnalysisCompleted?.Invoke(this, eventArgs);
            DiagnosticsUpdated?.Invoke(this, eventArgs);
            RefreshCommands();
            return snapshot;
        }

        public bool TryGoToDefinition()
        {
            DdfEditorAnalysisSnapshot snapshot = AnalyzeDocument();
            IReadOnlyList<IDdfEditorDocument> documents = GetAvailableDocuments();
            DdfEditorNavigationTarget target = DdfEditorNavigationService.FindDefinition(snapshot, documents);
            if (target == null) return false;
            IDdfEditorDocument activeDocument = documentService?.ActiveDocumentInfo;
            if (activeDocument != null &&
                !string.Equals(activeDocument.Id, target.DocumentId, StringComparison.Ordinal) &&
                !documentService.ActivateDocument(target.DocumentId)) return false;
            SetSelection(target.Start, target.Length);
            FocusEditor();
            DefinitionNavigated?.Invoke(this,
                new DdfEditorDefinitionNavigatedEventArgs(target));
            return true;
        }

        public DdfEditorPreparationSnapshot ValidateScript(
            DdfEditorPreparationRequest request = null)
        {
            EnsureNoActiveOperation("convalidare lo script");
            return preparationService.Validate(request);
        }

        public DdfEditorPreparationSnapshot PrepareScript(
            DdfEditorPreparationRequest request = null)
        {
            EnsureNoActiveOperation("preparare lo script");
            return preparationService.PrepareScript(request);
        }

        public DdfEditorPersistedState CapturePersistenceState()
        {
            IReadOnlyList<IDdfEditorDocument> openDocuments = documentService?.OpenDocuments;
            var documents = openDocuments == null
                ? new List<DdfEditorPersistedDocument>
                {
                    new DdfEditorPersistedDocument("Senza titolo.ddf", string.Empty,
                        SourceText, true)
                }
                : openDocuments.Select(document => new DdfEditorPersistedDocument(
                    document.DisplayName, document.FilePath, document.SourceText,
                    document.IsDirty)).ToList();
            int activeIndex = documents.Count == 0 ? -1 : 0;
            if (openDocuments != null && documentService.ActiveDocumentInfo != null)
            {
                for (int index = 0; index < openDocuments.Count; index++)
                    if (string.Equals(openDocuments[index].Id,
                        documentService.ActiveDocumentInfo.Id, StringComparison.Ordinal))
                    {
                        activeIndex = index;
                        break;
                    }
            }
            DdfEditorHostOptions current = hostConfiguration.Options;
            var effectiveOptions = new DdfEditorHostOptions(current.FontFamily,
                current.FontSize, current.ZoomPercent, current.Theme, WordWrap,
                current.UseTabs, current.IndentSize);
            var preferences = new DdfEditorPersistedPreferences(effectiveOptions,
                hostConfiguration.Shortcuts, experienceState.EducationalModeEnabled);
            var persistedBreakpoints = new List<DdfEditorPersistedBreakpoint>();
            if (openDocuments != null && debugService != null)
            {
                foreach (DdfEditorBreakpoint breakpoint in debugService.Breakpoints)
                {
                    int documentIndex = -1;
                    for (int index = 0; index < openDocuments.Count; index++)
                        if (string.Equals(openDocuments[index].Id, breakpoint.DocumentId,
                            StringComparison.Ordinal))
                        {
                            documentIndex = index;
                            break;
                        }
                    if (documentIndex >= 0)
                        persistedBreakpoints.Add(new DdfEditorPersistedBreakpoint(
                            documentIndex, breakpoint.Line, breakpoint.Enabled));
                }
            }
            var debugging = new DdfEditorPersistedDebugState(persistedBreakpoints,
                debugService?.WatchExpressions);
            return new DdfEditorPersistedState(documents, activeIndex, preferences,
                debugging);
        }

        public void ApplyPersistenceState(DdfEditorPersistedState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!string.Equals(state.SchemaVersion,
                    DdfEditorPersistedState.CurrentSchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Lo snapshot editor usa lo schema '" +
                    state.SchemaVersion + "', ma questa superficie richiede '" +
                    DdfEditorPersistedState.CurrentSchemaVersion + "'.");
            if (HasActiveOperation())
                throw new InvalidOperationException(
                    "Lo stato non può essere ripristinato durante un'esecuzione o una sessione di debug.");
            if (documentService != null && documentService.OpenDocuments.Count > 0)
                throw new InvalidOperationException(
                    "Il ripristino richiede un servizio documentale vuoto per non sovrascrivere buffer esistenti.");
            if (documentService == null && state.Documents.Count > 1)
                throw new InvalidOperationException(
                    "Una superficie senza servizio documentale può ripristinare un solo buffer.");
            if (documentService == null && state.Debugging.Breakpoints.Count > 0)
                throw new InvalidOperationException(
                    "Il ripristino dei breakpoint richiede un servizio documentale con identità stabili.");

            var createdDocumentIds = new List<string>();
            IReadOnlyList<DdfEditorBreakpoint> previousBreakpoints = debugService?.Breakpoints;
            IReadOnlyList<string> previousWatchExpressions = debugService?.WatchExpressions;
            try
            {
                if (documentService == null)
                {
                    SourceText = state.Documents.Count == 0
                        ? string.Empty
                        : state.Documents[state.ActiveDocumentIndex].SourceText;
                }
                else
                {
                    foreach (DdfEditorPersistedDocument persisted in state.Documents)
                    {
                        IDdfEditorDocument document = string.IsNullOrEmpty(persisted.FilePath)
                            ? documentService.CreateVirtual(persisted.DisplayName, persisted.SourceText)
                            : documentService.OpenDocument(persisted.FilePath, persisted.SourceText);
                        createdDocumentIds.Add(document.Id);
                        if (persisted.IsDirty)
                            documentService.UpdateDocument(document.Id, persisted.SourceText, true);
                    }
                    if (state.ActiveDocumentIndex >= 0)
                        documentService.ActivateDocument(
                            createdDocumentIds[state.ActiveDocumentIndex]);
                }
                if (debugService != null)
                {
                    debugService.SetBreakpoints(documentService == null
                        ? Array.Empty<DdfEditorBreakpoint>()
                        : state.Debugging.Breakpoints.Select(item =>
                            new DdfEditorBreakpoint(createdDocumentIds[item.DocumentIndex],
                                item.Line, item.Enabled)));
                    debugService.SetWatchExpressions(state.Debugging.WatchExpressions);
                }
            }
            catch
            {
                for (int index = createdDocumentIds.Count - 1; index >= 0; index--)
                    documentService?.TryCloseDocument(createdDocumentIds[index], true);
                if (debugService != null)
                {
                    debugService.SetBreakpoints(previousBreakpoints);
                    debugService.SetWatchExpressions(previousWatchExpressions);
                }
                throw;
            }

            hostConfiguration.ApplyOptions(state.Preferences.Options);
            hostConfiguration.ReplaceShortcuts(state.Preferences.Shortcuts);
            experienceState.SetEducationalMode(state.Preferences.EducationalModeEnabled);
            RefreshCommands();
        }

        public void SaveState()
        {
            if (persistenceProvider == null)
                throw new InvalidOperationException("Nessuna strategia di persistenza è associata alla superficie.");
            persistenceProvider.Save(CapturePersistenceState());
        }

        public bool RestoreState()
        {
            if (persistenceProvider == null)
                throw new InvalidOperationException("Nessuna strategia di persistenza è associata alla superficie.");
            DdfEditorPersistedState state = persistenceProvider.Load();
            if (state == null) return false;
            state = PreparePersistenceState(state,
                persistenceProvider as IDdfEditorPersistenceMigrationProvider);
            ApplyPersistenceState(state);
            return true;
        }

        public DdfEditorProjectState SaveProject(string projectId, string displayName,
            IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            if (projectContainer == null)
                throw new InvalidOperationException(
                    "Nessun contenitore progetto è associato alla superficie.");
            DdfLanguagePackage package = languageSession?.ActivePackage ??
                throw new InvalidOperationException(
                    "Nessun pacchetto linguaggio è associato alla superficie.");
            var project = new DdfEditorProjectState(projectId, displayName,
                package.Id, package.PackageVersion, CapturePersistenceState(), metadata);
            projectContainer.Save(project);
            currentProject = project;
            ProjectSaved?.Invoke(this, new DdfEditorProjectEventArgs(project));
            RefreshCommands();
            return project;
        }

        public DdfEditorProjectState SaveProject()
        {
            if (currentProject == null)
                throw new InvalidOperationException(
                    "Il progetto corrente non ha ancora un'identità assegnata dall'host.");
            return SaveProject(currentProject.ProjectId, currentProject.DisplayName,
                currentProject.Metadata);
        }

        public bool OpenProject()
        {
            if (projectContainer == null)
                throw new InvalidOperationException(
                    "Nessun contenitore progetto è associato alla superficie.");
            EnsureNoActiveOperation("aprire un progetto");
            DdfEditorProjectState project = projectContainer.Load();
            if (project == null) return false;
            DdfLanguagePackage package = languageSession?.ActivePackage ??
                throw new InvalidOperationException(
                    "Nessun pacchetto linguaggio è associato alla superficie.");
            if (!string.Equals(project.LanguagePackageId, package.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(project.LanguagePackageVersion, package.PackageVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il progetto richiede il pacchetto linguaggio '" +
                    project.LanguagePackageId + "' versione " +
                    project.LanguagePackageVersion + ". L'host deve attivarlo prima dell'apertura.");
            DdfEditorPersistedState editorState = PreparePersistenceState(
                project.EditorState,
                projectContainer as IDdfEditorPersistenceMigrationProvider);
            ApplyPersistenceState(editorState);
            currentProject = project;
            ProjectOpened?.Invoke(this, new DdfEditorProjectEventArgs(project));
            RefreshCommands();
            return true;
        }

        private static DdfEditorPersistedState PreparePersistenceState(
            DdfEditorPersistedState state,
            IDdfEditorPersistenceMigrationProvider migrationProvider)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.Equals(state.SchemaVersion,
                    DdfEditorPersistedState.CurrentSchemaVersion,
                    StringComparison.Ordinal))
                return state;
            DdfPersistenceSchemaContract contract =
                DdfPersistenceSchemaCatalog.Get(
                    DdfPersistenceArtifactKind.EditorState);
            DdfPersistenceInspection inspection = contract.Inspect(
                new DdfPersistenceArtifact(
                    DdfPersistenceArtifactKind.EditorState,
                    state.SchemaVersion, string.Empty));
            if (!inspection.CanMigrate || migrationProvider == null)
                throw new InvalidOperationException(inspection.Message +
                    " Lo snapshot originale non è stato applicato né modificato.");
            DdfEditorPersistedState migrated = migrationProvider.Migrate(state,
                DdfEditorPersistedState.CurrentSchemaVersion);
            if (migrated == null || !string.Equals(migrated.SchemaVersion,
                    DdfEditorPersistedState.CurrentSchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il provider non ha prodotto uno snapshot editor con lo schema richiesto; " +
                    "lo snapshot originale non è stato applicato né modificato.");
            return migrated;
        }

        public void AttachDocumentService(IDdfEditorDocumentService service)
        {
            AttachDocumentService(service, true);
        }

        public void AttachPaletteService(IDdfEditorPaletteService service)
        {
            paletteService = service;
        }

        public void AttachHostConfiguration(IDdfEditorHostConfiguration configuration)
        {
            AttachHostConfiguration(configuration, true);
        }

        public void AttachLanguageSession(IDdfLanguagePackageSession session)
        {
            if (ReferenceEquals(languageSession, session)) return;
            EnsureNoActiveOperation("cambiare il pacchetto linguaggio");
            if (languageSession != null)
                languageSession.ActiveLanguageChanged -= languageSession_ActiveLanguageChanged;
            languageSession = session ?? throw new ArgumentNullException(nameof(session));
            languageSession.ActiveLanguageChanged += languageSession_ActiveLanguageChanged;
            InvalidateAnalysis();
            RefreshCommands();
            LanguageSessionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AttachHostCatalog(IDdfHostCatalog catalog)
        {
            if (ReferenceEquals(hostCatalog, catalog)) return;
            EnsureNoActiveOperation("sostituire il catalogo host");
            if (hostCatalog != null) hostCatalog.Changed -= hostCatalog_Changed;
            hostCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            hostCatalog.Changed += hostCatalog_Changed;
            InvalidateAnalysis();
            RefreshCommands();
            HostCatalogChanged?.Invoke(this,
                new DdfHostCatalogChangedEventArgs(hostCatalog.Snapshot));
        }

        public void AttachExperienceState(IDdfEditorExperienceState state)
        {
            experienceState = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void AttachAnalysisService(IDdfEditorAnalysisService service)
        {
            AttachAnalysisService(service, false);
        }

        public void AttachCommandService(IDdfEditorCommandService service)
        {
            commandService = service ?? throw new ArgumentNullException(nameof(service));
            if (manageHostEditingPreferences) RegisterBuiltInCommands();
        }

        public void AttachPersistenceProvider(IDdfEditorPersistenceProvider provider)
        {
            persistenceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            RefreshCommands();
        }

        public void AttachScriptStatePersistenceProvider(
            IDdfEditorScriptStatePersistenceProvider provider)
        {
            EnsureNoActiveOperation("collegare il provider di persistenza state");
            IDdfEditorScriptStatePersistenceService service =
                RequireCapability<IDdfEditorScriptStatePersistenceService>(
                    DdfEditorCapabilityIds.ScriptStatePersistence);
            service.AttachScriptStatePersistenceProvider(provider);
        }

        public bool DetachScriptStatePersistenceProvider()
        {
            EnsureNoActiveOperation("revocare il provider di persistenza state");
            return RequireCapability<IDdfEditorScriptStatePersistenceService>(
                    DdfEditorCapabilityIds.ScriptStatePersistence)
                .DetachScriptStatePersistenceProvider();
        }

        public void SaveScriptState()
        {
            EnsureNoActiveOperation("salvare lo state");
            RequireCapability<IDdfEditorScriptStatePersistenceService>(
                    DdfEditorCapabilityIds.ScriptStatePersistence)
                .SavePersistedScriptState();
        }

        public bool RestoreScriptState()
        {
            EnsureNoActiveOperation("ripristinare lo state");
            return RequireCapability<IDdfEditorScriptStatePersistenceService>(
                    DdfEditorCapabilityIds.ScriptStatePersistence)
                .RestorePersistedScriptState();
        }

        public void ResetPersistedScriptState()
        {
            EnsureNoActiveOperation("azzerare lo state persistito");
            RequireCapability<IDdfEditorScriptStatePersistenceService>(
                    DdfEditorCapabilityIds.ScriptStatePersistence)
                .ResetPersistedScriptState();
        }

        public void AttachProjectContainer(IDdfEditorProjectContainer container)
        {
            if (ReferenceEquals(projectContainer, container)) return;
            EnsureNoActiveOperation("sostituire il contenitore progetto");
            projectContainer = container ?? throw new ArgumentNullException(nameof(container));
            currentProject = null;
            RefreshCommands();
        }

        public void AttachPreparationService(IDdfEditorPreparationService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (ReferenceEquals(preparationService, service)) return;
            EnsureNoActiveOperation("sostituire il servizio di preparazione");
            if (preparationService != null)
            {
                preparationService.StateChanged -= preparationService_StateChanged;
                preparationService.PreparationCompleted -= preparationService_PreparationCompleted;
            }
            preparationService = service;
            preparationService.StateChanged += preparationService_StateChanged;
            preparationService.PreparationCompleted += preparationService_PreparationCompleted;
            RefreshCommands();
        }

        public void AttachExecutionService(IDdfEditorExecutionService service)
        {
            AttachExecutionService(service, false);
        }

        public void AttachDebugService(IDdfEditorDebugService service)
        {
            AttachDebugService(service, false);
        }

        /// <summary>
        /// Attaches an opt-in verification service supplied and owned by the host.
        /// Merely attaching it performs no network request and creates no timer or UI.
        /// </summary>
        public void AttachProductUpdateService(
            IDdfEditorProductUpdateService service)
        {
            productUpdateService = service ??
                throw new ArgumentNullException(nameof(service));
        }

        public bool DetachProductUpdateService()
        {
            bool detached = productUpdateService != null;
            productUpdateService = null;
            return detached;
        }

        private void AttachAnalysisService(IDdfEditorAnalysisService service, bool ownsService)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (ReferenceEquals(analysisService, service))
            {
                ownsAnalysisService = ownsAnalysisService || ownsService;
                return;
            }
            if (ownsAnalysisService) analysisService?.Dispose();
            analysisService = service;
            ownsAnalysisService = ownsService;
            InvalidateAnalysis();
            RefreshCommands();
        }

        internal RichTextBox ActiveEditorControl => activeEditor;

        internal void AttachHostConfiguration(IDdfEditorHostConfiguration configuration, bool applyVisualOptions)
        {
            if (ReferenceEquals(hostConfiguration, configuration) && applyHostVisualOptions == applyVisualOptions) return;
            DetachHostConfiguration();
            hostConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            applyHostVisualOptions = applyVisualOptions;
            hostConfiguration.ConfigurationChanged += hostConfiguration_ConfigurationChanged;
            if (applyHostVisualOptions) ApplyHostOptions();
        }

        internal void AttachDocumentService(IDdfEditorDocumentService service, bool synchronizeText)
        {
            if (ReferenceEquals(documentService, service) && synchronizeDocumentText == synchronizeText) return;
            EnsureNoActiveOperation("sostituire il servizio documentale");
            DetachDocumentService();
            documentService = service;
            synchronizeDocumentText = synchronizeText;
            if (documentService != null)
            {
                documentService.DocumentAdded += documentService_DocumentAdded;
                documentService.ActiveDocumentChanged += documentService_ActiveDocumentChanged;
                documentService.DocumentChanged += documentService_DocumentChanged;
                documentService.DocumentClosed += documentService_DocumentClosed;
            }
            if (synchronizeDocumentText) ApplyDocumentText(documentService?.ActiveDocumentInfo?.SourceText);
            InvalidateAnalysis();
        }

        internal void BindEditor(RichTextBox editor)
        {
            if (ReferenceEquals(activeEditor, editor)) return;
            if (activeEditor != null)
            {
                activeEditor.TextChanged -= activeEditor_TextChanged;
                activeEditor.SelectionChanged -= activeEditor_SelectionChanged;
                if (manageHostEditingPreferences) activeEditor.KeyDown -= activeEditor_KeyDown;
            }
            activeEditor = editor;
            if (activeEditor != null)
            {
                activeEditor.TextChanged += activeEditor_TextChanged;
                activeEditor.SelectionChanged += activeEditor_SelectionChanged;
                if (manageHostEditingPreferences) activeEditor.KeyDown += activeEditor_KeyDown;
            }
            if (synchronizeDocumentText)
                ApplyDocumentText(documentService?.ActiveDocumentInfo?.SourceText);
            if (applyHostVisualOptions) ApplyHostOptions();
            InvalidateAnalysis();
            ActiveEditorChanged?.Invoke(this, EventArgs.Empty);
            RefreshCommands();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachDocumentService();
                DetachHostConfiguration();
                paletteService = null;
                DetachDebugService();
                DetachExecutionService();
                if (languageSession != null)
                    languageSession.ActiveLanguageChanged -= languageSession_ActiveLanguageChanged;
                languageSession = null;
                if (hostCatalog != null) hostCatalog.Changed -= hostCatalog_Changed;
                hostCatalog = null;
                experienceState = null;
                if (ownsAnalysisService) analysisService?.Dispose();
                analysisService = null;
                ownsAnalysisService = false;
                commandService = null;
                persistenceProvider = null;
                projectContainer = null;
                currentProject = null;
                productUpdateService = null;
                if (preparationService != null)
                {
                    preparationService.StateChanged -= preparationService_StateChanged;
                    preparationService.PreparationCompleted -= preparationService_PreparationCompleted;
                    preparationService = null;
                }
                if (activeEditor != null)
                {
                    activeEditor.TextChanged -= activeEditor_TextChanged;
                    activeEditor.SelectionChanged -= activeEditor_SelectionChanged;
                    if (manageHostEditingPreferences) activeEditor.KeyDown -= activeEditor_KeyDown;
                    activeEditor = null;
                }
            }
            base.Dispose(disposing);
        }

        private void activeEditor_TextChanged(object sender, EventArgs e)
        {
            if (settingSourceText) return;
            if (documentService?.ActiveDocumentInfo == null) standaloneDocumentVersion++;
            UpdateAttachedDocument(activeEditor.Text);
            InvalidateAnalysis();
            SourceTextChanged?.Invoke(this, EventArgs.Empty);
            RefreshCommands();
        }

        private void activeEditor_SelectionChanged(object sender, EventArgs e)
        {
            InvalidateAnalysis();
            EditorSelectionChanged?.Invoke(this, EventArgs.Empty);
            IDdfEditorDocument document = documentService?.ActiveDocumentInfo;
            SelectionSnapshotChanged?.Invoke(this,
                new DdfEditorSelectionChangedEventArgs(document?.Id,
                    document?.Version ?? standaloneDocumentVersion,
                    activeEditor.SelectionStart, activeEditor.SelectionLength));
            RefreshCommands();
        }

        private void documentService_DocumentAdded(object sender,
            DdfEditorDocumentEventArgs e)
        {
            DocumentAdded?.Invoke(this, e);
            RefreshCommands();
        }

        private void activeEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Tab || e.Shift || e.Control || e.Alt || activeEditor.ReadOnly) return;
            DdfEditorHostOptions options = hostConfiguration?.Options;
            activeEditor.SelectedText = options?.UseTabs == true
                ? "\t"
                : new string(' ', options?.IndentSize ?? EditorSettings.DefaultIndentSize);
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void EnsureEditor()
        {
            if (activeEditor == null || activeEditor.IsDisposed)
                throw new InvalidOperationException("Nessun editor attivo è associato alla superficie.");
        }

        private void documentService_ActiveDocumentChanged(object sender, DdfEditorDocumentEventArgs e)
        {
            InvalidateAnalysis();
            if (synchronizeDocumentText) ApplyDocumentText(e.Document?.SourceText);
            ActiveDocumentChanged?.Invoke(this, e);
            RefreshCommands();
        }

        private void documentService_DocumentChanged(object sender, DdfEditorDocumentChangedEventArgs e)
        {
            DocumentChanged?.Invoke(this, e);
            if (!synchronizeDocumentText || settingSourceText ||
                !ReferenceEquals(documentService?.ActiveDocumentInfo, e.Document)) return;
            InvalidateAnalysis();
            ApplyDocumentText(e.Document.SourceText);
        }

        private void documentService_DocumentClosed(object sender,
            DdfEditorDocumentEventArgs e)
        {
            DocumentClosed?.Invoke(this, e);
            RefreshCommands();
        }

        private void ApplyDocumentText(string sourceText)
        {
            if (activeEditor == null || activeEditor.IsDisposed) return;
            string next = sourceText ?? string.Empty;
            if (string.Equals(activeEditor.Text, next, StringComparison.Ordinal)) return;
            settingSourceText = true;
            try
            {
                activeEditor.Text = next;
                activeEditor.Select(0, 0);
            }
            finally
            {
                settingSourceText = false;
            }
            SourceTextChanged?.Invoke(this, EventArgs.Empty);
            RefreshCommands();
        }

        private void DetachDocumentService()
        {
            if (documentService != null)
            {
                documentService.DocumentAdded -= documentService_DocumentAdded;
                documentService.ActiveDocumentChanged -= documentService_ActiveDocumentChanged;
                documentService.DocumentChanged -= documentService_DocumentChanged;
                documentService.DocumentClosed -= documentService_DocumentClosed;
            }
            documentService = null;
            synchronizeDocumentText = false;
        }

        private void UpdateAttachedDocument(string sourceText)
        {
            IDdfEditorDocument activeDocument = documentService?.ActiveDocumentInfo;
            if (activeDocument != null &&
                !string.Equals(activeDocument.SourceText, sourceText, StringComparison.Ordinal))
                documentService.UpdateDocument(activeDocument.Id, sourceText);
        }

        private void hostConfiguration_ConfigurationChanged(
            object sender, DdfEditorHostConfigurationChangedEventArgs e)
        {
            if (applyHostVisualOptions && e.ChangeKind == DdfEditorHostConfigurationChangeKind.Options)
                ApplyHostOptions();
            else
                RefreshCommands();
        }

        private void ApplyHostOptions()
        {
            DdfEditorHostOptions options = hostConfiguration?.Options;
            if (options == null) return;
            Color background = options.Theme == EditorTheme.Dark
                ? Color.FromArgb(30, 30, 30)
                : Color.White;
            Color foreground = options.Theme == EditorTheme.Dark
                ? Color.FromArgb(212, 212, 212)
                : Color.FromArgb(32, 32, 32);
            BackColor = background;
            ForeColor = foreground;
            if (activeEditor == null || activeEditor.IsDisposed) return;
            try { activeEditor.Font = new Font(options.FontFamily, options.FontSize); }
            catch (ArgumentException) { activeEditor.Font = new Font(EditorSettings.DefaultFontFamily, options.FontSize); }
            activeEditor.BackColor = background;
            activeEditor.ForeColor = foreground;
            activeEditor.ZoomFactor = options.ZoomPercent / 100F;
            activeEditor.WordWrap = options.WordWrap;
            RefreshCommands();
        }

        private void RegisterBuiltInCommands()
        {
            RegisterBuiltInCommand(DdfEditorCommandIds.Undo, "Annulla", "Modifica",
                () => activeEditor.Undo(),
                () => CanEdit() && activeEditor.CanUndo,
                shortcutText: "Ctrl+Z");
            RegisterBuiltInCommand(DdfEditorCommandIds.Redo, "Ripristina", "Modifica",
                () => activeEditor.Redo(),
                () => CanEdit() && activeEditor.CanRedo,
                shortcutText: "Ctrl+Y");
            RegisterBuiltInCommand(DdfEditorCommandIds.SelectAll, "Seleziona tutto", "Modifica",
                () => activeEditor.SelectAll(),
                () => HasEditor() && activeEditor.TextLength > 0,
                shortcutText: "Ctrl+A");
            RegisterBuiltInCommand(DdfEditorCommandIds.DeleteSelection, "Elimina selezione", "Modifica",
                () => activeEditor.SelectedText = string.Empty,
                () => CanEdit() && activeEditor.SelectionLength > 0,
                shortcutText: "Canc");
            RegisterBuiltInCommand(DdfEditorCommandIds.ToggleWordWrap, "Accapo automatico", "Visualizza",
                () => WordWrap = !WordWrap,
                () => HasEditor(),
                () => WordWrap);
            RegisterBuiltInCommand(DdfEditorCommandIds.FocusEditor, "Attiva editor", "Editor",
                () => FocusEditor(),
                () => HasEditor());
            RegisterBuiltInCommand(DdfEditorCommandIds.AnalyzeDocument, "Analizza documento", "Analisi",
                () => AnalyzeDocument(),
                () => HasEditor() && analysisService != null && languageSession?.ActivePackage != null);
            RegisterBuiltInCommand(DdfEditorCommandIds.GoToDefinition, "Vai alla definizione", "Navigazione",
                () => TryGoToDefinition(),
                CanNavigateToDefinition,
                shortcutText: "F12");
            RegisterBuiltInCommand(DdfEditorCommandIds.SaveHostState, "Salva stato editor", "Persistenza",
                SaveState, () => persistenceProvider != null);
            RegisterBuiltInCommand(DdfEditorCommandIds.RestoreHostState, "Ripristina stato editor", "Persistenza",
                () => RestoreState(), () => persistenceProvider != null && !HasActiveOperation());
            RegisterBuiltInCommand(DdfEditorCommandIds.SaveHostProject, "Salva progetto host", "Progetto",
                () => SaveProject(), () => projectContainer != null && currentProject != null);
            RegisterBuiltInCommand(DdfEditorCommandIds.OpenHostProject, "Apri progetto host", "Progetto",
                () => OpenProject(), () => projectContainer != null && !HasActiveOperation() &&
                      (documentService == null || documentService.OpenDocuments.Count == 0));
            RegisterBuiltInCommand(DdfEditorCommandIds.ValidateScript, "Convalida script", "Script",
                () => ValidateScript(), CanPrepareScript);
            RegisterBuiltInCommand(DdfEditorCommandIds.PrepareScript, "Prepara script", "Script",
                () => PrepareScript(), CanPrepareScript);
            RegisterBuiltInCommand(DdfEditorCommandIds.RunWithoutDebug, "Avvia senza debug", "Esecuzione",
                () => _ = executionService.RunAsync(),
                () => executionService != null && !executionService.IsRunning &&
                      debugService?.IsActive != true &&
                      documentService?.OpenDocuments.Count > 0);
            RegisterBuiltInCommand(DdfEditorCommandIds.StopExecution, "Arresta esecuzione", "Esecuzione",
                () => executionService.Stop(),
                () => executionService?.State == DdfEditorExecutionState.Running);
            RegisterBuiltInCommand(DdfEditorCommandIds.StartDebugging, "Avvia debug", "Debug",
                () => _ = debugService.RunAsync(),
                () => debugService != null && !debugService.IsActive &&
                      executionService?.IsRunning != true &&
                      documentService?.OpenDocuments.Count > 0,
                shortcutText: "F5");
            RegisterBuiltInCommand(DdfEditorCommandIds.ContinueDebugging, "Continua debug", "Debug",
                () => debugService.Continue(),
                () => debugService?.IsPaused == true,
                shortcutText: "F5");
            RegisterBuiltInCommand(DdfEditorCommandIds.StepInto, "Esegui istruzione", "Debug",
                () => debugService.StepInto(),
                () => debugService?.IsPaused == true,
                shortcutText: "F11");
            RegisterBuiltInCommand(DdfEditorCommandIds.StepOver, "Esegui istruzione/routine", "Debug",
                () => debugService.StepOver(),
                () => debugService?.IsPaused == true,
                shortcutText: "F10");
            RegisterBuiltInCommand(DdfEditorCommandIds.StepOut, "Esci da istruzione/routine", "Debug",
                () => debugService.StepOut(),
                () => debugService?.IsPaused == true,
                shortcutText: "Shift+F11");
            RegisterBuiltInCommand(DdfEditorCommandIds.StopDebugging, "Arresta debug", "Debug",
                () => debugService.Stop(),
                () => debugService?.IsActive == true,
                shortcutText: "Shift+F5");
        }

        private void RegisterBuiltInCommand(string id, string title, string category,
            Action execute, Func<bool> canExecute, Func<bool> isChecked = null,
            string shortcutText = null)
        {
            if (commandService.Find(id) != null) return;
            commandService.Register(id, title, category, execute, canExecute,
                isChecked: isChecked, shortcutText: shortcutText,
                shortcutTextProvider: () => ResolveShortcutText(id, shortcutText));
        }

        private string ResolveShortcutText(string commandId, string fallback)
        {
            if (hostConfiguration?.Shortcuts != null &&
                hostConfiguration.Shortcuts.TryGetValue(commandId, out Keys shortcut))
                return shortcut == Keys.None ? string.Empty : ShortcutSettings.Format(shortcut);
            return fallback ?? string.Empty;
        }

        private bool HasEditor() => activeEditor != null && !activeEditor.IsDisposed;

        private bool CanEdit() => HasEditor() && !activeEditor.ReadOnly;

        private bool HasActiveOperation() =>
            preparationService?.IsPreparing == true || executionService?.IsRunning == true ||
            debugService?.IsActive == true;

        private bool CanPrepareScript() => preparationService != null && !HasActiveOperation() &&
            documentService?.OpenDocuments.Count > 0;

        private void EnsureNoActiveOperation(string action)
        {
            if (HasActiveOperation())
                throw new InvalidOperationException("Impossibile " + action +
                    " durante un'esecuzione o una sessione di debug.");
        }

        private void RefreshCommands()
        {
            commandService?.Refresh();
        }

        private DdfEditorAnalysisRequest CreateAnalysisRequest()
        {
            IDdfEditorDocument activeDocument = documentService?.ActiveDocumentInfo;
            DdfLanguagePackage package = languageSession?.ActivePackage ??
                throw new InvalidOperationException("Nessun pacchetto linguaggio è associato alla superficie.");
            var externalRoots = new List<CompilationUnitSyntax>();
            var externalSymbols = new List<DdfDocumentSymbol>();
            foreach (IDdfEditorDocument document in GetAvailableDocuments())
            {
                if (activeDocument != null &&
                    string.Equals(document.Id, activeDocument.Id, StringComparison.Ordinal)) continue;
                DdfLexResult lex = DdfLexer.Lex(document.SourceText, package.Language);
                DdfParseResult parse = DdfParser.Parse(document.SourceText, lex, package.Language);
                externalRoots.Add(parse.Root);
                externalSymbols.AddRange(DdfSymbolIndex.Create(parse, document.SourceText).Symbols);
            }
            return new DdfEditorAnalysisRequest(SourceText, SelectionStart,
                activeDocument?.Version ?? standaloneDocumentVersion,
                package, languageSession.Revision, activeDocument?.Id,
                activeDocument?.FilePath, externalRoots: externalRoots,
                externalSymbols: externalSymbols, computeCompletions: false,
                hostCatalog: hostCatalog?.Snapshot);
        }

        private void hostCatalog_Changed(object sender,
            DdfHostCatalogChangedEventArgs e)
        {
            InvalidateAnalysis();
            RefreshCommands();
            HostCatalogChanged?.Invoke(this, e);
        }

        private IReadOnlyList<IDdfEditorDocument> GetAvailableDocuments()
        {
            IDdfEditorDocument activeDocument = documentService?.ActiveDocumentInfo;
            if (documentService != null) return documentService.OpenDocuments;
            if (activeDocument != null) return new[] { activeDocument };
            return Array.Empty<IDdfEditorDocument>();
        }

        private bool CanNavigateToDefinition()
        {
            if (!HasEditor() || languageSession?.ActivePackage == null || SourceText.Length == 0)
                return false;
            int position = Math.Min(SelectionStart, SourceText.Length - 1);
            DdfLexicalDefinition lexical = languageSession.ActivePackage.Language.LexicalRules;
            if (position >= 0 && lexical.IsIdentifierPart(SourceText[position])) return true;
            return position > 0 && lexical.IsIdentifierPart(SourceText[position - 1]);
        }

        private void InvalidateAnalysis()
        {
            lastAnalysisSnapshot = null;
        }

        private void languageSession_ActiveLanguageChanged(object sender,
            DdfLanguagePackageChangedEventArgs e)
        {
            InvalidateAnalysis();
            RefreshCommands();
        }

        private void AttachExecutionService(IDdfEditorExecutionService service,
            bool ownsService)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (ReferenceEquals(executionService, service))
            {
                ownsExecutionService = ownsExecutionService || ownsService;
                return;
            }
            if (HasActiveOperation())
                throw new InvalidOperationException(
                    "Il servizio di esecuzione non può essere sostituito durante un'operazione attiva.");
            DetachExecutionService();
            executionService = service;
            ownsExecutionService = ownsService;
            executionService.StateChanged += executionService_StateChanged;
            if (executionService is IDdfEditorCooperativeExecutionService cooperative)
                cooperative.CooperativeStateChanged +=
                    executionService_CooperativeStateChanged;
            executionService.OutputReceived += executionService_OutputReceived;
            executionService.ExecutionCompleted += executionService_ExecutionCompleted;
            RefreshCommands();
        }

        private void DetachExecutionService()
        {
            if (executionService != null)
            {
                executionService.StateChanged -= executionService_StateChanged;
                if (executionService is IDdfEditorCooperativeExecutionService cooperative)
                    cooperative.CooperativeStateChanged -=
                        executionService_CooperativeStateChanged;
                executionService.OutputReceived -= executionService_OutputReceived;
                executionService.ExecutionCompleted -= executionService_ExecutionCompleted;
                if (ownsExecutionService) executionService.Dispose();
            }
            executionService = null;
            ownsExecutionService = false;
        }

        private void AttachDebugService(IDdfEditorDebugService service,
            bool ownsService)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (ReferenceEquals(debugService, service))
            {
                ownsDebugService = ownsDebugService || ownsService;
                return;
            }
            if (HasActiveOperation())
                throw new InvalidOperationException(
                    "Il servizio di debug non può essere sostituito durante un'operazione attiva.");
            DetachDebugService();
            debugService = service;
            ownsDebugService = ownsService;
            debugService.StateChanged += debugService_StateChanged;
            if (debugService is IDdfEditorCooperativeExecutionService cooperative)
                cooperative.CooperativeStateChanged +=
                    debugService_CooperativeStateChanged;
            debugService.Paused += debugService_Paused;
            debugService.OutputReceived += debugService_OutputReceived;
            debugService.DebugCompleted += debugService_DebugCompleted;
            RefreshCommands();
        }

        private void DetachDebugService()
        {
            if (debugService != null)
            {
                debugService.StateChanged -= debugService_StateChanged;
                if (debugService is IDdfEditorCooperativeExecutionService cooperative)
                    cooperative.CooperativeStateChanged -=
                        debugService_CooperativeStateChanged;
                debugService.Paused -= debugService_Paused;
                debugService.OutputReceived -= debugService_OutputReceived;
                debugService.DebugCompleted -= debugService_DebugCompleted;
                if (ownsDebugService) debugService.Dispose();
            }
            debugService = null;
            ownsDebugService = false;
        }

        private void debugService_StateChanged(object sender,
            DdfEditorDebugStateChangedEventArgs e)
        {
            DebugStateChanged?.Invoke(this, e);
            if (IsHandleCreated && InvokeRequired)
            {
                try { BeginInvoke(new Action(RefreshCommands)); }
                catch (InvalidOperationException) { }
                return;
            }
            RefreshCommands();
        }

        private void debugService_CooperativeStateChanged(object sender,
            DdfEditorCooperativeStateChangedEventArgs e)
        {
            CooperativeDebugStateChanged?.Invoke(this, e);
        }

        private void executionService_StateChanged(object sender,
            DdfEditorExecutionStateChangedEventArgs e)
        {
            ExecutionStateChanged?.Invoke(this, e);
            if (IsHandleCreated && InvokeRequired)
            {
                try { BeginInvoke(new Action(RefreshCommands)); }
                catch (InvalidOperationException) { }
                return;
            }
            RefreshCommands();
        }

        private void executionService_CooperativeStateChanged(object sender,
            DdfEditorCooperativeStateChangedEventArgs e)
        {
            CooperativeExecutionStateChanged?.Invoke(this, e);
        }

        private void preparationService_StateChanged(object sender,
            DdfEditorPreparationStateChangedEventArgs e)
        {
            PreparationStateChanged?.Invoke(this, e);
            RefreshCommands();
        }

        private void preparationService_PreparationCompleted(object sender,
            DdfEditorPreparationCompletedEventArgs e)
        {
            PreparationCompleted?.Invoke(this, e);
        }

        private void executionService_OutputReceived(object sender,
            DdfEditorExecutionOutputEventArgs e)
        {
            ExecutionOutputReceived?.Invoke(this, e);
        }

        private void executionService_ExecutionCompleted(object sender,
            DdfEditorExecutionCompletedEventArgs e)
        {
            ExecutionCompleted?.Invoke(this, e);
        }

        private void debugService_Paused(object sender,
            DdfEditorDebugPausedEventArgs e)
        {
            DebugPaused?.Invoke(this, e);
        }

        private void debugService_OutputReceived(object sender,
            DdfEditorExecutionOutputEventArgs e)
        {
            DebugOutputReceived?.Invoke(this, e);
        }

        private void debugService_DebugCompleted(object sender,
            DdfEditorExecutionCompletedEventArgs e)
        {
            DebugCompleted?.Invoke(this, e);
        }

        private TService RequireCapability<TService>(string capabilityId)
            where TService : class
        {
            DdfEditorCapabilityResult result = InspectCapability(capabilityId);
            if (result.TryGetService(out TService service)) return service;
            throw new DdfEditorCapabilityUnavailableException(result);
        }

        private void DetachHostConfiguration()
        {
            if (hostConfiguration != null)
                hostConfiguration.ConfigurationChanged -= hostConfiguration_ConfigurationChanged;
            hostConfiguration = null;
            applyHostVisualOptions = false;
        }
    }
}
