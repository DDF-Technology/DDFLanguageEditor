using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ContextMenuStrip diagnosticsClipboardMenu;
        private ContextMenuStrip outputClipboardMenu;
        private ToolStripMenuItem copySelectedDiagnosticsItem;
        private ToolStripMenuItem copyAllDiagnosticsItem;
        private ToolStripMenuItem copyOutputSelectionItem;
        private ToolStripMenuItem copyAllOutputItem;

        private void initializePaletteClipboardCommands()
        {
            listBoxDiagnostics.SelectionMode = SelectionMode.MultiExtended;
            listBoxDiagnostics.KeyDown += listBoxDiagnostics_KeyDown;
            listBoxDiagnostics.MouseDown += listBoxDiagnostics_MouseDown;

            diagnosticsClipboardMenu = createPaletteClipboardMenu();
            copySelectedDiagnosticsItem = createPaletteClipboardItem(
                "copySelectedDiagnosticsItem", "Copia righe selezionate", copySelectedDiagnosticsItem_Click);
            copyAllDiagnosticsItem = createPaletteClipboardItem(
                "copyAllDiagnosticsItem", "Copia tutto", copyAllDiagnosticsItem_Click);
            diagnosticsClipboardMenu.Items.AddRange(new ToolStripItem[]
            {
                copySelectedDiagnosticsItem,
                copyAllDiagnosticsItem
            });
            diagnosticsClipboardMenu.Opening += diagnosticsClipboardMenu_Opening;
            listBoxDiagnostics.ContextMenuStrip = diagnosticsClipboardMenu;

            richTextBoxOutput.KeyDown += richTextBoxOutput_KeyDown;
            richTextBoxOutput.MouseDown += richTextBoxOutput_MouseDown;

            outputClipboardMenu = createPaletteClipboardMenu();
            copyOutputSelectionItem = createPaletteClipboardItem(
                "standardTextCopyItem", "Copia selezione o riga", copyOutputSelectionItem_Click);
            copyAllOutputItem = createPaletteClipboardItem(
                "copyAllOutputItem", "Copia tutto", copyAllOutputItem_Click);
            outputClipboardMenu.Items.AddRange(new ToolStripItem[]
            {
                copyOutputSelectionItem,
                copyAllOutputItem
            });
            outputClipboardMenu.Opening += outputClipboardMenu_Opening;
            richTextBoxOutput.ContextMenuStrip = outputClipboardMenu;
        }

        private ContextMenuStrip createPaletteClipboardMenu()
        {
            return new ContextMenuStrip(components)
            {
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F)
            };
        }

        private static ToolStripMenuItem createPaletteClipboardItem(
            string name, string text, EventHandler handler)
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

        private void diagnosticsClipboardMenu_Opening(object sender, CancelEventArgs e)
        {
            copySelectedDiagnosticsItem.Enabled = listBoxDiagnostics.SelectedItems.Count > 0;
            copyAllDiagnosticsItem.Enabled = listBoxDiagnostics.Items.Count > 0;
        }

        private void outputClipboardMenu_Opening(object sender, CancelEventArgs e)
        {
            copyOutputSelectionItem.Enabled = !string.IsNullOrEmpty(getOutputSelectionOrCurrentLine());
            copyAllOutputItem.Enabled = richTextBoxOutput.TextLength > 0;
        }

        private void listBoxDiagnostics_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C) return;

            copyPaletteText(getSelectedDiagnosticsText());
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void richTextBoxOutput_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C) return;

            copyPaletteText(getOutputSelectionOrCurrentLine());
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void listBoxDiagnostics_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            int index = listBoxDiagnostics.IndexFromPoint(e.Location);
            if (index < 0 || listBoxDiagnostics.SelectedIndices.Contains(index)) return;

            listBoxDiagnostics.ClearSelected();
            listBoxDiagnostics.SelectedIndex = index;
        }

        private void richTextBoxOutput_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            int position = richTextBoxOutput.GetCharIndexFromPosition(e.Location);
            int selectionStart = richTextBoxOutput.SelectionStart;
            int selectionEnd = selectionStart + richTextBoxOutput.SelectionLength;
            if (position >= selectionStart && position < selectionEnd) return;

            richTextBoxOutput.Select(position, 0);
        }

        private void copySelectedDiagnosticsItem_Click(object sender, EventArgs e)
        {
            copyPaletteText(getSelectedDiagnosticsText());
        }

        private void copyAllDiagnosticsItem_Click(object sender, EventArgs e)
        {
            var text = new StringBuilder();
            foreach (object item in listBoxDiagnostics.Items)
            {
                if (text.Length > 0) text.AppendLine();
                text.Append(item?.ToString() ?? string.Empty);
            }
            copyPaletteText(text.ToString());
        }

        private void copyOutputSelectionItem_Click(object sender, EventArgs e)
        {
            copyPaletteText(getOutputSelectionOrCurrentLine());
        }

        private void copyAllOutputItem_Click(object sender, EventArgs e)
        {
            copyPaletteText(richTextBoxOutput.Text);
        }

        private string getSelectedDiagnosticsText()
        {
            var text = new StringBuilder();
            foreach (object item in listBoxDiagnostics.SelectedItems)
            {
                if (text.Length > 0) text.AppendLine();
                text.Append(item?.ToString() ?? string.Empty);
            }
            return text.ToString();
        }

        private string getOutputSelectionOrCurrentLine()
        {
            if (richTextBoxOutput.SelectionLength > 0)
                return richTextBoxOutput.SelectedText;
            if (richTextBoxOutput.TextLength == 0)
                return string.Empty;

            int lineIndex = richTextBoxOutput.GetLineFromCharIndex(richTextBoxOutput.SelectionStart);
            string[] lines = richTextBoxOutput.Lines;
            return lineIndex >= 0 && lineIndex < lines.Length ? lines[lineIndex] : string.Empty;
        }

        private void copyPaletteText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetDataObject(text, true, 5, 100);
            }
            catch (ExternalException)
            {
                MessageBox.Show(this,
                    "Gli Appunti di Windows sono temporaneamente occupati. Riprova tra qualche istante.",
                    "Copia non riuscita", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
