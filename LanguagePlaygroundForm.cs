using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class LanguagePlaygroundForm : Form
    {
        private readonly DdfLanguagePackage package;
        private readonly RichTextBox sourceTextBox;
        private readonly RichTextBox tokensTextBox;
        private readonly RichTextBox treeTextBox;
        private readonly RichTextBox diagnosticsTextBox;
        private readonly RichTextBox formattedTextBox;

        public LanguagePlaygroundForm(DdfLanguagePackage package)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            BackColor = AppTheme.Window;
            ClientSize = new Size(1000, 650);
            Font = new Font("Segoe UI", 9F);
            ForeColor = AppTheme.Text;
            MinimumSize = new Size(760, 480);
            Name = "LanguagePlaygroundForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Playground linguaggio — " + package.Name + " " + package.PackageVersion;
            Icon = AppIconProvider.LoadIcon();

            var split = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Name = "languagePlaygroundSplit" };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sourceTextBox = CreateTextBox("languagePlaygroundSourceTextBox", false);
            sourceTextBox.Text = CreateInitialSource(package);
            sourceTextBox.TextChanged += (sender, args) => RefreshPreview();
            var sourceLabel = new Label
            {
                Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Text = "Sorgente isolato (non modifica i documenti aperti)"
            };
            var sourcePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            sourcePanel.Controls.Add(sourceTextBox);
            sourcePanel.Controls.Add(sourceLabel);
            split.Controls.Add(sourcePanel, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill, Name = "languagePlaygroundTabs" };
            tokensTextBox = AddResultTab(tabs, "Token", "languagePlaygroundTokensTextBox");
            treeTextBox = AddResultTab(tabs, "Albero", "languagePlaygroundTreeTextBox");
            diagnosticsTextBox = AddResultTab(tabs, "Diagnostiche", "languagePlaygroundDiagnosticsTextBox");
            formattedTextBox = AddResultTab(tabs, "Formattazione", "languagePlaygroundFormattedTextBox");
            split.Controls.Add(tabs, 1, 0);

            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Name = "languagePlaygroundCloseButton",
                Size = new Size(100, 30), Text = "Chiudi", UseVisualStyleBackColor = false
            };
            var refreshButton = new Button
            {
                Dock = DockStyle.Right, Name = "languagePlaygroundRefreshButton", Size = new Size(110, 30),
                Text = "Aggiorna", UseVisualStyleBackColor = false
            };
            closeButton.Click += (sender, args) => Close();
            refreshButton.Click += (sender, args) => RefreshPreview();
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
            footer.Controls.Add(closeButton);
            footer.Controls.Add(refreshButton);
            Controls.Add(split);
            Controls.Add(footer);
            CancelButton = closeButton;
            AppTheme.ApplyLight(this);
            TextBoxEditingSupport.Apply(this);
            RefreshPreview();
        }

        private static string CreateInitialSource(DdfLanguagePackage target)
        {
            const string canonical = "main() out int\n{\n    ret 0;\n}";
            DdfLanguagePackageLoadResult embedded = DdfLanguagePackageLoader.LoadEmbeddedDefault();
            if (!embedded.Success || string.Equals(target.Id, embedded.Package.Id, StringComparison.Ordinal))
                return canonical;
            return DdfSyntaxProfileConverter.Convert(
                canonical, embedded.Package.Language, target.Language).Text;
        }

        private static RichTextBox AddResultTab(TabControl tabs, string title, string name)
        {
            var page = new TabPage(title) { BackColor = AppTheme.Surface, Padding = new Padding(4) };
            RichTextBox textBox = CreateTextBox(name, true);
            page.Controls.Add(textBox);
            tabs.TabPages.Add(page);
            return textBox;
        }

        private static RichTextBox CreateTextBox(string name, bool readOnly) => new RichTextBox
        {
            AcceptsTab = true,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Mono", 10F),
            ForeColor = AppTheme.Text,
            Name = name,
            ReadOnly = readOnly,
            WordWrap = false
        };

        private void RefreshPreview()
        {
            string source = sourceTextBox.Text;
            DdfLexResult lex = DdfLexer.Lex(source, package.Language);
            DdfParseResult parse = DdfParser.Parse(source, package.Language);
            DdfTypeCheckResult types = DdfTypeChecker.Check(source, parse.Root, null,
                package.Language.Types, package.Language.StandardLibrary);
            DdfIrBuildResult ir = DdfIrLowerer.Lower(source, parse, package.Language);

            tokensTextBox.Text = string.Join(Environment.NewLine, lex.Tokens.Select(token =>
                token.Kind + "  " + token.Start + ":" + token.Length + "  " +
                Escape(source.Substring(token.Start, token.Length))));
            var tree = new StringBuilder();
            AppendNode(tree, parse.GenericRoot, 0);
            treeTextBox.Text = tree.Length == 0 ? "Albero non disponibile." : tree.ToString().TrimEnd();
            IReadOnlyList<DdfDiagnostic> diagnostics = lex.Diagnostics.Concat(parse.Diagnostics)
                .Concat(types.Diagnostics).Concat(ir.Diagnostics).ToList().AsReadOnly();
            diagnosticsTextBox.Text = diagnostics.Count == 0
                ? "Nessuna diagnostica."
                : string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString()));
            formattedTextBox.Text = DdfFormatter.Format(source, package.Language).Text;
        }

        private static void AppendNode(StringBuilder builder, DdfGenericSyntaxNode node, int depth)
        {
            if (node == null) return;
            builder.Append(' ', depth * 2).Append(node.Kind).Append(" [")
                .Append(node.Start).Append(':').Append(node.Length).Append(']');
            if (node.IsMissing) builder.Append(" (mancante)");
            builder.AppendLine();
            foreach (DdfGenericSyntaxNode child in node.Children) AppendNode(builder, child, depth + 1);
        }

        private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
