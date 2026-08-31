using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem workspaceSearchMenuItem;
        private ToolStripMenuItem workspaceReplaceMenuItem;
        private ToolStripButton toolbarWorkspaceSearchButton;
        private ToolStripButton toolbarWorkspaceReplaceButton;
        private TabPage tabPageWorkspaceSearch;
        private TableLayoutPanel workspaceSearchHeaderLayout;
        private TableLayoutPanel workspaceReplaceLayout;
        private TextBox workspaceSearchTextBox;
        private TextBox workspaceReplacementTextBox;
        private ComboBox workspaceSearchKindComboBox;
        private CheckBox workspaceSearchMatchCaseCheckBox;
        private Button workspaceSearchButton;
        private Button workspaceReplacementPreviewButton;
        private Button workspaceReplacementApplyButton;
        private Label workspaceSearchStatusLabel;
        private ListView workspaceSearchResultsListView;
        private System.Windows.Forms.Timer workspaceSearchTimer;
        private CancellationTokenSource workspaceSearchCancellation;
        private long workspaceSearchRequestVersion;
        private bool isWorkspaceReplacementMode;
        private bool isPopulatingWorkspaceSearchResults;

        private void initializeWorkspaceSearch()
        {
            workspaceSearchMenuItem = new ToolStripMenuItem("Trova nel workspace...")
            {
                Name = "workspaceSearchMenuItem",
                ShortcutKeys = Keys.Control | Keys.Alt | Keys.F
            };
            workspaceSearchMenuItem.Click += workspaceSearchMenuItem_Click;
            int replaceIndex = editMenuItem.DropDownItems.IndexOf(replaceMenuItem);
            editMenuItem.DropDownItems.Insert(replaceIndex + 1, workspaceSearchMenuItem);
            workspaceReplaceMenuItem = new ToolStripMenuItem("Sostituisci nel workspace...")
            {
                Name = "workspaceReplaceMenuItem",
                ShortcutKeys = Keys.Control | Keys.Alt | Keys.H
            };
            workspaceReplaceMenuItem.Click += workspaceReplaceMenuItem_Click;
            editMenuItem.DropDownItems.Insert(replaceIndex + 2, workspaceReplaceMenuItem);

            toolbarWorkspaceSearchButton = createToolbarButton(
                "toolbarWorkspaceSearchButton", "\uE773", "Trova nel workspace (Ctrl+Alt+F)", workspaceSearchMenuItem_Click);
            int toolbarFindIndex = toolStripMain.Items.IndexOfKey("toolbarFindButton");
            toolStripMain.Items.Insert(toolbarFindIndex + 1, toolbarWorkspaceSearchButton);
            toolbarWorkspaceReplaceButton = createToolbarButton(
                "toolbarWorkspaceReplaceButton", "\uE8AC", "Sostituisci nel workspace (Ctrl+Alt+H)", workspaceReplaceMenuItem_Click);
            toolStripMain.Items.Insert(toolbarFindIndex + 2, toolbarWorkspaceReplaceButton);

            tabPageWorkspaceSearch = new TabPage("Ricerca")
            {
                Name = "tabPageWorkspaceSearch",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            workspaceSearchHeaderLayout = new TableLayoutPanel
            {
                Name = "workspaceSearchHeaderLayout",
                Dock = DockStyle.Top,
                Height = 35,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = AppTheme.Surface
            };
            workspaceSearchHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            workspaceSearchHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            workspaceSearchHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));

            var searchLayout = new TableLayoutPanel
            {
                Name = "workspaceSearchLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(5, 4, 30, 3),
                BackColor = AppTheme.Surface
            };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165F));

            workspaceSearchTextBox = new TextBox
            {
                Name = "workspaceSearchTextBox",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 0),
                PlaceholderText = "Testo o nome del simbolo"
            };
            workspaceSearchKindComboBox = new ComboBox
            {
                Name = "workspaceSearchKindComboBox",
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 6, 0)
            };
            workspaceSearchKindComboBox.Items.AddRange(new object[] { "Testo", "Simboli" });
            workspaceSearchKindComboBox.SelectedIndex = 0;
            workspaceSearchMatchCaseCheckBox = new CheckBox
            {
                Name = "workspaceSearchMatchCaseCheckBox",
                Dock = DockStyle.Fill,
                Text = "Maiuscole/minuscole",
                AutoSize = true,
                Margin = new Padding(0, 3, 6, 0)
            };
            workspaceSearchButton = new Button
            {
                Name = "workspaceSearchButton",
                Dock = DockStyle.Fill,
                Text = "Cerca",
                Margin = new Padding(0, 0, 6, 0)
            };
            workspaceSearchStatusLabel = new Label
            {
                Name = "workspaceSearchStatusLabel",
                Dock = DockStyle.Fill,
                Text = "Nessuna ricerca",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = AppTheme.MutedText
            };
            searchLayout.Controls.Add(workspaceSearchTextBox, 0, 0);
            searchLayout.Controls.Add(workspaceSearchKindComboBox, 1, 0);
            searchLayout.Controls.Add(workspaceSearchMatchCaseCheckBox, 2, 0);
            searchLayout.Controls.Add(workspaceSearchButton, 3, 0);
            searchLayout.Controls.Add(workspaceSearchStatusLabel, 4, 0);

            workspaceReplaceLayout = new TableLayoutPanel
            {
                Name = "workspaceReplaceLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(5, 3, 30, 4),
                Margin = new Padding(0),
                BackColor = AppTheme.Surface,
                Visible = false
            };
            workspaceReplaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            workspaceReplaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
            workspaceReplaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            workspaceReplaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12F));
            workspaceReplacementTextBox = new TextBox
            {
                Name = "workspaceReplacementTextBox",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 0),
                PlaceholderText = "Sostituisci con"
            };
            workspaceReplacementPreviewButton = new Button
            {
                Name = "workspaceReplacementPreviewButton",
                Dock = DockStyle.Fill,
                Text = "Anteprima",
                Margin = new Padding(0, 0, 6, 0)
            };
            workspaceReplacementApplyButton = new Button
            {
                Name = "workspaceReplacementApplyButton",
                Dock = DockStyle.Fill,
                Text = "Applica selezionati",
                Enabled = false,
                Margin = new Padding(0, 0, 6, 0)
            };
            workspaceReplaceLayout.Controls.Add(workspaceReplacementTextBox, 0, 0);
            workspaceReplaceLayout.Controls.Add(workspaceReplacementPreviewButton, 1, 0);
            workspaceReplaceLayout.Controls.Add(workspaceReplacementApplyButton, 2, 0);
            workspaceSearchHeaderLayout.Controls.Add(searchLayout, 0, 0);
            workspaceSearchHeaderLayout.Controls.Add(workspaceReplaceLayout, 0, 1);

            workspaceSearchResultsListView = new ListView
            {
                Name = "workspaceSearchResultsListView",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                MultiSelect = false,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F)
            };
            workspaceSearchResultsListView.Columns.Add("File", 210);
            workspaceSearchResultsListView.Columns.Add("Riga", 58);
            workspaceSearchResultsListView.Columns.Add("Tipo", 90);
            workspaceSearchResultsListView.Columns.Add("Contenuto", 620);
            workspaceSearchResultsListView.DoubleClick += workspaceSearchResultsListView_DoubleClick;
            workspaceSearchResultsListView.KeyDown += workspaceSearchResultsListView_KeyDown;
            workspaceSearchResultsListView.ItemChecked += workspaceSearchResultsListView_ItemChecked;

            tabPageWorkspaceSearch.Controls.Add(workspaceSearchResultsListView);
            tabPageWorkspaceSearch.Controls.Add(workspaceSearchHeaderLayout);
            tabControlBottom.TabPages.Add(tabPageWorkspaceSearch);

            workspaceSearchTimer = new System.Windows.Forms.Timer { Interval = 180 };
            workspaceSearchTimer.Tick += (sender, args) =>
            {
                workspaceSearchTimer.Stop();
                startWorkspaceSearch();
            };
            workspaceSearchTextBox.TextChanged += (sender, args) => scheduleWorkspaceSearch();
            workspaceSearchKindComboBox.SelectedIndexChanged += (sender, args) => scheduleWorkspaceSearch();
            workspaceSearchMatchCaseCheckBox.CheckedChanged += (sender, args) => scheduleWorkspaceSearch();
            workspaceSearchButton.Click += (sender, args) => startWorkspaceSearch();
            workspaceReplacementTextBox.TextChanged += (sender, args) =>
            {
                if (isWorkspaceReplacementMode) scheduleWorkspaceSearch();
            };
            workspaceReplacementPreviewButton.Click += (sender, args) => startWorkspaceSearch();
            workspaceReplacementApplyButton.Click += workspaceReplacementApplyButton_Click;
            workspaceSearchTextBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                args.SuppressKeyPress = true;
                startWorkspaceSearch();
            };
        }

        private void workspaceSearchMenuItem_Click(object sender, EventArgs e)
        {
            showWorkspaceSearch(false);
        }

        private void workspaceReplaceMenuItem_Click(object sender, EventArgs e)
        {
            showWorkspaceSearch(true);
        }

        private void showWorkspaceSearch(bool replacementMode)
        {
            expandDiagnosticsPalette();
            tabControlBottom.SelectedTab = tabPageWorkspaceSearch;
            isWorkspaceReplacementMode = replacementMode;
            workspaceReplaceLayout.Visible = replacementMode;
            workspaceSearchHeaderLayout.Height = replacementMode ? 70 : 35;
            workspaceSearchHeaderLayout.RowStyles[1].Height = replacementMode ? 35F : 0F;
            workspaceSearchKindComboBox.Enabled = !replacementMode;
            if (replacementMode) workspaceSearchKindComboBox.SelectedIndex = 0;
            workspaceSearchResultsListView.CheckBoxes = replacementMode;
            workspaceReplacementApplyButton.Enabled = false;
            string selectedText = richTextBoxMainEditor.SelectionLength > 0 &&
                                  !richTextBoxMainEditor.SelectedText.Contains("\n")
                ? richTextBoxMainEditor.SelectedText
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedText)) workspaceSearchTextBox.Text = selectedText;
            workspaceSearchTextBox.Focus();
            workspaceSearchTextBox.SelectAll();
            if (!string.IsNullOrWhiteSpace(workspaceSearchTextBox.Text)) scheduleWorkspaceSearch();
        }

        private void scheduleWorkspaceSearch()
        {
            if (workspaceSearchTimer == null || IsDisposed || Disposing) return;
            workspaceSearchTimer.Stop();
            workspaceSearchRequestVersion++;
            workspaceSearchCancellation?.Cancel();
            workspaceReplacementApplyButton.Enabled = false;
            if (string.IsNullOrWhiteSpace(workspaceSearchTextBox.Text))
            {
                workspaceSearchResultsListView.Items.Clear();
                workspaceSearchStatusLabel.Text = "Nessuna ricerca";
                return;
            }
            workspaceSearchTimer.Start();
        }

        private async void startWorkspaceSearch()
        {
            workspaceSearchTimer?.Stop();
            string query = workspaceSearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                scheduleWorkspaceSearch();
                return;
            }

            IReadOnlyList<DdfWorkspaceSearchDocument> documents = createWorkspaceSearchDocuments();
            bool replacementPreview = isWorkspaceReplacementMode;
            string replacement = workspaceReplacementTextBox.Text;
            DdfWorkspaceSearchKind kind = !replacementPreview && workspaceSearchKindComboBox.SelectedIndex == 1
                ? DdfWorkspaceSearchKind.Symbol
                : DdfWorkspaceSearchKind.Text;
            bool matchCase = workspaceSearchMatchCaseCheckBox.Checked;
            long version = ++workspaceSearchRequestVersion;
            workspaceSearchCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            workspaceSearchCancellation = cancellation;
            workspaceSearchStatusLabel.Text = "Ricerca in corso...";

            try
            {
                IReadOnlyList<DdfWorkspaceSearchResult> results = await Task.Run(() =>
                    DdfWorkspaceSearchService.Search(documents, query, kind, matchCase, cancellation.Token),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested || version != workspaceSearchRequestVersion) return;
                postEditorCallback(() => applyWorkspaceSearchResults(
                    version, query, replacement, replacementPreview, documents.Count, results));
            }
            catch (OperationCanceledException)
            {
                // A newer query or document snapshot superseded this search.
            }
            finally
            {
                if (ReferenceEquals(workspaceSearchCancellation, cancellation)) workspaceSearchCancellation = null;
                cancellation.Dispose();
            }
        }

        private IReadOnlyList<DdfWorkspaceSearchDocument> createWorkspaceSearchDocuments()
        {
            var documents = new Dictionary<string, DdfWorkspaceSearchDocument>(StringComparer.OrdinalIgnoreCase);
            if (workspaceIndex != null)
            {
                foreach (DdfWorkspaceDocument document in workspaceIndex.Documents)
                {
                    string key = "path:" + document.Path;
                    documents[key] = new DdfWorkspaceSearchDocument(key, document.Path, document.RelativePath, document.Source);
                }
            }

            foreach (OpenDocumentBuffer buffer in openDocuments.Documents)
            {
                string path = buffer.Session.HasPath ? buffer.Session.CurrentPath : null;
                string key = path == null ? "buffer:" + buffer.Id : "path:" + path;
                string displayName = buffer.Session.DisplayName;
                if (workspaceIndex != null && path != null)
                {
                    DdfWorkspaceDocument workspaceDocument = workspaceIndex.Documents.FirstOrDefault(document =>
                        string.Equals(document.Path, path, StringComparison.OrdinalIgnoreCase));
                    if (workspaceDocument != null) displayName = workspaceDocument.RelativePath;
                }
                documents[key] = new DdfWorkspaceSearchDocument(buffer.Id, path, displayName, buffer.Source);
            }
            return documents.Values.ToList().AsReadOnly();
        }

        private void applyWorkspaceSearchResults(long version, string query, string replacement,
            bool replacementPreview, int documentCount,
            IReadOnlyList<DdfWorkspaceSearchResult> results)
        {
            if (version != workspaceSearchRequestVersion || IsDisposed || Disposing ||
                replacementPreview != isWorkspaceReplacementMode ||
                !string.Equals(query, workspaceSearchTextBox.Text, StringComparison.Ordinal) ||
                (replacementPreview && !string.Equals(replacement, workspaceReplacementTextBox.Text, StringComparison.Ordinal))) return;
            isPopulatingWorkspaceSearchResults = true;
            workspaceSearchResultsListView.BeginUpdate();
            try
            {
                workspaceSearchResultsListView.Items.Clear();
                workspaceSearchResultsListView.CheckBoxes = replacementPreview;
                foreach (DdfWorkspaceSearchResult result in results)
                {
                    string type = result.Kind == DdfWorkspaceSearchKind.Symbol
                        ? getSearchSymbolKind(result.SymbolKind)
                        : "testo";
                    var item = new ListViewItem(result.Document.DisplayName) { Tag = result };
                    item.SubItems.Add(result.Line + ":" + result.Column);
                    item.SubItems.Add(type);
                    item.SubItems.Add(replacementPreview
                        ? DdfWorkspaceSearchService.CreateReplacementPreview(result, replacement)
                        : result.Preview);
                    item.Checked = replacementPreview;
                    workspaceSearchResultsListView.Items.Add(item);
                }
            }
            finally
            {
                workspaceSearchResultsListView.EndUpdate();
                isPopulatingWorkspaceSearchResults = false;
            }
            workspaceSearchStatusLabel.Text = (replacementPreview ? "Anteprima: " : string.Empty) +
                results.Count + (results.Count == 1 ? " risultato" : " risultati") +
                " in " + documentCount + (documentCount == 1 ? " documento" : " documenti");
            workspaceReplacementApplyButton.Enabled = replacementPreview && results.Count > 0;
            tabPageWorkspaceSearch.Text = results.Count == 0
                ? (replacementPreview ? "Sostituzione" : "Ricerca")
                : (replacementPreview ? "Sostituzione (" : "Ricerca (") + results.Count + ")";
            if (results.Count > 0) workspaceSearchResultsListView.Items[0].Selected = true;
        }

        private static string getSearchSymbolKind(DdfSymbolKind? kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "libreria";
                case DdfSymbolKind.Structure: return "struttura";
                case DdfSymbolKind.Function: return "funzione";
                case DdfSymbolKind.Parameter: return "parametro";
                case DdfSymbolKind.Field: return "campo";
                case DdfSymbolKind.Variable: return "variabile";
                default: return "simbolo";
            }
        }

        private void workspaceSearchResultsListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (isPopulatingWorkspaceSearchResults || !isWorkspaceReplacementMode) return;
            postEditorCallback(updateWorkspaceReplacementApplyState);
        }

        private void updateWorkspaceReplacementApplyState()
        {
            if (workspaceReplacementApplyButton == null || workspaceReplacementApplyButton.IsDisposed) return;
            workspaceReplacementApplyButton.Enabled = isWorkspaceReplacementMode &&
                workspaceSearchResultsListView.CheckedItems.Count > 0;
        }

        private void workspaceReplacementApplyButton_Click(object sender, EventArgs e)
        {
            List<DdfWorkspaceSearchResult> selectedResults = workspaceSearchResultsListView.CheckedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag as DdfWorkspaceSearchResult)
                .Where(result => result != null)
                .ToList();
            if (selectedResults.Count == 0)
            {
                workspaceSearchStatusLabel.Text = "Selezionare almeno un risultato da sostituire";
                return;
            }

            IReadOnlyList<DdfWorkspaceReplacementChange> changes;
            try
            {
                changes = DdfWorkspaceSearchService.CreateReplacementChanges(
                    selectedResults, workspaceReplacementTextBox.Text);
            }
            catch (ArgumentException exception)
            {
                workspaceSearchStatusLabel.Text = exception.Message;
                scheduleWorkspaceSearch();
                return;
            }

            var conflicts = new List<string>();
            foreach (DdfWorkspaceReplacementChange change in changes)
            {
                string currentSource;
                OpenDocumentBuffer buffer = findReplacementBuffer(change.Document);
                try
                {
                    currentSource = buffer != null
                        ? buffer.Source
                        : DdfDocumentFile.Load(change.Document.Path);
                }
                catch (Exception exception) when (isDocumentException(exception))
                {
                    conflicts.Add(change.Document.DisplayName + " (" + exception.Message + ")");
                    continue;
                }
                if (!string.Equals(currentSource, change.Document.Source, StringComparison.Ordinal))
                    conflicts.Add(change.Document.DisplayName);
            }

            if (conflicts.Count > 0)
            {
                workspaceSearchStatusLabel.Text = "Anteprima obsoleta: aggiornamento richiesto";
                MessageBox.Show(this,
                    "La sostituzione non è stata applicata. Questi documenti sono cambiati dopo l'anteprima:\n\n" +
                    string.Join("\n", conflicts.Distinct(StringComparer.OrdinalIgnoreCase)) +
                    "\n\nGenerare una nuova anteprima.",
                    "Anteprima non più valida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                scheduleWorkspaceSearch();
                return;
            }

            List<DdfWorkspaceReplacementChange> effectiveChanges = changes
                .Where(change => !string.Equals(change.UpdatedSource, change.Document.Source, StringComparison.Ordinal))
                .ToList();
            if (effectiveChanges.Count == 0)
            {
                workspaceSearchStatusLabel.Text = "Nessuna modifica: il testo sostitutivo è identico";
                return;
            }

            string originalDocumentId = openDocuments.ActiveDocument?.Id;
            var targetBuffers = new Dictionary<string, OpenDocumentBuffer>(StringComparer.OrdinalIgnoreCase);
            foreach (DdfWorkspaceReplacementChange change in effectiveChanges)
            {
                OpenDocumentBuffer buffer = findReplacementBuffer(change.Document);
                if (buffer == null)
                {
                    if (string.IsNullOrEmpty(change.Document.Path) || !openDocument(change.Document.Path))
                    {
                        if (originalDocumentId != null && documentViews.TryGetValue(originalDocumentId, out DocumentView original))
                            activateDocument(original);
                        return;
                    }
                    buffer = openDocuments.FindByPath(change.Document.Path);
                }
                if (buffer == null || !documentViews.ContainsKey(buffer.Id)) return;
                targetBuffers[change.Document.Id] = buffer;
            }

            int appliedDocuments = 0;
            int appliedReplacements = 0;
            foreach (DdfWorkspaceReplacementChange change in effectiveChanges)
            {
                OpenDocumentBuffer buffer = targetBuffers[change.Document.Id];
                if (buffer == null || !documentViews.TryGetValue(buffer.Id, out DocumentView view)) return;

                int selectionStart = Math.Min(view.Editor.SelectionStart, change.UpdatedSource.Length);
                int selectionLength = Math.Min(view.Editor.SelectionLength, change.UpdatedSource.Length - selectionStart);
                if (ReferenceEquals(view.Editor, richTextBoxMainEditor))
                {
                    // A complete RichEdit replacement inherits the character format at
                    // position zero. When that position is a comment, the inserted source
                    // initially becomes green. Force analysis to repaint from the start.
                    incrementalLexer.Reset();
                    diagnosticsFormatStart = 0;
                }
                view.Editor.SelectAll();
                view.Editor.SelectedText = change.UpdatedSource;
                view.Editor.Select(selectionStart, selectionLength);
                appliedDocuments++;
                appliedReplacements += change.ReplacementCount;
            }

            if (originalDocumentId != null && documentViews.TryGetValue(originalDocumentId, out DocumentView originalView))
                activateDocument(originalView);
            workspaceReplacementApplyButton.Enabled = false;
            workspaceSearchStatusLabel.Text = appliedReplacements +
                (appliedReplacements == 1 ? " sostituzione" : " sostituzioni") + " applicate in " +
                appliedDocuments + (appliedDocuments == 1 ? " documento non salvato" : " documenti non salvati");
            scheduleWorkspaceSearch();
        }

        private OpenDocumentBuffer findReplacementBuffer(DdfWorkspaceSearchDocument document)
        {
            OpenDocumentBuffer buffer = openDocuments.Documents.FirstOrDefault(item => item.Id == document.Id);
            if (buffer == null && !string.IsNullOrEmpty(document.Path)) buffer = openDocuments.FindByPath(document.Path);
            return buffer;
        }

        private void workspaceSearchResultsListView_DoubleClick(object sender, EventArgs e)
        {
            navigateToWorkspaceSearchResult();
        }

        private void workspaceSearchResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            navigateToWorkspaceSearchResult();
        }

        private void navigateToWorkspaceSearchResult()
        {
            if (workspaceSearchResultsListView.SelectedItems.Count == 0) return;
            var result = workspaceSearchResultsListView.SelectedItems[0].Tag as DdfWorkspaceSearchResult;
            if (result == null) return;
            navigateWithHistory(() => navigateToWorkspaceSearchResultCore(result));
        }

        private bool navigateToWorkspaceSearchResultCore(DdfWorkspaceSearchResult result)
        {
            OpenDocumentBuffer openBuffer = openDocuments.Documents.FirstOrDefault(document => document.Id == result.Document.Id);
            if (openBuffer != null)
                activateDocument(documentViews[openBuffer.Id]);
            else if (!string.IsNullOrEmpty(result.Document.Path) && !openDocument(result.Document.Path))
                return false;

            int safeStart = Math.Min(result.Start, richTextBoxMainEditor.TextLength);
            int safeLength = Math.Min(result.Length, richTextBoxMainEditor.TextLength - safeStart);
            if (safeLength != result.Length || safeStart + safeLength > richTextBoxMainEditor.TextLength ||
                !string.Equals(richTextBoxMainEditor.Text.Substring(safeStart, safeLength),
                    result.Document.Source.Substring(result.Start, result.Length), StringComparison.Ordinal))
            {
                scheduleWorkspaceSearch();
                return false;
            }
            leaveFoldedView();
            richTextBoxMainEditor.Select(safeStart, safeLength);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
            return true;
        }

        private void disposeWorkspaceSearch()
        {
            workspaceSearchRequestVersion++;
            workspaceSearchCancellation?.Cancel();
            workspaceSearchTimer?.Stop();
            workspaceSearchTimer?.Dispose();
            workspaceSearchTimer = null;
        }
    }
}
