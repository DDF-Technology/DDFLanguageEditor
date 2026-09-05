using System;
using System.Drawing;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private void formatDocumentMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
            hideCompletion();
            hideSignatureHelp();
            cancelSnippetSession(false);
            Point viewportPosition;
            bool preserveViewport = RichTextBoxUpdateScope.TryGetScrollPosition(
                richTextBoxMainEditor, out viewportPosition);
            try
            {
                string source = richTextBoxMainEditor.Text;
                DdfFormatResult result = DdfFormatter.Format(
                    source, null, editorSettings.IndentSize, editorSettings.UseTabs);
                if (string.Equals(source, result.Text, StringComparison.Ordinal)) return;

                EditorEdit edit = result.CreateEdit(
                    source,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength);

                // RichEdit assigns one inherited character format to a complete-buffer
                // replacement. If the document begins with a comment, that format can be
                // green. Relex and repaint from zero so unchanged prefixes do not retain it.
                incrementalLexer.Reset();
                diagnosticsFormatStart = 0;
                applyEdit(edit);
            }
            finally
            {
                richTextBoxMainEditor.Focus();
                if (preserveViewport)
                {
                    RichTextBoxUpdateScope.SetScrollPosition(richTextBoxMainEditor, viewportPosition);
                    captureDocumentViewport(activeDocumentView);
                    updateLineNumbers();
                    refreshEditorOverviewViewport();
                }
            }
        }
    }
}
