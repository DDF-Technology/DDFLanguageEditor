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
        private readonly OpenDocumentCollection openDocuments = new OpenDocumentCollection();
        private readonly DdfDocumentViewCoordinator documentViews;
        private TableLayoutPanel documentLayout;
        private Panel editorHost;
        private TabControl documentTabs;
        private ToolStripMenuItem saveAllMenuItem;
        private ToolStripMenuItem closeDocumentMenuItem;
        private bool isSwitchingDocument;
        private Timer documentViewportRestoreTimer;

        private DocumentView activeDocumentView => documentViews.ActiveView;

        private ISet<int> activeBreakpointLines => openDocuments.ActiveDocument?.BreakpointLines;

        /// <summary>
        /// Assegna nome e contenuto al documento principale di una shell incorporata.
        /// Il contenuto viene considerato fornito dall'host e non modificato dall'utente.
        /// </summary>
        public string SetEmbeddedPrimaryDocument(string displayName, string source)
        {
            if (!embeddedHostMode)
                throw new InvalidOperationException("Questa API è disponibile soltanto in modalità embedded.");
            OpenDocumentBuffer buffer = openDocuments.ActiveDocument;
            DocumentView view = activeDocumentView;
            if (buffer == null || view == null) return null;

            buffer.Session.SetUntitled(displayName);
            isReplacingDocument = true;
            try
            {
                view.Editor.Text = source ?? string.Empty;
                buffer.UpdateSource(view.Editor.Text, false);
                view.Editor.Modified = false;
            }
            finally
            {
                isReplacingDocument = false;
            }
            updateDocumentTab(view);
            updateDocumentUi();
            applyHighlighting();
            return buffer.Id;
        }

        /// <summary>
        /// Aggiunge una scheda virtuale fornita dall'applicazione host senza creare file su disco.
        /// </summary>
        public string AddEmbeddedDocument(string displayName, string source, bool activate = false)
        {
            if (!embeddedHostMode)
                throw new InvalidOperationException("Questa API è disponibile soltanto in modalità embedded.");
            DocumentView previousView = activeDocumentView;
            var buffer = (OpenDocumentBuffer)openDocuments.CreateVirtual(displayName, source);
            DocumentView view = addDocumentView(buffer);
            isReplacingDocument = true;
            try
            {
                view.Editor.Text = source ?? string.Empty;
                buffer.UpdateSource(view.Editor.Text, false);
                view.Editor.Modified = false;
            }
            finally
            {
                isReplacingDocument = false;
            }
            updateDocumentTab(view);
            if (activate) activateDocument(view);
            else if (previousView != null)
            {
                openDocuments.Activate(previousView.Buffer.Id);
                activateDocument(previousView);
            }
            return buffer.Id;
        }

        public bool ActivateEmbeddedDocument(string documentId)
        {
            if (!embeddedHostMode || string.IsNullOrWhiteSpace(documentId) ||
                !documentViews.TryGetValue(documentId, out DocumentView view)) return false;
            activateDocument(view);
            return ReferenceEquals(openDocuments.ActiveDocument, view.Buffer);
        }

        private void initializeDocuments()
        {
            editorHost = new Panel
            {
                Name = "editorHost",
                Dock = DockStyle.Fill,
                BackColor = richTextBoxMainEditor.BackColor,
                Padding = new Padding(0)
            };
            documentLayout = new TableLayoutPanel
            {
                Name = "documentLayout",
                Dock = DockStyle.Fill,
                BackColor = richTextBoxMainEditor.BackColor,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            documentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            documentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            documentTabs = new TabControl
            {
                Name = "documentTabs",
                Dock = DockStyle.Fill,
                HotTrack = true,
                Multiline = false,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(16, 3),
                ShowToolTips = true,
                SizeMode = TabSizeMode.Normal,
                BackColor = AppTheme.Window,
                ForeColor = AppTheme.Text
            };
            documentTabs.SelectedIndexChanged += documentTabs_SelectedIndexChanged;
            documentTabs.MouseDown += documentTabs_MouseDown;
            documentTabs.DrawItem += documentTabs_DrawItem;

            panelEditor.SuspendLayout();
            try
            {
                panelEditor.Controls.Remove(richTextBoxFoldedView);
                panelEditor.Controls.Remove(richTextBoxMainEditor);
                panelEditor.Controls.Remove(richTextBoxLineNumbers);
                editorHost.Controls.Add(richTextBoxFoldedView);
                editorHost.Controls.Add(richTextBoxMainEditor);
                editorHost.Controls.Add(richTextBoxLineNumbers);
                documentLayout.Controls.Add(documentTabs, 0, 0);
                documentLayout.Controls.Add(editorHost, 0, 1);
                panelEditor.Controls.Add(documentLayout);
            }
            finally
            {
                panelEditor.ResumeLayout(true);
            }

            OpenDocumentBuffer initial = openDocuments.CreateUntitled();
            documentSession = initial.Session;
            addDocumentView(initial, richTextBoxMainEditor);
            hookEditorMouseEvents(richTextBoxMainEditor);

            closeDocumentMenuItem = new ToolStripMenuItem("Chiudi documento")
            {
                Name = "closeDocumentMenuItem",
                ShortcutKeys = Keys.Control | Keys.W
            };
            closeDocumentMenuItem.Click += closeDocumentMenuItem_Click;
            saveAllMenuItem = new ToolStripMenuItem("Salva tutto")
            {
                Name = "saveAllMenuItem",
                ShortcutKeys = Keys.Control | Keys.Alt | Keys.S
            };
            saveAllMenuItem.Click += saveAllMenuItem_Click;
            int saveAsIndex = fileMenuItem.DropDownItems.IndexOf(saveAsMenuItem);
            fileMenuItem.DropDownItems.Insert(saveAsIndex + 1, saveAllMenuItem);
            fileMenuItem.DropDownItems.Insert(saveAsIndex + 2, closeDocumentMenuItem);
        }

        private DocumentView addDocumentView(OpenDocumentBuffer buffer, RichTextBox editor = null)
        {
            editor = editor ?? createDocumentEditor(buffer.Id);
            var page = new TabPage { Name = "documentTab_" + buffer.Id, Tag = buffer.Id };
            DocumentView view = documentViews.Register(buffer, editor, page);
            documentTabs.TabPages.Add(page);
            updateDocumentTab(view);
            return view;
        }

        private RichTextBox createDocumentEditor(string id)
        {
            var editor = new MultiCursorRichTextBox
            {
                AcceptsTab = richTextBoxMainEditor.AcceptsTab,
                BackColor = richTextBoxMainEditor.BackColor,
                BorderStyle = richTextBoxMainEditor.BorderStyle,
                Dock = DockStyle.Fill,
                Font = richTextBoxMainEditor.Font,
                ForeColor = richTextBoxMainEditor.ForeColor,
                HideSelection = false,
                Name = "documentEditor_" + id,
                ZoomFactor = richTextBoxMainEditor.ZoomFactor,
                WordWrap = editorSettings?.WordWrap == true,
                Visible = false,
                ContextMenuStrip = editorContextMenu
            };
            editor.VScroll += richTextBoxMainEditor_VScroll;
            editor.ReadOnly = debugUiLocked;
            editor.FontChanged += richTextBoxMainEditor_FontChanged;
            editor.SelectionChanged += richTextBoxMainEditor_SelectionChanged;
            editor.TextChanged += richTextBox_TextChanged;
            editor.KeyDown += richTextBoxMainEditor_KeyDown;
            editor.KeyPress += richTextBoxMainEditor_KeyPress;
            hookEditorMouseEvents(editor);
            if (editorSettings != null) configureEditorVisuals(editor);
            configureWorkspaceDropTarget(editor);
            editorHost.Controls.Add(editor);
            editor.BringToFront();
            return editor;
        }

        private void hookEditorMouseEvents(RichTextBox editor)
        {
            if (editor is MultiCursorRichTextBox multiCursorEditor)
                multiCursorEditor.AltCursorRequested += richTextBoxMainEditor_AltCursorRequested;
            editor.MouseDown += richTextBoxMainEditor_MouseDown;
            editor.MouseUp += richTextBoxMainEditor_MouseUp;
            editor.MouseMove += richTextBoxMainEditor_MouseMove;
            editor.MouseLeave += richTextBoxMainEditor_MouseLeave;
        }

        private void richTextBoxMainEditor_AltCursorRequested(object sender, AltCursorEventArgs e)
        {
            if (ReferenceEquals(sender, richTextBoxMainEditor)) addCursorAtPosition(e.Position);
        }

        private DocumentView findDocumentView(RichTextBox editor)
        {
            return documentViews.FindByEditor(editor);
        }

        private void createUntitledDocument()
        {
            OpenDocumentBuffer buffer = openDocuments.CreateUntitled();
            DocumentView view = addDocumentView(buffer);
            activateDocument(view);
            scheduleWorkspaceSearch();
        }

        private bool activateOpenDocument(string path)
        {
            OpenDocumentBuffer buffer = openDocuments.FindByPath(path);
            if (buffer == null) return false;
            activateDocument(documentViews[buffer.Id]);
            return true;
        }

        private void activateDocument(DocumentView view)
        {
            if (IsDisposed || Disposing || documentTabs.IsDisposed || documentTabs.Disposing) return;
            if (view == null || ReferenceEquals(view.Editor, richTextBoxMainEditor))
            {
                if (view != null) selectDocumentTab(view.Tab);
                return;
            }

            cancelDocumentViewportRestore();

            // Congela la viewport della scheda entrante prima che binding,
            // colorazione e notifiche VScroll possano aggiornarne lo stato.
            // Gli eventi intermedi non devono trasformare la coordinata già
            // salvata (in particolare con scaling DPI diverso da 100%).
            bool restoreIncomingViewport = view.HasScrollPosition;
            Point incomingViewport = view.ScrollPosition;

            isSwitchingDocument = true;
            try
            {
                DocumentView currentView = findDocumentView(richTextBoxMainEditor);
                leaveFoldedView();
                hideCompletion();
                hideSignatureHelp();
                cancelSnippetSession(false);
                highlightTimer.Stop();
                delimiterHighlightTimer.Stop();
                if (currentView != null)
                {
                    captureDocumentViewport(currentView);
                    currentView.Buffer.UpdateSource(richTextBoxMainEditor.Text, false);
                    richTextBoxMainEditor.Visible = false;
                    richTextBoxMainEditor.Name = "documentEditor_" + currentView.Buffer.Id;
                }

                openDocuments.Activate(view.Buffer.Id);
                documentSession = view.Buffer.Session;
                richTextBoxMainEditor = view.Editor;
                richTextBoxMainEditor.Name = "richTextBoxMainEditor";
                richTextBoxMainEditor.Visible = true;
                richTextBoxMainEditor.BringToFront();
                panelEditor.BindEditor(richTextBoxMainEditor);
                if (editorOverviewBar != null) editorOverviewBar.TargetEditor = richTextBoxMainEditor;
                editorOverviewBar?.BringToFront();
                richTextBoxLineNumbers.TargetControl = richTextBoxMainEditor;
                selectDocumentTab(view.Tab);
                resetActiveAnalysis();
            }
            finally
            {
                isSwitchingDocument = false;
            }

            applyHighlighting();
            updateDocumentUi();
            refreshBreakpointsPalette();
            updateCaretPosition();
            updateBreadcrumb();
            richTextBoxMainEditor.Focus();
            if (restoreIncomingViewport)
            {
                RichTextBoxUpdateScope.SetScrollPosition(view.Editor,
                    incomingViewport);
                view.ScrollPosition = incomingViewport;
                view.HasScrollPosition = true;
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing ||
                        !ReferenceEquals(activeDocumentView, view)) return;
                    RichTextBoxUpdateScope.SetScrollPosition(view.Editor,
                        incomingViewport);
                    view.ScrollPosition = incomingViewport;
                    view.HasScrollPosition = true;
                    updateLineNumbers();
                    refreshEditorOverviewViewport();
                    scheduleDocumentViewportVerification(view,
                        incomingViewport);
                }));
            }
            updateLineNumbers();
            refreshEditorOverview();
        }

        private void scheduleDocumentViewportVerification(DocumentView view,
            Point position)
        {
            cancelDocumentViewportRestore();
            documentViewportRestoreTimer = new Timer { Interval = 75 };
            documentViewportRestoreTimer.Tick += (sender, args) =>
            {
                cancelDocumentViewportRestore();
                if (IsDisposed || Disposing ||
                    !ReferenceEquals(activeDocumentView, view)) return;
                RichTextBoxUpdateScope.SetScrollPosition(view.Editor,
                    position);
                view.ScrollPosition = position;
                view.HasScrollPosition = true;
                updateLineNumbers();
                refreshEditorOverviewViewport();
            };
            documentViewportRestoreTimer.Start();
        }

        private void cancelDocumentViewportRestore()
        {
            if (documentViewportRestoreTimer == null) return;
            documentViewportRestoreTimer.Stop();
            documentViewportRestoreTimer.Dispose();
            documentViewportRestoreTimer = null;
        }

        private void selectDocumentTab(TabPage tab)
        {
            if (tab == null || documentTabs == null || documentTabs.IsDisposed) return;
            // Un TabControl Win32 disabilitato può non ridisegnare immediatamente una
            // selezione impostata dal debugger. Lo abilitiamo soltanto per la durata
            // atomica dello switch; l'utente continua a non poter cambiare documento.
            bool restoreDisabled = !documentTabs.Enabled;
            if (restoreDisabled) documentTabs.Enabled = true;
            try
            {
                documentTabs.SelectedTab = tab;
            }
            finally
            {
                if (restoreDisabled) documentTabs.Enabled = false;
                documentTabs.Invalidate();
                documentTabs.Update();
            }
        }

        private void resetActiveAnalysis()
        {
            incrementalLexer.Reset();
            lastLexResult = null;
            lastParseResult = null;
            lastSemanticModel = null;
            lastTypeCheckResult = null;
            lastFlowchartIr = null;
            lastFlowchartDiagnostics = Array.Empty<DdfDiagnostic>();
            flowchartSnapshotState = DdfFlowchartSnapshotState.Unavailable;
            requestedFlowchartFunction = null;
            fullFlowchartLayout = null;
            expandedFlowchartGroups.Clear();
            flowchartSnapshotService.Cancel();
            if (flowchartCanvas != null)
            {
                flowchartCanvas.SetDiagram(null);
                flowchartCanvas.SetViewNotice(string.Empty, false);
                flowchartView?.SetEducationalRuntimeValues(null);
                setFlowchartSnapshotState(DdfFlowchartSnapshotState.Unavailable,
                    "FLOWCHART — nessuna funzione rappresentabile");
                refreshFlowchartFunctionsMenu();
            }
            lastAnalyzedText = string.Empty;
            activeDelimiterMatch = null;
            diagnosticsFormatStart = int.MaxValue;
            outlineRenderSignature = null;
            diagnosticsRenderSignature = null;
            collapsedFoldStarts.Clear();
            foldingRanges = new List<DdfFoldingRange>();
        }

        private void documentTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || documentTabs.Disposing || isSwitchingDocument || documentTabs.SelectedTab == null) return;
            string id = documentTabs.SelectedTab.Tag as string;
            if (!string.IsNullOrEmpty(id) && documentViews.TryGetValue(id, out DocumentView view)) activateDocument(view);
        }

        private static void captureDocumentViewport(DocumentView view)
        {
            if (view == null) return;
            if (!RichTextBoxUpdateScope.TryGetScrollPosition(view.Editor, out Point position)) return;
            view.ScrollPosition = position;
            view.HasScrollPosition = true;
        }

        private static void restoreDocumentViewport(DocumentView view)
        {
            if (view?.HasScrollPosition != true) return;
            RichTextBoxUpdateScope.SetScrollPosition(view.Editor, view.ScrollPosition);
        }

        private void documentTabs_MouseDown(object sender, MouseEventArgs e)
        {
            for (int index = 0; index < documentTabs.TabCount; index++)
            {
                if (!documentTabs.GetTabRect(index).Contains(e.Location)) continue;
                bool closeRequested = e.Button == MouseButtons.Middle ||
                    (e.Button == MouseButtons.Left && getDocumentTabCloseBounds(index).Contains(e.Location));
                if (!closeRequested) return;
                string id = documentTabs.TabPages[index].Tag as string;
                if (documentViews.TryGetValue(id, out DocumentView view)) closeDocument(view);
                return;
            }
        }

        private void documentTabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage page = documentTabs.TabPages[e.Index];
            Rectangle bounds = documentTabs.GetTabRect(e.Index);
            Color background = e.Index == documentTabs.SelectedIndex ? AppTheme.Surface : AppTheme.Window;
            using (var backgroundBrush = new SolidBrush(background)) e.Graphics.FillRectangle(backgroundBrush, bounds);
            TextRenderer.DrawText(e.Graphics, page.Text, documentTabs.Font,
                new Rectangle(bounds.X + 7, bounds.Y + 2, Math.Max(0, bounds.Width - 25), bounds.Height - 3),
                AppTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            Rectangle close = getDocumentTabCloseBounds(e.Index);
            TextRenderer.DrawText(e.Graphics, "×", documentTabs.Font, close, AppTheme.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private Rectangle getDocumentTabCloseBounds(int index)
        {
            Rectangle bounds = documentTabs.GetTabRect(index);
            return new Rectangle(bounds.Right - 19, bounds.Top + 3, 16, Math.Max(16, bounds.Height - 6));
        }

        private void saveAllMenuItem_Click(object sender, EventArgs e)
        {
            saveAllDocuments();
        }

        private bool saveAllDocuments()
        {
            string originalId = openDocuments.ActiveDocument?.Id;
            foreach (OpenDocumentBuffer buffer in openDocuments.Documents.ToList())
            {
                if (!buffer.Session.IsDirty) continue;
                activateDocument(documentViews[buffer.Id]);
                if (!saveDocument(false)) return false;
            }
            if (originalId != null && documentViews.TryGetValue(originalId, out DocumentView original)) activateDocument(original);
            return true;
        }

        private void closeDocumentMenuItem_Click(object sender, EventArgs e)
        {
            closeDocument(activeDocumentView);
        }

        private bool closeDocument(DocumentView view)
        {
            if (view == null) return true;
            activateDocument(view);
            if (!confirmDiscardChanges()) return false;
            if (view.Buffer.Id == runtimeDocumentId)
            {
                stopExecution();
                outputNavigationTargets.Clear();
                runtimeDocumentId = null;
            }

            // The editor surface and its SDK services assume that an active document
            // always exists. Create the replacement before removing the last view so
            // DocumentClosed/ActiveDocumentChanged observers never see a transient
            // null document bound to a control that is about to be disposed.
            if (openDocuments.Documents.Count == 1)
                addDocumentView(openDocuments.CreateUntitled());

            isSwitchingDocument = true;
            try
            {
                leaveFoldedView();
                openDocuments.TryCloseDocument(view.Buffer.Id, true);
                documentTabs.TabPages.Remove(view.Tab);
                editorHost.Controls.Remove(view.Editor);
                documentViews.Unregister(view.Buffer.Id);
                knownDiskStates.Remove(view.Buffer.Id);
                markCrashRecoveryChanged();
                view.Tab.Dispose();
                view.Editor.Dispose();
                refreshBreakpointsPalette();
            }
            finally
            {
                isSwitchingDocument = false;
            }

            activateDocument(documentViews[openDocuments.ActiveDocument.Id]);
            scheduleWorkspaceSearch();
            updateNavigationHistoryCommandState();
            return true;
        }

        private bool confirmAllDocumentChanges()
        {
            List<OpenDocumentBuffer> dirtyDocuments = openDocuments.Documents.Where(item => item.Session.IsDirty).ToList();
            if (dirtyDocuments.Count == 0) return true;
            if (dirtyDocuments.Count == 1)
            {
                activateDocument(documentViews[dirtyDocuments[0].Id]);
                return confirmDiscardChanges();
            }

            DialogResult result = MessageBox.Show(
                this,
                "Ci sono " + dirtyDocuments.Count + " documenti modificati. Salvarli tutti prima di uscire?",
                "Modifiche non salvate",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);
            if (result == DialogResult.Cancel) return false;
            return result == DialogResult.No || saveAllDocuments();
        }

        private void updateDocumentTab(DocumentView view)
        {
            if (view == null) return;
            string marker = view.Buffer.Session.IsDirty ? " *" : string.Empty;
            view.Tab.Text = view.Buffer.Session.DisplayName + marker;
            view.Tab.ToolTipText = view.Buffer.Session.HasPath ? view.Buffer.Session.CurrentPath : view.Buffer.Session.DisplayName;
        }

    }
}
