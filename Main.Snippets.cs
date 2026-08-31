using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private SnippetSession activeSnippetSession;

        private void insertSnippet(DdfSnippetTemplate template, int start, int length)
        {
            string source = richTextBoxMainEditor.Text;
            DdfSnippetExpansion expansion = DdfSnippetService.Expand(
                template,
                DdfSnippetService.GetLineIndent(source, start));

            isApplyingCompletion = true;
            try
            {
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.SelectedText = expansion.Text;
                activeSnippetSession = new SnippetSession(start, expansion);
                if (activeSnippetSession.Placeholders.Count == 0)
                {
                    richTextBoxMainEditor.Select(activeSnippetSession.FinalCaret, 0);
                    activeSnippetSession = null;
                }
                else
                {
                    selectActiveSnippetPlaceholder();
                }
                richTextBoxMainEditor.Focus();
            }
            finally
            {
                isApplyingCompletion = false;
            }
        }

        private bool handleSnippetKeyDown(KeyEventArgs e)
        {
            if (activeSnippetSession == null) return false;
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                hideCompletion();
                hideSignatureHelp();
                cancelSnippetSession(true);
                return true;
            }

            if (e.KeyCode != Keys.Tab) return false;
            e.SuppressKeyPress = true;
            e.Handled = true;
            if (e.Shift)
            {
                activeSnippetSession.MovePrevious();
                selectActiveSnippetPlaceholder();
            }
            else if (activeSnippetSession.MoveNext())
            {
                selectActiveSnippetPlaceholder();
            }
            else
            {
                int finalCaret = Math.Min(activeSnippetSession.FinalCaret, richTextBoxMainEditor.TextLength);
                activeSnippetSession = null;
                richTextBoxMainEditor.Select(finalCaret, 0);
            }
            return true;
        }

        private void selectActiveSnippetPlaceholder()
        {
            if (activeSnippetSession == null) return;
            MutableSnippetPlaceholder placeholder = activeSnippetSession.ActivePlaceholder;
            int start = Math.Min(placeholder.Start, richTextBoxMainEditor.TextLength);
            int length = Math.Min(placeholder.Length, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
        }

        private void updateSnippetSession(string previousText, string currentText)
        {
            if (activeSnippetSession == null || isApplyingCompletion ||
                string.Equals(previousText, currentText, StringComparison.Ordinal)) return;
            if (!activeSnippetSession.ApplyChange(previousText, currentText)) activeSnippetSession = null;
        }

        private void cancelSnippetSession(bool collapseSelection)
        {
            activeSnippetSession = null;
            if (collapseSelection && richTextBoxMainEditor.SelectionLength > 0)
                richTextBoxMainEditor.Select(richTextBoxMainEditor.SelectionStart + richTextBoxMainEditor.SelectionLength, 0);
        }

        private sealed class SnippetSession
        {
            private int activeIndex;

            public SnippetSession(int insertionStart, DdfSnippetExpansion expansion)
            {
                Placeholders = expansion.Placeholders
                    .Select(placeholder => new MutableSnippetPlaceholder(
                        placeholder.Index,
                        insertionStart + placeholder.Start,
                        placeholder.Length))
                    .ToList();
                FinalCaret = insertionStart + expansion.FinalCaret;
            }

            public List<MutableSnippetPlaceholder> Placeholders { get; }
            public int FinalCaret { get; private set; }
            public MutableSnippetPlaceholder ActivePlaceholder => Placeholders[activeIndex];

            public bool MoveNext()
            {
                if (activeIndex + 1 >= Placeholders.Count) return false;
                activeIndex++;
                return true;
            }

            public void MovePrevious()
            {
                if (activeIndex > 0) activeIndex--;
            }

            public bool ApplyChange(string previousText, string currentText)
            {
                int prefix = 0;
                int shared = Math.Min(previousText.Length, currentText.Length);
                while (prefix < shared && previousText[prefix] == currentText[prefix]) prefix++;
                int suffix = 0;
                while (suffix < previousText.Length - prefix && suffix < currentText.Length - prefix &&
                       previousText[previousText.Length - suffix - 1] == currentText[currentText.Length - suffix - 1])
                    suffix++;

                int oldChangeEnd = previousText.Length - suffix;
                int delta = currentText.Length - previousText.Length;
                MutableSnippetPlaceholder active = ActivePlaceholder;
                if (prefix < active.Start || prefix > active.End || oldChangeEnd > active.End) return false;

                active.Length = Math.Max(0, active.Length + delta);
                for (int index = activeIndex + 1; index < Placeholders.Count; index++)
                    Placeholders[index].Start += delta;
                if (FinalCaret >= oldChangeEnd) FinalCaret += delta;
                return true;
            }
        }

        private sealed class MutableSnippetPlaceholder
        {
            public MutableSnippetPlaceholder(int index, int start, int length)
            {
                Index = index;
                Start = start;
                Length = length;
            }

            public int Index { get; }
            public int Start { get; set; }
            public int Length { get; set; }
            public int End => Start + Length;
        }
    }
}
