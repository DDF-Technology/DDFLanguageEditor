using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal static class TextBoxEditingSupport
    {
        public static void Apply(Form form)
        {
            if (form == null) return;
            var menu = createMenu();
            foreach (TextBoxBase textBox in descendants(form).OfType<TextBoxBase>())
            {
                if (textBox.Name == "richTextBoxLineNumbers") continue;
                textBox.ShortcutsEnabled = true;
                if (textBox.ContextMenuStrip == null) textBox.ContextMenuStrip = menu;
            }
            form.Disposed += (sender, args) => menu.Dispose();
        }

        private static ContextMenuStrip createMenu()
        {
            var menu = new ContextMenuStrip
            {
                Name = "standardTextEditingContextMenu",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text
            };
            var undo = item("standardTextUndoItem", "Annulla");
            var cut = item("standardTextCutItem", "Taglia");
            var copy = item("standardTextCopyItem", "Copia");
            var paste = item("standardTextPasteItem", "Incolla");
            var delete = item("standardTextDeleteItem", "Elimina");
            var selectAll = item("standardTextSelectAllItem", "Seleziona tutto");
            menu.Items.AddRange(new ToolStripItem[]
            {
                undo, new ToolStripSeparator(), cut, copy, paste, delete,
                new ToolStripSeparator(), selectAll
            });
            menu.Opening += (sender, args) =>
            {
                TextBoxBase textBox = menu.SourceControl as TextBoxBase;
                bool hasSelection = textBox != null && textBox.SelectionLength > 0;
                bool editable = textBox != null && !textBox.ReadOnly;
                undo.Enabled = editable && textBox.CanUndo;
                cut.Enabled = editable && hasSelection;
                copy.Enabled = hasSelection;
                paste.Enabled = editable && clipboardContainsText();
                delete.Enabled = editable && hasSelection;
                selectAll.Enabled = textBox != null && textBox.TextLength > 0;
            };
            undo.Click += (sender, args) => withSource(menu, textBox => { if (!textBox.ReadOnly && textBox.CanUndo) textBox.Undo(); });
            cut.Click += (sender, args) => withSource(menu, textBox => { if (!textBox.ReadOnly) textBox.Cut(); });
            copy.Click += (sender, args) => withSource(menu, textBox => textBox.Copy());
            paste.Click += (sender, args) => withSource(menu, textBox => { if (!textBox.ReadOnly) textBox.Paste(); });
            delete.Click += (sender, args) => withSource(menu, textBox => { if (!textBox.ReadOnly) textBox.SelectedText = string.Empty; });
            selectAll.Click += (sender, args) => withSource(menu, textBox => textBox.SelectAll());
            return menu;
        }

        private static ToolStripMenuItem item(string name, string text)
        {
            return new ToolStripMenuItem(text)
            {
                Name = name,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text
            };
        }

        private static void withSource(ContextMenuStrip menu, Action<TextBoxBase> action)
        {
            if (menu.SourceControl is TextBoxBase textBox) action(textBox);
        }

        private static bool clipboardContainsText()
        {
            try { return Clipboard.ContainsText(); }
            catch (ExternalException) { return false; }
        }

        private static System.Collections.Generic.IEnumerable<Control> descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in descendants(child)) yield return nested;
            }
        }
    }
}
