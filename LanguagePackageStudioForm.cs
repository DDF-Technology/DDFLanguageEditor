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
        private readonly DdfLanguageAuthoringProject initialProject;
        private readonly TabControl editorTabs;
        private readonly TabPage previewPage;
        private readonly TextBox packageIdTextBox;
        private readonly TextBox packageNameTextBox;
        private readonly TextBox packageVersionTextBox;
        private readonly TextBox languageVersionTextBox;
        private readonly TextBox fileExtensionsTextBox;
        private readonly DataGridView keywordsGrid;
        private readonly ComboBox guidedKeywordComboBox;
        private readonly TextBox guidedKeywordTextBox;
        private readonly TextBox guidedKeywordImpactTextBox;
        private readonly ListView guidedProfileListView;
        private readonly Dictionary<string, string> guidedKeywordChanges =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly DataGridView operatorsGrid;
        private readonly TextBox typesDescriptionTextBox;
        private readonly DataGridView primitiveTypesGrid;
        private readonly DataGridView conversionsGrid;
        private readonly TextBox semanticsDescriptionTextBox;
        private readonly DataGridView semanticBindingsGrid;
        private readonly TextBox formattingDescriptionTextBox;
        private readonly DataGridView formattingTokensGrid;
        private readonly DataGridView autoPairsGrid;
        private readonly DataGridView indentAfterGrid;
        private readonly DataGridView quickFixesGrid;
        private readonly CheckBox blankLineBetweenStatementsCheckBox;
        private readonly TextBox intellisenseDescriptionTextBox;
        private readonly DataGridView completionLabelsGrid;
        private readonly DataGridView signatureLabelsGrid;
        private readonly DataGridView symbolKindsGrid;
        private readonly DataGridView snippetsGrid;
        private readonly DataGridView flowchartLabelsGrid;
        private readonly DataGridView flowchartExplanationsGrid;
        private readonly DataGridView diagnosticMessagesGrid;
        private readonly TextBox runtimeDescriptionTextBox;
        private readonly TextBox runtimeEntryPointTextBox;
        private readonly TextBox runtimeInstructionLimitTextBox;
        private readonly TextBox runtimeOutputTargetTextBox;
        private readonly DataGridView vmOperationsGrid;
        private readonly DataGridView expressionOperationsGrid;
        private readonly TextBox buildDescriptionTextBox;
        private readonly TextBox buildDefaultBackendTextBox;
        private readonly TextBox buildArtifactFormatTextBox;
        private readonly DataGridView buildBackendsGrid;
        private readonly TextBox standardLibraryDescriptionTextBox;
        private readonly TextBox standardLibraryIdTextBox;
        private readonly TextBox standardLibraryVersionTextBox;
        private readonly DataGridView standardFunctionsGrid;
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
            : this(package, null)
        {
        }

        internal LanguagePackageStudioForm(DdfLanguagePackage package,
            DdfLanguageAuthoringProject project)
        {
            sourcePackage = package ?? throw new ArgumentNullException(nameof(package));
            initialProject = project;
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
            tools.Items.Add(Button("languageStudioCompareButton",
                "Confronta versione...", CompareVersion));
            tools.Items.Add(Button("languageStudioGenerateButton",
                "Genera cartella...", GenerateDirectory));
            tools.Items.Add(Button("languageStudioGenerateArchiveButton",
                "Genera .ddflang...", GenerateArchive));
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
            guidedKeywordComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "languageStudioGuidedKeywordComboBox",
                Width = 170
            };
            guidedKeywordTextBox = Field("languageStudioGuidedKeywordTextBox");
            guidedKeywordTextBox.Dock = DockStyle.None;
            guidedKeywordTextBox.Width = 170;
            guidedKeywordTextBox.TextChanged += (sender, args) =>
                InvalidateGuidedProfilePreview();
            guidedKeywordImpactTextBox = new TextBox
            {
                Dock = DockStyle.Fill, Height = 140, Multiline = true,
                Name = "languageStudioGuidedKeywordImpactTextBox",
                ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = AppTheme.Surface
            };
            guidedProfileListView = new ListView
            {
                Dock = DockStyle.Fill, FullRowSelect = true,
                GridLines = true, HideSelection = false,
                MultiSelect = false,
                Name = "languageStudioGuidedProfileListView",
                View = View.Details
            };
            guidedProfileListView.Columns.Add("Parola corrente", 190);
            guidedProfileListView.Columns.Add("Nuova scrittura", 190);
            guidedKeywordComboBox.SelectedIndexChanged += (sender, args) =>
            {
                string selected = guidedKeywordComboBox.SelectedItem?.ToString() ?? string.Empty;
                guidedKeywordTextBox.Text = guidedKeywordChanges.TryGetValue(selected,
                    out string staged) ? staged : selected;
                UpdateGuidedKeywordImpact();
            };
            operatorsGrid = Grid("languageStudioOperatorsGrid");
            operatorsGrid.Columns.Add(TextColumn("operatorTextColumn", "Operatore", 110));
            operatorsGrid.Columns.Add(TextColumn("operatorBinaryPrecedenceColumn", "Precedenza binaria", 140));
            operatorsGrid.Columns.Add(TextColumn("operatorAssociativityColumn", "Associativita", 140));
            operatorsGrid.Columns.Add(TextColumn("operatorPrefixPrecedenceColumn", "Prefissa", 100));
            operatorsGrid.Columns.Add(TextColumn("operatorPostfixPrecedenceColumn", "Postfissa", 100));
            operatorsGrid.Columns.Add(TextColumn("operatorRoleColumn", "Ruolo", 180));
            editorTabs.TabPages.Add(BuildLexiconPage());

            typesDescriptionTextBox = Field("languageStudioTypesDescriptionTextBox");
            primitiveTypesGrid = Grid("languageStudioPrimitiveTypesGrid");
            primitiveTypesGrid.Columns.Add(TextColumn("primitiveNameColumn", "Nome", 130));
            primitiveTypesGrid.Columns.Add(ComboColumn("primitiveCategoryColumn", "Categoria", 150,
                "Void", "Boolean", "Integer", "Floating", "Character", "String"));
            primitiveTypesGrid.Columns.Add(CheckColumn("primitiveCanDeclareColumn", "Dichiarabile", 110));
            primitiveTypesGrid.Columns.Add(TextColumn("primitiveIndexedElementColumn", "Elemento indicizzato", 170));
            conversionsGrid = Grid("languageStudioConversionsGrid");
            conversionsGrid.Columns.Add(TextColumn("conversionFromColumn", "Da", 150));
            conversionsGrid.Columns.Add(TextColumn("conversionToColumn", "A", 150));
            conversionsGrid.Columns.Add(ComboColumn("conversionContextColumn", "Contesto", 310,
                "General", "VariableInitializerSingleCharacterLiteral"));
            semanticsDescriptionTextBox = Field("languageStudioSemanticsDescriptionTextBox");
            semanticBindingsGrid = Grid("languageStudioSemanticBindingsGrid");
            semanticBindingsGrid.Columns.Add(TextColumn("semanticRuleColumn", "Regola EBNF", 190));
            semanticBindingsGrid.Columns.Add(ComboColumn("semanticOperationColumn", "Operazione", 210,
                "VisitCompilationUnit", "ImportLibrary", "DeclareStructure", "DeclareFunction",
                "DeclareParameter", "DeclareEnumeration", "Branch", "Select", "PreconditionLoop",
                "PostconditionLoop", "CountedLoop", "Break", "Continue", "Terminate", "CreateScope",
                "DeclareVariable", "Return", "EvaluateStatement", "InferExpression"));
            semanticBindingsGrid.Columns.Add(TextColumn("semanticArgumentsColumn", "Argomenti (JSON)", 390));
            editorTabs.TabPages.Add(BuildTypesAndSemanticsPage());

            formattingDescriptionTextBox = Field("languageStudioFormattingDescriptionTextBox");
            formattingTokensGrid = PropertyGrid("languageStudioFormattingTokensGrid");
            autoPairsGrid = Grid("languageStudioAutoPairsGrid");
            autoPairsGrid.Columns.Add(TextColumn("autoPairOpenColumn", "Apertura", 140));
            autoPairsGrid.Columns.Add(TextColumn("autoPairCloseColumn", "Chiusura", 140));
            indentAfterGrid = Grid("languageStudioIndentAfterGrid");
            indentAfterGrid.Columns.Add(TextColumn("indentAfterTokenColumn", "Indenta dopo", 180));
            quickFixesGrid = Grid("languageStudioQuickFixesGrid");
            quickFixesGrid.Columns.Add(TextColumn("quickFixCodeColumn", "Codice diagnostico", 145));
            quickFixesGrid.Columns.Add(TextColumn("quickFixTitleColumn", "Titolo", 300));
            quickFixesGrid.Columns.Add(TextColumn("quickFixReplacementColumn", "Sostituzione", 160));
            blankLineBetweenStatementsCheckBox = new CheckBox
            {
                AutoSize = true,
                Name = "languageStudioBlankLineBetweenStatementsCheckBox",
                Text = "Riga vuota tra istruzioni"
            };
            editorTabs.TabPages.Add(BuildFormattingPage());

            intellisenseDescriptionTextBox = Field("languageStudioIntellisenseDescriptionTextBox");
            completionLabelsGrid = PropertyGrid("languageStudioCompletionLabelsGrid");
            signatureLabelsGrid = PropertyGrid("languageStudioSignatureLabelsGrid");
            symbolKindsGrid = Grid("languageStudioSymbolKindsGrid");
            symbolKindsGrid.Columns.Add(TextColumn("symbolKindColumn", "Tipo simbolo", 170));
            symbolKindsGrid.Columns.Add(TextColumn("symbolLabelColumn", "Etichetta", 180));
            symbolKindsGrid.Columns.Add(TextColumn("symbolDocumentationColumn", "Documentazione", 400));
            snippetsGrid = Grid("languageStudioSnippetsGrid");
            snippetsGrid.Columns.Add(TextColumn("snippetPrefixColumn", "Prefisso", 130));
            snippetsGrid.Columns.Add(TextColumn("snippetDisplayColumn", "Testo mostrato", 210));
            snippetsGrid.Columns.Add(TextColumn("snippetDescriptionColumn", "Descrizione", 250));
            snippetsGrid.Columns.Add(TextColumn("snippetBodyColumn", "Corpo", 360));
            snippetsGrid.Columns.Add(CheckColumn("snippetRequiresKeywordColumn", "Richiede keyword", 130));
            flowchartLabelsGrid = PropertyGrid("languageStudioFlowchartLabelsGrid");
            flowchartExplanationsGrid = PropertyGrid("languageStudioFlowchartExplanationsGrid",
                allowNewRows: true);
            diagnosticMessagesGrid = PropertyGrid("languageStudioDiagnosticMessagesGrid",
                allowNewRows: true);
            editorTabs.TabPages.Add(BuildIntellisensePage());

            runtimeDescriptionTextBox = Field("languageStudioRuntimeDescriptionTextBox");
            runtimeEntryPointTextBox = Field("languageStudioRuntimeEntryPointTextBox");
            runtimeInstructionLimitTextBox = Field("languageStudioRuntimeInstructionLimitTextBox");
            runtimeOutputTargetTextBox = Field("languageStudioRuntimeOutputTargetTextBox");
            vmOperationsGrid = Grid("languageStudioVmOperationsGrid");
            vmOperationsGrid.Columns.Add(TextColumn("vmOperationColumn", "Operazione VM", 280));
            expressionOperationsGrid = Grid("languageStudioExpressionOperationsGrid");
            expressionOperationsGrid.Columns.Add(ComboColumn("expressionCategoryColumn", "Categoria", 150,
                "unary", "binary", "postfix"));
            expressionOperationsGrid.Columns.Add(TextColumn("expressionTokenColumn", "Token", 120));
            expressionOperationsGrid.Columns.Add(TextColumn("expressionOperationColumn", "Operazione", 280));
            editorTabs.TabPages.Add(BuildRuntimePage());

            buildDescriptionTextBox = Field("languageStudioBuildDescriptionTextBox");
            buildDefaultBackendTextBox = Field("languageStudioBuildDefaultBackendTextBox");
            buildArtifactFormatTextBox = Field("languageStudioBuildArtifactFormatTextBox");
            buildBackendsGrid = Grid("languageStudioBuildBackendsGrid");
            buildBackendsGrid.Columns.Add(TextColumn("buildBackendColumn", "Backend", 250));
            editorTabs.TabPages.Add(BuildBuildPage());

            standardLibraryDescriptionTextBox = Field("languageStudioStandardLibraryDescriptionTextBox");
            standardLibraryIdTextBox = Field("languageStudioStandardLibraryIdTextBox");
            standardLibraryVersionTextBox = Field("languageStudioStandardLibraryVersionTextBox");
            standardFunctionsGrid = Grid("languageStudioStandardFunctionsGrid");
            standardFunctionsGrid.Columns.Add(TextColumn("standardNameColumn", "Funzione", 150));
            standardFunctionsGrid.Columns.Add(TextColumn("standardReturnColumn", "Ritorno", 105));
            standardFunctionsGrid.Columns.Add(TextColumn("standardParametersColumn", "Parametri (JSON)", 180));
            standardFunctionsGrid.Columns.Add(TextColumn("standardDocumentationColumn", "Documentazione", 280));
            standardFunctionsGrid.Columns.Add(TextColumn("standardBindingColumn", "Binding", 155));
            standardFunctionsGrid.Columns.Add(TextColumn("standardEffectColumn", "Effetto", 110));
            standardFunctionsGrid.Columns.Add(TextColumn("standardErrorsColumn", "Errori (JSON)", 165));
            editorTabs.TabPages.Add(BuildStandardLibraryPage());

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
            editorTabs.TabPages.Insert(0, BuildGettingStartedPage());

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            var sourceLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Name = "languageStudioSourceLabel",
                Padding = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Origine: " + (project?.SourceRoot ?? package.Source)
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

        private static DataGridView PropertyGrid(string name,
            bool allowNewRows = false)
        {
            DataGridView grid = Grid(name);
            grid.AllowUserToAddRows = allowNewRows;
            var property = TextColumn(name + "PropertyColumn", "Proprietà", 220);
            property.ReadOnly = !allowNewRows;
            grid.Columns.Add(property);
            grid.Columns.Add(TextColumn(name + "ValueColumn", "Valore", 420));
            return grid;
        }

        private static DataGridViewTextBoxColumn TextColumn(string name,
            string header, int minimumWidth) => new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            MinimumWidth = minimumWidth,
            Name = name,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        private static DataGridViewComboBoxColumn ComboColumn(string name,
            string header, int minimumWidth, params string[] values)
        {
            var column = new DataGridViewComboBoxColumn
            {
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                HeaderText = header,
                MinimumWidth = minimumWidth,
                Name = name,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            column.Items.AddRange(values.Cast<object>().ToArray());
            return column;
        }

        private static DataGridViewCheckBoxColumn CheckColumn(string name,
            string header, int minimumWidth) => new DataGridViewCheckBoxColumn
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

        private TabPage BuildGettingStartedPage()
        {
            var page = new TabPage("Inizia")
            {
                Name = "languageStudioGettingStartedPage",
                Padding = new Padding(24), BackColor = AppTheme.Surface,
                AutoScroll = true
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
                Padding = new Padding(8), RowCount = 9
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddGuideRow(0, "1 · Identità", "Assegna ID e nome diversi dal DDF incorporato; aggiorna la versione del pacchetto.",
                "languageStudioManifestPage");
            AddGuideRow(1, "2 · Sintassi", "In Lessico modifica parole chiave e operatori. Per nuove regole usa Grammatica EBNF e Tipi e semantica.",
                "languageStudioLexiconPage");
            AddGuideRow(2, "3 · Prova", "Nel Playground e test scrivi codice di esempio e leggi token e diagnostiche. Il pacchetto attivo nell'editor non cambia.",
                "languageStudioPlaygroundPage");
            AddGuideRow(3, "4 · Verifica", "Usa Anteprima editor guidati e conferma le modifiche; poi Convalida. Gli errori devono essere risolti prima della distribuzione.",
                "languageStudioPreviewPage");
            AddGuideRow(4, "5 · Genera .ddflang...", "Convalida la bozza e scegli dove salvare l'archivio. Se ci sono modifiche non confermate, mostra prima l'anteprima. L'installazione resta separata.",
                null, PrepareGuidedDistribution);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var note = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill,
                ForeColor = AppTheme.MutedText,
                Margin = new Padding(4, 18, 4, 4),
                Text = "Percorso semplice: modifica una copia del DDF, prova nel Playground, convalida ed esporta. Le altre schede sono strumenti avanzati: non occorre toccarle per cambiare soltanto la sintassi."
            };
            layout.Controls.Add(note, 0, 5);
            layout.SetColumnSpan(note, 2);
            var keywordEditor = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(4, 16, 4, 4), WrapContents = true
            };
            keywordEditor.Controls.Add(new Label
            {
                AutoSize = true, Margin = new Padding(0, 7, 8, 0),
                Text = "Cambia parola chiave:"
            });
            keywordEditor.Controls.Add(guidedKeywordComboBox);
            keywordEditor.Controls.Add(new Label
            {
                AutoSize = true, Margin = new Padding(12, 7, 8, 0),
                Text = "Nuova scrittura:"
            });
            keywordEditor.Controls.Add(guidedKeywordTextBox);
            var stageButton = new Button
            {
                AutoSize = true, Name = "languageStudioGuidedKeywordStageButton",
                Text = "Aggiungi al profilo", UseVisualStyleBackColor = false
            };
            stageButton.Click += StageGuidedKeywordChange;
            keywordEditor.Controls.Add(stageButton);
            var keywordPreviewButton = new Button
            {
                AutoSize = true, Name = "languageStudioGuidedKeywordPreviewButton",
                Text = "Anteprima profilo", UseVisualStyleBackColor = false
            };
            keywordPreviewButton.Click += PreviewGuidedKeywordChange;
            keywordEditor.Controls.Add(keywordPreviewButton);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(keywordEditor, 0, 6);
            layout.SetColumnSpan(keywordEditor, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            layout.Controls.Add(guidedKeywordImpactTextBox, 0, 7);
            layout.SetColumnSpan(guidedKeywordImpactTextBox, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            var profilePanel = new TableLayoutPanel
            {
                ColumnCount = 2, Dock = DockStyle.Fill,
                Margin = new Padding(4, 5, 4, 5), RowCount = 1
            };
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            profilePanel.Controls.Add(guidedProfileListView, 0, 0);
            var removeProfileButton = new Button
            {
                Dock = DockStyle.Top, Height = 32,
                Name = "languageStudioGuidedProfileRemoveButton",
                Text = "Rimuovi selezionata", UseVisualStyleBackColor = false
            };
            removeProfileButton.Click += RemoveGuidedKeywordChange;
            profilePanel.Controls.Add(removeProfileButton, 1, 0);
            layout.Controls.Add(profilePanel, 0, 8);
            layout.SetColumnSpan(profilePanel, 2);
            page.Controls.Add(layout);
            return page;

            void AddGuideRow(int row, string title, string description,
                string destination, EventHandler action = null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
                var button = new Button
                {
                    Name = "languageStudioGuideStep" + row + "Button",
                    Text = title, Dock = DockStyle.Fill,
                    Margin = new Padding(4, 6, 8, 6),
                    UseVisualStyleBackColor = false
                };
                if (action != null)
                    button.Click += action;
                else
                    button.Click += (sender, args) => editorTabs.SelectedTab =
                        editorTabs.TabPages.Cast<TabPage>().First(tab =>
                            tab.Name == destination);
                layout.Controls.Add(button, 0, row);
                layout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = description,
                    Margin = new Padding(4, 3, 4, 3)
                }, 1, row);
            }
        }

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

        private TabPage BuildTypesAndSemanticsPage()
        {
            var page = new TabPage("Tipi e semantica")
            {
                Name = "languageStudioTypesSemanticsPage"
            };
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "languageStudioTypesSemanticsTabs"
            };
            tabs.TabPages.Add(BuildGridPage("Tipi primitivi",
                "Descrizione tipi", typesDescriptionTextBox, primitiveTypesGrid));
            tabs.TabPages.Add(BuildGridPage("Conversioni", null, null,
                conversionsGrid));
            tabs.TabPages.Add(BuildGridPage("Binding semantici",
                "Descrizione semantica", semanticsDescriptionTextBox,
                semanticBindingsGrid));
            page.Controls.Add(tabs);
            return page;
        }

        private TabPage BuildFormattingPage()
        {
            var page = new TabPage("Formatter")
            {
                Name = "languageStudioFormattingPage"
            };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage tokens = BuildGridPage("Token", "Descrizione formatter",
                formattingDescriptionTextBox, formattingTokensGrid);
            var layoutOption = new Panel { Dock = DockStyle.Bottom, Height = 34 };
            layoutOption.Controls.Add(blankLineBetweenStatementsCheckBox);
            blankLineBetweenStatementsCheckBox.Location = new Point(8, 7);
            tokens.Controls.Add(layoutOption);
            tabs.TabPages.Add(tokens);
            tabs.TabPages.Add(BuildGridPage("Coppie automatiche", null, null,
                autoPairsGrid));
            tabs.TabPages.Add(BuildGridPage("Indentazione", null, null,
                indentAfterGrid));
            tabs.TabPages.Add(BuildGridPage("Correzioni rapide", null, null,
                quickFixesGrid));
            page.Controls.Add(tabs);
            return page;
        }

        private TabPage BuildIntellisensePage()
        {
            var page = new TabPage("IntelliSense")
            {
                Name = "languageStudioIntellisensePage"
            };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildGridPage("Completamento",
                "Descrizione IntelliSense", intellisenseDescriptionTextBox,
                completionLabelsGrid));
            tabs.TabPages.Add(BuildGridPage("Aiuto firme", null, null,
                signatureLabelsGrid));
            tabs.TabPages.Add(BuildGridPage("Tipi di simbolo", null, null,
                symbolKindsGrid));
            tabs.TabPages.Add(BuildGridPage("Snippet", null, null,
                snippetsGrid));
            var flowchart = new TabPage("Etichette Flowchart");
            var flowchartSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 230
            };
            flowchartSplit.Panel1.Controls.Add(flowchartLabelsGrid);
            flowchartSplit.Panel2.Controls.Add(flowchartExplanationsGrid);
            flowchart.Controls.Add(flowchartSplit);
            tabs.TabPages.Add(flowchart);
            tabs.TabPages.Add(BuildGridPage("Diagnostiche", null, null,
                diagnosticMessagesGrid));
            page.Controls.Add(tabs);
            return page;
        }

        private TabPage BuildRuntimePage()
        {
            var page = new TabPage("Runtime") { Name = "languageStudioRuntimePage" };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var settings = BuildGridPage("Operazioni VM", null, null, vmOperationsGrid);
            settings.Controls.Add(SettingsHeader(
                ("Descrizione", runtimeDescriptionTextBox),
                ("Entry point", runtimeEntryPointTextBox),
                ("Limite istruzioni", runtimeInstructionLimitTextBox),
                ("Output", runtimeOutputTargetTextBox)));
            tabs.TabPages.Add(settings);
            tabs.TabPages.Add(BuildGridPage("Operazioni espressioni", null, null,
                expressionOperationsGrid));
            page.Controls.Add(tabs);
            return page;
        }

        private TabPage BuildBuildPage()
        {
            var page = BuildGridPage("Build", null, null, buildBackendsGrid);
            page.Name = "languageStudioBuildPage";
            page.Controls.Add(SettingsHeader(
                ("Descrizione", buildDescriptionTextBox),
                ("Backend predefinito", buildDefaultBackendTextBox),
                ("Formato artefatto", buildArtifactFormatTextBox)));
            return page;
        }

        private TabPage BuildStandardLibraryPage()
        {
            var page = BuildGridPage("Libreria standard", null, null,
                standardFunctionsGrid);
            page.Name = "languageStudioStandardLibraryPage";
            page.Controls.Add(SettingsHeader(
                ("Descrizione", standardLibraryDescriptionTextBox),
                ("ID", standardLibraryIdTextBox),
                ("Versione", standardLibraryVersionTextBox)));
            return page;
        }

        private static Control SettingsHeader(params (string Label, TextBox Field)[] fields)
        {
            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Height = fields.Length * 34 + 8,
                Padding = new Padding(8, 4, 8, 4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < fields.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 5, 8, 0),
                    Text = fields[i].Label
                }, 0, i);
                layout.Controls.Add(fields[i].Field, 1, i);
            }
            return layout;
        }

        private static TabPage BuildGridPage(string title, string label,
            TextBox description, DataGridView grid)
        {
            var page = new TabPage(title);
            page.Controls.Add(grid);
            if (description != null)
            {
                var header = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(8, 6, 8, 4)
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 5, 8, 0),
                    Text = label
                }, 0, 0);
                header.Controls.Add(description, 1, 0);
                page.Controls.Add(header);
            }
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
            DdfLanguageAuthoringProject project = initialProject;
            var identity = new DdfLanguageAuthoringIdentity(sourcePackage.Id,
                sourcePackage.Name, sourcePackage.PackageVersion,
                sourcePackage.LanguageVersion, sourcePackage.FileExtensions);
            if (project == null)
            {
                if (sourcePackage.Origin == DdfLanguagePackageOrigin.Embedded)
                    cloned = DdfLanguageAuthoringProjectFactory.CloneEmbeddedDdf(identity);
                else if (sourcePackage.Origin == DdfLanguagePackageOrigin.Directory)
                    loaded = DdfLanguagePackageAuthoring.LoadDirectory(sourcePackage.Source);
                else
                    cloned = DdfLanguageAuthoringProjectFactory.ClonePackage(
                        sourcePackage.Source, identity);
            }

            project ??= loaded?.Project ?? cloned?.Project;
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
            if (string.IsNullOrWhiteSpace(diagnosticsTextBox.Text))
                diagnosticsTextBox.Text = "Progetto caricato come snapshot separato. " +
                    "L'origine non verra modificata.";
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
            guidedKeywordChanges.Clear();
            guidedProfileListView.Items.Clear();
            pendingPreview = null;
            confirmPreviewButton.Enabled = false;
            diagnosticsTextBox.Clear();
            try
            {
                LoadManifest();
                LoadLexicon();
                LoadTypesAndSemantics();
                LoadFormattingAndIntellisense();
                LoadRuntimeBuildAndStandardLibrary();
                LoadEbnf();
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is InvalidDataException || exception is FormatException ||
                exception is InvalidOperationException)
            {
                diagnosticsTextBox.Text = "Gli editor guidati richiedono JSON validi. " +
                    "Correggere il documento nella scheda Sorgenti JSON / EBNF. " +
                    exception.Message;
                editorTabs.SelectedTab = editorTabs.TabPages["languageStudioSourcesPage"];
            }
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
            guidedKeywordComboBox.Items.Clear();
            foreach (JsonNode node in lexicon?["keywords"]?.AsArray() ?? new JsonArray())
            {
                JsonObject keyword = node as JsonObject;
                string keywordText = TextValue(keyword, "text");
                keywordsGrid.Rows.Add(keywordText,
                    TextValue(keyword, "tokenKind"), TextValue(keyword, "role"));
                guidedKeywordComboBox.Items.Add(keywordText);
            }
            if (guidedKeywordComboBox.Items.Count > 0)
                guidedKeywordComboBox.SelectedIndex = 0;
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

        private void UpdateGuidedKeywordImpact()
        {
            string keyword = guidedKeywordComboBox.SelectedItem?.ToString();
            if (session == null || string.IsNullOrEmpty(keyword))
            {
                guidedKeywordImpactTextBox.Clear();
                return;
            }
            var text = new StringBuilder();
            DataGridViewRow row = keywordsGrid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(item => !item.IsNewRow && Cell(item, 0) == keyword);
            text.Append("Parola: ").Append(keyword);
            if (row != null)
                text.Append("    Categoria: ").Append(Cell(row, 1))
                    .Append("    Ruolo: ").Append(Cell(row, 2));
            text.AppendLine();
            text.AppendLine("Il lessico riconosce la scrittura; grammatica, test e snippet possono usarla ancora. L'anteprima indica se il pacchetto resta valido.");
            try
            {
                if (session.Project.TryGetDocument("grammar.ebnf",
                    out DdfLanguageAuthoringDocument grammar))
                {
                    string ebnf = sourceDrafts.TryGetValue("grammar.ebnf", out string draft)
                        ? draft : grammar.Content;
                    string[] rules = ebnf.Split(new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => line.Contains("\"" + keyword + "\""))
                        .Select(line => line.Split('=')[0].Trim())
                        .Where(name => name.Length != 0).Distinct().Take(4).ToArray();
                    text.AppendLine("Grammatica EBNF: " + (rules.Length == 0
                        ? "nessun terminale letterale trovato"
                        : string.Join(", ", rules)));
                }
                JsonObject tests = ParseObject("tests.json");
                var matchingTests = new List<JsonObject>();
                foreach (JsonNode node in tests?["cases"]?.AsArray() ?? new JsonArray())
                    if (node is JsonObject test &&
                        ContainsWord(TextValue(test, "source"), keyword))
                        matchingTests.Add(test);
                text.AppendLine("Test da controllare: " + matchingTests.Count +
                    (matchingTests.Count == 0 ? string.Empty : " (" +
                        string.Join(", ", matchingTests.Take(4).Select(item =>
                            TextValue(item, "name"))) +
                        (matchingTests.Count > 4 ? ", ..." : string.Empty) + ")"));
                if (matchingTests.Count != 0)
                {
                    string example = TextValue(matchingTests[0], "source")
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                        .FirstOrDefault(line => ContainsWord(line, keyword))?.Trim();
                    if (!string.IsNullOrEmpty(example))
                        text.AppendLine("Esempio: " + (example.Length > 100
                            ? example.Substring(0, 100) + "..." : example));
                }
                JsonObject intellisense = ParseObject("intellisense.json");
                string[] snippets = (intellisense?["snippets"]?.AsArray() ??
                    new JsonArray()).OfType<JsonObject>()
                    .Where(item => ContainsWord(TextValue(item, "prefix"), keyword) ||
                        ContainsWord(TextValue(item, "body"), keyword))
                    .Select(item => TextValue(item, "prefix")).Take(5).ToArray();
                text.AppendLine("Snippet IntelliSense: " + (snippets.Length == 0
                    ? "nessuno trovato" : string.Join(", ", snippets)));
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is InvalidDataException || exception is InvalidOperationException)
            {
                text.AppendLine("Riferimenti non leggibili: correggere prima i sorgenti JSON/EBNF. " +
                    exception.Message);
            }
            text.Append("Il cambio guidato prepara insieme lessico, grammatica, " +
                "test e snippet. Verificare il diff prima della conferma.");
            text.AppendLine().Append("Parole nel profilo: ")
                .Append(guidedKeywordChanges.Count);
            guidedKeywordImpactTextBox.Text = text.ToString();
        }

        private static bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
            int position = 0;
            while ((position = text.IndexOf(word, position,
                StringComparison.Ordinal)) >= 0)
            {
                int end = position + word.Length;
                bool left = position == 0 || !IsWordCharacter(text[position - 1]);
                bool right = end == text.Length || !IsWordCharacter(text[end]);
                if (left && right) return true;
                position = end;
            }
            return false;
        }

        private static bool IsWordCharacter(char value) =>
            char.IsLetterOrDigit(value) || value == '_';

        private void LoadTypesAndSemantics()
        {
            JsonObject types = ParseObject("types.json");
            typesDescriptionTextBox.Text = TextValue(types, "description");
            primitiveTypesGrid.Rows.Clear();
            foreach (JsonNode node in types?["primitives"]?.AsArray() ?? new JsonArray())
            {
                JsonObject primitive = node as JsonObject;
                primitiveTypesGrid.Rows.Add(TextValue(primitive, "name"),
                    TextValue(primitive, "category"),
                    primitive?["canDeclareVariable"]?.GetValue<bool>() ?? false,
                    TextValue(primitive, "indexedElementType"));
            }
            conversionsGrid.Rows.Clear();
            foreach (JsonNode node in types?["conversions"]?.AsArray() ?? new JsonArray())
            {
                JsonObject conversion = node as JsonObject;
                conversionsGrid.Rows.Add(TextValue(conversion, "from"),
                    TextValue(conversion, "to"), TextValue(conversion, "context"));
            }

            JsonObject semantics = ParseObject("semantics.json");
            semanticsDescriptionTextBox.Text = TextValue(semantics, "description");
            semanticBindingsGrid.Rows.Clear();
            foreach (JsonNode node in semantics?["bindings"]?.AsArray() ?? new JsonArray())
            {
                JsonObject binding = node as JsonObject;
                string rule = TextValue(binding, "rule");
                foreach (JsonNode operationNode in binding?["operations"]?.AsArray() ??
                    new JsonArray())
                {
                    JsonObject operation = operationNode as JsonObject;
                    semanticBindingsGrid.Rows.Add(rule, TextValue(operation, "id"),
                        (operation?["arguments"] as JsonObject)?.ToJsonString() ?? "{}");
                }
            }
        }

        private void LoadFormattingAndIntellisense()
        {
            JsonObject formatting = ParseObject("formatting.json");
            formattingDescriptionTextBox.Text = TextValue(formatting, "description");
            LoadProperties(formattingTokensGrid, formatting?["tokens"] as JsonObject);
            blankLineBetweenStatementsCheckBox.Checked =
                formatting?["blankLineBetweenStatements"]?.GetValue<bool>() ?? false;
            autoPairsGrid.Rows.Clear();
            foreach (JsonNode node in formatting?["autoPairs"]?.AsArray() ?? new JsonArray())
            {
                JsonObject pair = node as JsonObject;
                autoPairsGrid.Rows.Add(TextValue(pair, "open"), TextValue(pair, "close"));
            }
            indentAfterGrid.Rows.Clear();
            foreach (JsonNode node in formatting?["indentAfter"]?.AsArray() ?? new JsonArray())
                indentAfterGrid.Rows.Add(node?.GetValue<string>() ?? string.Empty);
            quickFixesGrid.Rows.Clear();
            foreach (JsonNode node in formatting?["quickFixes"]?.AsArray() ?? new JsonArray())
            {
                JsonObject fix = node as JsonObject;
                quickFixesGrid.Rows.Add(TextValue(fix, "diagnosticCode"),
                    TextValue(fix, "title"), TextValue(fix, "replacement"));
            }

            JsonObject intellisense = ParseObject("intellisense.json");
            intellisenseDescriptionTextBox.Text = TextValue(intellisense, "description");
            LoadProperties(completionLabelsGrid,
                intellisense?["completion"] as JsonObject);
            LoadProperties(signatureLabelsGrid,
                intellisense?["signatureHelp"] as JsonObject);
            symbolKindsGrid.Rows.Clear();
            foreach (JsonNode node in intellisense?["symbolKinds"]?.AsArray() ??
                new JsonArray())
            {
                JsonObject symbol = node as JsonObject;
                symbolKindsGrid.Rows.Add(TextValue(symbol, "kind"),
                    TextValue(symbol, "label"), TextValue(symbol, "documentation"));
            }
            snippetsGrid.Rows.Clear();
            foreach (JsonNode node in intellisense?["snippets"]?.AsArray() ??
                new JsonArray())
            {
                JsonObject snippet = node as JsonObject;
                snippetsGrid.Rows.Add(TextValue(snippet, "prefix"),
                    TextValue(snippet, "displayText"),
                    TextValue(snippet, "description"), TextValue(snippet, "body"),
                    snippet?["requiresKeyword"]?.GetValue<bool>() ?? false);
            }
            JsonObject flowchart = intellisense?["flowchart"] as JsonObject;
            LoadProperties(flowchartLabelsGrid, flowchart, "explanations");
            LoadProperties(flowchartExplanationsGrid,
                flowchart?["explanations"] as JsonObject);
            LoadProperties(diagnosticMessagesGrid,
                intellisense?["diagnostics"] as JsonObject);
        }

        private static void LoadProperties(DataGridView grid, JsonObject values,
            string excludedKey = null)
        {
            grid.Rows.Clear();
            if (values == null) return;
            foreach (KeyValuePair<string, JsonNode> item in values)
                if (!string.Equals(item.Key, excludedKey,
                        StringComparison.Ordinal))
                    grid.Rows.Add(item.Key, item.Value?.GetValue<string>() ??
                        string.Empty);
        }

        private void LoadRuntimeBuildAndStandardLibrary()
        {
            JsonObject runtime = ParseObject("runtime.json");
            runtimeDescriptionTextBox.Text = TextValue(runtime, "description");
            runtimeEntryPointTextBox.Text = TextValue(runtime, "entryPoint");
            runtimeInstructionLimitTextBox.Text = runtime?["defaultInstructionLimit"]?.ToString() ?? "";
            runtimeOutputTargetTextBox.Text = TextValue(runtime, "outputTarget");
            vmOperationsGrid.Rows.Clear();
            foreach (JsonNode operation in runtime?["vmOperations"]?.AsArray() ?? new JsonArray())
                vmOperationsGrid.Rows.Add(operation?.GetValue<string>() ?? "");
            expressionOperationsGrid.Rows.Clear();
            JsonObject expressions = runtime?["expressionOperations"] as JsonObject;
            foreach (string category in new[] { "unary", "binary", "postfix" })
                foreach (JsonNode node in expressions?[category]?.AsArray() ?? new JsonArray())
                {
                    JsonObject binding = node as JsonObject;
                    int index = expressionOperationsGrid.Rows.Add(category,
                        TextValue(binding, "token"), TextValue(binding, "operation"));
                    expressionOperationsGrid.Rows[index].Tag = binding?.DeepClone();
                }

            JsonObject build = ParseObject("build.json");
            buildDescriptionTextBox.Text = TextValue(build, "description");
            buildDefaultBackendTextBox.Text = TextValue(build, "defaultBackend");
            buildArtifactFormatTextBox.Text = TextValue(build, "artifactFormat");
            buildBackendsGrid.Rows.Clear();
            foreach (JsonNode backend in build?["backends"]?.AsArray() ?? new JsonArray())
                buildBackendsGrid.Rows.Add(backend?.GetValue<string>() ?? "");

            JsonObject library = ParseObject("standard-library.json");
            standardLibraryDescriptionTextBox.Text = TextValue(library, "description");
            standardLibraryIdTextBox.Text = TextValue(library, "id");
            standardLibraryVersionTextBox.Text = TextValue(library, "version");
            standardFunctionsGrid.Rows.Clear();
            foreach (JsonNode node in library?["functions"]?.AsArray() ?? new JsonArray())
            {
                JsonObject function = node as JsonObject;
                int index = standardFunctionsGrid.Rows.Add(
                    TextValue(function, "name"), TextValue(function, "returnType"),
                    CompactJson(function?["parameterTypes"]), TextValue(function, "documentation"),
                    TextValue(function, "binding"), TextValue(function, "effect"),
                    CompactJson(function?["errorCodes"]));
                standardFunctionsGrid.Rows[index].Tag = function?.DeepClone();
            }
        }

        private static string CompactJson(JsonNode node) =>
            node?.ToJsonString() ?? "[]";

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

        private void PreviewGuidedKeywordChange(object sender, EventArgs e)
        {
            if (session == null || guidedKeywordComboBox.SelectedItem == null) return;
            if (!StageGuidedKeywordChange()) return;
            if (guidedKeywordChanges.Count == 0)
            {
                ShowPreviewFailure("Aggiungere almeno una parola modificata al profilo.");
                return;
            }
            DdfLanguagePackageLoadResult current =
                DdfLanguagePackageAuthoring.Validate(session.Project);
            if (!current.Success)
            {
                ShowPreviewFailure("Convalidare lo snapshot corrente prima di cambiare " +
                    "la sintassi.\r\n" + FormatDiagnostics(current.Diagnostics));
                return;
            }
            DdfLanguageDefinition language = current.Package.Language;
            foreach (KeyValuePair<string, string> change in guidedKeywordChanges)
            {
                if (!language.TryGetKeyword(change.Key, out _))
                {
                    ShowPreviewFailure("La parola chiave '" + change.Key +
                        "' non e piu presente nel pacchetto confermato.");
                    return;
                }
                if (!language.LexicalRules.IsValidIdentifier(change.Value))
                {
                    ShowPreviewFailure("'" + change.Value +
                        "' non e un identificatore valido per il pacchetto corrente.");
                    return;
                }
                if (language.Keywords.Any(item => item.Text == change.Value) &&
                    !guidedKeywordChanges.ContainsKey(change.Value))
                {
                    ShowPreviewFailure("La parola chiave '" + change.Value +
                        "' esiste gia e non viene rinominata nel profilo.");
                    return;
                }
            }
            string duplicate = guidedKeywordChanges.Values.GroupBy(value => value,
                StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate != null)
            {
                ShowPreviewFailure("Due parole chiave del profilo usano la stessa " +
                    "scrittura finale: '" + duplicate + "'.");
                return;
            }
            PreviewGuidedChanges(sender, e);
            if (pendingPreview == null || !pendingPreview.Success) return;
            if (HasSemanticChanges(session.Project, pendingPreview.ResultingProject))
            {
                diagnosticsTextBox.Text = "Ci sono altre modifiche non confermate. " +
                    "Controllare e confermare questa anteprima prima di avviare il " +
                    "cambio di sintassi, poi ripetere l'operazione.\r\n\r\n" +
                    diagnosticsTextBox.Text;
                return;
            }
            try
            {
                JsonObject lexicon = ParseObject("lexicon.json");
                foreach (JsonNode node in lexicon["keywords"].AsArray())
                    if (node is JsonObject item && guidedKeywordChanges.TryGetValue(
                        TextValue(item, "text"), out string changedText))
                        item["text"] = changedText;
                var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lexicon.json"] = Serialize(lexicon)
                };
                if (session.Project.TryGetDocument("grammar.ebnf",
                    out DdfLanguageAuthoringDocument ebnf))
                    changes["grammar.ebnf"] = ReplaceEbnfTerminals(ebnf.Content,
                        guidedKeywordChanges);
                JsonObject grammar = ParseObject("grammar.json");
                foreach (JsonNode node in grammar["rules"]?.AsArray() ?? new JsonArray())
                    if (node is JsonObject rule && rule["ide"] is JsonObject ide)
                    {
                        string oldLabel = TextValue(ide, "breadcrumbLabel");
                        string newLabel = ReplaceDisplayWords(oldLabel,
                            guidedKeywordChanges);
                        if (newLabel == oldLabel) continue;
                        ide["breadcrumbLabel"] = newLabel;
                        string firstWord = new string(oldLabel.TakeWhile(
                            IsWordCharacter).ToArray());
                        if (ide["breadcrumbSelectionLength"] != null &&
                            guidedKeywordChanges.TryGetValue(firstWord,
                                out string changedFirstWord))
                            ide["breadcrumbSelectionLength"] =
                                changedFirstWord.Length;
                    }
                changes["grammar.json"] = Serialize(grammar);
                JsonObject tests = ParseObject("tests.json");
                foreach (JsonNode node in tests["cases"]?.AsArray() ?? new JsonArray())
                    if (node is JsonObject test)
                        test["source"] = ReplaceKeywordTokensBatch(TextValue(test, "source"),
                            guidedKeywordChanges, language);
                changes["tests.json"] = Serialize(tests);
                JsonObject intellisense = ParseObject("intellisense.json");
                foreach (JsonNode node in intellisense["snippets"]?.AsArray() ??
                    new JsonArray())
                    if (node is JsonObject snippet)
                    {
                        if (guidedKeywordChanges.TryGetValue(TextValue(snippet,
                            "prefix"), out string changedPrefix))
                            snippet["prefix"] = changedPrefix;
                        string display = TextValue(snippet, "displayText");
                        snippet["displayText"] = ReplaceDisplayWords(display,
                            guidedKeywordChanges);
                        snippet["body"] = ReplaceKeywordTokensBatch(TextValue(snippet,
                            "body"), guidedKeywordChanges, language);
                    }
                changes["intellisense.json"] = Serialize(intellisense);
                PreviewChanges(changes.Select(item =>
                    DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                    "Cambio coordinato di " + guidedKeywordChanges.Count +
                    " parole chiave");
                if (pendingPreview?.ResultingProject == null) return;
                DdfLanguagePackageLoadResult checkedPackage =
                    DdfLanguagePackageAuthoring.Validate(pendingPreview.ResultingProject);
                if (!checkedPackage.Success)
                {
                    confirmPreviewButton.Enabled = false;
                    diagnosticsTextBox.Text = "Il profilo risultante non supera la " +
                        "convalida. Nessuna modifica e stata applicata.\r\n\r\n" +
                        FormatDiagnostics(checkedPackage.Diagnostics);
                }
                else
                    diagnosticsTextBox.Text = "Lessico, grammatica, test e snippet " +
                        "aggiornati nello stesso diff. Pacchetto convalidato; controllare " +
                        "l'anteprima prima di confermare.";
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is InvalidDataException || exception is InvalidOperationException)
            {
                ShowPreviewFailure(exception.Message);
            }
        }

        private void StageGuidedKeywordChange(object sender, EventArgs e) =>
            StageGuidedKeywordChange();

        private bool StageGuidedKeywordChange()
        {
            string original = guidedKeywordComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(original)) return false;
            string replacement = guidedKeywordTextBox.Text.Trim();
            if (replacement.Length == 0)
            {
                guidedKeywordImpactTextBox.Text = "Specificare la nuova scrittura " +
                    "prima di aggiungerla al profilo.";
                guidedKeywordTextBox.Focus();
                return false;
            }
            if (replacement == original)
                guidedKeywordChanges.Remove(original);
            else
                guidedKeywordChanges[original] = replacement;
            InvalidateGuidedProfilePreview();
            RefreshGuidedProfileList();
            return true;
        }

        private void RemoveGuidedKeywordChange(object sender, EventArgs e)
        {
            if (guidedProfileListView.SelectedItems.Count != 1) return;
            string original = guidedProfileListView.SelectedItems[0].Tag as string;
            if (original == null) return;
            guidedKeywordChanges.Remove(original);
            if (string.Equals(guidedKeywordComboBox.SelectedItem?.ToString(),
                original, StringComparison.Ordinal))
                guidedKeywordTextBox.Text = original;
            InvalidateGuidedProfilePreview();
            RefreshGuidedProfileList();
        }

        private void InvalidateGuidedProfilePreview()
        {
            if (pendingPreview?.ChangeSet?.Description?.StartsWith(
                "Cambio coordinato di ", StringComparison.Ordinal) != true) return;
            pendingPreview = null;
            confirmPreviewButton.Enabled = false;
            previewTextBox.Text = "Il profilo e stato modificato. Generare una nuova anteprima.";
        }

        private void RefreshGuidedProfileList()
        {
            guidedProfileListView.BeginUpdate();
            guidedProfileListView.Items.Clear();
            foreach (KeyValuePair<string, string> change in guidedKeywordChanges
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var item = new ListViewItem(change.Key) { Tag = change.Key };
                item.SubItems.Add(change.Value);
                guidedProfileListView.Items.Add(item);
            }
            guidedProfileListView.EndUpdate();
            UpdateGuidedKeywordImpact();
        }

        private static bool HasSemanticChanges(DdfLanguageAuthoringProject before,
            DdfLanguageAuthoringProject after)
        {
            foreach (KeyValuePair<string, DdfLanguageAuthoringDocument> entry in
                before.Documents)
            {
                if (!after.TryGetDocument(entry.Key, out DdfLanguageAuthoringDocument next))
                    return true;
                if (entry.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    if (!JsonNode.DeepEquals(JsonNode.Parse(entry.Value.Content),
                        JsonNode.Parse(next.Content))) return true;
                }
                else if (!string.Equals(entry.Value.Content, next.Content,
                    StringComparison.Ordinal)) return true;
            }
            return after.Documents.Count != before.Documents.Count;
        }

        private static string ReplaceKeywordTokens(string source, string original,
            string replacement, DdfTokenKind kind, DdfLanguageDefinition language)
        {
            if (!language.TryGetKeyword(original, out DdfKeywordDefinition keyword) ||
                keyword.TokenKind != kind) return source;
            return ReplaceKeywordTokensBatch(source,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [original] = replacement
                }, language);
        }

        private static string ReplaceKeywordTokensBatch(string source,
            IReadOnlyDictionary<string, string> changes, DdfLanguageDefinition language)
        {
            var result = new StringBuilder(source.Length);
            int position = 0;
            foreach (DdfToken token in DdfLexer.Lex(source, language).Tokens)
            {
                string spelling = source.Substring(token.Start, token.Length);
                if (!changes.TryGetValue(spelling, out string replacement) ||
                    !language.TryGetKeyword(spelling, out DdfKeywordDefinition keyword) ||
                    token.Kind != keyword.TokenKind) continue;
                result.Append(source, position, token.Start - position);
                result.Append(replacement);
                position = token.Start + token.Length;
            }
            result.Append(source, position, source.Length - position);
            return result.ToString();
        }

        private static string ReplaceDisplayWord(string source, string original,
            string replacement)
        {
            return ReplaceDisplayWords(source,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [original] = replacement
                });
        }

        private static string ReplaceDisplayWords(string source,
            IReadOnlyDictionary<string, string> changes)
        {
            var result = new StringBuilder(source.Length);
            for (int index = 0; index < source.Length;)
            {
                if (!IsWordCharacter(source[index]))
                {
                    result.Append(source[index++]);
                    continue;
                }
                int start = index;
                while (index < source.Length && IsWordCharacter(source[index]))
                    index++;
                string word = source.Substring(start, index - start);
                result.Append(changes.TryGetValue(word, out string replacement)
                    ? replacement : word);
            }
            return result.ToString();
        }

        private static string ReplaceEbnfTerminal(string source, string original,
            string replacement)
        {
            return ReplaceEbnfTerminals(source,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [original] = replacement
                });
        }

        private static string ReplaceEbnfTerminals(string source,
            IReadOnlyDictionary<string, string> changes)
        {
            var result = new StringBuilder(source.Length);
            bool comment = false;
            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                if (current == '\r' || current == '\n') comment = false;
                if (current == '#' && !comment) comment = true;
                if (!comment && current == '"')
                {
                    int end = source.IndexOf('"', index + 1);
                    if (end > index)
                    {
                        string literal = source.Substring(index + 1, end - index - 1);
                        result.Append('"').Append(changes.TryGetValue(literal,
                            out string replacement) ? replacement : literal).Append('"');
                        index = end;
                        continue;
                    }
                }
                result.Append(current);
            }
            return result.ToString();
        }

        private void PreviewGuidedChanges(object sender, EventArgs e)
        {
            if (session == null) return;
            try
            {
                foreach (DataGridView grid in new[] { keywordsGrid, operatorsGrid,
                    primitiveTypesGrid, conversionsGrid, semanticBindingsGrid,
                    formattingTokensGrid, autoPairsGrid, indentAfterGrid,
                    quickFixesGrid, completionLabelsGrid, signatureLabelsGrid,
                    symbolKindsGrid, snippetsGrid, flowchartLabelsGrid,
                    flowchartExplanationsGrid, diagnosticMessagesGrid,
                    vmOperationsGrid, expressionOperationsGrid, buildBackendsGrid,
                    standardFunctionsGrid })
                    grid.EndEdit();
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

                JsonObject types = ParseObject("types.json");
                types["description"] = typesDescriptionTextBox.Text.Trim();
                var primitives = new JsonArray();
                foreach (DataGridViewRow row in primitiveTypesGrid.Rows.Cast<DataGridViewRow>()
                    .Where(item => !item.IsNewRow))
                {
                    string name = Cell(row, 0);
                    if (name.Length == 0) continue;
                    var primitive = new JsonObject
                    {
                        ["name"] = name,
                        ["category"] = Cell(row, 1),
                        ["canDeclareVariable"] = BooleanCell(row, 2)
                    };
                    string indexedElementType = Cell(row, 3);
                    if (indexedElementType.Length != 0)
                        primitive["indexedElementType"] = indexedElementType;
                    primitives.Add(primitive);
                }
                types["primitives"] = primitives;

                var conversions = new JsonArray();
                foreach (DataGridViewRow row in conversionsGrid.Rows.Cast<DataGridViewRow>()
                    .Where(item => !item.IsNewRow))
                {
                    string from = Cell(row, 0);
                    string to = Cell(row, 1);
                    if (from.Length == 0 && to.Length == 0) continue;
                    conversions.Add(new JsonObject
                    {
                        ["from"] = from,
                        ["to"] = to,
                        ["context"] = Cell(row, 2)
                    });
                }
                types["conversions"] = conversions;

                JsonObject semantics = ParseObject("semantics.json");
                semantics["description"] = semanticsDescriptionTextBox.Text.Trim();
                var bindingOrder = new List<string>();
                var operationsByRule = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
                foreach (DataGridViewRow row in semanticBindingsGrid.Rows.Cast<DataGridViewRow>()
                    .Where(item => !item.IsNewRow))
                {
                    string rule = Cell(row, 0);
                    string operationId = Cell(row, 1);
                    if (rule.Length == 0 && operationId.Length == 0) continue;
                    if (!operationsByRule.TryGetValue(rule, out JsonArray ruleOperations))
                    {
                        ruleOperations = new JsonArray();
                        operationsByRule.Add(rule, ruleOperations);
                        bindingOrder.Add(rule);
                    }
                    ruleOperations.Add(new JsonObject
                    {
                        ["id"] = operationId,
                        ["arguments"] = ParseArguments(Cell(row, 2))
                    });
                }
                var bindings = new JsonArray();
                foreach (string rule in bindingOrder)
                {
                    bindings.Add(new JsonObject
                    {
                        ["rule"] = rule,
                        ["operations"] = operationsByRule[rule]
                    });
                }
                semantics["bindings"] = bindings;

                JsonObject formatting = ParseObject("formatting.json");
                formatting["description"] = formattingDescriptionTextBox.Text;
                UpdateProperties(formatting["tokens"] as JsonObject,
                    formattingTokensGrid);
                formatting["blankLineBetweenStatements"] =
                    blankLineBetweenStatementsCheckBox.Checked;
                var autoPairs = new JsonArray();
                foreach (DataGridViewRow row in DataRows(autoPairsGrid))
                {
                    string open = Cell(row, 0);
                    string close = Cell(row, 1);
                    if (open.Length == 0 && close.Length == 0) continue;
                    autoPairs.Add(new JsonObject
                    {
                        ["open"] = open,
                        ["close"] = close
                    });
                }
                formatting["autoPairs"] = autoPairs;
                var indentAfter = new JsonArray();
                foreach (DataGridViewRow row in DataRows(indentAfterGrid))
                {
                    string token = RawCell(row, 0);
                    if (token.Length != 0) indentAfter.Add(token);
                }
                formatting["indentAfter"] = indentAfter;
                var quickFixes = new JsonArray();
                foreach (DataGridViewRow row in DataRows(quickFixesGrid))
                {
                    string code = Cell(row, 0);
                    string title = RawCell(row, 1);
                    string replacement = RawCell(row, 2);
                    if (code.Length == 0 && title.Length == 0 &&
                        replacement.Length == 0) continue;
                    quickFixes.Add(new JsonObject
                    {
                        ["diagnosticCode"] = code,
                        ["title"] = title,
                        ["replacement"] = replacement
                    });
                }
                formatting["quickFixes"] = quickFixes;

                JsonObject intellisense = ParseObject("intellisense.json");
                intellisense["description"] = intellisenseDescriptionTextBox.Text;
                UpdateProperties(intellisense["completion"] as JsonObject,
                    completionLabelsGrid);
                UpdateProperties(intellisense["signatureHelp"] as JsonObject,
                    signatureLabelsGrid);
                var symbolKinds = new JsonArray();
                foreach (DataGridViewRow row in DataRows(symbolKindsGrid))
                {
                    string kind = Cell(row, 0);
                    if (kind.Length == 0) continue;
                    symbolKinds.Add(new JsonObject
                    {
                        ["kind"] = kind,
                        ["label"] = RawCell(row, 1),
                        ["documentation"] = RawCell(row, 2)
                    });
                }
                intellisense["symbolKinds"] = symbolKinds;
                var snippets = new JsonArray();
                foreach (DataGridViewRow row in DataRows(snippetsGrid))
                {
                    string prefix = Cell(row, 0);
                    if (prefix.Length == 0) continue;
                    var snippet = new JsonObject
                    {
                        ["prefix"] = prefix,
                        ["displayText"] = RawCell(row, 1),
                        ["description"] = RawCell(row, 2),
                        ["body"] = RawCell(row, 3)
                    };
                    if (BooleanCell(row, 4)) snippet["requiresKeyword"] = true;
                    snippets.Add(snippet);
                }
                intellisense["snippets"] = snippets;
                JsonObject flowchart = intellisense["flowchart"] as JsonObject;
                if (flowchart != null)
                {
                    UpdateProperties(flowchart, flowchartLabelsGrid);
                    flowchart["explanations"] = PropertiesObject(
                        flowchartExplanationsGrid);
                }
                intellisense["diagnostics"] = PropertiesObject(
                    diagnosticMessagesGrid);

                JsonObject runtime = ParseObject("runtime.json");
                runtime["description"] = runtimeDescriptionTextBox.Text;
                runtime["entryPoint"] = runtimeEntryPointTextBox.Text.Trim();
                if (!int.TryParse(runtimeInstructionLimitTextBox.Text.Trim(),
                        out int instructionLimit) || instructionLimit < 1)
                    throw new FormatException("Il limite istruzioni runtime deve essere un intero positivo.");
                runtime["defaultInstructionLimit"] = instructionLimit;
                runtime["outputTarget"] = runtimeOutputTargetTextBox.Text.Trim();
                var vmOperations = new JsonArray();
                foreach (DataGridViewRow row in DataRows(vmOperationsGrid))
                {
                    string operation = Cell(row, 0);
                    if (operation.Length != 0) vmOperations.Add(operation);
                }
                runtime["vmOperations"] = vmOperations;
                JsonObject expressions = runtime["expressionOperations"] as JsonObject ??
                    throw new InvalidDataException("Sezione expressionOperations mancante.");
                var expressionGroups = new Dictionary<string, JsonArray>
                {
                    ["unary"] = new JsonArray(),
                    ["binary"] = new JsonArray(),
                    ["postfix"] = new JsonArray()
                };
                foreach (DataGridViewRow row in DataRows(expressionOperationsGrid))
                {
                    string category = Cell(row, 0);
                    string token = Cell(row, 1);
                    string operation = Cell(row, 2);
                    if (category.Length == 0 && token.Length == 0 && operation.Length == 0)
                        continue;
                    if (!expressionGroups.TryGetValue(category, out JsonArray group))
                        throw new FormatException("Categoria espressione non prevista: " + category);
                    JsonObject binding = (row.Tag as JsonObject)?.DeepClone() as JsonObject ??
                        new JsonObject();
                    binding["token"] = token;
                    binding["operation"] = operation;
                    group.Add(binding);
                }
                foreach (KeyValuePair<string, JsonArray> group in expressionGroups)
                    expressions[group.Key] = group.Value;

                JsonObject build = ParseObject("build.json");
                build["description"] = buildDescriptionTextBox.Text;
                build["defaultBackend"] = buildDefaultBackendTextBox.Text.Trim();
                build["artifactFormat"] = buildArtifactFormatTextBox.Text.Trim();
                var backends = new JsonArray();
                foreach (DataGridViewRow row in DataRows(buildBackendsGrid))
                {
                    string backend = Cell(row, 0);
                    if (backend.Length != 0) backends.Add(backend);
                }
                build["backends"] = backends;

                JsonObject library = ParseObject("standard-library.json");
                library["description"] = standardLibraryDescriptionTextBox.Text;
                library["id"] = standardLibraryIdTextBox.Text.Trim();
                library["version"] = standardLibraryVersionTextBox.Text.Trim();
                var functions = new JsonArray();
                foreach (DataGridViewRow row in DataRows(standardFunctionsGrid))
                {
                    string name = Cell(row, 0);
                    if (name.Length == 0)
                    {
                        if (Enumerable.Range(1, 6).Any(index =>
                                RawCell(row, index).Length != 0))
                            throw new FormatException("Una funzione standard non ha un nome.");
                        continue;
                    }
                    JsonObject function = (row.Tag as JsonObject)?.DeepClone() as JsonObject ??
                        new JsonObject();
                    function["name"] = name;
                    function["returnType"] = Cell(row, 1);
                    function["parameterTypes"] = ParseStringArray(RawCell(row, 2),
                        "Parametri di " + name);
                    function["documentation"] = RawCell(row, 3);
                    function["binding"] = Cell(row, 4);
                    function["effect"] = Cell(row, 5);
                    function["errorCodes"] = ParseStringArray(RawCell(row, 6),
                        "Errori di " + name);
                    functions.Add(function);
                }
                library["functions"] = functions;

                var combined = new Dictionary<string, string>(sourceDrafts,
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["manifest.json"] = Serialize(manifest),
                    ["lexicon.json"] = Serialize(lexicon),
                    ["types.json"] = Serialize(types),
                    ["semantics.json"] = Serialize(semantics),
                    ["formatting.json"] = Serialize(formatting),
                    ["intellisense.json"] = Serialize(intellisense),
                    ["runtime.json"] = Serialize(runtime),
                    ["build.json"] = Serialize(build),
                    ["standard-library.json"] = Serialize(library)
                };
                PreviewChanges(combined.OrderBy(item => item.Key,
                    StringComparer.OrdinalIgnoreCase).Select(item =>
                        DdfLanguageAuthoringChange.Upsert(item.Key, item.Value)),
                    "Aggiorna sezioni guidate del pacchetto e draft sorgente");
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

        private void CompareVersion(object sender, EventArgs e)
        {
            if (session == null) return;
            using var dialog = new FolderBrowserDialog
            {
                Description = "Selezionare la cartella della versione precedente del medesimo pacchetto.",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            DdfLanguageAuthoringResult oldProject =
                DdfLanguagePackageAuthoring.LoadDirectory(dialog.SelectedPath);
            if (!oldProject.Success)
            {
                ShowPreviewFailure(FormatDiagnostics(oldProject.Diagnostics));
                return;
            }
            try
            {
                DdfLanguagePackageCompatibilityReport report =
                    DdfLanguagePackageCompatibility.Compare(oldProject.Project,
                        session.Project);
                var text = new StringBuilder();
                text.AppendLine("Confronto pacchetto: " + report.PreviousVersion +
                    " → " + report.CurrentVersion);
                text.AppendLine("La cartella precedente e lo snapshot corrente non vengono modificati.");
                text.AppendLine();
                foreach (DdfLanguageCompatibilityFinding finding in report.Findings)
                    text.AppendLine("[" + finding.Level + "] " + finding.Section +
                        ": " + finding.Message);
                if (report.Findings.Count == 0)
                    text.AppendLine("Nessuna differenza contrattuale rilevata.");
                pendingPreview = null;
                confirmPreviewButton.Enabled = false;
                previewTextBox.Text = text.ToString();
                diagnosticsTextBox.Text = report.HasBreakingChanges
                    ? "Sono presenti modifiche incompatibili."
                    : report.RequiresReview
                        ? "Nessuna rottura certa; alcune modifiche richiedono revisione."
                        : "Nessuna modifica incompatibile rilevata dalle regole automatiche.";
                editorTabs.SelectedTab = previewPage;
            }
            catch (Exception exception) when (exception is InvalidOperationException ||
                exception is JsonException)
            {
                ShowPreviewFailure(exception.Message);
            }
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

        private void PrepareGuidedDistribution(object sender, EventArgs e)
        {
            if (session == null) return;
            if (sourceDrafts.Any(item =>
                !session.Project.TryGetDocument(item.Key,
                    out DdfLanguageAuthoringDocument document) ||
                !string.Equals(item.Value, document.Content,
                    StringComparison.Ordinal)))
            {
                ShowPreviewFailure("Ci sono sorgenti JSON/EBNF modificati ma non confermati. " +
                    "Usare 'Anteprima sorgenti' o 'Anteprima EBNF', confermare " +
                    "e poi ripetere la distribuzione.");
                return;
            }
            PreviewGuidedChanges(sender, e);
            if (pendingPreview == null) return;
            if (!pendingPreview.Success)
            {
                diagnosticsTextBox.Text = "Il pacchetto non supera la convalida. " +
                    "Correggere le diagnostiche prima di distribuire." +
                    Environment.NewLine + Environment.NewLine + diagnosticsTextBox.Text;
                return;
            }
            if (HasSemanticChanges(session.Project, pendingPreview.ResultingProject))
            {
                diagnosticsTextBox.Text = "Prima di generare l'archivio, controllare " +
                    "l'anteprima, correggere eventuali errori e premere 'Conferma e applica " +
                    "anteprima'. Poi scegliere di nuovo 'Genera .ddflang...'." +
                    Environment.NewLine + Environment.NewLine + diagnosticsTextBox.Text;
                return;
            }
            GenerateArchive(sender, e);
        }

        private void GenerateArchive(object sender, EventArgs e)
        {
            if (session == null) return;
            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "ddflang",
                FileName = packageIdTextBox.Text.Trim() + "-" +
                    packageVersionTextBox.Text.Trim() + ".ddflang",
                Filter = "Pacchetto linguaggio DDF (*.ddflang)|*.ddflang",
                OverwritePrompt = false,
                Title = "Genera archivio linguaggio"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            DdfLanguagePackageArchiveResult result =
                DdfLanguagePackageArchiveBuilder.Generate(session.Project,
                    dialog.FileName);
            pendingPreview = null;
            confirmPreviewButton.Enabled = false;
            previewTextBox.Text = result.Success
                ? "Archivio: " + result.OutputPath + Environment.NewLine +
                  "SHA-256: " + result.Sha256 + Environment.NewLine +
                  "Metadati: " + DdfLanguagePackageArchiveBuilder.ChecksumsFile +
                  ", " + DdfLanguagePackageArchiveBuilder.ValidationFile
                : string.Empty;
            diagnosticsTextBox.Text = result.Success
                ? "Archivio riproducibile convalidato. Nessun pacchetto installato."
                : FormatDiagnostics(result.Diagnostics);
            editorTabs.SelectedTab = previewPage;
        }

        private static string Cell(DataGridViewRow row, int index) =>
            Convert.ToString(row.Cells[index].Value)?.Trim() ?? string.Empty;

        private static string RawCell(DataGridViewRow row, int index) =>
            Convert.ToString(row.Cells[index].Value) ?? string.Empty;

        private static JsonArray ParseStringArray(string json, string label)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JsonArray();
            JsonArray values = JsonNode.Parse(json) as JsonArray ??
                throw new FormatException(label + ": è richiesto un array JSON.");
            foreach (JsonNode value in values)
                if (value is not JsonValue item || !item.TryGetValue<string>(out _))
                    throw new FormatException(label + ": sono ammesse solo stringhe.");
            return values;
        }

        private static IEnumerable<DataGridViewRow> DataRows(DataGridView grid) =>
            grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow);

        private static void UpdateProperties(JsonObject target, DataGridView grid)
        {
            if (target == null) throw new InvalidDataException(
                "Sezione mancante per " + grid.Name + ".");
            foreach (DataGridViewRow row in DataRows(grid))
            {
                string key = Cell(row, 0);
                if (key.Length == 0) continue;
                if (!target.ContainsKey(key)) throw new FormatException(
                    "Proprietà non prevista: " + key + ".");
                target[key] = RawCell(row, 1);
            }
        }

        private static JsonObject PropertiesObject(DataGridView grid)
        {
            var result = new JsonObject();
            foreach (DataGridViewRow row in DataRows(grid))
            {
                string key = Cell(row, 0);
                string value = RawCell(row, 1);
                if (key.Length == 0 && value.Length == 0) continue;
                if (key.Length == 0 || result.ContainsKey(key))
                    throw new FormatException("Chiave mancante o duplicata in " +
                        grid.Name + ".");
                result[key] = value;
            }
            return result;
        }

        private static bool BooleanCell(DataGridViewRow row, int index)
        {
            object value = row.Cells[index].Value;
            if (value is bool boolean) return boolean;
            if (bool.TryParse(Convert.ToString(value), out boolean)) return boolean;
            throw new FormatException("Valore booleano non valido nella colonna " +
                row.DataGridView.Columns[index].HeaderText + ".");
        }

        private static JsonObject ParseArguments(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
            return JsonNode.Parse(text, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }) as JsonObject ?? throw new FormatException(
                "Gli argomenti semantici devono essere un oggetto JSON.");
        }

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
