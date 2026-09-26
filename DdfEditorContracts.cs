using System;
using System.Collections.Generic;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Identifies the public contract implemented by the first embeddable editor surface.
    /// Compatibility is independent from the standalone application version.
    /// </summary>
    public static class DdfEditorSdkContract
    {
        public const string CurrentVersion = "0.31";
    }

    /// <summary>
    /// Minimal host-facing editing contract. Additional document, language and
    /// execution services will be introduced without exposing MainForm internals.
    /// </summary>
    public interface IDdfEditorSurface
    {
        string ContractVersion { get; }
        string SourceText { get; set; }
        int SelectionStart { get; }
        int SelectionLength { get; }
        bool EditorReadOnly { get; set; }
        bool WordWrap { get; set; }
        IDdfEditorDocumentService DocumentService { get; }
        IDdfEditorPaletteService PaletteService { get; }
        IDdfEditorHostConfiguration HostConfiguration { get; }
        IDdfLanguagePackageSession LanguageSession { get; }
        IDdfHostCatalog HostCatalog { get; }
        IDdfEditorExperienceState ExperienceState { get; }
        IDdfEditorAnalysisService AnalysisService { get; }
        DdfEditorAnalysisSnapshot LastAnalysisSnapshot { get; }
        IDdfEditorCommandService CommandService { get; }
        IDdfEditorPersistenceProvider PersistenceProvider { get; }
        IDdfEditorProjectContainer ProjectContainer { get; }
        DdfEditorProjectState CurrentProject { get; }
        IDdfEditorBuildService BuildService { get; }
        IDdfEditorExecutionService ExecutionService { get; }
        IDdfEditorDebugService DebugService { get; }

        event EventHandler SourceTextChanged;
        event EventHandler EditorSelectionChanged;
        event EventHandler<DdfEditorSelectionChangedEventArgs> SelectionSnapshotChanged;
        event EventHandler ActiveEditorChanged;
        event EventHandler<DdfEditorDocumentEventArgs> DocumentAdded;
        event EventHandler<DdfEditorDocumentEventArgs> ActiveDocumentChanged;
        event EventHandler<DdfEditorDocumentChangedEventArgs> DocumentChanged;
        event EventHandler<DdfEditorDocumentEventArgs> DocumentClosed;
        event EventHandler<DdfEditorAnalysisCompletedEventArgs> AnalysisCompleted;
        event EventHandler<DdfEditorAnalysisCompletedEventArgs> DiagnosticsUpdated;
        event EventHandler<DdfHostCatalogChangedEventArgs> HostCatalogChanged;
        event EventHandler<DdfEditorDefinitionNavigatedEventArgs> DefinitionNavigated;
        event EventHandler<DdfEditorProjectEventArgs> ProjectSaved;
        event EventHandler<DdfEditorProjectEventArgs> ProjectOpened;
        event EventHandler<DdfEditorBuildStateChangedEventArgs> BuildStateChanged;
        event EventHandler<DdfEditorBuildCompletedEventArgs> BuildCompleted;
        event EventHandler<DdfEditorExecutionStateChangedEventArgs> ExecutionStateChanged;
        event EventHandler<DdfEditorExecutionOutputEventArgs> ExecutionOutputReceived;
        event EventHandler<DdfEditorExecutionCompletedEventArgs> ExecutionCompleted;
        event EventHandler<DdfEditorDebugStateChangedEventArgs> DebugStateChanged;
        event EventHandler<DdfEditorDebugPausedEventArgs> DebugPaused;
        event EventHandler<DdfEditorExecutionOutputEventArgs> DebugOutputReceived;
        event EventHandler<DdfEditorExecutionCompletedEventArgs> DebugCompleted;

        void SetSelection(int start, int length);
        bool FocusEditor();
        DdfEditorAnalysisSnapshot AnalyzeDocument();
        bool TryGoToDefinition();
        DdfEditorBuildSnapshot ValidateProject(DdfEditorBuildRequest request = null);
        DdfEditorBuildSnapshot BuildProject(DdfEditorBuildRequest request = null);
        DdfEditorPersistedState CapturePersistenceState();
        void ApplyPersistenceState(DdfEditorPersistedState state);
        void SaveState();
        bool RestoreState();
        DdfEditorProjectState SaveProject(string projectId, string displayName,
            IEnumerable<KeyValuePair<string, string>> metadata = null);
        DdfEditorProjectState SaveProject();
        bool OpenProject();
        void AttachDocumentService(IDdfEditorDocumentService documentService);
        void AttachPaletteService(IDdfEditorPaletteService paletteService);
        void AttachHostConfiguration(IDdfEditorHostConfiguration configuration);
        void AttachLanguageSession(IDdfLanguagePackageSession languageSession);
        void AttachHostCatalog(IDdfHostCatalog hostCatalog);
        void AttachExperienceState(IDdfEditorExperienceState experienceState);
        void AttachAnalysisService(IDdfEditorAnalysisService analysisService);
        void AttachCommandService(IDdfEditorCommandService commandService);
        void AttachPersistenceProvider(IDdfEditorPersistenceProvider persistenceProvider);
        void AttachProjectContainer(IDdfEditorProjectContainer projectContainer);
        void AttachBuildService(IDdfEditorBuildService buildService);
        void AttachExecutionService(IDdfEditorExecutionService executionService);
        void AttachDebugService(IDdfEditorDebugService debugService);
    }

    public sealed class DdfEditorSelectionChangedEventArgs : EventArgs
    {
        public DdfEditorSelectionChangedEventArgs(string documentId,
            long documentVersion, int start, int length)
        {
            DocumentId = documentId ?? string.Empty;
            DocumentVersion = documentVersion;
            Start = start;
            Length = length;
        }

        public string DocumentId { get; }
        public long DocumentVersion { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfEditorAnalysisCompletedEventArgs : EventArgs
    {
        public DdfEditorAnalysisCompletedEventArgs(DdfEditorAnalysisSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public DdfEditorAnalysisSnapshot Snapshot { get; }
    }

    public sealed class DdfEditorDefinitionNavigatedEventArgs : EventArgs
    {
        public DdfEditorDefinitionNavigatedEventArgs(DdfEditorNavigationTarget target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public DdfEditorNavigationTarget Target { get; }
    }

    /// <summary>
    /// Richiesta di persistenza di un documento virtuale inviata dalla shell embedded
    /// all'applicazione host. La shell non sceglie autonomamente file o database.
    /// </summary>
    public sealed class DdfEmbeddedDocumentSaveEventArgs : EventArgs
    {
        public DdfEmbeddedDocumentSaveEventArgs(string documentId, string displayName,
            string sourceText)
        {
            DocumentId = documentId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
        }

        public string DocumentId { get; }
        public string DisplayName { get; }
        public string SourceText { get; }
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
    }
}
