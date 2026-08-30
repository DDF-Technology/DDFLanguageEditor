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
        private DdfDebuggerSession debuggerSession;
        private TabPage tabPageBreakpoints;
        private ListBox listBoxBreakpoints;

        private void initializeDebugger()
        {
            richTextBoxLineNumbers.LineClicked += richTextBoxLineNumbers_LineClicked;
            tabPageBreakpoints = new TabPage("Breakpoint")
            {
                Name = "tabPageBreakpoints",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            listBoxBreakpoints = new ListBox
            {
                Name = "listBoxBreakpoints",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F),
                HorizontalScrollbar = true
            };
            listBoxBreakpoints.DoubleClick += listBoxBreakpoints_DoubleClick;
            tabPageBreakpoints.Controls.Add(listBoxBreakpoints);
            tabControlBottom.TabPages.Add(tabPageBreakpoints);
            refreshBreakpointsPalette();
        }

        private void richTextBoxLineNumbers_LineClicked(object sender, LineNumberClickEventArgs e)
        {
            toggleBreakpointAtLine(e.SourceLine);
        }

        private void toggleBreakpointMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
            int line = richTextBoxMainEditor.GetLineFromCharIndex(richTextBoxMainEditor.SelectionStart) + 1;
            toggleBreakpointAtLine(line);
        }

        private void toggleBreakpointAtLine(int line)
        {
            if (line <= 0) return;
            if (!activeBreakpointLines.Remove(line)) activeBreakpointLines.Add(line);
            openDocuments.ActiveDocument.UnboundBreakpointLines.Remove(line);
            CompilationUnitSyntax root = lastParseResult != null &&
                string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)
                ? lastParseResult.Root
                : DdfParser.Parse(richTextBoxMainEditor.Text).Root;
            validateBreakpoints(openDocuments.ActiveDocument, richTextBoxMainEditor.Text, root);
            debuggerSession?.SetBreakpoints(getEnabledBreakpointLines(openDocuments.ActiveDocument));
            refreshBreakpointsPalette();
            updateLineNumbers();
        }

        private void clearBreakpoints()
        {
            activeBreakpointLines?.Clear();
            openDocuments.ActiveDocument?.UnboundBreakpointLines.Clear();
            debuggerSession?.SetBreakpoints(activeBreakpointLines == null
                ? (IEnumerable<int>)Array.Empty<int>()
                : activeBreakpointLines);
            refreshBreakpointsPalette();
        }

        private string formatLineNumber(int line)
        {
            if (activeBreakpointLines == null || !activeBreakpointLines.Contains(line)) return line.ToString();
            return openDocuments.ActiveDocument.UnboundBreakpointLines.Contains(line) ? "○ " + line : "● " + line;
        }

        private IEnumerable<int> getEnabledBreakpointLines(OpenDocumentBuffer buffer)
        {
            return buffer == null
                ? Enumerable.Empty<int>()
                : buffer.BreakpointLines.Where(line => !buffer.UnboundBreakpointLines.Contains(line));
        }

        private void validateBreakpoints(OpenDocumentBuffer buffer, string source, CompilationUnitSyntax root)
        {
            if (buffer == null || buffer.BreakpointLines.Count == 0) return;
            IReadOnlyCollection<int> executable = DdfBreakpointService.GetExecutableLines(source, root);
            foreach (int line in buffer.BreakpointLines)
                if (!executable.Contains(line)) buffer.UnboundBreakpointLines.Add(line);
            if (ReferenceEquals(buffer, openDocuments.ActiveDocument))
            {
                debuggerSession?.SetBreakpoints(getEnabledBreakpointLines(buffer));
                updateLineNumbers();
            }
            refreshBreakpointsPalette();
        }

        private void refreshBreakpointsPalette()
        {
            if (listBoxBreakpoints == null || listBoxBreakpoints.IsDisposed) return;
            string selectedKey = (listBoxBreakpoints.SelectedItem as BreakpointListItem)?.Key;
            listBoxBreakpoints.BeginUpdate();
            try
            {
                listBoxBreakpoints.Items.Clear();
                foreach (OpenDocumentBuffer document in openDocuments.Documents)
                {
                    foreach (int line in document.BreakpointLines.OrderBy(value => value))
                    {
                        bool enabled = !document.UnboundBreakpointLines.Contains(line);
                        string documentName = document.Session.HasPath ? document.Session.CurrentPath : document.Session.DisplayName;
                        listBoxBreakpoints.Items.Add(new BreakpointListItem(document.Id, documentName, line, enabled));
                    }
                }
                if (selectedKey != null)
                    for (int index = 0; index < listBoxBreakpoints.Items.Count; index++)
                        if (((BreakpointListItem)listBoxBreakpoints.Items[index]).Key == selectedKey) listBoxBreakpoints.SelectedIndex = index;
            }
            finally
            {
                listBoxBreakpoints.EndUpdate();
            }
        }

        private void listBoxBreakpoints_DoubleClick(object sender, EventArgs e)
        {
            var item = listBoxBreakpoints.SelectedItem as BreakpointListItem;
            if (item == null || !documentViews.TryGetValue(item.DocumentId, out DocumentView view)) return;
            activateDocument(view);
            int index = richTextBoxMainEditor.GetFirstCharIndexFromLine(Math.Max(0, item.Line - 1));
            richTextBoxMainEditor.Select(Math.Max(0, index), 0);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private sealed class BreakpointListItem
        {
            public BreakpointListItem(string documentId, string displayName, int line, bool enabled)
            {
                DocumentId = documentId;
                DisplayName = displayName;
                Line = line;
                Enabled = enabled;
            }
            public string DocumentId { get; }
            public string DisplayName { get; }
            public int Line { get; }
            public bool Enabled { get; }
            public string Key => DocumentId + ":" + Line;
            public override string ToString() => (Enabled ? "● " : "○ ") + DisplayName + " — riga " + Line +
                (Enabled ? " — attivo" : " — non associato");
        }

        private void onDebuggerPaused(DdfDebugPauseInfo pause)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<DdfDebugPauseInfo>(onDebuggerPaused), pause);
                return;
            }

            if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView runtimeView))
                activateDocument(runtimeView);

            appendOutput("Breakpoint raggiunto: riga " + pause.Line + ", colonna " + pause.Column + ".");
            navigateToRuntimeSpan(pause.Start, pause.Length);
            updateExecutionCommands(true);
        }
    }
}
