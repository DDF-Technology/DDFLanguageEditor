using System;
using System.Collections.Generic;
using System.Globalization;
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

        private void runMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool running = executionTask != null && !executionTask.IsCompleted;
            runProgramMenuItem.Enabled = !running;
            stopProgramMenuItem.Enabled = running;
        }

        private void runProgramMenuItem_Click(object sender, EventArgs e)
        {
            if (executionTask != null && !executionTask.IsCompleted) return;

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

            richTextBoxOutput.Clear();
            tabControlBottom.SelectedTab = tabPageOutput;
            expandDiagnosticsPalette();
            appendOutput("DDF 0.7.3 Beta — avvio di main()");
            executionCancellation = new CancellationTokenSource();
            CancellationToken token = executionCancellation.Token;
            updateExecutionCommands(true);
            executionTask = Task.Run(() => DdfInterpreter.Execute(source, parse.Root, new DdfExecutionOptions
            {
                EntryPoint = DdfRuntimeCatalog.DefaultEntryPoint,
                MaxInstructions = DdfRuntimeCatalog.DefaultInstructionLimit,
                CancellationRequested = () => token.IsCancellationRequested,
                Output = line => appendOutput(line),
                Input = () => requestRuntimeInput == null ? string.Empty : requestRuntimeInput()
            }), token).ContinueWith(completed => finishExecution(completed), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void stopProgramMenuItem_Click(object sender, EventArgs e)
        {
            executionCancellation?.Cancel();
            stopProgramMenuItem.Enabled = false;
            appendOutput("Arresto richiesto...");
        }

        private void updateExecutionCommands(bool running)
        {
            runProgramMenuItem.Enabled = !running;
            stopProgramMenuItem.Enabled = running;
            if (toolbarRunButton != null) toolbarRunButton.Enabled = !running;
            if (toolbarStopButton != null) toolbarStopButton.Enabled = running;
        }

        private void finishExecution(Task<DdfExecutionResult> task)
        {
            updateExecutionCommands(false);
            if (task.IsCanceled)
            {
                appendOutput("Esecuzione arrestata.");
            }
            else if (task.IsFaulted)
            {
                appendOutput("Errore interno: " + task.Exception.GetBaseException().Message);
            }
            else
            {
                DdfExecutionResult result = task.Result;
                foreach (DdfDiagnostic diagnostic in result.Diagnostics) appendOutput(diagnostic.ToString());
                if (result.WasCancelled) appendOutput("Esecuzione arrestata.");
                else if (result.Succeeded)
                {
                    if (result.ReturnValue != null)
                        appendOutput("Valore restituito: " + Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
                    appendOutput("Esecuzione completata (" + result.Instructions + " passi runtime).");
                }
                else appendOutput("Esecuzione terminata con errori runtime.");
            }

            executionCancellation?.Dispose();
            executionCancellation = null;
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

        private void stopExecution()
        {
            executionCancellation?.Cancel();
        }

        private string showRuntimeInputDialog()
        {
            if (InvokeRequired) return (string)Invoke(new Func<string>(showRuntimeInputDialog));
            using (var dialog = new RuntimeInputForm())
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.InputText : string.Empty;
        }
    }
}
