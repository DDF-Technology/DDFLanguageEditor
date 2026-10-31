using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace DDFLanguageEditor.PackageStudio;

internal sealed class PackageStudioHomeForm : Form
{
    public PackageStudioHomeForm()
    {
        Name = "packageStudioHomeForm";
        Text = "DDF Language Package Studio";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 300);
        MinimumSize = new Size(500, 300);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(32, 39, 48);
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20, 16, 20, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        for (int index = 0; index < 3; index++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Crea un profilo dal DDF incorporato oppure apri un progetto di " +
                "authoring o un archivio esistente.",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(ActionButton("packageStudioEmbeddedButton",
            "Nuovo dal DDF incorporato", () => Open(null)), 0, 1);
        layout.Controls.Add(ActionButton("packageStudioDirectoryButton",
            "Apri cartella di authoring...", ChooseDirectory), 0, 2);
        layout.Controls.Add(ActionButton("packageStudioArchiveButton",
            "Apri archivio .ddflang...", ChooseArchive), 0, 3);
        Controls.Add(layout);
    }

    private static Button ActionButton(string name, string text, Action action)
    {
        var button = new Button
        {
            Name = name,
            AccessibleName = text,
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 0, 5),
            UseVisualStyleBackColor = true
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Seleziona una cartella di authoring DDF",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Open(dialog.SelectedPath);
    }

    private void ChooseArchive()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Apri pacchetto linguaggio DDF",
            Filter = "Pacchetti DDF (*.ddflang)|*.ddflang|Tutti i file (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Open(dialog.FileName);
    }

    private void Open(string path)
    {
        try
        {
            Form studio = Program.OpenStudio(path);
            studio.FormClosed += (_, _) =>
            {
                studio.Dispose();
                if (!IsDisposed)
                {
                    Show();
                    Activate();
                }
            };
            studio.Show();
            Hide();
        }
        catch (Exception exception) when (exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is JsonException)
        {
            MessageBox.Show(this, exception.Message,
                "DDF Language Package Studio", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
