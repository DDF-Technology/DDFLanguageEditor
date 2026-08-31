using System;
using System.Drawing;
using System.Linq;
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

            navigateWithHistory(() =>
            {
                leaveFoldedView();
                richTextBoxMainEditor.Select(occurrence.Symbol.SelectionStart, occurrence.Symbol.SelectionLength);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
        }

        private void renameSymbolMenuItem_Click(object sender, EventArgs e)
        {
            DdfSymbolOccurrence occurrence = getCurrentSymbolOccurrence();
            if (occurrence == null) return;

            leaveFoldedView();
            hideCompletion();
            hideSignatureHelp();
            cancelSnippetSession(false);
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
            if (IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed || !symbolToolTip.Active) return;
            if (lastSemanticModel == null || !string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
            int position = richTextBoxMainEditor.GetCharIndexFromPosition(e.Location);
            DdfDiagnostic diagnostic = activeDiagnostics.FirstOrDefault(item =>
                position >= item.Start && position < item.End);
            DdfSymbolOccurrence occurrence = lastSemanticModel.FindOccurrence(position);
            DdfWorkspaceSymbol workspaceSymbol = occurrence == null ? getWorkspaceSymbolAtCaret(position) : null;
            DdfDocumentSymbol symbol = occurrence?.Symbol ?? workspaceSymbol?.Symbol;
            DdfTypedSpan typedSpan = lastTypeCheckResult?.FindTypeAt(position);
            DdfStandardFunction standardFunction = symbol == null ? getStandardFunctionAt(position) : null;
            if (ReferenceEquals(symbol, hoveredSymbol) && ReferenceEquals(typedSpan, hoveredTypedSpan) &&
                ReferenceEquals(standardFunction, hoveredStandardFunction) &&
                ReferenceEquals(diagnostic, hoveredDiagnostic)) return;

            hoveredSymbol = symbol;
            hoveredTypedSpan = typedSpan;
            hoveredStandardFunction = standardFunction;
            hoveredDiagnostic = diagnostic;
            activeHoverInfo = null;
            symbolToolTip.Hide(richTextBoxMainEditor);
            if (diagnostic != null)
            {
                symbolToolTip.Show(diagnostic.ToHoverText(),
                    richTextBoxMainEditor, e.X + 14, e.Y + 18);
                return;
            }
            if (symbol == null && typedSpan == null && standardFunction == null) return;

            if (standardFunction != null)
            {
                activeHoverInfo = DdfHoverService.CreateForStandardFunction(standardFunction);
            }
            else if (symbol != null && workspaceSymbol == null)
            {
                activeHoverInfo = DdfHoverService.CreateForSymbol(
                    symbol,
                    lastSemanticModel,
                    richTextBoxMainEditor.Text,
                    getCurrentDocumentOrigin(),
                    typedSpan?.Type.DisplayName);
            }
            else if (workspaceSymbol != null)
            {
                DdfSemanticModel workspaceModel = DdfSemanticModel.Create(
                    workspaceSymbol.Document.Source,
                    workspaceSymbol.Document.Root);
                activeHoverInfo = DdfHoverService.CreateForSymbol(
                    workspaceSymbol.Symbol,
                    workspaceModel,
                    workspaceSymbol.Document.Source,
                    workspaceSymbol.Document.RelativePath,
                    typedSpan?.Type.DisplayName);
            }
            else
            {
                activeHoverInfo = DdfHoverService.CreateForType(typedSpan.Type.DisplayName);
            }

            symbolToolTip.Show(activeHoverInfo.ToDisplayText(),
                richTextBoxMainEditor, e.X + 14, e.Y + 18);
        }

        private DdfStandardFunction getStandardFunctionAt(int position)
        {
            if (lastLexResult == null) return null;
            DdfToken token = lastLexResult.Tokens.FirstOrDefault(item =>
                item.Kind == DdfTokenKind.Identifier && position >= item.Start && position <= item.End);
            if (token == null) return null;
            string name = richTextBoxMainEditor.Text.Substring(token.Start, token.Length);
            return DdfRuntimeCatalog.TryGetStandardFunction(name, out DdfStandardFunction function)
                ? function
                : null;
        }

        private void richTextBoxMainEditor_MouseLeave(object sender, EventArgs e)
        {
            hoveredSymbol = null;
            hoveredTypedSpan = null;
            hoveredStandardFunction = null;
            hoveredDiagnostic = null;
            activeHoverInfo = null;
            if (IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed || !symbolToolTip.Active) return;
            symbolToolTip.Hide(richTextBoxMainEditor);
        }

        private string showRenameSymbolDialog(string currentName)
        {
            using (var dialog = new RenameSymbolForm(currentName))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SymbolName : null;
        }
    }
}
