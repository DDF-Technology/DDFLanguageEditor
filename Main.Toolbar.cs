using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripButton toolbarRunButton;
        private ToolStripButton toolbarRunWithoutDebugButton;
        private ToolStripButton toolbarBuildButton;
        private ToolStripButton toolbarValidateButton;
        private ToolStripButton toolbarLanguageManagerButton;
        private ToolStripButton toolbarLanguagePlaygroundButton;
        private ToolStripButton toolbarStopButton;
        private ToolStripButton toolbarBreakpointButton;
        private ToolStripButton toolbarSaveAllButton;
        private ToolStripButton toolbarCloseDocumentButton;
        private ToolStripPanel toolStripPanelMain;
        private ToolStripMenuItem toolbarsMenuItem;
        private readonly List<ToolbarGroup> toolbarGroups = new List<ToolbarGroup>();
        private ToolbarLayoutSettings toolbarLayoutSettings;
        private Action<string> saveToolbarLayoutSetting;
        private bool toolbarInitialLayoutApplied;
        private bool toolbarLayoutReady;
        private bool suppressToolbarLayoutSave;
        private Timer toolbarLayoutSaveTimer;

        private static readonly ToolbarGroupDefinition[] ToolbarGroupDefinitions =
        {
            new ToolbarGroupDefinition("file", "File", "toolStripMain", new[]
            {
                "toolbarNewButton", "toolbarOpenButton", "toolbarSaveButton", "toolbarSaveAllButton", "toolbarCloseDocumentButton"
            }),
            new ToolbarGroupDefinition("edit", "Modifica", "toolStripEdit", new[]
            {
                "toolbarUndoButton", "toolbarRedoButton", "toolbarCutButton", "toolbarCopyButton", "toolbarPasteButton",
                "toolbarCommentButton", "toolbarDuplicateLinesButton", "toolbarMoveLinesUpButton", "toolbarMoveLinesDownButton",
                "toolbarDeleteLinesButton", "toolbarExpandSelectionButton", "toolbarShrinkSelectionButton",
                "toolbarSelectNextOccurrenceButton", "toolbarSelectAllOccurrencesButton"
            }),
            new ToolbarGroupDefinition("code", "Codice e formattazione", "toolStripCode", new[]
            {
                "toolbarCompletionButton", "toolbarQuickFixButton", "toolbarFormatButton", "toolbarFoldButton",
                "toolbarMatchingDelimiterButton"
            }),
            new ToolbarGroupDefinition("navigation", "Navigazione e ricerca", "toolStripNavigation", new[]
            {
                "toolbarFindButton", "toolbarWorkspaceSearchButton", "toolbarWorkspaceReplaceButton",
                "toolbarNavigateBackButton", "toolbarNavigateForwardButton", "toolbarGoToFileButton", "toolbarGoToSymbolButton",
                "toolbarFindReferencesButton", "toolbarGoToLineButton", "toolbarGoToLastEditButton", "toolbarCommandPaletteButton"
            }),
            new ToolbarGroupDefinition("view", "Visualizzazione", "toolStripView", new[]
            {
                "toolbarHighlightCurrentLineButton", "toolbarShowWhitespaceButton", "toolbarShowIndentGuidesButton",
                "toolbarWordWrapButton", "toolbarOverviewButton"
            }),
            new ToolbarGroupDefinition("project", "Progetto e linguaggio", "toolStripProject", new[]
            {
                "toolbarSettingsButton", "toolbarValidateButton", "toolbarBuildButton", "toolbarLanguageManagerButton",
                "toolbarLanguagePlaygroundButton", "toolbarLanguageButton"
            }),
            new ToolbarGroupDefinition("debug", "Esecuzione e debug", "toolStripDebug", new[]
            {
                "toolbarRunWithoutDebugButton", "toolbarBreakpointButton", "toolbarRunButton", "toolbarStepIntoButton",
                "toolbarStepOverButton", "toolbarStepOutButton", "toolbarStopButton"
            })
        };

        private void initializeMainToolbar()
        {
            toolStripMain.Items.Add(createToolbarButton("toolbarNewButton", "\uE710", "Nuovo (Ctrl+N)", newMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarOpenButton", "\uE8E5", "Apri (Ctrl+O)", openMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSaveButton", "\uE74E", "Salva (Ctrl+S)", saveMenuItem_Click));
            toolbarSaveAllButton = createToolbarButton("toolbarSaveAllButton", "\uE8F4", "Salva tutto (Ctrl+Alt+S)", saveAllMenuItem_Click);
            toolStripMain.Items.Add(toolbarSaveAllButton);
            toolbarCloseDocumentButton = createToolbarButton("toolbarCloseDocumentButton", "\uE8BB", "Chiudi documento (Ctrl+W)", closeDocumentMenuItem_Click);
            toolStripMain.Items.Add(toolbarCloseDocumentButton);
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarUndoButton", "\uE7A7", "Annulla (Ctrl+Z)", undoMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarRedoButton", "\uE7A6", "Ripristina (Ctrl+Y)", redoMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarCutButton", "\uE8C6", "Taglia (Ctrl+X)", cutMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCopyButton", "\uE8C8", "Copia (Ctrl+C)", copyMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarPasteButton", "\uE77F", "Incolla (Ctrl+V)", pasteMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCommentButton", "\uE8C1", "Commenta/decommenta (Ctrl+/)", toggleLineCommentMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarDuplicateLinesButton", "\uE8C8", "Duplica righe (Ctrl+Shift+D)", duplicateLinesMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMoveLinesUpButton", "\uE74A", "Sposta righe su (Alt+Su)", moveLinesUpMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMoveLinesDownButton", "\uE74B", "Sposta righe giù (Alt+Giù)", moveLinesDownMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarDeleteLinesButton", "\uE74D", "Elimina righe (Ctrl+Shift+K)", deleteLinesMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarExpandSelectionButton", "\uE8B7", "Espandi selezione (Shift+Alt+Destra)", expandSelectionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarShrinkSelectionButton", "\uE8B6", "Riduci selezione (Shift+Alt+Sinistra)", shrinkSelectionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMatchingDelimiterButton", "\uE8A7", "Vai al delimitatore corrispondente (Ctrl+Shift+\\)", matchingDelimiterMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSelectNextOccurrenceButton", "\uE8EE", "Seleziona occorrenza successiva (Ctrl+D)", selectNextOccurrenceMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSelectAllOccurrencesButton", "\uE8B3", "Seleziona tutte le occorrenze (Ctrl+Shift+L)", selectAllOccurrencesMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarFindButton", "\uE721", "Trova (Ctrl+F)", findMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCompletionButton", "\uE943", "Completamento contestuale (Ctrl+Spazio)", completionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarQuickFixButton", "\uEA80", "Correzione rapida (Ctrl+.)", quickFixMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarFormatButton", "\uE8D2", "Formatta documento (Ctrl+Shift+F)", formatDocumentMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarFoldButton", "\uE8B0", "Comprimi o espandi blocco (Ctrl+M)", toggleFoldMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolbarValidateButton = createToolbarButton("toolbarValidateButton", "\uE73E", "Convalida progetto", validateProjectMenuItem_Click);
            toolStripMain.Items.Add(toolbarValidateButton);
            toolbarBuildButton = createToolbarButton("toolbarBuildButton", "\uE895", "Compila progetto (Ctrl+Shift+B)", buildProjectMenuItem_Click);
            toolStripMain.Items.Add(toolbarBuildButton);
            toolbarRunWithoutDebugButton = createToolbarButton("toolbarRunWithoutDebugButton", "\uE72A", "Avvia senza debug (Ctrl+F5)", runWithoutDebugMenuItem_Click);
            toolbarRunWithoutDebugButton.ForeColor = AppTheme.Run;
            toolStripMain.Items.Add(toolbarRunWithoutDebugButton);
            toolbarLanguageManagerButton = createToolbarButton("toolbarLanguageManagerButton", "\uE7B8", "Gestisci pacchetti linguaggio", manageLanguagePackagesMenuItem_Click);
            toolStripMain.Items.Add(toolbarLanguageManagerButton);
            toolbarLanguagePlaygroundButton = createToolbarButton("toolbarLanguagePlaygroundButton", "\uE9D9", "Playground linguaggio", languagePlaygroundMenuItem_Click);
            toolStripMain.Items.Add(toolbarLanguagePlaygroundButton);
            toolbarBreakpointButton = createToolbarButton("toolbarBreakpointButton", "●", "Attiva/disattiva breakpoint (F9)", toggleBreakpointMenuItem_Click);
            toolbarBreakpointButton.Font = new Font("Segoe UI Symbol", 12F, FontStyle.Bold);
            toolbarBreakpointButton.ForeColor = AppTheme.Stop;
            toolStripMain.Items.Add(toolbarBreakpointButton);
            toolbarRunButton = createToolbarButton("toolbarRunButton", "\uE768", "Avvia debug (F5)", runProgramMenuItem_Click);
            toolbarRunButton.ForeColor = AppTheme.Run;
            toolStripMain.Items.Add(toolbarRunButton);
            toolbarStopButton = createToolbarButton("toolbarStopButton", "\uE71A", "Arresta (Shift+F5)", stopProgramMenuItem_Click);
            toolbarStopButton.ForeColor = AppTheme.Stop;
            toolbarStopButton.Enabled = false;
            toolStripMain.Items.Add(toolbarStopButton);
        }

        private static ToolStripButton createToolbarButton(string name, string glyph, string toolTip, EventHandler handler)
        {
            var button = new ToolStripButton
            {
                AutoSize = false,
                AccessibleName = toolTip,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe MDL2 Assets", 11F),
                Margin = new Padding(1, 1, 1, 2),
                Name = name,
                Size = new Size(30, 28),
                Text = glyph,
                ToolTipText = toolTip
            };
            button.Click += handler;
            return button;
        }

        private void initializeFloatingToolbars()
        {
            ToolStripItem[] stagedItems = toolStripMain.Items.Cast<ToolStripItem>().ToArray();
            var stagedByName = stagedItems.Where(item => !string.IsNullOrEmpty(item.Name))
                .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
            foreach (ToolStripSeparator separator in stagedItems.OfType<ToolStripSeparator>())
            {
                toolStripMain.Items.Remove(separator);
                separator.Dispose();
            }

            Controls.Remove(toolStripMain);
            toolStripPanelMain = new ToolStripPanel
            {
                Name = "toolStripPanelMain",
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = AppTheme.Chrome,
                Padding = new Padding(2, 1, 2, 1),
                TabIndex = 1
            };
            Controls.Add(toolStripPanelMain);
            menuStripMain.BringToFront();
            toolStripPanelMain.BringToFront();
            toolStripPanelMain.SizeChanged += toolbarChrome_SizeChanged;
            menuStripMain.SizeChanged += toolbarChrome_SizeChanged;
            PerformLayout();

            toolbarLayoutSettings = ToolbarLayoutSettings.Parse(
                AppSettingsStore.LoadToolbarLayout(), ToolbarGroupDefinitions.Select(definition => definition.Id));
            saveToolbarLayoutSetting = AppSettingsStore.SaveToolbarLayout;

            foreach (ToolbarGroupDefinition definition in ToolbarGroupDefinitions
                .OrderBy(item => getInitialToolbarRow(item.Id)))
            {
                ToolStrip strip = definition.Id == "file" ? toolStripMain : createCategoryToolbar(definition);
                configureCategoryToolbar(strip, definition);
                foreach (string itemName in definition.ItemNames)
                    if (stagedByName.TryGetValue(itemName, out ToolStripItem item)) strip.Items.Add(item);
                strip.Visible = toolbarLayoutSettings.IsVisible(definition.Id);
                int row = getInitialToolbarRow(definition.Id);
                toolStripPanelMain.Join(strip, row);
                strip.LocationChanged += toolbarStrip_LayoutChanged;
                toolbarGroups.Add(new ToolbarGroup(definition.Id, definition.DisplayName, strip));
            }

            toolbarLayoutSaveTimer = new Timer { Interval = 400 };
            toolbarLayoutSaveTimer.Tick += toolbarLayoutSaveTimer_Tick;

            toolbarsMenuItem = new ToolStripMenuItem("Barre degli strumenti")
            {
                Name = "toolbarsMenuItem"
            };
            foreach (ToolbarGroup group in toolbarGroups)
            {
                var visibilityItem = new ToolStripMenuItem(group.DisplayName)
                {
                    Name = "showToolbar" + char.ToUpperInvariant(group.Id[0]) + group.Id.Substring(1) + "MenuItem",
                    CheckOnClick = false,
                    Checked = group.Strip.Visible,
                    Tag = group
                };
                visibilityItem.Click += toolbarVisibilityMenuItem_Click;
                toolbarsMenuItem.DropDownItems.Add(visibilityItem);
            }
            viewMenuItem.DropDownItems.Insert(0, toolbarsMenuItem);
            viewMenuItem.DropDownItems.Insert(1, new ToolStripSeparator());

            PerformLayout();
        }

        private static int getInitialToolbarRow(string groupId)
        {
            if (groupId == "file" || groupId == "project" || groupId == "debug") return 0;
            if (groupId == "edit" || groupId == "view") return 1;
            return 2;
        }

        private void applyInitialToolbarPositions()
        {
            if (toolbarInitialLayoutApplied || toolStripPanelMain == null) return;
            toolbarInitialLayoutApplied = true;
            suppressToolbarLayoutSave = true;
            Dictionary<string, bool> visibility = toolbarGroups.ToDictionary(
                group => group.Id, group => toolbarLayoutSettings.IsVisible(group.Id), StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ToolbarRestorePlacement> placements = createToolbarRestorePlacements();
            toolStripPanelMain.SuspendLayout();
            foreach (ToolbarGroup group in toolbarGroups)
            {
                group.Strip.Visible = true;
                toolStripPanelMain.Controls.Remove(group.Strip);
            }
            toolStripPanelMain.ResumeLayout(true);
            foreach (IGrouping<int, ToolbarRestorePlacement> rowGroups in placements
                .GroupBy(item => item.Row).OrderBy(group => group.Key))
            {
                ToolbarRestorePlacement[] row = rowGroups.OrderBy(item => item.Order).ToArray();
                ToolbarRestorePlacement first = row[0];
                toolStripPanelMain.Join(first.Group.Strip, rowGroups.Key);
                toolStripPanelMain.PerformLayout();
                int middleY = first.Group.Strip.Top + Math.Max(1, first.Group.Strip.Height / 2);
                int nextX = first.Group.Strip.Right + 4;
                foreach (ToolbarRestorePlacement placement in row.Skip(1))
                {
                    int requestedX = Math.Max(nextX, placement.Location.X);
                    toolStripPanelMain.Join(placement.Group.Strip, new Point(requestedX, middleY));
                    toolStripPanelMain.PerformLayout();
                    nextX = placement.Group.Strip.Right + 4;
                }
            }
            foreach (ToolbarGroup group in toolbarGroups) group.Strip.Visible = visibility[group.Id];
            toolStripPanelMain.PerformLayout();
            updateEditorChromeInset();
            suppressToolbarLayoutSave = false;
            toolbarLayoutReady = true;
        }

        private IReadOnlyList<ToolbarRestorePlacement> createToolbarRestorePlacements()
        {
            bool hasStoredLayout = toolbarGroups.Any(group => toolbarLayoutSettings.GetRow(group.Id).HasValue);
            if (hasStoredLayout)
                return toolbarGroups.Select(group => new ToolbarRestorePlacement(group,
                    scaleToolbarLocation(toolbarLayoutSettings.GetLocation(group.Id)) ??
                    new Point(0, getInitialToolbarRow(group.Id) * 33),
                    toolbarLayoutSettings.GetRow(group.Id) ?? getInitialToolbarRow(group.Id),
                    toolbarLayoutSettings.GetOrder(group.Id) ?? 0)).ToArray();

            var nextXByRow = new Dictionary<int, int>();
            var placements = new List<ToolbarRestorePlacement>();
            foreach (ToolbarGroup group in toolbarGroups.OrderBy(item => getInitialToolbarRow(item.Id)))
            {
                int row = getInitialToolbarRow(group.Id);
                int x = nextXByRow.TryGetValue(row, out int nextX) ? nextX : 0;
                int order = placements.Count(item => item.Row == row);
                placements.Add(new ToolbarRestorePlacement(group, new Point(x, row * 33), row, order));
                nextXByRow[row] = x + group.Strip.PreferredSize.Width + 4;
            }
            return placements;
        }

        private Point? scaleToolbarLocation(Point? logicalLocation)
        {
            if (!logicalLocation.HasValue) return null;
            return new Point(scaled(logicalLocation.Value.X), scaled(logicalLocation.Value.Y));
        }

        private static ToolStrip createCategoryToolbar(ToolbarGroupDefinition definition) => new ToolStrip
        {
            Name = definition.ControlName
        };

        private static void configureCategoryToolbar(ToolStrip strip, ToolbarGroupDefinition definition)
        {
            strip.AccessibleName = "Barra " + definition.DisplayName;
            strip.AllowItemReorder = false;
            strip.AutoSize = true;
            strip.BackColor = AppTheme.Chrome;
            strip.CanOverflow = true;
            strip.Dock = DockStyle.None;
            strip.GripStyle = ToolStripGripStyle.Visible;
            strip.ImageScalingSize = new Size(20, 20);
            strip.Padding = new Padding(1);
            strip.RenderMode = ToolStripRenderMode.System;
            strip.Text = definition.DisplayName;
        }

        private void toolbarVisibilityMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is ToolbarGroup group)) return;
            bool visible = !group.Strip.Visible;
            group.Strip.Visible = visible;
            item.Checked = visible;
            toolbarLayoutSettings.SetVisible(group.Id, visible);
            toolStripPanelMain.PerformLayout();
            updateEditorChromeInset();
            persistToolbarLayout();
        }

        private void toolbarChrome_SizeChanged(object sender, EventArgs e) => updateEditorChromeInset();

        private void toolbarStrip_LayoutChanged(object sender, EventArgs e)
        {
            if (!toolbarLayoutReady || suppressToolbarLayoutSave || toolbarLayoutSaveTimer == null) return;
            toolbarLayoutSaveTimer.Stop();
            toolbarLayoutSaveTimer.Start();
        }

        private void toolbarLayoutSaveTimer_Tick(object sender, EventArgs e)
        {
            toolbarLayoutSaveTimer.Stop();
            persistToolbarLayout();
        }

        private void updateEditorChromeInset()
        {
            if (panelEditor == null || toolStripPanelMain == null || menuStripMain == null) return;
            int chromeBottom = Math.Max(menuStripMain.Bottom, toolStripPanelMain.Bottom);
            int requiredTop = Math.Max(3, chromeBottom - panelEditor.Top + 3);
            Padding current = panelEditor.Padding;
            if (current.Top == requiredTop) return;
            panelEditor.Padding = new Padding(current.Left, requiredTop, current.Right, current.Bottom);
        }

        private void updateToolbarVisibilityMenu()
        {
            if (toolbarsMenuItem == null) return;
            foreach (ToolStripMenuItem item in toolbarsMenuItem.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Tag is ToolbarGroup group) item.Checked = group.Strip.Visible;
        }

        private IEnumerable<ToolStrip> enumerateToolbars()
        {
            if (toolbarGroups.Count == 0) return new[] { toolStripMain };
            return toolbarGroups.Select(group => group.Strip);
        }

        private IEnumerable<ToolStripItem> enumerateToolbarItems() =>
            enumerateToolbars().SelectMany(strip => strip.Items.Cast<ToolStripItem>());

        private void setToolbarsBackColor(Color color)
        {
            if (toolStripPanelMain != null) toolStripPanelMain.BackColor = color;
            foreach (ToolStrip strip in enumerateToolbars()) strip.BackColor = color;
        }

        private void persistToolbarLayout()
        {
            if (saveToolbarLayoutSetting == null || toolbarLayoutSettings == null) return;
            ToolbarGroup[] visibleGroups = toolbarGroups.Where(group => group.Strip.Visible)
                .OrderBy(group => group.Strip.Top).ThenBy(group => group.Strip.Left).ToArray();
            int[] rowTops = visibleGroups.Select(group => group.Strip.Top).Distinct().OrderBy(value => value).ToArray();
            foreach (int rowTop in rowTops)
            {
                ToolbarGroup[] row = visibleGroups.Where(group => group.Strip.Top == rowTop)
                    .OrderBy(group => group.Strip.Left).ToArray();
                for (int order = 0; order < row.Length; order++)
                {
                    ToolbarGroup group = row[order];
                    toolbarLayoutSettings.Set(group.Id, true, new Point(
                        DpiLayout.ToLogical(group.Strip.Left, DeviceDpi),
                        DpiLayout.ToLogical(group.Strip.Top, DeviceDpi)),
                        Array.IndexOf(rowTops, rowTop), order);
                }
            }
            foreach (ToolbarGroup group in toolbarGroups.Where(group => !group.Strip.Visible))
                toolbarLayoutSettings.SetVisible(group.Id, false);
            try
            {
                saveToolbarLayoutSetting(toolbarLayoutSettings.Serialize());
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare le barre degli strumenti: " + exception.Message;
            }
        }

        private void disposeFloatingToolbars()
        {
            if (toolbarLayoutSaveTimer == null) return;
            toolbarLayoutSaveTimer.Stop();
            toolbarLayoutSaveTimer.Dispose();
            toolbarLayoutSaveTimer = null;
        }

        private sealed class ToolbarGroupDefinition
        {
            public ToolbarGroupDefinition(string id, string displayName, string controlName, string[] itemNames)
            { Id = id; DisplayName = displayName; ControlName = controlName; ItemNames = itemNames; }
            public string Id { get; }
            public string DisplayName { get; }
            public string ControlName { get; }
            public string[] ItemNames { get; }
        }

        private sealed class ToolbarGroup
        {
            public ToolbarGroup(string id, string displayName, ToolStrip strip)
            { Id = id; DisplayName = displayName; Strip = strip; }
            public string Id { get; }
            public string DisplayName { get; }
            public ToolStrip Strip { get; }
        }

        private sealed class ToolbarRestorePlacement
        {
            public ToolbarRestorePlacement(ToolbarGroup group, Point location, int row, int order)
            { Group = group; Location = location; Row = row; Order = order; }
            public ToolbarGroup Group { get; }
            public Point Location { get; }
            public int Row { get; }
            public int Order { get; }
        }
    }
}
