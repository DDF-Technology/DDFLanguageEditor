using System;
using System.Collections.Generic;
using System.Globalization;
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
        private string runtimeDocumentId;

        private void runMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool running = executionTask != null && !executionTask.IsCompleted;
            updateExecutionCommands(running);
        }

        private void runProgramMenuItem_Click(object sender, EventArgs e)
        {
            if (executionTask != null && !executionTask.IsCompleted)
            {
                if (debuggerSession != null && debuggerSession.IsPaused)
                {
                    appendOutput("Esecuzione ripresa.");
                    debuggerSession.Continue();
                    updateExecutionCommands(true);
                }
                return;
            }

            string source = richTextBoxMainEditor.Text;
            DdfParseResult parse = DdfParser.Parse(source);
            DdfTypeCheckResult types = DdfTypeChecker.Check(source, parse.Root, getWorkspaceTypeRoots());
            var blockingDiagnostics = new List<DdfDiagnostic>(parse.Diagnostics);
            blockingDiagnostics.AddRange(types.Diagnostics);
            if (blockingDiagnostics.Count > 0)
            {
                updateDiagnostics(blockingDiagnostics);
                tabControlBottom.SelectedTab = tabPageDiagnostics;
                expandDiagnosticsPalette();
                appendOutput("Esecuzione non avviata: correggere gli errori di sintassi o di tipo.");
                return;
            }

            clearOutput();
            runtimeDocumentId = openDocuments.ActiveDocument.Id;
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("DDF 0.9.3.1 Beta — avvio di main()");
            executionCancellation = new CancellationTokenSource();
            CancellationToken token = executionCancellation.Token;
            debuggerSession = new DdfDebuggerSession { Paused = onDebuggerPaused };
            debuggerSession.SetBreakpoints(getEnabledBreakpointLines(openDocuments.ActiveDocument));
            updateExecutionCommands(true);
            executionTask = Task.Run(() => DdfInterpreter.Execute(source, parse.Root, new DdfExecutionOptions
            {
                EntryPoint = DdfRuntimeCatalog.DefaultEntryPoint,
                MaxInstructions = DdfRuntimeCatalog.DefaultInstructionLimit,
                CancellationRequested = () => token.IsCancellationRequested,
                DebuggerSession = debuggerSession,
                Output = line => appendOutput(line),
                Input = () => requestRuntimeInput == null ? string.Empty : requestRuntimeInput()
            }), token).ContinueWith(completed => finishExecution(completed), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void stopProgramMenuItem_Click(object sender, EventArgs e)
        {
            executionCancellation?.Cancel();
            debuggerSession?.Continue();
            stopProgramMenuItem.Enabled = false;
            appendOutput("Arresto richiesto...");
        }

        private void updateExecutionCommands(bool running)
        {
            bool paused = running && debuggerSession != null && debuggerSession.IsPaused;
            runProgramMenuItem.Enabled = !running || paused;
            runProgramMenuItem.Text = paused ? "&Continua" : "&Avvia";
            stopProgramMenuItem.Enabled = running;
            if (toolbarRunButton != null)
            {
                toolbarRunButton.Enabled = !running || paused;
                toolbarRunButton.ToolTipText = paused ? "Continua (F5)" : "Avvia (F5)";
                toolbarRunButton.AccessibleName = toolbarRunButton.ToolTipText;
            }
            if (toolbarStopButton != null) toolbarStopButton.Enabled = running;
        }

        private void finishExecution(Task<DdfExecutionResult> task)
        {
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
                    lines.Add(new OutputLine(diagnostic.ToString(), diagnostic.Start, diagnostic.Length));
                if (result.StackTrace.Count > 0)
                {
                    lines.Add(new OutputLine("Stack chiamate DDF:"));
                    foreach (DdfRuntimeStackFrame frame in result.StackTrace)
                        lines.Add(new OutputLine("  " + frame, frame.Start, frame.Length));
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
                    outputNavigationTargets.Add(new OutputNavigationTarget(lineStart, line.Text.Length, line.SourceStart, line.SourceLength));
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
                navigateToRuntimeSpan(target.SourceStart, target.SourceLength);
                return;
            }
        }

        private void navigateToRuntimeSpan(int start, int length)
        {
            if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView runtimeView))
                activateDocument(runtimeView);
            leaveFoldedView();
            int safeStart = Math.Min(Math.Max(0, start), richTextBoxMainEditor.TextLength);
            int safeLength = Math.Min(Math.Max(1, length), richTextBoxMainEditor.TextLength - safeStart);
            richTextBoxMainEditor.Select(safeStart, safeLength);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private void stopExecution()
        {
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
            public OutputNavigationTarget(int outputStart, int outputLength, int sourceStart, int sourceLength)
            {
                OutputStart = outputStart;
                OutputLength = outputLength;
                SourceStart = sourceStart;
                SourceLength = sourceLength;
            }

            public int OutputStart { get; }
            public int OutputLength { get; }
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
            {
                IsNavigable = true;
                SourceStart = sourceStart;
                SourceLength = sourceLength;
            }

            public string Text { get; }
            public bool IsNavigable { get; }
            public int SourceStart { get; }
            public int SourceLength { get; }
        }
    }
}
