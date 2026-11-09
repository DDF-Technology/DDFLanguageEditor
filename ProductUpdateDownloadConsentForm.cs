using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class ProductUpdateDownloadConsentForm : Form
    {
        public ProductUpdateDownloadConsentForm(DdfProductUpdateResult result,
            DdfProductUpdateVerifiedOffer offer)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (!result.IsActionable || result.AvailableVersion != offer.Version ||
                !Uri.Equals(result.VerifiedReleaseUri, offer.AssetUri))
                throw new ArgumentException("Offerta non correlata al controllo.",
                    nameof(offer));

            Name = "ProductUpdateDownloadConsentForm";
            Text = "Conferma download aggiornamento";
            AccessibleName = Text;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(590, 295);
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Scaricare la release verificata?",
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 0);
            layout.Controls.Add(new TextBox
            {
                Name = "productUpdateDownloadDetailsTextBox",
                AccessibleName = "Dettagli del download verificato",
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = "Versione corrente: " + result.CurrentVersion +
                    Environment.NewLine +
                    "Nuova versione: " + offer.Version + Environment.NewLine +
                    "Dimensione: " + (offer.AssetSize / 1048576d).ToString(
                        "0.##", CultureInfo.CurrentCulture) + " MB" +
                    Environment.NewLine +
                    "Origine: " + offer.AssetUri.Host +
                    offer.AssetUri.AbsolutePath + Environment.NewLine +
                    "Firma: manifesto ufficiale verificato"
            }, 0, 1);
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(545, 0),
                Text = "Il download non installa né esegue file. " +
                    "L'installazione richiederà una conferma separata.",
                Margin = new Padding(0, 10, 0, 10)
            }, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var download = new Button
            {
                Name = "productUpdateConfirmDownloadButton",
                AccessibleName = "Conferma download aggiornamento",
                Text = "Scarica",
                AutoSize = true,
                DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Name = "productUpdateCancelDownloadButton",
                AccessibleName = "Annulla download aggiornamento",
                Text = "Annulla",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };
            buttons.Controls.Add(download);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            AcceptButton = download;
            CancelButton = cancel;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }
    }
}
