using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class LineNumberGutter : RichTextBox
    {
        private const int WmSetFocus = 0x0007;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonDoubleClick = 0x0203;
        private const int WmRightButtonDown = 0x0204;
        private IReadOnlyList<int> displayedSourceLines = new int[0];

        public event EventHandler<LineNumberClickEventArgs> LineClicked;

        public LineNumberGutter()
        {
            Cursor = Cursors.Arrow;
            ReadOnly = true;
            ShortcutsEnabled = false;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Control TargetControl { get; set; }

        public void SetDisplayedSourceLines(IReadOnlyList<int> sourceLines)
        {
            displayedSourceLines = sourceLines ?? new int[0];
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
    }

    internal sealed class LineNumberClickEventArgs : EventArgs
    {
        public LineNumberClickEventArgs(int sourceLine) { SourceLine = sourceLine; }
        public int SourceLine { get; }
    }
}
