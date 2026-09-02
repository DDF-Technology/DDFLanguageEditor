using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal static class AccessibilitySupport
    {
        public static void Apply(Control root)
        {
            if (root == null) return;
            if (root is Form form && form.AutoScaleMode == AutoScaleMode.None)
            {
                form.AutoScaleDimensions = new SizeF(96F, 96F);
                form.AutoScaleMode = AutoScaleMode.Dpi;
            }

            applyControl(root);
        }

        private static void applyControl(Control control)
        {
            if (isInteractive(control) && string.IsNullOrWhiteSpace(control.AccessibleName))
                control.AccessibleName = inferAccessibleName(control);

            if (control is TextBoxBase textBox && textBox.ReadOnly &&
                string.IsNullOrWhiteSpace(textBox.AccessibleDescription))
                textBox.AccessibleDescription = "Contenuto in sola lettura.";

            if (control is ToolStrip strip) applyItems(strip.Items);
            foreach (Control child in control.Controls) applyControl(child);
        }

        private static void applyItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (!(item is ToolStripSeparator) && string.IsNullOrWhiteSpace(item.AccessibleName))
                {
                    item.AccessibleName = cleanText(!string.IsNullOrWhiteSpace(item.ToolTipText)
                        ? item.ToolTipText
                        : item.Text);
                }
                if (item is ToolStripDropDownItem dropDown) applyItems(dropDown.DropDownItems);
            }
        }

        private static bool isInteractive(Control control) =>
            control.TabStop || control is TextBoxBase || control is ComboBox ||
            control is NumericUpDown || control is ListBox || control is TreeView ||
            control is DataGridView || control is TabControl;

        private static string inferAccessibleName(Control control)
        {
            string text = cleanText(control.Text);
            if (!string.IsNullOrWhiteSpace(text)) return text;

            if (control.Parent is TableLayoutPanel table)
            {
                TableLayoutPanelCellPosition cell = table.GetCellPosition(control);
                Label label = table.Controls.OfType<Label>()
                    .FirstOrDefault(candidate =>
                    {
                        TableLayoutPanelCellPosition labelCell = table.GetCellPosition(candidate);
                        return labelCell.Row == cell.Row && labelCell.Column < cell.Column;
                    });
                text = cleanText(label?.Text);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            switch (control.Name)
            {
                case "richTextBoxMainEditor": return "Editor del codice";
                case "richTextBoxFoldedView": return "Editor del codice compresso";
                case "richTextBoxLineNumbers": return "Numeri di riga";
                case "richTextBoxOutput": return "Output del programma";
                case "treeViewOutline": return "Struttura del documento";
                case "listBoxDiagnostics": return "Elenco diagnostica";
                case "tabControlBottom": return "Palette Diagnostica e Output";
            }
            return splitIdentifier(control.Name);
        }

        private static string cleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            int shortcut = value.IndexOf('\t');
            if (shortcut >= 0) value = value.Substring(0, shortcut);
            return value.Replace("&", string.Empty).Trim().TrimEnd('.');
        }

        private static string splitIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Controllo";
            var result = new StringBuilder();
            foreach (char character in value)
            {
                if (result.Length > 0 && char.IsUpper(character) && !char.IsWhiteSpace(result[result.Length - 1]))
                    result.Append(' ');
                result.Append(character);
            }
            return result.ToString().Trim();
        }
    }

    internal static class DpiLayout
    {
        public static int ScaleLogical(int logicalPixels, int dpi) =>
            Math.Max(1, (int)Math.Round(logicalPixels * Math.Max(96, dpi) / 96D));

        public static int ToLogical(int devicePixels, int dpi) =>
            Math.Max(1, (int)Math.Round(devicePixels * 96D / Math.Max(96, dpi)));
    }
}
