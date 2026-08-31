using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            TextBoxBase focusedTextBox = findFocusedTextBox(this);
            if (focusedTextBox != null && !isEditorTextControl(focusedTextBox))
            {
                if (handleStandardTextShortcut(focusedTextBox, keyData)) return true;
                return base.ProcessCmdKey(ref message, keyData);
            }

            if (keyData == (Keys.Control | Keys.OemPeriod))
            {
                applyFirstQuickFix();
                return true;
            }

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

        private bool handleStandardTextShortcut(TextBoxBase textBox, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.A))
            {
                textBox.SelectAll();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C) || keyData == (Keys.Control | Keys.Insert))
            {
                textBox.Copy();
                return true;
            }
            if (keyData == (Keys.Control | Keys.X) || keyData == (Keys.Shift | Keys.Delete))
            {
                if (!textBox.ReadOnly) textBox.Cut();
                return true;
            }
            if (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert))
            {
                if (!textBox.ReadOnly) textBox.Paste();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Z))
            {
                if (!textBox.ReadOnly && textBox.CanUndo) textBox.Undo();
                return true;
            }
            return false;
        }

        private bool isEditorTextControl(TextBoxBase control)
        {
            return ReferenceEquals(control, richTextBoxMainEditor) ||
                   ReferenceEquals(control, richTextBoxFoldedView);
        }

        private static TextBoxBase findFocusedTextBox(Control root)
        {
            if (root is TextBoxBase textBox && textBox.Focused) return textBox;
            foreach (Control child in root.Controls)
            {
                if (!child.ContainsFocus && !child.Focused) continue;
                TextBoxBase found = findFocusedTextBox(child);
                if (found != null) return found;
            }
            return null;
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
                    changedView.LastEditPosition = Math.Min(changedEditor.SelectionStart, changedEditor.TextLength);
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
            scheduleWorkspaceSearch();
        }

        private void highlightTimer_Tick(object sender, EventArgs e)
        {
            highlightTimer.Stop();
            startBackgroundAnalysis();
        }

        private void applyHighlighting()
        {
            if (isApplyingHighlighting || richTextBoxMainEditor.IsDisposed)
            {
                return;
            }

            cancelBackgroundAnalysis();
            long version = ++analysisRequestVersion;
            string text = richTextBoxMainEditor.Text;
            DdfLexUpdate update = incrementalLexer.Update(text);
            DdfParseResult parseResult = DdfParser.Parse(text, update.Result);
            DdfSemanticModel semanticModel = DdfSemanticModel.Create(text, parseResult.Root);
            DdfTypeCheckResult typeCheckResult = DdfTypeChecker.Check(
                text,
                parseResult.Root,
                getWorkspaceTypeRoots());
            var snapshot = new EditorAnalysisSnapshot(
                text,
                update.Result,
                parseResult,
                semanticModel,
                typeCheckResult,
                DdfSymbolIndex.Create(parseResult.Root).Symbols,
                DdfFoldingRangeProvider.Create(parseResult.Root, text));
            applyAnalysisSnapshot(snapshot, update.RelexStart, version);
        }

        private async void startBackgroundAnalysis()
        {
            if (isApplyingHighlighting || IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed) return;

            string text = richTextBoxMainEditor.Text;
            IReadOnlyList<CompilationUnitSyntax> externalRoots = getWorkspaceTypeRoots();
            long version = ++analysisRequestVersion;
            cancelBackgroundAnalysis();
            var cancellation = new CancellationTokenSource();
            analysisCancellation = cancellation;

            try
            {
                EditorAnalysisSnapshot snapshot = await Task.Run(
                    () => createAnalysisSnapshot(text, externalRoots, cancellation.Token),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested || version != analysisRequestVersion) return;
                postEditorCallback(() =>
                {
                    if (version != analysisRequestVersion || IsDisposed || Disposing ||
                        richTextBoxMainEditor.IsDisposed ||
                        !string.Equals(text, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
                    int formatStart = findChangedLineStart(lastAnalyzedText, text);
                    incrementalLexer.Reset();
                    applyAnalysisSnapshot(snapshot, formatStart, version);
                });
            }
            catch (OperationCanceledException)
            {
                // A newer editor snapshot has superseded this request.
            }
            finally
            {
                if (ReferenceEquals(analysisCancellation, cancellation)) analysisCancellation = null;
                cancellation.Dispose();
            }
        }

        private static EditorAnalysisSnapshot createAnalysisSnapshot(
            string text,
            IReadOnlyList<CompilationUnitSyntax> externalRoots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DdfLexResult lexResult = DdfLexer.Lex(text);
            cancellationToken.ThrowIfCancellationRequested();
            DdfParseResult parseResult = DdfParser.Parse(text, lexResult);
            cancellationToken.ThrowIfCancellationRequested();
            DdfSemanticModel semanticModel = DdfSemanticModel.Create(text, parseResult.Root);
            cancellationToken.ThrowIfCancellationRequested();
            DdfTypeCheckResult typeCheckResult = DdfTypeChecker.Check(text, parseResult.Root, externalRoots);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parseResult.Root).Symbols;
            IReadOnlyList<DdfFoldingRange> folding = DdfFoldingRangeProvider.Create(parseResult.Root, text);
            return new EditorAnalysisSnapshot(text, lexResult, parseResult, semanticModel, typeCheckResult, symbols, folding);
        }

        private void applyAnalysisSnapshot(EditorAnalysisSnapshot snapshot, int changedStart, long version)
        {
            if (snapshot == null || version != analysisRequestVersion ||
                !string.Equals(snapshot.Text, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;

            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;
            string text = snapshot.Text;
            validateBreakpoints(openDocuments.ActiveDocument, text, snapshot.ParseResult.Root);
            if (documentSession.IsDirty)
                updateWorkspaceDocument(documentSession.CurrentPath, text, snapshot.ParseResult.Root);

            var allDiagnostics = new List<DdfDiagnostic>(snapshot.ParseResult.Diagnostics);
            foreach (DdfDiagnostic diagnostic in snapshot.SemanticModel.Diagnostics)
            {
                if (!isResolvedByWorkspace(diagnostic, text)) allDiagnostics.Add(diagnostic);
            }
            allDiagnostics.AddRange(snapshot.TypeCheckResult.Diagnostics);
            allDiagnostics.Sort((left, right) => left.Start.CompareTo(right.Start));
            int currentDiagnosticStart = allDiagnostics.Count == 0
                ? int.MaxValue
                : allDiagnostics[0].Start;
            int delimiterFormatStart = activeDelimiterMatch == null
                ? int.MaxValue
                : Math.Min(activeDelimiterMatch.OpenStart, activeDelimiterMatch.CloseStart);
            int formatStart = Math.Min(changedStart, Math.Min(diagnosticsFormatStart, Math.Min(currentDiagnosticStart, delimiterFormatStart)));
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
                    RichTextBoxDiagnosticDecoration.ClearSelection(richTextBoxMainEditor);

                    foreach (DdfToken token in snapshot.LexResult.Tokens)
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
                        RichTextBoxDiagnosticDecoration.ApplySelection(richTextBoxMainEditor, diagnostic.Severity);
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
            updateOutline(snapshot.Symbols);
            lastLexResult = snapshot.LexResult;
            lastAnalyzedText = text;
            lastParseResult = snapshot.ParseResult;
            lastSemanticModel = snapshot.SemanticModel;
            lastTypeCheckResult = snapshot.TypeCheckResult;
            updateFoldingRanges(snapshot.FoldingRanges);
            analysisAppliedVersion = version;
            refreshDelimiterHighlight();
            updateNavigationCommandState();
        }

        private void cancelBackgroundAnalysis()
        {
            analysisCancellation?.Cancel();
        }

        private void postEditorCallback(Action callback)
        {
            if (callback == null || IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(callback);
            }
            catch (InvalidOperationException)
            {
                // The form handle was destroyed while the background request completed.
            }
        }

        private static int findChangedLineStart(string previousText, string currentText)
        {
            previousText = previousText ?? string.Empty;
            currentText = currentText ?? string.Empty;
            int limit = Math.Min(previousText.Length, currentText.Length);
            int position = 0;
            while (position < limit && previousText[position] == currentText[position]) position++;
            if (position >= currentText.Length) return currentText.Length;
            int lineBreak = position == 0 ? -1 : currentText.LastIndexOf('\n', position - 1);
            return lineBreak + 1;
        }

        private sealed class EditorAnalysisSnapshot
        {
            public EditorAnalysisSnapshot(string text, DdfLexResult lexResult, DdfParseResult parseResult,
                DdfSemanticModel semanticModel, DdfTypeCheckResult typeCheckResult,
                IReadOnlyList<DdfDocumentSymbol> symbols, IReadOnlyList<DdfFoldingRange> foldingRanges)
            {
                Text = text;
                LexResult = lexResult;
                ParseResult = parseResult;
                SemanticModel = semanticModel;
                TypeCheckResult = typeCheckResult;
                Symbols = symbols;
                FoldingRanges = foldingRanges;
            }

            public string Text { get; }
            public DdfLexResult LexResult { get; }
            public DdfParseResult ParseResult { get; }
            public DdfSemanticModel SemanticModel { get; }
            public DdfTypeCheckResult TypeCheckResult { get; }
            public IReadOnlyList<DdfDocumentSymbol> Symbols { get; }
            public IReadOnlyList<DdfFoldingRange> FoldingRanges { get; }
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

            navigateWithHistory(() =>
            {
                leaveFoldedView();
                int start = Math.Min(symbol.SelectionStart, richTextBoxMainEditor.TextLength);
                int length = Math.Min(symbol.SelectionLength, richTextBoxMainEditor.TextLength - start);
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
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
            updateNavigationCommandState();
        }

        private void richTextBoxMainEditor_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && !richTextBoxFoldedView.Visible)
            {
                int position = richTextBoxMainEditor.GetCharIndexFromPosition(e.Location);
                int selectionEnd = richTextBoxMainEditor.SelectionStart + richTextBoxMainEditor.SelectionLength;
                if (position < richTextBoxMainEditor.SelectionStart || position > selectionEnd)
                    richTextBoxMainEditor.Select(position, 0);
                return;
            }

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
            activeDiagnostics = diagnostics ?? new List<DdfDiagnostic>();
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

        private byte getDiagnosticUnderlineTypeAt(int position)
        {
            return getDiagnosticUnderlineFormatAt(position, true);
        }

        private byte getDiagnosticUnderlineColorAt(int position)
        {
            return getDiagnosticUnderlineFormatAt(position, false);
        }

        private byte getDiagnosticUnderlineFormatAt(int position, bool readType)
        {
            if (position < 0 || position >= richTextBoxMainEditor.TextLength) return 0;
            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;
            bool wasApplyingHighlighting = isApplyingHighlighting;
            try
            {
                isApplyingHighlighting = true;
                richTextBoxMainEditor.Select(position, 1);
                return readType
                    ? RichTextBoxDiagnosticDecoration.GetSelectionUnderlineType(richTextBoxMainEditor)
                    : RichTextBoxDiagnosticDecoration.GetSelectionUnderlineColor(richTextBoxMainEditor);
            }
            finally
            {
                richTextBoxMainEditor.Select(selectionStart, selectionLength);
                isApplyingHighlighting = wasApplyingHighlighting;
            }
        }

        private void listBoxDiagnostics_DoubleClick(object sender, EventArgs e)
        {
            var diagnostic = listBoxDiagnostics.SelectedItem as DdfDiagnostic;
            if (diagnostic == null)
            {
                return;
            }

            navigateWithHistory(() =>
            {
                int start = Math.Min(diagnostic.Start, richTextBoxMainEditor.TextLength);
                int length = Math.Min(diagnostic.Length, richTextBoxMainEditor.TextLength - start);
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
                return true;
            });
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
