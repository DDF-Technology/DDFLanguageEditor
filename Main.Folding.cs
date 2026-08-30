using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private void scheduleDelimiterHighlight()
        {
            if (delimiterHighlightTimer == null || IsDisposed || Disposing ||
                richTextBoxMainEditor == null || richTextBoxMainEditor.IsDisposed)
            {
                return;
            }

            delimiterHighlightTimer.Stop();
            if (isMouseSelecting || richTextBoxMainEditor.Capture ||
                richTextBoxMainEditor.SelectionLength > 0 || richTextBoxFoldedView.Visible)
            {
                return;
            }

            delimiterHighlightTimer.Start();
        }

        private void delimiterHighlightTimer_Tick(object sender, EventArgs e)
        {
            delimiterHighlightTimer.Stop();
            if (IsDisposed || Disposing || richTextBoxMainEditor == null || richTextBoxMainEditor.IsDisposed)
            {
                return;
            }

            if (!isMouseSelecting && !richTextBoxMainEditor.Capture &&
                richTextBoxMainEditor.SelectionLength == 0)
            {
                refreshDelimiterHighlight();
            }
        }

        private void refreshDelimiterHighlight()
        {
            if (isApplyingHighlighting || isUpdatingDelimiterHighlight || lastLexResult == null ||
                richTextBoxFoldedView.Visible ||
                isMouseSelecting || richTextBoxMainEditor.Capture ||
                richTextBoxMainEditor.SelectionLength > 0 ||
                !string.Equals(richTextBoxMainEditor.Text, lastAnalyzedText, StringComparison.Ordinal)) return;

            isUpdatingDelimiterHighlight = true;
            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;
            try
            {
                using (RichTextBoxUpdateScope.Begin(richTextBoxMainEditor))
                {
                    if (activeDelimiterMatch != null)
                    {
                        restoreSourceBackground(activeDelimiterMatch.OpenStart);
                        restoreSourceBackground(activeDelimiterMatch.CloseStart);
                    }

                    activeDelimiterMatch = DdfDelimiterMatcher.FindMatch(richTextBoxMainEditor.Text, selectionStart, lastLexResult);
                    if (activeDelimiterMatch != null)
                    {
                        setCharacterBackground(activeDelimiterMatch.OpenStart, System.Drawing.Color.FromArgb(38, 79, 120));
                        setCharacterBackground(activeDelimiterMatch.CloseStart, System.Drawing.Color.FromArgb(38, 79, 120));
                    }

                    int safeStart = Math.Min(selectionStart, richTextBoxMainEditor.TextLength);
                    int safeLength = Math.Min(selectionLength, richTextBoxMainEditor.TextLength - safeStart);
                    richTextBoxMainEditor.Select(safeStart, safeLength);
                }
            }
            finally
            {
                isUpdatingDelimiterHighlight = false;
            }
        }

        private void restoreSourceBackground(int position)
        {
            System.Drawing.Color color = richTextBoxMainEditor.BackColor;
            if (lastParseResult != null && lastParseResult.Diagnostics.Any(diagnostic => position >= diagnostic.Start && position < diagnostic.End))
            {
                color = System.Drawing.Color.FromArgb(128, 48, 48);
            }

            setCharacterBackground(position, color);
        }

        private void setCharacterBackground(int position, System.Drawing.Color color)
        {
            if (position < 0 || position >= richTextBoxMainEditor.TextLength) return;
            richTextBoxMainEditor.Select(position, 1);
            richTextBoxMainEditor.SelectionBackColor = color;
        }

        private void updateFoldingRanges(IReadOnlyList<DdfFoldingRange> ranges)
        {
            foldingRanges = ranges ?? new List<DdfFoldingRange>();
            updateFoldingCommandState();
        }

        private void viewMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            refreshFoldingRangesForCommand();
            updateFoldingCommandState();
        }

        private void updateFoldingCommandState()
        {
            if (toggleFoldMenuItem == null) return;
            if (isReplacingDocument || richTextBoxMainEditor == null || richTextBoxMainEditor.IsDisposed)
            {
                toggleFoldMenuItem.Enabled = false;
                expandAllFoldsMenuItem.Enabled = false;
                return;
            }

            if (richTextBoxFoldedView.Visible)
            {
                DdfFoldingRange foldedRange = findFoldingRangeAtPosition(getFoldedSourceCaret());
                toggleFoldMenuItem.Enabled = foldedRange != null;
                toggleFoldMenuItem.Text = foldedRange != null && collapsedFoldStarts.Contains(foldedRange.Start)
                    ? "Espandi blocco"
                    : "Comprimi blocco";
                expandAllFoldsMenuItem.Enabled = true;
                return;
            }

            DdfFoldingRange range = findFoldingRangeAtCaret();
            toggleFoldMenuItem.Enabled = range != null;
            toggleFoldMenuItem.Text = "Comprimi blocco";
            expandAllFoldsMenuItem.Enabled = false;
        }

        private void toggleFoldMenuItem_Click(object sender, EventArgs e)
        {
            refreshFoldingRangesForCommand();
            int sourceCaret = richTextBoxFoldedView.Visible
                ? getFoldedSourceCaret()
                : richTextBoxMainEditor.SelectionStart;
            DdfFoldingRange range = findFoldingRangeAtPosition(sourceCaret);
            if (range == null) return;

            if (collapsedFoldStarts.Contains(range.Start))
            {
                collapsedFoldStarts.Remove(range.Start);
            }
            else
            {
                removeOverlappingCollapsedRanges(range);
                collapsedFoldStarts.Add(range.Start);
            }

            refreshFoldedView(sourceCaret, range.Start);
        }

        private void expandAllFoldsMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
        }

        private void richTextBoxFoldedView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.M)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                if (e.Shift) leaveFoldedView();
                else toggleFoldMenuItem_Click(sender, EventArgs.Empty);
            }
        }

        private void refreshFoldedView(int sourceCaret, int preferredRangeStart)
        {
            if (collapsedFoldStarts.Count == 0)
            {
                leaveFoldedView(sourceCaret);
                return;
            }

            string source = richTextBoxMainEditor.Text;
            List<DdfFoldingRange> collapsedRanges = foldingRanges
                .Where(candidate => collapsedFoldStarts.Contains(candidate.Start))
                .OrderBy(candidate => candidate.ContentStart)
                .ToList();
            if (collapsedRanges.Count == 0)
            {
                leaveFoldedView(sourceCaret);
                return;
            }

            DdfFoldProjection projection = DdfFoldProjection.Create(source, collapsedRanges);
            activeFoldProjection = projection;
            richTextBoxFoldedView.Text = projection.Text;
            applyFoldedProjectionFormatting(projection);
            richTextBoxMainEditor.Visible = false;
            richTextBoxFoldedView.Visible = true;
            richTextBoxLineNumbers.Visible = true;
            richTextBoxFoldedView.BringToFront();
            DdfFoldMarker preferredMarker = projection.Markers
                .FirstOrDefault(marker => marker.SourceRangeStart == preferredRangeStart);
            int projectedCaret;
            if (preferredMarker != null)
            {
                projectedCaret = Math.Min(preferredMarker.ProjectedStart + 1, projection.Text.Length);
            }
            else if (!projection.TryProjectPosition(sourceCaret, out projectedCaret))
            {
                projectedCaret = projection.MarkerStart;
            }

            richTextBoxFoldedView.Select(Math.Min(projectedCaret, richTextBoxFoldedView.TextLength), 0);
            richTextBoxFoldedView.Focus();
            updateFoldedLineNumbers();
            updateFoldingCommandState();
        }

        private void leaveFoldedView()
        {
            leaveFoldedView(collapsedFoldStarts.Count == 0 ? 0 : collapsedFoldStarts.First());
        }

        private void leaveFoldedView(int sourcePosition)
        {
            if (richTextBoxFoldedView == null || !richTextBoxFoldedView.Visible)
            {
                collapsedFoldStarts.Clear();
                return;
            }

            richTextBoxFoldedView.Visible = false;
            richTextBoxFoldedView.Text = string.Empty;
            activeFoldProjection = null;
            richTextBoxMainEditor.Visible = true;
            richTextBoxLineNumbers.Visible = true;
            collapsedFoldStarts.Clear();
            richTextBoxMainEditor.Select(Math.Min(sourcePosition, richTextBoxMainEditor.TextLength), 0);
            richTextBoxMainEditor.Focus();
            updateLineNumbers();
            updateFoldingCommandState();
            refreshDelimiterHighlight();
        }

        private void richTextBoxFoldedView_VScroll(object sender, EventArgs e)
        {
            updateFoldedLineNumbers();
        }

        private void richTextBoxFoldedView_SelectionChanged(object sender, EventArgs e)
        {
            updateFoldingCommandState();
        }

        private void updateFoldedLineNumbers()
        {
            if (activeFoldProjection == null || richTextBoxFoldedView == null ||
                richTextBoxLineNumbers == null || !richTextBoxFoldedView.Visible)
            {
                return;
            }

            int firstIndex = Math.Max(0, richTextBoxFoldedView.GetCharIndexFromPosition(new System.Drawing.Point(1, 1)));
            int firstLine = richTextBoxFoldedView.GetLineFromCharIndex(firstIndex);
            int bottom = Math.Max(1, richTextBoxFoldedView.ClientRectangle.Height - 1);
            int lastIndex = Math.Max(0, richTextBoxFoldedView.GetCharIndexFromPosition(new System.Drawing.Point(1, bottom)));
            int lastLine = richTextBoxFoldedView.GetLineFromCharIndex(lastIndex);
            firstLine = Math.Min(firstLine, activeFoldProjection.LineNumberLabels.Count - 1);
            lastLine = Math.Min(Math.Max(firstLine, lastLine), activeFoldProjection.LineNumberLabels.Count - 1);

            var lineNumbers = new System.Text.StringBuilder();
            var sourceLines = new System.Collections.Generic.List<int>();
            for (int line = firstLine; line <= lastLine; line++)
            {
                string label = activeFoldProjection.LineNumberLabels[line];
                int sourceLine;
                if (int.TryParse(label, out sourceLine))
                {
                    sourceLines.Add(sourceLine);
                    lineNumbers.Append(formatLineNumber(sourceLine));
                }
                else
                {
                    sourceLines.Add(0);
                    lineNumbers.Append(label);
                }
                lineNumbers.Append('\n');
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

        private DdfFoldingRange findFoldingRangeAtCaret()
        {
            return findFoldingRangeAtPosition(richTextBoxMainEditor.SelectionStart);
        }

        private DdfFoldingRange findFoldingRangeAtPosition(int caret)
        {
            string source = richTextBoxMainEditor.Text;
            DdfFoldingRange range = foldingRanges
                .Where(candidate => containsFoldActivationPosition(source, candidate, caret))
                .OrderBy(candidate => candidate.Length)
                .FirstOrDefault();
            if (range != null || caret == 0) return range;

            return foldingRanges
                .Where(candidate => containsFoldActivationPosition(source, candidate, caret - 1))
                .OrderBy(candidate => candidate.Length)
                .FirstOrDefault();
        }

        private int getFoldedSourceCaret()
        {
            if (activeFoldProjection == null || richTextBoxFoldedView == null) return 0;
            return activeFoldProjection.TryMapProjectedPosition(
                richTextBoxFoldedView.SelectionStart,
                out int sourcePosition,
                out _) ? sourcePosition : 0;
        }

        private void removeOverlappingCollapsedRanges(DdfFoldingRange range)
        {
            int rangeEnd = range.ContentStart + range.ContentLength;
            foreach (DdfFoldingRange collapsed in foldingRanges
                .Where(candidate => collapsedFoldStarts.Contains(candidate.Start))
                .ToList())
            {
                int collapsedEnd = collapsed.ContentStart + collapsed.ContentLength;
                if (range.ContentStart < collapsedEnd && collapsed.ContentStart < rangeEnd)
                {
                    collapsedFoldStarts.Remove(collapsed.Start);
                }
            }
        }

        private void refreshFoldingRangesForCommand()
        {
            string source = richTextBoxMainEditor.Text;
            if (string.Equals(source, lastAnalyzedText, StringComparison.Ordinal) && lastParseResult != null)
            {
                return;
            }

            DdfLexResult lexResult = DdfLexer.Lex(source);
            DdfParseResult parseResult = DdfParser.Parse(source, lexResult);
            lastLexResult = lexResult;
            lastParseResult = parseResult;
            lastAnalyzedText = source;
            foldingRanges = DdfFoldingRangeProvider.Create(parseResult.Root, source);
        }

        private void applyFoldedProjectionFormatting(DdfFoldProjection projection)
        {
            using (RichTextBoxUpdateScope.Begin(richTextBoxFoldedView))
            {
                richTextBoxFoldedView.SelectAll();
                richTextBoxFoldedView.SelectionColor = System.Drawing.Color.FromArgb(212, 212, 212);
                richTextBoxFoldedView.SelectionBackColor = richTextBoxFoldedView.BackColor;

                if (lastLexResult != null)
                {
                    foreach (DdfToken token in lastLexResult.Tokens)
                    {
                        SyntaxKind? kind = DdfSyntaxClassifier.ToSyntaxKind(token.Kind);
                        if (!kind.HasValue ||
                            !projection.TryProjectSpan(token.Start, token.Length, out int start, out int length))
                        {
                            continue;
                        }

                        richTextBoxFoldedView.Select(start, length);
                        richTextBoxFoldedView.SelectionColor = getColor(kind.Value);
                    }
                }

                if (lastParseResult != null)
                {
                    foreach (DdfDiagnostic diagnostic in lastParseResult.Diagnostics)
                    {
                        if (!projection.TryProjectSpan(diagnostic.Start, diagnostic.Length, out int start, out int length))
                        {
                            continue;
                        }

                        richTextBoxFoldedView.Select(start, length);
                        richTextBoxFoldedView.SelectionColor = System.Drawing.Color.FromArgb(241, 241, 241);
                        richTextBoxFoldedView.SelectionBackColor = System.Drawing.Color.FromArgb(90, 29, 29);
                    }
                }

                foreach (DdfFoldMarker marker in projection.Markers)
                {
                    richTextBoxFoldedView.Select(marker.ProjectedStart, marker.ProjectedLength);
                    richTextBoxFoldedView.SelectionColor = System.Drawing.Color.FromArgb(220, 220, 170);
                    richTextBoxFoldedView.SelectionBackColor = System.Drawing.Color.FromArgb(38, 38, 50);
                }
                richTextBoxFoldedView.Select(
                    Math.Min(projection.MarkerStart + projection.MarkerLength, richTextBoxFoldedView.TextLength),
                    0);
                richTextBoxFoldedView.ClearUndo();
            }
        }

        private static bool containsFoldActivationPosition(string source, DdfFoldingRange range, int position)
        {
            if (string.IsNullOrEmpty(source) || range == null ||
                range.Start < 0 || range.Start >= source.Length ||
                range.End <= range.Start || range.End > source.Length ||
                position < 0 || position >= range.End)
            {
                return false;
            }

            int activationStart = findFoldHeaderStart(source, range.Start);
            return position >= activationStart;
        }

        private static int findFoldHeaderStart(string source, int openBrace)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(0, openBrace - 1)) + 1;
            for (int index = lineStart; index < openBrace; index++)
            {
                if (!char.IsWhiteSpace(source[index]))
                {
                    return lineStart;
                }
            }

            int previousLineEnd = lineStart - 1;
            while (previousLineEnd > 0)
            {
                int previousLineStart = source.LastIndexOf('\n', previousLineEnd - 1) + 1;
                for (int index = previousLineStart; index < previousLineEnd; index++)
                {
                    if (!char.IsWhiteSpace(source[index]))
                    {
                        return previousLineStart;
                    }
                }

                previousLineEnd = previousLineStart - 1;
            }

            return lineStart;
        }

    }
}
