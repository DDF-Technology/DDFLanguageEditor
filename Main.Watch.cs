using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ListView watchListView;
        private TextBox watchExpressionTextBox;
        private Label watchStatusLabel;
        private DdfEditorDebugSnapshot watchSnapshot;
        private WatchExpressionSettings watchExpressionSettings;
        private Func<string> loadWatchExpressionsSetting;
        private Action<string> saveWatchExpressionsSetting;
        private string activeWatchProjectPath;

        private void initializeWatch()
        {
            loadWatchExpressionsSetting = AppSettingsStore.LoadWatchExpressions;
            saveWatchExpressionsSetting = AppSettingsStore.SaveWatchExpressions;
            string persistedWatch = loadWatchExpressionsSetting();
            DdfPersistenceReadResult<WatchExpressionSettings> watchRead =
                WatchExpressionSettings.ParseVersioned(persistedWatch);
            watchExpressionSettings = watchRead.Value ?? new WatchExpressionSettings();
            if (!watchRead.Succeeded && !string.IsNullOrWhiteSpace(persistedWatch))
                statusFileLabel.ToolTipText =
                    "Watch persistite non applicate; i dati originali sono stati conservati. " +
                    watchRead.Failure;
            var page = new TabPage("Espressioni di controllo") { Name = "tabPageWatch" };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            watchExpressionTextBox = new TextBox { Name = "watchExpressionTextBox", Dock = DockStyle.Fill,
                AccessibleName = "Espressione da controllare", PlaceholderText = "Espressione pura: value + 1, array[index], point.x", MaxLength = 512 };
            var add = new Button { Name = "watchAddButton", Text = "Aggiungi", AutoSize = true };
            var remove = new Button { Name = "watchRemoveButton", Text = "Rimuovi", AutoSize = true };
            watchStatusLabel = new Label { Name = "watchStatusLabel", AutoSize = true, Dock = DockStyle.Fill,
                Text = "In attesa di una pausa del debugger." };
            watchListView = new ListView { Name = "watchListView", Dock = DockStyle.Fill, View = View.Details,
                FullRowSelect = true, HideSelection = false, AccessibleName = "Espressioni di controllo" };
            watchListView.Columns.Add("Espressione", 200);
            watchListView.Columns.Add("Tipo runtime", 100);
            watchListView.Columns.Add("Valore", 220);
            watchListView.Columns.Add("Stato", 350);
            add.Click += (sender, args) => addWatchExpression();
            watchExpressionTextBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter) { addWatchExpression(); args.SuppressKeyPress = true; }
            };
            remove.Click += (sender, args) => removeWatchExpressions();
            watchListView.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Delete) { removeWatchExpressions(); args.SuppressKeyPress = true; }
                if (args.Control && args.KeyCode == Keys.C && watchListView.SelectedItems.Count > 0)
                {
                    Clipboard.SetText(string.Join(Environment.NewLine, watchListView.SelectedItems.Cast<ListViewItem>()
                        .Select(row => string.Join("\t", row.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(cell => cell.Text)))));
                    args.SuppressKeyPress = true;
                }
            };
            layout.Controls.Add(watchExpressionTextBox, 0, 0);
            layout.Controls.Add(add, 1, 0);
            layout.Controls.Add(remove, 2, 0);
            layout.Controls.Add(watchStatusLabel, 0, 1); layout.SetColumnSpan(watchStatusLabel, 3);
            layout.Controls.Add(watchListView, 0, 2); layout.SetColumnSpan(watchListView, 3);
            page.Controls.Add(layout);
            tabControlBottom.TabPages.Add(page);
        }

        private void addWatchExpression()
        {
            string expression = watchExpressionTextBox.Text.Trim();
            if (expression.Length == 0) return;
            var existing = watchListView.Items.Cast<ListViewItem>().FirstOrDefault(row => row.Text == expression);
            if (existing == null) watchListView.Items.Add(new ListViewItem(new[] { expression, "", "", "" }));
            else existing.Selected = true;
            watchExpressionTextBox.Clear();
            refreshWatchValues();
            persistWatchExpressions();
        }

        private void removeWatchExpressions()
        {
            foreach (ListViewItem row in watchListView.SelectedItems.Cast<ListViewItem>().ToArray()) row.Remove();
            persistWatchExpressions();
        }

        private void refreshWatchValues()
        {
            if (watchListView == null) return;
            foreach (ListViewItem row in watchListView.Items)
            {
                DdfEditorWatchResult result = watchSnapshot == null ? null :
                    panelEditor.DebugService.EvaluateWatch(row.Text,
                        selectedCallStackFrameIndex);
                row.SubItems[1].Text = result?.Value == null ? "—" :
                    result.Value.TypeName + (result.Value.IsTransactionalState
                        ? " · state" : result.Value.IsFixed ? " · fixed" :
                        result.Value.IsReadOnly ? " · sola lettura" : string.Empty);
                row.SubItems[2].Text = result?.Value?.DisplayValue ?? "—";
                row.SubItems[3].Text = result?.Message ??
                    "In attesa di una pausa del debugger.";
            }
        }

        private void updateWatchExecutionState(bool running, bool paused)
        {
            if (watchStatusLabel == null) return;
            if (!running) watchSnapshot = null;
            watchStatusLabel.Text = !running ? "Debug inattivo — in attesa di una pausa." : paused ?
                "Valori alla pausa corrente (prima dell'istruzione evidenziata)." : watchSnapshot == null ?
                "Esecuzione in corso — in attesa di una pausa." : "Esecuzione in corso — valori dell'ultima pausa, non in tempo reale.";
            refreshWatchValues();
        }

        private void switchWatchProject(string projectPath)
        {
            persistWatchExpressions();
            activeWatchProjectPath = projectPath;
            watchListView.Items.Clear();
            foreach (string expression in watchExpressionSettings.GetExpressions(projectPath))
                watchListView.Items.Add(new ListViewItem(new[] { expression, "", "", "" }));
            refreshWatchValues();
        }

        private void persistWatchExpressions()
        {
            if (watchExpressionSettings == null || string.IsNullOrWhiteSpace(activeWatchProjectPath) ||
                watchListView == null) return;
            watchExpressionSettings.SetExpressions(activeWatchProjectPath,
                watchListView.Items.Cast<ListViewItem>().Select(item => item.Text));
            try { saveWatchExpressionsSetting?.Invoke(watchExpressionSettings.Serialize()); }
            catch (Exception exception) when (exception is System.IO.IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare le espressioni Watch: " + exception.Message;
            }
        }
    }
}
