using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private readonly DdfFlowchartSnapshotService flowchartSnapshotService = new DdfFlowchartSnapshotService();
        private TabPage flowchartTabPage;
        private FlowchartView flowchartView;
        private FlowchartCanvas flowchartCanvas;
        private Label flowchartStatusLabel;
        private ToolStripMenuItem showFlowchartMenuItem;
        private ToolStrip flowchartToolStrip;
        private ToolStripDropDownButton flowchartFunctionsButton;
        private ToolStripButton flowchartGroupsButton;
        private ToolStripMenuItem educationalModeMenuItem;
        private bool educationalModeEnabled;
        private readonly DdfEditorExperienceState editorExperienceState = new DdfEditorExperienceState();
        private Action<string> saveEducationalModeSetting;
        private DdfIrBuildResult lastFlowchartIr;
        private IReadOnlyList<DdfDiagnostic> lastFlowchartDiagnostics = Array.Empty<DdfDiagnostic>();
        private DdfFlowchartSnapshotState flowchartSnapshotState = DdfFlowchartSnapshotState.Unavailable;
        private string requestedFlowchartFunction;
        private DdfFlowchartLayout fullFlowchartLayout;
        private readonly List<NavigationHistoryEntry> flowchartBackwardHistory =
            new List<NavigationHistoryEntry>();
        private readonly List<NavigationHistoryEntry> flowchartForwardHistory =
            new List<NavigationHistoryEntry>();
        private readonly HashSet<string> expandedFlowchartGroups =
            new HashSet<string>(StringComparer.Ordinal);
        private volatile bool flowchartDisposing;

        private void initializeFlowchart()
        {
            educationalModeEnabled = string.Equals(AppSettingsStore.LoadEducationalMode().Trim(),
                bool.TrueString, StringComparison.OrdinalIgnoreCase);
            editorExperienceState.SetEducationalMode(educationalModeEnabled);
            editorExperienceState.EducationalModeChanged += editorExperienceState_EducationalModeChanged;
            panelEditor.AttachExperienceState(editorExperienceState);
            saveEducationalModeSetting = AppSettingsStore.SaveEducationalMode;
            flowchartTabPage = new TabPage("Flowchart")
            {
                Name = "flowchartTabPage",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            flowchartView = new FlowchartView(menuStripMain.Font);
            flowchartCanvas = flowchartView.Canvas;
            flowchartStatusLabel = flowchartView.StatusLabel;
            flowchartToolStrip = flowchartView.ToolStrip;
            flowchartFunctionsButton = flowchartView.FunctionsButton;
            flowchartGroupsButton = flowchartView.GroupsButton;
            flowchartView.FunctionsOpening += (sender, args) => refreshFlowchartFunctionsMenu();
            flowchartView.GroupsChanged += flowchartGroupsButton_Click;
            flowchartView.ZoomChanged += (sender, args) => updateFlowchartZoomStatus();
            flowchartView.ExportRequested += flowchartSaveButton_Click;
            flowchartView.EducationalModeChanged += flowchartEducationalModeChanged;
            flowchartView.AttachExperienceState(editorExperienceState);

            flowchartView.NodeActivated += flowchartCanvas_NodeActivated;
            flowchartView.FunctionActivated += flowchartCanvas_FunctionActivated;
            flowchartView.GroupActivated += flowchartCanvas_GroupActivated;
            flowchartTabPage.Controls.Add(flowchartView);
            navigationTabs.TabPages.Insert(1, flowchartTabPage);

            showFlowchartMenuItem = new ToolStripMenuItem("Flowchart")
            {
                Name = "showFlowchartMenuItem",
                Checked = true,
                CheckOnClick = true
            };
            showFlowchartMenuItem.Click += showFlowchartMenuItem_Click;
            educationalModeMenuItem = new ToolStripMenuItem("Modalità didattica")
            {
                Name = "educationalModeMenuItem",
                Checked = educationalModeEnabled,
                CheckOnClick = true,
                ToolTipText = "Attiva presentazione e supporti didattici senza cambiare il programma"
            };
            educationalModeMenuItem.Click += educationalModeMenuItem_Click;
            int foldingIndex = viewMenuItem.DropDownItems.IndexOf(expandAllFoldsMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 1, showFlowchartMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 2, educationalModeMenuItem);
        }

        private async void scheduleFlowchartSnapshot(string text, DdfIrBuildResult ir, long documentVersion,
            IEnumerable<DdfDiagnostic> diagnostics = null)
        {
            if (flowchartDisposing || IsDisposed || Disposing) return;
            lastFlowchartIr = ir;
            lastFlowchartDiagnostics = (diagnostics ?? Array.Empty<DdfDiagnostic>())
                .GroupBy(item => new { item.Code, item.Start, item.Length, item.Message })
                .Select(group => group.First()).ToList().AsReadOnly();
            DdfIrFunction function = findFlowchartFunction(ir, richTextBoxMainEditor.SelectionStart);
            if (function == null)
            {
                flowchartSnapshotService.Cancel();
                if (flowchartCanvas.Diagram == null)
                {
                    requestedFlowchartFunction = null;
                    setFlowchartSnapshotState(DdfFlowchartSnapshotState.Unavailable,
                        "FLOWCHART — nessuna funzione rappresentabile");
                }
                else setFlowchartSnapshotState(DdfFlowchartSnapshotState.Unavailable,
                    "FLOWCHART — vista precedente, sorgente incompleto");
                return;
            }

            requestedFlowchartFunction = function.Name;
            string documentPath = documentSession?.CurrentPath ?? string.Empty;
            try
            {
                DdfFlowchartSnapshot snapshot = await flowchartSnapshotService.RequestAsync(
                    new DdfIrModule(documentPath, text, ir.Program), function.Name, documentVersion,
                    lastFlowchartDiagnostics, language: editorLanguageSession.ActivePackage.Language);
                dispatchFlowchartSnapshot(snapshot, text, function, documentVersion);
            }
            catch (ObjectDisposedException)
            {
                // La form o il servizio sono stati chiusi mentre la richiesta era in corso.
            }
            catch (InvalidOperationException) when (flowchartDisposing || IsDisposed || Disposing)
            {
                // L'handle WinForms non è più disponibile durante la chiusura.
            }
            catch (OperationCanceledException)
            {
                // Una modifica o un cambio di funzione ha sostituito questa richiesta.
            }
        }

        private void dispatchFlowchartSnapshot(DdfFlowchartSnapshot snapshot, string text,
            DdfIrFunction function, long documentVersion)
        {
            if (flowchartDisposing || IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                if (!IsHandleCreated) return;
                try
                {
                    BeginInvoke(new Action(() => dispatchFlowchartSnapshot(
                        snapshot, text, function, documentVersion)));
                }
                catch (InvalidOperationException) when (flowchartDisposing || IsDisposed || Disposing)
                {
                    // La chiusura ha distrutto l'handle tra il controllo e BeginInvoke.
                }
                return;
            }

            if (flowchartDisposing || !flowchartSnapshotService.IsCurrent(snapshot) ||
                documentVersion != analysisAppliedVersion ||
                !string.Equals(text, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
            if (snapshot.Layout == null)
            {
                setFlowchartSnapshotState(DdfFlowchartSnapshotState.Unavailable,
                    "FLOWCHART — vista precedente, analisi non valida");
                return;
            }
            bool sameFunction = fullFlowchartLayout != null && string.Equals(
                fullFlowchartLayout.Flowchart.DocumentPath, snapshot.DocumentPath,
                StringComparison.OrdinalIgnoreCase) && string.Equals(
                fullFlowchartLayout.Flowchart.FunctionName, function.Name, StringComparison.Ordinal);
            if (!sameFunction) expandedFlowchartGroups.Clear();
            fullFlowchartLayout = snapshot.Layout;
            flowchartSnapshotState = snapshot.State;
            lastFlowchartDiagnostics = snapshot.BuildResult.Diagnostics;
            applyFlowchartProjection(richTextBoxMainEditor.SelectionStart);
        }

        private void refreshFlowchartForCaret()
        {
            flowchartCanvas?.SetSourcePosition(richTextBoxMainEditor.SelectionStart);
            DdfIrFunction function = findFlowchartFunction(lastFlowchartIr, richTextBoxMainEditor.SelectionStart);
            if (function != null && !string.Equals(function.Name, requestedFlowchartFunction, StringComparison.Ordinal) &&
                string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal))
                scheduleFlowchartSnapshot(lastAnalyzedText, lastFlowchartIr, analysisAppliedVersion,
                    lastFlowchartDiagnostics);
        }

        private static DdfIrFunction findFlowchartFunction(DdfIrBuildResult ir, int position)
        {
            if (ir?.Program == null) return null;
            var functions = ir.Program.Members.OfType<DdfIrFunction>().ToList();
            return functions.Where(item => position >= item.Start && position <= item.Start + item.Length)
                .OrderBy(item => item.Length).FirstOrDefault() ??
                functions.FirstOrDefault(item => string.Equals(item.Name, "main", StringComparison.Ordinal)) ??
                functions.FirstOrDefault();
        }

        private void flowchartCanvas_NodeActivated(object sender, DdfFlowchartNode node)
        {
            if (node == null || flowchartSnapshotState == DdfFlowchartSnapshotState.Unavailable ||
                !string.Equals(flowchartCanvas.Diagram?.Flowchart.DocumentPath ?? string.Empty,
                documentSession?.CurrentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return;
            int start = Math.Min(node.Start, richTextBoxMainEditor.TextLength);
            int length = Math.Min(node.Length, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private void flowchartCanvas_FunctionActivated(object sender, string functionName)
        {
            navigateToFlowchartFunction(functionName);
        }

        private void flowchartCanvas_GroupActivated(object sender, DdfFlowchartNode node)
        {
            if (node?.IsCollapsedGroup != true || !flowchartGroupsButton.Checked) return;
            expandedFlowchartGroups.Add(node.Id);
            applyFlowchartProjection(node.Start);
        }

        private void flowchartGroupsButton_Click(object sender, EventArgs e)
        {
            expandedFlowchartGroups.Clear();
            flowchartGroupsButton.ToolTipText = flowchartGroupsButton.Checked
                ? "Espandi tutte le sequenze raggruppate"
                : "Comprimi le sequenze lineari di istruzioni";
            applyFlowchartProjection(richTextBoxMainEditor.SelectionStart);
        }

        private void applyFlowchartProjection(int sourcePosition)
        {
            if (flowchartDisposing || IsDisposed || Disposing || fullFlowchartLayout == null) return;
            DdfFlowchartLayout display = fullFlowchartLayout;
            if (flowchartGroupsButton.Checked)
            {
                DdfFlowchart projected = DdfFlowchartProjection.CollapseLinearSequences(
                    fullFlowchartLayout.Flowchart, 3, expandedFlowchartGroups);
                display = ReferenceEquals(projected, fullFlowchartLayout.Flowchart)
                    ? fullFlowchartLayout
                    : DdfFlowchartLayouter.Layout(projected);
            }
            flowchartCanvas.SetDiagramPreservingViewport(display, sourcePosition);
            updateFlowchartStatus();
            refreshFlowchartFunctionsMenu();
        }

        private void updateFlowchartStatus()
        {
            if (flowchartCanvas?.Diagram == null) return;
            int groups = flowchartCanvas.Diagram.Flowchart.Nodes.Count(node => node.IsCollapsedGroup);
            flowchartStatusLabel.Text = "FLOWCHART — " + requestedFlowchartFunction + "() · " +
                flowchartCanvas.Diagram.Flowchart.Nodes.Count + " nodi" +
                (groups > 0 ? " · " + groups + (groups == 1 ? " gruppo" : " gruppi") : string.Empty) +
                (Math.Abs(flowchartCanvas.ZoomFactor - 1F) > 0.001F
                    ? " · " + Math.Round(flowchartCanvas.ZoomFactor * 100) + "%"
                    : string.Empty) + (educationalModeEnabled ? " · DIDATTICA" : string.Empty) +
                flowchartStatusSuffix() +
                (flowchartCanvas.ExecutionNode == null ? string.Empty :
                    panelEditor.DebugService?.State == DdfEditorDebugState.AwaitingCapability
                        ? " · DEBUG IN ATTESA"
                        : " · DEBUG IN PAUSA");
            updateFlowchartViewNotice();
        }

        private void showFlowchartDebugLocation(DdfEditorDebugSnapshot snapshot)
        {
            if (snapshot == null || flowchartCanvas == null) return;
            DdfEditorDebugLocation pause = snapshot.Location;
            DdfIrModule module = debugRuntimeModules.FirstOrDefault(item => string.Equals(
                item.DocumentPath ?? string.Empty, pause.FilePath ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
            if (module == null && debugRuntimeModules.Count == 1) module = debugRuntimeModules[0];
            if (module == null) return;
            DdfIrFunction function = module.Program.Members.OfType<DdfIrFunction>()
                .Where(item => pause.Start >= item.Start && pause.Start <= item.Start + item.Length)
                .OrderBy(item => item.Length).FirstOrDefault();
            if (function == null) return;

            flowchartSnapshotService.Cancel();
            DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(module, function.Name, null,
                editorLanguageSession.ActivePackage.Language.Intellisense.Flowchart);
            if (result.Flowchart == null) return;
            bool sameFunction = fullFlowchartLayout != null && string.Equals(
                fullFlowchartLayout.Flowchart.DocumentPath, module.DocumentPath,
                StringComparison.OrdinalIgnoreCase) && string.Equals(
                fullFlowchartLayout.Flowchart.FunctionName, function.Name, StringComparison.Ordinal);
            if (!sameFunction) expandedFlowchartGroups.Clear();
            requestedFlowchartFunction = function.Name;
            fullFlowchartLayout = DdfFlowchartLayouter.Layout(result.Flowchart);
            flowchartSnapshotState = DdfFlowchartSnapshotState.Current;
            lastFlowchartDiagnostics = Array.Empty<DdfDiagnostic>();
            applyFlowchartProjection(pause.Start);
            flowchartView.SetEducationalRuntimeValues((snapshot.Variables ??
                    Array.Empty<DdfEditorDebugVariableSnapshot>())
                .Select(variable => new KeyValuePair<string, string>(variable.Name,
                    variable.DisplayValue)));
            flowchartCanvas.SetExecutionSourcePosition(pause.Start, pause.Length);
            if (flowchartTabPage != null && navigationTabs.TabPages.Contains(flowchartTabPage))
                navigationTabs.SelectedTab = flowchartTabPage;
            updateFlowchartStatus();
        }

        private void clearFlowchartDebugLocation()
        {
            if (flowchartCanvas == null) return;
            flowchartCanvas.ClearExecutionPosition();
            flowchartView?.SetEducationalRuntimeValues(null);
            if (flowchartCanvas.Diagram != null) updateFlowchartStatus();
        }

        private string flowchartStatusSuffix()
        {
            if (flowchartSnapshotState == DdfFlowchartSnapshotState.Current) return string.Empty;
            int unsupported = lastFlowchartDiagnostics.Count(item =>
                string.Equals(item.Code, "DDFIR001", StringComparison.Ordinal));
            if (flowchartSnapshotState == DdfFlowchartSnapshotState.Partial)
                return unsupported > 0
                    ? " · PARZIALE · " + unsupported + (unsupported == 1
                        ? " costrutto non rappresentato"
                        : " costrutti non rappresentati")
                    : " · PARZIALE · sorgente incompleto";
            return " · NON AGGIORNATA";
        }

        private void setFlowchartSnapshotState(DdfFlowchartSnapshotState state, string status)
        {
            flowchartSnapshotState = state;
            flowchartStatusLabel.Text = status;
            updateFlowchartViewNotice();
        }

        private void updateFlowchartViewNotice()
        {
            if (flowchartCanvas == null) return;
            string notice = flowchartSnapshotState == DdfFlowchartSnapshotState.Partial
                ? "Vista parziale — correggi il sorgente per aggiornare il diagramma completo"
                : flowchartSnapshotState == DdfFlowchartSnapshotState.Unavailable && flowchartCanvas.Diagram != null
                    ? "Vista precedente — diagramma non aggiornato"
                    : string.Empty;
            flowchartCanvas.SetViewNotice(notice, flowchartSnapshotState != DdfFlowchartSnapshotState.Current);
            string details = string.Join(Environment.NewLine, lastFlowchartDiagnostics
                .Where(item => item.Severity == DdfDiagnosticSeverity.Error)
                .Take(8).Select(item => item.Code + ": " + item.Message));
            symbolToolTip.SetToolTip(flowchartStatusLabel, details);
        }

        private void refreshFlowchartFunctionsMenu()
        {
            if (flowchartDisposing || IsDisposed || Disposing || flowchartFunctionsButton == null ||
                flowchartFunctionsButton.IsDisposed) return;
            flowchartFunctionsButton.DropDownItems.Clear();
            string current = flowchartCanvas?.Diagram?.Flowchart.FunctionName;
            var back = createFlowchartFunctionMenuItem("← Torna indietro", null,
                flowchartBackwardHistory.Any(canRestoreNavigationLocation));
            back.Name = "flowchartNavigateBackMenuItem";
            back.ShortcutKeyDisplayString = "Alt+←";
            back.Click += flowchartNavigateBackMenuItem_Click;
            flowchartFunctionsButton.DropDownItems.Add(back);
            var forward = createFlowchartFunctionMenuItem("→ Vai avanti", null,
                flowchartForwardHistory.Any(canRestoreNavigationLocation));
            forward.Name = "flowchartNavigateForwardMenuItem";
            forward.ShortcutKeyDisplayString = "Alt+→";
            forward.Click += flowchartNavigateForwardMenuItem_Click;
            flowchartFunctionsButton.DropDownItems.Add(forward);
            flowchartFunctionsButton.DropDownItems.Add(new ToolStripSeparator());
            flowchartFunctionsButton.DropDownItems.Add(createFlowchartFunctionMenuItem(
                string.IsNullOrWhiteSpace(current) ? "Nessuna funzione corrente" : "Corrente: " + current + "()",
                null, false));
            flowchartFunctionsButton.DropDownItems.Add(new ToolStripSeparator());
            IReadOnlyList<string> calls = flowchartCanvas?.Diagram?.Flowchart.Nodes
                .SelectMany(node => node.CalledFunctions).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToList() ?? new List<string>();
            if (calls.Count == 0)
            {
                flowchartFunctionsButton.DropDownItems.Add(createFlowchartFunctionMenuItem(
                    "(nessuna funzione chiamata)", null, false));
                return;
            }
            foreach (string call in calls)
            {
                DdfWorkspaceSymbol target = findFlowchartWorkspaceFunction(call);
                bool local = lastFlowchartIr?.Program?.Members.OfType<DdfIrFunction>().Any(
                    function => string.Equals(function.Name, call, StringComparison.Ordinal)) == true;
                bool standard = editorLanguageSession.ActivePackage.Language.StandardLibrary.TryGetFunction(call, out _);
                string origin = local ? documentSession.DisplayName : target?.Document.RelativePath ??
                    (standard ? "libreria standard" : null);
                var item = createFlowchartFunctionMenuItem(call + "()" +
                    (string.IsNullOrWhiteSpace(origin) ? " — non disponibile" : " — " + origin),
                    call, local || target != null);
                item.Name = "flowchartFunction_" + call;
                item.Click += flowchartFunctionMenuItem_Click;
                flowchartFunctionsButton.DropDownItems.Add(item);
            }
        }

        private ToolStripMenuItem createFlowchartFunctionMenuItem(string text, string tag, bool enabled)
        {
            return new ToolStripMenuItem(text)
            {
                Tag = tag,
                Enabled = enabled,
                Font = menuStripMain.Font,
                BackColor = SystemInformation.HighContrast ? System.Drawing.SystemColors.Menu : AppTheme.Chrome,
                ForeColor = SystemInformation.HighContrast ? System.Drawing.SystemColors.MenuText : AppTheme.Text
            };
        }

        private void flowchartFunctionMenuItem_Click(object sender, EventArgs e)
        {
            navigateToFlowchartFunction((sender as ToolStripMenuItem)?.Tag as string);
        }

        private bool navigateToFlowchartFunction(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName)) return false;
            NavigationHistoryEntry origin = captureNavigationLocation();
            bool succeeded = navigateToFlowchartFunctionCore(functionName);
            NavigationHistoryEntry destination = captureNavigationLocation();
            if (succeeded && origin != null && destination != null &&
                !sameNavigationLocation(origin, destination))
            {
                pushNavigationEntry(flowchartBackwardHistory, origin);
                flowchartForwardHistory.Clear();
            }
            return succeeded;
        }

        private bool navigateToFlowchartFunctionCore(string functionName)
        {
            DdfIrFunction local = lastFlowchartIr?.Program?.Members.OfType<DdfIrFunction>()
                .FirstOrDefault(function => string.Equals(function.Name, functionName, StringComparison.Ordinal));
            if (local != null)
            {
                return navigateWithHistory(() =>
                {
                    richTextBoxMainEditor.Select(local.NameStart, local.NameLength);
                    richTextBoxMainEditor.ScrollToCaret();
                    richTextBoxMainEditor.Focus();
                    refreshFlowchartForCaret();
                    return true;
                });
            }
            DdfWorkspaceSymbol target = findFlowchartWorkspaceFunction(functionName);
            if (target != null) return navigateToWorkspaceSymbol(target);
            flowchartStatusLabel.Text = "FLOWCHART — funzione non disponibile: " + functionName + "()";
            return false;
        }

        private void flowchartNavigateBackMenuItem_Click(object sender, EventArgs e)
        {
            restoreNavigationHistory(flowchartBackwardHistory, flowchartForwardHistory);
        }

        private void flowchartNavigateForwardMenuItem_Click(object sender, EventArgs e)
        {
            restoreNavigationHistory(flowchartForwardHistory, flowchartBackwardHistory);
        }

        private DdfWorkspaceSymbol findFlowchartWorkspaceFunction(string functionName) =>
            workspaceIndex?.FindDefinitions(functionName).FirstOrDefault(definition =>
                definition.Symbol.Kind == DdfSymbolKind.Function);

        private void updateFlowchartZoomStatus()
        {
            if (flowchartCanvas?.Diagram == null) return;
            updateFlowchartStatus();
        }

        private void flowchartSaveButton_Click(object sender, EventArgs e)
        {
            if (flowchartCanvas.Diagram == null) return;
            using (var dialog = new SaveFileDialog
            {
                Title = "Salva flowchart",
                Filter = "Immagine SVG (*.svg)|*.svg|Immagine JPEG (*.jpg)|*.jpg|Tutti i file (*.*)|*.*",
                DefaultExt = "svg",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(requestedFlowchartFunction)
                    ? "flowchart.svg"
                    : requestedFlowchartFunction + "-flowchart.svg"
            })
            {
                if (showSaveFileDialog(dialog) != DialogResult.OK) return;
                try
                {
                    string extension = Path.GetExtension(dialog.FileName);
                    bool svg = string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase);
                    bool jpeg = string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
                    if (!svg && !jpeg) jpeg = dialog.FilterIndex == 2;
                    if (jpeg) flowchartView.SaveJpeg(dialog.FileName);
                    else flowchartView.SaveSvg(dialog.FileName);
                    flowchartStatusLabel.Text = "FLOWCHART — salvata in " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is ArgumentException || exception is System.Runtime.InteropServices.ExternalException)
                {
                    MessageBox.Show(this, "Impossibile salvare la flowchart:\n" + exception.Message,
                        "Salva flowchart", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void showFlowchartMenuItem_Click(object sender, EventArgs e)
        {
            if (editorPaletteCatalog.Find(DdfEditorPaletteIds.Flowchart) != null)
            {
                editorPaletteCatalog.SetVisible(DdfEditorPaletteIds.Flowchart, showFlowchartMenuItem.Checked);
                if (showFlowchartMenuItem.Checked)
                    editorPaletteCatalog.Activate(DdfEditorPaletteIds.Flowchart);
            }
            else
            {
                flowchartVisible = showFlowchartMenuItem.Checked;
                applyFlowchartVisibility(flowchartVisible);
                persistPaletteLayout();
            }
        }

        private void educationalModeMenuItem_Click(object sender, EventArgs e)
        {
            setEducationalMode(educationalModeMenuItem.Checked, true);
        }

        private void flowchartEducationalModeChanged(object sender, EventArgs e)
        {
            setEducationalMode(flowchartView.EducationalModeEnabled, true);
        }

        private void setEducationalMode(bool enabled, bool persist)
        {
            educationalModeEnabled = enabled;
            if (educationalModeMenuItem != null) educationalModeMenuItem.Checked = enabled;
            if (flowchartView != null && flowchartView.EducationalModeEnabled != enabled)
                flowchartView.EducationalModeEnabled = enabled;
            if (editorExperienceState.EducationalModeEnabled != enabled)
                editorExperienceState.SetEducationalMode(enabled);
            updateFlowchartStatus();
            if (!persist || saveEducationalModeSetting == null) return;
            try
            {
                saveEducationalModeSetting(enabled ? bool.TrueString : bool.FalseString);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare la modalità didattica: " + exception.Message;
            }
        }

        private void editorExperienceState_EducationalModeChanged(
            object sender, DdfEducationalModeChangedEventArgs e)
        {
            setEducationalMode(e.Enabled, false);
        }

        private void applyFlowchartVisibility(bool select)
        {
            if (showFlowchartMenuItem == null || navigationTabs == null) return;
            showFlowchartMenuItem.Checked = flowchartVisible;
            if (flowchartVisible)
            {
                if (!navigationTabs.TabPages.Contains(flowchartTabPage))
                    navigationTabs.TabPages.Insert(Math.Min(1, navigationTabs.TabPages.Count), flowchartTabPage);
                if (select)
                {
                    navigationTabs.SelectedTab = flowchartTabPage;
                    if (!outlinePinned) expandOutlinePalette();
                }
            }
            else if (navigationTabs.TabPages.Contains(flowchartTabPage))
            {
                if (ReferenceEquals(navigationTabs.SelectedTab, flowchartTabPage))
                    navigationTabs.SelectedTab = navigationTabs.TabPages.Cast<TabPage>()
                        .FirstOrDefault(page => page.Name == "outlineTabPage");
                navigationTabs.TabPages.Remove(flowchartTabPage);
            }
        }

        private void disposeFlowchart()
        {
            if (flowchartDisposing) return;
            flowchartDisposing = true;
            editorExperienceState.EducationalModeChanged -= editorExperienceState_EducationalModeChanged;
            flowchartSnapshotService.Dispose();
        }
    }
}
