using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.D))
            {
                selectNextOccurrence();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.L))
            {
                selectAllOccurrences();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.D))
            {
                duplicateLines();
                return true;
            }

            if (keyData == Keys.Escape && activeDocumentView != null && activeDocumentView.MultiSelections.Count > 0)
            {
                clearMultipleSelections();
                return true;
            }

            if (keyData == (Keys.Shift | Keys.Alt | Keys.Right))
            {
                expandSyntacticSelection();
                return true;
            }

            if (keyData == (Keys.Shift | Keys.Alt | Keys.Left))
            {
                shrinkSyntacticSelection();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.OemPipe))
            {
                goToMatchingDelimiter();
                return true;
            }

            if (keyData == (Keys.Control | Keys.C))
            {
                copySelectionToClipboard();
                return true;
            }

            if (keyData == (Keys.Control | Keys.X))
            {
                cutSelectionToClipboard();
                return true;
            }

            if (keyData == (Keys.Control | Keys.V))
            {
                pasteTextFromClipboard();
                return true;
            }

            if (keyData == (Keys.Control | Keys.OemQuestion) ||
                keyData == (Keys.Control | Keys.Shift | Keys.D7))
            {
                toggleLineComment();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Up))
            {
                moveLines(true);
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Down))
            {
                moveLines(false);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.K))
            {
                deleteLines();
                return true;
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        private void richTextBox_TextChanged(object sender, EventArgs e)
        {
            if (isApplyingHighlighting)
            {
                return;
            }

            var changedEditor = sender as RichTextBox;
            DocumentView changedView = findDocumentView(changedEditor);
            changedView?.SelectionHistory.Clear();
            if (!isApplyingMultiEdit && changedView != null && changedView.MultiSelections.Count > 0)
            {
                changedView.MultiSelections.Clear();
                diagnosticsFormatStart = 0;
            }

            if (!isReplacingDocument)
            {
                if (changedView != null)
                {
                    string previousSource = changedView.Buffer.Source;
                    if (ReferenceEquals(changedEditor, richTextBoxMainEditor))
                        updateSnippetSession(previousSource, changedEditor.Text);
                    if (changedView.Buffer.BreakpointLines.Count > 0)
                    {
                        DdfBreakpointRemapResult remap = DdfBreakpointService.Remap(
                            previousSource, changedEditor.Text, changedView.Buffer.BreakpointLines);
                        changedView.Buffer.BreakpointLines.Clear();
                        foreach (int line in remap.Lines) changedView.Buffer.BreakpointLines.Add(line);
                        changedView.Buffer.UnboundBreakpointLines.Clear();
                        foreach (int line in remap.UnboundLines) changedView.Buffer.UnboundBreakpointLines.Add(line);
                    }
                    changedView.Buffer.UpdateSource(changedEditor.Text);
                    updateWorkspaceDocument(changedView.Buffer.Session.CurrentPath, changedEditor.Text);
                    updateDocumentTab(changedView);
                    refreshBreakpointsPalette();
                }
                if (ReferenceEquals(changedEditor, richTextBoxMainEditor)) updateDocumentUi();
            }

            if (!ReferenceEquals(changedEditor, richTextBoxMainEditor)) return;

            updateLineNumbers();
            updateCaretPosition();
            highlightTimer.Stop();
            highlightTimer.Start();
            scheduleCompletion();
            scheduleSignatureHelp();
        }

        private void highlightTimer_Tick(object sender, EventArgs e)
        {
            highlightTimer.Stop();
            applyHighlighting();
        }

        private void applyHighlighting()
        {
            if (isApplyingHighlighting || richTextBoxMainEditor.IsDisposed)
            {
                return;
            }

            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;
            string text = richTextBoxMainEditor.Text;
            DdfLexUpdate update = incrementalLexer.Update(text);
            DdfParseResult parseResult = DdfParser.Parse(text, update.Result);
            validateBreakpoints(openDocuments.ActiveDocument, text, parseResult.Root);
            DdfSemanticModel semanticModel = DdfSemanticModel.Create(text, parseResult.Root);
            if (documentSession.IsDirty)
            {
                updateWorkspaceDocument(documentSession.CurrentPath, text, parseResult.Root);
            }
            DdfTypeCheckResult typeCheckResult = DdfTypeChecker.Check(
                text,
                parseResult.Root,
                getWorkspaceTypeRoots());
            var allDiagnostics = new List<DdfDiagnostic>(parseResult.Diagnostics);
            foreach (DdfDiagnostic diagnostic in semanticModel.Diagnostics)
            {
                if (!isResolvedByWorkspace(diagnostic, text)) allDiagnostics.Add(diagnostic);
            }
            allDiagnostics.AddRange(typeCheckResult.Diagnostics);
            allDiagnostics.Sort((left, right) => left.Start.CompareTo(right.Start));
            int currentDiagnosticStart = allDiagnostics.Count == 0
                ? int.MaxValue
                : allDiagnostics[0].Start;
            int delimiterFormatStart = activeDelimiterMatch == null
                ? int.MaxValue
                : Math.Min(activeDelimiterMatch.OpenStart, activeDelimiterMatch.CloseStart);
            int formatStart = Math.Min(update.RelexStart, Math.Min(diagnosticsFormatStart, Math.Min(currentDiagnosticStart, delimiterFormatStart)));
            formatStart = Math.Min(formatStart, text.Length);
            diagnosticsFormatStart = currentDiagnosticStart;
            activeDelimiterMatch = null;

            isApplyingHighlighting = true;
            try
            {
                using (RichTextBoxUpdateScope.Begin(richTextBoxMainEditor))
                {
                    richTextBoxMainEditor.Select(formatStart, text.Length - formatStart);
                    richTextBoxMainEditor.SelectionColor = Color.FromArgb(212, 212, 212);
                    richTextBoxMainEditor.SelectionBackColor = richTextBoxMainEditor.BackColor;

                    foreach (DdfToken token in update.Result.Tokens)
                    {
                        if (token.End <= formatStart)
                        {
                            continue;
                        }

                        SyntaxKind? kind = DdfSyntaxClassifier.ToSyntaxKind(token.Kind);
                        if (kind.HasValue)
                        {
                            richTextBoxMainEditor.Select(token.Start, token.Length);
                            richTextBoxMainEditor.SelectionColor = getColor(kind.Value);
                        }
                    }

                    foreach (DdfDiagnostic diagnostic in allDiagnostics)
                    {
                        if (diagnostic.End <= formatStart)
                        {
                            continue;
                        }

                        int diagnosticStart = Math.Min(diagnostic.Start, richTextBoxMainEditor.TextLength);
                        int diagnosticLength = Math.Min(diagnostic.Length, richTextBoxMainEditor.TextLength - diagnosticStart);
                        richTextBoxMainEditor.Select(diagnosticStart, diagnosticLength);
                        richTextBoxMainEditor.SelectionColor = Color.FromArgb(241, 241, 241);
                        richTextBoxMainEditor.SelectionBackColor = Color.FromArgb(90, 29, 29);
                    }

                    formatSecondarySelections(formatStart);

                    int safeStart = Math.Min(selectionStart, richTextBoxMainEditor.TextLength);
                    int safeLength = Math.Min(selectionLength, richTextBoxMainEditor.TextLength - safeStart);
                    richTextBoxMainEditor.Select(safeStart, safeLength);
                }
            }
            finally
            {
                isApplyingHighlighting = false;
            }

            updateDiagnostics(allDiagnostics);
            updateOutline(DdfSymbolIndex.Create(parseResult.Root).Symbols);
            lastLexResult = update.Result;
            lastAnalyzedText = text;
            lastParseResult = parseResult;
            lastSemanticModel = semanticModel;
            lastTypeCheckResult = typeCheckResult;
            updateFoldingRanges(DdfFoldingRangeProvider.Create(parseResult.Root, text));
            refreshDelimiterHighlight();
        }

        private void updateOutline(IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            treeViewOutline.BeginUpdate();
            try
            {
                treeViewOutline.Nodes.Clear();
                foreach (DdfDocumentSymbol symbol in symbols)
                {
                    treeViewOutline.Nodes.Add(createOutlineNode(symbol));
                }

                if (treeViewOutline.Nodes.Count == 0)
                {
                    treeViewOutline.Nodes.Add(new TreeNode("Nessun simbolo nel documento")
                    {
                        ForeColor = Color.Gray
                    });
                }
                else
                {
                    treeViewOutline.ExpandAll();
                }
            }
            finally
            {
                treeViewOutline.EndUpdate();
            }

            int count = countSymbols(symbols);
            labelOutline.Text = count == 1 ? "OUTLINE — 1 simbolo" : "OUTLINE — " + count + " simboli";
        }

        private static TreeNode createOutlineNode(DdfDocumentSymbol symbol)
        {
            var node = new TreeNode(getSymbolPrefix(symbol.Kind) + " " + symbol)
            {
                Tag = symbol
            };
            foreach (DdfDocumentSymbol child in symbol.Children)
            {
                node.Nodes.Add(createOutlineNode(child));
            }

            return node;
        }

        private static string getSymbolPrefix(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "◈";
                case DdfSymbolKind.Structure: return "◆";
                case DdfSymbolKind.Function: return "ƒ";
                case DdfSymbolKind.Parameter: return "p";
                case DdfSymbolKind.Field: return "▪";
                default: return "•";
            }
        }

        private static int countSymbols(IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            int count = 0;
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                count += 1 + countSymbols(symbol.Children);
            }

            return count;
        }

        private void treeViewOutline_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var symbol = e.Node.Tag as DdfDocumentSymbol;
            if (symbol == null) return;

            leaveFoldedView();

            int start = Math.Min(symbol.SelectionStart, richTextBoxMainEditor.TextLength);
            int length = Math.Min(symbol.SelectionLength, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private static Color getColor(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.Comment:
                    return Color.FromArgb(106, 153, 85);
                case SyntaxKind.Library:
                    return Color.FromArgb(220, 220, 170);
                case SyntaxKind.String:
                    return Color.FromArgb(206, 145, 120);
                case SyntaxKind.Grammar:
                    return Color.FromArgb(86, 156, 214);
                case SyntaxKind.Number:
                    return Color.FromArgb(181, 206, 168);
                case SyntaxKind.DataType:
                    return Color.FromArgb(78, 201, 176);
                case SyntaxKind.Operator:
                    return Color.FromArgb(212, 212, 212);
                case SyntaxKind.Function:
                    return Color.FromArgb(220, 220, 170);
                case SyntaxKind.ControlFlow:
                    return Color.FromArgb(197, 134, 192);
                case SyntaxKind.Error:
                    return Color.FromArgb(244, 71, 71);
                default:
                    return Color.FromArgb(212, 212, 212);
            }
        }

        private void richTextBoxMainEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (hasMultipleSelections)
            {
                if (e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    applyMultiEditorEdits(range => createMultiCursorTabEdit(range, e.Shift));
                    return;
                }
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    applyMultiEditorEdits(range => EditorEditing.CreateNewLineEdit(
                        richTextBoxMainEditor.Text, range.Start, range.Length));
                    return;
                }
                if (e.KeyCode == Keys.Back)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    applyMultiBackspace();
                    return;
                }
                if (e.KeyCode == Keys.Delete)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    applyMultiDelete();
                    return;
                }
            }

            if (handleSnippetKeyDown(e))
            {
                return;
            }

            if (handleCompletionKeyDown(e))
            {
                return;
            }
            if (e.Control && e.KeyCode == Keys.M)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                if (e.Shift)
                {
                    leaveFoldedView();
                }
                else
                {
                    toggleFoldMenuItem_Click(sender, EventArgs.Empty);
                }
            }
            else if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                applyEdit(EditorEditing.CreateTabEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength,
                    e.Shift));
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                applyEdit(EditorEditing.CreateNewLineEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength));
            }
            else if (e.KeyCode == Keys.Back && richTextBoxMainEditor.SelectionLength == 0)
            {
                EditorEdit edit = EditorEditing.CreatePairedBackspaceEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength);
                if (edit != null)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    applyEdit(edit);
                }
            }
        }

        private void richTextBoxMainEditor_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (hasMultipleSelections && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                applyMultiEditorEdits(range => EditorEditing.CreatePairedCharacterEdit(
                    richTextBoxMainEditor.Text, range.Start, range.Length, e.KeyChar) ??
                    new EditorEdit(range.Start, range.Length, e.KeyChar.ToString(), range.Start + 1, 0));
                return;
            }

            EditorEdit pairedEdit = EditorEditing.CreatePairedCharacterEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength,
                e.KeyChar);
            if (pairedEdit != null)
            {
                e.Handled = true;
                applyEdit(pairedEdit);
                return;
            }

            if (e.KeyChar == '}')
            {
                e.Handled = true;
                applyEdit(EditorEditing.CreateClosingBraceEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength));
            }
        }

        private void toggleLineComment()
        {
            leaveFoldedView();
            applyEdit(EditorEditing.CreateToggleLineCommentEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength));
            richTextBoxMainEditor.Focus();
        }

        private void duplicateLines()
        {
            leaveFoldedView();
            applyEdit(EditorEditing.CreateDuplicateLinesEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength));
            richTextBoxMainEditor.Focus();
        }

        private void moveLines(bool moveUp)
        {
            leaveFoldedView();
            EditorEdit edit = EditorEditing.CreateMoveLinesEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength,
                moveUp);
            if (edit != null) applyEdit(edit);
            richTextBoxMainEditor.Focus();
        }

        private void deleteLines()
        {
            leaveFoldedView();
            applyEdit(EditorEditing.CreateDeleteLinesEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength));
            richTextBoxMainEditor.Focus();
        }

        private void applyEdit(EditorEdit edit)
        {
            richTextBoxMainEditor.Select(edit.Start, edit.Length);
            richTextBoxMainEditor.SelectedText = edit.Replacement;
            richTextBoxMainEditor.Select(edit.SelectionStart, edit.SelectionLength);
        }

        private void richTextBoxMainEditor_VScroll(object sender, EventArgs e)
        {
            updateLineNumbers();
        }

        private void richTextBoxMainEditor_FontChanged(object sender, EventArgs e)
        {
            if (richTextBoxLineNumbers != null)
            {
                richTextBoxLineNumbers.Font = richTextBoxMainEditor.Font;
                updateLineNumbers();
            }
        }

        private void richTextBoxMainEditor_SelectionChanged(object sender, EventArgs e)
        {
            if (isApplyingHighlighting || isUpdatingDelimiterHighlight)
            {
                return;
            }

            if (!isApplyingSyntacticSelection && !isApplyingMultiSelection)
            {
                DocumentView view = findDocumentView(sender as RichTextBox);
                view?.SelectionHistory.Clear();
                if (view != null && view.MultiSelections.Count > 0)
                {
                    cancelSnippetSession(false);
                    view.MultiSelections.Clear();
                    diagnosticsFormatStart = 0;
                    highlightTimer.Stop();
                    highlightTimer.Start();
                }
            }

            updateCaretPosition();
            hideCompletionIfCaretMoved();
            scheduleSignatureHelp();
            scheduleDelimiterHighlight();
            updateFoldingCommandState();
        }

        private void richTextBoxMainEditor_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            isMouseSelecting = true;
            hideCompletion();
            hideSignatureHelp();
            delimiterHighlightTimer.Stop();
        }

        private void richTextBoxMainEditor_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            isMouseSelecting = false;
            scheduleSignatureHelp();
            scheduleDelimiterHighlight();
        }

        private void updateDiagnostics(System.Collections.Generic.IReadOnlyList<DdfDiagnostic> diagnostics)
        {
            listBoxDiagnostics.BeginUpdate();
            try
            {
                listBoxDiagnostics.Items.Clear();
                foreach (DdfDiagnostic diagnostic in diagnostics)
                {
                    listBoxDiagnostics.Items.Add(diagnostic);
                }
            }
            finally
            {
                listBoxDiagnostics.EndUpdate();
            }

            int count = diagnostics.Count;
            labelDiagnostics.Text = count == 1
                ? "Diagnostica sorgente — 1 problema"
                : "Diagnostica sorgente — " + count + " problemi";
            panelDiagnostics.Visible = true;
            updateLineNumbers();
        }

        private void listBoxDiagnostics_DoubleClick(object sender, EventArgs e)
        {
            var diagnostic = listBoxDiagnostics.SelectedItem as DdfDiagnostic;
            if (diagnostic == null)
            {
                return;
            }

            int start = Math.Min(diagnostic.Start, richTextBoxMainEditor.TextLength);
            int length = Math.Min(diagnostic.Length, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
        }

        private void updateCaretPosition()
        {
            if (richTextBoxMainEditor == null || statusPositionLabel == null)
            {
                return;
            }

            int position = richTextBoxMainEditor.SelectionStart;
            int line = richTextBoxMainEditor.GetLineFromCharIndex(position);
            int lineStart = richTextBoxMainEditor.GetFirstCharIndexFromLine(line);
            int column = lineStart < 0 ? 0 : position - lineStart;
            statusPositionLabel.Text = "Riga " + (line + 1) + ", Colonna " + (column + 1);
            if (hasMultipleSelections)
                statusPositionLabel.Text += " — " + activeDocumentView.MultiSelections.Count + " cursori";
        }

        private void updateLineNumbers()
        {
            if (richTextBoxMainEditor == null || richTextBoxLineNumbers == null ||
                richTextBoxMainEditor.IsDisposed || richTextBoxLineNumbers.IsDisposed)
            {
                return;
            }

            if (richTextBoxFoldedView != null && richTextBoxFoldedView.Visible && activeFoldProjection != null)
            {
                updateFoldedLineNumbers();
                return;
            }

            int firstIndex = Math.Max(0, richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, 1)));
            int firstLine = richTextBoxMainEditor.GetLineFromCharIndex(firstIndex);
            int bottom = Math.Max(1, richTextBoxMainEditor.ClientRectangle.Height - 1);
            int lastIndex = Math.Max(0, richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, bottom)));
            int lastLine = richTextBoxMainEditor.GetLineFromCharIndex(lastIndex);

            if (lastLine < firstLine)
            {
                lastLine = firstLine;
            }

            var lineNumbers = new StringBuilder();
            var sourceLines = new List<int>();
            for (int line = firstLine; line <= lastLine; line++)
            {
                int sourceLine = line + 1;
                sourceLines.Add(sourceLine);
                lineNumbers.Append(formatLineNumber(sourceLine)).Append('\n');
            }

            richTextBoxLineNumbers.SetDisplayedSourceLines(sourceLines);

            string value = lineNumbers.ToString();
            if (!string.Equals(richTextBoxLineNumbers.Text, value, StringComparison.Ordinal))
            {
                richTextBoxLineNumbers.Text = value;
                richTextBoxLineNumbers.SelectAll();
                richTextBoxLineNumbers.SelectionAlignment = HorizontalAlignment.Left;
                richTextBoxLineNumbers.SelectionIndent = 1;
                richTextBoxLineNumbers.Select(0, 0);
            }
        }
    }
}
