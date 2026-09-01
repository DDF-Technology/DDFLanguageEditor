using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private IReadOnlyList<ShortcutCommandDefinition> shortcutDefinitions;
        private ShortcutSettings shortcutSettings;
        private Action<string> saveShortcutSettingsSetting;

        private static readonly IReadOnlyDictionary<string, string> ToolbarCommandNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "toolbarNewButton", "newMenuItem" },
                { "toolbarOpenButton", "openMenuItem" },
                { "toolbarSaveButton", "saveMenuItem" },
                { "toolbarSaveAllButton", "saveAllMenuItem" },
                { "toolbarCloseDocumentButton", "closeDocumentMenuItem" },
                { "toolbarUndoButton", "undoMenuItem" },
                { "toolbarRedoButton", "redoMenuItem" },
                { "toolbarCutButton", "cutMenuItem" },
                { "toolbarCopyButton", "copyMenuItem" },
                { "toolbarPasteButton", "pasteMenuItem" },
                { "toolbarCommentButton", "toggleLineCommentMenuItem" },
                { "toolbarDuplicateLinesButton", "duplicateLinesMenuItem" },
                { "toolbarMoveLinesUpButton", "moveLinesUpMenuItem" },
                { "toolbarMoveLinesDownButton", "moveLinesDownMenuItem" },
                { "toolbarDeleteLinesButton", "deleteLinesMenuItem" },
                { "toolbarExpandSelectionButton", "expandSelectionMenuItem" },
                { "toolbarShrinkSelectionButton", "shrinkSelectionMenuItem" },
                { "toolbarMatchingDelimiterButton", "matchingDelimiterMenuItem" },
                { "toolbarSelectNextOccurrenceButton", "selectNextOccurrenceMenuItem" },
                { "toolbarSelectAllOccurrencesButton", "selectAllOccurrencesMenuItem" },
                { "toolbarFindButton", "findMenuItem" },
                { "toolbarWorkspaceSearchButton", "workspaceSearchMenuItem" },
                { "toolbarWorkspaceReplaceButton", "workspaceReplaceMenuItem" },
                { "toolbarCommandPaletteButton", "commandPaletteMenuItem" },
                { "toolbarSettingsButton", "settingsMenuItem" },
                { "toolbarNavigateBackButton", "navigateBackMenuItem" },
                { "toolbarNavigateForwardButton", "navigateForwardMenuItem" },
                { "toolbarGoToFileButton", "goToFileMenuItem" },
                { "toolbarGoToSymbolButton", "goToSymbolMenuItem" },
                { "toolbarFindReferencesButton", "findReferencesMenuItem" },
                { "toolbarGoToLineButton", "goToLineMenuItem" },
                { "toolbarGoToLastEditButton", "goToLastEditMenuItem" },
                { "toolbarCompletionButton", "completionMenuItem" },
                { "toolbarQuickFixButton", "quickFixMenuItem" },
                { "toolbarFormatButton", "formatDocumentMenuItem" },
                { "toolbarFoldButton", "toggleFoldMenuItem" },
                { "toolbarBreakpointButton", "toggleBreakpointMenuItem" },
                { "toolbarRunButton", "runProgramMenuItem" },
                { "toolbarStopButton", "stopProgramMenuItem" }
            };

        private void initializeShortcutSettings()
        {
            var definitions = new List<ShortcutCommandDefinition>();
            foreach (ToolStripMenuItem category in menuStripMain.Items.OfType<ToolStripMenuItem>())
            {
                collectShortcutDefinitions(category.DropDownItems, cleanMenuText(category.Text), definitions);
            }
            shortcutDefinitions = definitions.AsReadOnly();
            shortcutSettings = ShortcutSettings.Parse(AppSettingsStore.LoadShortcutSettings(), shortcutDefinitions);
            saveShortcutSettingsSetting = AppSettingsStore.SaveShortcutSettings;
            applyShortcutSettings();
        }

        private void collectShortcutDefinitions(ToolStripItemCollection items, string category,
            ICollection<ShortcutCommandDefinition> definitions)
        {
            foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
            {
                if (ReferenceEquals(item, recentMenuItem) || ReferenceEquals(item, recentWorkspacesMenuItem)) continue;
                if (item.DropDownItems.Count > 0)
                {
                    collectShortcutDefinitions(item.DropDownItems, category, definitions);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Text)) continue;
                definitions.Add(new ShortcutCommandDefinition(item.Name, category, cleanMenuText(item.Text), item.ShortcutKeys));
            }
        }

        private void applyShortcutSettings()
        {
            if (shortcutSettings == null) return;
            Dictionary<string, ToolStripMenuItem> commands = enumerateMenuItems(menuStripMain.Items)
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            foreach (ShortcutCommandDefinition definition in shortcutDefinitions)
            {
                if (commands.TryGetValue(definition.Id, out ToolStripMenuItem item))
                    item.ShortcutKeys = shortcutSettings.Get(definition.Id);
            }
            updateToolbarShortcutToolTips(commands);
        }

        private static IEnumerable<ToolStripMenuItem> enumerateMenuItems(ToolStripItemCollection items)
        {
            foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
            {
                yield return item;
                foreach (ToolStripMenuItem child in enumerateMenuItems(item.DropDownItems)) yield return child;
            }
        }

        private void updateToolbarShortcutToolTips(IReadOnlyDictionary<string, ToolStripMenuItem> commands)
        {
            foreach (ToolStripButton button in toolStripMain.Items.OfType<ToolStripButton>())
            {
                if (!ToolbarCommandNames.TryGetValue(button.Name, out string commandName) ||
                    !commands.TryGetValue(commandName, out ToolStripMenuItem command)) continue;
                string shortcut = formatShortcut(command.ShortcutKeys);
                button.ToolTipText = cleanMenuText(command.Text) +
                    (string.IsNullOrEmpty(shortcut) ? string.Empty : " (" + shortcut + ")");
                button.AccessibleName = button.ToolTipText;
            }
        }

        private void persistShortcutSettings()
        {
            if (saveShortcutSettingsSetting == null || shortcutSettings == null) return;
            try
            {
                saveShortcutSettingsSetting(shortcutSettings.Serialize());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare le scorciatoie: " + exception.Message;
            }
        }

        private static bool matchesShortcut(ToolStripMenuItem item, Keys keyData)
        {
            return item != null && item.ShortcutKeys != Keys.None && item.ShortcutKeys == keyData;
        }

        private bool shouldSuppressDisplacedNativeShortcut(Keys keyData)
        {
            bool isNativeEditorShortcut = keyData == (Keys.Control | Keys.A) ||
                keyData == (Keys.Control | Keys.C) || keyData == (Keys.Control | Keys.X) ||
                keyData == (Keys.Control | Keys.V) || keyData == (Keys.Control | Keys.Z) ||
                keyData == (Keys.Control | Keys.Y);
            return isNativeEditorShortcut && !enumerateMenuItems(menuStripMain.Items)
                .Any(item => item.ShortcutKeys != Keys.None && item.ShortcutKeys == keyData);
        }
    }
}
