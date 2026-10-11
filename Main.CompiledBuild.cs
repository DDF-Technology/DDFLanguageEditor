using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private const string StandaloneBuildTarget = "win-x64";
        private IDdfEditorCompiledBuildService standaloneCompiledBuildService;
        private DdfEditorBuildConfiguration standaloneBuildConfiguration =
            DdfEditorBuildConfiguration.Debug;
        private ToolStripMenuItem compilationMenuItem;
        private ToolStripMenuItem buildConfigurationMenuItem;
        private ToolStripMenuItem compileAndRunMenuItem;
        private ToolStripMenuItem publishExecutableMenuItem;
        private ToolStripMenuItem openCompiledOutputMenuItem;
        private ToolStripMenuItem runLastBuildMenuItem;
        private ToolStripMenuItem debugBuildConfigurationMenuItem;
        private ToolStripMenuItem releaseBuildConfigurationMenuItem;
        private ToolStripMenuItem buildTargetMenuItem;
        private bool compiledBuildRunning;
        private string compiledBuildProjectRoot = string.Empty;
        private string compiledBuildExecutableName = "DdfProgram";
        private string lastCompiledOutputDirectory = string.Empty;
        private CancellationTokenSource compiledBuildCancellation;
        private Func<PublishExecutableDialog, DialogResult>
            showPublishExecutableDialog;
        private Action<string> openCompiledOutputDirectory;

        private void initializeStandaloneBuildCommands()
        {
            compilationMenuItem = new ToolStripMenuItem("&Compilazione")
            {
                Name = "compilationMenuItem"
            };
            buildConfigurationMenuItem = new ToolStripMenuItem("&Configurazione")
            {
                Name = "buildConfigurationMenuItem"
            };
            debugBuildConfigurationMenuItem = new ToolStripMenuItem("Debug")
            {
                Name = "debugBuildConfigurationMenuItem",
                CheckOnClick = true
            };
            releaseBuildConfigurationMenuItem = new ToolStripMenuItem("Release")
            {
                Name = "releaseBuildConfigurationMenuItem",
                CheckOnClick = true
            };
            debugBuildConfigurationMenuItem.Click += (sender, args) =>
                selectStandaloneBuildConfiguration(
                    DdfEditorBuildConfiguration.Debug);
            releaseBuildConfigurationMenuItem.Click += (sender, args) =>
                selectStandaloneBuildConfiguration(
                    DdfEditorBuildConfiguration.Release);
            buildConfigurationMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                debugBuildConfigurationMenuItem,
                releaseBuildConfigurationMenuItem
            });
            buildTargetMenuItem = new ToolStripMenuItem(
                "Target: " + StandaloneBuildTarget)
            {
                Name = "buildTargetMenuItem",
                Enabled = false
            };
            runLastBuildMenuItem = new ToolStripMenuItem(
                "Esegui &ultima build")
            {
                Name = "runLastBuildMenuItem",
                ShortcutKeys = Keys.Control | Keys.Alt | Keys.F5,
                Enabled = false
            };
            runLastBuildMenuItem.Click += runLastBuildMenuItem_Click;
            compileAndRunMenuItem = new ToolStripMenuItem(
                "Compila ed &esegui")
            {
                Name = "compileAndRunMenuItem",
                ShortcutKeys = Keys.Control | Keys.Shift | Keys.F5
            };
            compileAndRunMenuItem.Click += compileAndRunMenuItem_Click;
            publishExecutableMenuItem = new ToolStripMenuItem(
                "&Pubblica EXE...")
            {
                Name = "publishExecutableMenuItem"
            };
            publishExecutableMenuItem.Click +=
                publishExecutableMenuItem_Click;
            openCompiledOutputMenuItem = new ToolStripMenuItem(
                "Apri cartella &output")
            {
                Name = "openCompiledOutputMenuItem",
                Enabled = false
            };
            openCompiledOutputMenuItem.Click +=
                openCompiledOutputMenuItem_Click;
            showPublishExecutableDialog = dialog => dialog.ShowDialog(this);
            openCompiledOutputDirectory = path => Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });

            runMenuItem.DropDownItems.Remove(validateProjectMenuItem);
            runMenuItem.DropDownItems.Remove(buildProjectMenuItem);
            buildProjectMenuItem.Text = "&Esegui build";
            compilationMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                validateProjectMenuItem,
                buildProjectMenuItem,
                compileAndRunMenuItem,
                runLastBuildMenuItem,
                publishExecutableMenuItem,
                openCompiledOutputMenuItem,
                new ToolStripSeparator(),
                buildConfigurationMenuItem,
                buildTargetMenuItem
            });
            int runIndex = menuStripMain.Items.IndexOf(runMenuItem);
            menuStripMain.Items.Insert(Math.Max(0, runIndex),
                compilationMenuItem);

            string persisted = (AppSettingsStore.LoadBuildConfiguration() ??
                string.Empty).Trim();
            selectStandaloneBuildConfiguration(string.Equals(persisted,
                    nameof(DdfEditorBuildConfiguration.Release),
                    StringComparison.OrdinalIgnoreCase)
                ? DdfEditorBuildConfiguration.Release
                : DdfEditorBuildConfiguration.Debug, false);
            if (toolbarBuildButton != null)
            {
                toolbarBuildButton.ToolTipText =
                    "Esegui build (Ctrl+Shift+B)";
                toolbarBuildButton.AccessibleName =
                    toolbarBuildButton.ToolTipText;
            }
        }

        private void selectStandaloneBuildConfiguration(
            DdfEditorBuildConfiguration configuration, bool persist = true)
        {
            standaloneBuildConfiguration = configuration;
            if (debugBuildConfigurationMenuItem != null)
                debugBuildConfigurationMenuItem.Checked =
                    configuration == DdfEditorBuildConfiguration.Debug;
            if (releaseBuildConfigurationMenuItem != null)
                releaseBuildConfigurationMenuItem.Checked =
                    configuration == DdfEditorBuildConfiguration.Release;
            if (buildConfigurationMenuItem != null)
                buildConfigurationMenuItem.Text = "&Configurazione: " +
                    configuration;
            if (persist)
                AppSettingsStore.SaveBuildConfiguration(
                    configuration.ToString());
        }

        public void AttachStandaloneCompiledBuildService(
            IDdfEditorCompiledBuildService service)
        {
            if (embeddedHostMode) throw new InvalidOperationException(
                "La compilazione non è disponibile nell'editor embedded.");
            standaloneCompiledBuildService = service ?? throw new
                ArgumentNullException(nameof(service));
            updateExecutionCommands(executionRunning);
        }

        private async void runLastBuildMenuItem_Click(object sender,
            EventArgs e)
        {
            if (embeddedHostMode || compiledBuildRunning ||
                standaloneCompiledBuildService?.HasRunnableBuild != true)
                return;

            compiledBuildRunning = true;
            compiledBuildCancellation = new CancellationTokenSource();
            updateExecutionCommands(executionRunning);
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("Esecuzione ultima build: " +
                standaloneCompiledBuildService.LastArtifactDirectory);
            if (openDocuments.Documents.Any(document => document.IsDirty))
                appendOutput("Nota: sono presenti modifiche non compilate; " +
                    "viene eseguita l'ultima build valida.");
            try
            {
                DdfEditorCompiledRunResult result = await
                    standaloneCompiledBuildService.RunLastBuildAsync(
                        TimeSpan.FromMinutes(5),
                        compiledBuildCancellation.Token);
                showCompiledRunResult(result, "Esecuzione ultima build");
            }
            catch (Exception exception)
            {
                showCompiledRunResult(new DdfEditorCompiledRunResult(
                    standaloneCompiledBuildService.LastArtifactDirectory,
                    null, 0, false, null, new[]
                    {
                        new DdfEditorCompiledBuildIssue("DDFC493",
                            "Esecuzione dell'ultima build fallita: " +
                            exception.Message)
                    }), "Esecuzione ultima build");
            }
            finally
            {
                compiledBuildCancellation?.Dispose();
                compiledBuildCancellation = null;
                compiledBuildRunning = false;
                updateExecutionCommands(executionRunning);
            }
        }

        private void showCompiledRunResult(DdfEditorCompiledRunResult result,
            string operation)
        {
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput(operation + ": " +
                result.ArtifactDirectory);
            foreach (string line in result.Output) appendOutput(line);
            if (!result.Success)
            {
                appendOutputBatch(result.Issues.Select(issue =>
                    new OutputLine(issue.ToString())).ToList());
                appendOutput("Esecuzione non completata.");
                return;
            }
            appendOutput("Valore restituito: " +
                (string.IsNullOrWhiteSpace(result.ResultJson)
                    ? "null"
                    : result.ResultJson));
            appendOutput("Esecuzione completata: " + result.Instructions +
                " passi runtime" + (result.Ended ? " (end)" : string.Empty) +
                ".");
        }

        private void compileAndRunMenuItem_Click(object sender, EventArgs e) =>
            _ = executeCompiledBuildAsync(true);

        private async void executeCompiledBuild() =>
            await executeCompiledBuildAsync(false);

        private async Task<bool> executeCompiledBuildAsync(bool runAfterBuild)
        {
            if (embeddedHostMode || compiledBuildRunning) return false;
            if (standaloneCompiledBuildService == null)
            {
                DdfEditorPreparationSnapshot fallback =
                    panelEditor.PrepareScript();
                showBuildResult("Preparazione build", fallback.Result);
                if (fallback.Result.Success)
                    appendOutput("Backend Roslyn non collegato: non sono stati " +
                        "prodotti artefatti compilati.");
                return false;
            }

            // Gli artefatti persistenti non vengono mai prodotti da buffer
            // impliciti: l'utente salva (o annulla) prima della preparazione.
            if (openDocuments.Documents.Any(document => document.IsDirty) &&
                !saveAllDocuments()) return false;
            if (!tryCreateBuildContext(out ProjectBuildContext context,
                    out IReadOnlyList<DdfBuildIssue> setupIssues))
            {
                showBuildIssues("Build", setupIssues);
                return false;
            }
            if (context.IsUntitled)
            {
                showBuildIssues("Build", new[]
                {
                    DdfBuildIssue.Configuration("DDFB008",
                        "Salvare il documento o aprire una cartella progetto " +
                        "prima di eseguire la build.")
                });
                return false;
            }

            DdfEditorPreparationSnapshot preparation =
                panelEditor.PrepareScript();
            if (!preparation.Result.Success)
            {
                showBuildResult("Build", preparation.Result);
                return false;
            }

            string outputRoot = Path.Combine(context.Root, "bin",
                standaloneBuildConfiguration.ToString(),
                StandaloneBuildTarget);
            compiledBuildProjectRoot = context.Root;
            compiledBuildExecutableName = createExecutableName(
                context.Manifest.Module.Id);
            var request = new DdfEditorCompiledBuildRequest(
                preparation.Result, context.Package, context.Root, outputRoot,
                standaloneBuildConfiguration, StandaloneBuildTarget);

            compiledBuildRunning = true;
            updateExecutionCommands(executionRunning);
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("ddf build \"" + context.Root + "\" --output \"" +
                outputRoot + "\"");
            appendOutput("Configurazione: " + standaloneBuildConfiguration +
                " — target: " + StandaloneBuildTarget + ".");
            appendOutput("Build Roslyn in corso...");
            try
            {
                DdfEditorCompiledBuildResult result = await Task.Run(() =>
                    standaloneCompiledBuildService.Build(request));
                showCompiledBuildResult(result);
                if (!result.Success) return false;
                if (!runAfterBuild) return true;

                compiledBuildCancellation = new CancellationTokenSource();
                updateExecutionCommands(executionRunning);
                appendOutput("Avvio dell'artefatto appena compilato nel " +
                    "worker isolato...");
                DdfEditorCompiledRunResult runResult = await
                    standaloneCompiledBuildService.RunLastBuildAsync(
                        TimeSpan.FromMinutes(5),
                        compiledBuildCancellation.Token);
                showCompiledRunResult(runResult, "Compila ed esegui");
                return runResult.Success;
            }
            catch (Exception exception)
            {
                if (runAfterBuild &&
                    standaloneCompiledBuildService.HasRunnableBuild &&
                    compiledBuildCancellation != null)
                {
                    showCompiledRunResult(new DdfEditorCompiledRunResult(
                        standaloneCompiledBuildService.LastArtifactDirectory,
                        null, 0, false, null, new[]
                        {
                            new DdfEditorCompiledBuildIssue("DDFC493",
                                "Esecuzione della build fallita: " +
                                exception.Message)
                        }), "Compila ed esegui");
                }
                else
                {
                    showCompiledBuildResult(new DdfEditorCompiledBuildResult(
                        outputRoot, null, false, new[]
                        {
                            new DdfEditorCompiledBuildIssue("DDFC490",
                                "Build standalone fallita: " +
                                exception.Message)
                        }));
                }
                return false;
            }
            finally
            {
                compiledBuildCancellation?.Dispose();
                compiledBuildCancellation = null;
                compiledBuildRunning = false;
                updateExecutionCommands(executionRunning);
            }
        }

        private async void publishExecutableMenuItem_Click(object sender,
            EventArgs e)
        {
            if (embeddedHostMode || compiledBuildRunning ||
                standaloneCompiledBuildService == null) return;
            if (!await executeCompiledBuildAsync(false)) return;

            string publishParent = Path.Combine(compiledBuildProjectRoot,
                "bin", standaloneBuildConfiguration.ToString(),
                StandaloneBuildTarget, "publish");
            try
            {
                Directory.CreateDirectory(publishParent);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                showCompiledPublishResult(new DdfEditorCompiledPublishResult(
                    publishParent, null, null, new[]
                    {
                        new DdfEditorCompiledBuildIssue("DDFC495",
                            "Impossibile preparare la cartella di " +
                            "pubblicazione: " + exception.Message)
                    }));
                return;
            }

            string persistedIcon = AppSettingsStore.LoadPublishIconPath();
            if (persistedIcon.Length != 0 && !File.Exists(persistedIcon))
                persistedIcon = string.Empty;
            using var dialog = new PublishExecutableDialog(Path.Combine(
                publishParent, compiledBuildExecutableName + ".exe"),
                persistedIcon);
            if (showPublishExecutableDialog(dialog) != DialogResult.OK) return;
            string executableName = createExecutableName(
                Path.GetFileNameWithoutExtension(dialog.ExecutablePath));
            string destination = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(
                    dialog.ExecutablePath)) ??
                    publishParent,
                executableName);
            AppSettingsStore.SavePublishIconPath(dialog.IconPath);
            var request = new DdfEditorCompiledPublishRequest(destination,
                executableName, StandaloneBuildTarget, dialog.IconPath);

            compiledBuildRunning = true;
            updateExecutionCommands(executionRunning);
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("ddf package \"" + compiledBuildProjectRoot +
                "\" --rid " + StandaloneBuildTarget + " --output \"" +
                destination + "\" --name \"" + executableName + "\"");
            appendOutput("Pubblicazione self-contained in corso...");
            appendOutput(dialog.IconPath.Length == 0
                ? "Icona: DDF predefinita."
                : "Icona personalizzata: " + dialog.IconPath);
            try
            {
                DdfEditorCompiledPublishResult result = await Task.Factory
                    .StartNew(() => standaloneCompiledBuildService
                            .PublishLastBuild(request), CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default).ConfigureAwait(false);
                invokeCompiledBuildUi(() => showCompiledPublishResult(result));
            }
            catch (Exception exception)
            {
                invokeCompiledBuildUi(() => showCompiledPublishResult(
                    new DdfEditorCompiledPublishResult(destination, null,
                        null, new[]
                        {
                            new DdfEditorCompiledBuildIssue("DDFC495",
                                "Pubblicazione standalone fallita: " +
                                exception.Message)
                        })));
            }
            finally
            {
                invokeCompiledBuildUi(() =>
                {
                    compiledBuildRunning = false;
                    updateExecutionCommands(executionRunning);
                });
            }
        }

        private void invokeCompiledBuildUi(Action action)
        {
            if (action == null || IsDisposed || Disposing) return;
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private void showCompiledPublishResult(
            DdfEditorCompiledPublishResult result)
        {
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            if (!result.Success)
            {
                appendOutputBatch(result.Issues.Select(issue =>
                    new OutputLine(issue.ToString())).ToList());
                appendOutput("Pubblicazione non completata.");
                return;
            }
            appendOutput("Pubblicazione EXE completata.");
            appendOutput("Package: " + result.OutputRoot);
            appendOutput("Eseguibile: " + result.ExecutablePath);
            appendOutput("File pubblicati: " + result.PublishedFiles.Count +
                ".");
            rememberCompiledOutputDirectory(result.OutputRoot);
        }

        private void openCompiledOutputMenuItem_Click(object sender,
            EventArgs e)
        {
            string path = lastCompiledOutputDirectory;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                appendOutput("Cartella output non disponibile: eseguire una " +
                    "build o una pubblicazione valida.");
                lastCompiledOutputDirectory = string.Empty;
                updateExecutionCommands(executionRunning);
                return;
            }
            try
            {
                openCompiledOutputDirectory(path);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception)
            {
                appendOutput("Impossibile aprire la cartella output: " +
                    exception.Message);
            }
        }

        private void rememberCompiledOutputDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            lastCompiledOutputDirectory = Path.GetFullPath(path);
            updateExecutionCommands(executionRunning);
        }

        private static string createExecutableName(string value)
        {
            string name = new string((value ?? string.Empty).Select(character =>
                char.IsLetterOrDigit(character) || character == '.' ||
                character == '_' || character == '-'
                    ? character
                    : '-').ToArray()).Trim('-', '.', '_');
            if (name.Length == 0 || !char.IsLetter(name[0]))
                name = "DdfProgram-" + name;
            return name.Length <= 64 ? name : name.Substring(0, 64);
        }

        private void showCompiledBuildResult(
            DdfEditorCompiledBuildResult result)
        {
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("ddf build \"" + compiledBuildProjectRoot +
                "\" --output \"" + result.OutputRoot + "\"");
            appendOutput("Configurazione: " + standaloneBuildConfiguration +
                " — target: " + StandaloneBuildTarget + ".");
            appendOutput("Directory configurazione: " + result.OutputRoot);
            if (result.Success)
            {
                appendOutput("Build completata (" +
                    (result.CacheHit ? "cache verificata" : "compilata") +
                    ").");
                appendOutput("Artefatti: " + result.ArtifactDirectory);
                if (!string.IsNullOrWhiteSpace(result.CacheRoot))
                    appendOutput("Cache tecnica: " + result.CacheRoot);
                rememberCompiledOutputDirectory(result.ArtifactDirectory);
                return;
            }

            appendOutputBatch(result.Issues.Select(createCompiledIssueLine)
                .ToList());
            appendOutput("Build non completata: correggere gli errori indicati.");
            formatNavigableOutput();
        }

        private OutputLine createCompiledIssueLine(
            DdfEditorCompiledBuildIssue issue)
        {
            string text = issue.ToString();
            if (string.IsNullOrWhiteSpace(issue.SourcePath) ||
                !issue.Line.HasValue) return new OutputLine(text);
            string path = Path.IsPathRooted(issue.SourcePath)
                ? issue.SourcePath
                : Path.GetFullPath(Path.Combine(compiledBuildProjectRoot,
                    issue.SourcePath));
            string source = openDocuments.Documents.FirstOrDefault(document =>
                document.Session.HasPath && string.Equals(
                    Path.GetFullPath(document.Session.CurrentPath), path,
                    StringComparison.OrdinalIgnoreCase))?.Source;
            if (source == null && File.Exists(path)) source = File.ReadAllText(path);
            int start = lineColumnToOffset(source ?? string.Empty,
                issue.Line.Value, issue.Column ?? 1);
            return new OutputLine(text, path, start, 1);
        }

        private static int lineColumnToOffset(string source, int line,
            int column)
        {
            int currentLine = 1;
            int offset = 0;
            while (currentLine < Math.Max(1, line) && offset < source.Length)
            {
                if (source[offset++] == '\n') currentLine++;
            }
            return Math.Min(source.Length, offset + Math.Max(0, column - 1));
        }
    }
}
