using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class SettingsForm : Form
    {
        private readonly ComboBox settingsFontComboBox;
        private readonly NumericUpDown settingsFontSizeNumeric;
        private readonly NumericUpDown settingsZoomNumeric;
        private readonly ComboBox settingsThemeComboBox;
        private readonly ComboBox settingsIndentStyleComboBox;
        private readonly NumericUpDown settingsIndentSizeNumeric;
        private readonly ComboBox settingsLineEndingComboBox;
        private readonly CheckBox settingsFormatOnSaveCheckBox;
        private readonly DataGridView settingsShortcutGrid;
        private readonly TextBox settingsShortcutTextBox;
        private readonly Label settingsShortcutStatusLabel;
        private readonly IReadOnlyList<ShortcutCommandDefinition> shortcutDefinitions;
        private readonly Dictionary<string, Keys> shortcutValues;
        private Keys capturedShortcut;

        public SettingsForm(EditorSettings settings)
            : this(settings, Array.Empty<ShortcutCommandDefinition>(), ShortcutSettings.Default(Array.Empty<ShortcutCommandDefinition>()))
        {
        }

        public SettingsForm(EditorSettings settings, IReadOnlyList<ShortcutCommandDefinition> definitions, ShortcutSettings shortcuts)
        {
            settings = settings ?? EditorSettings.Default;
            shortcutDefinitions = definitions ?? Array.Empty<ShortcutCommandDefinition>();
            shortcuts = shortcuts ?? ShortcutSettings.Default(shortcutDefinitions);
            shortcutValues = shortcutDefinitions.ToDictionary(item => item.Id, item => shortcuts.Get(item.Id), StringComparer.OrdinalIgnoreCase);

            Name = "SettingsForm";
            Text = "Impostazioni";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 560);
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            settingsFontComboBox = new ComboBox { Name = "settingsFontComboBox", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            settingsFontComboBox.Items.AddRange(FontFamily.Families.Select(family => (object)family.Name).OrderBy(name => name).ToArray());
            settingsFontComboBox.Text = settings.FontFamily;
            settingsFontSizeNumeric = createNumeric("settingsFontSizeNumeric", 8, 32, (decimal)settings.FontSize, 0.5M);
            settingsZoomNumeric = createNumeric("settingsZoomNumeric", 50, 200, settings.ZoomPercent, 10);
            settingsThemeComboBox = createChoice("settingsThemeComboBox", new[] { "Scuro", "Chiaro" }, settings.Theme == EditorTheme.Dark ? 0 : 1);
            settingsIndentStyleComboBox = createChoice("settingsIndentStyleComboBox", new[] { "Spazi", "Tab" }, settings.UseTabs ? 1 : 0);
            settingsIndentSizeNumeric = createNumeric("settingsIndentSizeNumeric", 1, 8, settings.IndentSize, 1);
            settingsLineEndingComboBox = createChoice("settingsLineEndingComboBox", new[] { "LF", "CRLF" }, settings.LineEnding == EditorLineEnding.Lf ? 0 : 1);
            settingsFormatOnSaveCheckBox = new CheckBox { Name = "settingsFormatOnSaveCheckBox", Text = "Formatta automaticamente prima di salvare", Checked = settings.FormatOnSave, Dock = DockStyle.Fill };

            var tabs = new TabControl { Name = "settingsTabs", Dock = DockStyle.Fill };
            tabs.TabPages.Add(createEditorTab());
            var shortcutTab = new TabPage("Scorciatoie") { Name = "settingsShortcutsTab", Padding = new Padding(12) };
            settingsShortcutGrid = createShortcutGrid();
            settingsShortcutTextBox = new TextBox { Name = "settingsShortcutTextBox", Dock = DockStyle.Fill, ReadOnly = true, Text = "Premi una combinazione", ShortcutsEnabled = false };
            settingsShortcutTextBox.KeyDown += settingsShortcutTextBox_KeyDown;
            settingsShortcutStatusLabel = new Label { Name = "settingsShortcutStatusLabel", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            var assign = new Button { Name = "settingsShortcutAssignButton", Text = "Assegna", Dock = DockStyle.Fill };
            var clear = new Button { Name = "settingsShortcutClearButton", Text = "Rimuovi", Dock = DockStyle.Fill };
            assign.Click += (sender, args) => assignShortcut(capturedShortcut);
            clear.Click += (sender, args) => assignShortcut(Keys.None);

            var shortcutLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            shortcutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shortcutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
            shortcutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
            shortcutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shortcutLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            shortcutLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            shortcutLayout.Controls.Add(settingsShortcutGrid, 0, 0);
            shortcutLayout.SetColumnSpan(settingsShortcutGrid, 3);
            shortcutLayout.Controls.Add(settingsShortcutTextBox, 0, 1);
            shortcutLayout.Controls.Add(assign, 1, 1);
            shortcutLayout.Controls.Add(clear, 2, 1);
            shortcutLayout.Controls.Add(settingsShortcutStatusLabel, 0, 2);
            shortcutLayout.SetColumnSpan(settingsShortcutStatusLabel, 3);
            shortcutTab.Controls.Add(shortcutLayout);
            tabs.TabPages.Add(shortcutTab);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            var ok = new Button { Name = "settingsOkButton", Text = "Applica", DialogResult = DialogResult.OK, Size = new Size(90, 30) };
            var cancel = new Button { Name = "settingsCancelButton", Text = "Annulla", DialogResult = DialogResult.Cancel, Size = new Size(90, 30) };
            var defaults = new Button { Name = "settingsDefaultsButton", Text = "Predefiniti", Size = new Size(100, 30) };
            defaults.Click += (sender, args) => ResetToDefaults();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(defaults);
            Controls.Add(tabs);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
            populateShortcutGrid();
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public EditorSettings SelectedSettings => new EditorSettings(
            string.IsNullOrWhiteSpace(settingsFontComboBox.Text) ? EditorSettings.DefaultFontFamily : settingsFontComboBox.Text,
            (float)settingsFontSizeNumeric.Value, (int)settingsZoomNumeric.Value,
            settingsThemeComboBox.SelectedIndex == 1 ? EditorTheme.Light : EditorTheme.Dark,
            settingsIndentStyleComboBox.SelectedIndex == 1, (int)settingsIndentSizeNumeric.Value,
            settingsLineEndingComboBox.SelectedIndex == 1 ? EditorLineEnding.CrLf : EditorLineEnding.Lf,
            settingsFormatOnSaveCheckBox.Checked);

        public ShortcutSettings SelectedShortcuts => new ShortcutSettings(shortcutDefinitions, shortcutValues);

        public void ResetToDefaults()
        {
            load(EditorSettings.Default);
            foreach (ShortcutCommandDefinition definition in shortcutDefinitions) shortcutValues[definition.Id] = definition.DefaultShortcut;
            populateShortcutGrid();
            settingsShortcutStatusLabel.Text = "Preset predefinito ripristinato.";
        }

        public bool TrySetShortcut(string commandId, Keys shortcut, out string conflict)
        {
            conflict = null;
            ShortcutCommandDefinition definition = shortcutDefinitions.FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.OrdinalIgnoreCase));
            if (definition == null) { conflict = "Comando non trovato."; return false; }
            if (!ShortcutSettings.IsBindable(shortcut)) { conflict = "Usa Ctrl o Alt, oppure un tasto funzione da F1 a F24."; return false; }
            ShortcutCommandDefinition existing = shortcutDefinitions.FirstOrDefault(item =>
                !string.Equals(item.Id, commandId, StringComparison.OrdinalIgnoreCase) && shortcut != Keys.None && shortcutValues[item.Id] == shortcut);
            if (existing != null) { conflict = ShortcutSettings.Format(shortcut) + " è già assegnata a “" + existing.Title + "”."; return false; }
            shortcutValues[definition.Id] = shortcut;
            updateShortcutRow(definition.Id);
            return true;
        }

        private TabPage createEditorTab()
        {
            var page = new TabPage("Editor") { Name = "settingsEditorTab", Padding = new Padding(6) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(18) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < 8; index++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            addRow(layout, 0, "Font del codice", settingsFontComboBox);
            addRow(layout, 1, "Dimensione font", settingsFontSizeNumeric);
            addRow(layout, 2, "Zoom (%)", settingsZoomNumeric);
            addRow(layout, 3, "Tema editor", settingsThemeComboBox);
            addRow(layout, 4, "Indentazione", settingsIndentStyleComboBox);
            addRow(layout, 5, "Ampiezza indentazione", settingsIndentSizeNumeric);
            addRow(layout, 6, "Fine riga al salvataggio", settingsLineEndingComboBox);
            addRow(layout, 7, "Formatter", settingsFormatOnSaveCheckBox);
            page.Controls.Add(layout);
            return page;
        }

        private DataGridView createShortcutGrid()
        {
            var grid = new DataGridView { Name = "settingsShortcutGrid", Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoGenerateColumns = false, BackgroundColor = AppTheme.Surface, BorderStyle = BorderStyle.FixedSingle, MultiSelect = false, ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "shortcutCategoryColumn", HeaderText = "Categoria", Width = 115 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "shortcutCommandColumn", HeaderText = "Comando", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "shortcutValueColumn", HeaderText = "Scorciatoia", Width = 160 });
            grid.SelectionChanged += (sender, args) => selectCurrentShortcut();
            return grid;
        }

        private void populateShortcutGrid()
        {
            if (settingsShortcutGrid == null) return;
            settingsShortcutGrid.Rows.Clear();
            foreach (ShortcutCommandDefinition definition in shortcutDefinitions)
            {
                int index = settingsShortcutGrid.Rows.Add(definition.Category, definition.Title, ShortcutSettings.Format(shortcutValues[definition.Id]));
                settingsShortcutGrid.Rows[index].Tag = definition.Id;
            }
            if (settingsShortcutGrid.Rows.Count > 0) settingsShortcutGrid.Rows[0].Selected = true;
            selectCurrentShortcut();
        }

        private void selectCurrentShortcut()
        {
            if (settingsShortcutTextBox == null || settingsShortcutGrid?.CurrentRow?.Tag == null) return;
            string id = settingsShortcutGrid.CurrentRow.Tag.ToString();
            capturedShortcut = shortcutValues[id];
            settingsShortcutTextBox.Text = ShortcutSettings.Format(capturedShortcut);
            settingsShortcutStatusLabel.Text = "Premi la nuova combinazione nella casella, poi scegli Assegna.";
        }

        private void settingsShortcutTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            capturedShortcut = e.KeyData;
            settingsShortcutTextBox.Text = ShortcutSettings.Format(capturedShortcut);
        }

        private void assignShortcut(Keys shortcut)
        {
            if (settingsShortcutGrid.CurrentRow?.Tag == null) return;
            if (TrySetShortcut(settingsShortcutGrid.CurrentRow.Tag.ToString(), shortcut, out string conflict))
            {
                capturedShortcut = shortcut;
                settingsShortcutTextBox.Text = ShortcutSettings.Format(shortcut);
                settingsShortcutStatusLabel.Text = shortcut == Keys.None ? "Scorciatoia rimossa." : "Scorciatoia assegnata.";
            }
            else
            {
                settingsShortcutStatusLabel.Text = conflict;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void updateShortcutRow(string commandId)
        {
            foreach (DataGridViewRow row in settingsShortcutGrid.Rows)
            {
                if (!string.Equals(row.Tag?.ToString(), commandId, StringComparison.OrdinalIgnoreCase)) continue;
                row.Cells[2].Value = ShortcutSettings.Format(shortcutValues[commandId]);
                break;
            }
        }

        private void load(EditorSettings settings)
        {
            settingsFontComboBox.Text = settings.FontFamily;
            settingsFontSizeNumeric.Value = (decimal)settings.FontSize;
            settingsZoomNumeric.Value = settings.ZoomPercent;
            settingsThemeComboBox.SelectedIndex = settings.Theme == EditorTheme.Dark ? 0 : 1;
            settingsIndentStyleComboBox.SelectedIndex = settings.UseTabs ? 1 : 0;
            settingsIndentSizeNumeric.Value = settings.IndentSize;
            settingsLineEndingComboBox.SelectedIndex = settings.LineEnding == EditorLineEnding.Lf ? 0 : 1;
            settingsFormatOnSaveCheckBox.Checked = settings.FormatOnSave;
        }

        private static NumericUpDown createNumeric(string name, decimal minimum, decimal maximum, decimal value, decimal increment) =>
            new NumericUpDown { Name = name, Dock = DockStyle.Left, Width = 110, Minimum = minimum, Maximum = maximum, Value = value, Increment = increment, DecimalPlaces = increment < 1 ? 1 : 0 };

        private static ComboBox createChoice(string name, string[] values, int selectedIndex)
        {
            var combo = new ComboBox { Name = name, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(values);
            combo.SelectedIndex = selectedIndex;
            return combo;
        }

        private static void addRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
