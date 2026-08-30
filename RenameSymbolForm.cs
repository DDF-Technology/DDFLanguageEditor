using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class RenameSymbolForm : Form
    {
        private readonly TextBox nameTextBox;

        public RenameSymbolForm(string currentName)
        {
            Text = "Rinomina simbolo";
            Name = "RenameSymbolForm";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(390, 116);

            var label = new Label
            {
                AutoSize = true,
                Location = new Point(14, 14),
                Text = "Nuovo nome per '" + currentName + "':"
            };
            nameTextBox = new TextBox
            {
                Location = new Point(17, 38),
                Name = "renameSymbolTextBox",
                Width = 356,
                Text = currentName
            };
            var confirm = new Button
            {
                Text = "Rinomina",
                DialogResult = DialogResult.OK,
                Location = new Point(207, 76),
                Name = "renameSymbolConfirmButton",
                Width = 80
            };
            var cancel = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Location = new Point(293, 76),
                Name = "renameSymbolCancelButton",
                Width = 80
            };
            Controls.AddRange(new Control[] { label, nameTextBox, confirm, cancel });
            AcceptButton = confirm;
            CancelButton = cancel;
            Shown += (sender, args) => { nameTextBox.SelectAll(); nameTextBox.Focus(); };
            AppTheme.ApplyLight(this);
        }

        public string SymbolName => nameTextBox.Text.Trim();
    }
}
