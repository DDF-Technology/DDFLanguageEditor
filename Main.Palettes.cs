using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private const int OutlineAutoHideLogicalWidth = 28;
        private const int DiagnosticsAutoHideLogicalHeight = 26;
        private const int MinimumOutlineExpandedLogicalWidth = 300;
        private const int MinimumDiagnosticsExpandedLogicalHeight = 116;

        private Timer paletteAutoHideTimer;
        private ToolTip paletteToolTip;
        private bool outlinePinned = true;
        private bool diagnosticsPinned = true;
        private int outlineExpandedWidth = MinimumOutlineExpandedLogicalWidth;
        private int diagnosticsExpandedHeight = MinimumDiagnosticsExpandedLogicalHeight;
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
                outlineExpandedWidth = Math.Max(MinimumOutlineExpandedLogicalWidth,
                    DpiLayout.ToLogical(panelOutline.Width, DeviceDpi));
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
                diagnosticsExpandedHeight = Math.Max(MinimumDiagnosticsExpandedLogicalHeight,
                    DpiLayout.ToLogical(panelDiagnostics.Height, DeviceDpi));
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
            if (!outlinePinned && panelOutline.Width > scaled(OutlineAutoHideLogicalWidth) &&
                !containsMouse(panelOutline) && !panelOutline.ContainsFocus)
            {
                collapseOutlinePalette();
            }

            if (!diagnosticsPinned && panelDiagnostics.Height > scaled(DiagnosticsAutoHideLogicalHeight) &&
                !containsMouse(panelDiagnostics) && !panelDiagnostics.ContainsFocus)
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
            int minimum = scaled(MinimumOutlineExpandedLogicalWidth);
            int maximum = Math.Max(minimum,
                ClientSize.Width - splitterOutline.Width - splitterOutline.MinExtra);
            panelOutline.Width = Math.Min(maximum, Math.Max(minimum, scaled(outlineExpandedWidth)));
            splitterOutline.Enabled = outlinePinned;
            buttonOutlinePin.BringToFront();
        }

        private void collapseOutlinePalette()
        {
            panelOutline.Width = scaled(OutlineAutoHideLogicalWidth);
            splitterOutline.Enabled = false;
            buttonOutlinePin.BringToFront();
        }

        private void expandDiagnosticsPalette()
        {
            splitterDiagnostics.Visible = true;
            splitterDiagnostics.Enabled = diagnosticsPinned;
            int minimum = scaled(MinimumDiagnosticsExpandedLogicalHeight);
            int maximum = Math.Max(minimum,
                ClientSize.Height - splitterDiagnostics.MinExtra);
            panelDiagnostics.Height = Math.Min(maximum, Math.Max(minimum, scaled(diagnosticsExpandedHeight)));
            buttonDiagnosticsPin.BringToFront();
        }

        private void collapseDiagnosticsPalette()
        {
            splitterDiagnostics.Enabled = false;
            splitterDiagnostics.Visible = false;
            panelDiagnostics.Height = scaled(DiagnosticsAutoHideLogicalHeight);
            buttonDiagnosticsPin.BringToFront();
        }

        private void splitterDiagnostics_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (!diagnosticsPinned) return;
            diagnosticsExpandedHeight = Math.Max(MinimumDiagnosticsExpandedLogicalHeight,
                DpiLayout.ToLogical(panelDiagnostics.Height, DeviceDpi));
            persistPaletteLayout();
        }

        private void splitterOutline_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (!outlinePinned) return;
            outlineExpandedWidth = Math.Max(MinimumOutlineExpandedLogicalWidth,
                DpiLayout.ToLogical(panelOutline.Width, DeviceDpi));
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

        private int scaled(int logicalPixels) => DpiLayout.ScaleLogical(logicalPixels, DeviceDpi);
    }
}
