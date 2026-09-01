using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class ExternalChangeForm : Form
    {
        public ExternalChangeForm(string path, string bufferSource, string diskSource, bool fileExists, bool bufferIsDirty)
        {
            string displayName = string.IsNullOrWhiteSpace(path) ? "documento" : Path.GetFileName(path);
            Name = "ExternalChangeForm";
            Text = "Modifica esterna — " + displayName;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 460);
            ClientSize = new Size(980, 620);
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            string state = fileExists
                ? "Il file è stato modificato da un altro programma."
                : "Il file è stato eliminato o spostato da un altro programma.";
            string dirtyWarning = bufferIsDirty
                ? " Il buffer contiene anche modifiche non salvate."
                : string.Empty;
            layout.Controls.Add(new Label
            {
                Name = "externalChangeMessageLabel",
                Text = state + dirtyWarning + "\nConfronta le versioni prima di scegliere; nessuna azione verrà eseguita automaticamente.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, 0);

            var comparison = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            comparison.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            comparison.Controls.Add(new Label
            {
                Name = "externalChangeBufferLabel",
                Text = "Versione nell'editor",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            comparison.Controls.Add(new Label
            {
                Name = "externalChangeDiskLabel",
                Text = fileExists ? "Versione su disco" : "Versione su disco — file non disponibile",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 0);
            comparison.Controls.Add(createViewer("externalChangeBufferTextBox", bufferSource), 0, 1);
            comparison.Controls.Add(createViewer("externalChangeDiskTextBox",
                fileExists ? diskSource : "[Il file non esiste più nel percorso originale]"), 1, 1);
            layout.Controls.Add(comparison, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            var postpone = new Button
            {
                Name = "externalChangePostponeButton",
                Text = "Più tardi",
                DialogResult = DialogResult.Cancel,
                Size = new Size(105, 30)
            };
            var keep = new Button
            {
                Name = "externalChangeKeepButton",
                Text = "Mantieni buffer",
                DialogResult = DialogResult.No,
                Size = new Size(125, 30)
            };
            var reload = new Button
            {
                Name = "externalChangeReloadButton",
                Text = "Ricarica da disco",
                DialogResult = DialogResult.Yes,
                Enabled = fileExists,
                Size = new Size(135, 30)
            };
            buttons.Controls.Add(postpone);
            buttons.Controls.Add(keep);
            buttons.Controls.Add(reload);
            layout.Controls.Add(buttons, 0, 2);

            Controls.Add(layout);
            AcceptButton = fileExists ? reload : keep;
            CancelButton = postpone;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        private static RichTextBox createViewer(string name, string source)
        {
            return new RichTextBox
            {
                Name = name,
                Text = source ?? string.Empty,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                DetectUrls = false,
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Both
            };
        }
    }
}
