using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private FlowLayoutPanel breadcrumbPanel;
        private IReadOnlyList<DdfBreadcrumbItem> activeBreadcrumbPath = Array.Empty<DdfBreadcrumbItem>();
        private string breadcrumbSignature = string.Empty;
        private bool isNavigatingBreadcrumb;

        private void initializeBreadcrumb()
        {
            breadcrumbPanel = new FlowLayoutPanel
            {
                Name = "breadcrumbPanel",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Margin = new Padding(0),
                Padding = new Padding(6, 2, 6, 1)
            };
            documentLayout.SuspendLayout();
            try
            {
                documentLayout.RowCount = 3;
                documentLayout.RowStyles.Clear();
                documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
                documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
                documentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                documentLayout.Controls.Remove(editorHost);
                documentLayout.Controls.Add(breadcrumbPanel, 0, 1);
                documentLayout.Controls.Add(editorHost, 0, 2);
            }
            finally
            {
                documentLayout.ResumeLayout(true);
            }
            updateBreadcrumb();
        }

        private void updateBreadcrumb()
        {
            if (isNavigatingBreadcrumb || breadcrumbPanel == null || breadcrumbPanel.IsDisposed || richTextBoxMainEditor == null) return;
            activeBreadcrumbPath = lastParseResult != null &&
                                   string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)
                ? DdfBreadcrumbService.GetPath(lastParseResult.Root, richTextBoxMainEditor.SelectionStart)
                : Array.Empty<DdfBreadcrumbItem>();
            string fileName = getBreadcrumbFileName();
            string signature = fileName + "|" + string.Join("|", activeBreadcrumbPath.Select(item =>
                item.Kind + ":" + item.SelectionStart + ":" + item.SelectionLength + ":" + item.Label));
            if (string.Equals(breadcrumbSignature, signature, StringComparison.Ordinal)) return;
            breadcrumbSignature = signature;

            breadcrumbPanel.SuspendLayout();
            try
            {
                Control[] obsoleteControls = breadcrumbPanel.Controls.Cast<Control>().ToArray();
                breadcrumbPanel.Controls.Clear();
                foreach (Control control in obsoleteControls) control.Dispose();
                breadcrumbPanel.Controls.Add(createBreadcrumbButton(
                    "breadcrumbFileButton",
                    fileName,
                    null));
                for (int index = 0; index < activeBreadcrumbPath.Count; index++)
                {
                    breadcrumbPanel.Controls.Add(createBreadcrumbSeparator(index));
                    DdfBreadcrumbItem item = activeBreadcrumbPath[index];
                    breadcrumbPanel.Controls.Add(createBreadcrumbButton(
                        "breadcrumbItemButton" + index,
                        item.Label,
                        item));
                }
            }
            finally
            {
                breadcrumbPanel.ResumeLayout(true);
            }
        }

        private Button createBreadcrumbButton(string name, string text, DdfBreadcrumbItem item)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                Tag = item,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Surface,
                ForeColor = item == null ? AppTheme.Text : AppTheme.Accent,
                Font = new Font("Segoe UI", 8.5F, item == null ? FontStyle.Regular : FontStyle.Bold),
                Margin = new Padding(0),
                Padding = new Padding(4, 0, 4, 0),
                Height = 22,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += breadcrumbButton_Click;
            return button;
        }

        private static Label createBreadcrumbSeparator(int index)
        {
            return new Label
            {
                Name = "breadcrumbSeparator" + index,
                Text = "›",
                AutoSize = true,
                ForeColor = AppTheme.MutedText,
                Margin = new Padding(1, 3, 1, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private string getBreadcrumbFileName()
        {
            if (documentSession == null) return "Senza titolo";
            if (documentSession.HasPath) return Path.GetFileName(documentSession.CurrentPath);
            return documentSession.DisplayName;
        }

        private void breadcrumbButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as DdfBreadcrumbItem;
            int selectionStart = item?.SelectionStart ?? 0;
            int selectionLength = item?.SelectionLength ?? 0;
            isNavigatingBreadcrumb = true;
            try
            {
                navigateWithHistory(() =>
                {
                    leaveFoldedView();
                    int safeStart = Math.Min(selectionStart, richTextBoxMainEditor.TextLength);
                    int safeLength = Math.Min(selectionLength, richTextBoxMainEditor.TextLength - safeStart);
                    richTextBoxMainEditor.Select(safeStart, safeLength);
                    richTextBoxMainEditor.ScrollToCaret();
                    richTextBoxMainEditor.Focus();
                    return true;
                });
            }
            finally
            {
                isNavigatingBreadcrumb = false;
                updateBreadcrumb();
            }
        }
    }
}
