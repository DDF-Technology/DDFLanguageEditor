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
        private ToolStripButton toolbarWorkspaceSearchButton;
        private TabPage tabPageWorkspaceSearch;
        private TextBox workspaceSearchTextBox;
        private ComboBox workspaceSearchKindComboBox;
        private CheckBox workspaceSearchMatchCaseCheckBox;
        private Button workspaceSearchButton;
        private Label workspaceSearchStatusLabel;
        private ListView workspaceSearchResultsListView;
        private System.Windows.Forms.Timer workspaceSearchTimer;
        private CancellationTokenSource workspaceSearchCancellation;
        private long workspaceSearchRequestVersion;

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

            toolbarWorkspaceSearchButton = createToolbarButton(
                "toolbarWorkspaceSearchButton", "\uE773", "Trova nel workspace (Ctrl+Alt+F)", workspaceSearchMenuItem_Click);
            int toolbarFindIndex = toolStripMain.Items.IndexOfKey("toolbarFindButton");
            toolStripMain.Items.Insert(toolbarFindIndex + 1, toolbarWorkspaceSearchButton);

            tabPageWorkspaceSearch = new TabPage("Ricerca")
            {
                Name = "tabPageWorkspaceSearch",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            var searchLayout = new TableLayoutPanel
            {
                Name = "workspaceSearchLayout",
                Dock = DockStyle.Top,
                Height = 35,
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

            tabPageWorkspaceSearch.Controls.Add(workspaceSearchResultsListView);
            tabPageWorkspaceSearch.Controls.Add(searchLayout);
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
            workspaceSearchTextBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                args.SuppressKeyPress = true;
                startWorkspaceSearch();
            };
        }

        private void workspaceSearchMenuItem_Click(object sender, EventArgs e)
        {
            expandDiagnosticsPalette();
            tabControlBottom.SelectedTab = tabPageWorkspaceSearch;
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
            if (string.IsNullOrWhiteSpace(workspaceSearchTextBox.Text))
            {
                workspaceSearchRequestVersion++;
                workspaceSearchCancellation?.Cancel();
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
            DdfWorkspaceSearchKind kind = workspaceSearchKindComboBox.SelectedIndex == 1
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
                postEditorCallback(() => applyWorkspaceSearchResults(version, query, documents.Count, results));
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

        private void applyWorkspaceSearchResults(long version, string query, int documentCount,
            IReadOnlyList<DdfWorkspaceSearchResult> results)
        {
            if (version != workspaceSearchRequestVersion || IsDisposed || Disposing) return;
            workspaceSearchResultsListView.BeginUpdate();
            try
            {
                workspaceSearchResultsListView.Items.Clear();
                foreach (DdfWorkspaceSearchResult result in results)
                {
                    string type = result.Kind == DdfWorkspaceSearchKind.Symbol
                        ? getSearchSymbolKind(result.SymbolKind)
                        : "testo";
                    var item = new ListViewItem(result.Document.DisplayName) { Tag = result };
                    item.SubItems.Add(result.Line + ":" + result.Column);
                    item.SubItems.Add(type);
                    item.SubItems.Add(result.Preview);
                    workspaceSearchResultsListView.Items.Add(item);
                }
            }
            finally
            {
                workspaceSearchResultsListView.EndUpdate();
            }
            workspaceSearchStatusLabel.Text = results.Count + (results.Count == 1 ? " risultato" : " risultati") +
                                                  " in " + documentCount + (documentCount == 1 ? " documento" : " documenti");
            tabPageWorkspaceSearch.Text = results.Count == 0 ? "Ricerca" : "Ricerca (" + results.Count + ")";
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

            OpenDocumentBuffer openBuffer = openDocuments.Documents.FirstOrDefault(document => document.Id == result.Document.Id);
            if (openBuffer != null)
                activateDocument(documentViews[openBuffer.Id]);
            else if (!string.IsNullOrEmpty(result.Document.Path) && !openDocument(result.Document.Path))
                return;

            int safeStart = Math.Min(result.Start, richTextBoxMainEditor.TextLength);
            int safeLength = Math.Min(result.Length, richTextBoxMainEditor.TextLength - safeStart);
            if (safeLength != result.Length || safeStart + safeLength > richTextBoxMainEditor.TextLength ||
                !string.Equals(richTextBoxMainEditor.Text.Substring(safeStart, safeLength),
                    result.Document.Source.Substring(result.Start, result.Length), StringComparison.Ordinal))
            {
                scheduleWorkspaceSearch();
                return;
            }
            leaveFoldedView();
            richTextBoxMainEditor.Select(safeStart, safeLength);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
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
