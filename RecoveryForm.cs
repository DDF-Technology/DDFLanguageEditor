using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class RecoveryForm : Form
    {
        public RecoveryForm(CrashRecoverySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Name = "RecoveryForm";
            Text = "Recupero dopo arresto anomalo";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(660, 390);
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.Controls.Add(new Label
            {
                Name = "recoveryMessageLabel",
                Text = "Sono stati trovati buffer conservati dopo una chiusura inattesa.\nIl ripristino non sovrascriverà i file originali.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var documents = new ListView
            {
                Name = "recoveryDocumentsListView",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            documents.Columns.Add("Documento", 430);
            documents.Columns.Add("Dimensione", 130);
            foreach (HotExitDocumentSnapshot document in snapshot.Documents)
            {
                string name = string.IsNullOrWhiteSpace(document.Path) ? "Senza titolo.ddf" : document.Path;
                documents.Items.Add(new ListViewItem(new[] { name, document.Source.Length + " caratteri" }));
            }
            layout.Controls.Add(documents, 0, 1);
            string savedAt = snapshot.SavedAtUtc == default
                ? "Ora del salvataggio non disponibile"
                : "Copia locale: " + snapshot.SavedAtUtc.ToLocalTime().ToString("g");
            layout.Controls.Add(new Label { Name = "recoveryTimestampLabel", Text = savedAt, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            var restore = new Button { Name = "recoveryRestoreButton", Text = "Ripristina", DialogResult = DialogResult.Yes, Size = new Size(100, 30) };
            var discard = new Button { Name = "recoveryDiscardButton", Text = "Ignora e rimuovi", DialogResult = DialogResult.No, Size = new Size(125, 30) };
            var later = new Button { Name = "recoveryLaterButton", Text = "Più tardi", DialogResult = DialogResult.Cancel, Size = new Size(95, 30) };
            buttons.Controls.Add(restore);
            buttons.Controls.Add(discard);
            buttons.Controls.Add(later);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            AcceptButton = restore;
            CancelButton = later;
            AppTheme.ApplyLight(this);
        }
    }
}
