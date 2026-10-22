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
            panelEditor.ExecutionStateChanged += executionService_StateChanged;
            panelEditor.ExecutionOutputReceived += executionService_OutputReceived;
            panelEditor.ExecutionCompleted += executionService_ExecutionCompleted;
            panelEditor.DebugStateChanged += debugService_StateChanged;
            panelEditor.CooperativeDebugStateChanged +=
                debugService_CooperativeStateChanged;
            panelEditor.DebugPaused += onDebuggerPaused;
            panelEditor.DebugOutputReceived += executionService_OutputReceived;
            panelEditor.DebugCompleted += debugService_DebugCompleted;
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
            if (panelEditor.DebugService?.IsPaused != true) return;
            clearDebugExecutionLine();
            appendOutput("Ingresso nella funzione...");
            panelEditor.DebugService.StepInto();
            updateExecutionCommands(true);
        }

        private void stepOutMenuItem_Click(object sender, EventArgs e)
        {
            if (panelEditor.DebugService?.IsPaused != true) return;
            clearDebugExecutionLine();
            appendOutput("Uscita dalla funzione corrente...");
            panelEditor.DebugService.StepOut();
            updateExecutionCommands(true);
        }

        private void stepOverMenuItem_Click(object sender, EventArgs e)
        {
            if (panelEditor.DebugService?.IsPaused != true) return;
            clearDebugExecutionLine();
            appendOutput("Passo successivo...");
            panelEditor.DebugService.StepOver();
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

        private void synchronizeRuntimeBreakpoints(OpenDocumentBuffer buffer,
            string runtimeEntryPath = null)
        {
            IDdfEditorDebugService service = panelEditor.DebugService;
            if (service == null || service.IsActive) return;
            var sourceBreakpoints = openDocuments.Documents
                .SelectMany(document => document.BreakpointLines
                    .Select(line => new DdfEditorBreakpoint(document.Id, line,
                        !document.UnboundBreakpointLines.Contains(line))))
                .ToList();
            service.SetBreakpoints(sourceBreakpoints);
        }

        private void synchronizeRuntimeWatchExpressions()
        {
            IDdfEditorDebugService service = panelEditor.DebugService;
            if (service == null || service.IsActive || watchListView == null) return;
            service.SetWatchExpressions(watchListView.Items.Cast<ListViewItem>()
                .Select(item => item.Text));
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

        private void onDebuggerPaused(object sender,
            DdfEditorDebugPausedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, DdfEditorDebugPausedEventArgs>(
                    onDebuggerPaused), sender, e);
                return;
            }

            DdfEditorDebugLocation pause = e.Location;
            string pausePath = resolveDebugDocumentPath(pause);

            if (!string.IsNullOrEmpty(pause.DocumentId) &&
                documentViews.TryGetValue(pause.DocumentId,
                    out DocumentView pauseView))
            {
                activateDocument(pauseView);
                runtimeDocumentId = pause.DocumentId;
                runtimeDocumentPath = pauseView.Buffer.Session.HasPath
                    ? pauseView.Buffer.Session.CurrentPath
                    : null;
            }
            else if (!string.IsNullOrEmpty(pausePath))
            {
                if (!openDocument(pausePath)) return;
                runtimeDocumentId = openDocuments.ActiveDocument?.Id;
                runtimeDocumentPath = pausePath;
            }
            else if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView runtimeView))
                activateDocument(runtimeView);

            appendOutput("Breakpoint raggiunto: riga " + pause.Line + ", colonna " + pause.Column + ".");
            if (panelEditor.DebugService?.IsPaused != true) return;
            watchSnapshot = e.Snapshot;
            selectedCallStackFrameIndex = 0;
            navigateToRuntimeSpan(pause.DocumentId, pausePath, pause.Start,
                pause.Length);
            showDebugExecutionLine(pause.Line);
            showFlowchartDebugLocation(e.Snapshot);
            updateExecutionCommands(true);
        }

        private void debugService_CooperativeStateChanged(object sender,
            DdfEditorCooperativeStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object,
                    DdfEditorCooperativeStateChangedEventArgs>(
                    debugService_CooperativeStateChanged), sender, e);
                return;
            }

            if (e.Snapshot.State ==
                DdfEditorCooperativeExecutionState.AwaitingCapability)
            {
                DdfEditorDebugSnapshot snapshot =
                    panelEditor.DebugService?.CurrentSnapshot;
                if (snapshot == null) return;
                watchSnapshot = snapshot;
                selectedCallStackFrameIndex = 0;
                DdfEditorDebugLocation location = snapshot.Location;
                string path = resolveDebugDocumentPath(location);
                if (!string.IsNullOrEmpty(location.DocumentId) &&
                    documentViews.TryGetValue(location.DocumentId,
                        out DocumentView view))
                    activateDocument(view);
                navigateToRuntimeSpan(location.DocumentId, path,
                    location.Start, location.Length);
                showDebugExecutionLine(location.Line);
                showFlowchartDebugLocation(snapshot);
                appendOutput("In attesa della capability '" +
                    e.Snapshot.PendingSuspension.OperationId + "'.");
                updateExecutionCommands(true);
            }
            else if (e.PreviousSnapshot.State ==
                DdfEditorCooperativeExecutionState.AwaitingCapability &&
                e.Snapshot.State == DdfEditorCooperativeExecutionState.Running)
            {
                clearDebugExecutionLine();
                watchSnapshot = null;
                updateExecutionCommands(true);
            }
        }

        private string resolveDebugDocumentPath(DdfEditorDebugLocation pause)
        {
            if (pause == null || string.IsNullOrWhiteSpace(pause.FilePath))
                return null;
            if (Path.IsPathRooted(pause.FilePath) && File.Exists(pause.FilePath))
                return Path.GetFullPath(pause.FilePath);

            if (workspaceIndex != null)
            {
                string workspacePath = Path.GetFullPath(Path.Combine(
                    workspaceIndex.RootPath, pause.FilePath.Replace('/',
                        Path.DirectorySeparatorChar)));
                if (isPathInside(workspaceIndex.RootPath, workspacePath) &&
                    File.Exists(workspacePath))
                    return workspacePath;
            }

            string relative = pause.FilePath.Replace('\\', '/');
            string[] modulePaths = debugRuntimeModules.Where(module =>
                    string.Equals(module.RelativePath, relative,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(module.RelativePath),
                        Path.GetFileName(relative),
                        StringComparison.OrdinalIgnoreCase))
                .Select(module => module.DocumentPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) &&
                    Path.IsPathRooted(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return modulePaths.Length == 1 ? Path.GetFullPath(modulePaths[0]) : null;
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
            clearFlowchartDebugLocation();
        }
    }
}
