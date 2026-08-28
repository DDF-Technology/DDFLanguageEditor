namespace DDF___Program_Language_Editor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStripMain = new System.Windows.Forms.MenuStrip();
            this.fileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.completionMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.formatDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleFoldMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandAllFoldsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelEditor = new System.Windows.Forms.Panel();
            this.richTextBoxMainEditor = new System.Windows.Forms.RichTextBox();
            this.richTextBoxFoldedView = new System.Windows.Forms.RichTextBox();
            this.richTextBoxLineNumbers = new DDF___Program_Language_Editor.LineNumberGutter();
            this.splitterOutline = new System.Windows.Forms.Splitter();
            this.panelOutline = new System.Windows.Forms.Panel();
            this.buttonOutlinePin = new System.Windows.Forms.Button();
            this.treeViewOutline = new System.Windows.Forms.TreeView();
            this.labelOutline = new System.Windows.Forms.Label();
            this.panelDiagnostics = new System.Windows.Forms.Panel();
            this.buttonDiagnosticsPin = new System.Windows.Forms.Button();
            this.labelDiagnostics = new System.Windows.Forms.Label();
            this.listBoxDiagnostics = new System.Windows.Forms.ListBox();
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.statusFileLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusPositionLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusEncodingLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStripMain.SuspendLayout();
            this.panelEditor.SuspendLayout();
            this.panelOutline.SuspendLayout();
            this.panelDiagnostics.SuspendLayout();
            this.statusStripMain.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStripMain
            //
            this.menuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenuItem,
            this.editMenuItem,
            this.viewMenuItem,
            this.helpMenuItem});
            this.menuStripMain.Location = new System.Drawing.Point(0, 0);
            this.menuStripMain.Name = "menuStripMain";
            this.menuStripMain.Size = new System.Drawing.Size(1027, 24);
            this.menuStripMain.TabIndex = 0;
            //
            // fileMenuItem
            //
            this.fileMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newMenuItem,
            this.openMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.saveMenuItem,
            this.saveAsMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.recentMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.exitMenuItem});
            this.fileMenuItem.Name = "fileMenuItem";
            this.fileMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileMenuItem.Text = "&File";
            //
            // newMenuItem
            //
            this.newMenuItem.Name = "newMenuItem";
            this.newMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.newMenuItem.Text = "&Nuovo";
            this.newMenuItem.Click += new System.EventHandler(this.newMenuItem_Click);
            //
            // openMenuItem
            //
            this.openMenuItem.Name = "openMenuItem";
            this.openMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openMenuItem.Text = "&Apri...";
            this.openMenuItem.Click += new System.EventHandler(this.openMenuItem_Click);
            //
            // saveMenuItem
            //
            this.saveMenuItem.Name = "saveMenuItem";
            this.saveMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveMenuItem.Text = "&Salva";
            this.saveMenuItem.Click += new System.EventHandler(this.saveMenuItem_Click);
            //
            // saveAsMenuItem
            //
            this.saveAsMenuItem.Name = "saveAsMenuItem";
            this.saveAsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.S)));
            this.saveAsMenuItem.Text = "Salva con nome...";
            this.saveAsMenuItem.Click += new System.EventHandler(this.saveAsMenuItem_Click);
            //
            // recentMenuItem
            //
            this.recentMenuItem.Name = "recentMenuItem";
            this.recentMenuItem.Text = "File &recenti";
            //
            // exitMenuItem
            //
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Text = "&Esci";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            //
            // editMenuItem
            //
            this.editMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoMenuItem,
            this.redoMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.cutMenuItem,
            this.copyMenuItem,
            this.pasteMenuItem,
            this.selectAllMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.findMenuItem,
            this.replaceMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.completionMenuItem,
            this.formatDocumentMenuItem});
            this.editMenuItem.Name = "editMenuItem";
            this.editMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editMenuItem.Text = "&Modifica";
            this.editMenuItem.DropDownOpening += new System.EventHandler(this.editMenuItem_DropDownOpening);
            //
            // undoMenuItem
            //
            this.undoMenuItem.Name = "undoMenuItem";
            this.undoMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.undoMenuItem.Text = "&Annulla";
            this.undoMenuItem.Click += new System.EventHandler(this.undoMenuItem_Click);
            //
            // redoMenuItem
            //
            this.redoMenuItem.Name = "redoMenuItem";
            this.redoMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.redoMenuItem.Text = "&Ripristina";
            this.redoMenuItem.Click += new System.EventHandler(this.redoMenuItem_Click);
            //
            // cutMenuItem
            //
            this.cutMenuItem.Name = "cutMenuItem";
            this.cutMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutMenuItem.Text = "&Taglia";
            this.cutMenuItem.Click += new System.EventHandler(this.cutMenuItem_Click);
            //
            // copyMenuItem
            //
            this.copyMenuItem.Name = "copyMenuItem";
            this.copyMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyMenuItem.Text = "&Copia";
            this.copyMenuItem.Click += new System.EventHandler(this.copyMenuItem_Click);
            //
            // pasteMenuItem
            //
            this.pasteMenuItem.Name = "pasteMenuItem";
            this.pasteMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteMenuItem.Text = "&Incolla";
            this.pasteMenuItem.Click += new System.EventHandler(this.pasteMenuItem_Click);
            //
            // selectAllMenuItem
            //
            this.selectAllMenuItem.Name = "selectAllMenuItem";
            this.selectAllMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this.selectAllMenuItem.Text = "Seleziona &tutto";
            this.selectAllMenuItem.Click += new System.EventHandler(this.selectAllMenuItem_Click);
            //
            // findMenuItem
            //
            this.findMenuItem.Name = "findMenuItem";
            this.findMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.findMenuItem.Text = "&Trova...";
            this.findMenuItem.Click += new System.EventHandler(this.findMenuItem_Click);
            //
            // replaceMenuItem
            //
            this.replaceMenuItem.Name = "replaceMenuItem";
            this.replaceMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.H)));
            this.replaceMenuItem.Text = "&Sostituisci...";
            this.replaceMenuItem.Click += new System.EventHandler(this.replaceMenuItem_Click);
            //
            // completionMenuItem
            //
            this.completionMenuItem.Name = "completionMenuItem";
            this.completionMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Space)));
            this.completionMenuItem.Text = "&Completamento";
            this.completionMenuItem.Click += new System.EventHandler(this.completionMenuItem_Click);
            //
            // formatDocumentMenuItem
            //
            this.formatDocumentMenuItem.Name = "formatDocumentMenuItem";
            this.formatDocumentMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.F)));
            this.formatDocumentMenuItem.Text = "&Formatta documento";
            this.formatDocumentMenuItem.Click += new System.EventHandler(this.formatDocumentMenuItem_Click);
            //
            // viewMenuItem
            //
            this.viewMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toggleFoldMenuItem,
            this.expandAllFoldsMenuItem});
            this.viewMenuItem.Name = "viewMenuItem";
            this.viewMenuItem.Size = new System.Drawing.Size(69, 20);
            this.viewMenuItem.Text = "&Visualizza";
            this.viewMenuItem.DropDownOpening += new System.EventHandler(this.viewMenuItem_DropDownOpening);
            //
            // toggleFoldMenuItem
            //
            this.toggleFoldMenuItem.Name = "toggleFoldMenuItem";
            this.toggleFoldMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.M)));
            this.toggleFoldMenuItem.Text = "Comprimi blocco";
            this.toggleFoldMenuItem.Click += new System.EventHandler(this.toggleFoldMenuItem_Click);
            //
            // expandAllFoldsMenuItem
            //
            this.expandAllFoldsMenuItem.Name = "expandAllFoldsMenuItem";
            this.expandAllFoldsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.M)));
            this.expandAllFoldsMenuItem.Text = "Espandi tutto";
            this.expandAllFoldsMenuItem.Click += new System.EventHandler(this.expandAllFoldsMenuItem_Click);
            //
            // helpMenuItem
            //
            this.helpMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutMenuItem});
            this.helpMenuItem.Name = "helpMenuItem";
            this.helpMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpMenuItem.Text = "&Help";
            //
            // aboutMenuItem
            //
            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            //
            // panelEditor
            //
            this.panelEditor.Controls.Add(this.richTextBoxFoldedView);
            this.panelEditor.Controls.Add(this.richTextBoxMainEditor);
            this.panelEditor.Controls.Add(this.richTextBoxLineNumbers);
            this.panelEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelEditor.Name = "panelEditor";
            this.panelEditor.Padding = new System.Windows.Forms.Padding(3);
            this.panelEditor.Size = new System.Drawing.Size(1027, 711);
            this.panelEditor.TabIndex = 2;
            //
            // richTextBoxMainEditor
            //
            this.richTextBoxMainEditor.AcceptsTab = true;
            this.richTextBoxMainEditor.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.richTextBoxMainEditor.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxMainEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxMainEditor.Font = new System.Drawing.Font("Consolas", 10F);
            this.richTextBoxMainEditor.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.richTextBoxMainEditor.HideSelection = false;
            this.richTextBoxMainEditor.Name = "richTextBoxMainEditor";
            this.richTextBoxMainEditor.Size = new System.Drawing.Size(971, 705);
            this.richTextBoxMainEditor.TabIndex = 0;
            this.richTextBoxMainEditor.Text = "";
            this.richTextBoxMainEditor.WordWrap = false;
            this.richTextBoxMainEditor.VScroll += new System.EventHandler(this.richTextBoxMainEditor_VScroll);
            this.richTextBoxMainEditor.FontChanged += new System.EventHandler(this.richTextBoxMainEditor_FontChanged);
            this.richTextBoxMainEditor.SelectionChanged += new System.EventHandler(this.richTextBoxMainEditor_SelectionChanged);
            this.richTextBoxMainEditor.TextChanged += new System.EventHandler(this.richTextBox_TextChanged);
            this.richTextBoxMainEditor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.richTextBoxMainEditor_KeyDown);
            //
            // richTextBoxFoldedView
            //
            this.richTextBoxFoldedView.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.richTextBoxFoldedView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxFoldedView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxFoldedView.Font = new System.Drawing.Font("Consolas", 10F);
            this.richTextBoxFoldedView.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.richTextBoxFoldedView.Name = "richTextBoxFoldedView";
            this.richTextBoxFoldedView.ReadOnly = true;
            this.richTextBoxFoldedView.Size = new System.Drawing.Size(1021, 705);
            this.richTextBoxFoldedView.TabIndex = 2;
            this.richTextBoxFoldedView.Text = "";
            this.richTextBoxFoldedView.Visible = false;
            this.richTextBoxFoldedView.WordWrap = false;
            this.richTextBoxFoldedView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.richTextBoxFoldedView_KeyDown);
            //
            // richTextBoxLineNumbers
            //
            this.richTextBoxLineNumbers.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.richTextBoxLineNumbers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxLineNumbers.Dock = System.Windows.Forms.DockStyle.Left;
            this.richTextBoxLineNumbers.Font = new System.Drawing.Font("Consolas", 10F);
            this.richTextBoxLineNumbers.ForeColor = System.Drawing.Color.FromArgb(133, 133, 133);
            this.richTextBoxLineNumbers.Name = "richTextBoxLineNumbers";
            this.richTextBoxLineNumbers.ReadOnly = true;
            this.richTextBoxLineNumbers.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.richTextBoxLineNumbers.Size = new System.Drawing.Size(50, 705);
            this.richTextBoxLineNumbers.TabStop = false;
            this.richTextBoxLineNumbers.Text = "";
            this.richTextBoxLineNumbers.WordWrap = false;
            //
            // splitterOutline
            //
            this.splitterOutline.BackColor = System.Drawing.Color.FromArgb(62, 62, 64);
            this.splitterOutline.Dock = System.Windows.Forms.DockStyle.Right;
            this.splitterOutline.MinExtra = 480;
            this.splitterOutline.MinSize = 180;
            this.splitterOutline.Name = "splitterOutline";
            this.splitterOutline.Size = new System.Drawing.Size(4, 711);
            this.splitterOutline.TabStop = false;
            //
            // panelOutline
            //
            this.panelOutline.BackColor = System.Drawing.Color.FromArgb(37, 37, 38);
            this.panelOutline.Controls.Add(this.buttonOutlinePin);
            this.panelOutline.Controls.Add(this.treeViewOutline);
            this.panelOutline.Controls.Add(this.labelOutline);
            this.panelOutline.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelOutline.Name = "panelOutline";
            this.panelOutline.Size = new System.Drawing.Size(180, 711);
            this.panelOutline.TabIndex = 3;
            //
            // buttonOutlinePin
            //
            this.buttonOutlinePin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOutlinePin.FlatAppearance.BorderSize = 0;
            this.buttonOutlinePin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonOutlinePin.Font = new System.Drawing.Font("Segoe MDL2 Assets", 9F);
            this.buttonOutlinePin.ForeColor = System.Drawing.Color.Gainsboro;
            this.buttonOutlinePin.Location = new System.Drawing.Point(154, 1);
            this.buttonOutlinePin.Name = "buttonOutlinePin";
            this.buttonOutlinePin.Size = new System.Drawing.Size(24, 24);
            this.buttonOutlinePin.TabIndex = 2;
            this.buttonOutlinePin.Text = "";
            this.buttonOutlinePin.UseVisualStyleBackColor = true;
            //
            // treeViewOutline
            //
            this.treeViewOutline.BackColor = System.Drawing.Color.FromArgb(37, 37, 38);
            this.treeViewOutline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeViewOutline.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewOutline.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.treeViewOutline.ForeColor = System.Drawing.Color.Gainsboro;
            this.treeViewOutline.FullRowSelect = true;
            this.treeViewOutline.HideSelection = false;
            this.treeViewOutline.Name = "treeViewOutline";
            this.treeViewOutline.ShowLines = false;
            this.treeViewOutline.Size = new System.Drawing.Size(180, 685);
            this.treeViewOutline.TabIndex = 0;
            this.treeViewOutline.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewOutline_NodeMouseDoubleClick);
            //
            // labelOutline
            //
            this.labelOutline.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelOutline.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelOutline.ForeColor = System.Drawing.Color.Gainsboro;
            this.labelOutline.Name = "labelOutline";
            this.labelOutline.Padding = new System.Windows.Forms.Padding(8, 0, 30, 0);
            this.labelOutline.Size = new System.Drawing.Size(180, 26);
            this.labelOutline.Text = "OUTLINE — 0 simboli";
            this.labelOutline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // panelDiagnostics
            //
            this.panelDiagnostics.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.panelDiagnostics.Controls.Add(this.buttonDiagnosticsPin);
            this.panelDiagnostics.Controls.Add(this.listBoxDiagnostics);
            this.panelDiagnostics.Controls.Add(this.labelDiagnostics);
            this.panelDiagnostics.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelDiagnostics.Name = "panelDiagnostics";
            this.panelDiagnostics.Size = new System.Drawing.Size(1027, 116);
            this.panelDiagnostics.TabIndex = 3;
            //
            // buttonDiagnosticsPin
            //
            this.buttonDiagnosticsPin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDiagnosticsPin.FlatAppearance.BorderSize = 0;
            this.buttonDiagnosticsPin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonDiagnosticsPin.Font = new System.Drawing.Font("Segoe MDL2 Assets", 9F);
            this.buttonDiagnosticsPin.ForeColor = System.Drawing.Color.Gainsboro;
            this.buttonDiagnosticsPin.Location = new System.Drawing.Point(1001, 1);
            this.buttonDiagnosticsPin.Name = "buttonDiagnosticsPin";
            this.buttonDiagnosticsPin.Size = new System.Drawing.Size(24, 24);
            this.buttonDiagnosticsPin.TabIndex = 2;
            this.buttonDiagnosticsPin.Text = "";
            this.buttonDiagnosticsPin.UseVisualStyleBackColor = true;
            //
            // labelDiagnostics
            //
            this.labelDiagnostics.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelDiagnostics.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelDiagnostics.ForeColor = System.Drawing.Color.Gainsboro;
            this.labelDiagnostics.Name = "labelDiagnostics";
            this.labelDiagnostics.Padding = new System.Windows.Forms.Padding(8, 0, 30, 0);
            this.labelDiagnostics.Size = new System.Drawing.Size(1027, 26);
            this.labelDiagnostics.Text = "Diagnostica sorgente";
            this.labelDiagnostics.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // listBoxDiagnostics
            //
            this.listBoxDiagnostics.BackColor = System.Drawing.Color.FromArgb(37, 37, 38);
            this.listBoxDiagnostics.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listBoxDiagnostics.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxDiagnostics.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listBoxDiagnostics.ForeColor = System.Drawing.Color.LightCoral;
            this.listBoxDiagnostics.FormattingEnabled = true;
            this.listBoxDiagnostics.IntegralHeight = false;
            this.listBoxDiagnostics.Name = "listBoxDiagnostics";
            this.listBoxDiagnostics.Size = new System.Drawing.Size(1027, 90);
            this.listBoxDiagnostics.TabIndex = 0;
            this.listBoxDiagnostics.DoubleClick += new System.EventHandler(this.listBoxDiagnostics_DoubleClick);
            //
            // statusStripMain
            //
            this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusFileLabel,
            this.statusPositionLabel,
            this.statusEncodingLabel});
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(1027, 22);
            this.statusStripMain.TabIndex = 3;
            //
            // statusFileLabel
            //
            this.statusFileLabel.Name = "statusFileLabel";
            this.statusFileLabel.Spring = true;
            this.statusFileLabel.Text = "Senza titolo.ddf";
            this.statusFileLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // statusPositionLabel
            //
            this.statusPositionLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this.statusPositionLabel.Name = "statusPositionLabel";
            this.statusPositionLabel.Text = "Riga 1, Colonna 1";
            //
            // statusEncodingLabel
            //
            this.statusEncodingLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this.statusEncodingLabel.Name = "statusEncodingLabel";
            this.statusEncodingLabel.Text = "UTF-8";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1027, 793);
            this.Controls.Add(this.panelEditor);
            this.Controls.Add(this.splitterOutline);
            this.Controls.Add(this.panelOutline);
            this.Controls.Add(this.panelDiagnostics);
            this.Controls.Add(this.statusStripMain);
            this.Controls.Add(this.menuStripMain);
            this.MainMenuStrip = this.menuStripMain;
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DDFLanguageEditor 0.5.4 Beta — Senza titolo.ddf";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStripMain.ResumeLayout(false);
            this.menuStripMain.PerformLayout();
            this.panelEditor.ResumeLayout(false);
            this.panelOutline.ResumeLayout(false);
            this.panelDiagnostics.ResumeLayout(false);
            this.statusStripMain.ResumeLayout(false);
            this.statusStripMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStripMain;
        private System.Windows.Forms.ToolStripMenuItem fileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cutMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectAllMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replaceMenuItem;
        private System.Windows.Forms.ToolStripMenuItem completionMenuItem;
        private System.Windows.Forms.ToolStripMenuItem formatDocumentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleFoldMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandAllFoldsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutMenuItem;
        private System.Windows.Forms.Panel panelEditor;
        private System.Windows.Forms.RichTextBox richTextBoxMainEditor;
        private System.Windows.Forms.RichTextBox richTextBoxFoldedView;
        private DDF___Program_Language_Editor.LineNumberGutter richTextBoxLineNumbers;
        private System.Windows.Forms.Splitter splitterOutline;
        private System.Windows.Forms.Panel panelOutline;
        private System.Windows.Forms.Button buttonOutlinePin;
        private System.Windows.Forms.TreeView treeViewOutline;
        private System.Windows.Forms.Label labelOutline;
        private System.Windows.Forms.Panel panelDiagnostics;
        private System.Windows.Forms.Button buttonDiagnosticsPin;
        private System.Windows.Forms.Label labelDiagnostics;
        private System.Windows.Forms.ListBox listBoxDiagnostics;
        private System.Windows.Forms.StatusStrip statusStripMain;
        private System.Windows.Forms.ToolStripStatusLabel statusFileLabel;
        private System.Windows.Forms.ToolStripStatusLabel statusPositionLabel;
        private System.Windows.Forms.ToolStripStatusLabel statusEncodingLabel;
    }
}
