using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class LineNumberGutter : RichTextBox
    {
        private const int WmSetFocus = 0x0007;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonDoubleClick = 0x0203;
        private const int WmRightButtonDown = 0x0204;
        private const int WmSetRedraw = 0x000B;
        private IReadOnlyList<int> displayedSourceLines = new int[0];

        public event EventHandler<LineNumberClickEventArgs> LineClicked;

        public LineNumberGutter()
        {
            AccessibleName = "Numeri di riga";
            AccessibleDescription = "Mostra i numeri delle righe logiche senza accettare il focus.";
            Cursor = Cursors.Arrow;
            ReadOnly = true;
            ShortcutsEnabled = false;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Control TargetControl { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ContentUpdateCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<int> DisplayedSourceLines => displayedSourceLines;

        public void SetDisplayedSourceLines(IReadOnlyList<int> sourceLines)
        {
            displayedSourceLines = sourceLines?.ToArray() ?? new int[0];
        }

        public bool UpdateDisplayedLines(string value, IReadOnlyList<int> sourceLines)
        {
            value = value ?? string.Empty;
            sourceLines = sourceLines ?? new int[0];
            if (string.Equals(Text, value, StringComparison.Ordinal) &&
                displayedSourceLines.SequenceEqual(sourceLines)) return false;

            bool canSuspendRedraw = IsHandleCreated && !IsDisposed;
            if (canSuspendRedraw) SendMessage(Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            try
            {
                Text = value;
                SelectAll();
                SelectionAlignment = HorizontalAlignment.Left;
                SelectionIndent = 1;
                Select(0, 0);
            }
            finally
            {
                if (canSuspendRedraw && IsHandleCreated && !IsDisposed)
                {
                    SendMessage(Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                    Invalidate();
                }
            }
            displayedSourceLines = sourceLines.ToArray();
            ContentUpdateCount++;
            return true;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmLeftButtonDown)
            {
                int y = unchecked((short)(((long)message.LParam >> 16) & 0xffff));
                int character = GetCharIndexFromPosition(new Point(1, Math.Max(0, y)));
                int displayedLine = GetLineFromCharIndex(character);
                if (displayedLine >= 0 && displayedLine < displayedSourceLines.Count && displayedSourceLines[displayedLine] > 0)
                    LineClicked?.Invoke(this, new LineNumberClickEventArgs(displayedSourceLines[displayedLine]));
                FocusTarget();
                return;
            }

            if (message.Msg == WmSetFocus ||
                message.Msg == WmLeftButtonDoubleClick || message.Msg == WmRightButtonDown)
            {
                FocusTarget();
                return;
            }

            base.WndProc(ref message);
        }

        private void FocusTarget()
        {
            if (TargetControl != null && !TargetControl.IsDisposed) TargetControl.Focus();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class LineNumberClickEventArgs : EventArgs
    {
        public LineNumberClickEventArgs(int sourceLine) { SourceLine = sourceLine; }
        public int SourceLine { get; }
    }
}
