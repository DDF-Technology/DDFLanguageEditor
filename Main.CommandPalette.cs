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
            registerShellCommands();

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
            panelEditor.CommandService.Refresh();
        }

        private IReadOnlyList<CommandPaletteCommand> createCommandPaletteCommands()
        {
            return panelEditor.CommandService.Commands
                .Where(command => command.IsVisible &&
                    command.Id != DdfEditorCommandIds.ShowCommandPalette)
                .Select(command => new CommandPaletteCommand(
                    command, () => panelEditor.CommandService.TryExecute(command.Id)))
                .OrderBy(command => command.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(command => command.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        private void registerShellCommands()
        {
            foreach (ToolStripMenuItem categoryItem in menuStripMain.Items.OfType<ToolStripMenuItem>())
            {
                string category = cleanMenuText(categoryItem.Text);
                foreach (ToolStripMenuItem item in categoryItem.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (ReferenceEquals(item, recentMenuItem) || item.DropDownItems.Count > 0 ||
                        string.IsNullOrWhiteSpace(item.Text) || string.IsNullOrWhiteSpace(item.Name)) continue;
                    ToolStripMenuItem commandItem = item;
                    panelEditor.CommandService.Register(
                        getShellCommandId(commandItem),
                        cleanMenuText(commandItem.Text),
                        category,
                        () => commandItem.PerformClick(),
                        () => canExecuteShellCommand(commandItem),
                        () => commandItem.Available,
                        () => commandItem.Checked,
                        shortcutTextProvider: () => formatShortcut(commandItem.ShortcutKeys));
                }
            }
        }

        private static string getShellCommandId(ToolStripMenuItem item)
        {
            switch (item.Name)
            {
                case "newMenuItem": return DdfEditorCommandIds.NewDocument;
                case "openMenuItem": return DdfEditorCommandIds.OpenDocument;
                case "openWorkspaceMenuItem": return DdfEditorCommandIds.OpenWorkspace;
                case "saveMenuItem": return DdfEditorCommandIds.SaveDocument;
                case "saveAsMenuItem": return DdfEditorCommandIds.SaveDocumentAs;
                case "undoMenuItem": return DdfEditorCommandIds.Undo;
                case "redoMenuItem": return DdfEditorCommandIds.Redo;
                case "cutMenuItem": return DdfEditorCommandIds.Cut;
                case "copyMenuItem": return DdfEditorCommandIds.Copy;
                case "pasteMenuItem": return DdfEditorCommandIds.Paste;
                case "selectAllMenuItem": return DdfEditorCommandIds.SelectAll;
                case "findMenuItem": return DdfEditorCommandIds.Find;
                case "replaceMenuItem": return DdfEditorCommandIds.Replace;
                case "completionMenuItem": return DdfEditorCommandIds.Completion;
                case "formatDocumentMenuItem": return DdfEditorCommandIds.FormatDocument;
                case "goToDefinitionMenuItem": return DdfEditorCommandIds.GoToDefinition;
                case "validateProjectMenuItem": return DdfEditorCommandIds.ValidateScript;
                case "buildProjectMenuItem": return DdfEditorCommandIds.PrepareScript;
                case "runWithoutDebugMenuItem": return DdfEditorCommandIds.RunWithoutDebug;
                case "runProgramMenuItem": return DdfEditorCommandIds.StartDebugging;
                case "stopProgramMenuItem": return DdfEditorCommandIds.StopDebugging;
                case "toggleBreakpointMenuItem": return DdfEditorCommandIds.ToggleBreakpoint;
                case "toggleFoldMenuItem": return DdfEditorCommandIds.ToggleFold;
                case "expandAllFoldsMenuItem": return DdfEditorCommandIds.ExpandAllFolds;
                case "commandPaletteMenuItem": return DdfEditorCommandIds.ShowCommandPalette;
                default: return "shell." + item.Name;
            }
        }

        private bool canExecuteShellCommand(ToolStripMenuItem item)
        {
            if (item == null || !item.Available) return false;
            if (debugUiLocked && DebugUnsafeMenuCommands.Contains(item.Name)) return false;
            bool folded = richTextBoxFoldedView != null && richTextBoxFoldedView.Visible;
            RichTextBox editor = folded ? richTextBoxFoldedView : richTextBoxMainEditor;
            switch (item.Name)
            {
                case "saveMenuItem": return documentSession.IsDirty || !documentSession.HasPath;
                case "undoMenuItem": return !folded && richTextBoxMainEditor.CanUndo;
                case "redoMenuItem": return !folded && richTextBoxMainEditor.CanRedo;
                case "cutMenuItem": return !folded && richTextBoxMainEditor.SelectionLength > 0;
                case "copyMenuItem": return editor != null && editor.SelectionLength > 0;
                case "selectAllMenuItem": return editor != null && editor.TextLength > 0;
                case "completionMenuItem": return !folded;
                case "formatDocumentMenuItem": return richTextBoxMainEditor.TextLength > 0;
                case "runProgramMenuItem":
                    return !executionRunning || panelEditor.DebugService?.IsPaused == true;
                case "runWithoutDebugMenuItem":
                case "validateProjectMenuItem":
                    return !executionRunning;
                case "buildProjectMenuItem": return !embeddedHostMode &&
                    !executionRunning && !compiledBuildRunning;
                case "stopProgramMenuItem": return executionRunning;
                default: return item.Enabled;
            }
        }

        private void refreshEditorCommandCatalog()
        {
            panelEditor?.CommandService?.Refresh();
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
