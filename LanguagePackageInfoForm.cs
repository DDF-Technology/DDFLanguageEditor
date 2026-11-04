using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class LanguagePackageInfoForm : Form
    {
        private readonly DdfLanguagePackage package;
        private readonly IReadOnlyList<DdfLanguagePackageDiagnostic> diagnostics;

        public LanguagePackageInfoForm(DdfLanguagePackage activePackage = null,
            IReadOnlyList<DdfLanguagePackageDiagnostic> loadDiagnostics = null)
        {
            package = activePackage ?? DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            diagnostics = loadDiagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            BackColor = AppTheme.Window;
            ClientSize = new Size(720, 520);
            Font = new Font("Segoe UI", 9F);
            ForeColor = AppTheme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "LanguagePackageInfoForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Pacchetto linguaggio attivo";

            Icon = AppIconProvider.LoadIcon();

            var titleLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                Location = new Point(24, 20),
                Name = "languagePackageTitleLabel",
                Text = package.Name
            };
            var statusLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = package.Origin == DdfLanguagePackageOrigin.Embedded ? AppTheme.Error : AppTheme.Run,
                Location = new Point(27, 57),
                Name = "languagePackageStatusLabel",
                Text = package.Origin == DdfLanguagePackageOrigin.Embedded
                    ? "Fallback incorporato attivo"
                    : "Pacchetto esterno caricato correttamente"
            };

            var details = new TableLayoutPanel
            {
                ColumnCount = 2,
                Location = new Point(24, 91),
                Name = "languagePackageDetailsTable",
                RowCount = 6,
                Size = new Size(672, 169)
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AddDetail(details, 0, "Linguaggio", package.Name + " " + package.LanguageVersion, "languagePackageLanguageValueLabel");
            AddDetail(details, 1, "Pacchetto", package.Id + " " + package.PackageVersion, "languagePackageVersionValueLabel");
            AddDetail(details, 2, "Origine", OriginText(package.Origin), "languagePackageOriginValueLabel");
            AddDetail(details, 3, "Estensioni file", string.Join(", ", package.FileExtensions), "languagePackageExtensionsValueLabel");
            AddDetail(details, 4, "Plug-in nativi", NativeExtensionSummary(), "languagePackageNativeExtensionsValueLabel");

            var sourceCaption = CreateCaption("Sorgente");
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            details.Controls.Add(sourceCaption, 0, 5);
            var sourceTextBox = new TextBox
            {
                BackColor = AppTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                Name = "languagePackageSourceTextBox",
                ReadOnly = true,
                Text = package.Source
            };
            details.Controls.Add(sourceTextBox, 1, 5);

            var diagnosticsLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(24, 276),
                Name = "languagePackageDiagnosticsLabel",
                Text = "Diagnostiche di caricamento"
            };
            var diagnosticsList = new ListBox
            {
                BackColor = AppTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = AppTheme.Text,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                Location = new Point(27, 301),
                Name = "languagePackageDiagnosticsListBox",
                Size = new Size(669, 108)
            };
            if (diagnostics.Count == 0)
                diagnosticsList.Items.Add("Nessuna diagnostica: caricamento completato correttamente.");
            else
                diagnosticsList.Items.AddRange(diagnostics.Select(item => item.ToString()).Cast<object>().ToArray());

            var noteLabel = new Label
            {
                AutoSize = false,
                ForeColor = AppTheme.MutedText,
                Location = new Point(27, 419),
                Name = "languagePackageNoteLabel",
                Size = new Size(669, 37),
                Text = "Usa Linguaggio > Gestisci pacchetti o il pulsante Linguaggio nella barra strumenti. 'Ripristina DDF incorporato' è un'azione di fallback, non un'opzione da deselezionare."
            };
            var copyButton = new Button
            {
                Location = new Point(462, 470),
                Name = "languagePackageCopyButton",
                Size = new Size(112, 30),
                Text = "Copia dettagli",
                UseVisualStyleBackColor = false
            };
            copyButton.Click += (sender, args) => Clipboard.SetText(BuildDetailsText());
            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(584, 470),
                Name = "languagePackageCloseButton",
                Size = new Size(112, 30),
                Text = "Chiudi",
                UseVisualStyleBackColor = false
            };
            closeButton.Click += (sender, args) => Close();

            AcceptButton = closeButton;
            CancelButton = closeButton;
            Controls.Add(titleLabel);
            Controls.Add(statusLabel);
            Controls.Add(details);
            Controls.Add(diagnosticsLabel);
            Controls.Add(diagnosticsList);
            Controls.Add(noteLabel);
            Controls.Add(copyButton);
            Controls.Add(closeButton);
            AppTheme.ApplyLight(this);
            statusLabel.ForeColor = package.Origin == DdfLanguagePackageOrigin.Embedded ? AppTheme.Error : AppTheme.Run;
            TextBoxEditingSupport.Apply(this);
        }

        private static void AddDetail(TableLayoutPanel table, int row, string caption, string value, string name)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            table.Controls.Add(CreateCaption(caption), 0, row);
            table.Controls.Add(new Label { AutoSize = true, Margin = new Padding(3, 5, 3, 3), Name = name, Text = value }, 1, row);
        }

        private static Label CreateCaption(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 5, 3, 3),
                Text = text
            };
        }

        private static string OriginText(DdfLanguagePackageOrigin origin)
        {
            return origin == DdfLanguagePackageOrigin.Directory
                ? "Cartella esterna"
                : origin == DdfLanguagePackageOrigin.Archive ? "Archivio .ddflang" : "Fallback incorporato";
        }

        private string NativeExtensionSummary()
        {
            if (package.NativeExtensions.Count == 0) return "Nessuno";
            return string.Join(", ", package.NativeExtensions.Select(item =>
                item.Id + " " + item.Version + " (autorizzazione richiesta)"));
        }

        private string BuildDetailsText()
        {
            var result = new StringBuilder();
            result.AppendLine("Linguaggio: " + package.Name + " " + package.LanguageVersion);
            result.AppendLine("Pacchetto: " + package.Id + " " + package.PackageVersion);
            result.AppendLine("Origine: " + OriginText(package.Origin));
            result.AppendLine("Sorgente: " + package.Source);
            result.AppendLine("Estensioni: " + string.Join(", ", package.FileExtensions));
            result.AppendLine("Runtime: " + package.Language.Runtime.InstructionSet +
                "; entry point=" + package.Language.Runtime.EntryPoint +
                "; limite=" + package.Language.Runtime.DefaultInstructionLimit);
            result.AppendLine("Libreria standard: " + package.Language.StandardLibrary.Functions.Count +
                " funzioni (" + string.Join(", ", package.Language.StandardLibrary.Functions.Select(item => item.Name)) + ")");
            result.AppendLine("Plug-in nativi:");
            if (package.NativeExtensions.Count == 0) result.AppendLine("Nessuno.");
            else
            {
                foreach (DdfNativeExtensionRequirement extension in package.NativeExtensions)
                {
                    result.AppendLine("- " + extension.Id + " " + extension.Version +
                        "; autore=" + extension.Publisher + "; sha256=" + extension.Sha256);
                    result.AppendLine("  capacità richieste: " + string.Join(", ", extension.Capabilities));
                    result.AppendLine("  stato: non caricato; autorizzazione separata richiesta");
                }
            }
            result.AppendLine("Diagnostiche:");
            if (diagnostics.Count == 0) result.AppendLine("Nessuna.");
            else foreach (DdfLanguagePackageDiagnostic diagnostic in diagnostics) result.AppendLine(diagnostic.ToString());
            return result.ToString();
        }
    }
}
