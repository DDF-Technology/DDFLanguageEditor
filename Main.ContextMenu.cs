using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem toggleLineCommentMenuItem;
        private ToolStripMenuItem duplicateLinesMenuItem;
        private ToolStripMenuItem moveLinesUpMenuItem;
        private ToolStripMenuItem moveLinesDownMenuItem;
        private ToolStripMenuItem deleteLinesMenuItem;
        private ContextMenuStrip editorContextMenu;
        private ToolStripMenuItem contextUndoItem;
        private ToolStripMenuItem contextRedoItem;
        private ToolStripMenuItem contextCutItem;
        private ToolStripMenuItem contextCopyItem;
        private ToolStripMenuItem contextPasteItem;
        private ToolStripMenuItem contextSelectAllItem;
        private ToolStripMenuItem contextFindItem;
        private ToolStripMenuItem contextRenameItem;
        private ToolStripMenuItem contextCommentItem;
        private ToolStripMenuItem contextDuplicateLinesItem;
        private ToolStripMenuItem contextMoveLinesUpItem;
        private ToolStripMenuItem contextMoveLinesDownItem;
        private ToolStripMenuItem contextDeleteLinesItem;

        private void initializeEditingExperience()
        {
            toggleLineCommentMenuItem = new ToolStripMenuItem("Commenta/decommenta riga")
            {
                Name = "toggleLineCommentMenuItem",
                ShortcutKeys = Keys.Control | Keys.OemQuestion
            };
            toggleLineCommentMenuItem.Click += toggleLineCommentMenuItem_Click;
            int selectAllIndex = editMenuItem.DropDownItems.IndexOf(selectAllMenuItem);
            editMenuItem.DropDownItems.Insert(selectAllIndex + 1, toggleLineCommentMenuItem);
            duplicateLinesMenuItem = createEditingMenuItem("duplicateLinesMenuItem", "Duplica righe", Keys.Control | Keys.D, duplicateLinesMenuItem_Click);
            moveLinesUpMenuItem = createEditingMenuItem("moveLinesUpMenuItem", "Sposta righe su", Keys.Alt | Keys.Up, moveLinesUpMenuItem_Click);
            moveLinesDownMenuItem = createEditingMenuItem("moveLinesDownMenuItem", "Sposta righe giù", Keys.Alt | Keys.Down, moveLinesDownMenuItem_Click);
            deleteLinesMenuItem = createEditingMenuItem("deleteLinesMenuItem", "Elimina righe", Keys.Control | Keys.Shift | Keys.K, deleteLinesMenuItem_Click);
            int commentIndex = editMenuItem.DropDownItems.IndexOf(toggleLineCommentMenuItem);
            editMenuItem.DropDownItems.Insert(commentIndex + 1, duplicateLinesMenuItem);
            editMenuItem.DropDownItems.Insert(commentIndex + 2, moveLinesUpMenuItem);
            editMenuItem.DropDownItems.Insert(commentIndex + 3, moveLinesDownMenuItem);
            editMenuItem.DropDownItems.Insert(commentIndex + 4, deleteLinesMenuItem);

            editorContextMenu = new ContextMenuStrip
            {
                Name = "editorContextMenu",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F)
            };
            contextUndoItem = createContextItem("contextUndoItem", "Annulla", undoMenuItem_Click);
            contextRedoItem = createContextItem("contextRedoItem", "Ripristina", redoMenuItem_Click);
            contextCutItem = createContextItem("contextCutItem", "Taglia", cutMenuItem_Click);
            contextCopyItem = createContextItem("contextCopyItem", "Copia", copyMenuItem_Click);
            contextPasteItem = createContextItem("contextPasteItem", "Incolla", pasteMenuItem_Click);
            contextSelectAllItem = createContextItem("contextSelectAllItem", "Seleziona tutto", selectAllMenuItem_Click);
            contextFindItem = createContextItem("contextFindItem", "Trova...", findMenuItem_Click);
            contextRenameItem = createContextItem("contextRenameItem", "Rinomina simbolo...", renameSymbolMenuItem_Click);
            contextCommentItem = createContextItem("contextCommentItem", "Commenta/decommenta riga", toggleLineCommentMenuItem_Click);
            contextDuplicateLinesItem = createContextItem("contextDuplicateLinesItem", "Duplica righe", duplicateLinesMenuItem_Click);
            contextMoveLinesUpItem = createContextItem("contextMoveLinesUpItem", "Sposta righe su", moveLinesUpMenuItem_Click);
            contextMoveLinesDownItem = createContextItem("contextMoveLinesDownItem", "Sposta righe giù", moveLinesDownMenuItem_Click);
            contextDeleteLinesItem = createContextItem("contextDeleteLinesItem", "Elimina righe", deleteLinesMenuItem_Click);
            editorContextMenu.Items.AddRange(new ToolStripItem[]
            {
                contextUndoItem, contextRedoItem, new ToolStripSeparator(),
                contextCutItem, contextCopyItem, contextPasteItem, contextSelectAllItem,
                new ToolStripSeparator(), contextFindItem, contextRenameItem, contextCommentItem,
                new ToolStripSeparator(), contextDuplicateLinesItem, contextMoveLinesUpItem,
                contextMoveLinesDownItem, contextDeleteLinesItem
            });
            editorContextMenu.Opening += editorContextMenu_Opening;
            richTextBoxMainEditor.ContextMenuStrip = editorContextMenu;
        }

        private ToolStripMenuItem createContextItem(string name, string text, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text)
            {
                Name = name,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text
            };
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem createEditingMenuItem(string name, string text, Keys shortcut, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text) { Name = name, ShortcutKeys = shortcut };
            item.Click += handler;
            return item;
        }

        private void editorContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            editMenuItem_DropDownOpening(editMenuItem, EventArgs.Empty);
            contextUndoItem.Enabled = undoMenuItem.Enabled;
            contextRedoItem.Enabled = redoMenuItem.Enabled;
            contextCutItem.Enabled = cutMenuItem.Enabled;
            contextCopyItem.Enabled = copyMenuItem.Enabled;
            contextPasteItem.Enabled = pasteMenuItem.Enabled;
            contextSelectAllItem.Enabled = selectAllMenuItem.Enabled;
            contextFindItem.Enabled = true;
            contextRenameItem.Enabled = renameSymbolMenuItem.Enabled;
            contextCommentItem.Enabled = !richTextBoxFoldedView.Visible;
            contextDuplicateLinesItem.Enabled = duplicateLinesMenuItem.Enabled;
            contextMoveLinesUpItem.Enabled = moveLinesUpMenuItem.Enabled;
            contextMoveLinesDownItem.Enabled = moveLinesDownMenuItem.Enabled;
            contextDeleteLinesItem.Enabled = deleteLinesMenuItem.Enabled;
        }

        private void toggleLineCommentMenuItem_Click(object sender, EventArgs e)
        {
            toggleLineComment();
        }

        private void duplicateLinesMenuItem_Click(object sender, EventArgs e) => duplicateLines();
        private void moveLinesUpMenuItem_Click(object sender, EventArgs e) => moveLines(true);
        private void moveLinesDownMenuItem_Click(object sender, EventArgs e) => moveLines(false);
        private void deleteLinesMenuItem_Click(object sender, EventArgs e) => deleteLines();
    }
}
