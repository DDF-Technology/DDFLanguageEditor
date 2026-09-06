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
        private readonly Dictionary<string, KnownDiskState> knownDiskStates =
            new Dictionary<string, KnownDiskState>(StringComparer.OrdinalIgnoreCase);
        private Timer externalChangeTimer;
        private Func<ExternalChangeForm, DialogResult> showExternalChangeDialog;
        private bool isResolvingExternalChange;

        private void initializeExternalChangeMonitoring()
        {
            showExternalChangeDialog = dialog => dialog.ShowDialog(this);
            externalChangeTimer = new Timer { Interval = 2000 };
            externalChangeTimer.Tick += externalChangeTimer_Tick;
            externalChangeTimer.Start();
        }

        private void disposeExternalChangeMonitoring()
        {
            if (externalChangeTimer == null) return;
            externalChangeTimer.Stop();
            externalChangeTimer.Dispose();
            externalChangeTimer = null;
        }

        private void externalChangeTimer_Tick(object sender, EventArgs e)
        {
            if (isResolvingExternalChange || isReplacingDocument || IsDisposed || !Visible) return;
            foreach (OpenDocumentBuffer buffer in openDocuments.Documents.ToList())
            {
                if (!buffer.Session.HasPath || !documentViews.TryGetValue(buffer.Id, out DocumentView view)) continue;
                if (detectAndResolveExternalChange(view, false) != ExternalChangeOutcome.None) break;
            }
        }

        private void rememberCurrentDiskState(DocumentView view)
        {
            if (view == null) return;
            if (!view.Buffer.Session.HasPath)
            {
                knownDiskStates.Remove(view.Buffer.Id);
                return;
            }

            try
            {
                knownDiskStates[view.Buffer.Id] = readDiskState(view.Buffer.Session.CurrentPath);
            }
            catch (Exception exception) when (isDocumentException(exception))
            {
                statusFileLabel.ToolTipText = "Impossibile registrare lo stato del file: " + exception.Message;
            }
        }

        private ExternalChangeOutcome detectAndResolveExternalChange(DocumentView view, bool beforeWrite)
        {
            if (view == null || !view.Buffer.Session.HasPath) return ExternalChangeOutcome.None;
            string path = view.Buffer.Session.CurrentPath;
            KnownDiskState current;
            try
            {
                current = readDiskState(path);
            }
            catch (Exception exception) when (isDocumentException(exception))
            {
                statusFileLabel.ToolTipText = "Controllo modifiche esterne non riuscito: " + exception.Message;
                return beforeWrite ? ExternalChangeOutcome.Postponed : ExternalChangeOutcome.None;
            }

            if (!knownDiskStates.TryGetValue(view.Buffer.Id, out KnownDiskState known))
            {
                knownDiskStates[view.Buffer.Id] = current;
                return ExternalChangeOutcome.None;
            }
            if (current.Exists && hasSameEditorContent(current.Source, view.Editor.Text))
            {
                synchronizeMatchingDiskAndBuffer(view, current);
                return ExternalChangeOutcome.None;
            }
            if (known.HasSameContent(current))
            {
                known.Postponed = null;
                return ExternalChangeOutcome.None;
            }
            if (!beforeWrite && known.Postponed != null && known.Postponed.HasSameContent(current))
                return ExternalChangeOutcome.None;

            isResolvingExternalChange = true;
            try
            {
                using (var dialog = new ExternalChangeForm(path, view.Editor.Text, current.Source,
                    current.Exists, view.Buffer.Session.IsDirty))
                {
                    DialogResult result = showExternalChangeDialog(dialog);
                    if (result == DialogResult.Yes && current.Exists)
                    {
                        reloadDocumentFromExternalState(view, current);
                        return ExternalChangeOutcome.Reloaded;
                    }
                    else if (result == DialogResult.No)
                    {
                        knownDiskStates[view.Buffer.Id] = current;
                        view.Buffer.UpdateSource(view.Editor.Text);
                        view.Editor.Modified = true;
                        view.LastEditUtc = getUtcNow?.Invoke() ?? DateTime.UtcNow;
                        markCrashRecoveryChanged();
                        updateDocumentTab(view);
                        if (ReferenceEquals(view, activeDocumentView)) updateDocumentUi();
                        statusFileLabel.ToolTipText = "Conservata la versione nell'editor: " + view.Buffer.Session.DisplayName;
                        return ExternalChangeOutcome.Kept;
                    }
                    else
                    {
                        known.Postponed = current;
                        statusFileLabel.ToolTipText = "Decisione sulla modifica esterna rimandata: " +
                            view.Buffer.Session.DisplayName;
                        return ExternalChangeOutcome.Postponed;
                    }
                }
            }
            finally
            {
                isResolvingExternalChange = false;
            }
        }

        private void synchronizeMatchingDiskAndBuffer(DocumentView view, KnownDiskState disk)
        {
            knownDiskStates[view.Buffer.Id] = disk;
            if (!view.Buffer.Session.IsDirty) return;
            view.Buffer.UpdateSource(view.Editor.Text, false);
            view.Buffer.MarkSaved(view.Buffer.Session.CurrentPath);
            view.Editor.Modified = false;
            view.LastEditUtc = null;
            markCrashRecoveryChanged();
            updateWorkspaceDocument(view.Buffer.Session.CurrentPath, view.Editor.Text);
            updateDocumentTab(view);
            if (ReferenceEquals(view, activeDocumentView)) updateDocumentUi();
            statusFileLabel.ToolTipText = "La versione su disco coincide già con il buffer: " +
                view.Buffer.Session.DisplayName;
        }

        private static bool hasSameEditorContent(string left, string right) => string.Equals(
            normalizeLineEndings(left), normalizeLineEndings(right), StringComparison.Ordinal);

        private static string normalizeLineEndings(string source) =>
            (source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

        private bool canWriteDocument(DocumentView view, string targetPath)
        {
            if (view == null || !view.Buffer.Session.HasPath ||
                !string.Equals(view.Buffer.Session.CurrentPath, Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                return true;
            ExternalChangeOutcome outcome = detectAndResolveExternalChange(view, true);
            return outcome == ExternalChangeOutcome.None || outcome == ExternalChangeOutcome.Kept;
        }

        private void reloadDocumentFromExternalState(DocumentView view, KnownDiskState disk)
        {
            if (ReferenceEquals(view, activeDocumentView) && richTextBoxFoldedView?.Visible == true)
                leaveFoldedView(getFoldedSourceCaret());
            string previousSource = view.Editor.Text;
            int selectionStart = Math.Min(view.Editor.SelectionStart, disk.Source.Length);
            DdfBreakpointRemapResult remap = DdfBreakpointService.Remap(previousSource, disk.Source,
                view.Buffer.BreakpointLines);
            isReplacingDocument = true;
            try
            {
                view.Editor.Text = disk.Source;
                view.Editor.Select(selectionStart, 0);
                view.Editor.ClearUndo();
                view.Editor.Modified = false;
                view.Buffer.UpdateSource(disk.Source, false);
                view.Buffer.MarkSaved(view.Buffer.Session.CurrentPath);
                view.Buffer.BreakpointLines.Clear();
                foreach (int line in remap.Lines) view.Buffer.BreakpointLines.Add(line);
                view.Buffer.UnboundBreakpointLines.Clear();
                foreach (int line in remap.UnboundLines) view.Buffer.UnboundBreakpointLines.Add(line);
                view.LastEditPosition = selectionStart;
                view.LastEditUtc = null;
                knownDiskStates[view.Buffer.Id] = disk;
                updateWorkspaceDocument(view.Buffer.Session.CurrentPath, disk.Source);
                updateDocumentTab(view);
                markCrashRecoveryChanged();
            }
            finally
            {
                isReplacingDocument = false;
            }

            if (ReferenceEquals(view, activeDocumentView))
            {
                resetActiveAnalysis();
                applyHighlighting();
                updateDocumentUi();
                updateLineNumbers();
                updateCaretPosition();
                updateBreadcrumb();
            }
            refreshBreakpointsPalette();
            scheduleWorkspaceSearch();
            statusFileLabel.ToolTipText = "Ricaricata la versione su disco: " + view.Buffer.Session.DisplayName;
        }

        private static KnownDiskState readDiskState(string path)
        {
            bool exists = File.Exists(path);
            return new KnownDiskState(exists, exists ? DdfDocumentFile.Load(path) : string.Empty);
        }

        private sealed class KnownDiskState
        {
            public KnownDiskState(bool exists, string source)
            {
                Exists = exists;
                Source = source ?? string.Empty;
            }

            public bool Exists { get; }
            public string Source { get; }
            public KnownDiskState Postponed { get; set; }

            public bool HasSameContent(KnownDiskState other)
            {
                return other != null && Exists == other.Exists &&
                    hasSameEditorContent(Source, other.Source);
            }
        }

        private enum ExternalChangeOutcome
        {
            None,
            Reloaded,
            Kept,
            Postponed
        }
    }
}
