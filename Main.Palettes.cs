using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private const int OutlineAutoHideLogicalWidth = 28;
        private const int DiagnosticsAutoHideLogicalHeight = 26;
        private const int MinimumOutlineExpandedLogicalWidth = 300;
        private const int MinimumDiagnosticsExpandedLogicalHeight = 116;
        private const int MaximumOutlineClientPercentage = 40;

        private Timer paletteAutoHideTimer;
        private ToolTip paletteToolTip;
        private bool outlinePinned = true;
        private bool diagnosticsPinned = true;
        private bool flowchartVisible = true;
        private int outlineExpandedWidth = MinimumOutlineExpandedLogicalWidth;
        private int diagnosticsExpandedHeight = MinimumDiagnosticsExpandedLogicalHeight;
        private Action<string> savePaletteLayoutSetting;
        private readonly DdfEditorPaletteCatalog editorPaletteCatalog = new DdfEditorPaletteCatalog();
        private readonly Dictionary<string, PaletteTabBinding> paletteTabBindings =
            new Dictionary<string, PaletteTabBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, ToolStripMenuItem> paletteMenuItems =
            new Dictionary<string, ToolStripMenuItem>(StringComparer.Ordinal);
        private readonly HashSet<string> hiddenPaletteIds = new HashSet<string>(StringComparer.Ordinal);
        private ToolStripMenuItem palettesMenuItem;
        private bool applyingPaletteCatalog;

        private void initializePaletteBehavior()
        {
            string persistedLayout = AppSettingsStore.LoadPaletteLayout();
            DdfPersistenceReadResult<PaletteLayoutSettings> layoutRead =
                PaletteLayoutSettings.ParseVersioned(persistedLayout);
            PaletteLayoutSettings layout = layoutRead.Value ?? PaletteLayoutSettings.Default;
            if (!layoutRead.Succeeded && !string.IsNullOrWhiteSpace(persistedLayout))
                statusFileLabel.ToolTipText =
                    "Layout palette non applicato; i dati originali sono stati conservati. " +
                    layoutRead.Failure;
            outlineExpandedWidth = layout.OutlineWidth;
            diagnosticsExpandedHeight = layout.DiagnosticsHeight;
            outlinePinned = layout.OutlinePinned;
            diagnosticsPinned = layout.DiagnosticsPinned;
            hiddenPaletteIds.Clear();
            foreach (string id in layout.HiddenPaletteIds) hiddenPaletteIds.Add(id);
            flowchartVisible = layout.FlowchartVisible && !hiddenPaletteIds.Contains(DdfEditorPaletteIds.Flowchart);
            applyFlowchartVisibility(false);
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
            initializeEditorPaletteCatalog();
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
            int available = Math.Max(minimum,
                ClientSize.Width - splitterOutline.Width);
            int editorMaximum = Math.Max(minimum, available - splitterOutline.MinExtra);
            int responsiveWidth = DpiLayout.ClampToClientPercentage(
                scaled(outlineExpandedWidth), available, minimum,
                MaximumOutlineClientPercentage);
            panelOutline.Width = Math.Min(editorMaximum, responsiveWidth);
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
                    outlineExpandedWidth, diagnosticsExpandedHeight, outlinePinned, diagnosticsPinned,
                    flowchartVisible, hiddenPaletteIds).Serialize());
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

        private void initializeEditorPaletteCatalog()
        {
            registerPalette(DdfEditorPaletteIds.Workspace, "File", DdfEditorPalettePlacement.Sidebar,
                navigationTabs, workspaceTabPage, 0);
            registerPalette(DdfEditorPaletteIds.Flowchart, "Flowchart", DdfEditorPalettePlacement.Sidebar,
                navigationTabs, flowchartTabPage, 1, flowchartVisible);
            registerPalette(DdfEditorPaletteIds.Outline, "Outline", DdfEditorPalettePlacement.Sidebar,
                navigationTabs, findTabPage(navigationTabs, "outlineTabPage"), 2);
            if (embeddedHostMode)
                registerPalette(DdfEditorPaletteIds.HostApi, "API host", DdfEditorPalettePlacement.Sidebar,
                    navigationTabs, hostApiTabPage, 3);
            registerPalette(DdfEditorPaletteIds.Diagnostics, "Diagnostica", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageDiagnostics, 0);
            registerPalette(DdfEditorPaletteIds.Output, "Output", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageOutput, 1);
            registerPalette(DdfEditorPaletteIds.Breakpoints, "Breakpoint", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageBreakpoints, 2);
            registerPalette(DdfEditorPaletteIds.Variables, "Variabili", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageVariables, 3);
            registerPalette(DdfEditorPaletteIds.CallStack, "Call Stack", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageCallStack, 4);
            registerPalette(DdfEditorPaletteIds.Watch, "Espressioni di controllo", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, findTabPage(tabControlBottom, "tabPageWatch"), 5);
            registerPalette(DdfEditorPaletteIds.WorkspaceSearch, "Ricerca workspace", DdfEditorPalettePlacement.Bottom,
                tabControlBottom, tabPageWorkspaceSearch, 6);

            editorPaletteCatalog.PaletteChanged += editorPaletteCatalog_PaletteChanged;
            navigationTabs.SelectedIndexChanged += paletteTabs_SelectedIndexChanged;
            tabControlBottom.SelectedIndexChanged += paletteTabs_SelectedIndexChanged;
            applyingPaletteCatalog = true;
            try
            {
                foreach (IDdfEditorPalette palette in editorPaletteCatalog.Palettes.Where(item => !item.IsVisible))
                    if (paletteTabBindings.TryGetValue(palette.Id, out PaletteTabBinding binding))
                        applyPaletteVisibility(binding, false);
            }
            finally
            {
                applyingPaletteCatalog = false;
            }
            panelEditor.AttachPaletteService(editorPaletteCatalog);
            createPalettesMenu();
            synchronizeActivePalette(navigationTabs);
            synchronizeActivePalette(tabControlBottom);
        }

        private void registerPalette(string id, string displayName, DdfEditorPalettePlacement placement,
            TabControl tabs, TabPage page, int order, bool? visible = null)
        {
            if (tabs == null || page == null) return;
            bool isVisible = (visible ?? tabs.TabPages.Contains(page)) && !hiddenPaletteIds.Contains(id);
            paletteTabBindings.Add(id, new PaletteTabBinding(id, tabs, page, order));
            editorPaletteCatalog.Register(id, displayName, placement, isVisible);
        }

        private void createPalettesMenu()
        {
            palettesMenuItem = new ToolStripMenuItem("Palette") { Name = "palettesMenuItem" };
            foreach (IDdfEditorPalette palette in editorPaletteCatalog.Palettes)
            {
                var item = new ToolStripMenuItem(palette.DisplayName)
                {
                    Name = "showPalette_" + palette.Id.Replace('-', '_'),
                    Checked = palette.IsVisible,
                    CheckOnClick = true,
                    Tag = palette.Id
                };
                item.Click += paletteMenuItem_Click;
                paletteMenuItems.Add(palette.Id, item);
                palettesMenuItem.DropDownItems.Add(item);
            }
            int educationalIndex = viewMenuItem.DropDownItems.IndexOf(educationalModeMenuItem);
            viewMenuItem.DropDownItems.Insert(Math.Max(0, educationalIndex), palettesMenuItem);
        }

        private void paletteMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is string id)
            {
                editorPaletteCatalog.SetVisible(id, item.Checked);
                if (item.Checked) editorPaletteCatalog.Activate(id);
            }
        }

        private void editorPaletteCatalog_PaletteChanged(object sender, DdfEditorPaletteChangedEventArgs e)
        {
            if (e?.Palette == null || !paletteTabBindings.TryGetValue(e.Palette.Id, out PaletteTabBinding binding))
                return;
            applyingPaletteCatalog = true;
            try
            {
                if (e.ChangeKind == DdfEditorPaletteChangeKind.Visibility)
                    applyPaletteVisibility(binding, e.Palette.IsVisible);
                if (e.ChangeKind == DdfEditorPaletteChangeKind.Activation)
                {
                    applyPaletteVisibility(binding, true);
                    binding.Tabs.SelectedTab = binding.Page;
                    if (e.Palette.Placement == DdfEditorPalettePlacement.Sidebar && !outlinePinned)
                        expandOutlinePalette();
                    if (e.Palette.Placement == DdfEditorPalettePlacement.Bottom && !diagnosticsPinned)
                        expandDiagnosticsPalette();
                }
                if (paletteMenuItems.TryGetValue(e.Palette.Id, out ToolStripMenuItem menuItem))
                    menuItem.Checked = e.Palette.IsVisible;
                if (e.Palette.IsVisible) hiddenPaletteIds.Remove(e.Palette.Id);
                else hiddenPaletteIds.Add(e.Palette.Id);
                if (e.Palette.Id == DdfEditorPaletteIds.Flowchart)
                {
                    flowchartVisible = e.Palette.IsVisible;
                    if (showFlowchartMenuItem != null) showFlowchartMenuItem.Checked = flowchartVisible;
                }
                if (e.ChangeKind == DdfEditorPaletteChangeKind.Visibility) persistPaletteLayout();
            }
            finally
            {
                applyingPaletteCatalog = false;
            }
            if (e.ChangeKind == DdfEditorPaletteChangeKind.Visibility)
                synchronizeActivePalette(binding.Tabs);
        }

        private void applyPaletteVisibility(PaletteTabBinding binding, bool visible)
        {
            bool contained = binding.Tabs.TabPages.Contains(binding.Page);
            if (visible == contained) return;
            if (!visible)
            {
                binding.Tabs.TabPages.Remove(binding.Page);
                return;
            }
            int index = paletteTabBindings.Values.Count(item =>
                ReferenceEquals(item.Tabs, binding.Tabs) && item.Order < binding.Order &&
                binding.Tabs.TabPages.Contains(item.Page));
            binding.Tabs.TabPages.Insert(Math.Min(index, binding.Tabs.TabPages.Count), binding.Page);
        }

        private void paletteTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!applyingPaletteCatalog && sender is TabControl tabs)
                synchronizeActivePalette(tabs);
        }

        private void synchronizeActivePalette(TabControl tabs)
        {
            if (tabs?.SelectedTab == null) return;
            PaletteTabBinding binding = paletteTabBindings.Values.FirstOrDefault(item =>
                ReferenceEquals(item.Tabs, tabs) && ReferenceEquals(item.Page, tabs.SelectedTab));
            if (binding != null) editorPaletteCatalog.Activate(binding.Id);
        }

        private static TabPage findTabPage(TabControl tabs, string name) => tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Name, name, StringComparison.Ordinal));

        private sealed class PaletteTabBinding
        {
            public PaletteTabBinding(string id, TabControl tabs, TabPage page, int order)
            {
                Id = id;
                Tabs = tabs;
                Page = page;
                Order = order;
            }

            public string Id { get; }
            public TabControl Tabs { get; }
            public TabPage Page { get; }
            public int Order { get; }
        }
    }
}
