using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private DdfEditorExecutionService standaloneExecutionService;
        private IDdfEditorDebugService standaloneDebugService;
        private readonly List<OutputNavigationTarget> outputNavigationTargets = new List<OutputNavigationTarget>();
        private readonly DdfBuildService projectBuildService = new DdfBuildService();
        private string runtimeDocumentId;
        private string runtimeDocumentPath;
        private IReadOnlyList<DdfIrModule> debugRuntimeModules = Array.Empty<DdfIrModule>();
        private bool debugUiLocked;
        private bool executionRunning;
        private string debugBaseWindowTitle;
        private string embeddedEntryPoint;
        private IReadOnlyDictionary<string, object> embeddedEntryArguments;

        private static readonly HashSet<string> DebugUnsafeMenuCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "newMenuItem", "openMenuItem", "openWorkspaceMenuItem", "closeWorkspaceMenuItem",
            "saveMenuItem", "saveAsMenuItem", "saveAllMenuItem", "closeDocumentMenuItem",
            "recentMenuItem", "recentWorkspacesMenuItem", "undoMenuItem", "redoMenuItem",
            "cutMenuItem", "pasteMenuItem", "replaceMenuItem", "workspaceReplaceMenuItem",
            "completionMenuItem", "formatDocumentMenuItem", "renameSymbolMenuItem",
            "toggleLineCommentMenuItem", "duplicateLinesMenuItem", "moveLinesUpMenuItem",
            "moveLinesDownMenuItem", "deleteLinesMenuItem", "quickFixMenuItem",
            "validateProjectMenuItem", "buildProjectMenuItem", "runWithoutDebugMenuItem",
            "compileAndRunMenuItem", "runLastBuildMenuItem",
            "publishExecutableMenuItem",
            "settingsMenuItem", "manageLanguagePackagesMenuItem", "validateLanguagePackageMenuItem",
            "languagePlaygroundMenuItem", "manageModulePackagesMenuItem"
        };

        private static readonly HashSet<string> DebugUnsafeToolbarCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "toolbarNewButton", "toolbarOpenButton", "toolbarSaveButton", "toolbarSaveAllButton",
            "toolbarCloseDocumentButton", "toolbarUndoButton", "toolbarRedoButton", "toolbarCutButton",
            "toolbarPasteButton", "toolbarCommentButton", "toolbarDuplicateLinesButton",
            "toolbarMoveLinesUpButton", "toolbarMoveLinesDownButton", "toolbarDeleteLinesButton",
            "toolbarCompletionButton", "toolbarQuickFixButton", "toolbarFormatButton",
            "toolbarValidateButton", "toolbarBuildButton", "toolbarRunWithoutDebugButton",
            "toolbarCompileAndRunButton", "toolbarRunLastBuildButton",
            "toolbarPublishExecutableButton",
            "toolbarSettingsButton", "toolbarLanguageManagerButton", "toolbarLanguagePlaygroundButton",
            "toolbarLanguageButton", "toolbarModuleManagerButton"
        };

        private void runMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            updateExecutionCommands(executionRunning);
        }

        private void runProgramMenuItem_Click(object sender, EventArgs e)
        {
            if (embeddedHostMode && !executionRunning) return;
            if (executionRunning)
            {
                if (panelEditor.DebugService?.IsPaused == true)
                {
                    clearDebugExecutionLine();
                    appendOutput("Esecuzione ripresa.");
                    panelEditor.DebugService.Continue();
                    updateExecutionCommands(true);
                }
                return;
            }

            executeProject(true);
        }

        private void runWithoutDebugMenuItem_Click(object sender, EventArgs e)
        {
            if (embeddedHostMode) return;
            if (!executionRunning) executeProject(false);
        }

        private void validateProjectMenuItem_Click(object sender, EventArgs e) => validateOrBuildProject(false);

        private void buildProjectMenuItem_Click(object sender, EventArgs e) =>
            executeCompiledBuild();

        private void validateOrBuildProject(bool writeArtifact)
        {
            DdfEditorPreparationSnapshot snapshot = writeArtifact
                ? panelEditor.PrepareScript()
                : panelEditor.ValidateScript();
            showBuildResult(writeArtifact ? "Compilazione" : "Convalida",
                snapshot.Result);
        }

        private DdfBuildResult executeStandalonePreparation(
            DdfEditorPreparationMode mode,
            DdfEditorPreparationRequest request,
            IReadOnlyList<DdfEditorPreparationDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfHostCatalogSnapshot hostCatalog)
        {
            if (!tryCreateBuildContext(out ProjectBuildContext context,
                out IReadOnlyList<DdfBuildIssue> setupIssues))
                return DdfBuildResult.Failure(setupIssues);
            if (mode == DdfEditorPreparationMode.PrepareScript && context.IsUntitled)
                return DdfBuildResult.Failure(DdfBuildIssue.Configuration("DDFB008",
                    "Salvare il documento o aprire una cartella progetto prima di generare un artefatto."));

            applyEditorPreparationRequest(context, request);
            try
            {
                return mode == DdfEditorPreparationMode.PrepareScript
                    ? projectBuildService.Build(context.Root, context.Manifest,
                        context.Package, context.Buffers, hostCatalog)
                    : projectBuildService.Validate(context.Root, context.Manifest,
                        context.Package, context.Buffers, hostCatalog);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                return DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                    "DDFB999", exception.Message));
            }
        }

        private void applyEditorPreparationRequest(ProjectBuildContext context,
            DdfEditorPreparationRequest request)
        {
            if (request == null) return;
            if (!string.IsNullOrWhiteSpace(request.EntryPoint))
                context.Manifest.EntryPoint = request.EntryPoint.Trim();
            if (request.MaxInstructions > 0)
                context.Manifest.Options.MaxInstructions = request.MaxInstructions;
            if (string.IsNullOrWhiteSpace(request.EntryDocumentId)) return;

            OpenDocumentBuffer entry = openDocuments.Documents.FirstOrDefault(item =>
                string.Equals(item.Id, request.EntryDocumentId,
                    StringComparison.Ordinal));
            if (entry == null || !entry.Session.HasPath ||
                !isPathInside(context.Root, entry.Session.CurrentPath)) return;
            context.Manifest.EntrySource = Path.GetRelativePath(context.Root,
                entry.Session.CurrentPath).Replace('\\', '/');
        }

        private void executeProject(bool debug)
        {
            EmbeddedExecutionPreparing?.Invoke(this, EventArgs.Empty);
            string requestedEntryPoint = string.IsNullOrWhiteSpace(embeddedEntryPoint)
                ? null : embeddedEntryPoint;
            IReadOnlyDictionary<string, object> requestedEntryArguments =
                embeddedEntryArguments;
            clearDebugExecutionLine();
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            var request = new DdfEditorExecutionRequest(
                entryPoint: requestedEntryPoint,
                input: () => requestRuntimeInput == null ? string.Empty : requestRuntimeInput(),
                mode: DdfEditorExecutionMode.HostEffects,
                entryArguments: requestedEntryArguments);
            try
            {
                if (debug)
                {
                    synchronizeRuntimeBreakpoints(openDocuments.ActiveDocument);
                    synchronizeRuntimeWatchExpressions();
                    _ = panelEditor.DebugService.RunAsync(request);
                }
                else
                    _ = panelEditor.ExecutionService.RunAsync(request);
                string runtimeEntryPoint = requestedEntryPoint ??
                    activeRuntimeArtifact?.EntryPoint ?? "main";
                string buildIdentity = activeRuntimeArtifact == null ? string.Empty :
                    " [" + activeRuntimeArtifact.Backend + ", " +
                    activeRuntimeArtifact.Fingerprint.Substring(0, 12) + "]";
                appendOutput(ProductInfo.DisplayName + " — " +
                    (debug ? "debug" : "esecuzione") + " di " +
                    runtimeEntryPoint + "()" + buildIdentity);
            }
            catch (Exception exception)
            {
                appendOutput("Avvio non riuscito: " + exception.Message);
                embeddedEntryPoint = null;
                embeddedEntryArguments = null;
                executionRunning = false;
                updateExecutionCommands(false);
            }
        }

        private DdfBuildArtifact activeRuntimeArtifact;

        private DdfEditorExecutionBuildContext prepareStandaloneExecution(
            DdfEditorExecutionRequest request,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package, DdfHostCatalogSnapshot hostCatalog)
        {
            activeRuntimeArtifact = null;
            debugRuntimeModules = Array.Empty<DdfIrModule>();
            if (!tryCreateBuildContext(out ProjectBuildContext context,
                out IReadOnlyList<DdfBuildIssue> setupIssues,
                preferActiveDocument: true))
                return new DdfEditorExecutionBuildContext(
                    DdfBuildResult.Failure(setupIssues), package);
            applyEditorExecutionRequest(context, request);
            DdfBuildResult build;
            try
            {
                build = projectBuildService.Validate(context.Root,
                    context.Manifest, context.Package, context.Buffers,
                    hostCatalog);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                build = DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                    "DDFB999", exception.Message));
            }

            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DdfEditorBuildDocumentSnapshot document in documents)
            {
                string path = document.FilePath;
                if (string.IsNullOrWhiteSpace(path) && context.IsUntitled)
                {
                    if (!string.Equals(document.Id,
                        openDocuments.ActiveDocument?.Id, StringComparison.Ordinal))
                        continue;
                    path = Path.Combine(context.Root,
                        context.Manifest.EntrySource ?? "untitled.ddf");
                }
                if (!string.IsNullOrWhiteSpace(path)) paths[document.Id] =
                    Path.GetFullPath(path);
            }
            activeRuntimeArtifact = build.Artifact;
            debugRuntimeModules = build.Success ? build.Artifact.RuntimeModules :
                Array.Empty<DdfIrModule>();
            if (build.Success)
            {
                runtimeDocumentPath = Path.GetFullPath(Path.Combine(context.Root,
                    build.Artifact.EntrySource.Replace('/',
                        Path.DirectorySeparatorChar)));
                runtimeDocumentId = paths.FirstOrDefault(item =>
                    string.Equals(item.Value, runtimeDocumentPath,
                        StringComparison.OrdinalIgnoreCase)).Key;
            }
            return new DdfEditorExecutionBuildContext(build, context.Package,
                paths);
        }

        public DdfEditorExecutionBuildContext PrepareStandaloneBackendExecution(
            DdfEditorExecutionRequest request,
            IReadOnlyList<DdfEditorBuildDocumentSnapshot> documents,
            DdfLanguagePackage package)
        {
            if (embeddedHostMode) throw new InvalidOperationException(
                "Il backend compilato non è disponibile nell'editor embedded.");
            return prepareStandaloneExecution(request,
                documents ?? Array.Empty<DdfEditorBuildDocumentSnapshot>(),
                package, panelEditor.HostCatalog?.Snapshot);
        }

        public void AttachStandaloneDebugService(IDdfEditorDebugService service)
        {
            if (embeddedHostMode) throw new InvalidOperationException(
                "Il debugger compilato non è disponibile nell'editor embedded.");
            if (service == null) throw new ArgumentNullException(nameof(service));
            IDdfEditorDebugService previous = standaloneDebugService;
            standaloneDebugService = service;
            panelEditor.AttachDebugService(service);
            previous?.Dispose();
        }

        private void applyEditorExecutionRequest(ProjectBuildContext context,
            DdfEditorExecutionRequest request)
        {
            if (request == null) return;
            if (!string.IsNullOrWhiteSpace(request.EntryPoint))
                context.Manifest.EntryPoint = request.EntryPoint.Trim();
            if (request.MaxInstructions > 0)
                context.Manifest.Options.MaxInstructions = request.MaxInstructions;
            if (string.IsNullOrWhiteSpace(request.EntryDocumentId)) return;
            OpenDocumentBuffer entry = openDocuments.Documents.FirstOrDefault(item =>
                string.Equals(item.Id, request.EntryDocumentId,
                    StringComparison.Ordinal));
            if (entry?.Session.HasPath == true &&
                isPathInside(context.Root, entry.Session.CurrentPath))
                context.Manifest.EntrySource = Path.GetRelativePath(context.Root,
                    entry.Session.CurrentPath).Replace('\\', '/');
        }

        private bool tryCreateBuildContext(out ProjectBuildContext context, out IReadOnlyList<DdfBuildIssue> issues,
            bool preferActiveDocument = false)
        {
            context = null;
            var setupIssues = new List<DdfBuildIssue>();
            DdfLanguagePackage package = editorLanguageSession.ActivePackage;
            OpenDocumentBuffer active = openDocuments.ActiveDocument;
            active?.UpdateSource(richTextBoxMainEditor.Text, false);
            bool isolateActiveDocument = preferActiveDocument && workspaceIndex != null && active != null &&
                (!active.Session.HasPath || !isPathInside(workspaceIndex.RootPath, active.Session.CurrentPath));
            bool untitled = (workspaceIndex == null || isolateActiveDocument) &&
                (active == null || !active.Session.HasPath);
            string root = isolateActiveDocument ? null : workspaceIndex?.RootPath;
            string entrySource = null;
            if (string.IsNullOrEmpty(root) && active != null && active.Session.HasPath)
            {
                root = Path.GetDirectoryName(active.Session.CurrentPath);
                entrySource = Path.GetFileName(active.Session.CurrentPath);
            }
            else if (string.IsNullOrEmpty(root))
            {
                root = AppContext.BaseDirectory;
                entrySource = "untitled.ddf";
            }
            // In un workspace senza manifest l'entry point viene scoperto cercando
            // l'unica main(), indipendentemente dalla scheda attiva.

            DdfProjectManifest manifest;
            string manifestPath = Path.Combine(root, DdfProjectManifest.FileName);
            if (!isolateActiveDocument && File.Exists(manifestPath))
            {
                DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
                if (!loaded.Success)
                {
                    issues = loaded.Issues;
                    return false;
                }
                manifest = loaded.Manifest;
            }
            else
            {
                manifest = DdfProjectManifest.CreateImplicit(Path.GetFileName(root), package, entrySource);
                if ((workspaceIndex == null || isolateActiveDocument) && !string.IsNullOrEmpty(entrySource))
                    manifest.Sources = new List<string> { entrySource };
            }

            var buffers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (OpenDocumentBuffer document in openDocuments.Documents.Where(item => item.Session.HasPath))
            {
                if ((!isolateActiveDocument || ReferenceEquals(document, active)) &&
                    isPathInside(root, document.Session.CurrentPath))
                    buffers[Path.GetFullPath(document.Session.CurrentPath)] = document.Source;
            }
            if (untitled && active != null) buffers[Path.Combine(root, entrySource)] = richTextBoxMainEditor.Text;
            context = new ProjectBuildContext(root, manifest, package, buffers, untitled);
            issues = setupIssues.AsReadOnly();
            return true;
        }

        private static bool isPathInside(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private void showBuildResult(string operation, DdfBuildResult result)
        {
            if (!result.Success) { showBuildIssues(operation, result.Issues); return; }
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput(operation + " completata: " + result.Artifact.ProjectName + ".");
            appendOutput("Pacchetto: " + result.Artifact.LanguageId + " " + result.Artifact.LanguagePackageVersion +
                " — backend: " + result.Artifact.Backend + ".");
            appendOutput("Fingerprint: " + result.Artifact.Fingerprint);
            appendOutput(string.IsNullOrEmpty(result.ArtifactPath)
                ? "Nessun file scritto (convalida in memoria)."
                : "Artefatto: " + result.ArtifactPath);
        }

        private void showBuildIssues(string operation, IReadOnlyList<DdfBuildIssue> issues)
        {
            clearOutput();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutputBatch((issues ?? Array.Empty<DdfBuildIssue>()).Select(issue =>
                new OutputLine(issue.ToString(), issue.DocumentPath, issue.Start, issue.Length)).ToList());
            appendOutput(operation + " non completata: correggere gli errori indicati.");
            formatNavigableOutput();
        }

        private void stopProgramMenuItem_Click(object sender, EventArgs e)
        {
            clearDebugExecutionLine();
            if (compiledBuildRunning && compiledBuildCancellation != null)
            {
                compiledBuildCancellation?.Cancel();
                appendOutput("Arresto dell'esecuzione compilata richiesto...");
                return;
            }
            if (panelEditor.DebugService?.IsActive == true)
                panelEditor.DebugService.Stop();
            else
                panelEditor.ExecutionService?.Stop();
            stopProgramMenuItem.Enabled = false;
            appendOutput("Arresto richiesto...");
            updateWatchExecutionState(true, false);
        }

        private void updateExecutionCommands(bool running)
        {
            bool paused = running && panelEditor.DebugService?.IsPaused == true;
            bool busy = running || compiledBuildRunning;
            bool compiledRunCancellable = compiledBuildRunning &&
                compiledBuildCancellation != null;
            bool runtimeControlsVisible = !embeddedHostMode || busy;
            setDebugUiLock(busy);
            updateWatchExecutionState(running, paused);
            updateVariablesExecutionState(running, paused);
            updateCallStackExecutionState(running, paused);
            runProgramMenuItem.Enabled = !compiledBuildRunning &&
                (!running || paused);
            runProgramMenuItem.Text = paused ? "&Continua" : "Avvia &debug";
            runWithoutDebugMenuItem.Enabled = !busy;
            runProgramMenuItem.Available = runtimeControlsVisible;
            runWithoutDebugMenuItem.Available = !embeddedHostMode;
            if (compilationMenuItem != null)
                compilationMenuItem.Available = !embeddedHostMode;
            validateProjectMenuItem.Enabled = !running && !compiledBuildRunning;
            buildProjectMenuItem.Enabled = !embeddedHostMode && !running &&
                !compiledBuildRunning;
            if (compileAndRunMenuItem != null)
                compileAndRunMenuItem.Enabled = !embeddedHostMode && !busy &&
                    standaloneCompiledBuildService != null;
            if (runLastBuildMenuItem != null)
                runLastBuildMenuItem.Enabled = !embeddedHostMode && !busy &&
                    standaloneCompiledBuildService?.HasRunnableBuild == true;
            if (publishExecutableMenuItem != null)
                publishExecutableMenuItem.Enabled = !embeddedHostMode &&
                    !busy && standaloneCompiledBuildService != null;
            if (openCompiledOutputMenuItem != null)
            {
                openCompiledOutputMenuItem.Available = !embeddedHostMode;
                openCompiledOutputMenuItem.Enabled = !busy &&
                    Directory.Exists(lastCompiledOutputDirectory);
            }
            if (buildConfigurationMenuItem != null)
                buildConfigurationMenuItem.Enabled = !running &&
                    !compiledBuildRunning;
            stopProgramMenuItem.Enabled = running || compiledRunCancellable;
            stopProgramMenuItem.Available = runtimeControlsVisible;
            if (toolbarValidateButton != null)
                toolbarValidateButton.Enabled = !running && !compiledBuildRunning;
            if (toolbarBuildButton != null)
                toolbarBuildButton.Enabled = !embeddedHostMode && !running &&
                    !compiledBuildRunning;
            if (toolbarCompileAndRunButton != null)
            {
                toolbarCompileAndRunButton.Available = !embeddedHostMode;
                toolbarCompileAndRunButton.Enabled = !busy &&
                    standaloneCompiledBuildService != null;
            }
            if (toolbarRunLastBuildButton != null)
            {
                toolbarRunLastBuildButton.Available = !embeddedHostMode;
                toolbarRunLastBuildButton.Enabled = !busy &&
                    standaloneCompiledBuildService?.HasRunnableBuild == true;
            }
            if (toolbarPublishExecutableButton != null)
            {
                toolbarPublishExecutableButton.Available = !embeddedHostMode;
                toolbarPublishExecutableButton.Enabled = !busy &&
                    standaloneCompiledBuildService != null;
            }
            if (toolbarOpenCompiledOutputButton != null)
            {
                toolbarOpenCompiledOutputButton.Available = !embeddedHostMode;
                toolbarOpenCompiledOutputButton.Enabled = !busy &&
                    Directory.Exists(lastCompiledOutputDirectory);
            }
            if (toolbarRunWithoutDebugButton != null) toolbarRunWithoutDebugButton.Enabled = !busy;
            if (toolbarRunWithoutDebugButton != null) toolbarRunWithoutDebugButton.Available = !embeddedHostMode;
            if (toolbarRunButton != null)
            {
                toolbarRunButton.Available = runtimeControlsVisible;
                toolbarRunButton.Enabled = !compiledBuildRunning &&
                    (!running || paused);
                toolbarRunButton.ToolTipText = paused ? "Continua (F5)" : "Avvia debug (F5)";
                toolbarRunButton.AccessibleName = toolbarRunButton.ToolTipText;
            }
            if (toolbarStopButton != null)
            {
                toolbarStopButton.Available = runtimeControlsVisible;
                toolbarStopButton.Enabled = running || compiledRunCancellable;
            }
            if (stepIntoMenuItem != null) stepIntoMenuItem.Enabled = paused;
            if (toolbarStepIntoButton != null) toolbarStepIntoButton.Enabled = paused;
            if (stepOverMenuItem != null) stepOverMenuItem.Enabled = paused;
            if (toolbarStepOverButton != null) toolbarStepOverButton.Enabled = paused;
            bool canStepOut = paused && watchSnapshot?.CallStack != null && watchSnapshot.CallStack.Count > 1;
            if (stepOutMenuItem != null) stepOutMenuItem.Enabled = canStepOut;
            if (toolbarStepOutButton != null) toolbarStepOutButton.Enabled = canStepOut;
            updateDebugStatus(running, paused);
            refreshEditorCommandCatalog();
        }

        private void setDebugUiLock(bool locked)
        {
            if (locked == debugUiLocked) return;
            debugUiLocked = locked;
            if (locked) closeModulePackageManager();
            if (locked) debugBaseWindowTitle = Text;
            foreach (ToolStripMenuItem item in enumerateMenuItems(menuStripMain.Items))
                if (DebugUnsafeMenuCommands.Contains(item.Name)) item.Enabled = !locked;
            foreach (ToolStripItem item in enumerateToolbarItems())
                if (DebugUnsafeToolbarCommands.Contains(item.Name)) item.Enabled = !locked;
            foreach (DocumentView view in documentViews.Values) view.Editor.ReadOnly = locked;
            if (documentTabs != null) documentTabs.Enabled = !locked;
            if (!locked)
            {
                updateDocumentUi();
                updateNavigationCommandState();
            }
        }

        private void applyDebugUnsafeCommandLock()
        {
            if (!debugUiLocked) return;
            foreach (ToolStripMenuItem item in enumerateMenuItems(menuStripMain.Items))
                if (DebugUnsafeMenuCommands.Contains(item.Name)) item.Enabled = false;
        }

        private void updateDebugStatus(bool running, bool paused)
        {
            if (statusDebugLabel == null) return;
            if (!debugUiLocked) { running = false; paused = false; }
            statusDebugLabel.Available = true;
            statusDebugLabel.Visible = true;
            if (!running)
            {
                statusDebugLabel.Text = "MODIFICA";
                statusDebugLabel.BackColor = AppTheme.Chrome;
                statusDebugLabel.ForeColor = AppTheme.MutedText;
                if (!string.IsNullOrEmpty(debugBaseWindowTitle)) Text = debugBaseWindowTitle;
                debugBaseWindowTitle = null;
                setToolbarsBackColor(AppTheme.Chrome);
                return;
            }
            statusDebugLabel.Text = paused ? "⏸ DEBUG IN PAUSA" : "● DEBUG IN ESECUZIONE";
            statusDebugLabel.BackColor = paused ? Color.FromArgb(255, 193, 7) : AppTheme.Run;
            statusDebugLabel.ForeColor = paused ? Color.FromArgb(32, 32, 32) : Color.White;
            setToolbarsBackColor(paused ? Color.FromArgb(255, 248, 225) : Color.FromArgb(232, 245, 233));
            Text = (debugBaseWindowTitle ?? Text) + (paused ? " — [DEBUG IN PAUSA]" : " — [DEBUG IN ESECUZIONE]");
        }

        private void finishExecution(DdfEditorExecutionCompletion completion)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<DdfEditorExecutionCompletion>(finishExecution),
                    completion);
                return;
            }
            clearDebugExecutionLine();
            var lines = new List<OutputLine>();
            if (completion == null)
                lines.Add(new OutputLine("Esecuzione terminata senza risultato."));
            else if (completion.BuildIssues.Any(item =>
                item.Severity == DdfDiagnosticSeverity.Error))
            {
                lines.AddRange(completion.BuildIssues.Select(issue =>
                    new OutputLine(issue.ToString(), issue.DocumentPath,
                        issue.Start, issue.Length)));
                lines.Add(new OutputLine(
                    "Esecuzione non avviata: correggere gli errori indicati."));
            }
            else if (!string.IsNullOrEmpty(completion.InternalError))
            {
                lines.Add(new OutputLine("Errore interno: " +
                    completion.InternalError));
            }
            else if (completion.ExecutionResult != null)
            {
                DdfExecutionResult result = completion.ExecutionResult;
                LastEmbeddedExecutionResult = result;
                foreach (DdfDiagnostic diagnostic in result.Diagnostics)
                    lines.Add(!string.IsNullOrEmpty(diagnostic.DocumentPath) && File.Exists(diagnostic.DocumentPath)
                        ? new OutputLine(diagnostic.ToString(), diagnostic.DocumentPath, diagnostic.Start, diagnostic.Length)
                        : new OutputLine(diagnostic.ToString(), diagnostic.Start, diagnostic.Length));
                if (result.StackTrace.Count > 0)
                {
                    lines.Add(new OutputLine("Stack chiamate DDF:"));
                    foreach (DdfRuntimeStackFrame frame in result.StackTrace)
                        lines.Add(!string.IsNullOrEmpty(frame.DocumentPath) && File.Exists(frame.DocumentPath)
                            ? new OutputLine("  " + frame, frame.DocumentPath, frame.Start, frame.Length)
                            : new OutputLine("  " + frame, frame.Start, frame.Length));
                }
                if (result.WasCancelled) lines.Add(new OutputLine("Esecuzione arrestata."));
                else if (result.Succeeded)
                {
                    if (result.ReturnValue != null)
                        lines.Add(new OutputLine("Valore restituito: " + Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture)));
                    lines.Add(new OutputLine("Esecuzione completata (" + result.Instructions + " passi runtime)."));
                }
                else lines.Add(new OutputLine("Esecuzione terminata con errori runtime."));
            }

            appendOutputBatch(lines);
            formatNavigableOutput();
            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.ScrollToCaret();

            debugRuntimeModules = Array.Empty<DdfIrModule>();
            activeRuntimeArtifact = null;
            embeddedEntryPoint = null;
            embeddedEntryArguments = null;
            EmbeddedExecutionCompleted?.Invoke(this, EventArgs.Empty);
            executionRunning = false;
            updateExecutionCommands(false);
        }

        private void executionService_OutputReceived(object sender,
            DdfEditorExecutionOutputEventArgs e) => appendOutput(e.Text);

        private void executionService_ExecutionCompleted(object sender,
            DdfEditorExecutionCompletedEventArgs e) => finishExecution(e.Completion);

        private void debugService_DebugCompleted(object sender,
            DdfEditorExecutionCompletedEventArgs e) => finishExecution(e.Completion);

        private void executionService_StateChanged(object sender,
            DdfEditorExecutionStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, DdfEditorExecutionStateChangedEventArgs>(
                    executionService_StateChanged), sender, e);
                return;
            }
            executionRunning = e.State != DdfEditorExecutionState.Idle;
            updateExecutionCommands(executionRunning);
        }

        private void debugService_StateChanged(object sender,
            DdfEditorDebugStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, DdfEditorDebugStateChangedEventArgs>(
                    debugService_StateChanged), sender, e);
                return;
            }
            executionRunning = e.State != DdfEditorDebugState.Idle;
            if (e.State == DdfEditorDebugState.Paused) return;
            updateExecutionCommands(executionRunning);
        }

        private void appendOutput(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(appendOutput), text);
                return;
            }
            if (richTextBoxOutput.TextLength > 0) richTextBoxOutput.AppendText(Environment.NewLine);
            richTextBoxOutput.AppendText(text ?? string.Empty);
            richTextBoxOutput.SelectionStart = richTextBoxOutput.TextLength;
            richTextBoxOutput.ScrollToCaret();
        }

        private void clearOutput()
        {
            outputNavigationTargets.Clear();
            richTextBoxOutput.Clear();
        }

        private void appendOutputBatch(IList<OutputLine> lines)
        {
            if (lines == null || lines.Count == 0) return;
            var builder = new StringBuilder();
            int outputStart = richTextBoxOutput.TextLength;
            const string outputNewLine = "\n";
            if (outputStart > 0) builder.Append(outputNewLine);

            foreach (OutputLine line in lines)
            {
                if (builder.Length > (outputStart > 0 ? outputNewLine.Length : 0)) builder.Append(outputNewLine);
                int lineStart = outputStart + builder.Length;
                builder.Append(line.Text);
                if (line.IsNavigable)
                    outputNavigationTargets.Add(new OutputNavigationTarget(lineStart, line.Text.Length,
                        line.DocumentPath, line.SourceStart, line.SourceLength));
            }

            richTextBoxOutput.AppendText(builder.ToString());
        }

        private void formatNavigableOutput()
        {
            foreach (OutputNavigationTarget target in outputNavigationTargets)
            {
                richTextBoxOutput.Select(target.OutputStart, target.OutputLength);
                richTextBoxOutput.SelectionColor = AppTheme.Accent;
            }
            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.SelectionColor = AppTheme.Text;
        }

        private void richTextBoxOutput_DoubleClick(object sender, EventArgs e)
        {
            int position = richTextBoxOutput.SelectionStart;
            foreach (OutputNavigationTarget target in outputNavigationTargets)
            {
                if (position < target.OutputStart || position > target.OutputStart + target.OutputLength) continue;
                navigateToRuntimeSpan(target.DocumentPath, target.SourceStart, target.SourceLength);
                return;
            }
        }

        private void navigateToRuntimeSpan(string documentPath, int start, int length)
        {
            navigateWithHistory(() =>
            {
                if (!string.IsNullOrEmpty(documentPath))
                {
                    if (!openDocument(documentPath)) return false;
                }
                else if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView runtimeView))
                    activateDocument(runtimeView);
                else if (!string.IsNullOrEmpty(runtimeDocumentPath) && File.Exists(runtimeDocumentPath) && !openDocument(runtimeDocumentPath))
                    return false;
                leaveFoldedView();
                int safeStart = Math.Min(Math.Max(0, start), richTextBoxMainEditor.TextLength);
                int safeLength = Math.Min(Math.Max(1, length), richTextBoxMainEditor.TextLength - safeStart);
                richTextBoxMainEditor.Select(safeStart, safeLength);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
        }

        private void navigateToRuntimeSpan(int start, int length) =>
            navigateToRuntimeSpan(null, start, length);

        private void stopExecution()
        {
            clearDebugExecutionLine();
            if (panelEditor?.DebugService?.IsActive == true)
                panelEditor.DebugService.Stop();
            else if (panelEditor?.ExecutionService?.IsRunning == true)
                panelEditor.ExecutionService.Stop();
        }

        private string showRuntimeInputDialog()
        {
            if (InvokeRequired) return (string)Invoke(new Func<string>(showRuntimeInputDialog));
            using (var dialog = new RuntimeInputForm())
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.InputText : string.Empty;
        }

        private sealed class OutputNavigationTarget
        {
            public OutputNavigationTarget(int outputStart, int outputLength, string documentPath, int sourceStart, int sourceLength)
            {
                OutputStart = outputStart;
                OutputLength = outputLength;
                DocumentPath = documentPath ?? string.Empty;
                SourceStart = sourceStart;
                SourceLength = sourceLength;
            }

            public int OutputStart { get; }
            public int OutputLength { get; }
            public string DocumentPath { get; }
            public int SourceStart { get; }
            public int SourceLength { get; }
        }

        private sealed class OutputLine
        {
            public OutputLine(string text)
            {
                Text = text ?? string.Empty;
            }

            public OutputLine(string text, int sourceStart, int sourceLength) : this(text)
                { IsNavigable = true; SourceStart = sourceStart; SourceLength = sourceLength; }

            public OutputLine(string text, string documentPath, int sourceStart, int sourceLength) : this(text)
            {
                IsNavigable = !string.IsNullOrEmpty(documentPath);
                DocumentPath = documentPath ?? string.Empty;
                SourceStart = sourceStart;
                SourceLength = sourceLength;
            }

            public string Text { get; }
            public bool IsNavigable { get; }
            public string DocumentPath { get; }
            public int SourceStart { get; }
            public int SourceLength { get; }
        }

        private sealed class ProjectBuildContext
        {
            public ProjectBuildContext(string root, DdfProjectManifest manifest, DdfLanguagePackage package,
                IReadOnlyDictionary<string, string> buffers, bool isUntitled)
            { Root = root; Manifest = manifest; Package = package; Buffers = buffers; IsUntitled = isUntitled; }
            public string Root { get; }
            public DdfProjectManifest Manifest { get; }
            public DdfLanguagePackage Package { get; }
            public IReadOnlyDictionary<string, string> Buffers { get; }
            public bool IsUntitled { get; }
        }
    }
}
