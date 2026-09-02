using System;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;

        private static bool isPaneFocusShortcut(Keys keyData) =>
            keyData == Keys.F6 || keyData == (Keys.Shift | Keys.F6);

        private bool moveFocusBetweenPanes(bool backwards)
        {
            Control editorPane = activeEditorPane();
            Control target;
            if (panelOutline.ContainsFocus)
                target = backwards ? editorPane : listBoxDiagnostics;
            else if (panelDiagnostics.ContainsFocus)
                target = backwards ? (Control)treeViewOutline : editorPane;
            else
                target = backwards ? (Control)listBoxDiagnostics : treeViewOutline;

            if (ReferenceEquals(target, treeViewOutline))
            {
                if (!outlinePinned) expandOutlinePalette();
                if (treeViewOutline.Parent is TabPage outlinePage)
                    navigationTabs.SelectedTab = outlinePage;
            }
            if (ReferenceEquals(target, listBoxDiagnostics))
            {
                if (!diagnosticsPinned) expandDiagnosticsPalette();
                tabControlBottom.SelectedTab = tabPageDiagnostics;
            }

            bool focused = target.Focus();
            if (!focused)
            {
                Control fallback = ReferenceEquals(target, treeViewOutline)
                    ? (Control)navigationTabs
                    : ReferenceEquals(target, listBoxDiagnostics)
                        ? tabControlBottom
                        : editorPane;
                focused = fallback.Focus();
                target = fallback;
            }
            return true;
        }

        private Control activeEditorPane() => richTextBoxFoldedView?.Visible == true
            ? (Control)richTextBoxFoldedView
            : richTextBoxMainEditor;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (paneFocusMessageFilterRegistered) return;
            Application.AddMessageFilter(this);
            paneFocusMessageFilterRegistered = true;
        }

        public bool PreFilterMessage(ref Message message)
        {
            if ((message.Msg != WmKeyDown && message.Msg != WmSystemKeyDown) ||
                (Keys)message.WParam.ToInt32() != Keys.F6) return false;

            Control source = Control.FromHandle(message.HWnd);
            if (source == null || !ReferenceEquals(source.FindForm(), this)) return false;
            moveFocusBetweenPanes((Control.ModifierKeys & Keys.Shift) == Keys.Shift);
            return true;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (isPaneFocusShortcut(keyData))
                return moveFocusBetweenPanes(keyData == (Keys.Shift | Keys.F6));
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (isPaneFocusShortcut(e.KeyData))
            {
                moveFocusBetweenPanes(e.Shift);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (outlinePinned) expandOutlinePalette();
            else collapseOutlinePalette();
            if (diagnosticsPinned) expandDiagnosticsPalette();
            else collapseDiagnosticsPalette();
            updateLineNumbers();
            refreshEditorOverview();
        }
    }
}
