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

        public FindReplaceForm(RichTextBox editor)
        {
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));

            Text = "Trova";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 174);
            Font = new Font("Segoe UI", 9F);

            var findLabel = new Label { AutoSize = true, Location = new Point(12, 16), Text = "Trova:" };
            findTextBox = new TextBox
            {
                Name = "findTextBox",
                Location = new Point(90, 12),
                Size = new Size(235, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            replaceLabel = new Label { AutoSize = true, Location = new Point(12, 50), Text = "Sostituisci:" };
            replaceTextBox = new TextBox
            {
                Name = "replaceTextBox",
                Location = new Point(90, 46),
                Size = new Size(235, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            matchCaseCheckBox = new CheckBox
            {
                Name = "matchCaseCheckBox",
                AutoSize = true,
                Location = new Point(90, 79),
                Text = "Maiuscole/minuscole"
            };
            var findNextButton = new Button
            {
                Name = "findNextButton",
                Location = new Point(333, 11),
                Size = new Size(85, 27),
                Text = "Trova dopo"
            };
            replaceButton = new Button
            {
                Name = "replaceButton",
                Location = new Point(333, 45),
                Size = new Size(85, 27),
                Text = "Sostituisci"
            };
            replaceAllButton = new Button
            {
                Name = "replaceAllButton",
                Location = new Point(333, 79),
                Size = new Size(85, 27),
                Text = "Tutti"
            };
            resultLabel = new Label
            {
                Name = "resultLabel",
                AutoEllipsis = true,
                ForeColor = Color.DimGray,
                Location = new Point(12, 112),
                Size = new Size(406, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var closeButton = new Button
            {
                Name = "closeButton",
                Location = new Point(333, 137),
                Size = new Size(85, 27),
                Text = "Chiudi"
            };

            findNextButton.Click += (sender, args) => findNext();
            replaceButton.Click += (sender, args) => replaceCurrent();
            replaceAllButton.Click += (sender, args) => replaceAll();
            closeButton.Click += (sender, args) => Close();
            findTextBox.TextChanged += (sender, args) => resultLabel.Text = string.Empty;

            Controls.Add(findLabel);
            Controls.Add(findTextBox);
            Controls.Add(replaceLabel);
            Controls.Add(replaceTextBox);
            Controls.Add(matchCaseCheckBox);
            Controls.Add(findNextButton);
            Controls.Add(replaceButton);
            Controls.Add(replaceAllButton);
            Controls.Add(resultLabel);
            Controls.Add(closeButton);

            AcceptButton = findNextButton;
            CancelButton = closeButton;
        }

        public void SetReplaceMode(bool enabled)
        {
            Text = enabled ? "Trova e sostituisci" : "Trova";
            replaceLabel.Visible = enabled;
            replaceTextBox.Visible = enabled;
            replaceButton.Visible = enabled;
            replaceAllButton.Visible = enabled;
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
