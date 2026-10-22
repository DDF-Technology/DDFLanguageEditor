using System;
using System.Drawing;
using System.Windows.Forms;
using DDF___Program_Language_Editor;

namespace DDFLanguageEditor.MinimalHost
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MinimalHostForm());
        }
    }

    public sealed class MinimalHostForm : Form
    {
        private const string InitialScript =
            "main() out int\r\n" +
            "{\r\n" +
            "    int value << 40;\r\n\r\n" +
            "    value << value + 2;\r\n\r\n" +
            "    ret value;\r\n" +
            "}\r\n";

        private readonly DdfEmbeddedEditorControl editorHost;
        private readonly Button executeButton;
        private readonly Label statusLabel;
        private readonly string scriptDocumentId;
        private string savedScript = InitialScript;

        public MinimalHostForm()
        {
            Text = "DDF Editor SDK — Minimal Host";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 760);

            executeButton = new Button
            {
                AutoSize = true,
                Text = "Esegui script collegato"
            };
            executeButton.Click += executeButton_Click;
            statusLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(12, 8, 0, 0),
                Text = "Pronto"
            };
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8)
            };
            header.Controls.Add(executeButton);
            header.Controls.Add(statusLabel);

            editorHost = new DdfEmbeddedEditorControl
            {
                Dock = DockStyle.Fill,
                HostDisplayName = "Minimal Host"
            };
            editorHost.DocumentSaveRequested += editorHost_DocumentSaveRequested;
            editorHost.ExecutionCompleted += editorHost_ExecutionCompleted;

            Controls.Add(editorHost);
            Controls.Add(header);
            scriptDocumentId = editorHost.SetPrimaryDocument("minimal-script.ddf", savedScript);
        }

        public DdfEmbeddedEditorControl EditorHost => editorHost;
        public DdfEditorView Editor => editorHost.Editor;
        public Button ExecuteButton => executeButton;
        public Label StatusLabel => statusLabel;
        public string ScriptDocumentId => scriptDocumentId;
        public string SavedScript => savedScript;

        public bool ExecuteLinkedScript()
        {
            editorHost.ActivateDocument(scriptDocumentId);
            bool started = editorHost.ExecuteScript(editorHost.HasBreakpoints);
            statusLabel.Text = started
                ? "Esecuzione avviata"
                : "Impossibile avviare lo script";
            return started;
        }

        private void executeButton_Click(object sender, EventArgs e)
        {
            ExecuteLinkedScript();
        }

        private void editorHost_DocumentSaveRequested(object sender,
            DdfEmbeddedDocumentSaveEventArgs e)
        {
            if (!string.Equals(e.DocumentId, scriptDocumentId, StringComparison.Ordinal)) return;
            savedScript = e.SourceText;
            e.Succeeded = true;
            statusLabel.Text = "Script salvato dall'applicazione host";
        }

        private void editorHost_ExecutionCompleted(object sender, EventArgs e)
        {
            void UpdateStatus()
            {
                executeButton.Enabled = true;
                statusLabel.Text = editorHost.LastExecutionResult?.Succeeded == true
                    ? "Risultato: " + editorHost.LastExecutionResult.ReturnValue
                    : "Esecuzione non completata";
            }

            if (InvokeRequired) BeginInvoke((Action)UpdateStatus);
            else UpdateStatus();
        }
    }
}
