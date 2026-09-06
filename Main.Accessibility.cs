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

        private static bool isPaletteTabShortcut(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            bool supportedModifiers = modifiers == Keys.Control ||
                modifiers == (Keys.Control | Keys.Shift);
            return supportedModifiers && (keyCode == Keys.Tab ||
                keyCode == Keys.PageUp || keyCode == Keys.PageDown);
        }

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

        private bool cycleFocusedPaletteTab(Keys keyData)
        {
            TabControl tabs = panelOutline.ContainsFocus
                ? navigationTabs
                : panelDiagnostics.ContainsFocus
                    ? tabControlBottom
                    : null;
            if (tabs == null || tabs.TabCount < 2) return false;

            Keys keyCode = keyData & Keys.KeyCode;
            bool backwards = keyCode == Keys.PageUp ||
                (keyCode == Keys.Tab && (keyData & Keys.Shift) == Keys.Shift);
            int direction = backwards ? -1 : 1;
            tabs.SelectedIndex = (tabs.SelectedIndex + direction + tabs.TabCount) % tabs.TabCount;
            focusSelectedPaletteContent(tabs);
            return true;
        }

        private void focusSelectedPaletteContent(TabControl tabs)
        {
            Control target = null;
            if (ReferenceEquals(tabs, navigationTabs))
                target = ReferenceEquals(tabs.SelectedTab, workspaceTabPage)
                    ? (Control)treeViewWorkspace
                    : treeViewOutline;
            else if (ReferenceEquals(tabs.SelectedTab, tabPageDiagnostics))
                target = listBoxDiagnostics;
            else if (ReferenceEquals(tabs.SelectedTab, tabPageOutput))
                target = richTextBoxOutput;
            else if (ReferenceEquals(tabs.SelectedTab, tabPageWorkspaceSearch))
                target = workspaceSearchTextBox;
            else if (ReferenceEquals(tabs.SelectedTab, tabPageVariables))
                target = treeViewVariables;
            else if (ReferenceEquals(tabs.SelectedTab, tabPageCallStack))
                target = callStackListView;
            else if (tabs.SelectedTab?.Name == "tabPageWatch")
                target = watchExpressionTextBox;

            if (target?.CanFocus == true) target.Focus();
            else tabs.Focus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            applyInitialToolbarPositions();
            if (paneFocusMessageFilterRegistered) return;
            Application.AddMessageFilter(this);
            paneFocusMessageFilterRegistered = true;
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != WmKeyDown && message.Msg != WmSystemKeyDown) return false;

            Control source = Control.FromHandle(message.HWnd);
            if (source == null || !ReferenceEquals(source.FindForm(), this)) return false;
            Keys keyData = (Keys)message.WParam.ToInt32() | (Control.ModifierKeys & Keys.Modifiers);
            if (isPaneFocusShortcut(keyData))
                return moveFocusBetweenPanes((keyData & Keys.Shift) == Keys.Shift);
            return isPaletteTabShortcut(keyData) && cycleFocusedPaletteTab(keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (isPaneFocusShortcut(keyData))
                return moveFocusBetweenPanes(keyData == (Keys.Shift | Keys.F6));
            if (isPaletteTabShortcut(keyData) && cycleFocusedPaletteTab(keyData)) return true;
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
            if (isPaletteTabShortcut(e.KeyData) && cycleFocusedPaletteTab(e.KeyData))
            {
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
