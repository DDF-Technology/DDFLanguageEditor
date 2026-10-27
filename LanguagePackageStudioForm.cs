using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class LanguagePackageStudioForm : Form
    {
        private static readonly JsonSerializerOptions IndentedJson =
            new JsonSerializerOptions { WriteIndented = true };

        private readonly DdfLanguagePackage sourcePackage;
        private readonly TabControl editorTabs;
        private readonly TabPage previewPage;
        private readonly TextBox packageIdTextBox;
        private readonly TextBox packageNameTextBox;
        private readonly TextBox packageVersionTextBox;
        private readonly TextBox languageVersionTextBox;
        private readonly TextBox fileExtensionsTextBox;
        private readonly DataGridView keywordsGrid;
        private readonly DataGridView operatorsGrid;
        private readonly TextBox ebnfTextBox;
        private readonly TextBox ebnfStartRuleTextBox;
        private readonly TreeView ebnfTreeView;
        private readonly ListView ebnfDiagnosticsListView;
        private readonly ListView ebnfReferencesListView;
        private readonly Label ebnfStatusLabel;
        private readonly Timer ebnfAnalysisTimer;
        private readonly TextBox playgroundSourceTextBox;
        private readonly TextBox playgroundTokensTextBox;
        private readonly ListView playgroundDiagnosticsListView;
        private readonly ListView packageTestsListView;
        private readonly Label playgroundStatusLabel;
        private readonly Timer playgroundAnalysisTimer;
        private readonly ListBox sourceFilesListBox;
        private readonly TextBox sourceTextBox;
        private readonly TextBox previewTextBox;
        private readonly TextBox diagnosticsTextBox;
        private readonly ToolStripButton undoButton;
        private readonly ToolStripButton redoButton;
        private readonly Button confirmPreviewButton;
        private readonly Dictionary<string, string> sourceDrafts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private DdfLanguageAuthoringSession session;
        private DdfLanguageAuthoringPreview pendingPreview;
        private string selectedSourcePath;
        private bool loadingSource;
        private bool loadingEbnf;

        public LanguagePackageStudioForm(DdfLanguagePackage package)
        {
            sourcePackage = package ?? throw new ArgumentNullException(nameof(package));
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = AppTheme.Window;
            ClientSize = new Size(1120, 740);
            Font = new Font("Segoe UI", 9F);
            ForeColor = AppTheme.Text;
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon = AppIconProvider.LoadIcon();
            MinimumSize = new Size(880, 600);
            Name = "LanguagePackageStudioForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "DDF Language Package Studio";

            var tools = new ToolStrip
            {
                CanOverflow = true,
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Name = "languagePackageStudioToolStrip",
                Padding = new Padding(6, 3, 6, 3)
            };
            tools.Items.Add(Button("languageStudioPreviewGuidedButton",
                "Anteprima editor guidati", PreviewGuidedChanges));
            tools.Items.Add(Button("languageStudioPreviewSourcesButton",
                "Anteprima sorgenti", PreviewSourceChanges));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("languageStudioValidateButton",
                "Convalida", ValidateProject));
            tools.Items.Add(Button("languageStudioGenerateButton",
                "Genera cartella...", GenerateDirectory));
            tools.Items.Add(new ToolStripSeparator());
            undoButton = Button("languageStudioUndoButton", "Annulla", Undo);
            redoButton = Button("languageStudioRedoButton", "Ripristina", Redo);
            tools.Items.Add(undoButton);
            tools.Items.Add(redoButton);

            editorTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "languagePackageStudioTabs",
                Padding = new Point(12, 5)
            };

            packageIdTextBox = Field("languageStudioPackageIdTextBox");
            packageNameTextBox = Field("languageStudioPackageNameTextBox");
            packageVersionTextBox = Field("languageStudioPackageVersionTextBox");
            languageVersionTextBox = Field("languageStudioLanguageVersionTextBox");
            fileExtensionsTextBox = Field("languageStudioFileExtensionsTextBox");
            editorTabs.TabPages.Add(BuildManifestPage());

            keywordsGrid = Grid("languageStudioKeywordsGrid");
            keywordsGrid.Columns.Add(TextColumn("keywordTextColumn", "Testo", 190));
            keywordsGrid.Columns.Add(TextColumn("keywordTokenKindColumn", "Token kind", 220));
            keywordsGrid.Columns.Add(TextColumn("keywordRoleColumn", "Ruolo", 220));
            operatorsGrid = Grid("languageStudioOperatorsGrid");
            operatorsGrid.Columns.Add(TextColumn("operatorTextColumn", "Operatore", 110));
            operatorsGrid.Columns.Add(TextColumn("operatorBinaryPrecedenceColumn", "Precedenza binaria", 140));
            operatorsGrid.Columns.Add(TextColumn("operatorAssociativityColumn", "Associativita", 140));
            operatorsGrid.Columns.Add(TextColumn("operatorPrefixPrecedenceColumn", "Prefissa", 100));
            operatorsGrid.Columns.Add(TextColumn("operatorPostfixPrecedenceColumn", "Postfissa", 100));
            operatorsGrid.Columns.Add(TextColumn("operatorRoleColumn", "Ruolo", 180));
            editorTabs.TabPages.Add(BuildLexiconPage());

            ebnfTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                Font = new Font("Cascadia Mono", 10F),
                Multiline = true,
                Name = "languageStudioEbnfTextBox",
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            ebnfTextBox.TextChanged += EbnfTextChanged;
            ebnfStartRuleTextBox = Field("languageStudioEbnfStartRuleTextBox");
            ebnfStartRuleTextBox.TextChanged += EbnfStartRuleChanged;
            ebnfTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                Name = "languageStudioEbnfTreeView",
                ShowNodeToolTips = true
            };
            ebnfTreeView.NodeMouseDoubleClick += EbnfTreeNodeDoubleClick;
            ebnfDiagnosticsListView = AnalysisList(
                "languageStudioEbnfDiagnosticsListView");
            ebnfDiagnosticsListView.Columns.Add("Severita", 85);
            ebnfDiagnosticsListView.Columns.Add("Codice", 90);
            ebnfDiagnosticsListView.Columns.Add("Posizione", 105);
            ebnfDiagnosticsListView.Columns.Add("Messaggio", 420);
            ebnfDiagnosticsListView.DoubleClick += EbnfDiagnosticDoubleClick;
            ebnfReferencesListView = AnalysisList(
                "languageStudioEbnfReferencesListView");
            ebnfReferencesListView.Columns.Add("Da", 150);
            ebnfReferencesListView.Columns.Add("A", 150);
            ebnfReferencesListView.Columns.Add("Riga", 60);
            ebnfReferencesListView.Columns.Add("Colonna", 70);
            ebnfReferencesListView.DoubleClick += EbnfReferenceDoubleClick;
            ebnfStatusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Name = "languageStudioEbnfStatusLabel",
                Padding = new Padding(8, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            editorTabs.TabPages.Add(BuildEbnfPage());
            ebnfAnalysisTimer = new Timer { Interval = 250 };
            ebnfAnalysisTimer.Tick += EbnfAnalysisTimerTick;

            playgroundSourceTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                Font = new Font("Cascadia Mono", 10F),
                Multiline = true,
                Name = "languageStudioPlaygroundSourceTextBox",
                ScrollBars = ScrollBars.Both,
                Text = CreatePlaygroundSource(),
                WordWrap = false
            };
            playgroundSourceTextBox.TextChanged += PlaygroundSourceTextChanged;
            playgroundTokensTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Fill,
                Font = new Font("Cascadia Mono", 9F),
                Multiline = true,
                Name = "languageStudioPlaygroundTokensTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            playgroundDiagnosticsListView = AnalysisList(
                "languageStudioPlaygroundDiagnosticsListView");
            playgroundDiagnosticsListView.Columns.Add("Severita", 80);
            playgroundDiagnosticsListView.Columns.Add("Codice", 90);
            playgroundDiagnosticsListView.Columns.Add("Posizione", 125);
            playgroundDiagnosticsListView.Columns.Add("Messaggio", 470);
            playgroundDiagnosticsListView.DoubleClick +=
                PlaygroundDiagnosticDoubleClick;
            packageTestsListView = AnalysisList(
                "languageStudioPackageTestsListView");
            packageTestsListView.Columns.Add("Esito", 75);
            packageTestsListView.Columns.Add("Test", 240);
            packageTestsListView.Columns.Add("Dettaglio", 470);
            packageTestsListView.DoubleClick += PackageTestDoubleClick;
            playgroundStatusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Name = "languageStudioPlaygroundStatusLabel",
                Padding = new Padding(8, 0, 0, 0),
                Text = "Analisi in attesa...",
                TextAlign = ContentAlignment.MiddleLeft
            };
            editorTabs.TabPages.Add(BuildPlaygroundPage());
            playgroundAnalysisTimer = new Timer { Interval = 350 };
            playgroundAnalysisTimer.Tick += PlaygroundAnalysisTimerTick;

            sourceFilesListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Name = "languageStudioSourceFilesListBox"
            };
            sourceFilesListBox.SelectedIndexChanged += SourceFileChanged;
            sourceTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                Font = new Font("Cascadia Mono", 10F),
                Multiline = true,
                Name = "languageStudioSourceTextBox",
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            sourceTextBox.TextChanged += SourceTextChanged;
            editorTabs.TabPages.Add(BuildSourcesPage());

            previewTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Fill,
                Font = new Font("Cascadia Mono", 9F),
                Multiline = true,
                Name = "languageStudioPreviewTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            diagnosticsTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Bottom,
                Height = 104,
                Multiline = true,
                Name = "languageStudioDiagnosticsTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            confirmPreviewButton = new Button
            {
                Dock = DockStyle.Bottom,
                Enabled = false,
                Height = 36,
                Name = "languageStudioConfirmPreviewButton",
                Text = "Conferma e applica anteprima",
                UseVisualStyleBackColor = false
            };
            confirmPreviewButton.Click += ConfirmPreview;
            previewPage = new TabPage("Anteprima") { Name = "languageStudioPreviewPage" };
            previewPage.Controls.Add(previewTextBox);
            previewPage.Controls.Add(diagnosticsTextBox);
            previewPage.Controls.Add(confirmPreviewButton);
            editorTabs.TabPages.Add(previewPage);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            var sourceLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Name = "languageStudioSourceLabel",
                Padding = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Origine: " + package.Source
            };
            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Right,
                Margin = new Padding(8),
                Name = "languageStudioCloseButton",
                Text = "Chiudi",
                Width = 100,
                UseVisualStyleBackColor = false
            };
            closeButton.Click += (sender, args) => Close();
            footer.Controls.Add(sourceLabel);
            footer.Controls.Add(closeButton);

            Controls.Add(editorTabs);
            Controls.Add(footer);
            Controls.Add(tools);
            CancelButton = closeButton;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
            LoadProject();
        }

        private static ToolStripButton Button(string name, string text,
            EventHandler handler)
        {
            var button = new ToolStripButton(text)
            {
                AccessibleName = text,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Name = name
            };
            button.Click += handler;
            return button;
        }

        private static TextBox Field(string name) => new TextBox
        {
            Dock = DockStyle.Fill,
            Name = name
        };

        private static DataGridView Grid(string name) => new DataGridView
        {
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Name = name,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        private static DataGridViewTextBoxColumn TextColumn(string name,
            string header, int minimumWidth) => new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            MinimumWidth = minimumWidth,
            Name = name,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        private static ListView AnalysisList(string name) => new ListView
        {
            BackColor = AppTheme.Surface,
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.Text,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            Name = name,
            View = View.Details
        };

        private TabPage BuildManifestPage()
        {
            var page = new TabPage("Manifest") { Name = "languageStudioManifestPage" };
            var description = new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(12, 8, 12, 4),
                Text = "Modifica l'identita del pacchetto. Le sezioni non mostrate restano nel JSON autorevole. " +
                    "Le modifiche vengono applicate soltanto dopo l'anteprima."
            };
            var fields = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                RowCount = 5
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddField(fields, 0, "ID pacchetto", packageIdTextBox);
            AddField(fields, 1, "Nome", packageNameTextBox);
            AddField(fields, 2, "Versione pacchetto", packageVersionTextBox);
            AddField(fields, 3, "Versione linguaggio", languageVersionTextBox);
            AddField(fields, 4, "Estensioni (separate da virgola)", fileExtensionsTextBox);
            page.Controls.Add(fields);
            page.Controls.Add(description);
            return page;
        }

        private static void AddField(TableLayoutPanel panel, int row,
            string label, Control editor)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 7, 8, 0),
                Text = label
            }, 0, row);
            panel.Controls.Add(editor, 1, row);
        }

        private TabPage BuildLexiconPage()
        {
            var page = new TabPage("Lessico") { Name = "languageStudioLexiconPage" };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var keywords = new TabPage("Parole chiave");
            keywords.Controls.Add(keywordsGrid);
            var operators = new TabPage("Operatori");
            operators.Controls.Add(operatorsGrid);
            tabs.TabPages.Add(keywords);
            tabs.TabPages.Add(operators);
            page.Controls.Add(tabs);
            return page;
        }

        private TabPage BuildEbnfPage()
        {
            var page = new TabPage("Grammatica EBNF")
            {
                Name = "languageStudioEbnfPage"
            };
            var header = new TableLayoutPanel
            {
                ColumnCount = 4,
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(6)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 8, 0),
                Text = "Regola iniziale"
            }, 0, 0);
            header.Controls.Add(ebnfStartRuleTextBox, 1, 0);
            var previewButton = new Button
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioPreviewEbnfButton",
                Text = "Anteprima modifica EBNF",
                UseVisualStyleBackColor = false
            };
            previewButton.Click += PreviewEbnfChanges;
            header.Controls.Add(previewButton, 3, 0);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioEbnfSplitContainer",
                SplitterDistance = 590
            };
            split.Panel1.Controls.Add(ebnfTextBox);
            var analysisSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioEbnfAnalysisSplitContainer",
                Orientation = Orientation.Horizontal,
                SplitterDistance = 280
            };
            var treeGroup = new GroupBox
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioEbnfTreeGroup",
                Text = "Struttura e riferimenti per regola"
            };
            treeGroup.Controls.Add(ebnfTreeView);
            analysisSplit.Panel1.Controls.Add(treeGroup);
            var resultTabs = new TabControl { Dock = DockStyle.Fill };
            var diagnostics = new TabPage("Diagnostiche");
            diagnostics.Controls.Add(ebnfDiagnosticsListView);
            var references = new TabPage("Riferimenti");
            references.Controls.Add(ebnfReferencesListView);
            resultTabs.TabPages.Add(diagnostics);
            resultTabs.TabPages.Add(references);
            analysisSplit.Panel2.Controls.Add(resultTabs);
            split.Panel2.Controls.Add(analysisSplit);
            split.Panel2.Controls.Add(ebnfStatusLabel);
            page.Controls.Add(split);
            page.Controls.Add(header);
            return page;
        }

        private TabPage BuildSourcesPage()
        {
            var page = new TabPage("Sorgenti JSON / EBNF")
            {
                Name = "languageStudioSourcesPage"
            };
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                Name = "languageStudioSourcesSplitContainer",
                SplitterDistance = 230
            };
            split.Panel1.Controls.Add(sourceFilesListBox);
            split.Panel2.Controls.Add(sourceTextBox);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildPlaygroundPage()
        {
            var page = new TabPage("Playground e test")
            {
                Name = "languageStudioPlaygroundPage"
            };
            var header = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(6)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(2, 5, 8, 0),
                Text = "Il codice e i test vengono valutati sullo snapshot in lavorazione.",
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            var openTestsButton = new Button
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioOpenTestsButton",
                Text = "Modifica tests.json",
                UseVisualStyleBackColor = false
            };
            openTestsButton.Click += OpenTestsSource;
            header.Controls.Add(openTestsButton, 1, 0);
            var refreshButton = new Button
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioRefreshPlaygroundButton",
                Text = "Aggiorna ora",
                UseVisualStyleBackColor = false
            };
            refreshButton.Click += (sender, args) => AnalyzePlayground();
            header.Controls.Add(refreshButton, 2, 0);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioPlaygroundSplitContainer",
                SplitterDistance = 500
            };
            split.Panel1.Controls.Add(playgroundSourceTextBox);
            var resultTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioPlaygroundResultTabs"
            };
            var diagnostics = new TabPage("Diagnostiche");
            diagnostics.Controls.Add(playgroundDiagnosticsListView);
            var tokens = new TabPage("Token");
            tokens.Controls.Add(playgroundTokensTextBox);
            var tests = new TabPage("Test dichiarativi");
            tests.Controls.Add(packageTestsListView);
            resultTabs.TabPages.Add(diagnostics);
            resultTabs.TabPages.Add(tokens);
            resultTabs.TabPages.Add(tests);
            split.Panel2.Controls.Add(resultTabs);
            page.Controls.Add(split);
            page.Controls.Add(playgroundStatusLabel);
            page.Controls.Add(header);
            return page;
        }

        private void LoadProject()
        {
            DdfLanguageAuthoringTemplateResult cloned = null;
            DdfLanguageAuthoringResult loaded = null;
            var identity = new DdfLanguageAuthoringIdentity(sourcePackage.Id,
                sourcePackage.Name, sourcePackage.PackageVersion,
                sourcePackage.LanguageVersion, sourcePackage.FileExtensions);
            if (sourcePackage.Origin == DdfLanguagePackageOrigin.Embedded)
                cloned = DdfLanguageAuthoringProjectFactory.CloneEmbeddedDdf(identity);
            else if (sourcePackage.Origin == DdfLanguagePackageOrigin.Directory)
                loaded = DdfLanguagePackageAuthoring.LoadDirectory(sourcePackage.Source);
            else
                cloned = DdfLanguageAuthoringProjectFactory.ClonePackage(
                    sourcePackage.Source, identity);

            DdfLanguageAuthoringProject project = loaded?.Project ?? cloned?.Project;
            IReadOnlyList<DdfLanguagePackageDiagnostic> diagnostics = loaded?.Diagnostics ??
                cloned?.Diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>();
            if (project == null)
            {
                diagnosticsTextBox.Text = FormatDiagnostics(diagnostics);
                SetEditingEnabled(false);
                return;
            }

            session = new DdfLanguageAuthoringSession(project);
            ReloadEditors();
            diagnosticsTextBox.Text = "Progetto caricato come snapshot separato. L'origine non verra modificata.";
        }

        private void SetEditingEnabled(bool enabled)
        {
            editorTabs.Enabled = enabled;
            confirmPreviewButton.Enabled = false;
            undoButton.Enabled = false;
            redoButton.Enabled = false;
        }

        private void ReloadEditors()
        {
            if (session == null) return;
            sourceDrafts.Clear();
            pendingPreview = null;
            confirmPreviewButton.Enabled = false;
            LoadManifest();
            LoadLexicon();
            LoadEbnf();
            LoadSourceList();
            undoButton.Enabled = session.CanUndo;
            redoButton.Enabled = session.CanRedo;
            Text = "DDF Language Package Studio — revisione " + session.Project.Revision;
            SchedulePlaygroundAnalysis();
        }

        private void LoadManifest()
        {
            JsonObject manifest = ParseObject("manifest.json");
            packageIdTextBox.Text = TextValue(manifest, "id");
            packageNameTextBox.Text = TextValue(manifest, "name");
            packageVersionTextBox.Text = TextValue(manifest, "packageVersion");
            languageVersionTextBox.Text = TextValue(manifest, "languageVersion");
            fileExtensionsTextBox.Text = string.Join(", ",
                manifest?["fileExtensions"]?.AsArray().Select(item =>
                    item?.GetValue<string>() ?? string.Empty) ?? Array.Empty<string>());
        }

        private void LoadLexicon()
        {
            JsonObject lexicon = ParseObject("lexicon.json");
            keywordsGrid.Rows.Clear();
            foreach (JsonNode node in lexicon?["keywords"]?.AsArray() ?? new JsonArray())
            {
                JsonObject keyword = node as JsonObject;
                keywordsGrid.Rows.Add(TextValue(keyword, "text"),
                    TextValue(keyword, "tokenKind"), TextValue(keyword, "role"));
            }
            operatorsGrid.Rows.Clear();
            foreach (JsonNode node in lexicon?["operators"]?.AsArray() ?? new JsonArray())
            {
                JsonObject op = node as JsonObject;
                operatorsGrid.Rows.Add(TextValue(op, "text"),
                    NumberValue(op, "binaryPrecedence"), TextValue(op, "associativity"),
                    NumberValue(op, "prefixPrecedence"), NumberValue(op, "postfixPrecedence"),
                    TextValue(op, "role"));
            }
        }

        private void LoadEbnf()
        {
            JsonObject grammar = ParseObject("grammar.json");
            if (!session.Project.TryGetDocument("grammar.ebnf",
                    out DdfLanguageAuthoringDocument document))
                throw new InvalidDataException("Documento mancante: grammar.ebnf");
            loadingEbnf = true;
            try
            {
                ebnfStartRuleTextBox.Text = TextValue(grammar, "startRule");
                ebnfTextBox.Text = sourceDrafts.TryGetValue("grammar.ebnf",
                    out string draft) ? draft : document.Content;
            }
            finally { loadingEbnf = false; }
            AnalyzeEbnf();
        }

        private void LoadSourceList()
        {
            string previous = selectedSourcePath;
            sourceFilesListBox.BeginUpdate();
            sourceFilesListBox.Items.Clear();
            foreach (string path in session.Project.Documents.Keys.OrderBy(item => item,
                StringComparer.OrdinalIgnoreCase))
                sourceFilesListBox.Items.Add(path);
            sourceFilesListBox.EndUpdate();
            int index = previous == null ? 0 : sourceFilesListBox.Items.IndexOf(previous);
            sourceFilesListBox.SelectedIndex = index >= 0 ? index : 0;
        }

        private JsonObject ParseObject(string path)
        {
            if (!session.Project.TryGetDocument(path, out DdfLanguageAuthoringDocument document))
                throw new InvalidDataException("Documento mancante: " + path);
            string content = sourceDrafts.TryGetValue(path, out string draft)
                ? draft : document.Content;
            return JsonNode.Parse(content, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }) as JsonObject ?? throw new InvalidDataException("Oggetto JSON non valido: " + path);
        }

        private static string TextValue(JsonObject value, string property) =>
            value?[property]?.GetValue<string>() ?? string.Empty;

        private static object NumberValue(JsonObject value, string property) =>
            value?[property] == null ? string.Empty : value[property].ToString();

        private void SourceFileChanged(object sender, EventArgs e)
        {
            selectedSourcePath = sourceFilesListBox.SelectedItem as string;
            loadingSource = true;
            try
            {
                sourceTextBox.Text = selectedSourcePath != null &&
                    sourceDrafts.TryGetValue(selectedSourcePath, out string draft)
                    ? draft
                    : selectedSourcePath != null && session.Project.TryGetDocument(
                        selectedSourcePath, out DdfLanguageAuthoringDocument document)
                        ? document.Content : string.Empty;
            }
            finally { loadingSource = false; }
        }

        private void SourceTextChanged(object sender, EventArgs e)
        {
            if (!loadingSource && selectedSourcePath != null)
            {
                sourceDrafts[selectedSourcePath] = sourceTextBox.Text;
                if (string.Equals(selectedSourcePath, "grammar.ebnf",
                        StringComparison.OrdinalIgnoreCase) && !loadingEbnf &&
                    !string.Equals(ebnfTextBox.Text, sourceTextBox.Text,
                        StringComparison.Ordinal))
                {
                    loadingEbnf = true;
                    try { ebnfTextBox.Text = sourceTextBox.Text; }
                    finally { loadingEbnf = false; }
                    ScheduleEbnfAnalysis();
                }
                SchedulePlaygroundAnalysis();
            }
        }

        private void EbnfTextChanged(object sender, EventArgs e)
        {
            if (loadingEbnf) return;
            sourceDrafts["grammar.ebnf"] = ebnfTextBox.Text;
            if (string.Equals(selectedSourcePath, "grammar.ebnf",
                    StringComparison.OrdinalIgnoreCase) && !loadingSource)
            {
                loadingSource = true;
                try { sourceTextBox.Text = ebnfTextBox.Text; }
                finally { loadingSource = false; }
            }
            ScheduleEbnfAnalysis();
            SchedulePlaygroundAnalysis();
        }

        private void EbnfStartRuleChanged(object sender, EventArgs e)
        {
            if (!loadingEbnf)
            {
                ScheduleEbnfAnalysis();
                SchedulePlaygroundAnalysis();
            }
        }

        private void ScheduleEbnfAnalysis()
        {
            ebnfAnalysisTimer.Stop();
            ebnfAnalysisTimer.Start();
        }

        private void EbnfAnalysisTimerTick(object sender, EventArgs e)
        {
            ebnfAnalysisTimer.Stop();
            AnalyzeEbnf();
        }

        private void AnalyzeEbnf()
        {
            DdfEbnfAuthoringAnalysis analysis =
                DdfEbnfAuthoringAnalyzer.Analyze(ebnfTextBox.Text,
                    ebnfStartRuleTextBox.Text);
            ebnfTreeView.BeginUpdate();
            ebnfTreeView.Nodes.Clear();
            var root = new TreeNode("Grammatica — " + analysis.Rules.Count +
                " regole") { Name = "grammar" };
            foreach (DdfEbnfRuleAnalysis rule in analysis.Rules)
            {
                string suffix = string.Equals(rule.Name, analysis.StartRule,
                    StringComparison.Ordinal) ? "  [iniziale]" : string.Empty;
                var ruleNode = new TreeNode(rule.Name + suffix) { Tag = rule };
                foreach (DdfEbnfRuleReference reference in rule.References)
                    ruleNode.Nodes.Add(new TreeNode("→ " + reference.TargetRule)
                    {
                        Tag = reference
                    });
                if (rule.ReferencedBy.Count > 0)
                    ruleNode.ToolTipText = "Richiamata da: " +
                        string.Join(", ", rule.ReferencedBy);
                root.Nodes.Add(ruleNode);
            }
            ebnfTreeView.Nodes.Add(root);
            root.Expand();
            ebnfTreeView.EndUpdate();

            ebnfDiagnosticsListView.BeginUpdate();
            ebnfDiagnosticsListView.Items.Clear();
            foreach (DdfEbnfAuthoringDiagnostic diagnostic in analysis.Diagnostics)
            {
                var item = new ListViewItem(diagnostic.Severity.ToString())
                {
                    Tag = diagnostic
                };
                item.SubItems.Add(diagnostic.Code);
                item.SubItems.Add(diagnostic.Line + ":" + diagnostic.Column);
                item.SubItems.Add(diagnostic.Message);
                ebnfDiagnosticsListView.Items.Add(item);
            }
            ebnfDiagnosticsListView.EndUpdate();

            ebnfReferencesListView.BeginUpdate();
            ebnfReferencesListView.Items.Clear();
            foreach (DdfEbnfRuleReference reference in analysis.References)
            {
                var item = new ListViewItem(reference.SourceRule)
                {
                    Tag = reference
                };
                item.SubItems.Add(reference.TargetRule);
                item.SubItems.Add(reference.Line.ToString());
                item.SubItems.Add(reference.Column.ToString());
                ebnfReferencesListView.Items.Add(item);
            }
            ebnfReferencesListView.EndUpdate();
            int errors = analysis.Diagnostics.Count(item => item.Severity ==
                DdfEbnfAuthoringDiagnosticSeverity.Error);
            int warnings = analysis.Diagnostics.Count(item => item.Severity ==
                DdfEbnfAuthoringDiagnosticSeverity.Warning);
            ebnfStatusLabel.Text = errors == 0
                ? analysis.Rules.Count + " regole · " + analysis.References.Count +
                    " riferimenti · " + warnings + " avvisi"
                : errors + " errori · " + warnings + " avvisi";
            ebnfStatusLabel.ForeColor = errors == 0 ? AppTheme.Run : AppTheme.Error;
        }

        private void EbnfTreeNodeDoubleClick(object sender,
            TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is DdfEbnfRuleAnalysis rule)
                NavigateEbnf(rule.Start, rule.Length);
            else if (e.Node?.Tag is DdfEbnfRuleReference reference)
                NavigateEbnf(reference.Start, reference.Length);
        }

        private void EbnfDiagnosticDoubleClick(object sender, EventArgs e)
        {
            if (ebnfDiagnosticsListView.SelectedItems.Count == 1 &&
                ebnfDiagnosticsListView.SelectedItems[0].Tag is
                    DdfEbnfAuthoringDiagnostic diagnostic)
                NavigateEbnf(diagnostic.Start, diagnostic.Length);
        }

        private void EbnfReferenceDoubleClick(object sender, EventArgs e)
        {
            if (ebnfReferencesListView.SelectedItems.Count == 1 &&
                ebnfReferencesListView.SelectedItems[0].Tag is
                    DdfEbnfRuleReference reference)
                NavigateEbnf(reference.Start, reference.Length);
        }

        private void NavigateEbnf(int start, int length)
        {
            editorTabs.SelectedTab = editorTabs.TabPages[
                "languageStudioEbnfPage"];
            ebnfTextBox.Focus();
            ebnfTextBox.Select(Math.Min(start, ebnfTextBox.TextLength),
                Math.Min(Math.Max(1, length), Math.Max(0,
                    ebnfTextBox.TextLength - Math.Min(start,
                        ebnfTextBox.TextLength))));
            ebnfTextBox.ScrollToCaret();
        }

        private string CreatePlaygroundSource()
        {
            string canonical = "main() out int" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    ret 42;" + Environment.NewLine +
                "}";
            DdfLanguagePackageLoadResult embedded =
                DdfLanguagePackageLoader.LoadEmbeddedDefault();
            if (!embedded.Success || string.Equals(sourcePackage.Id,
                    embedded.Package.Id, StringComparison.Ordinal))
                return canonical;
            return DdfSyntaxProfileConverter.Convert(canonical,
                embedded.Package.Language, sourcePackage.Language).Text;
        }

        private void PlaygroundSourceTextChanged(object sender, EventArgs e) =>
            SchedulePlaygroundAnalysis();

        private void SchedulePlaygroundAnalysis()
        {
            if (playgroundAnalysisTimer == null || session == null) return;
            playgroundAnalysisTimer.Stop();
            playgroundAnalysisTimer.Start();
        }

        private void PlaygroundAnalysisTimerTick(object sender, EventArgs e)
        {
            playgroundAnalysisTimer.Stop();
            AnalyzePlayground();
        }

        private DdfLanguageAuthoringProject BuildLiveProject()
        {
            var drafts = new Dictionary<string, string>(sourceDrafts,
                StringComparer.OrdinalIgnoreCase);
            JsonObject grammar = ParseObject("grammar.json");
            grammar["startRule"] = ebnfStartRuleTextBox.Text.Trim();
            string grammarContent = Serialize(grammar);
            if (!session.Project.TryGetDocument("grammar.json",
                    out DdfLanguageAuthoringDocument grammarDocument) ||
                !string.Equals(grammarDocument.Content, grammarContent,
                    StringComparison.Ordinal))
                drafts["grammar.json"] = grammarContent;
            if (drafts.Count == 0) return session.Project;
            var changeSet = new DdfLanguageAuthoringChangeSet(
                session.Project.Revision,
                drafts.OrderBy(item => item.Key,
                    StringComparer.OrdinalIgnoreCase).Select(item =>
                    DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                "Snapshot live Package Studio");
            DdfLanguageAuthoringResult result =
                DdfLanguagePackageAuthoring.Apply(session.Project, changeSet);
            return result.Project;
        }

        private void AnalyzePlayground()
        {
            if (session == null) return;
            playgroundDiagnosticsListView.BeginUpdate();
            packageTestsListView.BeginUpdate();
            try
            {
                playgroundDiagnosticsListView.Items.Clear();
                packageTestsListView.Items.Clear();
                DdfLanguageAuthoringProject project = BuildLiveProject();
                if (project == null)
                {
                    playgroundStatusLabel.Text = "Snapshot in lavorazione non valido.";
                    playgroundStatusLabel.ForeColor = AppTheme.Error;
                    return;
                }

                DdfLanguagePackageLoadResult evaluated =
                    DdfLanguagePackageAuthoring.Evaluate(project);
                foreach (DdfLanguagePackageDiagnostic diagnostic in
                    evaluated.Diagnostics)
                    AddPackageDiagnostic(diagnostic);
                if (evaluated.Package == null)
                {
                    playgroundTokensTextBox.Text =
                        "Token non disponibili: correggere la definizione del pacchetto.";
                    playgroundStatusLabel.Text = evaluated.Diagnostics.Count +
                        " diagnostiche di definizione · Playground sospeso";
                    playgroundStatusLabel.ForeColor = AppTheme.Error;
                    return;
                }

                string source = playgroundSourceTextBox.Text;
                DdfLanguagePackage package = evaluated.Package;
                DdfLexResult lex = DdfLexer.Lex(source, package.Language);
                DdfParseResult parse = DdfParser.Parse(source, package.Language);
                DdfTypeCheckResult types = DdfTypeChecker.Check(source,
                    parse.Root, null, package.Language.Types,
                    package.Language.StandardLibrary, null,
                    package.Language.Runtime);
                DdfIrBuildResult ir = DdfIrLowerer.Lower(source, parse,
                    package.Language);
                IReadOnlyList<DdfDiagnostic> sourceDiagnostics = lex.Diagnostics
                    .Concat(parse.Diagnostics).Concat(types.Diagnostics)
                    .Concat(ir.Diagnostics)
                    .GroupBy(item => item.Code + ":" + item.Start + ":" +
                        item.Length + ":" + item.Message, StringComparer.Ordinal)
                    .Select(group => group.First()).ToList().AsReadOnly();
                foreach (DdfDiagnostic diagnostic in sourceDiagnostics)
                    AddSourceDiagnostic(diagnostic);

                playgroundTokensTextBox.Text = string.Join(Environment.NewLine,
                    lex.Tokens.Select(token => token.Kind + "  " + token.Start +
                        ":" + token.Length + "  " + EscapeText(SliceText(source,
                            token.Start, token.Length))));
                IReadOnlyList<DdfLanguagePackageTestResult> tests =
                    DdfLanguagePackageTestRunner.Run(package, package.Tests);
                foreach (DdfLanguagePackageTestResult test in tests)
                {
                    var item = new ListViewItem(test.Passed ? "OK" : "ERRORE")
                    {
                        Tag = test
                    };
                    item.SubItems.Add(test.Name);
                    item.SubItems.Add(test.Message);
                    item.ForeColor = test.Passed ? AppTheme.Run : AppTheme.Error;
                    packageTestsListView.Items.Add(item);
                }
                int definitionErrors = evaluated.Diagnostics.Count(item =>
                    item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
                int sourceErrors = sourceDiagnostics.Count(item =>
                    item.Severity == DdfDiagnosticSeverity.Error);
                int failedTests = tests.Count(item => !item.Passed);
                playgroundStatusLabel.Text = lex.Tokens.Count + " token · " +
                    sourceErrors + " errori sorgente · " + definitionErrors +
                    " errori pacchetto · " + (tests.Count - failedTests) + "/" +
                    tests.Count + " test superati";
                playgroundStatusLabel.ForeColor = sourceErrors == 0 &&
                    definitionErrors == 0 && failedTests == 0
                    ? AppTheme.Run : AppTheme.Error;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException || exception is IOException ||
                exception is JsonException || exception is InvalidDataException)
            {
                playgroundStatusLabel.Text = "Analisi non disponibile: " +
                    exception.Message;
                playgroundStatusLabel.ForeColor = AppTheme.Error;
            }
            finally
            {
                playgroundDiagnosticsListView.EndUpdate();
                packageTestsListView.EndUpdate();
            }
        }

        private void AddPackageDiagnostic(DdfLanguagePackageDiagnostic diagnostic)
        {
            var item = new ListViewItem(diagnostic.Severity.ToString())
            {
                Tag = diagnostic
            };
            item.SubItems.Add(diagnostic.Code);
            item.SubItems.Add(string.IsNullOrEmpty(diagnostic.File)
                ? diagnostic.JsonPath : diagnostic.File + diagnostic.JsonPath);
            item.SubItems.Add(diagnostic.Message);
            playgroundDiagnosticsListView.Items.Add(item);
        }

        private void AddSourceDiagnostic(DdfDiagnostic diagnostic)
        {
            var item = new ListViewItem(diagnostic.Severity.ToString())
            {
                Tag = diagnostic
            };
            item.SubItems.Add(diagnostic.Code);
            item.SubItems.Add(diagnostic.Line + ":" + diagnostic.Column);
            item.SubItems.Add(diagnostic.DisplayMessage);
            playgroundDiagnosticsListView.Items.Add(item);
        }

        private void PlaygroundDiagnosticDoubleClick(object sender,
            EventArgs e)
        {
            if (playgroundDiagnosticsListView.SelectedItems.Count != 1) return;
            object target = playgroundDiagnosticsListView.SelectedItems[0].Tag;
            if (target is DdfDiagnostic diagnostic)
            {
                playgroundSourceTextBox.Focus();
                int start = Math.Min(diagnostic.Start,
                    playgroundSourceTextBox.TextLength);
                playgroundSourceTextBox.Select(start, Math.Min(
                    Math.Max(1, diagnostic.Length),
                    Math.Max(0, playgroundSourceTextBox.TextLength - start)));
                playgroundSourceTextBox.ScrollToCaret();
            }
            else if (target is DdfLanguagePackageDiagnostic packageDiagnostic)
                NavigateSourceDocument(packageDiagnostic.File);
        }

        private void PackageTestDoubleClick(object sender, EventArgs e) =>
            NavigateSourceDocument("tests.json");

        private void OpenTestsSource(object sender, EventArgs e) =>
            NavigateSourceDocument("tests.json");

        private void NavigateSourceDocument(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || sourceFilesListBox == null)
                return;
            string normalized = path.Replace('\\', '/');
            int index = sourceFilesListBox.Items.IndexOf(normalized);
            if (index < 0)
            {
                string fileName = Path.GetFileName(normalized);
                for (int current = 0; current < sourceFilesListBox.Items.Count;
                    current++)
                {
                    if (string.Equals(Path.GetFileName(Convert.ToString(
                            sourceFilesListBox.Items[current])), fileName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        index = current;
                        break;
                    }
                }
            }
            if (index < 0) return;
            sourceFilesListBox.SelectedIndex = index;
            editorTabs.SelectedTab = editorTabs.TabPages[
                "languageStudioSourcesPage"];
            sourceTextBox.Focus();
        }

        private static string SliceText(string source, int start, int length)
        {
            if (string.IsNullOrEmpty(source) || start < 0 || length <= 0 ||
                start >= source.Length) return string.Empty;
            return source.Substring(start, Math.Min(length,
                source.Length - start));
        }

        private static string EscapeText(string value) =>
            value.Replace("\r", "\\r").Replace("\n", "\\n")
                .Replace("\t", "\\t");

        private void PreviewEbnfChanges(object sender, EventArgs e)
        {
            if (session == null) return;
            try
            {
                DdfEbnfAuthoringAnalysis analysis =
                    DdfEbnfAuthoringAnalyzer.Analyze(ebnfTextBox.Text,
                        ebnfStartRuleTextBox.Text);
                if (!analysis.Success)
                {
                    ShowPreviewFailure("Correggere le diagnostiche EBNF prima " +
                        "di creare l'anteprima.");
                    return;
                }
                sourceDrafts["grammar.ebnf"] = ebnfTextBox.Text;
                JsonObject grammar = ParseObject("grammar.json");
                grammar["startRule"] = ebnfStartRuleTextBox.Text.Trim();
                var combined = new Dictionary<string, string>(sourceDrafts,
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["grammar.json"] = Serialize(grammar),
                    ["grammar.ebnf"] = ebnfTextBox.Text
                };
                PreviewChanges(combined.OrderBy(item => item.Key,
                    StringComparer.OrdinalIgnoreCase).Select(item =>
                        DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                    "Aggiorna grammatica EBNF e regola iniziale");
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is InvalidOperationException ||
                exception is InvalidDataException)
            {
                ShowPreviewFailure(exception.Message);
            }
        }

        private void PreviewGuidedChanges(object sender, EventArgs e)
        {
            if (session == null) return;
            try
            {
                JsonObject manifest = ParseObject("manifest.json");
                manifest["id"] = packageIdTextBox.Text.Trim();
                manifest["name"] = packageNameTextBox.Text.Trim();
                manifest["packageVersion"] = packageVersionTextBox.Text.Trim();
                manifest["languageVersion"] = languageVersionTextBox.Text.Trim();
                var extensions = new JsonArray();
                foreach (string extension in fileExtensionsTextBox.Text.Split(',')
                    .Select(item => item.Trim()).Where(item => item.Length != 0))
                    extensions.Add(extension);
                manifest["fileExtensions"] = extensions;

                JsonObject lexicon = ParseObject("lexicon.json");
                var keywords = new JsonArray();
                foreach (DataGridViewRow row in keywordsGrid.Rows.Cast<DataGridViewRow>()
                    .Where(item => !item.IsNewRow))
                {
                    string text = Cell(row, 0);
                    if (text.Length == 0) continue;
                    keywords.Add(new JsonObject
                    {
                        ["text"] = text,
                        ["tokenKind"] = Cell(row, 1),
                        ["role"] = Cell(row, 2)
                    });
                }
                lexicon["keywords"] = keywords;

                var operators = new JsonArray();
                foreach (DataGridViewRow row in operatorsGrid.Rows.Cast<DataGridViewRow>()
                    .Where(item => !item.IsNewRow))
                {
                    string text = Cell(row, 0);
                    if (text.Length == 0) continue;
                    var op = new JsonObject { ["text"] = text };
                    AddInteger(op, "binaryPrecedence", Cell(row, 1));
                    op["associativity"] = Cell(row, 2);
                    AddInteger(op, "prefixPrecedence", Cell(row, 3));
                    AddInteger(op, "postfixPrecedence", Cell(row, 4));
                    op["role"] = Cell(row, 5);
                    operators.Add(op);
                }
                lexicon["operators"] = operators;

                var combined = new Dictionary<string, string>(sourceDrafts,
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["manifest.json"] = Serialize(manifest),
                    ["lexicon.json"] = Serialize(lexicon)
                };
                PreviewChanges(combined.OrderBy(item => item.Key,
                    StringComparer.OrdinalIgnoreCase).Select(item =>
                        DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                    "Aggiorna manifest, parole chiave, operatori e draft sorgente");
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is InvalidOperationException ||
                exception is InvalidDataException || exception is FormatException)
            {
                ShowPreviewFailure(exception.Message);
            }
        }

        private void PreviewSourceChanges(object sender, EventArgs e)
        {
            if (session == null) return;
            if (sourceDrafts.Count == 0)
            {
                ShowPreviewFailure("Nessun sorgente e stato modificato.");
                return;
            }
            PreviewChanges(sourceDrafts.OrderBy(item => item.Key,
                StringComparer.OrdinalIgnoreCase).Select(item =>
                    DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                "Aggiorna sorgenti JSON/EBNF");
        }

        private void PreviewChanges(IEnumerable<DdfLanguageAuthoringChange> changes,
            string description)
        {
            var changeSet = new DdfLanguageAuthoringChangeSet(
                session.Project.Revision, changes, description);
            pendingPreview = DdfLanguagePackageAuthoring.Preview(session.Project,
                changeSet);
            RenderPreview(pendingPreview);
        }

        private void RenderPreview(DdfLanguageAuthoringPreview preview)
        {
            var text = new StringBuilder();
            foreach (DdfLanguageAuthoringDocumentDiff document in preview.Documents)
            {
                text.AppendLine("=== " + document.RelativePath + " [" + document.Kind + "] ===");
                foreach (DdfLanguageAuthoringDiffLine line in document.Lines)
                {
                    char prefix = line.Kind == DdfLanguageAuthoringDiffLineKind.Added ? '+' :
                        line.Kind == DdfLanguageAuthoringDiffLineKind.Removed ? '-' : ' ';
                    text.Append(prefix).Append(' ').AppendLine(line.Text);
                }
                text.AppendLine();
            }
            if (preview.Documents.Count == 0)
                text.AppendLine("Nessuna differenza rispetto allo snapshot corrente.");
            previewTextBox.Text = text.ToString();
            diagnosticsTextBox.Text = FormatDiagnostics(preview.Diagnostics);
            if (diagnosticsTextBox.Text.Length == 0)
                diagnosticsTextBox.Text = preview.Success
                    ? "Anteprima pronta. Controllare il diff prima della conferma."
                    : "L'anteprima non puo essere applicata.";
            confirmPreviewButton.Enabled = preview.Success && preview.HasChanges;
            editorTabs.SelectedTab = previewPage;
        }

        private void ShowPreviewFailure(string message)
        {
            pendingPreview = null;
            previewTextBox.Clear();
            diagnosticsTextBox.Text = message;
            confirmPreviewButton.Enabled = false;
            editorTabs.SelectedTab = previewPage;
        }

        private void ConfirmPreview(object sender, EventArgs e)
        {
            if (session == null || pendingPreview == null) return;
            DdfLanguageAuthoringResult result = session.Commit(pendingPreview);
            if (!result.Success)
            {
                diagnosticsTextBox.Text = FormatDiagnostics(result.Diagnostics);
                confirmPreviewButton.Enabled = false;
                return;
            }
            ReloadEditors();
            diagnosticsTextBox.Text = "Modifica applicata allo snapshot. L'origine e ancora invariata.";
        }

        private void ValidateProject(object sender, EventArgs e)
        {
            if (session == null) return;
            DdfLanguagePackageLoadResult result =
                DdfLanguagePackageAuthoring.Validate(session.Project);
            diagnosticsTextBox.Text = result.Success
                ? "Convalida e test dichiarativi completati correttamente."
                : FormatDiagnostics(result.Diagnostics);
            editorTabs.SelectedTab = previewPage;
        }

        private void Undo(object sender, EventArgs e)
        {
            DdfLanguageAuthoringResult result = session?.Undo();
            if (result == null) return;
            if (result.Success) ReloadEditors();
            diagnosticsTextBox.Text = result.Success ? "Modifica annullata."
                : FormatDiagnostics(result.Diagnostics);
        }

        private void Redo(object sender, EventArgs e)
        {
            DdfLanguageAuthoringResult result = session?.Redo();
            if (result == null) return;
            if (result.Success) ReloadEditors();
            diagnosticsTextBox.Text = result.Success ? "Modifica ripristinata."
                : FormatDiagnostics(result.Diagnostics);
        }

        private void GenerateDirectory(object sender, EventArgs e)
        {
            if (session == null) return;
            using var dialog = new FolderBrowserDialog
            {
                Description = "Selezionare la cartella padre. Il Package Studio creera una nuova sottocartella.",
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string safeName = packageIdTextBox.Text.Trim();
            if (safeName.Length == 0) safeName = "ddf-language-package";
            string destination = Path.Combine(dialog.SelectedPath, safeName);
            DdfLanguageAuthoringGenerationResult result =
                DdfLanguagePackageAuthoring.GenerateDirectory(session.Project,
                    destination);
            diagnosticsTextBox.Text = result.Success
                ? "Pacchetto validato e generato in: " + result.OutputPath
                : FormatDiagnostics(result.Diagnostics);
            editorTabs.SelectedTab = previewPage;
        }

        private static string Cell(DataGridViewRow row, int index) =>
            Convert.ToString(row.Cells[index].Value)?.Trim() ?? string.Empty;

        private static void AddInteger(JsonObject value, string property,
            string text)
        {
            if (text.Length == 0) return;
            if (!int.TryParse(text, out int number))
                throw new FormatException("Valore numerico non valido per " + property + ".");
            value[property] = number;
        }

        private static string Serialize(JsonObject value) =>
            value.ToJsonString(IndentedJson) + Environment.NewLine;

        private static string FormatDiagnostics(
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics) =>
            string.Join(Environment.NewLine, (diagnostics ??
                Array.Empty<DdfLanguagePackageDiagnostic>()).Select(item => item.ToString()));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ebnfAnalysisTimer?.Dispose();
                playgroundAnalysisTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
