using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private IReadOnlyList<DdfQuickFix> getQuickFixesAtCaret()
        {
            if (richTextBoxFoldedView.Visible) return Array.Empty<DdfQuickFix>();
            ensureDiagnosticsCurrent();
            int position = Math.Min(richTextBoxMainEditor.SelectionStart, richTextBoxMainEditor.TextLength);
            return quickFixService.GetFixesAt(richTextBoxMainEditor.Text, activeDiagnostics, position);
        }

        private void ensureDiagnosticsCurrent()
        {
            if (string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
            highlightTimer.Stop();
            applyHighlighting();
        }

        private void populateQuickFixMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Clear();
            IReadOnlyList<DdfQuickFix> fixes = getQuickFixesAtCaret();
            foreach (DdfQuickFix fix in fixes)
            {
                var item = new ToolStripMenuItem(fix.Title)
                {
                    BackColor = AppTheme.Surface,
                    ForeColor = AppTheme.Text,
                    Tag = fix,
                    ToolTipText = fix.Diagnostic.Code + ": " + fix.Diagnostic.Message
                };
                item.Click += quickFixItem_Click;
                menu.DropDownItems.Add(item);
            }

            menu.Enabled = fixes.Count > 0;
            if (fixes.Count == 0)
            {
                menu.DropDownItems.Add(new ToolStripMenuItem("Nessuna correzione disponibile")
                {
                    BackColor = AppTheme.Surface,
                    ForeColor = Color.Gray,
                    Enabled = false
                });
            }
        }

        private void quickFixItem_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            applyQuickFix(item?.Tag as DdfQuickFix);
        }

        private void applyFirstQuickFix()
        {
            IReadOnlyList<DdfQuickFix> fixes = getQuickFixesAtCaret();
            if (fixes.Count > 0) applyQuickFix(fixes[0]);
        }

        private void applyQuickFix(DdfQuickFix fix)
        {
            if (fix == null || richTextBoxFoldedView.Visible) return;
            leaveFoldedView();
            hideCompletion();
            hideSignatureHelp();
            cancelSnippetSession(false);
            if (hasMultipleSelections) clearMultipleSelections();
            applyEdit(fix.ToEditorEdit());
            richTextBoxMainEditor.Focus();
        }
    }
}
