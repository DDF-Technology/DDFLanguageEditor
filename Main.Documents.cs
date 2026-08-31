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
        private readonly Dictionary<string, DocumentView> documentViews = new Dictionary<string, DocumentView>();
        private TableLayoutPanel documentLayout;
        private Panel editorHost;
        private TabControl documentTabs;
        private ToolStripMenuItem saveAllMenuItem;
        private ToolStripMenuItem closeDocumentMenuItem;
        private bool isSwitchingDocument;

        private DocumentView activeDocumentView => openDocuments.ActiveDocument == null
            ? null
            : documentViews[openDocuments.ActiveDocument.Id];

        private ISet<int> activeBreakpointLines => openDocuments.ActiveDocument?.BreakpointLines;

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
            var view = new DocumentView(buffer, editor, page);
            documentViews.Add(buffer.Id, view);
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
                WordWrap = false,
                Visible = false,
                ContextMenuStrip = editorContextMenu
            };
            editor.VScroll += richTextBoxMainEditor_VScroll;
            editor.FontChanged += richTextBoxMainEditor_FontChanged;
            editor.SelectionChanged += richTextBoxMainEditor_SelectionChanged;
            editor.TextChanged += richTextBox_TextChanged;
            editor.KeyDown += richTextBoxMainEditor_KeyDown;
            editor.KeyPress += richTextBoxMainEditor_KeyPress;
            hookEditorMouseEvents(editor);
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
            return documentViews.Values.FirstOrDefault(view => ReferenceEquals(view.Editor, editor));
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
            if (view == null || ReferenceEquals(view.Editor, richTextBoxMainEditor))
            {
                if (view != null) documentTabs.SelectedTab = view.Tab;
                return;
            }

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
                richTextBoxLineNumbers.TargetControl = richTextBoxMainEditor;
                documentTabs.SelectedTab = view.Tab;
                resetActiveAnalysis();
            }
            finally
            {
                isSwitchingDocument = false;
            }

            applyHighlighting();
            updateDocumentUi();
            updateLineNumbers();
            refreshBreakpointsPalette();
            updateCaretPosition();
            richTextBoxMainEditor.Focus();
        }

        private void resetActiveAnalysis()
        {
            incrementalLexer.Reset();
            lastLexResult = null;
            lastParseResult = null;
            lastSemanticModel = null;
            lastTypeCheckResult = null;
            lastAnalyzedText = string.Empty;
            activeDelimiterMatch = null;
            diagnosticsFormatStart = int.MaxValue;
            collapsedFoldStarts.Clear();
            foldingRanges = new List<DdfFoldingRange>();
        }

        private void documentTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isSwitchingDocument || documentTabs.SelectedTab == null) return;
            string id = documentTabs.SelectedTab.Tag as string;
            if (!string.IsNullOrEmpty(id) && documentViews.TryGetValue(id, out DocumentView view)) activateDocument(view);
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

            isSwitchingDocument = true;
            try
            {
                leaveFoldedView();
                documentTabs.TabPages.Remove(view.Tab);
                editorHost.Controls.Remove(view.Editor);
                documentViews.Remove(view.Buffer.Id);
                openDocuments.Remove(view.Buffer.Id);
                view.Tab.Dispose();
                view.Editor.Dispose();
                refreshBreakpointsPalette();
            }
            finally
            {
                isSwitchingDocument = false;
            }

            if (openDocuments.ActiveDocument == null)
            {
                createUntitledDocument();
            }
            else
            {
                activateDocument(documentViews[openDocuments.ActiveDocument.Id]);
            }
            scheduleWorkspaceSearch();
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

        private sealed class DocumentView
        {
            public DocumentView(OpenDocumentBuffer buffer, RichTextBox editor, TabPage tab)
            {
                Buffer = buffer;
                Editor = editor;
                Tab = tab;
            }

            public OpenDocumentBuffer Buffer { get; }
            public RichTextBox Editor { get; }
            public TabPage Tab { get; }
            public Stack<Tuple<int, int>> SelectionHistory { get; } = new Stack<Tuple<int, int>>();
            public List<DdfTextRange> MultiSelections { get; } = new List<DdfTextRange>();
        }
    }
}
