using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public enum NavigationMode
    {
        File,
        Symbol,
        Reference,
        Line
    }

    public sealed class NavigationForm : Form
    {
        private readonly IReadOnlyDictionary<NavigationMode, IReadOnlyList<DdfWorkspaceNavigationLocation>> sources;
        private readonly int lineCount;
        private readonly ComboBox navigationModeComboBox;
        private readonly TextBox navigationQueryTextBox;
        private readonly ListView navigationResultsListView;
        private readonly Label navigationStatusLabel;
        private readonly Button navigationAcceptButton;
        private DdfWorkspaceNavigationLocation selectedLocation;

        public NavigationForm(
            IReadOnlyDictionary<NavigationMode, IReadOnlyList<DdfWorkspaceNavigationLocation>> sources,
            NavigationMode initialMode,
            int currentLine,
            int currentColumn,
            int lineCount,
            string referenceName)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
            this.lineCount = Math.Max(1, lineCount);
            Name = "NavigationForm";
            Text = "Navigazione rapida";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(620, 360);
            ClientSize = new Size(760, 460);
            ShowInTaskbar = false;
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = AppTheme.Window
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            navigationModeComboBox = new ComboBox
            {
                Name = "navigationModeComboBox",
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            navigationModeComboBox.Items.AddRange(new object[]
            {
                new ModeOption(NavigationMode.File, "Vai a file"),
                new ModeOption(NavigationMode.Symbol, "Vai a simbolo"),
                new ModeOption(NavigationMode.Reference, "Trova riferimenti"),
                new ModeOption(NavigationMode.Line, "Vai a riga/colonna")
            });

            navigationQueryTextBox = new TextBox
            {
                Name = "navigationQueryTextBox",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F),
                Margin = new Padding(0, 4, 0, 5)
            };
            navigationResultsListView = new ListView
            {
                Name = "navigationResultsListView",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            navigationResultsListView.Columns.Add("Elemento", 300);
            navigationResultsListView.Columns.Add("Posizione", 410);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            navigationStatusLabel = new Label
            {
                Name = "navigationStatusLabel",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppTheme.MutedText
            };
            navigationAcceptButton = new Button
            {
                Name = "navigationAcceptButton",
                Dock = DockStyle.Fill,
                Text = "Vai",
                DialogResult = DialogResult.OK,
                Enabled = false,
                Margin = new Padding(4, 0, 4, 0)
            };
            var cancelButton = new Button
            {
                Name = "navigationCancelButton",
                Dock = DockStyle.Fill,
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(4, 0, 0, 0)
            };
            footer.Controls.Add(navigationStatusLabel, 0, 0);
            footer.Controls.Add(navigationAcceptButton, 1, 0);
            footer.Controls.Add(cancelButton, 2, 0);

            layout.Controls.Add(navigationModeComboBox, 0, 0);
            layout.Controls.Add(navigationQueryTextBox, 0, 1);
            layout.Controls.Add(navigationResultsListView, 0, 2);
            layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
            AcceptButton = navigationAcceptButton;
            CancelButton = cancelButton;

            navigationModeComboBox.SelectedIndexChanged += (sender, args) => refreshResults();
            navigationQueryTextBox.TextChanged += (sender, args) => refreshResults();
            navigationResultsListView.SelectedIndexChanged += (sender, args) =>
            {
                if (navigationResultsListView.SelectedItems.Count > 0)
                    selectedLocation = navigationResultsListView.SelectedItems[0].Tag as DdfWorkspaceNavigationLocation;
                updateAcceptState();
            };
            navigationResultsListView.DoubleClick += (sender, args) =>
            {
                if (navigationAcceptButton.Enabled) DialogResult = DialogResult.OK;
            };
            Shown += (sender, args) =>
            {
                navigationQueryTextBox.Focus();
                navigationQueryTextBox.SelectAll();
            };

            int modeIndex = navigationModeComboBox.Items.Cast<ModeOption>().ToList()
                .FindIndex(option => option.Mode == initialMode);
            navigationModeComboBox.SelectedIndex = Math.Max(0, modeIndex);
            if (initialMode == NavigationMode.Line)
                navigationQueryTextBox.Text = currentLine + ":" + currentColumn;
            else if (initialMode == NavigationMode.Reference)
                navigationQueryTextBox.Text = referenceName ?? string.Empty;
            refreshResults();
            AppTheme.ApplyLight(this);
        }

        public NavigationMode SelectedMode => ((ModeOption)navigationModeComboBox.SelectedItem).Mode;

        public DdfWorkspaceNavigationLocation SelectedLocation => selectedLocation;

        public bool TryGetLineColumn(out int line, out int column)
        {
            return DdfWorkspaceNavigationService.TryParseLineColumn(
                navigationQueryTextBox.Text, lineCount, out line, out column);
        }

        private void refreshResults()
        {
            if (navigationModeComboBox.SelectedItem == null) return;
            NavigationMode mode = SelectedMode;
            bool lineMode = mode == NavigationMode.Line;
            navigationResultsListView.Enabled = !lineMode;
            navigationResultsListView.Items.Clear();
            selectedLocation = null;

            if (lineMode)
            {
                bool valid = TryGetLineColumn(out int line, out int column);
                navigationStatusLabel.Text = valid
                    ? "Riga " + line + ", colonna " + column
                    : "Inserire riga oppure riga:colonna (1-" + lineCount + ")";
                navigationAcceptButton.Enabled = valid;
                return;
            }

            string[] terms = navigationQueryTextBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            IReadOnlyList<DdfWorkspaceNavigationLocation> items = sources.TryGetValue(mode, out IReadOnlyList<DdfWorkspaceNavigationLocation> value)
                ? value
                : Array.Empty<DdfWorkspaceNavigationLocation>();
            foreach (DdfWorkspaceNavigationLocation location in items.Where(location => matches(location, terms)))
            {
                var item = new ListViewItem(location.Name) { Tag = location };
                string position = location.Document.DisplayName;
                if (mode != NavigationMode.File) position += "  " + location.Line + ":" + location.Column;
                if (!string.IsNullOrWhiteSpace(location.Detail)) position += "  —  " + location.Detail;
                item.SubItems.Add(position);
                navigationResultsListView.Items.Add(item);
            }
            if (navigationResultsListView.Items.Count > 0)
            {
                navigationResultsListView.Items[0].Selected = true;
                selectedLocation = navigationResultsListView.Items[0].Tag as DdfWorkspaceNavigationLocation;
            }
            navigationStatusLabel.Text = navigationResultsListView.Items.Count +
                (navigationResultsListView.Items.Count == 1 ? " risultato" : " risultati");
            updateAcceptState();
        }

        private static bool matches(DdfWorkspaceNavigationLocation location, IEnumerable<string> terms)
        {
            string searchable = location.Name + " " + location.Detail + " " + location.Document.DisplayName;
            return terms.All(term => searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void updateAcceptState()
        {
            if (SelectedMode != NavigationMode.Line)
                navigationAcceptButton.Enabled = selectedLocation != null;
        }

        private sealed class ModeOption
        {
            public ModeOption(NavigationMode mode, string text)
            {
                Mode = mode;
                Text = text;
            }

            public NavigationMode Mode { get; }
            public string Text { get; }
            public override string ToString() => Text;
        }
    }
}
