using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal enum EditorOverviewMarkerKind
    {
        Search,
        Warning,
        Error,
        Breakpoint
    }

    internal sealed class EditorOverviewMarker
    {
        public EditorOverviewMarker(int line, EditorOverviewMarkerKind kind)
        {
            Line = Math.Max(1, line);
            Kind = kind;
        }

        public int Line { get; }
        public EditorOverviewMarkerKind Kind { get; }
    }

    internal sealed class EditorOverviewLineRequestedEventArgs : EventArgs
    {
        public EditorOverviewLineRequestedEventArgs(int line) => Line = line;
        public int Line { get; }
    }

    internal sealed class EditorOverviewBar : Control
    {
        private IReadOnlyList<EditorOverviewMarker> markers = Array.Empty<EditorOverviewMarker>();
        private int totalLines = 1;
        private int firstVisibleLine = 1;
        private int lastVisibleLine = 1;
        private int currentLine = 1;
        private string markerSignature = string.Empty;

        public EditorOverviewBar()
        {
            Name = "editorOverviewBar";
            Dock = DockStyle.Right;
            Width = 24;
            MinimumSize = new Size(20, 0);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            TabStop = false;
            AccessibleName = "Panoramica del documento";
            AccessibleDescription = "Mostra errori, avvisi, risultati di ricerca, breakpoint e area visibile.";
        }

        public event EventHandler<EditorOverviewLineRequestedEventArgs> LineRequested;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RichTextBox TargetEditor { get; set; }
        public int TotalLines => totalLines;
        public int FirstVisibleLine => firstVisibleLine;
        public int LastVisibleLine => lastVisibleLine;
        public int CurrentLine => currentLine;
        public int SearchMarkerCount => markers.Count(marker => marker.Kind == EditorOverviewMarkerKind.Search);
        public int WarningMarkerCount => markers.Count(marker => marker.Kind == EditorOverviewMarkerKind.Warning);
        public int ErrorMarkerCount => markers.Count(marker => marker.Kind == EditorOverviewMarkerKind.Error);
        public int BreakpointMarkerCount => markers.Count(marker => marker.Kind == EditorOverviewMarkerKind.Breakpoint);

        public void SetMarkers(int lineCount, IEnumerable<EditorOverviewMarker> value)
        {
            int safeLineCount = Math.Max(1, lineCount);
            EditorOverviewMarker[] normalized = (value ?? Enumerable.Empty<EditorOverviewMarker>())
                .Where(marker => marker != null)
                .Select(marker => new EditorOverviewMarker(Math.Min(safeLineCount, marker.Line), marker.Kind))
                .Distinct(new MarkerComparer())
                .OrderBy(marker => marker.Line)
                .ThenBy(marker => marker.Kind)
                .ToArray();
            string signature = safeLineCount + ":" + string.Join(",", normalized.Select(marker => marker.Line + "/" + (int)marker.Kind));
            if (signature == markerSignature) return;
            totalLines = safeLineCount;
            markers = normalized;
            markerSignature = signature;
            firstVisibleLine = Math.Min(firstVisibleLine, totalLines);
            lastVisibleLine = Math.Max(firstVisibleLine, Math.Min(lastVisibleLine, totalLines));
            Invalidate();
        }

        public void SetViewport(int firstLine, int lastLine, int caretLine)
        {
            int first = Math.Max(1, Math.Min(totalLines, firstLine));
            int last = Math.Max(first, Math.Min(totalLines, lastLine));
            int current = Math.Max(1, Math.Min(totalLines, caretLine));
            if (first == firstVisibleLine && last == lastVisibleLine && current == currentLine) return;
            firstVisibleLine = first;
            lastVisibleLine = last;
            currentLine = current;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Height <= 0 || ClientSize.Width <= 0) return;
            using (var trackBrush = new SolidBrush(Color.FromArgb(18, ForeColor)))
                e.Graphics.FillRectangle(trackBrush, 2, 0, Math.Max(1, ClientSize.Width - 4), ClientSize.Height);
            int top = lineToY(firstVisibleLine);
            int bottom = lineToY(lastVisibleLine);
            int height = Math.Max(5, bottom - top + 1);
            if (top + height >= ClientSize.Height) height = Math.Max(1, ClientSize.Height - top - 1);
            using (var brush = new SolidBrush(Color.FromArgb(48, ForeColor)))
                e.Graphics.FillRectangle(brush, 1, top, ClientSize.Width - 2, height);
            using (var pen = new Pen(Color.FromArgb(190, ForeColor), 2F))
                e.Graphics.DrawRectangle(pen, 1, top, Math.Max(0, ClientSize.Width - 3), height);

            foreach (EditorOverviewMarker marker in markers)
            {
                bool visibleLine = isLineVisible(marker.Line);
                int y = visibleLine ? markerToY(marker.Line) : lineToY(marker.Line);
                int x = visibleLine ? 3 : Math.Max(1, ClientSize.Width - 3);
                int width = visibleLine ? Math.Max(1, ClientSize.Width - 7) : 2;
                using (var brush = new SolidBrush(markerColor(marker.Kind)))
                    e.Graphics.FillRectangle(brush, x, Math.Max(0, y - 1), width, 2);
            }

            int caretY = markerToY(currentLine, true);
            using (var pen = new Pen(Color.FromArgb(86, 190, 255), 2F))
                e.Graphics.DrawLine(pen, 1, caretY, ClientSize.Width - 2, caretY);
            Point[] pointer =
            {
                new Point(0, Math.Max(0, caretY - 4)),
                new Point(5, caretY),
                new Point(0, Math.Min(ClientSize.Height - 1, caretY + 4))
            };
            using (var brush = new SolidBrush(Color.FromArgb(86, 190, 255)))
                e.Graphics.FillPolygon(brush, pointer);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int line = ClientSize.Height <= 1
                ? 1
                : 1 + (int)Math.Round((double)Math.Max(0, Math.Min(ClientSize.Height - 1, e.Y)) * (totalLines - 1) / (ClientSize.Height - 1));
            LineRequested?.Invoke(this, new EditorOverviewLineRequestedEventArgs(line));
        }

        private int lineToY(int line) => totalLines <= 1 || ClientSize.Height <= 1
            ? 0
            : (int)Math.Round((double)(Math.Max(1, Math.Min(totalLines, line)) - 1) * (ClientSize.Height - 1) / (totalLines - 1));

        private int markerToY(int line, bool useCaretPosition = false)
        {
            if (TargetEditor != null && !TargetEditor.IsDisposed && isLineVisible(line))
            {
                int character = useCaretPosition
                    ? TargetEditor.SelectionStart
                    : TargetEditor.GetFirstCharIndexFromLine(Math.Max(0, line - 1));
                if (character >= 0)
                {
                    int y = TargetEditor.GetPositionFromCharIndex(character).Y;
                    if (y >= 0 && y < ClientSize.Height) return y;
                }
            }
            return lineToY(line);
        }

        private bool isLineVisible(int line) => line >= firstVisibleLine && line <= lastVisibleLine;

        private static Color markerColor(EditorOverviewMarkerKind kind)
        {
            switch (kind)
            {
                case EditorOverviewMarkerKind.Search: return Color.FromArgb(0, 122, 204);
                case EditorOverviewMarkerKind.Warning: return Color.FromArgb(215, 154, 46);
                case EditorOverviewMarkerKind.Error: return Color.FromArgb(241, 76, 76);
                default: return Color.FromArgb(210, 45, 45);
            }
        }

        private sealed class MarkerComparer : IEqualityComparer<EditorOverviewMarker>
        {
            public bool Equals(EditorOverviewMarker x, EditorOverviewMarker y) =>
                ReferenceEquals(x, y) || (x != null && y != null && x.Line == y.Line && x.Kind == y.Kind);
            public int GetHashCode(EditorOverviewMarker marker) => (marker.Line * 397) ^ (int)marker.Kind;
        }
    }
}
