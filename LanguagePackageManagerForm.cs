using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class LanguagePackageManagerForm : Form
    {
        private readonly DdfLanguagePackageStore store;
        private readonly Action<string> activate;
        private readonly Action disable;
        private readonly Action<DdfLanguagePackage> openPlayground;
        private readonly ListView packagesList;
        private readonly TextBox resultTextBox;

        public LanguagePackageManagerForm(DdfLanguagePackageStore store, Action<string> activate,
            Action disable, Action<DdfLanguagePackage> openPlayground)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.activate = activate ?? throw new ArgumentNullException(nameof(activate));
            this.disable = disable ?? throw new ArgumentNullException(nameof(disable));
            this.openPlayground = openPlayground ?? throw new ArgumentNullException(nameof(openPlayground));
            BackColor = AppTheme.Window;
            ClientSize = new Size(820, 520);
            Font = new Font("Segoe UI", 9F);
            ForeColor = AppTheme.Text;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(700, 430);
            Name = "LanguagePackageManagerForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Gestione pacchetti linguaggio";
            Icon = AppIconProvider.LoadIcon();

            var title = new Label
            {
                Dock = DockStyle.Top, Height = 42, Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Text = "Pacchetti installati"
            };
            packagesList = new ListView
            {
                BackColor = AppTheme.Surface, Dock = DockStyle.Fill, ForeColor = AppTheme.Text,
                FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false,
                Name = "languagePackagesListView", View = View.Details
            };
            packagesList.Columns.Add("Nome", 220);
            packagesList.Columns.Add("ID", 170);
            packagesList.Columns.Add("Pacchetto", 100);
            packagesList.Columns.Add("Linguaggio", 100);
            packagesList.DoubleClick += (sender, args) => ActivateSelected();

            resultTextBox = new TextBox
            {
                BackColor = AppTheme.Surface, Dock = DockStyle.Bottom, Height = 72,
                Multiline = true, Name = "languagePackageManagerResultTextBox", ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Text = "Seleziona un pacchetto o installane uno nuovo."
            };
            var buttons = new TableLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Bottom, ColumnCount = 5, RowCount = 2,
                Name = "languagePackageManagerButtons", Padding = new Padding(8)
            };
            for (int column = 0; column < 5; column++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var archive = Button("languagePackageInstallArchiveButton", "Installa/Aggiorna archivio...", InstallArchive, 174);
            var folder = Button("languagePackageInstallFolderButton", "Installa/Aggiorna cartella...", InstallFolder, 174);
            buttons.Controls.Add(archive, 0, 0); buttons.SetColumnSpan(archive, 2);
            buttons.Controls.Add(folder, 2, 0); buttons.SetColumnSpan(folder, 3);
            buttons.Controls.Add(Button("languagePackageValidateButton", "Convalida", (s, e) => ValidateSelected(), 100), 0, 1);
            buttons.Controls.Add(Button("languagePackageActivateButton", "Attiva", (s, e) => ActivateSelected(), 90), 1, 1);
            buttons.Controls.Add(Button("languagePackageDisableButton", "Disattiva", (s, e) => { disable(); RefreshPackages(); }, 90), 2, 1);
            buttons.Controls.Add(Button("languagePackagePlaygroundButton", "Playground", (s, e) => OpenSelectedPlayground(), 100), 3, 1);
            var close = Button("languagePackageManagerCloseButton", "Chiudi", (s, e) => Close(), 90);
            close.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(close, 4, 1);

            Controls.Add(packagesList);
            Controls.Add(resultTextBox);
            Controls.Add(buttons);
            Controls.Add(title);
            CancelButton = close;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
            RefreshPackages();
        }

        private static Button Button(string name, string text, EventHandler handler, int width)
        {
            var button = new Button { Height = 30, Name = name, Text = text, Width = width,
                Dock = DockStyle.Fill, Margin = new Padding(3), UseVisualStyleBackColor = false };
            button.Click += handler;
            return button;
        }

        private void RefreshPackages()
        {
            packagesList.Items.Clear();
            foreach (DdfInstalledLanguagePackage package in store.List())
            {
                var item = new ListViewItem(package.Name) { Tag = package };
                item.SubItems.Add(package.Id);
                item.SubItems.Add(package.Version);
                item.SubItems.Add(package.LanguageVersion);
                packagesList.Items.Add(item);
            }
            Text = "Gestione pacchetti linguaggio — " + packagesList.Items.Count + " installati";
        }

        private void InstallArchive(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = true, Filter = "Pacchetti linguaggio (*.ddflang)|*.ddflang|Tutti i file (*.*)|*.*",
                Multiselect = false, Title = "Installa pacchetto linguaggio"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) Install(dialog.FileName);
        }

        private void InstallFolder(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            { Description = "Installa o aggiorna il pacchetto contenuto nella cartella", ShowNewFolderButton = false };
            if (dialog.ShowDialog(this) == DialogResult.OK) Install(dialog.SelectedPath);
        }

        private void Install(string path)
        {
            try
            {
                DdfLanguagePackageInstallResult result = store.Install(path);
                resultTextBox.Text = result.Success
                    ? (result.Updated ? "Pacchetto aggiornato: " : "Pacchetto installato: ") + result.Installed
                    : string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString()));
                RefreshPackages();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { resultTextBox.Text = "Installazione non riuscita: " + exception.Message; }
        }

        private DdfInstalledLanguagePackage Selected() =>
            packagesList.SelectedItems.Count == 1 ? packagesList.SelectedItems[0].Tag as DdfInstalledLanguagePackage : null;

        private void ValidateSelected()
        {
            DdfInstalledLanguagePackage selected = Selected();
            if (selected == null) { resultTextBox.Text = "Selezionare un pacchetto installato."; return; }
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(selected.Path);
            resultTextBox.Text = result.Success
                ? "Convalida e test dichiarativi completati: " + selected + "."
                : string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString()));
        }

        private void ActivateSelected()
        {
            DdfInstalledLanguagePackage selected = Selected();
            if (selected == null) { resultTextBox.Text = "Selezionare un pacchetto installato."; return; }
            activate(selected.Path);
            resultTextBox.Text = "Pacchetto attivo: " + selected + ".";
        }

        private void OpenSelectedPlayground()
        {
            DdfInstalledLanguagePackage selected = Selected();
            if (selected == null) { openPlayground(DdfLanguageCatalog.DefaultPackage); return; }
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(selected.Path);
            if (!result.Success) { resultTextBox.Text = string.Join(Environment.NewLine, result.Diagnostics); return; }
            openPlayground(result.Package);
        }
    }
}
