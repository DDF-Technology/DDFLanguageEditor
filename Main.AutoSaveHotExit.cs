using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private Timer autoSaveTimer;
        private Func<DateTime> getUtcNow;
        private Func<string> loadHotExitSnapshotSetting;
        private Action<string> saveHotExitSnapshotSetting;
        private Action clearHotExitSnapshotSetting;

        private void initializeAutoSaveAndHotExit()
        {
            getUtcNow = () => DateTime.UtcNow;
            loadHotExitSnapshotSetting = AppSettingsStore.LoadHotExitSnapshot;
            saveHotExitSnapshotSetting = AppSettingsStore.SaveHotExitSnapshot;
            clearHotExitSnapshotSetting = AppSettingsStore.ClearHotExitSnapshot;
            autoSaveTimer = new Timer { Interval = 1000 };
            autoSaveTimer.Tick += autoSaveTimer_Tick;
            updateAutoSaveBehavior();
        }

        private void updateAutoSaveBehavior()
        {
            if (autoSaveTimer == null) return;
            if (editorSettings?.AutoSaveEnabled == true) autoSaveTimer.Start();
            else autoSaveTimer.Stop();
            if (editorSettings?.HotExitEnabled != true)
            {
                try { clearHotExitSnapshotSetting?.Invoke(); }
                catch (Exception exception) when (isSettingsStorageException(exception))
                {
                    statusFileLabel.ToolTipText = "Impossibile rimuovere i dati Hot Exit: " + exception.Message;
                }
            }
        }

        private void disposeAutoSaveAndHotExit()
        {
            if (autoSaveTimer == null) return;
            autoSaveTimer.Stop();
            autoSaveTimer.Dispose();
            autoSaveTimer = null;
        }

        private void autoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (editorSettings?.AutoSaveEnabled != true || isReplacingDocument) return;
            DateTime now = getUtcNow();
            foreach (DocumentView view in documentViews.Values.ToList())
            {
                if (!view.Buffer.Session.IsDirty || !view.Buffer.Session.HasPath || view.LastEditUtc == null) continue;
                if ((now - view.LastEditUtc.Value).TotalSeconds < editorSettings.AutoSaveDelaySeconds) continue;
                autoSaveDocument(view, now);
            }
        }

        private void autoSaveDocument(DocumentView view, DateTime attemptTime)
        {
            try
            {
                string source = view.Editor.Text;
                string path = view.Buffer.Session.CurrentPath;
                if (!canWriteDocument(view, path))
                {
                    view.LastEditUtc = attemptTime;
                    return;
                }
                DdfDocumentFile.Save(path, editorSettings.ApplyLineEndings(source));
                view.Buffer.UpdateSource(source, false);
                view.Buffer.MarkSaved(path);
                markCrashRecoveryChanged();
                view.Editor.Modified = false;
                view.LastEditUtc = null;
                updateWorkspaceDocument(path, source);
                updateDocumentTab(view);
                rememberCurrentDiskState(view);
                if (ReferenceEquals(view, activeDocumentView)) updateDocumentUi();
                statusFileLabel.ToolTipText = "Salvataggio automatico completato: " + view.Buffer.Session.DisplayName;
            }
            catch (Exception exception) when (isDocumentException(exception))
            {
                view.LastEditUtc = attemptTime;
                statusFileLabel.ToolTipText = "Salvataggio automatico non riuscito: " + exception.Message;
            }
        }

        private bool prepareHotExit()
        {
            try
            {
                List<DocumentView> dirtyViews = openDocuments.Documents
                    .Where(buffer => buffer.Session.IsDirty)
                    .Select(buffer => documentViews[buffer.Id])
                    .ToList();
                if (dirtyViews.Count == 0)
                {
                    clearHotExitSnapshotSetting();
                    return true;
                }

                var snapshot = new HotExitSnapshot
                {
                    ActiveDocumentIndex = Math.Max(0, dirtyViews.FindIndex(view => ReferenceEquals(view, activeDocumentView))),
                    Documents = dirtyViews.Select(view => new HotExitDocumentSnapshot
                    {
                        SessionDocumentIndex = openDocuments.Documents.ToList().FindIndex(buffer =>
                            ReferenceEquals(buffer, view.Buffer)),
                        Path = view.Buffer.Session.CurrentPath,
                        Source = view.Editor.Text,
                        SelectionStart = view.Editor.SelectionStart,
                        SelectionLength = view.Editor.SelectionLength,
                        BreakpointLines = view.Buffer.BreakpointLines.OrderBy(line => line).ToList()
                    }).ToList()
                };
                saveHotExitSnapshotSetting(snapshot.Serialize());
                return true;
            }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                MessageBox.Show(this,
                    "Hot Exit non ha potuto conservare i buffer. Verrà usata la normale richiesta di salvataggio.\n\n" + exception.Message,
                    "Hot Exit non disponibile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void restoreHotExitBuffers()
        {
            if (editorSettings?.HotExitEnabled != true)
            {
                try { clearHotExitSnapshotSetting?.Invoke(); }
                catch (Exception exception) when (isSettingsStorageException(exception))
                {
                    statusFileLabel.ToolTipText = "Impossibile rimuovere i dati Hot Exit: " + exception.Message;
                }
                return;
            }

            string serialized;
            try { serialized = loadHotExitSnapshotSetting?.Invoke(); }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile leggere i dati Hot Exit: " + exception.Message;
                return;
            }
            HotExitSnapshot snapshot = HotExitSnapshot.Parse(serialized);
            if (snapshot == null)
            {
                if (!string.IsNullOrWhiteSpace(serialized))
                {
                    try { clearHotExitSnapshotSetting?.Invoke(); }
                    catch (Exception exception) when (isSettingsStorageException(exception))
                    {
                        statusFileLabel.ToolTipText = "Impossibile rimuovere dati Hot Exit non validi: " + exception.Message;
                    }
                }
                return;
            }

            try
            {
                DocumentView sessionActive = activeDocumentView;
                var restored = new List<DocumentView>();
                var usedDocumentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < snapshot.Documents.Count; index++)
                {
                    HotExitDocumentSnapshot document = snapshot.Documents[index];
                    DocumentView view = findHotExitTarget(document, usedDocumentIds);
                    if (view == null)
                    {
                        OpenDocumentBuffer buffer = openDocuments.CreateUntitled();
                        view = addDocumentView(buffer);
                    }
                    restoreHotExitDocument(view, document);
                    usedDocumentIds.Add(view.Buffer.Id);
                    restored.Add(view);
                }
                activateDocument(workspaceSessionRestored && sessionActive != null
                    ? sessionActive
                    : restored[snapshot.ActiveDocumentIndex]);
                refreshRestoredActiveDocument();
                clearHotExitSnapshotSetting();
                statusFileLabel.ToolTipText = restored.Count == 1
                    ? "Hot Exit: ripristinato un buffer non salvato."
                    : "Hot Exit: ripristinati " + restored.Count + " buffer non salvati.";
            }
            catch (Exception exception) when (isSettingsStorageException(exception) || isDocumentException(exception))
            {
                statusFileLabel.ToolTipText = "Ripristino Hot Exit non riuscito: " + exception.Message;
            }
        }

        private DocumentView findHotExitTarget(HotExitDocumentSnapshot snapshot, ISet<string> usedDocumentIds)
        {
            if (snapshot.SessionDocumentIndex >= 0 &&
                restoredSessionDocumentIds.TryGetValue(snapshot.SessionDocumentIndex, out string restoredId) &&
                !usedDocumentIds.Contains(restoredId) && documentViews.TryGetValue(restoredId, out DocumentView indexed))
                return indexed;
            DocumentView match = documentViews.Values.FirstOrDefault(view =>
                !usedDocumentIds.Contains(view.Buffer.Id) &&
                (string.IsNullOrWhiteSpace(snapshot.Path)
                    ? !view.Buffer.Session.HasPath
                    : view.Buffer.Session.HasPath && string.Equals(view.Buffer.Session.CurrentPath,
                        Path.GetFullPath(snapshot.Path), StringComparison.OrdinalIgnoreCase)));
            if (match != null) return match;
            if (!workspaceSessionRestored && usedDocumentIds.Count == 0) return activeDocumentView;
            return null;
        }

        private void restoreHotExitDocument(DocumentView view, HotExitDocumentSnapshot snapshot)
        {
            isReplacingDocument = true;
            try
            {
                if (string.IsNullOrWhiteSpace(snapshot.Path)) view.Buffer.Session.SetUntitled();
                else view.Buffer.MarkSaved(snapshot.Path);
                view.Editor.Text = snapshot.Source;
                view.Editor.Select(snapshot.SelectionStart, snapshot.SelectionLength);
                view.Editor.ClearUndo();
                view.Editor.Modified = true;
                view.Buffer.UpdateSource(snapshot.Source);
                view.Buffer.BreakpointLines.Clear();
                foreach (int line in snapshot.BreakpointLines) view.Buffer.BreakpointLines.Add(line);
                view.LastEditUtc = getUtcNow();
                rememberCurrentDiskState(view);
                updateDocumentTab(view);
            }
            finally
            {
                isReplacingDocument = false;
            }
        }

        private static bool isSettingsStorageException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is ArgumentException || exception is NotSupportedException;
        }
    }
}
