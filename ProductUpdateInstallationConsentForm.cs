using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal enum ProductUpdateConsentStage
    {
        Download,
        Install
    }

    internal sealed class ProductUpdateInstallationConsentForm : Form
    {
        private readonly CheckBox restartCheckBox;

        public ProductUpdateInstallationConsentForm(
            DdfProductUpdateInstallationPlan plan,
            ProductUpdateConsentStage stage)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            bool installation = stage == ProductUpdateConsentStage.Install;
            Text = installation
                ? "Conferma installazione aggiornamento"
                : "Conferma download aggiornamento";
            AccessibleName = Text;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(590, installation ? 365 : 330);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var titleLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
                Text = installation
                    ? "Installare la release verificata?"
                    : "Scaricare la release verificata?"
            };
            var detailBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                TabStop = true,
                ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Dettagli aggiornamento",
                Text = BuildDetails(plan, installation)
            };
            restartCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = plan.RestartRequested,
                Visible = installation,
                Text = "Riavvia DDF Language Editor al termine",
                AccessibleName = "Riavvia l'editor dopo l'installazione",
                Margin = new Padding(0, 12, 0, 4)
            };
            var warningLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(545, 0),
                Margin = new Padding(0, 8, 0, 12),
                Text = installation
                    ? "L'editor verrà chiuso soltanto dopo questa conferma. " +
                      "Il worker conserverà il backup fino al controllo di integrità."
                    : "Il download non installa né esegue il pacchetto. " +
                      "Firma, hash e inventario saranno verificati prima dell'installazione."
            };

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var confirmButton = new Button
            {
                AutoSize = true,
                DialogResult = DialogResult.OK,
                Text = installation ? "Installa" : "Scarica",
                AccessibleName = installation
                    ? "Conferma installazione"
                    : "Conferma download"
            };
            var cancelButton = new Button
            {
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                Text = "Annulla"
            };
            buttons.Controls.Add(confirmButton);
            buttons.Controls.Add(cancelButton);

            layout.Controls.Add(titleLabel, 0, 0);
            layout.Controls.Add(detailBox, 0, 1);
            layout.Controls.Add(restartCheckBox, 0, 2);
            layout.Controls.Add(warningLabel, 0, 3);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);
            AcceptButton = confirmButton;
            CancelButton = cancelButton;
            AppTheme.ApplyLight(this);
        }

        public bool RestartRequested => restartCheckBox.Visible &&
            restartCheckBox.Checked;

        private static string BuildDetails(DdfProductUpdateInstallationPlan plan,
            bool installation)
        {
            string action = installation ? "Installazione" : "Download";
            return action + " aggiornamento DDF Language Editor" +
                Environment.NewLine + Environment.NewLine +
                "Versione attuale: " + plan.CurrentVersion + Environment.NewLine +
                "Nuova versione: " + plan.TargetVersion + Environment.NewLine +
                "Canale/asset: " + plan.AssetUri.Host + plan.AssetUri.AbsolutePath +
                Environment.NewLine +
                "Dimensione: " + FormatSize(plan.AssetSize) + Environment.NewLine +
                "Architettura: " + plan.Architecture + " (" +
                plan.RuntimeIdentifier + ")" + Environment.NewLine +
                "Destinazione: " + plan.TargetRoot + Environment.NewLine +
                "Firma: " + plan.SigningKeyId;
        }

        private static string FormatSize(long bytes)
        {
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.##",
                CultureInfo.CurrentCulture) + " " + units[unit];
        }
    }
}
