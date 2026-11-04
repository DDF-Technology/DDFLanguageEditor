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
        private readonly RichTextBox typesTextBox;
        private readonly RichTextBox diagnosticsTextBox;
        private readonly RichTextBox errorsTextBox;
        private readonly RichTextBox loweringTextBox;
        private readonly RichTextBox formattedTextBox;
        private readonly Label statusLabel;

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

            var packageLabel = new Label
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Height = 27,
                Name = "languagePlaygroundPackageLabel",
                Padding = new Padding(8, 0, 8, 0),
                Text = "Pacchetto: " + package.Name + " (" + package.Id + ")  •  package " +
                    package.PackageVersion + "  •  linguaggio " + package.LanguageVersion +
                    "  •  " + package.Origin,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 25,
                Name = "languagePlaygroundStatusLabel",
                Padding = new Padding(8, 0, 8, 0),
                Text = "Analisi in corso...",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Name = "languagePlaygroundHeader"
            };
            header.Controls.Add(statusLabel);
            header.Controls.Add(packageLabel);

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
                Text = "Prova il pacchetto attivo: analisi isolata, senza modificare documenti o pacchetti"
            };
            var sourcePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            sourcePanel.Controls.Add(sourceTextBox);
            sourcePanel.Controls.Add(sourceLabel);
            split.Controls.Add(sourcePanel, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill, Name = "languagePlaygroundTabs" };
            tokensTextBox = AddResultTab(tabs, "Token", "languagePlaygroundTokensTextBox");
            treeTextBox = AddResultTab(tabs, "Albero", "languagePlaygroundTreeTextBox");
            typesTextBox = AddResultTab(tabs, "Tipi", "languagePlaygroundTypesTextBox");
            diagnosticsTextBox = AddResultTab(tabs, "Diagnostiche", "languagePlaygroundDiagnosticsTextBox");
            errorsTextBox = AddResultTab(tabs, "Errori dichiarati", "languagePlaygroundErrorsTextBox");
            loweringTextBox = AddResultTab(tabs, "Lowering IR", "languagePlaygroundLoweringTextBox");
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
            Controls.Add(header);
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
            try
            {
                DdfLexResult lex = DdfLexer.Lex(source, package.Language);
                DdfParseResult parse = DdfParser.Parse(source, package.Language);
                DdfTypeCheckResult types = DdfTypeChecker.Check(source, parse.Root, null,
                    package.Language.Types, package.Language.StandardLibrary);
                DdfIrBuildResult ir = DdfIrLowerer.Lower(source, parse, package.Language);

                tokensTextBox.Text = string.Join(Environment.NewLine, lex.Tokens.Select(token =>
                    token.Kind + "  " + token.Start + ":" + token.Length + "  " +
                    Escape(Slice(source, token.Start, token.Length))));
                var tree = new StringBuilder();
                AppendNode(tree, parse.GenericRoot, 0);
                treeTextBox.Text = tree.Length == 0 ? "Albero non disponibile." : tree.ToString().TrimEnd();
                typesTextBox.Text = FormatTypes(source, types.TypedSpans);

                IReadOnlyList<DdfDiagnostic> diagnostics = lex.Diagnostics.Concat(parse.Diagnostics)
                    .Concat(types.Diagnostics).Concat(ir.Diagnostics)
                    .GroupBy(item => item.Code + ":" + item.Start + ":" + item.Length + ":" + item.Message,
                        StringComparer.Ordinal)
                    .Select(group => group.First()).ToList().AsReadOnly();
                diagnosticsTextBox.Text = diagnostics.Count == 0
                    ? "Nessuna diagnostica."
                    : string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString()));
                errorsTextBox.Text = FormatDeclaredErrors(package.Language.StandardLibrary);
                loweringTextBox.Text = FormatLowering(ir);
                formattedTextBox.Text = DdfFormatter.Format(source, package.Language).Text;
                int treeNodes = CountNodes(parse.GenericRoot);
                int errorCount = diagnostics.Count(item =>
                    item.Severity == DdfDiagnosticSeverity.Error);
                statusLabel.Text = "Aggiornamento in tempo reale  •  " + lex.Tokens.Count +
                    " token  •  " + treeNodes + " nodi  •  " + types.TypedSpans.Count +
                    " intervalli tipizzati  •  " + errorCount +
                    (errorCount == 1 ? " errore" : " errori") +
                    "  •  IR " + (ir.Success ? "disponibile" : "parziale");
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is InvalidOperationException ||
                                               exception is NotSupportedException)
            {
                statusLabel.Text = "Analisi non disponibile: " + exception.Message;
                diagnosticsTextBox.Text = "[Funzionalità motore] " + exception.Message;
                typesTextBox.Text = "Tipi non disponibili.";
                loweringTextBox.Text = "Lowering non disponibile.";
            }
        }

        private static string FormatTypes(string source,
            IReadOnlyList<DdfTypedSpan> spans)
        {
            string[] rows = (spans ?? Array.Empty<DdfTypedSpan>())
                .Where(span => span != null && span.Length > 0)
                .GroupBy(span => span.Start + ":" + span.Length + ":" +
                    span.Type.DisplayName, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(span => span.Start).ThenBy(span => span.Length)
                .Select(span => span.Type.DisplayName.PadRight(20) + "  " +
                    span.Start + ":" + span.Length + "  " +
                    Escape(Slice(source, span.Start, span.Length)))
                .ToArray();
            return rows.Length == 0 ? "Nessun intervallo tipizzato disponibile." :
                string.Join(Environment.NewLine, rows);
        }

        private static string FormatDeclaredErrors(
            DdfStandardLibraryDefinition standardLibrary)
        {
            var text = new StringBuilder();
            text.Append("Libreria standard: ").Append(standardLibrary.Id)
                .Append(' ').AppendLine(standardLibrary.Version).AppendLine();
            DdfStandardFunction[] functions = standardLibrary.Functions
                .Where(function => function.ErrorDefinitions.Count > 0)
                .OrderBy(function => function.Name, StringComparer.Ordinal)
                .ToArray();
            if (functions.Length == 0)
            {
                text.Append("Nessuna funzione standard dichiara errori recuperabili.");
                return text.ToString();
            }
            foreach (DdfStandardFunction function in functions)
            {
                text.AppendLine(function.Signature);
                foreach (DdfErrorDefinition error in function.ErrorDefinitions)
                    text.Append("  ").Append(error.Code).Append("  ")
                        .Append(error.TypeId).Append(" — ")
                        .AppendLine(error.Documentation);
            }
            text.AppendLine().Append(
                "Sono mostrati soltanto gli errori recuperabili dichiarati dal contratto; i fault del motore non sono catturabili dal codice DDF.");
            return text.ToString();
        }

        private static string FormatLowering(DdfIrBuildResult result)
        {
            if (result?.Program == null) return "IR non disponibile.";
            var text = new StringBuilder();
            text.Append("DDF IR ").AppendLine(DdfIrProgram.FormatVersion);
            AppendIrNode(text, result.Program, 0);
            if (result.Program.SemanticOperations.Count > 0)
            {
                text.AppendLine().AppendLine("Operazioni semantiche dichiarative:");
                foreach (DdfPlannedSemanticOperation operation in
                         result.Program.SemanticOperations)
                    text.Append("  ").Append(operation.Id).Append("  ")
                        .Append(operation.Rule).Append("  [")
                        .Append(operation.Start).Append(':')
                        .Append(operation.Length).AppendLine("]");
            }
            return text.ToString().TrimEnd();
        }

        private static void AppendIrNode(StringBuilder builder, DdfIrNode node,
            int depth)
        {
            if (node == null) return;
            builder.Append(' ', depth * 2).Append(IrLabel(node)).Append(" [")
                .Append(node.Start).Append(':').Append(node.Length)
                .AppendLine("]");
            foreach (DdfIrNode child in IrChildren(node))
                AppendIrNode(builder, child, depth + 1);
        }

        private static string IrLabel(DdfIrNode node)
        {
            return node switch
            {
                DdfIrProgram => "Program",
                DdfIrLibrary library => "Library " + library.Text,
                DdfIrStructure structure => "Structure " + structure.Name,
                DdfIrEnum enumeration => "Enum " + enumeration.Name,
                DdfIrEnumMember member => "EnumMember " + member.Name,
                DdfIrFunction function => "Function " + function.Name,
                DdfIrGlobal => "Global",
                DdfIrParameter parameter => "Parameter " + parameter.Name,
                DdfIrType type => "Type " + type.Name,
                DdfIrBlock => "Block",
                DdfIrVariableDeclaration variable =>
                    (variable.IsFixed ? "Fixed " : variable.IsState ? "State " : "Variable ") + variable.Name,
                DdfIrExpressionStatement => "ExpressionStatement",
                DdfIrIf => "If",
                DdfIrSelect => "Select",
                DdfIrSelectClause clause => clause.IsOtherwise ? "Otherwise" : "When",
                DdfIrWhile => "While",
                DdfIrDoWhile => "DoWhile",
                DdfIrFor => "For",
                DdfIrReturn => "Return",
                DdfIrBreak => "Break",
                DdfIrContinue => "Continue",
                DdfIrEnd => "End",
                DdfIrMissingExpression => "MissingExpression",
                DdfIrLiteral literal => "Literal " + literal.Kind + " " + literal.Text,
                DdfIrName name => "Name " + name.Name,
                DdfIrGroup => "Group",
                DdfIrUnary unary => "Unary " + unary.OperatorText + " → " + unary.OperationId,
                DdfIrBinary binary => "Binary " + binary.OperatorText + " → " + binary.OperationId,
                DdfIrPostfix postfix => "Postfix " + postfix.OperatorText + " → " + postfix.OperationId,
                DdfIrCall => "Call",
                DdfIrIndex => "Index",
                DdfIrMemberAccess member => "Member " + member.Name,
                _ => node.GetType().Name
            };
        }

        private static IEnumerable<DdfIrNode> IrChildren(DdfIrNode node)
        {
            switch (node)
            {
                case DdfIrProgram program: return program.Members;
                case DdfIrStructure structure: return structure.Fields;
                case DdfIrEnum enumeration: return enumeration.Members;
                case DdfIrFunction function: return Nodes(function.Parameters
                    .Cast<DdfIrNode>().Concat(Nodes(function.ReturnType,
                        function.Body)));
                case DdfIrGlobal global: return Nodes(global.Statement);
                case DdfIrParameter parameter: return Nodes(parameter.Type);
                case DdfIrType type: return type.ArrayLengths;
                case DdfIrBlock block: return block.Statements;
                case DdfIrVariableDeclaration variable: return Nodes(
                    variable.Type, variable.Initializer);
                case DdfIrExpressionStatement statement: return Nodes(statement.Expression);
                case DdfIrIf conditional: return Nodes(conditional.Condition,
                    conditional.Body, conditional.ElseBody);
                case DdfIrSelect selection: return Nodes(selection.Clauses
                    .Cast<DdfIrNode>().Prepend(selection.Expression));
                case DdfIrSelectClause clause: return Nodes(clause.Value,
                    clause.Body);
                case DdfIrWhile loop: return Nodes(loop.Condition, loop.Body);
                case DdfIrDoWhile loop: return Nodes(loop.Body, loop.Condition);
                case DdfIrFor loop: return Nodes(loop.Initializer, loop.Condition,
                    loop.Increment, loop.Body);
                case DdfIrReturn returned: return Nodes(returned.Expression);
                case DdfIrGroup group: return Nodes(group.Expression);
                case DdfIrUnary unary: return Nodes(unary.Operand);
                case DdfIrBinary binary: return Nodes(binary.Left, binary.Right);
                case DdfIrPostfix postfix: return Nodes(postfix.Operand);
                case DdfIrCall call: return Nodes(call.Arguments
                    .Cast<DdfIrNode>().Prepend(call.Target));
                case DdfIrIndex index: return Nodes(index.Target, index.Index);
                case DdfIrMemberAccess member: return Nodes(member.Target);
                default: return Array.Empty<DdfIrNode>();
            }
        }

        private static IEnumerable<DdfIrNode> Nodes(params DdfIrNode[] nodes) =>
            nodes.Where(node => node != null);

        private static IEnumerable<DdfIrNode> Nodes(
            IEnumerable<DdfIrNode> nodes) =>
            (nodes ?? Array.Empty<DdfIrNode>()).Where(node => node != null);

        private static int CountNodes(DdfGenericSyntaxNode node) => node == null
            ? 0 : 1 + node.Children.Sum(CountNodes);

        private static string Slice(string source, int start, int length)
        {
            if (string.IsNullOrEmpty(source) || start < 0 || length <= 0 ||
                start >= source.Length) return string.Empty;
            return source.Substring(start, Math.Min(length, source.Length - start));
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
