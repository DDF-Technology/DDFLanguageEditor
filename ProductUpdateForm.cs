using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class ProductUpdateForm : Form
    {
        private readonly Button openReleaseButton;
        private readonly Button downloadButton;
        private readonly DdfProductUpdateResult result;
        private readonly Label actionHintLabel;
        private readonly ProgressBar downloadProgressBar;
        private readonly string initialActionHint;
        private bool downloadProgressActive;
        private bool downloadProgressReceiving;

        public ProductUpdateForm(DdfProductUpdateResult result,
            Action<Uri> openRelease,
            Func<DdfProductUpdateResult, Task> download = null)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            Name = "ProductUpdateForm";
            Text = "Aggiornamenti DDFLanguageEditor";
            AccessibleName = "Dettagli aggiornamento DDFLanguageEditor";
            AccessibleDescription =
                "Mostra stato, compatibilità e note della release verificata.";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 430);
            MinimumSize = Size;
            Font = new Font("Segoe UI", 9F);
            Icon = AppIconProvider.LoadIcon();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 2,
                RowCount = 7
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int row = 0; row < 5; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));

            addRow(layout, 0, "Stato", describeState(result));
            addRow(layout, 1, "Versione corrente", result.CurrentVersion);
            addRow(layout, 2, "Versione disponibile",
                result.AvailableVersion.Length == 0 ? "—" : result.AvailableVersion);
            addRow(layout, 3, "Canale",
                result.Channel == DdfProductUpdateChannel.Stable ? "Stable" : "Beta");
            addRow(layout, 4, "Ultimo controllo",
                result.CheckedAtUtc.ToLocalTime().ToString("g"));

            var notes = new TextBox
            {
                Name = "productUpdateDetailsTextBox",
                AccessibleName = "Note e compatibilità dell'aggiornamento",
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = buildDetails(result)
            };
            layout.Controls.Add(notes, 0, 5);
            layout.SetColumnSpan(notes, 2);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            actionHintLabel = new Label
            {
                Name = "productUpdateActionHintLabel",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Text = result.IsActionable
                    ? download == null
                        ? "Risolvi prima l'aggiornamento precedente."
                        : "Release verificata disponibile: apri i dettagli sul sito."
                    : result.State == DdfProductUpdateState.UpToDate
                        ? "Nessuna nuova release da aprire."
                        : "Nessuna pagina di release verificata da aprire."
            };
            initialActionHint = actionHintLabel.Text;
            footer.Controls.Add(actionHintLabel, 0, 0);
            footer.SetColumnSpan(actionHintLabel, 4);
            downloadProgressBar = new ProgressBar
            {
                Name = "productUpdateProgressBar",
                AccessibleName = "Avanzamento download aggiornamento",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Maximum = 1000,
                Visible = false
            };
            footer.Controls.Add(downloadProgressBar, 0, 1);
            footer.SetColumnSpan(downloadProgressBar, 4);
            var close = new Button
            {
                Name = "productUpdateCloseButton",
                AccessibleName = "Chiudi dettagli aggiornamento",
                Text = "Chiudi",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 2, 0, 3)
            };
            close.Click += (sender, args) => Close();
            openReleaseButton = new Button
            {
                Name = "productUpdateOpenReleaseButton",
                AccessibleName = "Apri pagina della release verificata",
                Text = "Apri release",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 2, 0, 3),
                Enabled = result.IsActionable
            };
            openReleaseButton.Click += (sender, args) =>
            {
                if (result.IsActionable) openRelease?.Invoke(result.VerifiedReleaseUri);
            };
            downloadButton = new Button
            {
                Name = "productUpdateDownloadButton",
                AccessibleName = "Scarica aggiornamento verificato",
                Text = "Scarica",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 2, 0, 3),
                Enabled = result.IsActionable && download != null
            };
            downloadButton.Click += async (sender, args) =>
            {
                if (!result.IsActionable || download == null) return;
                downloadButton.Enabled = false;
                try { await download(result); }
                catch (Exception exception)
                {
                    if (!IsDisposed)
                        MessageBox.Show(this, exception.Message,
                            "Download aggiornamento", MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                }
                finally
                {
                    if (!IsDisposed) downloadButton.Enabled = true;
                }
            };
            footer.Controls.Add(downloadButton, 1, 2);
            footer.Controls.Add(openReleaseButton, 2, 2);
            footer.Controls.Add(close, 3, 2);
            layout.Controls.Add(footer, 0, 6);
            layout.SetColumnSpan(footer, 2);
            Controls.Add(layout);
            CancelButton = close;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public DdfProductUpdateResult Result => result;

        public void BeginDownloadProgress()
        {
            if (IsDisposed) return;
            downloadProgressActive = true;
            downloadProgressReceiving = true;
            actionHintLabel.Text = "Preparazione del download verificato...";
            downloadProgressBar.Style = ProgressBarStyle.Marquee;
            downloadProgressBar.Visible = true;
        }

        public void ReportDownloadProgress(DdfProductUpdateStagingProgress progress)
        {
            if (!downloadProgressActive || !downloadProgressReceiving ||
                IsDisposed || progress == null) return;
            downloadProgressBar.Style = ProgressBarStyle.Continuous;
            downloadProgressBar.Value = (int)Math.Min(1000,
                progress.Fraction * 1000);
            actionHintLabel.Text = string.Format(
                "Scaricamento: {0:F1} / {1:F1} MB ({2:F0}%)",
                progress.BytesReceived / 1048576d,
                progress.TotalBytes / 1048576d,
                progress.Fraction * 100);
        }

        public void ShowDownloadVerification()
        {
            if (!downloadProgressActive || IsDisposed) return;
            downloadProgressReceiving = false;
            actionHintLabel.Text = "Verifica dell'archivio scaricato...";
            downloadProgressBar.Style = ProgressBarStyle.Marquee;
        }

        public void EndDownloadProgress()
        {
            if (IsDisposed) return;
            downloadProgressActive = false;
            downloadProgressReceiving = false;
            downloadProgressBar.Visible = false;
            actionHintLabel.Text = initialActionHint;
        }

        private static string describeState(DdfProductUpdateResult value)
        {
            return value.State switch
            {
                DdfProductUpdateState.UpToDate => "Aggiornato",
                DdfProductUpdateState.UpdateAvailable => "Aggiornamento disponibile",
                DdfProductUpdateState.Incompatible => "Release non compatibile",
                _ => "Controllo non disponibile"
            };
        }

        private static string buildDetails(DdfProductUpdateResult value)
        {
            if (value.State == DdfProductUpdateState.Unknown)
                return value.StatusDetail;
            if (value.State == DdfProductUpdateState.UpToDate)
                return "Non risultano release più recenti nel canale selezionato. " +
                    "Il controllo non installa versioni dal codice sorgente: " +
                    "per proporre un aggiornamento serve una release pubblicata " +
                    "con manifesto firmato e compatibile.";
            string details = value.ReleaseNotes;
            if (value.CompatibilityReasons.Count > 0)
                details = string.Join(Environment.NewLine,
                    value.CompatibilityReasons) + Environment.NewLine +
                    Environment.NewLine + details;
            return string.IsNullOrWhiteSpace(details)
                ? "Nessuna nota di rilascio disponibile."
                : details;
        }

        private static void addRow(TableLayoutPanel layout, int row,
            string caption, string value)
        {
            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Text = caption
            }, 0, row);
            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = value ?? string.Empty
            }, 1, row);
        }
    }
}
