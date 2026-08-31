using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private EditorSettings editorSettings;
        private ToolStripMenuItem settingsMenuItem;
        private ToolStripButton toolbarSettingsButton;
        private Func<SettingsForm, DialogResult> showSettingsDialog;
        private Action<string> saveEditorSettingsSetting;

        private void initializeEditorSettings()
        {
            editorSettings = EditorSettings.Parse(AppSettingsStore.LoadEditorSettings());
            saveEditorSettingsSetting = AppSettingsStore.SaveEditorSettings;
            showSettingsDialog = dialog => dialog.ShowDialog(this);
            settingsMenuItem = new ToolStripMenuItem("Impostazioni...")
            {
                Name = "settingsMenuItem",
                ShortcutKeys = Keys.Control | Keys.Oemcomma
            };
            settingsMenuItem.Click += settingsMenuItem_Click;
            viewMenuItem.DropDownItems.Add(new ToolStripSeparator());
            viewMenuItem.DropDownItems.Add(settingsMenuItem);

            toolbarSettingsButton = createToolbarButton("toolbarSettingsButton", "\uE713", "Impostazioni (Ctrl+,)", settingsMenuItem_Click);
            int breakpointIndex = toolStripMain.Items.IndexOf(toolbarBreakpointButton);
            toolStripMain.Items.Insert(breakpointIndex, toolbarSettingsButton);
        }

        private void settingsMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new SettingsForm(editorSettings))
            {
                if (showSettingsDialog(dialog) != DialogResult.OK) return;
                editorSettings = dialog.SelectedSettings;
            }
            persistEditorSettings();
            applyEditorSettings();
        }

        private void persistEditorSettings()
        {
            try
            {
                saveEditorSettingsSetting(editorSettings.Serialize());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare le impostazioni: " + exception.Message;
            }
        }

        private void applyEditorSettings()
        {
            Color background = editorSettings.Theme == EditorTheme.Dark ? Color.FromArgb(30, 30, 30) : Color.White;
            Color foreground = editorDefaultForeground;
            foreach (DocumentView view in documentViews.Values)
            {
                applyEditorControlSettings(view.Editor, background, foreground, true);
            }
            applyEditorControlSettings(richTextBoxFoldedView, background, foreground, true);
            applyEditorControlSettings(richTextBoxLineNumbers, background,
                editorSettings.Theme == EditorTheme.Dark ? Color.FromArgb(133, 133, 133) : Color.FromArgb(90, 90, 90), false);
            editorHost.BackColor = background;
            panelEditor.BackColor = background;
            if (completionListBox != null) completionListBox.Font = createEditorFont(Math.Max(8F, editorSettings.FontSize - 0.5F));
            if (signatureHelpLabel != null) signatureHelpLabel.Font = createEditorFont(editorSettings.FontSize);
            diagnosticsFormatStart = 0;
            applyHighlighting();
            updateLineNumbers();
        }

        private void applyEditorControlSettings(RichTextBox control, Color background, Color foreground, bool configureTabs)
        {
            if (control == null) return;
            control.Font = createEditorFont(editorSettings.FontSize);
            control.ZoomFactor = editorSettings.ZoomPercent / 100F;
            control.BackColor = background;
            control.ForeColor = foreground;
            if (configureTabs) applyEditorTabStops(control);
        }

        private void applyEditorTabStops(RichTextBox control)
        {
            int selectionStart = control.SelectionStart;
            int selectionLength = control.SelectionLength;
            int spaceWidth = Math.Max(1, TextRenderer.MeasureText(" ", control.Font, Size.Empty,
                TextFormatFlags.NoPadding).Width);
            int tabWidth = Math.Max(4, spaceWidth * editorSettings.IndentSize);
            var stops = new int[32];
            for (int index = 0; index < stops.Length; index++) stops[index] = tabWidth * (index + 1);
            bool previousHighlightState = isApplyingHighlighting;
            isApplyingHighlighting = true;
            try
            {
                control.SelectAll();
                control.SelectionTabs = stops;
                control.Select(Math.Min(selectionStart, control.TextLength),
                    Math.Min(selectionLength, control.TextLength - Math.Min(selectionStart, control.TextLength)));
            }
            finally
            {
                isApplyingHighlighting = previousHighlightState;
            }
        }

        private Font createEditorFont(float size)
        {
            try { return new Font(editorSettings.FontFamily, size); }
            catch (ArgumentException) { return new Font(EditorSettings.DefaultFontFamily, size); }
        }

        private Color editorDefaultForeground => editorSettings?.Theme == EditorTheme.Light
            ? Color.FromArgb(32, 32, 32)
            : Color.FromArgb(212, 212, 212);
    }
}
