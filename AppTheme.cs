using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal static class AppTheme
    {
        public static readonly Color Window = Color.FromArgb(245, 245, 245);
        public static readonly Color Surface = Color.White;
        public static readonly Color Chrome = Color.FromArgb(250, 250, 250);
        public static readonly Color Border = Color.FromArgb(204, 204, 204);
        public static readonly Color Text = Color.FromArgb(32, 32, 32);
        public static readonly Color MutedText = Color.FromArgb(96, 96, 96);
        public static readonly Color Accent = Color.FromArgb(0, 102, 184);
        public static readonly Color Error = Color.FromArgb(176, 0, 32);
        public static readonly Color Run = Color.FromArgb(16, 112, 48);
        public static readonly Color Stop = Color.FromArgb(176, 32, 32);

        public static void ApplyLight(Control root)
        {
            if (root == null || IsEditorSurface(root)) return;

            root.ForeColor = Text;
            if (root is Form || root is Panel || root is TabControl) root.BackColor = Window;
            if (root is TabPage || root is TextBox || root is RichTextBox || root is ListBox || root is TreeView)
                root.BackColor = Surface;
            if (root is Splitter) root.BackColor = Border;

            if (root is Button button)
            {
                button.BackColor = Chrome;
                button.ForeColor = Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.UseVisualStyleBackColor = false;
            }
            else if (root is LinkLabel link)
            {
                link.LinkColor = Accent;
                link.ActiveLinkColor = Color.FromArgb(0, 76, 138);
                link.VisitedLinkColor = Accent;
            }
            else if (root is MenuStrip menu)
            {
                menu.BackColor = Chrome;
                ApplyItems(menu.Items);
            }
            else if (root is ToolStrip strip)
            {
                strip.BackColor = Chrome;
                strip.ForeColor = Text;
                strip.RenderMode = ToolStripRenderMode.System;
                ApplyItems(strip.Items);
            }

            foreach (Control child in root.Controls) ApplyLight(child);
        }

        private static void ApplyItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.BackColor = Chrome;
                item.ForeColor = Text;
                if (item is ToolStripDropDownItem dropDown) ApplyItems(dropDown.DropDownItems);
            }
        }

        private static bool IsEditorSurface(Control control)
        {
            return control.Name == "richTextBoxMainEditor" ||
                   control.Name == "richTextBoxFoldedView" ||
                   control.Name == "richTextBoxLineNumbers";
        }
    }
}
