using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem highlightCurrentLineMenuItem;
        private ToolStripMenuItem showWhitespaceMenuItem;
        private ToolStripMenuItem showIndentGuidesMenuItem;
        private ToolStripMenuItem wordWrapMenuItem;
        private ToolStripButton toolbarHighlightCurrentLineButton;
        private ToolStripButton toolbarShowWhitespaceButton;
        private ToolStripButton toolbarShowIndentGuidesButton;
        private ToolStripButton toolbarWordWrapButton;

        private void initializeEditorVisuals()
        {
            highlightCurrentLineMenuItem = createVisualMenuItem(
                "highlightCurrentLineMenuItem", "Evidenzia riga corrente", () =>
                    setEditorVisualOptions(!editorSettings.HighlightCurrentLine, editorSettings.ShowWhitespace,
                        editorSettings.ShowIndentGuides, editorSettings.WordWrap));
            showWhitespaceMenuItem = createVisualMenuItem(
                "showWhitespaceMenuItem", "Mostra caratteri invisibili", () =>
                    setEditorVisualOptions(editorSettings.HighlightCurrentLine, !editorSettings.ShowWhitespace,
                        editorSettings.ShowIndentGuides, editorSettings.WordWrap));
            showIndentGuidesMenuItem = createVisualMenuItem(
                "showIndentGuidesMenuItem", "Mostra guide di indentazione", () =>
                    setEditorVisualOptions(editorSettings.HighlightCurrentLine, editorSettings.ShowWhitespace,
                        !editorSettings.ShowIndentGuides, editorSettings.WordWrap));
            wordWrapMenuItem = createVisualMenuItem(
                "wordWrapMenuItem", "A capo automatico", () =>
                    setEditorVisualOptions(editorSettings.HighlightCurrentLine, editorSettings.ShowWhitespace,
                        editorSettings.ShowIndentGuides, !editorSettings.WordWrap));

            int foldingIndex = viewMenuItem.DropDownItems.IndexOf(expandAllFoldsMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 1, new ToolStripSeparator());
            viewMenuItem.DropDownItems.Insert(foldingIndex + 2, highlightCurrentLineMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 3, showWhitespaceMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 4, showIndentGuidesMenuItem);
            viewMenuItem.DropDownItems.Insert(foldingIndex + 5, wordWrapMenuItem);

            toolbarHighlightCurrentLineButton = createToolbarButton(
                "toolbarHighlightCurrentLineButton", "▰", "Evidenzia riga corrente", highlightCurrentLineMenuItem_Click);
            toolbarShowWhitespaceButton = createToolbarButton(
                "toolbarShowWhitespaceButton", "¶", "Mostra caratteri invisibili", showWhitespaceMenuItem_Click);
            toolbarShowIndentGuidesButton = createToolbarButton(
                "toolbarShowIndentGuidesButton", "┊", "Mostra guide di indentazione", showIndentGuidesMenuItem_Click);
            toolbarWordWrapButton = createToolbarButton(
                "toolbarWordWrapButton", "↵", "A capo automatico", wordWrapMenuItem_Click);
            foreach (ToolStripButton button in new[]
            {
                toolbarHighlightCurrentLineButton, toolbarShowWhitespaceButton,
                toolbarShowIndentGuidesButton, toolbarWordWrapButton
            })
            {
                button.CheckOnClick = false;
                button.Font = new Font("Segoe UI Symbol", 11F);
            }

            int breakpointIndex = toolStripMain.Items.IndexOf(toolbarBreakpointButton);
            toolStripMain.Items.Insert(breakpointIndex, toolbarHighlightCurrentLineButton);
            toolStripMain.Items.Insert(breakpointIndex + 1, toolbarShowWhitespaceButton);
            toolStripMain.Items.Insert(breakpointIndex + 2, toolbarShowIndentGuidesButton);
            toolStripMain.Items.Insert(breakpointIndex + 3, toolbarWordWrapButton);
            toolStripMain.Items.Insert(breakpointIndex + 4, new ToolStripSeparator());
            updateEditorVisualCommandState();
        }

        private static ToolStripMenuItem createVisualMenuItem(string name, string text, Action action)
        {
            var item = new ToolStripMenuItem(text) { Name = name, CheckOnClick = false };
            item.Click += (sender, args) => action();
            return item;
        }

        private void highlightCurrentLineMenuItem_Click(object sender, EventArgs e) => highlightCurrentLineMenuItem.PerformClick();
        private void showWhitespaceMenuItem_Click(object sender, EventArgs e) => showWhitespaceMenuItem.PerformClick();
        private void showIndentGuidesMenuItem_Click(object sender, EventArgs e) => showIndentGuidesMenuItem.PerformClick();
        private void wordWrapMenuItem_Click(object sender, EventArgs e) => wordWrapMenuItem.PerformClick();

        private void setEditorVisualOptions(bool highlightCurrentLine, bool showWhitespace,
            bool showIndentGuides, bool wordWrap)
        {
            editorSettings = editorSettings.WithVisualOptions(
                highlightCurrentLine, showWhitespace, showIndentGuides, wordWrap);
            persistEditorSettings();
            applyEditorVisualSettings();
        }

        private void applyEditorVisualSettings()
        {
            if (editorSettings == null) return;
            foreach (DocumentView view in documentViews.Values) configureEditorVisuals(view.Editor);
            if (richTextBoxFoldedView != null) richTextBoxFoldedView.WordWrap = editorSettings.WordWrap;
            updateEditorVisualCommandState();
            updateLineNumbers();
        }

        private void configureEditorVisuals(RichTextBox editor)
        {
            if (editor == null) return;
            editor.WordWrap = editorSettings?.WordWrap == true;
            if (editor is MultiCursorRichTextBox visualEditor)
            {
                visualEditor.HighlightCurrentLine = editorSettings?.HighlightCurrentLine != false;
                visualEditor.ShowWhitespace = editorSettings?.ShowWhitespace == true;
                visualEditor.ShowIndentGuides = editorSettings?.ShowIndentGuides != false;
                visualEditor.VisualIndentSize = editorSettings?.IndentSize ?? EditorSettings.DefaultIndentSize;
                visualEditor.RefreshEditorVisuals();
            }
        }

        private void updateEditorVisualCommandState()
        {
            if (editorSettings == null || highlightCurrentLineMenuItem == null) return;
            highlightCurrentLineMenuItem.Checked = editorSettings.HighlightCurrentLine;
            showWhitespaceMenuItem.Checked = editorSettings.ShowWhitespace;
            showIndentGuidesMenuItem.Checked = editorSettings.ShowIndentGuides;
            wordWrapMenuItem.Checked = editorSettings.WordWrap;
            toolbarHighlightCurrentLineButton.Checked = editorSettings.HighlightCurrentLine;
            toolbarShowWhitespaceButton.Checked = editorSettings.ShowWhitespace;
            toolbarShowIndentGuidesButton.Checked = editorSettings.ShowIndentGuides;
            toolbarWordWrapButton.Checked = editorSettings.WordWrap;
        }
    }
}
