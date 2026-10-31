using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private string outlineRenderSignature;
        private string diagnosticsRenderSignature;
        private int outlineRenderCount;
        private int diagnosticsRenderCount;
        private IReadOnlyList<ClassifiedSpan> lastFunctionColorSpans =
            Array.Empty<ClassifiedSpan>();
        private string lastFunctionColorSource = string.Empty;

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (isPaneFocusShortcut(keyData))
                return moveFocusBetweenPanes(keyData == (Keys.Shift | Keys.F6));
            if (isPaletteTabShortcut(keyData) && cycleFocusedPaletteTab(keyData)) return true;

            // F10 è intercettato nativamente dalla barra dei menu su Windows e non
            // sempre raggiunge lo ShortcutKeys del ToolStrip. Gestiamo esplicitamente
            // tutti i passi del debugger, rispettando anche scorciatoie personalizzate.
            if (matchesShortcut(stepIntoMenuItem, keyData) ||
                (keyData == Keys.F11 && stepIntoMenuItem.ShortcutKeys == Keys.F10))
            {
                if (stepIntoMenuItem.Enabled) stepIntoMenuItem.PerformClick();
                return true;
            }
            if (matchesShortcut(stepOverMenuItem, keyData))
            {
                if (stepOverMenuItem.Enabled) stepOverMenuItem.PerformClick();
                return true;
            }
            if (matchesShortcut(stepOutMenuItem, keyData))
            {
                if (stepOutMenuItem.Enabled) stepOutMenuItem.PerformClick();
                return true;
            }

            TextBoxBase focusedTextBox = findFocusedTextBox(this);
            if (focusedTextBox != null && !isEditorTextControl(focusedTextBox))
            {
                if (handleStandardTextShortcut(focusedTextBox, keyData)) return true;
                return base.ProcessCmdKey(ref message, keyData);
            }

            if (matchesShortcut(quickFixMenuItem, keyData))
            {
                applyFirstQuickFix();
                return true;
            }

            if (matchesShortcut(selectNextOccurrenceMenuItem, keyData))
            {
                selectNextOccurrence();
                return true;
            }

            if (matchesShortcut(selectAllOccurrencesMenuItem, keyData))
            {
                selectAllOccurrences();
                return true;
            }

            if (matchesShortcut(duplicateLinesMenuItem, keyData))
            {
                duplicateLines();
                return true;
            }

            if (keyData == Keys.Escape && activeDocumentView != null && activeDocumentView.MultiSelections.Count > 0)
            {
                clearMultipleSelections();
                return true;
            }

            if (matchesShortcut(expandSelectionMenuItem, keyData))
            {
                expandSyntacticSelection();
                return true;
            }

            if (matchesShortcut(shrinkSelectionMenuItem, keyData))
            {
                shrinkSyntacticSelection();
                return true;
            }

            if (matchesShortcut(matchingDelimiterMenuItem, keyData))
            {
                goToMatchingDelimiter();
                return true;
            }

            if (matchesShortcut(copyMenuItem, keyData))
            {
                copySelectionToClipboard();
                return true;
            }

            if (matchesShortcut(cutMenuItem, keyData))
            {
                cutSelectionToClipboard();
                return true;
            }

            if (matchesShortcut(pasteMenuItem, keyData))
            {
                pasteTextFromClipboard();
                return true;
            }

            if (matchesShortcut(toggleLineCommentMenuItem, keyData) ||
                (toggleLineCommentMenuItem.ShortcutKeys == (Keys.Control | Keys.OemQuestion) &&
                 keyData == (Keys.Control | Keys.Shift | Keys.D7)))
            {
                toggleLineComment();
                return true;
            }

            if (matchesShortcut(moveLinesUpMenuItem, keyData))
            {
                moveLines(true);
                return true;
            }

            if (matchesShortcut(moveLinesDownMenuItem, keyData))
            {
                moveLines(false);
                return true;
            }

            if (matchesShortcut(deleteLinesMenuItem, keyData))
            {
                deleteLines();
                return true;
            }

            if (shouldSuppressDisplacedNativeShortcut(keyData)) return true;

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
                if (textBox.SelectionLength > 0) trySetClipboardText(textBox.SelectedText);
                return true;
            }
            if (keyData == (Keys.Control | Keys.X) || keyData == (Keys.Shift | Keys.Delete))
            {
                if (!textBox.ReadOnly && textBox.SelectionLength > 0 && trySetClipboardText(textBox.SelectedText))
                    textBox.SelectedText = string.Empty;
                return true;
            }
            if (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert))
            {
                if (!textBox.ReadOnly && tryGetClipboardText(out string text)) textBox.SelectedText = text;
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
            if (changedEditor == null) return;
            // RichTextBox.Text crosses the native RichEdit boundary and copies the
            // complete document. Keep one snapshot for the synchronous edit path:
            // large documents previously paid for the same copy several times per key.
            string currentSource = changedEditor.Text;
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
                    bool wasDirty = changedView.Buffer.Session.IsDirty;
                    bool sourceChanged = !string.Equals(previousSource, currentSource,
                        StringComparison.Ordinal);
                    bool breakpointsChanged = sourceChanged &&
                        changedView.Buffer.BreakpointLines.Count > 0;
                    if (sourceChanged)
                    {
                        changedView.LastEditPosition = Math.Min(changedEditor.SelectionStart,
                            changedEditor.TextLength);
                        changedView.LastEditUtc = getUtcNow?.Invoke() ?? DateTime.UtcNow;
                        markCrashRecoveryChanged();
                        if (ReferenceEquals(changedEditor, richTextBoxMainEditor))
                            updateSnippetSession(previousSource, currentSource);
                        if (breakpointsChanged)
                        {
                            DdfBreakpointRemapResult remap = DdfBreakpointService.Remap(
                                previousSource, currentSource, changedView.Buffer.BreakpointLines);
                            changedView.Buffer.BreakpointLines.Clear();
                            foreach (int line in remap.Lines)
                                changedView.Buffer.BreakpointLines.Add(line);
                            changedView.Buffer.UnboundBreakpointLines.Clear();
                            foreach (int line in remap.UnboundLines)
                                changedView.Buffer.UnboundBreakpointLines.Add(line);
                        }
                        changedView.Buffer.UpdateSource(currentSource);
                        updateWorkspaceDocument(changedView.Buffer.Session.CurrentPath,
                            currentSource);
                    }
                    else if (!changedView.Buffer.Session.IsDirty)
                    {
                        // RichEdit can raise TextChanged after applying character
                        // formatting even though the source is byte-for-byte
                        // unchanged. Such a native notification must not turn a
                        // freshly opened document into an unsaved one.
                        changedEditor.Modified = false;
                    }
                    bool dirtyStateChanged = wasDirty != changedView.Buffer.Session.IsDirty;
                    if (dirtyStateChanged) updateDocumentTab(changedView);
                    if (breakpointsChanged) refreshBreakpointsPalette();
                    if (ReferenceEquals(changedEditor, richTextBoxMainEditor) && dirtyStateChanged)
                        updateDocumentUi();
                }
            }

            if (!ReferenceEquals(changedEditor, richTextBoxMainEditor)) return;

            updateLineNumbersFromSource(currentSource);
            overviewDiagnosticsSource = string.Empty;
            refreshEditorOverviewFromSource(currentSource);
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
            long languageRevision = editorLanguageSession.Revision;
            string text = richTextBoxMainEditor.Text;
            DdfLexUpdate lexUpdate = incrementalLexer.Update(text);
            int changedStart = lexUpdate.RelexStart;
            DdfEditorAnalysisRequest request = createHostAnalysisRequest(
                text, richTextBoxMainEditor.SelectionStart, version, getWorkspaceTypeRoots(), lexUpdate.Result);
            var coreStopwatch = System.Diagnostics.Stopwatch.StartNew();
            DdfEditorAnalysisSnapshot snapshot = panelEditor.AnalysisService.Analyze(request);
            coreStopwatch.Stop();
            lastAnalysisCoreMilliseconds = coreStopwatch.Elapsed.TotalMilliseconds;
            applyAnalysisSnapshot(snapshot, changedStart, version, languageRevision);
        }

        private async void startBackgroundAnalysis()
        {
            if (isApplyingHighlighting || IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed) return;
            IDdfEditorAnalysisService analysisService = panelEditor?.AnalysisService;
            if (analysisService == null) return;

            string text = richTextBoxMainEditor.Text;
            IReadOnlyList<CompilationUnitSyntax> externalRoots = getWorkspaceTypeRoots();
            long languageRevision = editorLanguageSession.Revision;
            long version = ++analysisRequestVersion;
            DdfEditorAnalysisRequest request = createHostAnalysisRequest(
                text, richTextBoxMainEditor.SelectionStart, version, externalRoots);
            cancelBackgroundAnalysis();
            var cancellation = new CancellationTokenSource();
            analysisCancellation = cancellation;

            try
            {
                var coreStopwatch = System.Diagnostics.Stopwatch.StartNew();
                DdfEditorAnalysisSnapshot snapshot = await analysisService.RequestAsync(
                    request, TimeSpan.Zero, cancellation.Token);
                coreStopwatch.Stop();
                lastAnalysisCoreMilliseconds = coreStopwatch.Elapsed.TotalMilliseconds;
                if (cancellation.IsCancellationRequested || version != analysisRequestVersion ||
                    !analysisService.IsCurrent(snapshot) ||
                    languageRevision != editorLanguageSession.Revision) return;
                postEditorCallback(() =>
                {
                    if (version != analysisRequestVersion || IsDisposed || Disposing ||
                        languageRevision != editorLanguageSession.Revision ||
                        richTextBoxMainEditor.IsDisposed ||
                        !string.Equals(text, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;
                    int formatStart = findChangedLineStart(lastAnalyzedText, text);
                    incrementalLexer.Reset();
                    applyAnalysisSnapshot(snapshot, formatStart, version, languageRevision);
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

        private DdfEditorAnalysisRequest createHostAnalysisRequest(
            string text, int position, long fallbackVersion,
            IReadOnlyList<CompilationUnitSyntax> externalRoots,
            DdfLexResult precomputedLexResult = null)
        {
            IDdfEditorDocument document = openDocuments.ActiveDocumentInfo;
            return new DdfEditorAnalysisRequest(
                text,
                Math.Max(0, Math.Min(position, text.Length)),
                document?.Version ?? fallbackVersion,
                editorLanguageSession.ActivePackage,
                editorLanguageSession.Revision,
                document?.Id,
                document?.FilePath,
                false,
                externalRoots,
                getWorkspaceExternalSymbols(),
                null,
                false,
                precomputedLexResult,
                panelEditor.HostCatalog?.Snapshot);
        }

        private void applyAnalysisSnapshot(
            DdfEditorAnalysisSnapshot snapshot, int changedStart, long version, long languageRevision)
        {
            if (snapshot == null || version != analysisRequestVersion ||
                languageRevision != editorLanguageSession.Revision ||
                !string.Equals(snapshot.Source, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return;

            var applyStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            lastAnalysisProfile = snapshot.Profile;
            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;
            string text = snapshot.Source;
            IReadOnlyList<ClassifiedSpan> functionColorSpans =
                DdfFunctionSyntaxClassifier.Classify(text, snapshot.LexResult,
                    snapshot.ParseResult, snapshot.SemanticModel,
                    snapshot.Request.LanguagePackage.Language,
                    snapshot.Request.ExternalRoots, snapshot.Request.HostCatalog);
            validateBreakpoints(openDocuments.ActiveDocument, text, snapshot.ParseResult.Root);
            if (documentSession.IsDirty)
                updateWorkspaceDocument(documentSession.CurrentPath, text,
                    snapshot.ParseResult.Root, snapshot.Symbols);
            lastAnalysisPreparationMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;

            // Consume the normalized diagnostic set produced by the shared analysis service.
            // Reassembling parser/IR diagnostics here used to reintroduce generic DDFG101
            // duplicates that the analysis layer had already suppressed in favour of DDF102.
            var allDiagnostics = snapshot.Diagnostics
                .Where(diagnostic => !isResolvedByWorkspace(diagnostic, text))
                .ToList();
            allDiagnostics.Sort((left, right) => left.Start.CompareTo(right.Start));
            int currentDiagnosticStart = allDiagnostics.Count == 0
                ? int.MaxValue
                : allDiagnostics[0].Start;
            DdfDelimiterMatch delimiterToClear = activeDelimiterMatch;
            DocumentView formattingView = activeDocumentView;
            string syntaxFormattingStyle = createSyntaxFormattingStyle();
            bool syntaxFormattingCurrent = formattingView != null &&
                formattingView.MultiSelections.Count < 2 &&
                formattingView.SyntaxFormattedLanguageRevision == languageRevision &&
                string.Equals(formattingView.SyntaxFormattedSource, text,
                    StringComparison.Ordinal) &&
                string.Equals(formattingView.SyntaxFormattingStyle,
                    syntaxFormattingStyle, StringComparison.Ordinal);
            bool syntaxStyleChanged = formattingView == null ||
                formattingView.SyntaxFormattedLanguageRevision != languageRevision ||
                !string.Equals(formattingView.SyntaxFormattingStyle,
                    syntaxFormattingStyle, StringComparison.Ordinal);
            // Diagnostic waves are cleared independently below. An early
            // diagnostic must not force a native token-by-token recolor of the
            // unchanged prefix of a large document.
            int formatStart = syntaxStyleChanged ? 0 : changedStart;
            formatStart = Math.Min(formatStart, text.Length);
            diagnosticsFormatStart = currentDiagnosticStart;
            activeDelimiterMatch = null;
            lastAnalysisFormatStart = formatStart;
            lastAnalysisUsedRtf = !syntaxFormattingCurrent && formatStart == 0 &&
                !richTextBoxMainEditor.CanUndo && !richTextBoxMainEditor.CanRedo;
            lastSyntaxRtfBuildMilliseconds = 0;
            lastSyntaxRtfStreamMilliseconds = 0;

            bool editorModified = richTextBoxMainEditor.Modified;
            isApplyingHighlighting = true;
            phaseStopwatch.Restart();
            try
            {
                using (RichTextBoxUpdateScope.Begin(richTextBoxMainEditor))
                {
                    if (delimiterToClear != null)
                    {
                        restoreSourceBackground(delimiterToClear.OpenStart);
                        restoreSourceBackground(delimiterToClear.CloseStart);
                    }
                    if (syntaxFormattingCurrent)
                    {
                        // The native control already contains the exact syntax
                        // projection for this document, language and visual
                        // style. Diagnostics remain independent and are
                        // cleared/reapplied below.
                        if (formattingView.HasDiagnosticDecorations)
                        {
                            richTextBoxMainEditor.Select(0, text.Length);
                            RichTextBoxDiagnosticDecoration.ClearSelection(
                                richTextBoxMainEditor);
                        }
                    }
                    else if (lastAnalysisUsedRtf)
                    {
                        // A complete RTF stream replaces every prior character
                        // format. Clearing diagnostic waves before this branch
                        // would format the whole document twice.
                        RichTextBoxSyntaxRtf.Apply(richTextBoxMainEditor, text,
                            snapshot.LexResult.Tokens, editorDefaultForeground,
                            getColor, functionColorSpans);
                        lastSyntaxRtfBuildMilliseconds =
                            RichTextBoxSyntaxRtf.LastBuildMilliseconds;
                        lastSyntaxRtfStreamMilliseconds =
                            RichTextBoxSyntaxRtf.LastStreamMilliseconds;
                        if (formattingView?.HasDiagnosticDecorations == true)
                        {
                            // RichEdit may preserve a native wave underline even
                            // when the replacement RTF starts with \ulnone. Avoid
                            // this full-document native pass on a clean first load.
                            richTextBoxMainEditor.Select(0, text.Length);
                            RichTextBoxDiagnosticDecoration.ClearSelection(
                                richTextBoxMainEditor);
                        }
                    }
                    else
                    {
                        // Undo may restore character formatting together with
                        // text, so the incremental branch must explicitly
                        // remove stale waves before recoloring the changed tail.
                        richTextBoxMainEditor.Select(0, text.Length);
                        RichTextBoxDiagnosticDecoration.ClearSelection(
                            richTextBoxMainEditor);
                        RichTextBoxCharacterFormatting.ApplyColors(
                            richTextBoxMainEditor, formatStart, text.Length - formatStart,
                            editorDefaultForeground, richTextBoxMainEditor.BackColor);
                        foreach (DdfToken token in snapshot.LexResult.Tokens)
                        {
                            if (token.End <= formatStart) continue;

                            SyntaxKind? kind = DdfSyntaxClassifier.ToSyntaxKind(token.Kind);
                            if (kind.HasValue)
                            {
                                RichTextBoxCharacterFormatting.ApplyTextColor(
                                    richTextBoxMainEditor, token.Start, token.Length,
                                    getColor(kind.Value));
                            }
                        }
                        foreach (ClassifiedSpan span in functionColorSpans)
                        {
                            if (span.Start + span.Length <= formatStart) continue;
                            RichTextBoxCharacterFormatting.ApplyTextColor(
                                richTextBoxMainEditor, span.Start, span.Length,
                                getColor(SyntaxKind.Function));
                        }
                    }

                    foreach (DdfDiagnostic diagnostic in allDiagnostics)
                    {
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
                if (formattingView != null)
                {
                    formattingView.SyntaxFormattedSource = text;
                    formattingView.SyntaxFormattedLanguageRevision = languageRevision;
                    formattingView.SyntaxFormattingStyle = syntaxFormattingStyle;
                    formattingView.HasDiagnosticDecorations = allDiagnostics.Any(
                        diagnostic => diagnostic.Length > 0);
                }
                // A complete RTF assignment resets RichEdit's control-level zoom
                // after the document stream is loaded. Reapply the persisted
                // editor setting once the native update scope has completed.
                richTextBoxMainEditor.ZoomFactor = editorSettings.ZoomPercent / 100F;
            }
            finally
            {
                if (!richTextBoxMainEditor.IsDisposed)
                    richTextBoxMainEditor.Modified = editorModified;
                isApplyingHighlighting = false;
            }
            lastAnalysisFormattingMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;

            phaseStopwatch.Restart();
            overviewDiagnosticsSource = text;
            updateDiagnostics(allDiagnostics);
            updateOutline(snapshot.Symbols);
            lastLexResult = snapshot.LexResult;
            lastAnalyzedText = text;
            lastFunctionColorSpans = functionColorSpans;
            lastFunctionColorSource = text;
            lastParseResult = snapshot.ParseResult;
            lastSemanticModel = snapshot.SemanticModel;
            lastTypeCheckResult = snapshot.TypeCheckResult;
            updateFoldingRanges(snapshot.FoldingRanges);
            analysisAppliedVersion = version;
            analysisAppliedLanguageRevision = languageRevision;
            scheduleFlowchartSnapshot(text, snapshot.IntermediateRepresentation, version,
                snapshot.ParseResult.Diagnostics.Concat(snapshot.IntermediateRepresentation.Diagnostics));
            updateBreadcrumb();
            refreshEditorOverviewViewport();
            refreshDelimiterHighlight();
            updateNavigationCommandState();
            lastAnalysisUiServicesMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
            applyStopwatch.Stop();
            lastAnalysisApplyMilliseconds = applyStopwatch.Elapsed.TotalMilliseconds;
        }

        private string createSyntaxFormattingStyle()
        {
            var signature = new StringBuilder();
            signature.Append(richTextBoxMainEditor.Font.Name).Append('|')
                .Append(richTextBoxMainEditor.Font.SizeInPoints).Append('|')
                .Append(editorDefaultForeground.ToArgb()).Append('|')
                .Append(richTextBoxMainEditor.BackColor.ToArgb());
            foreach (SyntaxKind kind in Enum.GetValues(typeof(SyntaxKind)))
                signature.Append('|').Append((int)kind).Append(':')
                    .Append(getColor(kind).ToArgb());
            return signature.ToString();
        }

        private void cancelBackgroundAnalysis()
        {
            analysisCancellation?.Cancel();
            panelEditor.AnalysisService?.Cancel();
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


        private void updateOutline(IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            string signature = createOutlineRenderSignature(symbols);
            if (string.Equals(outlineRenderSignature, signature, StringComparison.Ordinal)) return;
            outlineRenderSignature = signature;
            outlineRenderCount++;
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

        private static string createOutlineRenderSignature(IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            var signature = new StringBuilder();
            appendOutlineRenderSignature(signature, symbols ?? Array.Empty<DdfDocumentSymbol>());
            return signature.ToString();
        }

        private static void appendOutlineRenderSignature(StringBuilder signature,
            IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            foreach (DdfDocumentSymbol symbol in symbols)
            {
                signature.Append((int)symbol.Kind).Append(':').Append(symbol.Start).Append(':')
                    .Append(symbol.Length).Append(':').Append(symbol.SelectionStart).Append(':')
                    .Append(symbol.SelectionLength).Append(':').Append(symbol.Name).Append(':')
                    .Append(symbol.Detail).Append(':').Append(symbol.IsReadOnly).Append(':')
                    .Append(symbol.IsFixed).Append('|');
                appendOutlineRenderSignature(signature, symbol.Children);
                signature.Append(';');
            }
        }

        private static TreeNode createOutlineNode(DdfDocumentSymbol symbol)
        {
            string mutability = symbol.IsFixed ? "  [fixed]" :
                symbol.IsReadOnly ? "  [sola lettura]" : string.Empty;
            var node = new TreeNode(getSymbolPrefix(symbol.Kind) + " " + symbol +
                mutability)
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

        private Color getColor(SyntaxKind kind)
        {
            if (editorSettings?.Theme == EditorTheme.Light)
            {
                switch (kind)
                {
                    case SyntaxKind.Comment: return Color.FromArgb(0, 128, 0);
                    case SyntaxKind.Library: return Color.FromArgb(121, 94, 38);
                    case SyntaxKind.String: return Color.FromArgb(163, 21, 21);
                    case SyntaxKind.Grammar: return Color.FromArgb(0, 0, 255);
                    case SyntaxKind.Number: return Color.FromArgb(9, 134, 88);
                    case SyntaxKind.DataType: return Color.FromArgb(38, 127, 153);
                    case SyntaxKind.Function: return Color.FromArgb(121, 94, 38);
                    case SyntaxKind.ControlFlow: return Color.FromArgb(175, 0, 219);
                    case SyntaxKind.Error: return Color.FromArgb(200, 0, 0);
                    default: return Color.FromArgb(32, 32, 32);
                }
            }
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
                        richTextBoxMainEditor.Text, range.Start, range.Length,
                        editorSettings.IndentSize, editorSettings.UseTabs,
                        editorLanguageSession.ActivePackage.Language.Formatting));
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
                    e.Shift,
                    editorSettings.IndentSize,
                    editorSettings.UseTabs));
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                applyEdit(EditorEditing.CreateNewLineEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength,
                    editorSettings.IndentSize,
                    editorSettings.UseTabs,
                    editorLanguageSession.ActivePackage.Language.Formatting));
            }
            else if (e.KeyCode == Keys.Back && richTextBoxMainEditor.SelectionLength == 0)
            {
                EditorEdit edit = EditorEditing.CreatePairedBackspaceEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength,
                    editorLanguageSession.ActivePackage.Language.Formatting);
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
                    richTextBoxMainEditor.Text, range.Start, range.Length, e.KeyChar,
                    editorLanguageSession.ActivePackage.Language.Formatting) ??
                    new EditorEdit(range.Start, range.Length, e.KeyChar.ToString(), range.Start + 1, 0));
                return;
            }

            EditorEdit pairedEdit = EditorEditing.CreatePairedCharacterEdit(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength,
                e.KeyChar,
                editorLanguageSession.ActivePackage.Language.Formatting);
            if (pairedEdit != null)
            {
                e.Handled = true;
                applyEdit(pairedEdit);
                return;
            }

            if (e.KeyChar == editorLanguageSession.ActivePackage.Language.Formatting.BlockClose)
            {
                e.Handled = true;
                applyEdit(EditorEditing.CreateClosingBraceEdit(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength,
                    editorLanguageSession.ActivePackage.Language.Formatting));
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
            if (isSwitchingDocument ||
                !ReferenceEquals(sender, richTextBoxMainEditor)) return;
            captureDocumentViewport(findDocumentView(sender as RichTextBox));
            updateLineNumbers();
            refreshEditorOverviewViewport();
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
            updateBreadcrumb();
            refreshEditorOverviewViewport();
            refreshFlowchartForCaret();
            refreshEditorCommandCatalog();
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
            refreshEditorOverview();
            string signature = createDiagnosticsRenderSignature(activeDiagnostics);
            if (string.Equals(diagnosticsRenderSignature, signature, StringComparison.Ordinal)) return;
            diagnosticsRenderSignature = signature;
            diagnosticsRenderCount++;
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
            string domainSummary = string.Join(", ", diagnostics
                .GroupBy(item => item.Domain)
                .OrderBy(group => group.Key)
                .Select(group => DdfDiagnosticDomainNames.GetDisplayName(group.Key) + ": " + group.Count()));
            labelDiagnostics.Text = count == 0
                ? "Diagnostica — nessun problema"
                : "Diagnostica — " + count + (count == 1 ? " problema" : " problemi") +
                  (domainSummary.Length == 0 ? string.Empty : " (" + domainSummary + ")");
            panelDiagnostics.Visible = true;
            updateLineNumbers();
        }

        private static string createDiagnosticsRenderSignature(IReadOnlyList<DdfDiagnostic> diagnostics)
        {
            var signature = new StringBuilder();
            foreach (DdfDiagnostic diagnostic in diagnostics)
            {
                signature.Append(diagnostic.Code).Append(':').Append((int)diagnostic.Severity).Append(':')
                    .Append((int)diagnostic.Domain).Append(':')
                    .Append(diagnostic.Start).Append(':').Append(diagnostic.Length).Append(':')
                    .Append(diagnostic.Line).Append(':').Append(diagnostic.Column).Append(':')
                    .Append(diagnostic.Message).Append('|');
            }
            return signature.ToString();
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
            updateLineNumbersFromSource(null);
        }

        private void updateLineNumbersFromSource(string currentSource)
        {
            if (richTextBoxMainEditor == null || richTextBoxLineNumbers == null ||
                richTextBoxMainEditor.IsDisposed || richTextBoxLineNumbers.IsDisposed)
            {
                return;
            }

            if (richTextBoxMainEditor is MultiCursorRichTextBox visualEditor)
                visualEditor.SetBreakpointLines((activeBreakpointLines ?? new HashSet<int>()).Select(line => line - 1));

            if (richTextBoxFoldedView != null && richTextBoxFoldedView.Visible && activeFoldProjection != null)
            {
                updateFoldedLineNumbers();
                return;
            }

            if (richTextBoxMainEditor.WordWrap)
            {
                updateWrappedLineNumbers();
                return;
            }

            int firstIndex = Math.Max(0, Math.Min(richTextBoxMainEditor.TextLength,
                richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, 1))));
            int firstLine = richTextBoxMainEditor.GetLineFromCharIndex(firstIndex);
            int bottom = Math.Max(1, richTextBoxMainEditor.ClientRectangle.Height - 1);
            int lastIndex = Math.Max(0, Math.Min(richTextBoxMainEditor.TextLength,
                richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, bottom))));
            int lastLine = richTextBoxMainEditor.GetLineFromCharIndex(lastIndex);

            // RichEdit can return the previous line for a point near the lower
            // border even when the next logical line is already partially visible.
            // Extend the range from real line starts so the gutter never leaves a
            // visible source row without its number after a viewport restore.
            string visibleSource = currentSource ?? richTextBoxMainEditor.Text;
            int currentLineStart = richTextBoxMainEditor.GetFirstCharIndexFromLine(lastLine);
            int nextNewline = currentLineStart < 0
                ? -1 : visibleSource.IndexOf('\n', currentLineStart);
            int nextStart = nextNewline < 0 ? -1 : nextNewline + 1;
            while (nextStart >= 0 && nextStart <= visibleSource.Length)
            {
                int nextY = richTextBoxMainEditor.GetPositionFromCharIndex(nextStart).Y;
                if (nextY < 0 || nextY >= richTextBoxMainEditor.ClientRectangle.Height) break;
                lastLine++;
                nextNewline = visibleSource.IndexOf('\n', nextStart);
                nextStart = nextNewline < 0 ? -1 : nextNewline + 1;
            }

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

            string value = lineNumbers.ToString();
            richTextBoxLineNumbers.UpdateDisplayedLines(value, sourceLines);
        }

        private void updateWrappedLineNumbers()
        {
            int lineHeight = Math.Max(1, (int)Math.Ceiling(
                richTextBoxMainEditor.Font.GetHeight() * richTextBoxMainEditor.ZoomFactor));
            int rows = Math.Max(1, (int)Math.Ceiling(
                richTextBoxMainEditor.ClientRectangle.Height / (double)lineHeight) + 1);
            var sourceLines = Enumerable.Repeat(0, rows).ToList();
            string source = richTextBoxMainEditor.Text;
            int topCharacter = Math.Max(0, Math.Min(source.Length,
                richTextBoxMainEditor.GetCharIndexFromPosition(new Point(1, 0))));
            int searchBefore = Math.Min(source.Length - 1, topCharacter - 1);
            int lineStart = searchBefore < 0 ? 0 : source.LastIndexOf('\n', searchBefore) + 1;
            int sourceLine = 1;
            for (int index = 0; index < lineStart; index++)
                if (source[index] == '\n') sourceLine++;

            // RichTextBox.GetLineFromCharIndex counts visual wrapped rows. Source numbers must
            // instead follow actual newline characters, querying RichEdit only for their Y.
            while (lineStart <= source.Length)
            {
                int y = richTextBoxMainEditor.GetPositionFromCharIndex(lineStart).Y;
                if (y >= -lineHeight / 2 && y < richTextBoxMainEditor.ClientRectangle.Height + lineHeight / 2)
                {
                    int row = Math.Max(0, Math.Min(rows - 1,
                        (int)Math.Round(y / (double)lineHeight, MidpointRounding.AwayFromZero)));
                    while (row < sourceLines.Count && sourceLines[row] != 0) row++;
                    if (row < sourceLines.Count)
                    {
                        sourceLines[row] = sourceLine;
                    }
                    else if (y >= 0 && y < richTextBoxMainEditor.ClientRectangle.Height)
                    {
                        // A trailing empty logical line can share RichEdit's last
                        // visual row. Preserve its identity in the gutter metadata
                        // and give it a following row instead of silently dropping it.
                        sourceLines.Add(sourceLine);
                    }
                }
                else if (y >= richTextBoxMainEditor.ClientRectangle.Height + lineHeight / 2) break;

                int newline = source.IndexOf('\n', lineStart);
                if (newline < 0) break;
                lineStart = newline + 1;
                sourceLine++;
            }

            var lineNumbers = new StringBuilder();
            foreach (int visibleSourceLine in sourceLines)
            {
                if (visibleSourceLine > 0) lineNumbers.Append(formatLineNumber(visibleSourceLine));
                lineNumbers.Append('\n');
            }
            richTextBoxLineNumbers.UpdateDisplayedLines(lineNumbers.ToString(), sourceLines);
        }
    }
}
