using System;
using System.Collections.Generic;
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
            DdfWorkspaceSymbol workspaceSymbol = occurrence == null
                ? getWorkspaceSymbolAtCaret(richTextBoxMainEditor.SelectionStart)
                : null;
            if (occurrence == null && workspaceSymbol == null) return;

            leaveFoldedView();
            hideCompletion();
            hideSignatureHelp();
            cancelSnippetSession(false);
            string currentName = occurrence?.Symbol.Name ?? workspaceSymbol.Symbol.Name;
            string newName = requestSymbolRename(currentName);
            if (newName == null) return;

            try
            {
                if (workspaceIndex != null && documentSession.HasPath)
                {
                    renameWorkspaceSymbol(occurrence, workspaceSymbol, newName);
                    return;
                }

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

        private void renameWorkspaceSymbol(DdfSymbolOccurrence occurrence,
            DdfWorkspaceSymbol workspaceSymbol, string newName)
        {
            IReadOnlyList<DdfWorkspaceSearchDocument> documents = createWorkspaceSearchDocuments();
            string declarationDocumentId = openDocuments.ActiveDocument?.Id;
            int declarationStart = occurrence?.Symbol.SelectionStart ?? -1;
            if (workspaceSymbol != null)
            {
                DdfWorkspaceSearchDocument declarationDocument = documents.FirstOrDefault(document =>
                    string.Equals(document.Path, workspaceSymbol.Document.Path, StringComparison.OrdinalIgnoreCase));
                if (declarationDocument == null) return;
                declarationDocumentId = declarationDocument.Id;
                declarationStart = workspaceSymbol.Symbol.SelectionStart;
            }

            IReadOnlyList<DdfWorkspaceNavigationLocation> references =
                DdfWorkspaceNavigationService.FindReferences(documents, declarationDocumentId, declarationStart);
            if (references.Count == 0) return;
            IReadOnlyList<DdfWorkspaceReplacementChange> changes =
                DdfWorkspaceNavigationService.CreateRenameChanges(documents, declarationDocumentId,
                    declarationStart, newName, editorLanguageSession.ActivePackage.Language);
            if (changes.All(change => string.Equals(change.UpdatedSource, change.Document.Source,
                StringComparison.Ordinal))) return;

            string originalDocumentId = openDocuments.ActiveDocument?.Id;
            string originalPath = documentSession.CurrentPath;
            int originalPosition = richTextBoxMainEditor.SelectionStart;
            DdfWorkspaceNavigationLocation selectedReference = references.FirstOrDefault(reference =>
                string.Equals(reference.Document.Path, originalPath, StringComparison.OrdinalIgnoreCase) &&
                originalPosition >= reference.Start && originalPosition <= reference.Start + reference.Length);

            var targetBuffers = new Dictionary<string, OpenDocumentBuffer>(StringComparer.OrdinalIgnoreCase);
            foreach (DdfWorkspaceReplacementChange change in changes)
            {
                OpenDocumentBuffer buffer = findReplacementBuffer(change.Document);
                if (buffer == null)
                {
                    if (string.IsNullOrEmpty(change.Document.Path) || !openDocument(change.Document.Path))
                    {
                        restoreRenamedDocument(originalDocumentId, selectedReference, references, newName);
                        return;
                    }
                    buffer = openDocuments.FindByPath(change.Document.Path);
                }
                if (buffer == null || !documentViews.ContainsKey(buffer.Id)) return;
                targetBuffers[change.Document.Id] = buffer;
            }

            foreach (DdfWorkspaceReplacementChange change in changes)
            {
                OpenDocumentBuffer buffer = targetBuffers[change.Document.Id];
                if (!documentViews.TryGetValue(buffer.Id, out DocumentView view)) return;
                if (ReferenceEquals(view.Editor, richTextBoxMainEditor))
                {
                    incrementalLexer.Reset();
                    diagnosticsFormatStart = 0;
                }
                view.Editor.SelectAll();
                view.Editor.SelectedText = change.UpdatedSource;
            }

            restoreRenamedDocument(originalDocumentId, selectedReference, references, newName);
            scheduleWorkspaceSearch();
        }

        private void restoreRenamedDocument(string originalDocumentId,
            DdfWorkspaceNavigationLocation selectedReference,
            IReadOnlyList<DdfWorkspaceNavigationLocation> references, string newName)
        {
            if (originalDocumentId != null && documentViews.TryGetValue(originalDocumentId, out DocumentView original))
                activateDocument(original);
            if (selectedReference == null) return;
            int renamedStart = selectedReference.Start + references.Count(reference =>
                string.Equals(reference.Document.Id, selectedReference.Document.Id, StringComparison.OrdinalIgnoreCase) &&
                reference.Start < selectedReference.Start) * (newName.Length - selectedReference.Length);
            renamedStart = Math.Max(0, Math.Min(renamedStart, richTextBoxMainEditor.TextLength));
            richTextBoxMainEditor.Select(renamedStart,
                Math.Min(newName.Length, richTextBoxMainEditor.TextLength - renamedStart));
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
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
            DdfHostFunctionDefinition hostFunction = symbol == null &&
                standardFunction == null ? getHostFunctionAt(position) : null;
            if (ReferenceEquals(symbol, hoveredSymbol) && ReferenceEquals(typedSpan, hoveredTypedSpan) &&
                ReferenceEquals(standardFunction, hoveredStandardFunction) &&
                ReferenceEquals(hostFunction, hoveredHostFunction) &&
                ReferenceEquals(diagnostic, hoveredDiagnostic)) return;

            hoveredSymbol = symbol;
            hoveredTypedSpan = typedSpan;
            hoveredStandardFunction = standardFunction;
            hoveredHostFunction = hostFunction;
            hoveredDiagnostic = diagnostic;
            activeHoverInfo = null;
            symbolToolTip.Hide(richTextBoxMainEditor);
            if (diagnostic != null)
            {
                symbolToolTip.Show(diagnostic.ToHoverText(),
                    richTextBoxMainEditor, e.X + 14, e.Y + 18);
                return;
            }
            if (symbol == null && typedSpan == null && standardFunction == null &&
                hostFunction == null) return;

            if (standardFunction != null)
            {
                activeHoverInfo = DdfHoverService.CreateForStandardFunction(standardFunction);
            }
            else if (hostFunction != null)
            {
                activeHoverInfo = DdfHoverService.CreateForHostFunction(hostFunction);
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

        private DdfHostFunctionDefinition getHostFunctionAt(int position)
        {
            if (lastLexResult == null) return null;
            DdfToken token = lastLexResult.Tokens.FirstOrDefault(item =>
                item.Kind == DdfTokenKind.Identifier && position >= item.Start &&
                position <= item.End);
            if (token == null) return null;
            string name = richTextBoxMainEditor.Text.Substring(token.Start,
                token.Length);
            return panelEditor.HostCatalog?.Snapshot.Functions.FirstOrDefault(
                function => string.Equals(function.Name, name,
                    StringComparison.Ordinal));
        }

        private void richTextBoxMainEditor_MouseLeave(object sender, EventArgs e)
        {
            hoveredSymbol = null;
            hoveredTypedSpan = null;
            hoveredStandardFunction = null;
            hoveredHostFunction = null;
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
