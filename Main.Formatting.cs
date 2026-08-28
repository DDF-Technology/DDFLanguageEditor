using System;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private void formatDocumentMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
            hideCompletion();
            string source = richTextBoxMainEditor.Text;
            DdfFormatResult result = DdfFormatter.Format(source);
            if (string.Equals(source, result.Text, StringComparison.Ordinal))
            {
                richTextBoxMainEditor.Focus();
                return;
            }

            EditorEdit edit = result.CreateEdit(
                source,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength);
            applyEdit(edit);
            richTextBoxMainEditor.Focus();
        }
    }
}
