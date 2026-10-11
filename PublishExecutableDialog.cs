using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class PublishExecutableDialog : Form
    {
        private readonly TextBox executablePathTextBox;
        private readonly TextBox iconPathTextBox;
        private readonly CheckBox defaultIconCheckBox;
        private readonly PictureBox iconPreview;
        private readonly Button publishButton;

        public PublishExecutableDialog(string executablePath,
            string iconPath)
        {
            Text = "Pubblica applicazione DDF";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 225);
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 3,
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            layout.Controls.Add(new Label
            {
                Text = "Applicazione:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            }, 0, 0);
            executablePathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 3)
            };
            layout.Controls.Add(executablePathTextBox, 1, 0);
            var browseExecutableButton = new Button
            {
                Text = "Sfoglia...",
                Dock = DockStyle.Fill,
                UseVisualStyleBackColor = true
            };
            browseExecutableButton.Click += browseExecutableButton_Click;
            layout.Controls.Add(browseExecutableButton, 2, 0);

            layout.Controls.Add(new Label
            {
                Text = "Icona .ico:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            }, 0, 1);
            iconPathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 3)
            };
            iconPathTextBox.TextChanged += (sender, args) => updatePreview();
            layout.Controls.Add(iconPathTextBox, 1, 1);
            var browseIconButton = new Button
            {
                Name = "browsePublishIconButton",
                Text = "Sfoglia...",
                Dock = DockStyle.Fill,
                UseVisualStyleBackColor = true
            };
            browseIconButton.Click += browseIconButton_Click;
            layout.Controls.Add(browseIconButton, 2, 1);

            defaultIconCheckBox = new CheckBox
            {
                Text = "Usa l'icona DDF predefinita",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Checked = string.IsNullOrWhiteSpace(iconPath)
            };
            defaultIconCheckBox.CheckedChanged += (sender, args) =>
            {
                iconPathTextBox.Enabled = !defaultIconCheckBox.Checked;
                browseIconButton.Enabled = !defaultIconCheckBox.Checked;
                updatePreview();
            };
            layout.SetColumnSpan(defaultIconCheckBox, 2);
            layout.Controls.Add(defaultIconCheckBox, 1, 2);

            iconPreview = new PictureBox
            {
                Name = "publishIconPreview",
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            layout.Controls.Add(iconPreview, 0, 3);
            var hint = new Label
            {
                Text = "L'icona viene incorporata nell'EXE. Sono accettati " +
                    "file ICO, preferibilmente multi-risoluzione.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 7, 0, 0)
            };
            layout.SetColumnSpan(hint, 2);
            layout.Controls.Add(hint, 1, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var cancelButton = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            publishButton = new Button
            {
                Name = "confirmPublishExecutableButton",
                Text = "Pubblica",
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            publishButton.Click += publishButton_Click;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(publishButton);
            layout.SetColumnSpan(buttons, 3);
            layout.Controls.Add(buttons, 0, 4);

            Controls.Add(layout);
            AcceptButton = publishButton;
            CancelButton = cancelButton;
            ExecutablePath = executablePath;
            IconPath = iconPath;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ExecutablePath
        {
            get => executablePathTextBox.Text.Trim();
            set => executablePathTextBox.Text = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IconPath
        {
            get => defaultIconCheckBox.Checked
                ? string.Empty
                : iconPathTextBox.Text.Trim();
            set
            {
                string path = value ?? string.Empty;
                iconPathTextBox.Text = path;
                defaultIconCheckBox.Checked = path.Length == 0;
                iconPathTextBox.Enabled = path.Length != 0;
                updatePreview();
            }
        }

        private void browseExecutableButton_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Scegli nome e cartella del package EXE",
                Filter = "Applicazione Windows (*.exe)|*.exe",
                DefaultExt = "exe",
                AddExtension = true,
                CheckPathExists = true,
                OverwritePrompt = false,
                FileName = Path.GetFileName(ExecutablePath)
            };
            string directory = Path.GetDirectoryName(ExecutablePath);
            if (Directory.Exists(directory)) dialog.InitialDirectory = directory;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                ExecutablePath = dialog.FileName;
        }

        private void browseIconButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Scegli l'icona dell'applicazione",
                Filter = "Icone Windows (*.ico)|*.ico",
                CheckFileExists = true,
                Multiselect = false
            };
            string directory = Path.GetDirectoryName(iconPathTextBox.Text);
            if (Directory.Exists(directory)) dialog.InitialDirectory = directory;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                iconPathTextBox.Text = dialog.FileName;
        }

        private void publishButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExecutablePath) ||
                !string.Equals(Path.GetExtension(ExecutablePath), ".exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Scegli un percorso EXE valido.",
                    "Pubblica applicazione", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (!defaultIconCheckBox.Checked &&
                (!File.Exists(IconPath) || !string.Equals(
                    Path.GetExtension(IconPath), ".ico",
                    StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Scegli un file ICO valido oppure usa " +
                        "l'icona DDF predefinita.", "Pubblica applicazione",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void updatePreview()
        {
            Image previous = iconPreview?.Image;
            Image next = null;
            try
            {
                if (defaultIconCheckBox == null || defaultIconCheckBox.Checked)
                {
                    using Icon icon = AppIconProvider.LoadIcon();
                    next = icon?.ToBitmap();
                }
                else if (File.Exists(iconPathTextBox.Text))
                {
                    using var icon = new Icon(iconPathTextBox.Text,
                        new Size(48, 48));
                    next = icon.ToBitmap();
                }
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            if (iconPreview != null) iconPreview.Image = next;
            previous?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) iconPreview?.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
