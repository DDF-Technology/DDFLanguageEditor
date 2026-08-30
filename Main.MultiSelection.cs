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
        private static readonly Color SecondarySelectionColor = Color.FromArgb(38, 79, 120);
        private bool isApplyingMultiSelection;
        private bool isApplyingMultiEdit;

        private bool hasMultipleSelections => activeDocumentView != null &&
            activeDocumentView.MultiSelections.Count > 1;

        private void selectNextOccurrence()
        {
            leaveFoldedView();
            DocumentView view = activeDocumentView;
            if (view == null) return;
            IReadOnlyList<DdfTextRange> occurrences = DdfMultiSelectionService.FindOccurrences(
                richTextBoxMainEditor.Text, richTextBoxMainEditor.SelectionStart, richTextBoxMainEditor.SelectionLength);
            if (occurrences.Count == 0) return;

            if (view.MultiSelections.Count == 0)
            {
                bool startedFromCaret = richTextBoxMainEditor.SelectionLength == 0;
                DdfTextRange current = occurrences.FirstOrDefault(range =>
                    range.Start <= richTextBoxMainEditor.SelectionStart &&
                    richTextBoxMainEditor.SelectionStart <= range.End) ?? occurrences[0];
                view.MultiSelections.Add(current);
                setPrimaryMultiSelection(current);
                if (startedFromCaret) { refreshMultiSelectionVisuals(); return; }
            }

            DdfTextRange active = view.MultiSelections[view.MultiSelections.Count - 1];
            DdfTextRange next = occurrences.FirstOrDefault(range => range.Start >= active.End &&
                !containsMultiSelection(view.MultiSelections, range));
            if (next == null) next = occurrences.FirstOrDefault(range => !containsMultiSelection(view.MultiSelections, range));
            if (next == null) return;
            view.MultiSelections.Add(next);
            setPrimaryMultiSelection(next);
            refreshMultiSelectionVisuals();
        }

        private void selectAllOccurrences()
        {
            leaveFoldedView();
            DocumentView view = activeDocumentView;
            if (view == null) return;
            IReadOnlyList<DdfTextRange> occurrences = DdfMultiSelectionService.FindOccurrences(
                richTextBoxMainEditor.Text, richTextBoxMainEditor.SelectionStart, richTextBoxMainEditor.SelectionLength);
            if (occurrences.Count == 0) return;
            view.MultiSelections.Clear();
            view.MultiSelections.AddRange(occurrences);
            setPrimaryMultiSelection(view.MultiSelections[view.MultiSelections.Count - 1]);
            refreshMultiSelectionVisuals();
        }

        private void addCursorAtPosition(int position)
        {
            DocumentView view = activeDocumentView;
            if (view == null) return;
            position = Math.Max(0, Math.Min(position, richTextBoxMainEditor.TextLength));
            if (view.MultiSelections.Count == 0)
                view.MultiSelections.Add(new DdfTextRange(richTextBoxMainEditor.SelectionStart, richTextBoxMainEditor.SelectionLength));
            var added = new DdfTextRange(position, 0);
            if (overlapsMultiSelection(view.MultiSelections, added)) return;
            view.MultiSelections.Add(added);
            setPrimaryMultiSelection(added);
            refreshMultiSelectionVisuals();
        }

        private void clearMultipleSelections(bool refresh = true)
        {
            DocumentView view = activeDocumentView;
            if (view == null || view.MultiSelections.Count == 0) return;
            view.MultiSelections.Clear();
            if (refresh) refreshMultiSelectionVisuals();
            updateCaretPosition();
        }

        private void applyMultiReplacement(string replacement)
        {
            applyMultiEdit(DdfMultiSelectionService.Replace(
                richTextBoxMainEditor.Text, activeDocumentView.MultiSelections, replacement));
        }

        private void applyMultiBackspace()
        {
            applyMultiEdit(DdfMultiSelectionService.Backspace(
                richTextBoxMainEditor.Text, activeDocumentView.MultiSelections));
        }

        private void applyMultiDelete()
        {
            applyMultiEdit(DdfMultiSelectionService.Delete(
                richTextBoxMainEditor.Text, activeDocumentView.MultiSelections));
        }

        private void applyMultiEditorEdits(Func<DdfTextRange, EditorEdit> createEdit)
        {
            var edits = activeDocumentView.MultiSelections.Select(createEdit).ToList();
            applyMultiEdit(DdfMultiSelectionService.ApplyEdits(richTextBoxMainEditor.Text, edits));
        }

        private EditorEdit createMultiCursorTabEdit(DdfTextRange range, bool outdent)
        {
            if (!outdent)
            {
                string spaces = new string(' ', EditorEditing.DefaultTabSize);
                return new EditorEdit(range.Start, 0, spaces,
                    range.Start + spaces.Length, range.Length);
            }

            int remove = 0;
            while (remove < EditorEditing.DefaultTabSize && range.Start - remove - 1 >= 0 &&
                   richTextBoxMainEditor.Text[range.Start - remove - 1] == ' ')
                remove++;
            return new EditorEdit(range.Start - remove, remove, string.Empty,
                range.Start - remove, range.Length);
        }

        private void applyMultiEdit(DdfMultiEditResult result)
        {
            DocumentView view = activeDocumentView;
            if (view == null) return;
            isApplyingMultiEdit = true;
            isApplyingMultiSelection = true;
            try
            {
                richTextBoxMainEditor.SelectAll();
                richTextBoxMainEditor.SelectedText = result.Text;
                view.MultiSelections.Clear();
                foreach (DdfTextRange selection in result.Selections)
                    if (!containsMultiSelection(view.MultiSelections, selection)) view.MultiSelections.Add(selection);
                DdfTextRange active = view.MultiSelections[view.MultiSelections.Count - 1];
                richTextBoxMainEditor.Select(active.Start, active.Length);
            }
            finally
            {
                isApplyingMultiSelection = false;
                isApplyingMultiEdit = false;
            }
            diagnosticsFormatStart = 0;
            highlightTimer.Stop();
            highlightTimer.Start();
            updateCaretPosition();
        }

        private void setPrimaryMultiSelection(DdfTextRange range)
        {
            isApplyingMultiSelection = true;
            try { richTextBoxMainEditor.Select(range.Start, range.Length); }
            finally { isApplyingMultiSelection = false; }
            updateCaretPosition();
        }

        private void refreshMultiSelectionVisuals()
        {
            diagnosticsFormatStart = 0;
            applyHighlighting();
        }

        private void formatSecondarySelections(int formatStart)
        {
            DocumentView view = activeDocumentView;
            if (view == null || view.MultiSelections.Count < 2) return;
            for (int index = 0; index < view.MultiSelections.Count - 1; index++)
            {
                DdfTextRange range = view.MultiSelections[index];
                int length = range.Length;
                int start = range.Start;
                if (length == 0)
                {
                    if (start < richTextBoxMainEditor.TextLength) length = 1;
                    else if (start > 0) { start--; length = 1; }
                }
                if (length == 0 || start + length <= formatStart) continue;
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.SelectionBackColor = SecondarySelectionColor;
            }
        }

        private static bool containsMultiSelection(IEnumerable<DdfTextRange> selections, DdfTextRange candidate)
        {
            return selections.Any(range => range.Start == candidate.Start && range.Length == candidate.Length);
        }

        private static bool overlapsMultiSelection(IEnumerable<DdfTextRange> selections, DdfTextRange candidate)
        {
            return selections.Any(range => candidate.Length == 0
                ? range.Start <= candidate.Start && candidate.Start <= range.End
                : candidate.Start <= range.End && range.Start <= candidate.End);
        }
    }
}
