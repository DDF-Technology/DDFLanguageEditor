using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class AltCursorEventArgs : EventArgs
    {
        public AltCursorEventArgs(int position) { Position = position; }
        public int Position { get; }
    }

    internal sealed class MultiCursorRichTextBox : RichTextBox
    {
        private const int WmLeftButtonDown = 0x0201;
        private const int WmPaint = 0x000F;
        private bool paintingVisuals;
        private bool visualPaintDeferred;
        private bool visualRefreshPending;
        private string visualSource = string.Empty;
        private int debugExecutionLine = -1;
        private readonly HashSet<int> breakpointLines = new HashSet<int>();

        public event EventHandler<AltCursorEventArgs> AltCursorRequested;

        [DefaultValue(true)]
        public bool HighlightCurrentLine { get; set; } = true;

        [DefaultValue(false)]
        public bool ShowWhitespace { get; set; }

        [DefaultValue(true)]
        public bool ShowIndentGuides { get; set; } = true;

        [DefaultValue(4)]
        public int VisualIndentSize { get; set; } = 4;

        [DefaultValue(-1)]
        public int DebugExecutionLine
        {
            get => debugExecutionLine;
            set
            {
                int normalized = value < 0 ? -1 : value;
                if (debugExecutionLine == normalized) return;
                debugExecutionLine = normalized;
                Invalidate();
            }
        }

        public void SetBreakpointLines(IEnumerable<int> zeroBasedLines)
        {
            var replacement = new HashSet<int>(zeroBasedLines ?? Array.Empty<int>());
            if (breakpointLines.SetEquals(replacement)) return;
            breakpointLines.Clear();
            breakpointLines.UnionWith(replacement);
            Invalidate();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int VisualRenderCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int LastRenderedWhitespaceCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int LastRenderedIndentGuideCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BreakpointLineCount => breakpointLines.Count;

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmLeftButtonDown && (ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                int packed = unchecked((int)(long)message.LParam);
                var point = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff));
                int position = GetCharIndexFromPosition(point);
                AltCursorRequested?.Invoke(this, new AltCursorEventArgs(position));
                return;
            }
            base.WndProc(ref message);
            if (message.Msg == WmPaint && !visualPaintDeferred) drawEditorVisuals();
        }

        protected override void OnSelectionChanged(EventArgs e)
        {
            base.OnSelectionChanged(e);
            deferEditorVisualRefresh();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            visualSource = Text;
            deferEditorVisualRefresh();
        }

        public void RefreshEditorVisuals()
        {
            visualSource = Text;
            Invalidate();
        }

        private void deferEditorVisualRefresh()
        {
            visualPaintDeferred = true;
            if (IsDisposed || !IsHandleCreated)
            {
                visualPaintDeferred = false;
                return;
            }
            if (visualRefreshPending) return;
            visualRefreshPending = true;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    visualRefreshPending = false;
                    visualPaintDeferred = false;
                    if (!IsDisposed) Invalidate();
                }));
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ObjectDisposedException)
            {
                visualRefreshPending = false;
                visualPaintDeferred = false;
            }
        }

        private void drawEditorVisuals()
        {
            if (paintingVisuals || IsDisposed || !Visible || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            paintingVisuals = true;
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(Handle))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    drawBreakpointLines(graphics);
                    if (HighlightCurrentLine && SelectionLength == 0) drawCurrentLine(graphics);
                    if (DebugExecutionLine >= 0) drawDebugExecutionLine(graphics);
                    LastRenderedIndentGuideCount = ShowIndentGuides ? drawIndentGuides(graphics) : 0;
                    LastRenderedWhitespaceCount = ShowWhitespace ? drawWhitespace(graphics) : 0;
                    VisualRenderCount++;
                }
            }
            finally
            {
                paintingVisuals = false;
            }
        }

        private void drawCurrentLine(Graphics graphics)
        {
            int line = GetLineFromCharIndex(Math.Min(SelectionStart, TextLength));
            int start = GetFirstCharIndexFromLine(line);
            if (start < 0) return;
            Point position = GetPositionFromCharIndex(start);
            int height = visualLineHeight();
            if (position.Y + height < 0 || position.Y > ClientSize.Height) return;
            Color tint = isDarkBackground() ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(15, 0, 90, 180);
            using (var brush = new SolidBrush(tint))
                graphics.FillRectangle(brush, 0, position.Y, ClientSize.Width, height);
        }

        private void drawBreakpointLines(Graphics graphics)
        {
            if (breakpointLines.Count == 0) return;
            Color tint = isDarkBackground()
                ? Color.FromArgb(38, 255, 76, 76)
                : Color.FromArgb(30, 210, 35, 35);
            using (var brush = new SolidBrush(tint))
                foreach (int line in breakpointLines)
                {
                    int start = GetFirstCharIndexFromLine(line);
                    if (start < 0) continue;
                    Point position = GetPositionFromCharIndex(start);
                    int height = visualLineHeight();
                    int next = GetFirstCharIndexFromLine(line + 1);
                    if (next >= 0)
                    {
                        Point nextPosition = GetPositionFromCharIndex(next);
                        if (nextPosition.Y > position.Y) height = nextPosition.Y - position.Y;
                    }
                    if (position.Y + height >= 0 && position.Y <= ClientSize.Height)
                        graphics.FillRectangle(brush, 0, position.Y, ClientSize.Width, height);
                }
        }

        private void drawDebugExecutionLine(Graphics graphics)
        {
            int start = GetFirstCharIndexFromLine(DebugExecutionLine);
            if (start < 0) return;
            Point position = GetPositionFromCharIndex(start);
            int height = visualLineHeight();
            int next = GetFirstCharIndexFromLine(DebugExecutionLine + 1);
            if (next >= 0)
            {
                Point nextPosition = GetPositionFromCharIndex(next);
                if (nextPosition.Y > position.Y) height = nextPosition.Y - position.Y;
            }
            if (position.Y + height < 0 || position.Y > ClientSize.Height) return;
            Color tint = isDarkBackground()
                ? Color.FromArgb(82, 255, 205, 40)
                : Color.FromArgb(68, 255, 190, 0);
            using (var brush = new SolidBrush(tint))
                graphics.FillRectangle(brush, 0, position.Y, ClientSize.Width, height);
        }

        private int drawWhitespace(Graphics graphics)
        {
            if (TextLength == 0) return 0;
            int first = Math.Max(0, GetCharIndexFromPosition(new Point(0, 0)) - 1);
            int last = Math.Min(TextLength, GetCharIndexFromPosition(
                new Point(Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1))) + 2);
            string source = visualSource;
            last = Math.Min(last, source.Length);
            int count = 0;
            Color color = isDarkBackground() ? Color.FromArgb(125, 145, 145, 145) : Color.FromArgb(115, 90, 90, 90);
            using (var pen = new Pen(color, 1F))
            using (var brush = new SolidBrush(color))
            using (var font = new Font(Font.FontFamily, Math.Max(6F, Font.Size * 0.72F), FontStyle.Regular))
            {
                for (int index = first; index < last; index++)
                {
                    char value = source[index];
                    if (value != ' ' && value != '\t' && value != '\n') continue;
                    Point point = GetPositionFromCharIndex(index);
                    if (point.Y < -visualLineHeight() || point.Y > ClientSize.Height) continue;
                    if (value == ' ')
                    {
                        int width = characterWidthAt(index, point);
                        float x = point.X + Math.Max(2F, width / 2F);
                        float y = point.Y + visualLineHeight() * 0.55F;
                        graphics.FillEllipse(brush, x - 1F, y - 1F, 2F, 2F);
                    }
                    else if (value == '\t')
                    {
                        int width = Math.Max(8, characterWidthAt(index, point));
                        float y = point.Y + visualLineHeight() * 0.55F;
                        graphics.DrawLine(pen, point.X + 2, y, point.X + width - 3, y);
                        graphics.DrawLine(pen, point.X + width - 6, y - 3, point.X + width - 3, y);
                        graphics.DrawLine(pen, point.X + width - 6, y + 3, point.X + width - 3, y);
                    }
                    else
                    {
                        graphics.DrawString("↵", font, brush, point.X + 1, point.Y + 1);
                    }
                    count++;
                }
            }
            return count;
        }

        private int drawIndentGuides(Graphics graphics)
        {
            if (TextLength == 0 || VisualIndentSize <= 0) return 0;
            string source = visualSource;
            int firstIndex = Math.Max(0, GetCharIndexFromPosition(new Point(0, 0)));
            int lastIndex = Math.Min(TextLength, GetCharIndexFromPosition(
                new Point(Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1))) + 1);
            int firstLine = Math.Max(0, GetLineFromCharIndex(firstIndex));
            int lastLine = Math.Max(firstLine, GetLineFromCharIndex(lastIndex));
            int spaceWidth = Math.Max(1, TextRenderer.MeasureText(" ", Font, Size.Empty, TextFormatFlags.NoPadding).Width);
            int count = 0;
            Color color = isDarkBackground() ? Color.FromArgb(85, 125, 125, 125) : Color.FromArgb(75, 95, 95, 95);
            using (var pen = new Pen(color, 1F) { DashStyle = DashStyle.Dot })
            {
                for (int line = firstLine; line <= lastLine; line++)
                {
                    int start = GetFirstCharIndexFromLine(line);
                    if (start < 0 || start >= source.Length) continue;
                    int end = start;
                    int columns = 0;
                    while (end < source.Length && source[end] != '\r' && source[end] != '\n' &&
                           (source[end] == ' ' || source[end] == '\t'))
                    {
                        columns = source[end] == '\t'
                            ? ((columns / VisualIndentSize) + 1) * VisualIndentSize
                            : columns + 1;
                        end++;
                    }
                    if (columns < VisualIndentSize) continue;
                    Point origin = GetPositionFromCharIndex(start);
                    int lineHeight = visualLineHeight();
                    for (int column = VisualIndentSize; column <= columns; column += VisualIndentSize)
                    {
                        float x = origin.X + column * spaceWidth;
                        if (x >= 0 && x <= ClientSize.Width)
                        {
                            graphics.DrawLine(pen, x, origin.Y, x, origin.Y + lineHeight);
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private int characterWidthAt(int index, Point position)
        {
            if (index + 1 <= TextLength)
            {
                Point next = GetPositionFromCharIndex(index + 1);
                if (next.Y == position.Y && next.X > position.X) return next.X - position.X;
            }
            return Math.Max(4, TextRenderer.MeasureText(" ", Font, Size.Empty, TextFormatFlags.NoPadding).Width);
        }

        private int visualLineHeight()
        {
            return Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
        }

        private bool isDarkBackground()
        {
            return BackColor.GetBrightness() < 0.5F;
        }
    }
}
