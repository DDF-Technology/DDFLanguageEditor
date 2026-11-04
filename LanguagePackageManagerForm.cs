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
        private readonly Func<DdfLanguagePackage> activePackage;
        private readonly ListView packagesList;
        private readonly Label activePackageLabel;
        private readonly Button installActiveButton;
        private readonly TextBox resultTextBox;

        public LanguagePackageManagerForm(DdfLanguagePackageStore store, Action<string> activate,
            Action disable, Action<DdfLanguagePackage> openPlayground,
            Func<DdfLanguagePackage> activePackage = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.activate = activate ?? throw new ArgumentNullException(nameof(activate));
            this.disable = disable ?? throw new ArgumentNullException(nameof(disable));
            this.openPlayground = openPlayground ?? throw new ArgumentNullException(nameof(openPlayground));
            this.activePackage = activePackage ?? (() => DdfLanguagePackageLoader.LoadEmbeddedDefault().Package);
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
            activePackageLabel = new Label
            {
                Dock = DockStyle.Top, Height = 48,
                Name = "languagePackageManagerActiveLabel",
                Padding = new Padding(12, 0, 12, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
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
            var archive = Button("languagePackageInstallArchiveButton", "Installa archivio...", InstallArchive, 174);
            var folder = Button("languagePackageInstallFolderButton", "Installa cartella...", InstallFolder, 174);
            installActiveButton = Button("languagePackageInstallActiveButton",
                "Installa attivo", InstallActive, 120);
            buttons.Controls.Add(archive, 0, 0); buttons.SetColumnSpan(archive, 2);
            buttons.Controls.Add(folder, 2, 0); buttons.SetColumnSpan(folder, 2);
            buttons.Controls.Add(installActiveButton, 4, 0);
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
            Controls.Add(activePackageLabel);
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
            var installed = store.List();
            foreach (DdfInstalledLanguagePackage package in installed)
            {
                var item = new ListViewItem(package.Name) { Tag = package };
                item.SubItems.Add(package.Id);
                item.SubItems.Add(package.Version);
                item.SubItems.Add(package.LanguageVersion);
                packagesList.Items.Add(item);
            }
            Text = "Gestione pacchetti linguaggio — " + packagesList.Items.Count + " installati";
            DdfLanguagePackage active = activePackage();
            bool inStore = active != null && installed.Any(item =>
                string.Equals(item.Path, active.Source,
                    StringComparison.OrdinalIgnoreCase));
            bool versionInstalled = active != null && installed.Any(item =>
                string.Equals(item.Id, active.Id, StringComparison.Ordinal) &&
                string.Equals(item.Version, active.PackageVersion,
                    StringComparison.Ordinal));
            installActiveButton.Enabled = active != null &&
                active.Origin != DdfLanguagePackageOrigin.Embedded &&
                !versionInstalled && (Directory.Exists(active.Source) ||
                    File.Exists(active.Source));
            activePackageLabel.Text = active == null
                ? "Nessun pacchetto attivo."
                : "Attivo: " + active.Name + " " + active.PackageVersion +
                    (active.Origin == DdfLanguagePackageOrigin.Embedded
                        ? " — DDF incorporato (fallback, non è un'installazione utente)."
                        : inStore
                            ? " — installato nello store utente."
                            : versionInstalled
                                ? " — versione installata, ma questa sessione usa una copia esterna. Seleziona la versione in elenco e premi Attiva."
                            : " — caricato da cartella/archivio esterno; non compare tra gli installati. Premi Installa attivo per copiarlo nello store.");
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
            { Description = "Installa una nuova versione del pacchetto contenuto nella cartella", ShowNewFolderButton = false };
            if (dialog.ShowDialog(this) == DialogResult.OK) Install(dialog.SelectedPath);
        }

        private void Install(string path)
        {
            try
            {
                DdfLanguagePackageInstallResult result = store.Install(path);
                resultTextBox.Text = result.Success
                    ? "Pacchetto installato: " + result.Installed
                    : string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString()));
                RefreshPackages();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { resultTextBox.Text = "Installazione non riuscita: " + exception.Message; }
        }

        private void InstallActive(object sender, EventArgs e)
        {
            DdfLanguagePackage current = activePackage();
            if (current == null || current.Origin ==
                DdfLanguagePackageOrigin.Embedded) return;
            Install(current.Source);
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
            if (selected == null) { openPlayground(activePackage()); return; }
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(selected.Path);
            if (!result.Success) { resultTextBox.Text = string.Join(Environment.NewLine, result.Diagnostics); return; }
            openPlayground(result.Package);
        }
    }
}
