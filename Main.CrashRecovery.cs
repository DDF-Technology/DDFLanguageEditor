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
        private Timer crashRecoveryTimer;
        private long crashRecoveryVersion;
        private long lastCrashRecoveryVersion = -1;
        private Func<string> loadCrashRecoverySetting;
        private Action<string> saveCrashRecoverySetting;
        private Action clearCrashRecoverySetting;
        private Func<RecoveryForm, DialogResult> showRecoveryDialog;

        private void initializeCrashRecovery()
        {
            loadCrashRecoverySetting = AppSettingsStore.LoadCrashRecovery;
            saveCrashRecoverySetting = AppSettingsStore.SaveCrashRecovery;
            clearCrashRecoverySetting = AppSettingsStore.ClearCrashRecovery;
            showRecoveryDialog = dialog => dialog.ShowDialog(this);
            crashRecoveryTimer = new Timer { Interval = 5000 };
            crashRecoveryTimer.Tick += crashRecoveryTimer_Tick;
            updateCrashRecoveryBehavior();
        }

        private void updateCrashRecoveryBehavior()
        {
            if (crashRecoveryTimer == null) return;
            if (editorSettings?.CrashRecoveryEnabled == true) crashRecoveryTimer.Start();
            else
            {
                crashRecoveryTimer.Stop();
                clearCrashRecoverySafely();
            }
        }

        private void disposeCrashRecovery()
        {
            if (crashRecoveryTimer == null) return;
            crashRecoveryTimer.Stop();
            crashRecoveryTimer.Dispose();
            crashRecoveryTimer = null;
        }

        private void markCrashRecoveryChanged()
        {
            crashRecoveryVersion++;
        }

        private void crashRecoveryTimer_Tick(object sender, EventArgs e)
        {
            if (editorSettings?.CrashRecoveryEnabled != true || isReplacingDocument) return;
            bool hasDirtyDocuments = openDocuments.Documents.Any(buffer => buffer.Session.IsDirty);
            if (!hasDirtyDocuments)
            {
                clearCrashRecoverySafely();
                lastCrashRecoveryVersion = crashRecoveryVersion;
                return;
            }
            if (lastCrashRecoveryVersion == crashRecoveryVersion) return;
            persistCrashRecovery();
        }

        private void persistCrashRecovery()
        {
            try
            {
                List<OpenDocumentBuffer> allBuffers = openDocuments.Documents.ToList();
                List<DocumentView> dirtyViews = allBuffers.Where(buffer => buffer.Session.IsDirty)
                    .Select(buffer => documentViews[buffer.Id])
                    .Where(view => view.Editor != null && !view.Editor.IsDisposed).ToList();
                if (dirtyViews.Count == 0)
                {
                    clearCrashRecoverySetting();
                    lastCrashRecoveryVersion = crashRecoveryVersion;
                    return;
                }
                var snapshot = new CrashRecoverySnapshot
                {
                    SavedAtUtc = getUtcNow(),
                    ActiveDocumentIndex = Math.Max(0, dirtyViews.FindIndex(view => ReferenceEquals(view, activeDocumentView))),
                    Documents = dirtyViews.Select(view => new HotExitDocumentSnapshot
                    {
                        SessionDocumentIndex = allBuffers.FindIndex(buffer => ReferenceEquals(buffer, view.Buffer)),
                        Path = view.Buffer.Session.CurrentPath,
                        Source = view.Editor.Text,
                        SelectionStart = view.Editor.SelectionStart,
                        SelectionLength = view.Editor.SelectionLength,
                        BreakpointLines = view.Buffer.BreakpointLines.OrderBy(line => line).ToList()
                    }).ToList()
                };
                saveCrashRecoverySetting(snapshot.Serialize());
                lastCrashRecoveryVersion = crashRecoveryVersion;
            }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Recovery locale non riuscita: " + exception.Message;
            }
        }

        private bool restoreCrashRecoveryBuffers()
        {
            if (editorSettings?.CrashRecoveryEnabled != true)
            {
                clearCrashRecoverySafely();
                return false;
            }
            string serialized;
            try { serialized = loadCrashRecoverySetting?.Invoke(); }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile leggere il recovery locale: " + exception.Message;
                return false;
            }
            DdfPersistenceReadResult<CrashRecoverySnapshot> read =
                CrashRecoverySnapshot.ParseVersioned(serialized);
            CrashRecoverySnapshot snapshot = read.Value;
            if (snapshot == null)
            {
                if (!string.IsNullOrWhiteSpace(serialized))
                    statusFileLabel.ToolTipText =
                        "Recovery non applicato; lo snapshot originale è stato conservato. " +
                        read.Failure;
                return false;
            }

            using (var dialog = new RecoveryForm(snapshot))
            {
                DialogResult choice = showRecoveryDialog(dialog);
                if (choice == DialogResult.No)
                {
                    clearCrashRecoverySafely();
                    return false;
                }
                if (choice != DialogResult.Yes) return false;
            }

            try
            {
                DocumentView sessionActive = activeDocumentView;
                var restored = new List<DocumentView>();
                var usedDocumentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (HotExitDocumentSnapshot document in snapshot.Documents)
                {
                    DocumentView view = findHotExitTarget(document, usedDocumentIds);
                    if (view == null) view = addDocumentView(openDocuments.CreateUntitled());
                    restoreHotExitDocument(view, document);
                    usedDocumentIds.Add(view.Buffer.Id);
                    restored.Add(view);
                }
                activateDocument(workspaceSessionRestored && sessionActive != null
                    ? sessionActive
                    : restored[snapshot.ActiveDocumentIndex]);
                refreshRestoredActiveDocument();
                clearCrashRecoverySetting();
                markCrashRecoveryChanged();
                lastCrashRecoveryVersion = -1;
                statusFileLabel.ToolTipText = restored.Count == 1
                    ? "Recovery: ripristinato un buffer senza modificare il file originale."
                    : "Recovery: ripristinati " + restored.Count + " buffer senza modificare i file originali.";
                return true;
            }
            catch (Exception exception) when (isSettingsStorageException(exception) || isDocumentException(exception))
            {
                statusFileLabel.ToolTipText = "Ripristino recovery non riuscito: " + exception.Message;
                return false;
            }
        }

        private void clearCrashRecoverySafely()
        {
            try { clearCrashRecoverySetting?.Invoke(); }
            catch (Exception exception) when (isSettingsStorageException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile rimuovere il recovery locale: " + exception.Message;
            }
        }
    }
}
