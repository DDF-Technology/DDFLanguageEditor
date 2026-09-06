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
        private CancellationTokenSource executionCancellation;
        private Task executionTask;
        private readonly List<OutputNavigationTarget> outputNavigationTargets = new List<OutputNavigationTarget>();
        private readonly DdfBuildService buildService = new DdfBuildService();
        private string runtimeDocumentId;
        private string runtimeDocumentPath;
        private bool debugUiLocked;
        private bool executionRunning;
        private long executionGeneration;
        private string debugBaseWindowTitle;

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
            "settingsMenuItem", "manageLanguagePackagesMenuItem", "validateLanguagePackageMenuItem",
            "languagePlaygroundMenuItem"
        };

        private static readonly HashSet<string> DebugUnsafeToolbarCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "toolbarNewButton", "toolbarOpenButton", "toolbarSaveButton", "toolbarSaveAllButton",
            "toolbarCloseDocumentButton", "toolbarUndoButton", "toolbarRedoButton", "toolbarCutButton",
            "toolbarPasteButton", "toolbarCommentButton", "toolbarDuplicateLinesButton",
            "toolbarMoveLinesUpButton", "toolbarMoveLinesDownButton", "toolbarDeleteLinesButton",
            "toolbarCompletionButton", "toolbarQuickFixButton", "toolbarFormatButton",
            "toolbarValidateButton", "toolbarBuildButton", "toolbarRunWithoutDebugButton",
            "toolbarSettingsButton", "toolbarLanguageManagerButton", "toolbarLanguagePlaygroundButton",
            "toolbarLanguageButton"
        };

        private void runMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            updateExecutionCommands(executionRunning);
        }

        private void runProgramMenuItem_Click(object sender, EventArgs e)
        {
            if (executionRunning)
            {
                if (debuggerSession != null && debuggerSession.IsPaused)
                {
                    clearDebugExecutionLine();
                    appendOutput("Esecuzione ripresa.");
                    debuggerSession.Continue();
                    updateExecutionCommands(true);
                }
                return;
            }

            executeProject(true);
        }

        private void runWithoutDebugMenuItem_Click(object sender, EventArgs e)
        {
            if (!executionRunning) executeProject(false);
        }

        private void validateProjectMenuItem_Click(object sender, EventArgs e) => validateOrBuildProject(false);

        private void buildProjectMenuItem_Click(object sender, EventArgs e) => validateOrBuildProject(true);

        private void validateOrBuildProject(bool writeArtifact)
        {
            if (!tryCreateBuildContext(out ProjectBuildContext context, out IReadOnlyList<DdfBuildIssue> setupIssues))
            {
                showBuildIssues(writeArtifact ? "Compilazione" : "Convalida", setupIssues);
                return;
            }
            if (writeArtifact && context.IsUntitled)
            {
                showBuildIssues("Compilazione", new[] { DdfBuildIssue.Configuration("DDFB008",
                    "Salvare il documento o aprire una cartella progetto prima di generare un artefatto.") });
                return;
            }

            try
            {
                DdfBuildResult result = writeArtifact
                    ? buildService.Build(context.Root, context.Manifest, context.Package, context.Buffers)
                    : buildService.Validate(context.Root, context.Manifest, context.Package, context.Buffers);
                showBuildResult(writeArtifact ? "Compilazione" : "Convalida", result);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                showBuildIssues(writeArtifact ? "Compilazione" : "Convalida", new[]
                { DdfBuildIssue.Configuration("DDFB999", exception.Message) });
            }
        }

        private void executeProject(bool debug)
        {
            long generation = ++executionGeneration;
            clearDebugExecutionLine();
            if (!tryCreateBuildContext(out ProjectBuildContext context, out IReadOnlyList<DdfBuildIssue> setupIssues,
                preferActiveDocument: true))
            {
                showBuildIssues(debug ? "Debug" : "Esecuzione", setupIssues);
                return;
            }
            DdfBuildResult build;
            try
            {
                build = buildService.Validate(context.Root, context.Manifest, context.Package, context.Buffers);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                showBuildIssues(debug ? "Debug" : "Esecuzione", new[]
                { DdfBuildIssue.Configuration("DDFB999", exception.Message) });
                return;
            }
            if (!build.Success)
            {
                showBuildResult(debug ? "Debug" : "Esecuzione", build);
                return;
            }

            string entryPath = Path.GetFullPath(Path.Combine(context.Root, build.Artifact.EntrySource.Replace('/', Path.DirectorySeparatorChar)));
            DdfBuildArtifactSource entryArtifact = build.Artifact.Sources.First(item =>
                string.Equals(item.RelativePath, build.Artifact.EntrySource, StringComparison.OrdinalIgnoreCase));
            string source;
            try
            {
                source = entryArtifact.FromUnsavedBuffer && context.Buffers.TryGetValue(entryPath, out string bufferedSource)
                    ? bufferedSource : File.ReadAllText(entryPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                showBuildIssues(debug ? "Debug" : "Esecuzione", new[]
                { DdfBuildIssue.Configuration("DDFB999", exception.Message, entryPath) });
                return;
            }
            clearOutput();
            OpenDocumentBuffer entryBuffer = context.IsUntitled ? openDocuments.ActiveDocument : openDocuments.FindByPath(entryPath);
            runtimeDocumentId = entryBuffer?.Id;
            runtimeDocumentPath = entryPath;
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("DDFLanguageEditor 0.11.0 Beta — " + (debug ? "debug" : "esecuzione") + " di " +
                build.Artifact.EntryPoint + "() [" + build.Artifact.Backend + ", " + build.Artifact.Fingerprint.Substring(0, 12) + "]");
            executionCancellation = new CancellationTokenSource();
            CancellationToken token = executionCancellation.Token;
            debuggerSession = debug ? new DdfDebuggerSession { Paused = onDebuggerPaused } : null;
            if (debuggerSession != null)
            {
                synchronizeRuntimeBreakpoints(entryBuffer);
            }
            executionRunning = true;
            updateExecutionCommands(true);
            executionTask = Task.Run(() => DdfInterpreter.Execute(build.Artifact.RuntimeModules, context.Package.Language, new DdfExecutionOptions
            {
                EntryPoint = build.Artifact.EntryPoint,
                MaxInstructions = build.Artifact.MaxInstructions,
                CancellationRequested = () => token.IsCancellationRequested,
                DebuggerSession = debuggerSession,
                SourcePath = entryPath,
                Output = line => appendOutput(line),
                Input = () => requestRuntimeInput == null ? string.Empty : requestRuntimeInput()
            }), token).ContinueWith(completed => finishExecution(completed, generation), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private bool tryCreateBuildContext(out ProjectBuildContext context, out IReadOnlyList<DdfBuildIssue> issues,
            bool preferActiveDocument = false)
        {
            context = null;
            var setupIssues = new List<DdfBuildIssue>();
            DdfLanguagePackage package = DdfLanguageCatalog.Capture().Package;
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
            executionCancellation?.Cancel();
            debuggerSession?.Continue();
            stopProgramMenuItem.Enabled = false;
            appendOutput("Arresto richiesto...");
            updateWatchExecutionState(true, false);
        }

        private void updateExecutionCommands(bool running)
        {
            bool paused = running && debuggerSession != null && debuggerSession.IsPaused;
            setDebugUiLock(running);
            updateWatchExecutionState(running, paused);
            updateVariablesExecutionState(running, paused);
            updateCallStackExecutionState(running, paused);
            runProgramMenuItem.Enabled = !running || paused;
            runProgramMenuItem.Text = paused ? "&Continua" : "Avvia &debug";
            runWithoutDebugMenuItem.Enabled = !running;
            validateProjectMenuItem.Enabled = !running;
            buildProjectMenuItem.Enabled = !running;
            stopProgramMenuItem.Enabled = running;
            if (toolbarValidateButton != null) toolbarValidateButton.Enabled = !running;
            if (toolbarBuildButton != null) toolbarBuildButton.Enabled = !running;
            if (toolbarRunWithoutDebugButton != null) toolbarRunWithoutDebugButton.Enabled = !running;
            if (toolbarRunButton != null)
            {
                toolbarRunButton.Enabled = !running || paused;
                toolbarRunButton.ToolTipText = paused ? "Continua (F5)" : "Avvia debug (F5)";
                toolbarRunButton.AccessibleName = toolbarRunButton.ToolTipText;
            }
            if (toolbarStopButton != null) toolbarStopButton.Enabled = running;
            if (stepIntoMenuItem != null) stepIntoMenuItem.Enabled = paused;
            if (toolbarStepIntoButton != null) toolbarStepIntoButton.Enabled = paused;
            if (stepOverMenuItem != null) stepOverMenuItem.Enabled = paused;
            if (toolbarStepOverButton != null) toolbarStepOverButton.Enabled = paused;
            bool canStepOut = paused && watchSnapshot?.CallStack != null && watchSnapshot.CallStack.Count > 1;
            if (stepOutMenuItem != null) stepOutMenuItem.Enabled = canStepOut;
            if (toolbarStepOutButton != null) toolbarStepOutButton.Enabled = canStepOut;
            updateDebugStatus(running, paused);
        }

        private void setDebugUiLock(bool locked)
        {
            if (locked == debugUiLocked) return;
            debugUiLocked = locked;
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

        private void finishExecution(Task<DdfExecutionResult> task, long generation)
        {
            if (generation != executionGeneration) return;
            executionRunning = false;
            clearDebugExecutionLine();
            var lines = new List<OutputLine>();
            if (task.IsCanceled)
            {
                lines.Add(new OutputLine("Esecuzione arrestata."));
            }
            else if (task.IsFaulted)
            {
                lines.Add(new OutputLine("Errore interno: " + task.Exception.GetBaseException().Message));
            }
            else
            {
                DdfExecutionResult result = task.Result;
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
            updateExecutionCommands(false);
            formatNavigableOutput();
            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.ScrollToCaret();

            executionCancellation?.Dispose();
            executionCancellation = null;
            debuggerSession?.Dispose();
            debuggerSession = null;
            executionTask = null;
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
            executionCancellation?.Cancel();
            debuggerSession?.Continue();
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
