using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class FindReplaceForm : Form
    {
        private readonly RichTextBox editor;
        private readonly Label replaceLabel;
        private readonly TextBox findTextBox;
        private readonly TextBox replaceTextBox;
        private readonly CheckBox matchCaseCheckBox;
        private readonly Button replaceButton;
        private readonly Button replaceAllButton;
        private readonly Label resultLabel;
        private readonly RowStyle replaceRowStyle;

        public FindReplaceForm(RichTextBox editor)
        {
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));

            Text = "Trova";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 212);
            Font = new Font("Segoe UI", 9F);

            var layout = new TableLayoutPanel
            {
                Name = "findReplaceLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(14, 12, 14, 10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            replaceRowStyle = new RowStyle(SizeType.Absolute, 34F);
            layout.RowStyles.Add(replaceRowStyle);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var findLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Name = "findLabel",
                Text = "Trova:",
                TextAlign = ContentAlignment.MiddleLeft
            };
            findTextBox = new TextBox
            {
                Name = "findTextBox",
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 5, 0, 5)
            };
            replaceLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Name = "replaceLabel",
                Text = "Sostituisci con:",
                TextAlign = ContentAlignment.MiddleLeft
            };
            replaceTextBox = new TextBox
            {
                Name = "replaceTextBox",
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 5, 0, 5)
            };
            matchCaseCheckBox = new CheckBox
            {
                Name = "matchCaseCheckBox",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 4, 0, 4),
                Text = "Maiuscole/minuscole"
            };
            var findNextButton = new Button
            {
                Name = "findNextButton",
                Size = new Size(108, 30),
                Text = "Trova successivo"
            };
            replaceButton = new Button
            {
                Name = "replaceButton",
                Size = new Size(96, 30),
                Text = "Sostituisci"
            };
            replaceAllButton = new Button
            {
                Name = "replaceAllButton",
                Size = new Size(116, 30),
                Text = "Sostituisci tutto"
            };
            resultLabel = new Label
            {
                Name = "resultLabel",
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Margin = new Padding(0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var closeButton = new Button
            {
                Name = "closeButton",
                Size = new Size(84, 30),
                Text = "Chiudi"
            };
            var actionPanel = new FlowLayoutPanel
            {
                Name = "findReplaceActionPanel",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                Padding = new Padding(0, 4, 0, 0),
                WrapContents = false
            };
            closeButton.Margin = new Padding(6, 0, 0, 0);
            replaceAllButton.Margin = new Padding(6, 0, 0, 0);
            replaceButton.Margin = new Padding(6, 0, 0, 0);
            findNextButton.Margin = new Padding(6, 0, 0, 0);
            actionPanel.Controls.Add(closeButton);
            actionPanel.Controls.Add(replaceButton);
            actionPanel.Controls.Add(replaceAllButton);
            actionPanel.Controls.Add(findNextButton);

            findNextButton.Click += (sender, args) => findNext();
            replaceButton.Click += (sender, args) => replaceCurrent();
            replaceAllButton.Click += (sender, args) => replaceAll();
            closeButton.Click += (sender, args) => Close();
            findTextBox.TextChanged += (sender, args) => resultLabel.Text = string.Empty;

            layout.Controls.Add(findLabel, 0, 0);
            layout.Controls.Add(findTextBox, 1, 0);
            layout.Controls.Add(replaceLabel, 0, 1);
            layout.Controls.Add(replaceTextBox, 1, 1);
            layout.Controls.Add(matchCaseCheckBox, 1, 2);
            layout.Controls.Add(resultLabel, 0, 3);
            layout.SetColumnSpan(resultLabel, 2);
            layout.Controls.Add(actionPanel, 0, 4);
            layout.SetColumnSpan(actionPanel, 2);
            Controls.Add(layout);

            AcceptButton = findNextButton;
            CancelButton = closeButton;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
        }

        public void SetReplaceMode(bool enabled)
        {
            Text = enabled ? "Trova e sostituisci" : "Trova";
            replaceLabel.Visible = enabled;
            replaceTextBox.Visible = enabled;
            replaceAllButton.Visible = enabled;
            replaceButton.Visible = enabled;
            replaceRowStyle.Height = enabled ? 34F : 0F;
            ClientSize = new Size(560, enabled ? 212 : 178);
            if (Visible)
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            }
            findTextBox.Focus();
            findTextBox.SelectAll();
        }

        public void PrefillFind(string value)
        {
            if (!string.IsNullOrEmpty(value) && value.IndexOfAny(new[] { '\r', '\n' }) < 0)
            {
                findTextBox.Text = value;
            }
        }

        private bool findNext()
        {
            string value = findTextBox.Text;
            int start = Math.Min(editor.SelectionStart + editor.SelectionLength, editor.TextLength);
            int match = TextSearch.FindNext(editor.Text, value, start, matchCaseCheckBox.Checked);
            if (match < 0)
            {
                resultLabel.Text = string.IsNullOrEmpty(value)
                    ? "Inserire il testo da cercare."
                    : "Nessuna corrispondenza.";
                return false;
            }

            editor.Select(match, value.Length);
            editor.ScrollToCaret();
            editor.Focus();
            resultLabel.Text = match < start ? "Ricerca ripresa dall'inizio." : string.Empty;
            return true;
        }

        private void replaceCurrent()
        {
            if (!selectionMatchesFind())
            {
                findNext();
                return;
            }

            editor.SelectedText = replaceTextBox.Text;
            findNext();
        }

        private void replaceAll()
        {
            ReplaceResult result = TextSearch.ReplaceAll(
                editor.Text,
                findTextBox.Text,
                replaceTextBox.Text,
                matchCaseCheckBox.Checked);
            if (result.ReplacementCount == 0)
            {
                resultLabel.Text = string.IsNullOrEmpty(findTextBox.Text)
                    ? "Inserire il testo da cercare."
                    : "Nessuna corrispondenza.";
                return;
            }

            int caret = Math.Min(editor.SelectionStart, result.Text.Length);
            editor.SelectAll();
            editor.SelectedText = result.Text;
            editor.Select(caret, 0);
            editor.ScrollToCaret();
            editor.Focus();
            resultLabel.Text = result.ReplacementCount.ToString(CultureInfo.CurrentCulture) +
                               (result.ReplacementCount == 1 ? " sostituzione." : " sostituzioni.");
        }

        private bool selectionMatchesFind()
        {
            string value = findTextBox.Text;
            if (string.IsNullOrEmpty(value) || editor.SelectionLength != value.Length)
            {
                return false;
            }

            StringComparison comparison = matchCaseCheckBox.Checked
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            return string.Equals(editor.SelectedText, value, comparison);
        }
    }
}
