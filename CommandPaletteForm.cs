using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class CommandPaletteCommand
    {
        private readonly Action execute;

        internal CommandPaletteCommand(string title, string category, string shortcutText, bool enabled, Action execute)
        {
            Title = title ?? string.Empty;
            Category = category ?? string.Empty;
            ShortcutText = shortcutText ?? string.Empty;
            Enabled = enabled;
            this.execute = execute;
        }

        public string Title { get; }
        public string Category { get; }
        public string ShortcutText { get; }
        public bool Enabled { get; }
        internal void Execute() => execute?.Invoke();
    }

    public sealed class CommandPaletteForm : Form
    {
        private readonly IReadOnlyList<CommandPaletteCommand> commands;
        private readonly TextBox commandPaletteQueryTextBox;
        private readonly ListView commandPaletteResultsListView;
        private readonly Label commandPaletteStatusLabel;
        private readonly Button commandPaletteExecuteButton;
        private CommandPaletteCommand selectedCommand;

        public CommandPaletteForm(IReadOnlyList<CommandPaletteCommand> commands)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Name = "CommandPaletteForm";
            Text = "Command Palette";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(620, 360);
            ClientSize = new Size(780, 470);
            ShowInTaskbar = false;
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                BackColor = AppTheme.Window
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            commandPaletteQueryTextBox = new TextBox
            {
                Name = "commandPaletteQueryTextBox",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12F),
                PlaceholderText = "Cerca un comando...",
                Margin = new Padding(0, 2, 0, 7)
            };
            commandPaletteResultsListView = new ListView
            {
                Name = "commandPaletteResultsListView",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            commandPaletteResultsListView.Columns.Add("Comando", 330);
            commandPaletteResultsListView.Columns.Add("Categoria", 120);
            commandPaletteResultsListView.Columns.Add("Scorciatoia", 150);
            commandPaletteResultsListView.Columns.Add("Stato", 130);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            commandPaletteStatusLabel = new Label
            {
                Name = "commandPaletteStatusLabel",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppTheme.MutedText
            };
            commandPaletteExecuteButton = new Button
            {
                Name = "commandPaletteExecuteButton",
                Dock = DockStyle.Fill,
                Text = "Esegui",
                DialogResult = DialogResult.OK,
                Enabled = false,
                Margin = new Padding(4, 0, 4, 0)
            };
            var cancelButton = new Button
            {
                Name = "commandPaletteCancelButton",
                Dock = DockStyle.Fill,
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(4, 0, 0, 0)
            };
            footer.Controls.Add(commandPaletteStatusLabel, 0, 0);
            footer.Controls.Add(commandPaletteExecuteButton, 1, 0);
            footer.Controls.Add(cancelButton, 2, 0);
            layout.Controls.Add(commandPaletteQueryTextBox, 0, 0);
            layout.Controls.Add(commandPaletteResultsListView, 0, 1);
            layout.Controls.Add(footer, 0, 2);
            Controls.Add(layout);
            AcceptButton = commandPaletteExecuteButton;
            CancelButton = cancelButton;

            commandPaletteQueryTextBox.TextChanged += (sender, args) => refreshResults();
            commandPaletteQueryTextBox.KeyDown += commandPaletteQueryTextBox_KeyDown;
            commandPaletteResultsListView.SelectedIndexChanged += (sender, args) =>
            {
                selectedCommand = commandPaletteResultsListView.SelectedItems.Count == 0
                    ? null
                    : commandPaletteResultsListView.SelectedItems[0].Tag as CommandPaletteCommand;
                updateExecuteState();
            };
            commandPaletteResultsListView.DoubleClick += (sender, args) =>
            {
                if (commandPaletteExecuteButton.Enabled) DialogResult = DialogResult.OK;
            };
            Shown += (sender, args) =>
            {
                commandPaletteQueryTextBox.Focus();
                commandPaletteQueryTextBox.SelectAll();
            };
            refreshResults();
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public CommandPaletteCommand SelectedCommand => selectedCommand;

        private void refreshResults()
        {
            string[] terms = commandPaletteQueryTextBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            commandPaletteResultsListView.BeginUpdate();
            try
            {
                commandPaletteResultsListView.Items.Clear();
                selectedCommand = null;
                foreach (CommandPaletteCommand command in commands.Where(command => matches(command, terms)))
                {
                    var item = new ListViewItem(command.Title) { Tag = command };
                    item.SubItems.Add(command.Category);
                    item.SubItems.Add(command.ShortcutText);
                    item.SubItems.Add(command.Enabled ? "Disponibile" : "Non disponibile");
                    if (!command.Enabled) item.ForeColor = AppTheme.MutedText;
                    commandPaletteResultsListView.Items.Add(item);
                }
                if (commandPaletteResultsListView.Items.Count > 0)
                {
                    commandPaletteResultsListView.Items[0].Selected = true;
                    selectedCommand = commandPaletteResultsListView.Items[0].Tag as CommandPaletteCommand;
                }
            }
            finally
            {
                commandPaletteResultsListView.EndUpdate();
            }
            commandPaletteStatusLabel.Text = commandPaletteResultsListView.Items.Count +
                (commandPaletteResultsListView.Items.Count == 1 ? " comando" : " comandi");
            updateExecuteState();
        }

        private static bool matches(CommandPaletteCommand command, IEnumerable<string> terms)
        {
            string searchable = command.Title + " " + command.Category + " " + command.ShortcutText;
            return terms.All(term => searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void commandPaletteQueryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Down && e.KeyCode != Keys.Up) return;
            if (commandPaletteResultsListView.Items.Count == 0) return;
            int current = commandPaletteResultsListView.SelectedIndices.Count == 0
                ? 0
                : commandPaletteResultsListView.SelectedIndices[0];
            int next = e.KeyCode == Keys.Down
                ? Math.Min(commandPaletteResultsListView.Items.Count - 1, current + 1)
                : Math.Max(0, current - 1);
            commandPaletteResultsListView.Items[next].Selected = true;
            commandPaletteResultsListView.Items[next].Focused = true;
            commandPaletteResultsListView.EnsureVisible(next);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void updateExecuteState()
        {
            commandPaletteExecuteButton.Enabled = SelectedCommand?.Enabled == true;
        }
    }
}
