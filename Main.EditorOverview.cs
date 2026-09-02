using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private EditorOverviewBar editorOverviewBar;
        private ToolStripMenuItem showOverviewMenuItem;
        private ToolStripButton toolbarOverviewButton;
        private IReadOnlyList<DdfWorkspaceSearchResult> activeWorkspaceSearchResults = Array.Empty<DdfWorkspaceSearchResult>();
        private string overviewDiagnosticsSource = string.Empty;

        private void initializeEditorOverview()
        {
            editorOverviewBar = new EditorOverviewBar();
            editorOverviewBar.TargetEditor = richTextBoxMainEditor;
            editorOverviewBar.LineRequested += editorOverviewBar_LineRequested;
            editorHost.Controls.Add(editorOverviewBar);
            editorOverviewBar.BringToFront();

            showOverviewMenuItem = new ToolStripMenuItem("Mostra overview diagnostica")
            {
                Name = "showOverviewMenuItem",
                CheckOnClick = false
            };
            showOverviewMenuItem.Click += showOverviewMenuItem_Click;
            int visualIndex = viewMenuItem.DropDownItems.IndexOf(wordWrapMenuItem);
            viewMenuItem.DropDownItems.Insert(visualIndex + 1, showOverviewMenuItem);

            toolbarOverviewButton = createToolbarButton(
                "toolbarOverviewButton", "▥", "Overview diagnostica", showOverviewMenuItem_Click);
            toolbarOverviewButton.CheckOnClick = false;
            toolbarOverviewButton.Font = new Font("Segoe UI Symbol", 11F);
            int toolbarIndex = toolStripMain.Items.IndexOf(toolbarWordWrapButton);
            toolStripMain.Items.Insert(toolbarIndex + 1, toolbarOverviewButton);
            applyEditorOverviewSettings();
        }

        private void showOverviewMenuItem_Click(object sender, EventArgs e)
        {
            editorSettings = editorSettings.WithOverview(!editorSettings.ShowOverview);
            persistEditorSettings();
            applyEditorOverviewSettings();
        }

        private void applyEditorOverviewSettings()
        {
            if (editorOverviewBar == null || editorSettings == null) return;
            bool visible = editorSettings.ShowOverview;
            editorOverviewBar.Visible = visible;
            showOverviewMenuItem.Checked = visible;
            toolbarOverviewButton.Checked = visible;
            editorOverviewBar.BackColor = editorSettings.Theme == EditorTheme.Dark
                ? Color.FromArgb(37, 37, 38)
                : Color.FromArgb(245, 245, 245);
            editorOverviewBar.ForeColor = editorSettings.Theme == EditorTheme.Dark
                ? Color.FromArgb(190, 190, 190)
                : Color.FromArgb(80, 80, 80);
            if (visible)
            {
                editorOverviewBar.TargetEditor = richTextBoxMainEditor;
                editorOverviewBar.BringToFront();
                refreshEditorOverview();
            }
        }

        private void refreshEditorOverview()
        {
            if (editorOverviewBar == null || !editorOverviewBar.Visible || richTextBoxMainEditor == null) return;
            int lineCount = Math.Max(1, richTextBoxMainEditor.GetLineFromCharIndex(richTextBoxMainEditor.TextLength) + 1);
            var markers = new List<EditorOverviewMarker>();
            bool diagnosticsCurrent = string.Equals(
                overviewDiagnosticsSource, richTextBoxMainEditor.Text, StringComparison.Ordinal);
            foreach (DdfDiagnostic diagnostic in diagnosticsCurrent
                ? activeDiagnostics ?? Array.Empty<DdfDiagnostic>()
                : Array.Empty<DdfDiagnostic>())
            {
                int position = Math.Max(0, Math.Min(diagnostic.Start, richTextBoxMainEditor.TextLength));
                int line = richTextBoxMainEditor.GetLineFromCharIndex(position) + 1;
                markers.Add(new EditorOverviewMarker(line,
                    diagnostic.Severity == DdfDiagnosticSeverity.Error
                        ? EditorOverviewMarkerKind.Error
                        : EditorOverviewMarkerKind.Warning,
                    position));
            }
            foreach (int line in activeBreakpointLines ?? new HashSet<int>())
            {
                int position = richTextBoxMainEditor.GetFirstCharIndexFromLine(Math.Max(0, line - 1));
                markers.Add(new EditorOverviewMarker(line, EditorOverviewMarkerKind.Breakpoint, position));
            }

            OpenDocumentBuffer active = openDocuments.ActiveDocument;
            string activePath = active?.Session.CurrentPath;
            foreach (DdfWorkspaceSearchResult result in activeWorkspaceSearchResults ?? Array.Empty<DdfWorkspaceSearchResult>())
            {
                bool sameDocument = active != null && (string.Equals(result.Document.Id, active.Id, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(activePath) && !string.IsNullOrWhiteSpace(result.Document.Path) &&
                     string.Equals(result.Document.Path, activePath, StringComparison.OrdinalIgnoreCase)));
                if (sameDocument)
                {
                    int position = Math.Max(0, Math.Min(result.Start, richTextBoxMainEditor.TextLength));
                    markers.Add(new EditorOverviewMarker(
                        richTextBoxMainEditor.GetLineFromCharIndex(position) + 1,
                        EditorOverviewMarkerKind.Search,
                        position));
                }
            }
            editorOverviewBar.SetMarkers(lineCount, markers);
            refreshEditorOverviewViewport();
        }

        private void refreshEditorOverviewViewport()
        {
            if (editorOverviewBar == null || !editorOverviewBar.Visible || richTextBoxMainEditor == null ||
                richTextBoxMainEditor.ClientSize.Height <= 0) return;
            int firstIndex = richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, 1));
            int lastIndex = richTextBoxMainEditor.GetCharIndexFromPosition(
                new Point(1, Math.Max(1, richTextBoxMainEditor.ClientSize.Height - 2)));
            editorOverviewBar.SetViewport(
                richTextBoxMainEditor.GetLineFromCharIndex(firstIndex) + 1,
                richTextBoxMainEditor.GetLineFromCharIndex(lastIndex) + 1,
                richTextBoxMainEditor.GetLineFromCharIndex(richTextBoxMainEditor.SelectionStart) + 1);
        }

        private void editorOverviewBar_LineRequested(object sender, EditorOverviewLineRequestedEventArgs e)
        {
            navigateWithHistory(() =>
            {
                leaveFoldedView();
                int index = richTextBoxMainEditor.GetFirstCharIndexFromLine(Math.Max(0, e.Line - 1));
                if (index < 0) index = richTextBoxMainEditor.TextLength;
                richTextBoxMainEditor.Select(index, 0);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
        }
    }
}
