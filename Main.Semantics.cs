using System;
using System.Drawing;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private DdfSymbolOccurrence getCurrentSymbolOccurrence()
        {
            if (lastSemanticModel == null || !string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal))
            {
                return null;
            }

            return lastSemanticModel.FindOccurrence(richTextBoxMainEditor.SelectionStart);
        }

        private void goToDefinitionMenuItem_Click(object sender, EventArgs e)
        {
            DdfSymbolOccurrence occurrence = getCurrentSymbolOccurrence();
            if (occurrence == null)
            {
                navigateToWorkspaceSymbol(getWorkspaceSymbolAtCaret(richTextBoxMainEditor.SelectionStart));
                return;
            }

            leaveFoldedView();
            richTextBoxMainEditor.Select(occurrence.Symbol.SelectionStart, occurrence.Symbol.SelectionLength);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private void renameSymbolMenuItem_Click(object sender, EventArgs e)
        {
            DdfSymbolOccurrence occurrence = getCurrentSymbolOccurrence();
            if (occurrence == null) return;

            leaveFoldedView();
            hideCompletion();
            string newName = requestSymbolRename(occurrence.Symbol.Name);
            if (newName == null) return;

            try
            {
                DdfRenameResult result = lastSemanticModel.Rename(occurrence.Start, newName);
                if (string.Equals(result.Text, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;

                // Replacing the complete RichEdit buffer preserves a single Undo step, but the
                // inserted text initially inherits the format at position zero. Force the next
                // pass to recolor the entire document rather than only the renamed token range.
                incrementalLexer.Reset();
                diagnosticsFormatStart = 0;
                richTextBoxMainEditor.Select(0, richTextBoxMainEditor.TextLength);
                richTextBoxMainEditor.SelectedText = result.Text;
                richTextBoxMainEditor.Select(result.SelectionStart, result.SelectionLength);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
            }
            catch (ArgumentException exception)
            {
                MessageBox.Show(this, exception.Message, "Rinomina simbolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void richTextBoxMainEditor_MouseMove(object sender, MouseEventArgs e)
        {
            if (lastSemanticModel == null || !string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
            int position = richTextBoxMainEditor.GetCharIndexFromPosition(e.Location);
            DdfSymbolOccurrence occurrence = lastSemanticModel.FindOccurrence(position);
            DdfWorkspaceSymbol workspaceSymbol = occurrence == null ? getWorkspaceSymbolAtCaret(position) : null;
            DdfDocumentSymbol symbol = occurrence?.Symbol ?? workspaceSymbol?.Symbol;
            DdfTypedSpan typedSpan = lastTypeCheckResult?.FindTypeAt(position);
            if (ReferenceEquals(symbol, hoveredSymbol) && ReferenceEquals(typedSpan, hoveredTypedSpan)) return;

            hoveredSymbol = symbol;
            hoveredTypedSpan = typedSpan;
            symbolToolTip.Hide(richTextBoxMainEditor);
            if (symbol == null && typedSpan == null) return;

            if (symbol == null)
            {
                symbolToolTip.Show("Tipo: " + typedSpan.Type.DisplayName,
                    richTextBoxMainEditor, e.X + 14, e.Y + 18, 5000);
                return;
            }

            int line = richTextBoxMainEditor.GetLineFromCharIndex(symbol.SelectionStart) + 1;
            string kind = getSymbolKindLabel(symbol.Kind);
            string detail = string.IsNullOrEmpty(symbol.Detail) ? kind : kind + " · " + symbol.Detail;
            if (typedSpan != null && detail.IndexOf(typedSpan.Type.DisplayName, StringComparison.Ordinal) < 0)
            {
                detail += "\nTipo: " + typedSpan.Type.DisplayName;
            }
            string location = workspaceSymbol == null
                ? "Definito alla riga " + line
                : "Definito in " + workspaceSymbol.Document.RelativePath;
            symbolToolTip.Show(symbol.Name + "\n" + detail + "\n" + location,
                richTextBoxMainEditor, e.X + 14, e.Y + 18, 5000);
        }

        private void richTextBoxMainEditor_MouseLeave(object sender, EventArgs e)
        {
            hoveredSymbol = null;
            hoveredTypedSpan = null;
            symbolToolTip.Hide(richTextBoxMainEditor);
        }

        private static string getSymbolKindLabel(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "libreria";
                case DdfSymbolKind.Structure: return "struttura";
                case DdfSymbolKind.Function: return "funzione";
                case DdfSymbolKind.Parameter: return "parametro";
                case DdfSymbolKind.Field: return "campo";
                default: return "variabile";
            }
        }

        private string showRenameSymbolDialog(string currentName)
        {
            using (var dialog = new RenameSymbolForm(currentName))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SymbolName : null;
        }
    }
}
