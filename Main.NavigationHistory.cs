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
        private const int NavigationHistoryLimit = 200;
        private readonly List<NavigationHistoryEntry> backwardNavigationHistory = new List<NavigationHistoryEntry>();
        private readonly List<NavigationHistoryEntry> forwardNavigationHistory = new List<NavigationHistoryEntry>();
        private ToolStripMenuItem navigateBackMenuItem;
        private ToolStripMenuItem navigateForwardMenuItem;
        private ToolStripButton toolbarNavigateBackButton;
        private ToolStripButton toolbarNavigateForwardButton;
        private bool isRestoringNavigationHistory;

        private void initializeNavigationHistory()
        {
            navigateBackMenuItem = createNavigationMenuItem(
                "navigateBackMenuItem", "Navigazione indietro", Keys.Alt | Keys.Left, navigateBackMenuItem_Click);
            navigateForwardMenuItem = createNavigationMenuItem(
                "navigateForwardMenuItem", "Navigazione avanti", Keys.Alt | Keys.Right, navigateForwardMenuItem_Click);
            int menuIndex = editMenuItem.DropDownItems.IndexOf(goToFileMenuItem);
            editMenuItem.DropDownItems.Insert(menuIndex++, navigateBackMenuItem);
            editMenuItem.DropDownItems.Insert(menuIndex++, navigateForwardMenuItem);
            editMenuItem.DropDownItems.Insert(menuIndex, new ToolStripSeparator());

            toolbarNavigateBackButton = createToolbarButton(
                "toolbarNavigateBackButton", "\uE72B", "Navigazione indietro (Alt+Sinistra)", navigateBackMenuItem_Click);
            toolbarNavigateForwardButton = createToolbarButton(
                "toolbarNavigateForwardButton", "\uE72A", "Navigazione avanti (Alt+Destra)", navigateForwardMenuItem_Click);
            int toolbarIndex = toolStripMain.Items.IndexOf(toolbarGoToFileButton);
            toolStripMain.Items.Insert(toolbarIndex++, toolbarNavigateBackButton);
            toolStripMain.Items.Insert(toolbarIndex, toolbarNavigateForwardButton);
            updateNavigationHistoryCommandState();
        }

        private bool navigateWithHistory(Func<bool> navigation)
        {
            if (navigation == null) return false;
            NavigationHistoryEntry origin = captureNavigationLocation();
            bool succeeded = navigation();
            if (!succeeded || isRestoringNavigationHistory) return succeeded;
            NavigationHistoryEntry destination = captureNavigationLocation();
            if (origin != null && destination != null && !sameNavigationLocation(origin, destination))
            {
                pushNavigationEntry(backwardNavigationHistory, origin);
                forwardNavigationHistory.Clear();
                updateNavigationHistoryCommandState();
            }
            return true;
        }

        private NavigationHistoryEntry captureNavigationLocation()
        {
            OpenDocumentBuffer document = openDocuments.ActiveDocument;
            if (document == null || richTextBoxMainEditor == null) return null;
            return new NavigationHistoryEntry(
                document.Id,
                document.Session.HasPath ? document.Session.CurrentPath : null,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength);
        }

        private void navigateBackMenuItem_Click(object sender, EventArgs e)
        {
            restoreNavigationHistory(backwardNavigationHistory, forwardNavigationHistory);
        }

        private void navigateForwardMenuItem_Click(object sender, EventArgs e)
        {
            restoreNavigationHistory(forwardNavigationHistory, backwardNavigationHistory);
        }

        private void restoreNavigationHistory(
            List<NavigationHistoryEntry> source,
            List<NavigationHistoryEntry> destination)
        {
            NavigationHistoryEntry current = captureNavigationLocation();
            while (source.Count > 0)
            {
                NavigationHistoryEntry target = source[source.Count - 1];
                source.RemoveAt(source.Count - 1);
                if (current != null && sameNavigationLocation(current, target)) continue;
                isRestoringNavigationHistory = true;
                bool restored;
                try { restored = restoreNavigationLocation(target); }
                finally { isRestoringNavigationHistory = false; }
                if (!restored) continue;
                if (current != null) pushNavigationEntry(destination, current);
                break;
            }
            updateNavigationHistoryCommandState();
        }

        private bool restoreNavigationLocation(NavigationHistoryEntry entry)
        {
            if (entry == null) return false;
            OpenDocumentBuffer buffer = openDocuments.Documents.FirstOrDefault(document =>
                string.Equals(document.Id, entry.DocumentId, StringComparison.OrdinalIgnoreCase));
            if (buffer == null && !string.IsNullOrEmpty(entry.Path)) buffer = openDocuments.FindByPath(entry.Path);
            if (buffer != null)
                activateDocument(documentViews[buffer.Id]);
            else
            {
                if (string.IsNullOrEmpty(entry.Path) || !File.Exists(entry.Path) || !openDocument(entry.Path)) return false;
            }
            leaveFoldedView();
            int start = Math.Min(Math.Max(0, entry.SelectionStart), richTextBoxMainEditor.TextLength);
            int length = Math.Min(Math.Max(0, entry.SelectionLength), richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
            return true;
        }

        private void pushNavigationEntry(List<NavigationHistoryEntry> history, NavigationHistoryEntry entry)
        {
            if (entry == null || (history.Count > 0 && sameNavigationLocation(history[history.Count - 1], entry))) return;
            history.Add(entry);
            if (history.Count > NavigationHistoryLimit) history.RemoveAt(0);
        }

        private static bool sameNavigationLocation(NavigationHistoryEntry left, NavigationHistoryEntry right)
        {
            if (left == null || right == null) return false;
            bool sameDocument = string.Equals(left.DocumentId, right.DocumentId, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(left.Path) && !string.IsNullOrEmpty(right.Path) &&
                 string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
            return sameDocument && left.SelectionStart == right.SelectionStart && left.SelectionLength == right.SelectionLength;
        }

        private void updateNavigationHistoryCommandState()
        {
            bool canBack = backwardNavigationHistory.Any(canRestoreNavigationLocation);
            bool canForward = forwardNavigationHistory.Any(canRestoreNavigationLocation);
            if (navigateBackMenuItem != null) navigateBackMenuItem.Enabled = canBack;
            if (navigateForwardMenuItem != null) navigateForwardMenuItem.Enabled = canForward;
            if (toolbarNavigateBackButton != null) toolbarNavigateBackButton.Enabled = canBack;
            if (toolbarNavigateForwardButton != null) toolbarNavigateForwardButton.Enabled = canForward;
        }

        private bool canRestoreNavigationLocation(NavigationHistoryEntry entry)
        {
            if (entry == null) return false;
            if (openDocuments.Documents.Any(document =>
                    string.Equals(document.Id, entry.DocumentId, StringComparison.OrdinalIgnoreCase))) return true;
            return !string.IsNullOrEmpty(entry.Path) && File.Exists(entry.Path);
        }

        private sealed class NavigationHistoryEntry
        {
            public NavigationHistoryEntry(string documentId, string path, int selectionStart, int selectionLength)
            {
                DocumentId = documentId;
                Path = path;
                SelectionStart = selectionStart;
                SelectionLength = selectionLength;
            }

            public string DocumentId { get; }
            public string Path { get; }
            public int SelectionStart { get; }
            public int SelectionLength { get; }
        }
    }
}
