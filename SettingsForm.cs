using System;
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

        public SettingsForm(EditorSettings settings)
        {
            settings = settings ?? EditorSettings.Default;
            Name = "SettingsForm";
            Text = "Impostazioni editor";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 420);
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(18) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < 8; index++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

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

            addRow(layout, 0, "Font del codice", settingsFontComboBox);
            addRow(layout, 1, "Dimensione font", settingsFontSizeNumeric);
            addRow(layout, 2, "Zoom (%)", settingsZoomNumeric);
            addRow(layout, 3, "Tema editor", settingsThemeComboBox);
            addRow(layout, 4, "Indentazione", settingsIndentStyleComboBox);
            addRow(layout, 5, "Ampiezza indentazione", settingsIndentSizeNumeric);
            addRow(layout, 6, "Fine riga al salvataggio", settingsLineEndingComboBox);
            addRow(layout, 7, "Formatter", settingsFormatOnSaveCheckBox);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            var ok = new Button { Name = "settingsOkButton", Text = "Applica", DialogResult = DialogResult.OK, Size = new Size(90, 30) };
            var cancel = new Button { Name = "settingsCancelButton", Text = "Annulla", DialogResult = DialogResult.Cancel, Size = new Size(90, 30) };
            var defaults = new Button { Name = "settingsDefaultsButton", Text = "Predefiniti", Size = new Size(100, 30) };
            defaults.Click += (sender, args) => ResetToDefaults();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(defaults);
            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public EditorSettings SelectedSettings => new EditorSettings(
            string.IsNullOrWhiteSpace(settingsFontComboBox.Text) ? EditorSettings.DefaultFontFamily : settingsFontComboBox.Text,
            (float)settingsFontSizeNumeric.Value,
            (int)settingsZoomNumeric.Value,
            settingsThemeComboBox.SelectedIndex == 1 ? EditorTheme.Light : EditorTheme.Dark,
            settingsIndentStyleComboBox.SelectedIndex == 1,
            (int)settingsIndentSizeNumeric.Value,
            settingsLineEndingComboBox.SelectedIndex == 1 ? EditorLineEnding.CrLf : EditorLineEnding.Lf,
            settingsFormatOnSaveCheckBox.Checked);

        public void ResetToDefaults()
        {
            load(EditorSettings.Default);
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
