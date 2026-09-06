using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private ToolStripMenuItem stepIntoMenuItem;
        private ToolStripMenuItem stepOverMenuItem;
        private ToolStripMenuItem stepOutMenuItem;
        private ToolStripButton toolbarStepIntoButton;
        private ToolStripButton toolbarStepOverButton;
        private ToolStripButton toolbarStepOutButton;
        private ToolStripStatusLabel statusDebugLabel;

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
            stepIntoMenuItem = new ToolStripMenuItem("Entra nella funzione")
            {
                Name = "stepIntoMenuItem",
                ShortcutKeys = Keys.F11,
                Enabled = false
            };
            stepIntoMenuItem.Click += stepIntoMenuItem_Click;
            int breakpointMenuIndex = runMenuItem.DropDownItems.IndexOf(toggleBreakpointMenuItem);
            runMenuItem.DropDownItems.Insert(breakpointMenuIndex, stepIntoMenuItem);
            stepOverMenuItem = new ToolStripMenuItem("Passo successivo")
            {
                Name = "stepOverMenuItem",
                ShortcutKeys = Keys.F10,
                Enabled = false
            };
            stepOverMenuItem.Click += stepOverMenuItem_Click;
            runMenuItem.DropDownItems.Insert(breakpointMenuIndex + 1, stepOverMenuItem);
            stepOutMenuItem = new ToolStripMenuItem("Esci dalla funzione")
            {
                Name = "stepOutMenuItem",
                ShortcutKeys = Keys.Shift | Keys.F11,
                Enabled = false
            };
            stepOutMenuItem.Click += stepOutMenuItem_Click;
            runMenuItem.DropDownItems.Insert(breakpointMenuIndex + 2, stepOutMenuItem);
            toolbarStepIntoButton = createToolbarButton(
                "toolbarStepIntoButton", "↳", "Entra nella funzione (F11)", stepIntoMenuItem_Click);
            toolbarStepIntoButton.Enabled = false;
            toolStripMain.Items.Insert(toolStripMain.Items.IndexOf(toolbarStopButton), toolbarStepIntoButton);
            toolbarStepOverButton = createToolbarButton(
                "toolbarStepOverButton", "", "Passo successivo (F10)", stepOverMenuItem_Click);
            toolbarStepOverButton.Enabled = false;
            toolStripMain.Items.Insert(toolStripMain.Items.IndexOf(toolbarStopButton), toolbarStepOverButton);
            toolbarStepOutButton = createToolbarButton(
                "toolbarStepOutButton", "", "Esci dalla funzione (Shift+F11)", stepOutMenuItem_Click);
            toolbarStepOutButton.Enabled = false;
            toolStripMain.Items.Insert(toolStripMain.Items.IndexOf(toolbarStopButton), toolbarStepOutButton);
            statusDebugLabel = new ToolStripStatusLabel
            {
                Name = "statusDebugLabel",
                Available = true,
                Visible = true,
                Text = "MODIFICA",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Right,
                Padding = new Padding(8, 0, 8, 0)
            };
            statusStripMain.Items.Insert(1, statusDebugLabel);
            refreshBreakpointsPalette();
            initializeVariables();
            initializeCallStack();
            initializeWatch();
        }

        private void stepIntoMenuItem_Click(object sender, EventArgs e)
        {
            if (debuggerSession == null || !debuggerSession.IsPaused) return;
            clearDebugExecutionLine();
            appendOutput("Ingresso nella funzione...");
            debuggerSession.StepInto();
            updateExecutionCommands(true);
        }

        private void stepOutMenuItem_Click(object sender, EventArgs e)
        {
            if (debuggerSession == null || !debuggerSession.IsPaused) return;
            clearDebugExecutionLine();
            appendOutput("Uscita dalla funzione corrente...");
            debuggerSession.StepOut();
            updateExecutionCommands(true);
        }

        private void stepOverMenuItem_Click(object sender, EventArgs e)
        {
            if (debuggerSession == null || !debuggerSession.IsPaused) return;
            clearDebugExecutionLine();
            appendOutput("Passo successivo...");
            debuggerSession.StepOver();
            updateExecutionCommands(true);
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
            synchronizeRuntimeBreakpoints(openDocuments.ActiveDocument);
            refreshBreakpointsPalette();
            updateLineNumbers();
            refreshEditorOverview();
        }

        private void clearBreakpoints()
        {
            activeBreakpointLines?.Clear();
            openDocuments.ActiveDocument?.UnboundBreakpointLines.Clear();
            synchronizeRuntimeBreakpoints(openDocuments.ActiveDocument);
            refreshBreakpointsPalette();
            refreshEditorOverview();
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

        private void synchronizeRuntimeBreakpoints(OpenDocumentBuffer buffer)
        {
            if (debuggerSession == null) return;
            if (buffer != null && !buffer.Session.HasPath)
            {
                debuggerSession.SetBreakpoints(getEnabledBreakpointLines(buffer));
                return;
            }
            var sourceBreakpoints = openDocuments.Documents
                .Where(document => document.Session.HasPath)
                .SelectMany(document => getEnabledBreakpointLines(document)
                    .Select(line => new DdfSourceBreakpoint(document.Session.CurrentPath, line)))
                .ToList();
            debuggerSession.SetBreakpoints(sourceBreakpoints);
        }

        private void validateBreakpoints(OpenDocumentBuffer buffer, string source, CompilationUnitSyntax root)
        {
            if (buffer == null || buffer.BreakpointLines.Count == 0) return;
            IReadOnlyCollection<int> executable = DdfBreakpointService.GetExecutableLines(source, root);
            foreach (int line in buffer.BreakpointLines)
                if (!executable.Contains(line)) buffer.UnboundBreakpointLines.Add(line);
            if (ReferenceEquals(buffer, openDocuments.ActiveDocument))
            {
                synchronizeRuntimeBreakpoints(buffer);
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
            navigateWithHistory(() =>
            {
                activateDocument(view);
                int index = richTextBoxMainEditor.GetFirstCharIndexFromLine(Math.Max(0, item.Line - 1));
                richTextBoxMainEditor.Select(Math.Max(0, index), 0);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
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

            if (!string.IsNullOrEmpty(pause.DocumentPath) && File.Exists(pause.DocumentPath))
            {
                if (!openDocument(pause.DocumentPath)) return;
                runtimeDocumentId = openDocuments.ActiveDocument?.Id;
                runtimeDocumentPath = pause.DocumentPath;
            }
            else if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView runtimeView))
                activateDocument(runtimeView);

            appendOutput("Breakpoint raggiunto: riga " + pause.Line + ", colonna " + pause.Column + ".");
            if (debuggerSession == null || !debuggerSession.IsPaused) return;
            watchSnapshot = pause.State;
            selectedCallStackFrameIndex = 0;
            navigateToRuntimeSpan(!string.IsNullOrEmpty(pause.DocumentPath) && File.Exists(pause.DocumentPath)
                ? pause.DocumentPath : null, pause.Start, pause.Length);
            showDebugExecutionLine(pause.Line);
            updateExecutionCommands(true);
        }

        private void showDebugExecutionLine(int oneBasedLine)
        {
            clearDebugExecutionLine();
            if (richTextBoxMainEditor is MultiCursorRichTextBox editor)
                editor.DebugExecutionLine = Math.Max(0, oneBasedLine - 1);
        }

        private void clearDebugExecutionLine()
        {
            foreach (DocumentView view in documentViews.Values)
                if (view.Editor is MultiCursorRichTextBox editor) editor.DebugExecutionLine = -1;
        }
    }
}
