using System;
using System.Drawing;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private Panel signatureHelpPanel;
        private Label signatureHelpLabel;
        private Label signatureParameterLabel;
        private Timer signatureHelpTimer;
        private DdfSignatureHelpResult activeSignatureHelpResult;

        private void initializeSignatureHelp()
        {
            signatureHelpPanel = new Panel
            {
                Name = "signatureHelpPanel",
                Visible = false,
                BackColor = AppTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(560, 62),
                TabStop = false
            };
            signatureHelpLabel = new Label
            {
                Name = "signatureHelpLabel",
                AutoEllipsis = true,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Consolas", 10F),
                Location = new Point(8, 5),
                Size = new Size(542, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            signatureParameterLabel = new Label
            {
                Name = "signatureParameterLabel",
                AutoEllipsis = true,
                BackColor = AppTheme.Surface,
                ForeColor = Color.FromArgb(35, 100, 170),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(8, 31),
                Size = new Size(542, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            signatureHelpPanel.Controls.Add(signatureHelpLabel);
            signatureHelpPanel.Controls.Add(signatureParameterLabel);
            panelEditor.Controls.Add(signatureHelpPanel);
            signatureHelpPanel.BringToFront();

            signatureHelpTimer = new Timer { Interval = 65 };
            signatureHelpTimer.Tick += (sender, args) =>
            {
                signatureHelpTimer.Stop();
                showSignatureHelp();
            };
        }

        private void scheduleSignatureHelp()
        {
            if (signatureHelpTimer == null || isReplacingDocument || richTextBoxFoldedView.Visible ||
                richTextBoxMainEditor.SelectionLength > 0 || !richTextBoxMainEditor.Focused)
            {
                hideSignatureHelp();
                return;
            }

            signatureHelpTimer.Stop();
            signatureHelpTimer.Start();
        }

        private void showSignatureHelp()
        {
            if (IsDisposed || Disposing || richTextBoxMainEditor.IsDisposed ||
                richTextBoxFoldedView.Visible || richTextBoxMainEditor.SelectionLength > 0)
            {
                hideSignatureHelp();
                return;
            }

            DdfSignatureHelpResult result = DdfSignatureHelpService.GetSignatureHelp(
                richTextBoxMainEditor.Text,
                richTextBoxMainEditor.SelectionStart,
                getWorkspaceTypeRoots());
            if (result == null)
            {
                hideSignatureHelp();
                return;
            }

            activeSignatureHelpResult = result;
            signatureHelpLabel.Text = result.Signature.Signature +
                                      (string.IsNullOrEmpty(result.Signature.Origin)
                                          ? string.Empty
                                          : "  —  " + result.Signature.Origin);
            DdfSignatureParameter parameter = result.ActiveParameterInformation;
            if (result.Signature.Parameters.Count == 0)
            {
                signatureParameterLabel.Text = "Nessun parametro";
            }
            else if (parameter != null)
            {
                signatureParameterLabel.Text = "Parametro " + (result.ActiveParameter + 1) + "/" +
                                                   result.Signature.Parameters.Count + ":  " + parameter.DisplayText;
            }
            else
            {
                signatureParameterLabel.Text = "Argomento " + (result.ActiveParameter + 1) +
                                                   ": nessun parametro previsto";
            }

            signatureHelpPanel.Visible = true;
            positionSignatureHelp();
            signatureHelpPanel.BringToFront();
        }

        private void positionSignatureHelp()
        {
            if (signatureHelpPanel == null || !signatureHelpPanel.Visible) return;
            Point caret = richTextBoxMainEditor.GetPositionFromCharIndex(richTextBoxMainEditor.SelectionStart);
            Point editorOrigin = panelEditor.PointToClient(richTextBoxMainEditor.PointToScreen(Point.Empty));
            int x = editorOrigin.X + caret.X;
            int y = editorOrigin.Y + caret.Y - signatureHelpPanel.Height - 3;
            if (y < 3)
            {
                y = completionListBox != null && completionListBox.Visible
                    ? completionListBox.Bottom + 3
                    : editorOrigin.Y + caret.Y + richTextBoxMainEditor.Font.Height + 4;
            }

            x = Math.Max(3, Math.Min(x, panelEditor.ClientSize.Width - signatureHelpPanel.Width - 3));
            y = Math.Max(3, Math.Min(y, panelEditor.ClientSize.Height - signatureHelpPanel.Height - 3));
            signatureHelpPanel.Location = new Point(x, y);
        }

        private void hideSignatureHelp()
        {
            if (signatureHelpTimer != null) signatureHelpTimer.Stop();
            if (signatureHelpPanel != null) signatureHelpPanel.Visible = false;
            activeSignatureHelpResult = null;
        }

        private void disposeSignatureHelp()
        {
            if (signatureHelpTimer == null) return;
            signatureHelpTimer.Stop();
            signatureHelpTimer.Dispose();
        }
    }
}
