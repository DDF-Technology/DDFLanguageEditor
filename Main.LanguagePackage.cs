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
        private ToolStripMenuItem useEmbeddedLanguageMenuItem;
        private ToolStripMenuItem manageLanguagePackagesMenuItem;
        private ToolStripMenuItem validateLanguagePackageMenuItem;
        private ToolStripMenuItem languagePlaygroundMenuItem;
        private ToolStripMenuItem languagePackageStudioMenuItem;
        private ToolStripMenuItem toolbarLanguageStudioMenuItem;
        private ToolStripDropDownButton toolbarLanguageButton;
        private LanguagePackageManagerForm languagePackageManagerForm;
        private LanguagePlaygroundForm languagePlaygroundForm;
        private Form languagePackageStudioForm;
        private Action<string> saveLanguagePackagePathSetting;
        private IDdfLanguagePackageSession editorLanguageSession;
        private DdfLanguagePackage observedLanguagePackage;
        private string pendingLanguagePreferencePath;
        private bool persistPendingLanguagePreference;
        private IReadOnlyList<DdfLanguagePackageDiagnostic> languagePackageActivationDiagnostics =
            Array.Empty<DdfLanguagePackageDiagnostic>();

        private void restoreActiveLanguagePackage()
        {
            string path = (AppSettingsStore.LoadLanguagePackagePath() ?? string.Empty).Trim();
            if (path.Length == 0) return;
            string installedPath = migratePersistedUserPackage(path);
            DdfLanguagePackageLoadResult result = DdfLanguageCatalog.TryActivate(installedPath);
            if (result.Success && !string.Equals(path, installedPath, StringComparison.OrdinalIgnoreCase))
            {
                try { AppSettingsStore.SaveLanguagePackagePath(installedPath); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) { }
            }
            if (!result.Success) languagePackageActivationDiagnostics = result.Diagnostics;
        }

        private void installShippedUserLanguagePackages()
        {
            if (embeddedHostMode) return;
            string seedRoot = Path.Combine(AppContext.BaseDirectory, "language-package-seeds");
            if (!Directory.Exists(seedRoot)) return;
            try
            {
                var completedSeeds = new HashSet<string>((AppSettingsStore.LoadLanguagePackageSeeds() ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);
                var store = new DdfLanguagePackageStore(AppSettingsStore.GetLanguagePackagesDirectory());
                bool changed = false;
                foreach (string directory in Directory.EnumerateDirectories(seedRoot)
                    .Where(item => File.Exists(Path.Combine(item, "manifest.json"))))
                {
                    DdfLanguagePackageLoadResult candidate = DdfLanguagePackageLoader.Load(directory);
                    if (!candidate.Success || string.Equals(candidate.Package.Id, "ddf", StringComparison.Ordinal)) continue;
                    string key = candidate.Package.Id + "@" + candidate.Package.PackageVersion;
                    if (completedSeeds.Contains(key)) continue;

                    bool alreadyInstalled = store.List().Any(item =>
                        string.Equals(item.Id, candidate.Package.Id, StringComparison.Ordinal) &&
                        string.Equals(item.Version, candidate.Package.PackageVersion, StringComparison.Ordinal));
                    DdfLanguagePackageInstallResult installation = alreadyInstalled ? null : store.Install(directory);
                    if (alreadyInstalled || installation.Success)
                    {
                        completedSeeds.Add(key);
                        changed = true;
                        DdfInstalledLanguagePackage installed = store.List()
                            .FirstOrDefault(item =>
                                string.Equals(item.Id, candidate.Package.Id, StringComparison.Ordinal) &&
                                string.Equals(item.Version, candidate.Package.PackageVersion, StringComparison.Ordinal));
                        upgradePersistedShippedPackage(candidate.Package, installed);
                    }
                }
                if (changed)
                    AppSettingsStore.SaveLanguagePackageSeeds(string.Join(Environment.NewLine,
                        completedSeeds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                languagePackageActivationDiagnostics = new[]
                {
                    new DdfLanguagePackageDiagnostic("DDFL006",
                        "I pacchetti utente distribuiti non possono essere installati: " + exception.Message)
                };
            }
        }

        private static void upgradePersistedShippedPackage(DdfLanguagePackage candidate,
            DdfInstalledLanguagePackage installed)
        {
            if (candidate == null || installed == null) return;
            string persistedPath = (AppSettingsStore.LoadLanguagePackagePath() ?? string.Empty).Trim();
            if (persistedPath.Length == 0) return;
            DdfLanguagePackageLoadResult persisted = DdfLanguagePackageLoader.Load(persistedPath);
            string persistedId = persisted.Success ? persisted.Package.Id :
                Path.GetFileName(persistedPath.TrimEnd(Path.DirectorySeparatorChar));
            if (!string.Equals(persistedId, candidate.Id, StringComparison.Ordinal)) return;
            if (persisted.Success && Version.Parse(persisted.Package.PackageVersion) >=
                Version.Parse(candidate.PackageVersion)) return;
            AppSettingsStore.SaveLanguagePackagePath(installed.Path);
        }

        private static string migratePersistedUserPackage(string path)
        {
            try
            {
                var store = new DdfLanguagePackageStore(AppSettingsStore.GetLanguagePackagesDirectory());
                DdfLanguagePackageLoadResult source = DdfLanguagePackageLoader.Load(path);
                string packageId = source.Success ? source.Package.Id :
                    Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(packageId) || string.Equals(packageId, "ddf", StringComparison.Ordinal))
                    return path;

                DdfInstalledLanguagePackage installed = store.List()
                    .Where(item => string.Equals(item.Id, packageId, StringComparison.Ordinal))
                    .OrderByDescending(item => Version.Parse(item.Version))
                    .FirstOrDefault(item => !source.Success ||
                        string.Equals(item.Version, source.Package.PackageVersion, StringComparison.Ordinal));
                if (installed != null) return installed.Path;
                if (!source.Success) return path;

                DdfLanguagePackageInstallResult migrated = store.Install(path);
                return migrated.Success ? migrated.Installed.Path : path;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return path;
            }
        }

        private void recreateLanguageServices()
        {
            DdfLanguageDefinition language = editorLanguageSession.ActivePackage.Language;
            incrementalLexer = new IncrementalDdfLexer(language);
            quickFixService = DdfQuickFixService.CreateDefault(language.Formatting);
        }

        private void initializeLanguagePackageCommands()
        {
            saveLanguagePackagePathSetting = AppSettingsStore.SaveLanguagePackagePath;
            useEmbeddedLanguageMenuItem = new ToolStripMenuItem("Ripristina DDF incorporato")
            {
                Name = "useEmbeddedLanguageMenuItem"
            };
            useEmbeddedLanguageMenuItem.Click += useEmbeddedLanguageMenuItem_Click;
            manageLanguagePackagesMenuItem = new ToolStripMenuItem("Gestisci pacchetti...")
            {
                Name = "manageLanguagePackagesMenuItem"
            };
            manageLanguagePackagesMenuItem.Click += manageLanguagePackagesMenuItem_Click;
            validateLanguagePackageMenuItem = new ToolStripMenuItem("Convalida pacchetto attivo")
            {
                Name = "validateLanguagePackageMenuItem"
            };
            validateLanguagePackageMenuItem.Click += validateLanguagePackageMenuItem_Click;
            languagePlaygroundMenuItem = new ToolStripMenuItem("Playground linguaggio...")
            {
                Name = "languagePlaygroundMenuItem"
            };
            languagePlaygroundMenuItem.Click += languagePlaygroundMenuItem_Click;
            languagePackageStudioMenuItem = new ToolStripMenuItem("Package Studio...")
            {
                Name = "languagePackageStudioMenuItem"
            };
            languagePackageStudioMenuItem.Click += languagePackageStudioMenuItem_Click;

            languagePackageInfoMenuItem.Click -= languagePackageInfoMenuItem_Click;
            languagePackageInfoMenuItem.Text = "&Linguaggio";
            helpMenuItem.DropDownItems.Remove(languagePackageInfoMenuItem);
            helpMenuItem.DropDownItems.Remove(helpSeparator);
            int helpIndex = menuStripMain.Items.IndexOf(helpMenuItem);
            if (helpIndex < 0) menuStripMain.Items.Add(languagePackageInfoMenuItem);
            else menuStripMain.Items.Insert(helpIndex, languagePackageInfoMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(manageLanguagePackagesMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(languagePackageStudioMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(validateLanguagePackageMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(languagePlaygroundMenuItem);
            languagePackageInfoMenuItem.DropDownItems.Add(new ToolStripSeparator());
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
            toolbarLanguageButton.DropDownItems.Add("Gestisci pacchetti...", null, manageLanguagePackagesMenuItem_Click);
            toolbarLanguageStudioMenuItem = new ToolStripMenuItem(
                "Package Studio...", null, languagePackageStudioMenuItem_Click)
            {
                Name = "toolbarLanguageStudioMenuItem"
            };
            toolbarLanguageButton.DropDownItems.Add(toolbarLanguageStudioMenuItem);
            toolbarLanguageButton.DropDownItems.Add("Convalida pacchetto attivo", null, validateLanguagePackageMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add("Playground...", null, languagePlaygroundMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add(new ToolStripSeparator());
            toolbarLanguageButton.DropDownItems.Add("Ripristina DDF incorporato", null, useEmbeddedLanguageMenuItem_Click);
            toolbarLanguageButton.DropDownItems.Add(new ToolStripSeparator());
            toolbarLanguageButton.DropDownItems.Add("Informazioni...", null, languagePackageInfoMenuItem_Click);
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(toolbarLanguageButton);
            panelEditor.AttachLanguageSession(editorLanguageSession);
            observedLanguagePackage = editorLanguageSession.ActivePackage;
            editorLanguageSession.ActiveLanguageChanged += editorLanguageSession_ActiveLanguageChanged;
            panelEditor.LanguageSessionChanged += panelEditor_LanguageSessionChanged;
        }

        private void panelEditor_LanguageSessionChanged(object sender, EventArgs e)
        {
            IDdfLanguagePackageSession replacement = panelEditor.LanguageSession;
            if (replacement == null || ReferenceEquals(replacement, editorLanguageSession)) return;
            DdfLanguagePackage previousPackage = observedLanguagePackage;
            if (editorLanguageSession != null)
                editorLanguageSession.ActiveLanguageChanged -= editorLanguageSession_ActiveLanguageChanged;
            editorLanguageSession = replacement;
            observedLanguagePackage = replacement.ActivePackage;
            replacement.ActiveLanguageChanged += editorLanguageSession_ActiveLanguageChanged;
            applyLanguagePackageChange(previousPackage, replacement.ActivePackage, null, false);
        }

        private void applyActiveLanguagePackageStatus()
        {
            DdfLanguagePackage package = editorLanguageSession.ActivePackage;
            string origin = package.Origin == DdfLanguagePackageOrigin.Directory
                ? "cartella"
                : package.Origin == DdfLanguagePackageOrigin.Archive ? "archivio" : "incorporato";

            statusEncodingLabel.Text = "UTF-8 · " + package.Name + " " + package.LanguageVersion + " · " + origin;
            statusEncodingLabel.ToolTipText =
                "Pacchetto linguaggio: " + package.Id + " " + package.PackageVersion + Environment.NewLine +
                "Origine: " + origin + Environment.NewLine +
                "Sorgente: " + package.Source;

            if (editorLanguageSession.Diagnostics.Count > 0)
            {
                statusEncodingLabel.ToolTipText += Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, editorLanguageSession.Diagnostics.Select(item => item.ToString()));
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
            {
                languagePackageInfoMenuItem.Text = "&Linguaggio";
                languagePackageInfoMenuItem.ToolTipText = "Pacchetto attivo: " +
                    package.Name + " " + package.LanguageVersion;
            }
            if (toolbarLanguageButton != null)
            {
                toolbarLanguageButton.ToolTipText = "Linguaggio attivo: " + package.Name + " " + package.LanguageVersion;
                toolbarLanguageButton.AccessibleName = toolbarLanguageButton.ToolTipText;
            }
            if (toolbarLanguageStudioButton != null)
            {
                toolbarLanguageStudioButton.ToolTipText =
                    "Package Studio — modifica una copia di " + package.Name;
                toolbarLanguageStudioButton.AccessibleName =
                    toolbarLanguageStudioButton.ToolTipText;
            }
            flowchartView?.ApplyPresentation(package.Language.Intellisense.Flowchart);
        }

        private void useEmbeddedLanguageMenuItem_Click(object sender, EventArgs e)
        {
            cancelBackgroundAnalysis();
            applyLanguagePackageResult(DdfLanguagePackageLoader.LoadEmbeddedDefault(), string.Empty);
        }

        private void manageLanguagePackagesMenuItem_Click(object sender, EventArgs e)
        {
            if (languagePackageManagerForm == null || languagePackageManagerForm.IsDisposed)
            {
                var store = new DdfLanguagePackageStore(AppSettingsStore.GetLanguagePackagesDirectory());
                languagePackageManagerForm = new LanguagePackageManagerForm(store,
                    activateLanguagePackage,
                    () => useEmbeddedLanguageMenuItem_Click(this, EventArgs.Empty),
                    showLanguagePlayground,
                    () => editorLanguageSession.ActivePackage);
                languagePackageManagerForm.FormClosed += (closedSender, closedArgs) => languagePackageManagerForm = null;
                languagePackageManagerForm.Show(this);
                return;
            }
            languagePackageManagerForm.Activate();
        }

        private void validateLanguagePackageMenuItem_Click(object sender, EventArgs e)
        {
            DdfLanguagePackage active = editorLanguageSession.ActivePackage;
            DdfLanguagePackageLoadResult result = active.Origin == DdfLanguagePackageOrigin.Embedded
                ? DdfLanguagePackageLoader.LoadEmbeddedDefault()
                : DdfLanguagePackageLoader.Load(active.Source);
            string text = result.Success
                ? "Struttura e test dichiarativi completati correttamente per " + active.Name + " " + active.PackageVersion + "."
                : string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString()));
            MessageBox.Show(this, text, "Convalida pacchetto linguaggio", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void languagePlaygroundMenuItem_Click(object sender, EventArgs e) =>
            showLanguagePlayground(editorLanguageSession.ActivePackage);

        private void languagePackageStudioMenuItem_Click(object sender, EventArgs e)
        {
            if (embeddedHostMode || executionRunning || compiledBuildRunning)
                return;
            if (languagePackageStudioForm == null || languagePackageStudioForm.IsDisposed)
            {
                languagePackageStudioForm = DdfLanguagePackageStudio.CreateForm(
                    editorLanguageSession.ActivePackage);
                languagePackageStudioForm.FormClosed += (closedSender, closedArgs) =>
                    languagePackageStudioForm = null;
                languagePackageStudioForm.Show(this);
                return;
            }
            languagePackageStudioForm.Activate();
        }

        private void showLanguagePlayground(DdfLanguagePackage package)
        {
            languagePlaygroundForm?.Close();
            languagePlaygroundForm = new LanguagePlaygroundForm(package);
            languagePlaygroundForm.FormClosed += (closedSender, closedArgs) => languagePlaygroundForm = null;
            languagePlaygroundForm.Show(this);
        }

        private void activateLanguagePackage(string path)
        {
            cancelBackgroundAnalysis();
            applyLanguagePackageResult(DdfLanguagePackageLoader.Load(path), path);
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

            DdfLanguageMigrationPlan migration =
                DdfLanguageMigrationAnalyzer.Analyze(
                    documentViews.Values.Select(view =>
                        new DdfLanguageMigrationDocument(
                            string.IsNullOrWhiteSpace(view.Buffer.FilePath)
                                ? view.Buffer.DisplayName
                                : view.Buffer.FilePath,
                            view.Editor.Text)),
                    editorLanguageSession.ActivePackage, result.Package);
            if (!migration.CanActivate)
            {
                string[] blocking = migration.Diagnostics
                    .Where(item => item.Severity ==
                        DdfLanguageMigrationSeverity.Error)
                    .Select(item => item.ToString()).ToArray();
                string details = string.Join(Environment.NewLine,
                    blocking.Take(20));
                if (blocking.Length > 20)
                    details += Environment.NewLine + "... e altri " +
                        (blocking.Length - 20) + " problemi.";
                languagePackageActivationDiagnostics = result.Diagnostics;
                applyActiveLanguagePackageStatus();
                appendOutput("Cambio linguaggio annullato: " +
                    blocking.Length + " collisioni o incompatibilità da correggere.");
                MessageBox.Show(this,
                    "Il pacchetto non è stato attivato perché alcuni identificatori " +
                    "diventerebbero parole riservate oppure la semantica dei profili " +
                    "non coincide. Nessun documento è stato modificato.\n\n" +
                    details,
                    "Migrazione linguaggio richiesta", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            languagePackageActivationDiagnostics = result.Diagnostics;
            pendingLanguagePreferencePath = persistedPath ?? string.Empty;
            persistPendingLanguagePreference = true;
            editorLanguageSession.UsePackage(result.Package, result.Diagnostics);
        }

        private void editorLanguageSession_ActiveLanguageChanged(object sender,
            DdfLanguagePackageChangedEventArgs e)
        {
            DdfLanguagePackage previousPackage = observedLanguagePackage;
            observedLanguagePackage = e.Package;
            string persistedPath = pendingLanguagePreferencePath;
            bool persistPreference = persistPendingLanguagePreference;
            pendingLanguagePreferencePath = null;
            persistPendingLanguagePreference = false;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => applyLanguagePackageChange(previousPackage, e.Package,
                    persistedPath, persistPreference)));
                return;
            }
            applyLanguagePackageChange(previousPackage, e.Package, persistedPath, persistPreference);
        }

        private void applyLanguagePackageChange(DdfLanguagePackage previousPackage,
            DdfLanguagePackage package, string persistedPath, bool persistPreference)
        {
            if (package == null || IsDisposed || Disposing) return;
            stopExecution();
            cancelBackgroundAnalysis();
            hideCompletion();
            hideSignatureHelp();
            leaveFoldedView();
            int convertedDocuments = convertOpenDocumentSyntax(previousPackage, package);
            if (persistPreference)
            {
                try
                {
                    saveLanguagePackagePathSetting(persistedPath ?? string.Empty);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    statusFileLabel.ToolTipText = "Pacchetto attivato, ma preferenza non salvata: " + exception.Message;
                }
            }

            cancelBackgroundAnalysis();
            recreateLanguageServices();
            languagePackageInfoForm?.Close();
            applyActiveLanguagePackageStatus();
            appendOutput("Linguaggio attivo: " + package.Name + " " +
                package.LanguageVersion + ". " +
                (convertedDocuments == 0
                    ? "Nessun documento richiedeva conversione."
                    : convertedDocuments + (convertedDocuments == 1
                        ? " documento convertito automaticamente."
                        : " documenti convertiti automaticamente.")) +
                (previousPackage == null ? string.Empty : " Profilo precedente: " + previousPackage.Id + "."));

            if (workspaceIndex != null) startWorkspaceLoad(workspaceIndex.RootPath);
            else
            {
                diagnosticsFormatStart = 0;
                incrementalLexer.Reset();
                applyHighlighting();
                scheduleWorkspaceSearch();
            }
        }

        private int convertOpenDocumentSyntax(
            DdfLanguagePackage sourcePackage, DdfLanguagePackage targetPackage)
        {
            if (sourcePackage == null || targetPackage == null ||
                string.Equals(sourcePackage.Id, targetPackage.Id, StringComparison.Ordinal)) return 0;
            int converted = 0;
            foreach (DocumentView view in documentViews.Values.ToList())
            {
                string source = view.Editor.Text;
                DdfSyntaxProfileConversionResult result = DdfSyntaxProfileConverter.Convert(
                    source, sourcePackage.Language, targetPackage.Language);
                if (!result.Changed) continue;
                EditorEdit edit = result.CreateEdit(source,
                    view.Editor.SelectionStart, view.Editor.SelectionLength);
                view.Editor.Select(edit.Start, edit.Length);
                view.Editor.SelectedText = edit.Replacement;
                view.Editor.Select(edit.SelectionStart, edit.SelectionLength);
                converted++;
            }
            return converted;
        }

        private bool isActiveLanguageExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return editorLanguageSession.ActivePackage.FileExtensions.Any(item =>
                string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private string getDefaultDocumentExtension()
        {
            return editorLanguageSession.ActivePackage.FileExtensions.First().TrimStart('.');
        }

        private string getDocumentFilter()
        {
            DdfLanguagePackage package = editorLanguageSession.ActivePackage;
            string patterns = string.Join(";", package.FileExtensions.Select(item => "*" + item));
            string name = (package.Name ?? "Linguaggio").Replace("|", string.Empty);
            return "Sorgenti " + name + " (" + patterns + ")|" + patterns + "|Tutti i file (*.*)|*.*";
        }

        private void languagePackageInfoMenuItem_Click(object sender, EventArgs e)
        {
            if (languagePackageInfoForm == null || languagePackageInfoForm.IsDisposed)
            {
                // The active-package dialog must describe only the package that is actually in use.
                // Diagnostics from a rejected activation remain available in the status tooltip and
                // in the rejection dialog, but mixing them here made a healthy package look invalid.
                languagePackageInfoForm = new LanguagePackageInfoForm(editorLanguageSession.ActivePackage,
                    editorLanguageSession.Diagnostics);
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
