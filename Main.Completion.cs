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
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10F),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24,
                Size = new Size(560, 192),
                TabStop = false
            };
            completionListBox.DrawItem += completionListBox_DrawItem;
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

        private void completionListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= completionListBox.Items.Count) return;
            var item = completionListBox.Items[e.Index] as DdfCompletionItem;
            if (item == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color foreground = selected ? SystemColors.HighlightText : AppTheme.Text;
            Color muted = selected ? SystemColors.HighlightText : AppTheme.MutedText;
            Color glyph = selected ? SystemColors.HighlightText : getCompletionKindColor(item.Kind);
            int top = e.Bounds.Top + 3;
            TextRenderer.DrawText(e.Graphics, item.Glyph, completionListBox.Font,
                new Rectangle(e.Bounds.Left + 6, top, 20, e.Bounds.Height - 4), glyph,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, item.DisplayText, completionListBox.Font,
                new Rectangle(e.Bounds.Left + 30, top, 150, e.Bounds.Height - 4), foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, item.CategoryLabel, completionListBox.Font,
                new Rectangle(e.Bounds.Left + 184, top, 92, e.Bounds.Height - 4), muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, string.IsNullOrEmpty(item.TypeName) ? "—" : item.TypeName, completionListBox.Font,
                new Rectangle(e.Bounds.Left + 280, top, 110, e.Bounds.Height - 4), muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, item.Origin, completionListBox.Font,
                new Rectangle(e.Bounds.Left + 394, top, Math.Max(10, e.Bounds.Width - 400), e.Bounds.Height - 4), muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            e.DrawFocusRectangle();
        }

        private static Color getCompletionKindColor(DdfCompletionKind kind)
        {
            switch (kind)
            {
                case DdfCompletionKind.Function: return Color.FromArgb(180, 120, 35);
                case DdfCompletionKind.Type:
                case DdfCompletionKind.Structure: return Color.FromArgb(0, 120, 110);
                case DdfCompletionKind.Keyword: return Color.FromArgb(80, 90, 180);
                case DdfCompletionKind.Boolean: return Color.FromArgb(145, 75, 150);
                case DdfCompletionKind.Library: return Color.FromArgb(180, 100, 20);
                default: return Color.FromArgb(35, 100, 170);
            }
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
                includeAll,
                externalItems: getWorkspaceCompletionItems(),
                externalRoots: getWorkspaceTypeRoots());
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
            Point editorOrigin = panelEditor.PointToClient(richTextBoxMainEditor.PointToScreen(Point.Empty));
            int x = editorOrigin.X + caret.X;
            int y = editorOrigin.Y + caret.Y + richTextBoxMainEditor.Font.Height + 4;
            x = Math.Max(3, Math.Min(x, panelEditor.ClientSize.Width - completionListBox.Width - 3));
            if (y + completionListBox.Height > panelEditor.ClientSize.Height)
            {
                y = editorOrigin.Y + caret.Y - completionListBox.Height - 3;
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
