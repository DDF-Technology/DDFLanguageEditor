using System;
using System.Drawing;
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

        public event EventHandler<AltCursorEventArgs> AltCursorRequested;

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
        }
    }
}
