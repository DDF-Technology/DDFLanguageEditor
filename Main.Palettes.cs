using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private const int OutlineAutoHideWidth = 28;
        private const int DiagnosticsAutoHideHeight = 26;
        private const int MinimumOutlineExpandedWidth = 300;
        private const int MinimumDiagnosticsExpandedHeight = 116;

        private Timer paletteAutoHideTimer;
        private ToolTip paletteToolTip;
        private bool outlinePinned = true;
        private bool diagnosticsPinned = true;
        private int outlineExpandedWidth = MinimumOutlineExpandedWidth;
        private int diagnosticsExpandedHeight = MinimumDiagnosticsExpandedHeight;
        private Action<string> savePaletteLayoutSetting;

        private void initializePaletteBehavior()
        {
            PaletteLayoutSettings layout = PaletteLayoutSettings.Parse(AppSettingsStore.LoadPaletteLayout());
            outlineExpandedWidth = layout.OutlineWidth;
            diagnosticsExpandedHeight = layout.DiagnosticsHeight;
            outlinePinned = layout.OutlinePinned;
            diagnosticsPinned = layout.DiagnosticsPinned;
            savePaletteLayoutSetting = AppSettingsStore.SavePaletteLayout;

            buttonOutlinePin.Click += buttonOutlinePin_Click;
            buttonDiagnosticsPin.Click += buttonDiagnosticsPin_Click;
            splitterOutline.SplitterMoved += splitterOutline_SplitterMoved;
            attachMouseEnter(panelOutline, panelOutline_MouseEnter);
            attachMouseEnter(panelDiagnostics, panelDiagnostics_MouseEnter);

            paletteToolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 300,
                ReshowDelay = 100
            };
            paletteAutoHideTimer = new Timer { Interval = 250 };
            paletteAutoHideTimer.Tick += paletteAutoHideTimer_Tick;
            paletteAutoHideTimer.Start();

            buttonOutlinePin.BringToFront();
            buttonDiagnosticsPin.BringToFront();
            updatePaletteButtons();
        }

        private void applyPersistedPaletteLayout()
        {
            if (outlinePinned) expandOutlinePalette();
            else collapseOutlinePalette();
            if (diagnosticsPinned) expandDiagnosticsPalette();
            else collapseDiagnosticsPalette();
            updatePaletteButtons();
        }

        private void disposePaletteBehavior()
        {
            if (paletteAutoHideTimer != null)
            {
                paletteAutoHideTimer.Stop();
                paletteAutoHideTimer.Dispose();
                paletteAutoHideTimer = null;
            }

            if (paletteToolTip != null)
            {
                paletteToolTip.Dispose();
                paletteToolTip = null;
            }
        }

        private static void attachMouseEnter(Control control, EventHandler handler)
        {
            control.MouseEnter += handler;
            foreach (Control child in control.Controls)
            {
                attachMouseEnter(child, handler);
            }
        }

        private void buttonOutlinePin_Click(object sender, EventArgs e)
        {
            if (outlinePinned)
            {
                outlineExpandedWidth = Math.Max(MinimumOutlineExpandedWidth, panelOutline.Width);
                outlinePinned = false;
                collapseOutlinePalette();
            }
            else
            {
                outlinePinned = true;
                expandOutlinePalette();
            }

            updatePaletteButtons();
            persistPaletteLayout();
        }

        private void buttonDiagnosticsPin_Click(object sender, EventArgs e)
        {
            if (diagnosticsPinned)
            {
                diagnosticsExpandedHeight = Math.Max(MinimumDiagnosticsExpandedHeight, panelDiagnostics.Height);
                diagnosticsPinned = false;
                collapseDiagnosticsPalette();
            }
            else
            {
                diagnosticsPinned = true;
                expandDiagnosticsPalette();
            }

            updatePaletteButtons();
            persistPaletteLayout();
        }

        private void panelOutline_MouseEnter(object sender, EventArgs e)
        {
            if (!outlinePinned) expandOutlinePalette();
        }

        private void panelDiagnostics_MouseEnter(object sender, EventArgs e)
        {
            if (!diagnosticsPinned) expandDiagnosticsPalette();
        }

        private void paletteAutoHideTimer_Tick(object sender, EventArgs e)
        {
            if (!outlinePinned && panelOutline.Width > OutlineAutoHideWidth &&
                !containsMouse(panelOutline))
            {
                collapseOutlinePalette();
            }

            if (!diagnosticsPinned && panelDiagnostics.Height > DiagnosticsAutoHideHeight &&
                !containsMouse(panelDiagnostics))
            {
                collapseDiagnosticsPalette();
            }
        }

        private static bool containsMouse(Control control)
        {
            return control.IsHandleCreated && control.Visible &&
                   control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position);
        }

        private void expandOutlinePalette()
        {
            int maximum = Math.Max(MinimumOutlineExpandedWidth,
                ClientSize.Width - splitterOutline.Width - splitterOutline.MinExtra);
            panelOutline.Width = Math.Min(maximum, Math.Max(MinimumOutlineExpandedWidth, outlineExpandedWidth));
            splitterOutline.Enabled = outlinePinned;
            buttonOutlinePin.BringToFront();
        }

        private void collapseOutlinePalette()
        {
            panelOutline.Width = OutlineAutoHideWidth;
            splitterOutline.Enabled = false;
            buttonOutlinePin.BringToFront();
        }

        private void expandDiagnosticsPalette()
        {
            splitterDiagnostics.Visible = true;
            splitterDiagnostics.Enabled = diagnosticsPinned;
            int maximum = Math.Max(MinimumDiagnosticsExpandedHeight,
                ClientSize.Height - splitterDiagnostics.MinExtra);
            panelDiagnostics.Height = Math.Min(maximum, Math.Max(MinimumDiagnosticsExpandedHeight, diagnosticsExpandedHeight));
            buttonDiagnosticsPin.BringToFront();
        }

        private void collapseDiagnosticsPalette()
        {
            splitterDiagnostics.Enabled = false;
            splitterDiagnostics.Visible = false;
            panelDiagnostics.Height = DiagnosticsAutoHideHeight;
            buttonDiagnosticsPin.BringToFront();
        }

        private void splitterDiagnostics_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (!diagnosticsPinned) return;
            diagnosticsExpandedHeight = Math.Max(MinimumDiagnosticsExpandedHeight, panelDiagnostics.Height);
            persistPaletteLayout();
        }

        private void splitterOutline_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (!outlinePinned) return;
            outlineExpandedWidth = Math.Max(MinimumOutlineExpandedWidth, panelOutline.Width);
            persistPaletteLayout();
        }

        private void persistPaletteLayout()
        {
            if (savePaletteLayoutSetting == null) return;
            try
            {
                savePaletteLayoutSetting(new PaletteLayoutSettings(
                    outlineExpandedWidth, diagnosticsExpandedHeight, outlinePinned, diagnosticsPinned).Serialize());
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare il layout delle palette: " + exception.Message;
            }
        }

        private void updatePaletteButtons()
        {
            buttonOutlinePin.Text = outlinePinned ? "\uE718" : "\uE76C";
            buttonOutlinePin.AccessibleName = outlinePinned ? "Outline fissato" : "Outline auto-hide";
            buttonDiagnosticsPin.Text = diagnosticsPinned ? "\uE718" : "\uE70E";
            buttonDiagnosticsPin.AccessibleName = diagnosticsPinned ? "Diagnostica fissata" : "Diagnostica auto-hide";

            if (paletteToolTip != null)
            {
                paletteToolTip.SetToolTip(buttonOutlinePin,
                    outlinePinned ? "Attiva auto-hide Outline" : "Mantieni Outline aperto");
                paletteToolTip.SetToolTip(buttonDiagnosticsPin,
                    diagnosticsPinned ? "Attiva auto-hide Diagnostica" : "Mantieni Diagnostica aperta");
            }
        }
    }
}
