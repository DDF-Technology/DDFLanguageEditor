using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class ModulePackageManagerForm : Form
    {
        private readonly DdfModulePackageStore store;
        private readonly ListView modulesList;
        private readonly TextBox detailsTextBox;
        private readonly TextBox resultTextBox;
        private readonly DdfEmbeddedModuleStoreOperations allowedOperations;

        public ModulePackageManagerForm(DdfModulePackageStore store,
            DdfEmbeddedModuleStoreOperations allowedOperations = DdfEmbeddedModuleStoreOperations.All)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            if ((allowedOperations & ~DdfEmbeddedModuleStoreOperations.All) != 0 ||
                (allowedOperations & DdfEmbeddedModuleStoreOperations.BrowseAndValidate) == 0)
                throw new ArgumentOutOfRangeException(nameof(allowedOperations));
            this.allowedOperations = allowedOperations;
            BackColor = AppTheme.Window;
            ClientSize = new Size(940, 570);
            Font = new Font("Segoe UI", 9F);
            ForeColor = AppTheme.Text;
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon = AppIconProvider.LoadIcon();
            MinimumSize = new Size(900, 470);
            Name = "ModulePackageManagerForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Gestione moduli progetto";

            var title = new Label
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                Height = 42,
                Padding = new Padding(12, 0, 0, 0),
                Text = "Moduli installati nel progetto",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var project = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Name = "modulePackageProjectLabel",
                Padding = new Padding(12, 0, 8, 0),
                Text = store.ProjectRoot,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            modulesList = new ListView
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.Text,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false,
                Name = "modulePackagesListView",
                View = View.Details
            };
            modulesList.Columns.Add("Nome", 180);
            modulesList.Columns.Add("ID", 210);
            modulesList.Columns.Add("Versione", 90);
            modulesList.Columns.Add("Export", 65);
            modulesList.Columns.Add("Dipendenze", 85);
            modulesList.SelectedIndexChanged += (sender, args) => ShowSelectedDetails();

            detailsTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Fill,
                Multiline = true,
                Name = "modulePackageDetailsTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            var content = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Name = "modulePackageManagerContentPanel",
                RowCount = 1
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            content.Controls.Add(modulesList, 0, 0);
            content.Controls.Add(detailsTextBox, 1, 0);

            resultTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                Dock = DockStyle.Bottom,
                Height = 74,
                Multiline = true,
                Name = "modulePackageManagerResultTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "Seleziona un modulo oppure installa un pacchetto .ddfmod."
            };
            var buttons = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 7,
                Dock = DockStyle.Bottom,
                Name = "modulePackageManagerButtons",
                Padding = new Padding(8),
                RowCount = 1
            };
            for (int column = 0; column < buttons.ColumnCount; column++)
                buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / buttons.ColumnCount));

            buttons.Controls.Add(Button("modulePackageInstallArchiveButton", "Installa .ddfmod...", InstallArchive,
                Allows(DdfEmbeddedModuleStoreOperations.Install)), 0, 0);
            buttons.Controls.Add(Button("modulePackageInstallFolderButton", "Installa cartella...", InstallFolder,
                Allows(DdfEmbeddedModuleStoreOperations.Install)), 1, 0);
            buttons.Controls.Add(Button("modulePackageCreateButton", "Crea .ddfmod...", CreatePackage,
                Allows(DdfEmbeddedModuleStoreOperations.CreatePackage)), 2, 0);
            buttons.Controls.Add(Button("modulePackageRemoveButton", "Rimuovi", RemoveSelected,
                Allows(DdfEmbeddedModuleStoreOperations.Remove)), 3, 0);
            buttons.Controls.Add(Button("modulePackageValidateButton", "Convalida", ValidateSelected,
                Allows(DdfEmbeddedModuleStoreOperations.BrowseAndValidate)), 4, 0);
            buttons.Controls.Add(Button("modulePackageRefreshButton", "Aggiorna", (s, e) => RefreshModules()), 5, 0);
            var close = Button("modulePackageManagerCloseButton", "Chiudi", (s, e) => Close());
            close.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(close, 6, 0);

            Controls.Add(content);
            Controls.Add(resultTextBox);
            Controls.Add(buttons);
            Controls.Add(project);
            Controls.Add(title);
            CancelButton = close;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
            RefreshModules();
        }

        public string ProjectRoot => store.ProjectRoot;

        private static Button Button(string name, string text, EventHandler handler, bool enabled = true)
        {
            var button = new Button
            {
                Dock = DockStyle.Fill,
                Height = 32,
                Margin = new Padding(3),
                Name = name,
                Enabled = enabled,
                Text = text,
                UseVisualStyleBackColor = false
            };
            button.Click += handler;
            return button;
        }

        private void RefreshModules()
        {
            string selectedKey = Selected() is DdfInstalledModule selected
                ? selected.Id + "@" + selected.Version : null;
            modulesList.BeginUpdate();
            try
            {
                modulesList.Items.Clear();
                foreach (DdfInstalledModule module in store.List())
                {
                    var item = new ListViewItem(module.Name) { Tag = module };
                    item.SubItems.Add(module.Id);
                    item.SubItems.Add(module.Version);
                    item.SubItems.Add(module.Exports.Count.ToString());
                    item.SubItems.Add(module.Dependencies.Count.ToString());
                    modulesList.Items.Add(item);
                    if (selectedKey == module.Id + "@" + module.Version) item.Selected = true;
                }
            }
            finally { modulesList.EndUpdate(); }
            Text = "Gestione moduli progetto — " + modulesList.Items.Count + " installati";
            if (modulesList.SelectedItems.Count == 0) detailsTextBox.Text =
                "Store locale:" + Environment.NewLine + store.RootPath + Environment.NewLine + Environment.NewLine +
                "I percorsi sono dettagli di archiviazione e non creano namespace.";
        }

        private void InstallArchive(object sender, EventArgs e)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.Install)) return;
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "Moduli DDF (*.ddfmod)|*.ddfmod",
                Multiselect = false,
                Title = "Installa modulo DDF"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) PreviewAndInstall(dialog.FileName);
        }

        private void InstallFolder(object sender, EventArgs e)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.Install)) return;
            using var dialog = new FolderBrowserDialog
            {
                Description = "Seleziona la cartella del modulo DDF",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) PreviewAndInstall(dialog.SelectedPath);
        }

        private void PreviewAndInstall(string path)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.Install)) return;
            DdfModulePackageResult preview = store.Inspect(path);
            if (!preview.Success)
            {
                resultTextBox.Text = Describe(preview);
                return;
            }
            DdfInstalledModule module = preview.Module;
            bool exists = store.List().Any(item => item.Id == module.Id && item.Version == module.Version);
            string action = exists
                ? "La stessa versione è già installata e verrà sostituita esplicitamente."
                : "La versione verrà installata accanto alle altre versioni dello stesso modulo.";
            string message = FormatModule(module) + Environment.NewLine + Environment.NewLine + action +
                Environment.NewLine + Environment.NewLine + "Continuare?";
            if (MessageBox.Show(this, message, "Anteprima modulo", MessageBoxButtons.YesNo,
                    exists ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;
            DdfModulePackageResult installed = store.Install(path, exists);
            resultTextBox.Text = installed.Success
                ? (installed.Replaced ? "Modulo sostituito: " : "Modulo installato: ") + installed.Module + "."
                : Describe(installed);
            RefreshModules();
        }

        private void CreatePackage(object sender, EventArgs e)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.CreatePackage)) return;
            DdfProjectManifestLoadResult manifest = DdfProjectManifestLoader.Load(store.ProjectRoot);
            if (!manifest.Success)
            {
                resultTextBox.Text = string.Join(Environment.NewLine,
                    manifest.Issues.Select(item => item.ToString()));
                return;
            }
            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = DdfModulePackageStore.PackageExtension.TrimStart('.'),
                FileName = manifest.Manifest.Module.Id + "-" + manifest.Manifest.Module.Version +
                    DdfModulePackageStore.PackageExtension,
                Filter = "Moduli DDF (*.ddfmod)|*.ddfmod",
                OverwritePrompt = true,
                Title = "Crea pacchetto modulo DDF"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            DdfModulePackageResult packed = store.Pack(store.ProjectRoot, dialog.FileName);
            resultTextBox.Text = packed.Success
                ? "Pacchetto creato: " + packed.PackagePath
                : Describe(packed);
        }

        private void RemoveSelected(object sender, EventArgs e)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.Remove)) return;
            DdfInstalledModule selected = Selected();
            if (selected == null) { resultTextBox.Text = "Selezionare un modulo installato."; return; }
            if (MessageBox.Show(this, "Rimuovere " + selected + " dallo store del progetto?",
                    "Rimuovi modulo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                resultTextBox.Text = store.Remove(selected.Id, selected.Version)
                    ? "Modulo rimosso: " + selected + "."
                    : "Il modulo non è più presente o non corrisponde alla propria identità.";
                RefreshModules();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                                exception is InvalidDataException || exception is ArgumentException)
            { resultTextBox.Text = "Rimozione non riuscita: " + exception.Message; }
        }

        private void ValidateSelected(object sender, EventArgs e)
        {
            if (!EnsureAllowed(DdfEmbeddedModuleStoreOperations.BrowseAndValidate)) return;
            DdfInstalledModule selected = Selected();
            if (selected == null) { resultTextBox.Text = "Selezionare un modulo installato."; return; }
            DdfModulePackageResult preview = store.Inspect(selected.Path);
            resultTextBox.Text = preview.Success
                ? "Manifesto e payload validi: " + selected + ". La semantica completa verrà verificata dal build consumatore."
                : Describe(preview);
        }

        private DdfInstalledModule Selected() => modulesList.SelectedItems.Count == 1
            ? modulesList.SelectedItems[0].Tag as DdfInstalledModule : null;

        private bool Allows(DdfEmbeddedModuleStoreOperations operation) =>
            (allowedOperations & operation) == operation;

        private bool EnsureAllowed(DdfEmbeddedModuleStoreOperations operation)
        {
            if (Allows(operation)) return true;
            resultTextBox.Text = "Operazione non autorizzata dall'applicazione host.";
            return false;
        }

        private void ShowSelectedDetails()
        {
            DdfInstalledModule selected = Selected();
            if (selected != null) detailsTextBox.Text = FormatModule(selected) +
                Environment.NewLine + Environment.NewLine + "Percorso:" + Environment.NewLine + selected.Path;
        }

        private static string FormatModule(DdfInstalledModule module)
        {
            var text = new StringBuilder();
            text.AppendLine(module.Name);
            text.AppendLine(module.Id + "@" + module.Version);
            text.AppendLine("Linguaggio: " + module.LanguageId + " " + module.LanguagePackageVersion);
            text.AppendLine();
            text.AppendLine("Export:");
            if (module.Exports.Count == 0) text.AppendLine("  (nessuno)");
            else foreach (string export in module.Exports) text.AppendLine("  " + export);
            text.AppendLine();
            text.AppendLine("Dipendenze:");
            if (module.Dependencies.Count == 0) text.AppendLine("  (nessuna)");
            else foreach (DdfModuleDependencyInfo dependency in module.Dependencies)
                text.AppendLine("  " + dependency);
            return text.ToString().TrimEnd();
        }

        private static string Describe(DdfModulePackageResult result) =>
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString()));
    }
}
