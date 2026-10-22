using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private TabPage tabPageCallStack;
        private ListView callStackListView;
        private Label callStackStatusLabel;
        private int selectedCallStackFrameIndex;
        private bool refreshingCallStack;

        private void initializeCallStack()
        {
            tabPageCallStack = new TabPage("Call Stack")
            {
                Name = "tabPageCallStack",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppTheme.Surface
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            callStackStatusLabel = new Label
            {
                Name = "callStackStatusLabel",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 4, 6, 4),
                Text = "Debug inattivo — in attesa di una pausa."
            };
            callStackListView = new ListView
            {
                Name = "callStackListView",
                AccessibleName = "Stack delle chiamate del debugger",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                ShowItemToolTips = true,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text
            };
            callStackListView.Columns.Add("Frame", 60);
            callStackListView.Columns.Add("Funzione", 190);
            callStackListView.Columns.Add("Posizione", 150);
            callStackListView.Columns.Add("Documento", 360);
            callStackListView.DoubleClick += callStackListView_DoubleClick;
            callStackListView.SelectedIndexChanged += callStackListView_SelectedIndexChanged;
            callStackListView.KeyDown += callStackListView_KeyDown;
            layout.Controls.Add(callStackStatusLabel, 0, 0);
            layout.Controls.Add(callStackListView, 0, 1);
            tabPageCallStack.Controls.Add(layout);
            tabControlBottom.TabPages.Add(tabPageCallStack);
        }

        private void updateCallStackExecutionState(bool running, bool paused)
        {
            if (callStackListView == null || callStackStatusLabel == null) return;
            callStackStatusLabel.Text = !running ? "Debug inattivo — in attesa di una pausa." : paused ?
                "Frame alla pausa corrente — doppio clic o Invio per navigare." : watchSnapshot == null ?
                "Esecuzione in corso — in attesa di una pausa." :
                "Esecuzione in corso — stack dell'ultima pausa, non in tempo reale.";
            refreshCallStack(running);
        }

        private void refreshCallStack(bool running)
        {
            int selectedFrame = selectedCallStackFrameIndex;
            refreshingCallStack = true;
            callStackListView.BeginUpdate();
            try
            {
                callStackListView.Items.Clear();
                if (!running || watchSnapshot == null) return;
                for (int index = 0; index < watchSnapshot.CallStack.Count; index++)
                {
                    DdfEditorDebugStackFrameSnapshot frame = watchSnapshot.CallStack[index];
                    DdfEditorDebugLocation location = frame.Location;
                    string document = string.IsNullOrEmpty(location.FilePath)
                        ? (string.IsNullOrEmpty(runtimeDocumentPath) ? "Documento corrente" : Path.GetFileName(runtimeDocumentPath))
                        : Path.GetFileName(location.FilePath);
                    var row = new ListViewItem(new[]
                    {
                        index == 0 ? "▶ 0" : index.ToString(),
                        frame.FunctionName + "()",
                        "riga " + location.Line + ", colonna " + location.Column,
                        document
                    }) { Tag = index, ToolTipText = location.FilePath ?? runtimeDocumentPath };
                    callStackListView.Items.Add(row);
                    if (index == selectedFrame) row.Selected = true;
                }
                if (callStackListView.Items.Count > 0 && callStackListView.SelectedItems.Count == 0)
                {
                    selectedCallStackFrameIndex = 0;
                    callStackListView.Items[0].Selected = true;
                }
            }
            finally
            {
                callStackListView.EndUpdate();
                refreshingCallStack = false;
            }
        }

        private void callStackListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (refreshingCallStack || callStackListView.SelectedItems.Count == 0) return;
            selectedCallStackFrameIndex = (int)callStackListView.SelectedItems[0].Tag;
            refreshVariablesTree(executionRunning);
            refreshWatchValues();
            updateSelectedVariableFrameStatus();
        }

        private void updateSelectedVariableFrameStatus()
        {
            if (variablesStatusLabel == null || watchSnapshot == null ||
                selectedCallStackFrameIndex < 0 || selectedCallStackFrameIndex >= watchSnapshot.CallStack.Count) return;
            variablesStatusLabel.Text = "Scope selezionato: " +
                watchSnapshot.CallStack[selectedCallStackFrameIndex].FunctionName + "() — snapshot in sola lettura.";
        }

        private void callStackListView_DoubleClick(object sender, EventArgs e) => navigateToSelectedCallStackFrame();

        private void callStackListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                navigateToSelectedCallStackFrame();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.C && callStackListView.SelectedItems.Count > 0)
            {
                Clipboard.SetText(string.Join("\t", callStackListView.SelectedItems[0].SubItems
                    .Cast<ListViewItem.ListViewSubItem>().Select(item => item.Text)));
                e.SuppressKeyPress = true;
            }
        }

        private void navigateToSelectedCallStackFrame()
        {
            if (watchSnapshot == null || callStackListView.SelectedItems.Count == 0) return;
            int index = (int)callStackListView.SelectedItems[0].Tag;
            if (index < 0 || index >= watchSnapshot.CallStack.Count) return;
            DdfEditorDebugLocation frame = watchSnapshot.CallStack[index].Location;

            if (!string.IsNullOrEmpty(frame.FilePath) && File.Exists(frame.FilePath))
            {
                if (!openDocument(frame.FilePath)) return;
            }
            else if (!string.IsNullOrEmpty(runtimeDocumentId) && documentViews.TryGetValue(runtimeDocumentId, out DocumentView view))
                activateDocument(view);

            navigateToRuntimeSpan(frame.Start, frame.Length);
            richTextBoxMainEditor.Focus();
        }
    }
}
