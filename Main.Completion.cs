using System;
using System.Drawing;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ListBox completionListBox;
        private Timer completionTimer;
        private DdfCompletionResult activeCompletionResult;
        private bool isApplyingCompletion;

        private void initializeCompletion()
        {
            completionListBox = new ListBox
            {
                Name = "completionListBox",
                Visible = false,
                IntegralHeight = false,
                BackColor = Color.FromArgb(37, 37, 38),
                ForeColor = Color.FromArgb(212, 212, 212),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10F),
                ItemHeight = 20,
                Size = new Size(360, 160),
                TabStop = false
            };
            completionListBox.DoubleClick += (sender, args) => acceptSelectedCompletion();
            panelEditor.Controls.Add(completionListBox);
            completionListBox.BringToFront();

            completionTimer = new Timer { Interval = 90 };
            completionTimer.Tick += (sender, args) =>
            {
                completionTimer.Stop();
                showCompletion(false);
            };
        }

        private void completionMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
            richTextBoxMainEditor.Focus();
            showCompletion(true);
        }

        private void disposeCompletion()
        {
            if (completionTimer == null) return;
            completionTimer.Stop();
            completionTimer.Dispose();
        }

        private void scheduleCompletion()
        {
            if (completionTimer == null || isReplacingDocument || isApplyingCompletion ||
                richTextBoxFoldedView.Visible || richTextBoxMainEditor.SelectionLength > 0 ||
                !richTextBoxMainEditor.Focused)
            {
                hideCompletion();
                return;
            }

            completionTimer.Stop();
            completionTimer.Start();
        }

        private void showCompletion(bool includeAll)
        {
            if (IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed ||
                richTextBoxFoldedView.Visible || richTextBoxMainEditor.SelectionLength > 0)
            {
                hideCompletion();
                return;
            }

            DdfCompletionResult result = DdfCompletionService.GetCompletions(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                includeAll);
            if (result.Items.Count == 0)
            {
                hideCompletion();
                return;
            }

            activeCompletionResult = result;
            completionListBox.BeginUpdate();
            try
            {
                completionListBox.Items.Clear();
                foreach (DdfCompletionItem item in result.Items) completionListBox.Items.Add(item);
                completionListBox.SelectedIndex = 0;
            }
            finally
            {
                completionListBox.EndUpdate();
            }

            int rows = Math.Min(8, result.Items.Count);
            completionListBox.Height = Math.Max(42, rows * completionListBox.ItemHeight + 4);
            Point caret = richTextBoxMainEditor.GetPositionFromCharIndex(richTextBoxMainEditor.SelectionStart);
            int x = richTextBoxMainEditor.Left + caret.X;
            int y = richTextBoxMainEditor.Top + caret.Y + richTextBoxMainEditor.Font.Height + 4;
            x = Math.Max(3, Math.Min(x, panelEditor.ClientSize.Width - completionListBox.Width - 3));
            if (y + completionListBox.Height > panelEditor.ClientSize.Height)
            {
                y = richTextBoxMainEditor.Top + caret.Y - completionListBox.Height - 3;
            }

            completionListBox.Location = new Point(x, Math.Max(3, y));
            completionListBox.Visible = true;
            completionListBox.BringToFront();
        }

        private bool handleCompletionKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                showCompletion(true);
                return true;
            }

            if (completionListBox == null || !completionListBox.Visible) return false;
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                hideCompletion();
                return true;
            }

            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up ||
                e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                int delta = e.KeyCode == Keys.Down ? 1 :
                    e.KeyCode == Keys.Up ? -1 :
                    e.KeyCode == Keys.PageDown ? Math.Max(1, completionListBox.ClientSize.Height / completionListBox.ItemHeight - 1) :
                    -Math.Max(1, completionListBox.ClientSize.Height / completionListBox.ItemHeight - 1);
                completionListBox.SelectedIndex = Math.Max(
                    0,
                    Math.Min(completionListBox.Items.Count - 1, completionListBox.SelectedIndex + delta));
                return true;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                acceptSelectedCompletion();
                return true;
            }

            return false;
        }

        private void acceptSelectedCompletion()
        {
            var item = completionListBox.SelectedItem as DdfCompletionItem;
            DdfCompletionResult result = activeCompletionResult;
            if (item == null || result == null) return;

            int start = Math.Min(result.ReplacementStart, richTextBoxMainEditor.TextLength);
            int length = Math.Min(result.ReplacementLength, richTextBoxMainEditor.TextLength - start);
            hideCompletion();
            isApplyingCompletion = true;
            try
            {
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.SelectedText = item.InsertionText;
                richTextBoxMainEditor.Select(start + item.InsertionText.Length, 0);
                richTextBoxMainEditor.Focus();
            }
            finally
            {
                isApplyingCompletion = false;
            }
        }

        private void hideCompletion()
        {
            if (completionTimer != null) completionTimer.Stop();
            if (completionListBox != null) completionListBox.Visible = false;
            activeCompletionResult = null;
        }

        private void hideCompletionIfCaretMoved()
        {
            if (activeCompletionResult == null) return;
            int expectedCaret = activeCompletionResult.ReplacementStart + activeCompletionResult.ReplacementLength;
            if (richTextBoxMainEditor.SelectionLength > 0 || richTextBoxMainEditor.SelectionStart != expectedCaret)
            {
                hideCompletion();
            }
        }
    }
}
