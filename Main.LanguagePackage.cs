using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem loadLanguageArchiveMenuItem;
        private ToolStripMenuItem loadLanguageFolderMenuItem;
        private ToolStripMenuItem useEmbeddedLanguageMenuItem;
        private ToolStripDropDownButton toolbarLanguageButton;
        private IReadOnlyList<DdfLanguagePackageDiagnostic> languagePackageActivationDiagnostics =
            Array.Empty<DdfLanguagePackageDiagnostic>();

        private void restoreActiveLanguagePackage()
        {
            string path = (AppSettingsStore.LoadLanguagePackagePath() ?? string.Empty).Trim();
            if (path.Length == 0) return;
            DdfLanguagePackageLoadResult result = DdfLanguageCatalog.TryActivate(path);
            if (!result.Success) languagePackageActivationDiagnostics = result.Diagnostics;
        }

        private void recreateLanguageServices()
        {
            incrementalLexer = new IncrementalDdfLexer(DdfLanguageCatalog.Default);
            quickFixService = DdfQuickFixService.CreateDefault(DdfLanguageCatalog.Default.Formatting);
        }

        private void initializeLanguagePackageCommands()
        {
            loadLanguageArchiveMenuItem = new ToolStripMenuItem("Carica archivio .ddflang...")
            {
                Name = "loadLanguageArchiveMenuItem"
            };
            loadLanguageArchiveMenuItem.Click += loadLanguageArchiveMenuItem_Click;
            loadLanguageFolderMenuItem = new ToolStripMenuItem("Carica da cartella...")
            {
                Name = "loadLanguageFolderMenuItem"
            };
            loadLanguageFolderMenuItem.Click += loadLanguageFolderMenuItem_Click;
            useEmbeddedLanguageMenuItem = new ToolStripMenuItem("Ripristina linguaggio incorporato")
            {
                Name = "useEmbeddedLanguageMenuItem"
            };
            useEmbeddedLanguageMenuItem.Click += useEmbeddedLanguageMenuItem_Click;

            languagePackageInfoMenuItem.Click -= languagePackageInfoMenuItem_Click;
            languagePackageInfoMenuItem.Text = "&Linguaggio";
            languagePackageInfoMenuItem.DropDownItems.Add(loadLanguageArchiveMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(loadLanguageFolderMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(useEmbeddedLanguageMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(new ToolStripSeparator());
            var detailsItem = new ToolStripMenuItem("Informazioni sul pacchetto attivo...")
            {
                Name = "languagePackageDetailsMenuItem"
            };
            detailsItem.Click += languagePackageInfoMenuItem_Click;
            languagePackageInfoMenuItem.DropDownItems.Add(detailsItem);

            toolbarLanguageButton = new ToolStripDropDownButton
            {
                AccessibleName = "Linguaggio attivo",
                AutoSize = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new System.Drawing.Font("Segoe MDL2 Assets", 11F),
                Margin = new Padding(1, 1, 1, 2),
                Name = "toolbarLanguageButton",
                Size = new System.Drawing.Size(38, 28),
                Text = "\uE8F1"
            };
            toolbarLanguageButton.DropDownItems.Add("Carica archivio .ddflang...", null, loadLanguageArchiveMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add("Carica da cartella...", null, loadLanguageFolderMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add("Ripristina incorporato", null, useEmbeddedLanguageMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add(new ToolStripSeparator());
            toolbarLanguageButton.DropDownItems.Add("Informazioni...", null, languagePackageInfoMenuItem_Click);
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(toolbarLanguageButton);
        }

        private void applyActiveLanguagePackageStatus()
        {
            DdfLanguagePackage package = DdfLanguageCatalog.DefaultPackage;
            string origin = package.Origin == DdfLanguagePackageOrigin.Directory
                ? "cartella"
                : package.Origin == DdfLanguagePackageOrigin.Archive ? "archivio" : "incorporato";

            statusEncodingLabel.Text = "UTF-8 · " + package.Name + " " + package.LanguageVersion + " · " + origin;
            statusEncodingLabel.ToolTipText =
                "Pacchetto linguaggio: " + package.Id + " " + package.PackageVersion + Environment.NewLine +
                "Origine: " + origin + Environment.NewLine +
                "Sorgente: " + package.Source;

            if (DdfLanguageCatalog.LoadDiagnostics.Count > 0)
            {
                statusEncodingLabel.ToolTipText += Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, DdfLanguageCatalog.LoadDiagnostics.Select(item => item.ToString()));
            }
            if (languagePackageActivationDiagnostics.Count > 0)
            {
                statusEncodingLabel.ToolTipText += Environment.NewLine + Environment.NewLine +
                    "Ultimo caricamento non riuscito:" + Environment.NewLine +
                    string.Join(Environment.NewLine, languagePackageActivationDiagnostics.Select(item => item.ToString()));
            }

            if (useEmbeddedLanguageMenuItem != null)
                useEmbeddedLanguageMenuItem.Checked = package.Origin == DdfLanguagePackageOrigin.Embedded;
            if (languagePackageInfoMenuItem != null)
                languagePackageInfoMenuItem.Text = "&Linguaggio — " + package.Name + " " + package.LanguageVersion;
            if (toolbarLanguageButton != null)
            {
                toolbarLanguageButton.ToolTipText = "Linguaggio attivo: " + package.Name + " " + package.LanguageVersion;
                toolbarLanguageButton.AccessibleName = toolbarLanguageButton.ToolTipText;
            }
        }

        private void loadLanguageArchiveMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                DefaultExt = "ddflang",
                Filter = "Pacchetti linguaggio DDF (*.ddflang)|*.ddflang|Tutti i file (*.*)|*.*",
                Multiselect = false,
                Title = "Carica pacchetto linguaggio"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) activateLanguagePackage(dialog.FileName);
            }
        }

        private void loadLanguageFolderMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Seleziona la cartella che contiene manifest.json del pacchetto linguaggio",
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) activateLanguagePackage(dialog.SelectedPath);
            }
        }

        private void useEmbeddedLanguageMenuItem_Click(object sender, EventArgs e)
        {
            cancelBackgroundAnalysis();
            applyLanguagePackageResult(DdfLanguageCatalog.ActivateEmbedded(), string.Empty);
        }

        private void activateLanguagePackage(string path)
        {
            cancelBackgroundAnalysis();
            applyLanguagePackageResult(DdfLanguageCatalog.TryActivate(path), path);
        }

        private void applyLanguagePackageResult(DdfLanguagePackageLoadResult result, string persistedPath)
        {
            if (result == null || !result.Success)
            {
                languagePackageActivationDiagnostics = result?.Diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>();
                applyActiveLanguagePackageStatus();
                string details = languagePackageActivationDiagnostics.Count == 0
                    ? "Il pacchetto non è valido. Il linguaggio precedente resta attivo."
                    : string.Join(Environment.NewLine, languagePackageActivationDiagnostics.Select(item => item.ToString()));
                MessageBox.Show(this, details, "Pacchetto linguaggio non caricato",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            languagePackageActivationDiagnostics = result.Diagnostics;
            try
            {
                AppSettingsStore.SaveLanguagePackagePath(persistedPath ?? string.Empty);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Pacchetto attivato, ma preferenza non salvata: " + exception.Message;
            }

            stopExecution();
            cancelBackgroundAnalysis();
            hideCompletion();
            hideSignatureHelp();
            leaveFoldedView();
            recreateLanguageServices();
            languagePackageInfoForm?.Close();
            applyActiveLanguagePackageStatus();

            if (workspaceIndex != null) startWorkspaceLoad(workspaceIndex.RootPath);
            else
            {
                diagnosticsFormatStart = 0;
                incrementalLexer.Reset();
                applyHighlighting();
                scheduleWorkspaceSearch();
            }
        }

        private static bool isActiveLanguageExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return DdfLanguageCatalog.DefaultPackage.FileExtensions.Any(item =>
                string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static string getDefaultDocumentExtension()
        {
            return DdfLanguageCatalog.DefaultPackage.FileExtensions.First().TrimStart('.');
        }

        private static string getDocumentFilter()
        {
            DdfLanguagePackage package = DdfLanguageCatalog.DefaultPackage;
            string patterns = string.Join(";", package.FileExtensions.Select(item => "*" + item));
            string name = (package.Name ?? "Linguaggio").Replace("|", string.Empty);
            return "Sorgenti " + name + " (" + patterns + ")|" + patterns + "|Tutti i file (*.*)|*.*";
        }

        private void languagePackageInfoMenuItem_Click(object sender, EventArgs e)
        {
            if (languagePackageInfoForm == null || languagePackageInfoForm.IsDisposed)
            {
                languagePackageInfoForm = new LanguagePackageInfoForm();
                languagePackageInfoForm.FormClosed += (closedSender, closedArgs) => languagePackageInfoForm = null;
                languagePackageInfoForm.Show(this);
                return;
            }

            if (languagePackageInfoForm.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                languagePackageInfoForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
            languagePackageInfoForm.Activate();
        }
    }
}
