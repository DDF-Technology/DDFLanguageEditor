using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem commandPaletteMenuItem;
        private ToolStripButton toolbarCommandPaletteButton;
        private Func<CommandPaletteForm, DialogResult> showCommandPaletteDialog;

        private void initializeCommandPalette()
        {
            commandPaletteMenuItem = new ToolStripMenuItem("Command Palette...")
            {
                Name = "commandPaletteMenuItem",
                ShortcutKeys = Keys.Control | Keys.Shift | Keys.P
            };
            commandPaletteMenuItem.Click += commandPaletteMenuItem_Click;
            viewMenuItem.DropDownItems.Add(new ToolStripSeparator());
            viewMenuItem.DropDownItems.Add(commandPaletteMenuItem);

            toolbarCommandPaletteButton = createToolbarButton(
                "toolbarCommandPaletteButton",
                "\uE945",
                "Command Palette (Ctrl+Shift+P)",
                commandPaletteMenuItem_Click);
            int toolbarIndex = toolStripMain.Items.IndexOf(toolbarBreakpointButton);
            toolStripMain.Items.Insert(toolbarIndex, toolbarCommandPaletteButton);
            showCommandPaletteDialog = dialog => dialog.ShowDialog(this);
        }

        private void commandPaletteMenuItem_Click(object sender, EventArgs e)
        {
            refreshCommandStatesForPalette();
            IReadOnlyList<CommandPaletteCommand> commands = createCommandPaletteCommands();
            using (var dialog = new CommandPaletteForm(commands))
            {
                if (showCommandPaletteDialog(dialog) != DialogResult.OK) return;
                CommandPaletteCommand command = dialog.SelectedCommand;
                if (command?.Enabled == true) command.Execute();
            }
        }

        private void refreshCommandStatesForPalette()
        {
            updateDocumentUi();
            editMenuItem_DropDownOpening(editMenuItem, EventArgs.Empty);
            runMenuItem_DropDownOpening(runMenuItem, EventArgs.Empty);
            viewMenuItem_DropDownOpening(viewMenuItem, EventArgs.Empty);
            updateNavigationHistoryCommandState();
        }

        private IReadOnlyList<CommandPaletteCommand> createCommandPaletteCommands()
        {
            var commands = new List<CommandPaletteCommand>();
            foreach (ToolStripMenuItem categoryItem in menuStripMain.Items.OfType<ToolStripMenuItem>())
            {
                string category = cleanMenuText(categoryItem.Text);
                foreach (ToolStripMenuItem item in categoryItem.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (ReferenceEquals(item, recentMenuItem) || ReferenceEquals(item, commandPaletteMenuItem) ||
                        item.DropDownItems.Count > 0 || string.IsNullOrWhiteSpace(item.Text)) continue;
                    ToolStripMenuItem commandItem = item;
                    commands.Add(new CommandPaletteCommand(
                        cleanMenuText(commandItem.Text),
                        category,
                        formatShortcut(commandItem.ShortcutKeys),
                        commandItem.Enabled,
                        () => commandItem.PerformClick()));
                }
            }
            return commands
                .OrderBy(command => command.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(command => command.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        private static string cleanMenuText(string text)
        {
            return (text ?? string.Empty).Replace("&", string.Empty).Trim();
        }

        private static string formatShortcut(Keys shortcut)
        {
            if (shortcut == Keys.None) return string.Empty;
            return new KeysConverter().ConvertToString(shortcut) ?? string.Empty;
        }
    }
}
