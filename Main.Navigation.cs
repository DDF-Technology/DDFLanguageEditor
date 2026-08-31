using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem goToFileMenuItem;
        private ToolStripMenuItem goToSymbolMenuItem;
        private ToolStripMenuItem findReferencesMenuItem;
        private ToolStripMenuItem goToLineMenuItem;
        private ToolStripMenuItem goToLastEditMenuItem;
        private ToolStripButton toolbarGoToFileButton;
        private ToolStripButton toolbarGoToSymbolButton;
        private ToolStripButton toolbarFindReferencesButton;
        private ToolStripButton toolbarGoToLineButton;
        private ToolStripButton toolbarGoToLastEditButton;
        private Func<NavigationForm, DialogResult> showNavigationDialog;

        private void initializeWorkspaceNavigation()
        {
            goToFileMenuItem = createNavigationMenuItem(
                "goToFileMenuItem", "Vai a file...", Keys.Control | Keys.P, goToFileMenuItem_Click);
            goToSymbolMenuItem = createNavigationMenuItem(
                "goToSymbolMenuItem", "Vai a simbolo...", Keys.Control | Keys.Shift | Keys.O, goToSymbolMenuItem_Click);
            findReferencesMenuItem = createNavigationMenuItem(
                "findReferencesMenuItem", "Trova riferimenti", Keys.Shift | Keys.F12, findReferencesMenuItem_Click);
            goToLineMenuItem = createNavigationMenuItem(
                "goToLineMenuItem", "Vai a riga/colonna...", Keys.Control | Keys.G, goToLineMenuItem_Click);
            goToLastEditMenuItem = createNavigationMenuItem(
                "goToLastEditMenuItem", "Vai all'ultima modifica", Keys.Control | Keys.Shift | Keys.Back, goToLastEditMenuItem_Click);

            int insertIndex = editMenuItem.DropDownItems.IndexOf(workspaceReplaceMenuItem) + 1;
            editMenuItem.DropDownItems.Insert(insertIndex++, new ToolStripSeparator());
            editMenuItem.DropDownItems.Insert(insertIndex++, goToFileMenuItem);
            editMenuItem.DropDownItems.Insert(insertIndex++, goToSymbolMenuItem);
            editMenuItem.DropDownItems.Insert(insertIndex++, findReferencesMenuItem);
            editMenuItem.DropDownItems.Insert(insertIndex++, goToLineMenuItem);
            editMenuItem.DropDownItems.Insert(insertIndex, goToLastEditMenuItem);

            toolbarGoToFileButton = createToolbarButton(
                "toolbarGoToFileButton", "\uE8A5", "Vai a file (Ctrl+P)", goToFileMenuItem_Click);
            toolbarGoToSymbolButton = createToolbarButton(
                "toolbarGoToSymbolButton", "\uE71E", "Vai a simbolo (Ctrl+Shift+O)", goToSymbolMenuItem_Click);
            toolbarFindReferencesButton = createToolbarButton(
                "toolbarFindReferencesButton", "\uE8A7", "Trova riferimenti (Shift+F12)", findReferencesMenuItem_Click);
            toolbarGoToLineButton = createToolbarButton(
                "toolbarGoToLineButton", "\uE8EF", "Vai a riga/colonna (Ctrl+G)", goToLineMenuItem_Click);
            toolbarGoToLastEditButton = createToolbarButton(
                "toolbarGoToLastEditButton", "\uE72C", "Vai all'ultima modifica (Ctrl+Shift+Backspace)", goToLastEditMenuItem_Click);
            int toolbarIndex = toolStripMain.Items.IndexOf(toolbarWorkspaceReplaceButton) + 1;
            toolStripMain.Items.Insert(toolbarIndex++, toolbarGoToFileButton);
            toolStripMain.Items.Insert(toolbarIndex++, toolbarGoToSymbolButton);
            toolStripMain.Items.Insert(toolbarIndex++, toolbarFindReferencesButton);
            toolStripMain.Items.Insert(toolbarIndex++, toolbarGoToLineButton);
            toolStripMain.Items.Insert(toolbarIndex, toolbarGoToLastEditButton);

            showNavigationDialog = dialog => dialog.ShowDialog(this);
            initializeNavigationHistory();
        }

        private static ToolStripMenuItem createNavigationMenuItem(
            string name, string text, Keys shortcut, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text) { Name = name, ShortcutKeys = shortcut };
            item.Click += handler;
            return item;
        }

        private void goToFileMenuItem_Click(object sender, EventArgs e) => showWorkspaceNavigation(NavigationMode.File);
        private void goToSymbolMenuItem_Click(object sender, EventArgs e) => showWorkspaceNavigation(NavigationMode.Symbol);
        private void findReferencesMenuItem_Click(object sender, EventArgs e) => showWorkspaceNavigation(NavigationMode.Reference);
        private void goToLineMenuItem_Click(object sender, EventArgs e) => showWorkspaceNavigation(NavigationMode.Line);

        private void showWorkspaceNavigation(NavigationMode initialMode)
        {
            leaveFoldedView();
            IReadOnlyList<DdfWorkspaceSearchDocument> documents = createWorkspaceSearchDocuments();
            IReadOnlyList<DdfWorkspaceNavigationLocation> references = createReferenceLocations(documents, out string referenceName);
            var sources = new Dictionary<NavigationMode, IReadOnlyList<DdfWorkspaceNavigationLocation>>
            {
                { NavigationMode.File, DdfWorkspaceNavigationService.ListFiles(documents) },
                { NavigationMode.Symbol, DdfWorkspaceNavigationService.ListSymbols(documents) },
                { NavigationMode.Reference, references },
                { NavigationMode.Line, Array.Empty<DdfWorkspaceNavigationLocation>() }
            };
            int currentLine = richTextBoxMainEditor.GetLineFromCharIndex(richTextBoxMainEditor.SelectionStart) + 1;
            int currentLineStart = richTextBoxMainEditor.GetFirstCharIndexFromLine(currentLine - 1);
            int currentColumn = richTextBoxMainEditor.SelectionStart - Math.Max(0, currentLineStart) + 1;
            using (var dialog = new NavigationForm(
                sources, initialMode, currentLine, currentColumn,
                Math.Max(1, richTextBoxMainEditor.Lines.Length), referenceName))
            {
                if (showNavigationDialog(dialog) != DialogResult.OK) return;
                if (dialog.SelectedMode == NavigationMode.Line)
                {
                    if (dialog.TryGetLineColumn(out int line, out int column)) navigateToLine(line, column);
                    return;
                }
                navigateToNavigationLocation(dialog.SelectedLocation);
            }
        }

        private IReadOnlyList<DdfWorkspaceNavigationLocation> createReferenceLocations(
            IReadOnlyList<DdfWorkspaceSearchDocument> documents, out string referenceName)
        {
            referenceName = string.Empty;
            DdfSymbolOccurrence occurrence = getCurrentSymbolOccurrence();
            string documentId = openDocuments.ActiveDocument?.Id;
            int declarationStart = occurrence?.Symbol.SelectionStart ?? -1;
            if (occurrence != null) referenceName = occurrence.Symbol.Name;
            else
            {
                DdfWorkspaceSymbol external = getWorkspaceSymbolAtCaret(richTextBoxMainEditor.SelectionStart);
                if (external == null) return Array.Empty<DdfWorkspaceNavigationLocation>();
                DdfWorkspaceSearchDocument document = documents.FirstOrDefault(item =>
                    string.Equals(item.Path, external.Document.Path, StringComparison.OrdinalIgnoreCase));
                if (document == null) return Array.Empty<DdfWorkspaceNavigationLocation>();
                documentId = document.Id;
                declarationStart = external.Symbol.SelectionStart;
                referenceName = external.Symbol.Name;
            }
            return DdfWorkspaceNavigationService.FindReferences(documents, documentId, declarationStart);
        }

        private bool navigateToNavigationLocation(DdfWorkspaceNavigationLocation location)
        {
            if (location == null) return false;
            return navigateWithHistory(() => navigateToNavigationLocationCore(location));
        }

        private bool navigateToNavigationLocationCore(DdfWorkspaceNavigationLocation location)
        {
            OpenDocumentBuffer buffer = openDocuments.Documents.FirstOrDefault(document =>
                string.Equals(document.Id, location.Document.Id, StringComparison.OrdinalIgnoreCase));
            if (buffer != null)
                activateDocument(documentViews[buffer.Id]);
            else if (!string.IsNullOrEmpty(location.Document.Path))
            {
                if (!openDocument(location.Document.Path)) return false;
            }
            else return false;

            int start = Math.Min(location.Start, richTextBoxMainEditor.TextLength);
            int length = Math.Min(location.Length, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
            return true;
        }

        private void navigateToLine(int line, int column)
        {
            navigateWithHistory(() =>
            {
                int position = DdfWorkspaceNavigationService.GetPosition(richTextBoxMainEditor.Text, line, column);
                richTextBoxMainEditor.Select(position, 0);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
        }

        private void goToLastEditMenuItem_Click(object sender, EventArgs e)
        {
            DocumentView view = activeDocumentView;
            if (view?.LastEditPosition == null) return;
            navigateWithHistory(() =>
            {
                int position = Math.Min(view.LastEditPosition.Value, richTextBoxMainEditor.TextLength);
                richTextBoxMainEditor.Select(position, 0);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
        }

        private bool canFindReferences()
        {
            return getCurrentSymbolOccurrence() != null || getWorkspaceSymbolAtCaret(richTextBoxMainEditor.SelectionStart) != null;
        }

        private void updateNavigationCommandState()
        {
            bool referencesAvailable = !richTextBoxFoldedView.Visible && canFindReferences();
            bool lastEditAvailable = !richTextBoxFoldedView.Visible && activeDocumentView?.LastEditPosition != null;
            if (findReferencesMenuItem != null) findReferencesMenuItem.Enabled = referencesAvailable;
            if (goToLastEditMenuItem != null) goToLastEditMenuItem.Enabled = lastEditAvailable;
            if (toolbarFindReferencesButton != null) toolbarFindReferencesButton.Enabled = referencesAvailable;
            if (toolbarGoToLastEditButton != null) toolbarGoToLastEditButton.Enabled = lastEditAvailable;
        }
    }
}
