using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class ProductUpdateForm : Form
    {
        private readonly Button openReleaseButton;
        private readonly DdfProductUpdateResult result;

        public ProductUpdateForm(DdfProductUpdateResult result,
            Action<Uri> openRelease)
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

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

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 7, 0, 0)
            };
            var close = new Button
            {
                Name = "productUpdateCloseButton",
                AccessibleName = "Chiudi dettagli aggiornamento",
                Text = "Chiudi",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 30)
            };
            openReleaseButton = new Button
            {
                Name = "productUpdateOpenReleaseButton",
                AccessibleName = "Apri pagina della release verificata",
                Text = "Apri release",
                Size = new Size(110, 30),
                Enabled = result.IsActionable
            };
            openReleaseButton.Click += (sender, args) =>
            {
                if (result.IsActionable) openRelease?.Invoke(result.VerifiedReleaseUri);
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(openReleaseButton);
            layout.Controls.Add(buttons, 0, 6);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            CancelButton = close;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public DdfProductUpdateResult Result => result;

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
