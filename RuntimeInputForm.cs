using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class RuntimeInputForm : Form
    {
        private readonly TextBox inputTextBox;

        public RuntimeInputForm()
        {
            Text = "Input DDF";
            Name = "RuntimeInputForm";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 118);
            BackColor = AppTheme.Window;
            ForeColor = AppTheme.Text;

            var label = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = "Valore richiesto da readLine():"
            };
            inputTextBox = new TextBox
            {
                Location = new Point(15, 37),
                Name = "runtimeInputTextBox",
                Size = new Size(390, 23)
            };
            var ok = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(249, 76),
                Name = "runtimeInputOkButton",
                Size = new Size(75, 28),
                Text = "OK"
            };
            var cancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(330, 76),
                Name = "runtimeInputCancelButton",
                Size = new Size(75, 28),
                Text = "Annulla"
            };
            Controls.Add(label);
            Controls.Add(inputTextBox);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public string InputText => inputTextBox.Text;
    }
}
