using System;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private bool moveFocusBetweenPanes(bool backwards)
        {
            Control target;
            if (panelOutline.ContainsFocus)
                target = backwards ? (Control)richTextBoxMainEditor : listBoxDiagnostics;
            else if (panelDiagnostics.ContainsFocus)
                target = backwards ? (Control)treeViewOutline : richTextBoxMainEditor;
            else
                target = backwards ? (Control)listBoxDiagnostics : treeViewOutline;

            if (ReferenceEquals(target, treeViewOutline) && !outlinePinned) expandOutlinePalette();
            if (ReferenceEquals(target, listBoxDiagnostics) && !diagnosticsPinned) expandDiagnosticsPalette();
            target.Focus();
            return target.Focused || target.ContainsFocus;
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
