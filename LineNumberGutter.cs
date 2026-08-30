using System.ComponentModel;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class LineNumberGutter : RichTextBox
    {
        private const int WmSetFocus = 0x0007;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonDoubleClick = 0x0203;
        private const int WmRightButtonDown = 0x0204;

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

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmSetFocus || message.Msg == WmLeftButtonDown ||
                message.Msg == WmLeftButtonDoubleClick || message.Msg == WmRightButtonDown)
            {
                if (TargetControl != null && !TargetControl.IsDisposed)
                {
                    TargetControl.Focus();
                }

                return;
            }

            base.WndProc(ref message);
        }
    }
}
