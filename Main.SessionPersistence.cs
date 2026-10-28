using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private Func<string> loadWorkspaceSessionSetting;
        private Action<string> saveWorkspaceSessionSetting;
        private Action clearWorkspaceSessionSetting;
        private bool workspaceSessionRestored;
        private readonly Dictionary<int, string> restoredSessionDocumentIds = new Dictionary<int, string>();

        private void initializeWorkspaceSessionPersistence()
        {
            loadWorkspaceSessionSetting = AppSettingsStore.LoadWorkspaceSession;
            saveWorkspaceSessionSetting = AppSettingsStore.SaveWorkspaceSession;
            clearWorkspaceSessionSetting = AppSettingsStore.ClearWorkspaceSession;
            updateWorkspaceSessionBehavior();
        }

        private void updateWorkspaceSessionBehavior()
        {
            if (editorSettings?.RestoreSessionEnabled == true) return;
            try { clearWorkspaceSessionSetting?.Invoke(); }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile rimuovere la sessione salvata: " + exception.Message;
            }
        }

        private void persistWorkspaceSession()
        {
            try
            {
                if (editorSettings?.RestoreSessionEnabled != true)
                {
                    clearWorkspaceSessionSetting();
                    return;
                }
                List<OpenDocumentBuffer> buffers = openDocuments.Documents.ToList();
                var snapshot = new WorkspaceSessionSnapshot
                {
                    WorkspacePath = workspaceIndex?.RootPath,
                    ActiveDocumentIndex = Math.Max(0, buffers.FindIndex(buffer => ReferenceEquals(buffer, openDocuments.ActiveDocument))),
                    Documents = buffers.Select(buffer =>
                    {
                        DocumentView view = documentViews[buffer.Id];
                        captureDocumentViewport(view);
                        return new WorkspaceSessionDocumentSnapshot
                        {
                            Path = buffer.Session.CurrentPath,
                            SelectionStart = view.Editor.SelectionStart,
                            SelectionLength = view.Editor.SelectionLength,
                            ScrollX = view.HasScrollPosition ? view.ScrollPosition.X : (int?)null,
                            ScrollY = view.HasScrollPosition ? view.ScrollPosition.Y : (int?)null,
                            BreakpointLines = buffer.BreakpointLines.OrderBy(line => line).ToList()
                        };
                    }).ToList()
                };
                saveWorkspaceSessionSetting(snapshot.Serialize());
            }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare la sessione: " + exception.Message;
            }
        }

        private void restoreWorkspaceSession()
        {
            workspaceSessionRestored = false;
            restoredSessionDocumentIds.Clear();
            if (editorSettings?.RestoreSessionEnabled != true)
            {
                updateWorkspaceSessionBehavior();
                return;
            }

            string serialized;
            try { serialized = loadWorkspaceSessionSetting?.Invoke(); }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile leggere la sessione precedente: " + exception.Message;
                return;
            }
            DdfPersistenceReadResult<WorkspaceSessionSnapshot> read =
                WorkspaceSessionSnapshot.ParseVersioned(serialized);
            WorkspaceSessionSnapshot snapshot = read.Value;
            if (snapshot == null)
            {
                if (!string.IsNullOrWhiteSpace(serialized))
                    statusFileLabel.ToolTipText =
                        "Sessione precedente non applicata; il file originale è stato conservato. " +
                        read.Failure;
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(snapshot.WorkspacePath) && Directory.Exists(snapshot.WorkspacePath))
                    openWorkspace(snapshot.WorkspacePath);

                DocumentView initial = activeDocumentView;
                var restoredByIndex = new Dictionary<int, DocumentView>();
                for (int index = 0; index < snapshot.Documents.Count; index++)
                {
                    WorkspaceSessionDocumentSnapshot document = snapshot.Documents[index];
                    string source;
                    if (string.IsNullOrWhiteSpace(document.Path)) source = string.Empty;
                    else
                    {
                        if (!File.Exists(document.Path)) continue;
                        source = DdfDocumentFile.Load(document.Path);
                    }

                    DocumentView view;
                    if (restoredByIndex.Count == 0) view = initial;
                    else view = addDocumentView(openDocuments.CreateUntitled());
                    restoreWorkspaceSessionDocument(view, document, source);
                    restoredByIndex[index] = view;
                    restoredSessionDocumentIds[index] = view.Buffer.Id;
                }

                if (restoredByIndex.Count == 0) return;
                DocumentView active = restoredByIndex.TryGetValue(snapshot.ActiveDocumentIndex, out DocumentView selected)
                    ? selected
                    : restoredByIndex.OrderBy(pair => Math.Abs(pair.Key - snapshot.ActiveDocumentIndex)).First().Value;
                activateDocument(active);
                refreshRestoredActiveDocument();
                workspaceSessionRestored = true;
                statusFileLabel.ToolTipText = "Sessione precedente ripristinata: " + restoredByIndex.Count +
                    (restoredByIndex.Count == 1 ? " scheda." : " schede.");
            }
            catch (Exception exception) when (isSettingsStorageException(exception) || isDocumentException(exception))
            {
                statusFileLabel.ToolTipText = "Ripristino della sessione non riuscito: " + exception.Message;
            }
        }

        private void refreshRestoredActiveDocument()
        {
            applyHighlighting();
            updateDocumentUi();
            updateLineNumbers();
            updateCaretPosition();
            refreshBreakpointsPalette();
            updateBreadcrumb();
        }

        private void restoreWorkspaceSessionDocument(DocumentView view,
            WorkspaceSessionDocumentSnapshot snapshot, string source)
        {
            isReplacingDocument = true;
            try
            {
                if (string.IsNullOrWhiteSpace(snapshot.Path)) view.Buffer.Session.SetUntitled();
                else view.Buffer.MarkSaved(snapshot.Path);
                view.Editor.Text = source;
                int selectionStart = Math.Max(0, Math.Min(source.Length, snapshot.SelectionStart));
                int selectionLength = Math.Max(0, Math.Min(source.Length - selectionStart, snapshot.SelectionLength));
                view.Editor.Select(selectionStart, selectionLength);
                view.ScrollPosition = new System.Drawing.Point(
                    snapshot.ScrollX.GetValueOrDefault(), snapshot.ScrollY.GetValueOrDefault());
                view.HasScrollPosition = snapshot.ScrollX.HasValue && snapshot.ScrollY.HasValue;
                view.Editor.ClearUndo();
                view.Editor.Modified = false;
                view.Buffer.UpdateSource(view.Editor.Text, false);
                view.Buffer.BreakpointLines.Clear();
                foreach (int line in snapshot.BreakpointLines) view.Buffer.BreakpointLines.Add(line);
                view.LastEditPosition = selectionStart;
                view.LastEditUtc = null;
                rememberCurrentDiskState(view);
                updateDocumentTab(view);
            }
            finally
            {
                isReplacingDocument = false;
            }
        }
    }
}
