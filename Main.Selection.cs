using System;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private bool isApplyingSyntacticSelection;

        private void expandSyntacticSelection()
        {
            leaveFoldedView();
            DocumentView view = activeDocumentView;
            if (view == null) return;
            DdfTextRange next = DdfSelectionService.GetNextExpansion(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength);
            if (next == null) return;

            view.SelectionHistory.Push(Tuple.Create(
                richTextBoxMainEditor.SelectionStart,
                richTextBoxMainEditor.SelectionLength));
            selectSyntacticRange(next.Start, next.Length);
        }

        private void shrinkSyntacticSelection()
        {
            leaveFoldedView();
            DocumentView view = activeDocumentView;
            if (view == null || view.SelectionHistory.Count == 0) return;
            Tuple<int, int> previous = view.SelectionHistory.Pop();
            selectSyntacticRange(previous.Item1, previous.Item2);
        }

        private void goToMatchingDelimiter()
        {
            leaveFoldedView();
            int? target = DdfDelimiterNavigation.GetMatchingPosition(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart);
            if (!target.HasValue) return;
            activeDocumentView?.SelectionHistory.Clear();
            selectSyntacticRange(target.Value, 0);
        }

        private void selectSyntacticRange(int start, int length)
        {
            isApplyingSyntacticSelection = true;
            try
            {
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.ScrollToCaret();
                richTextBoxMainEditor.Focus();
            }
            finally
            {
                isApplyingSyntacticSelection = false;
            }
        }

        private void updateEditingSelectionCommandState()
        {
            if (expandSelectionMenuItem == null) return;
            bool editableView = !richTextBoxFoldedView.Visible;
            expandSelectionMenuItem.Enabled = editableView && richTextBoxMainEditor.TextLength > 0 &&
                DdfSelectionService.GetNextExpansion(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart,
                    richTextBoxMainEditor.SelectionLength) != null;
            shrinkSelectionMenuItem.Enabled = editableView && activeDocumentView != null &&
                activeDocumentView.SelectionHistory.Count > 0;
            matchingDelimiterMenuItem.Enabled = editableView &&
                DdfDelimiterNavigation.GetMatchingPosition(
                    richTextBoxMainEditor.Text,
                    richTextBoxMainEditor.SelectionStart).HasValue;
        }
    }
}
