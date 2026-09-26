using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Reusable Flowchart surface. It owns presentation and local interaction;
    /// the host supplies analysis, navigation and export destinations.
    /// </summary>
    public sealed class FlowchartView : UserControl
    {
        private readonly FlowchartCanvas canvas;
        private readonly Label statusLabel;
        private readonly ToolStrip toolStrip;
        private readonly ToolStripDropDownButton functionsButton;
        private readonly ToolStripButton pointerButton;
        private readonly ToolStripButton panButton;
        private readonly ToolStripButton groupsButton;
        private readonly ToolStripButton educationalModeButton;
        private readonly Panel educationalPanel;
        private readonly Label educationalTitleLabel;
        private readonly RichTextBox educationalExplanationBox;
        private readonly RowStyle educationalRowStyle;
        private readonly Dictionary<string, string> educationalRuntimeValues =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public FlowchartView(Font menuFont)
        {
            Name = "flowchartView";
            Dock = DockStyle.Fill;
            BackColor = AppTheme.Surface;
            ForeColor = AppTheme.Text;
            Margin = Padding.Empty;
            Padding = Padding.Empty;

            canvas = new FlowchartCanvas
            {
                Name = "flowchartCanvas",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F),
                AccessibleName = "Flowchart della funzione corrente",
                AccessibleDescription = "Usa le frecce per selezionare un nodo e Invio o Spazio per raggiungere il codice."
            };
            statusLabel = new Label
            {
                Name = "flowchartStatusLabel",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 0, 4, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "FLOWCHART — in attesa dell'analisi"
            };
            toolStrip = new ToolStrip
            {
                Name = "flowchartToolStrip",
                GripStyle = ToolStripGripStyle.Hidden,
                Dock = DockStyle.Fill,
                Padding = new Padding(3, 1, 0, 1),
                BackColor = AppTheme.Chrome
            };

            functionsButton = new ToolStripDropDownButton("ƒ")
            {
                Name = "flowchartFunctionsButton",
                AutoSize = false,
                Size = new Size(38, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ToolTipText = "Naviga tra la funzione corrente e le funzioni chiamate",
                AccessibleName = "Navigazione funzioni della flowchart"
            };
            functionsButton.DropDown.Font = menuFont;
            functionsButton.DropDown.BackColor = SystemInformation.HighContrast
                ? SystemColors.Menu : AppTheme.Chrome;
            functionsButton.DropDown.ForeColor = SystemInformation.HighContrast
                ? SystemColors.MenuText : AppTheme.Text;
            functionsButton.DropDown.RenderMode = ToolStripRenderMode.System;
            functionsButton.DropDownOpening += (sender, args) => FunctionsOpening?.Invoke(this, EventArgs.Empty);

            pointerButton = CreateButton("flowchartPointerButton", "\uE7C9",
                "Puntatore: seleziona nel codice il nodo cliccato");
            pointerButton.CheckOnClick = true;
            pointerButton.Checked = true;
            pointerButton.Click += pointerButton_Click;
            panButton = CreateButton("flowchartPanButton", string.Empty,
                "Mano: trascina per spostare il diagramma");
            panButton.CheckOnClick = true;
            panButton.Image = CreatePanIcon();
            panButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            panButton.Click += panButton_Click;
            groupsButton = CreateButton("flowchartGroupsButton", "▤",
                "Comprimi le sequenze lineari di istruzioni");
            groupsButton.CheckOnClick = true;
            groupsButton.Click += groupsButton_Click;
            educationalModeButton = CreateButton("flowchartEducationalModeButton", "D",
                "Attiva la modalità didattica");
            educationalModeButton.CheckOnClick = true;
            educationalModeButton.Click += educationalModeButton_Click;
            ToolStripButton zoomOut = CreateButton("flowchartZoomOutButton", "−", "Riduci zoom");
            zoomOut.Click += (sender, args) => ZoomOut();
            ToolStripButton zoomIn = CreateButton("flowchartZoomInButton", "+", "Aumenta zoom");
            zoomIn.Click += (sender, args) => ZoomIn();
            ToolStripButton save = CreateButton("flowchartSaveButton", string.Empty,
                "Salva l'intero diagramma come SVG o JPEG");
            save.Image = CreateJpegExportIcon();
            save.DisplayStyle = ToolStripItemDisplayStyle.Image;
            save.Click += (sender, args) => ExportRequested?.Invoke(this, EventArgs.Empty);
            toolStrip.Items.AddRange(new ToolStripItem[] { functionsButton,
                new ToolStripSeparator(), pointerButton, panButton, groupsButton, educationalModeButton,
                new ToolStripSeparator(), zoomOut, zoomIn,
                new ToolStripSeparator(), save });

            educationalTitleLabel = new Label
            {
                Name = "flowchartEducationalTitleLabel",
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(8, 3, 4, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 82, 124),
                Text = "GUIDA DIDATTICA"
            };
            educationalExplanationBox = new RichTextBox
            {
                Name = "flowchartEducationalExplanationBox",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Color.FromArgb(239, 247, 255),
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 8.5F),
                AccessibleName = "Spiegazione didattica del nodo Flowchart"
            };
            educationalPanel = new Panel
            {
                Name = "flowchartEducationalPanel",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(239, 247, 255),
                Padding = new Padding(0, 0, 2, 4),
                Visible = false
            };
            educationalPanel.Controls.Add(educationalExplanationBox);
            educationalPanel.Controls.Add(educationalTitleLabel);

            var layout = new TableLayoutPanel
            {
                Name = "flowchartLayoutPanel",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            educationalRowStyle = new RowStyle(SizeType.Absolute, 0F);
            layout.RowStyles.Add(educationalRowStyle);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(toolStrip, 0, 0);
            layout.Controls.Add(statusLabel, 0, 1);
            layout.Controls.Add(educationalPanel, 0, 2);
            layout.Controls.Add(canvas, 0, 3);
            Controls.Add(layout);

            canvas.SelectedNodeChanged += canvas_SelectedNodeChanged;
            canvas.ExecutionNodeChanged += canvas_ExecutionNodeChanged;
        }

        public event EventHandler FunctionsOpening;
        public event EventHandler GroupsChanged;
        public event EventHandler ZoomChanged;
        public event EventHandler ExportRequested;
        public event EventHandler EducationalModeChanged;
        public event EventHandler<DdfFlowchartNode> NodeActivated
        {
            add { canvas.NodeActivated += value; }
            remove { canvas.NodeActivated -= value; }
        }
        public event EventHandler<DdfFlowchartNode> GroupActivated
        {
            add { canvas.GroupActivated += value; }
            remove { canvas.GroupActivated -= value; }
        }
        public event EventHandler<string> FunctionActivated
        {
            add { canvas.FunctionActivated += value; }
            remove { canvas.FunctionActivated -= value; }
        }

        internal FlowchartCanvas Canvas => canvas;
        internal Label StatusLabel => statusLabel;
        internal ToolStrip ToolStrip => toolStrip;
        internal ToolStripDropDownButton FunctionsButton => functionsButton;
        internal ToolStripButton GroupsButton => groupsButton;
        public DdfFlowchartLayout Diagram => canvas.Diagram;
        public DdfFlowchartNode SelectedNode => canvas.SelectedNode;
        public DdfFlowchartNode ExecutionNode => canvas.ExecutionNode;
        public float ZoomFactor => canvas.ZoomFactor;
        public bool GroupsEnabled => groupsButton.Checked;
        public IDdfEditorExperienceState ExperienceState { get; private set; }
        [DefaultValue(false)]
        public bool EducationalModeEnabled
        {
            get => educationalModeButton.Checked;
            set
            {
                educationalModeButton.Checked = value;
                UpdateEducationalModePresentation();
                if (ExperienceState != null && ExperienceState.EducationalModeEnabled != value)
                    ExperienceState.SetEducationalMode(value);
            }
        }

        public void AttachExperienceState(IDdfEditorExperienceState state)
        {
            if (ReferenceEquals(ExperienceState, state)) return;
            if (ExperienceState != null)
                ExperienceState.EducationalModeChanged -= experienceState_EducationalModeChanged;
            ExperienceState = state ?? throw new ArgumentNullException(nameof(state));
            ExperienceState.EducationalModeChanged += experienceState_EducationalModeChanged;
            EducationalModeEnabled = ExperienceState.EducationalModeEnabled;
        }

        public void SetStatus(string text) => statusLabel.Text = text ?? string.Empty;
        public void SetDiagramPreservingViewport(DdfFlowchartLayout diagram, int sourcePosition) =>
            canvas.SetDiagramPreservingViewport(diagram, sourcePosition);
        public void SetSourcePosition(int position) => canvas.SetSourcePosition(position);
        public void SetExecutionSourcePosition(int start, int length) =>
            canvas.SetExecutionSourcePosition(start, length);
        public void ClearExecutionPosition() => canvas.ClearExecutionPosition();
        public void SetEducationalRuntimeValues(IEnumerable<KeyValuePair<string, string>> values)
        {
            educationalRuntimeValues.Clear();
            foreach (KeyValuePair<string, string> value in values ??
                     Enumerable.Empty<KeyValuePair<string, string>>())
                if (!string.IsNullOrWhiteSpace(value.Key))
                    educationalRuntimeValues[value.Key] = value.Value ?? string.Empty;
            UpdateEducationalContent();
        }
        public void SetViewNotice(string text, bool warning) => canvas.SetViewNotice(text, warning);
        public void SaveJpeg(string path) => canvas.SaveJpeg(path);
        public void SaveSvg(string path) => canvas.SaveSvg(path);
        public void ZoomIn()
        {
            canvas.ZoomIn();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
        public void ZoomOut()
        {
            canvas.ZoomOut();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        private void pointerButton_Click(object sender, EventArgs e)
        {
            if (!pointerButton.Checked && !panButton.Checked) pointerButton.Checked = true;
            if (pointerButton.Checked) panButton.Checked = false;
            ApplyToolMode();
        }

        private void panButton_Click(object sender, EventArgs e)
        {
            if (!panButton.Checked && !pointerButton.Checked) panButton.Checked = true;
            if (panButton.Checked) pointerButton.Checked = false;
            ApplyToolMode();
        }

        private void ApplyToolMode()
        {
            canvas.SelectionToolEnabled = pointerButton.Checked;
            canvas.PanToolEnabled = panButton.Checked;
        }

        private void groupsButton_Click(object sender, EventArgs e)
        {
            groupsButton.ToolTipText = groupsButton.Checked
                ? "Espandi tutte le sequenze raggruppate"
                : "Comprimi le sequenze lineari di istruzioni";
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void educationalModeButton_Click(object sender, EventArgs e)
        {
            UpdateEducationalModePresentation();
            if (ExperienceState != null &&
                ExperienceState.EducationalModeEnabled != educationalModeButton.Checked)
                ExperienceState.SetEducationalMode(educationalModeButton.Checked);
            EducationalModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void experienceState_EducationalModeChanged(
            object sender, DdfEducationalModeChangedEventArgs e)
        {
            if (EducationalModeEnabled != e.Enabled) EducationalModeEnabled = e.Enabled;
        }

        private void UpdateEducationalModePresentation()
        {
            if (IsDisposed || Disposing) return;
            educationalModeButton.ToolTipText = educationalModeButton.Checked
                ? "Disattiva la modalità didattica"
                : "Attiva la modalità didattica";
            educationalModeButton.AccessibleName = educationalModeButton.Checked
                ? "Modalità didattica attiva"
                : "Modalità didattica disattiva";
            educationalPanel.Visible = educationalModeButton.Checked;
            educationalRowStyle.Height = educationalModeButton.Checked ? 112F : 0F;
            canvas.EducationalModeEnabled = educationalModeButton.Checked;
            if (educationalModeButton.Checked) UpdateEducationalContent();
            PerformLayout();
        }

        private void UpdateEducationalContent()
        {
            if (IsDisposed || Disposing || !educationalModeButton.Checked) return;
            DdfFlowchartNode node = canvas.SelectedNode ?? canvas.ExecutionNode;
            if (node == null)
            {
                educationalTitleLabel.Text = "GUIDA DIDATTICA";
                educationalExplanationBox.Text =
                    "Posiziona il cursore su un'istruzione oppure seleziona un nodo per leggerne la funzione nel flusso.";
                return;
            }

            bool executing = canvas.ExecutionNode != null && canvas.ExecutionNode.Id == node.Id;
            educationalTitleLabel.Text = executing ? "PASSO CORRENTE DEL DEBUG" : "NODO SELEZIONATO";
            string explanation = ExplainNode(node);
            if (executing)
            {
                List<string> values = educationalRuntimeValues.Where(item =>
                        ContainsIdentifier(node.Label, item.Key))
                    .Take(6).Select(item => item.Key + " = " + item.Value).ToList();
                explanation = "L'esecuzione è sospesa su questo punto. " + explanation;
                if (values.Count > 0)
                    explanation += Environment.NewLine + "Valori coinvolti: " + string.Join("; ", values);
            }
            educationalExplanationBox.Text = explanation;
        }

        private static string ExplainNode(DdfFlowchartNode node)
        {
            string code = string.IsNullOrWhiteSpace(node.Label) ? "questo elemento" : "«" + node.Label + "»";
            switch (node.Kind)
            {
                case DdfFlowchartNodeKind.Entry:
                    return "Ingresso della funzione: da qui comincia il suo flusso di esecuzione.";
                case DdfFlowchartNodeKind.Exit:
                    return "Uscita della funzione: il flusso termina e il controllo torna all'eventuale chiamante.";
                case DdfFlowchartNodeKind.Process:
                    return "Istruzione sequenziale " + code + ": viene eseguita una volta, poi il flusso continua verso il nodo successivo.";
                case DdfFlowchartNodeKind.Call:
                    return "Chiamata " + code + ": il controllo entra nella funzione indicata e, al termine, riprende dal passo seguente.";
                case DdfFlowchartNodeKind.Decision:
                    return "Decisione " + code + ": la condizione viene valutata; il ramo Sì è seguito quando è vera, il ramo No quando è falsa.";
                case DdfFlowchartNodeKind.LoopDecision:
                    return "Controllo di ciclo " + code + ": la condizione decide se eseguire o ripetere il corpo del ciclo oppure proseguire dopo di esso.";
                case DdfFlowchartNodeKind.Return:
                    return "Ritorno " + code + ": termina la funzione e restituisce il valore al chiamante.";
                case DdfFlowchartNodeKind.Break:
                    return "Interruzione del ciclo: il flusso esce immediatamente dal ciclo più vicino.";
                case DdfFlowchartNodeKind.Terminate:
                    return "Terminazione esplicita: l'esecuzione del percorso corrente viene conclusa.";
                case DdfFlowchartNodeKind.Partial:
                    return "Nodo parziale: il codice è ancora incompleto e questa rappresentazione potrebbe cambiare durante la correzione.";
                case DdfFlowchartNodeKind.Group:
                    return "Gruppo compatto: raccoglie più istruzioni sequenziali. Un doppio clic mostra i singoli passaggi.";
                default:
                    return "Elemento del flusso " + code + ".";
            }
        }

        private static bool ContainsIdentifier(string text, string identifier)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(identifier)) return false;
            for (int index = text.IndexOf(identifier, StringComparison.Ordinal); index >= 0;
                 index = text.IndexOf(identifier, index + identifier.Length, StringComparison.Ordinal))
            {
                int before = index - 1;
                int after = index + identifier.Length;
                bool leftBoundary = before < 0 || (!char.IsLetterOrDigit(text[before]) && text[before] != '_');
                bool rightBoundary = after >= text.Length || (!char.IsLetterOrDigit(text[after]) && text[after] != '_');
                if (leftBoundary && rightBoundary) return true;
            }
            return false;
        }

        private void canvas_SelectedNodeChanged(object sender, DdfFlowchartNode node)
        {
            UpdateEducationalContent();
        }

        private void canvas_ExecutionNodeChanged(object sender, DdfFlowchartNode node)
        {
            UpdateEducationalContent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ExperienceState != null)
                    ExperienceState.EducationalModeChanged -= experienceState_EducationalModeChanged;
                ExperienceState = null;
                canvas.SelectedNodeChanged -= canvas_SelectedNodeChanged;
                canvas.ExecutionNodeChanged -= canvas_ExecutionNodeChanged;
            }
            base.Dispose(disposing);
        }

        private static ToolStripButton CreateButton(string name, string text, string toolTip)
        {
            return new ToolStripButton(text)
            {
                Name = name,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(27, 25),
                Font = name == "flowchartPointerButton"
                    ? new Font("Segoe MDL2 Assets", 9F)
                    : new Font("Segoe UI", 10F, FontStyle.Bold),
                ToolTipText = toolTip,
                AccessibleName = toolTip
            };
        }

        private static Image CreateJpegExportIcon()
        {
            var bitmap = new Bitmap(22, 22, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var outline = new Pen(Color.FromArgb(45, 70, 94), 1.5F))
            using (var accent = new Pen(Color.FromArgb(0, 102, 184), 2F))
            using (var landscape = new SolidBrush(Color.FromArgb(91, 153, 96)))
            using (var sun = new SolidBrush(Color.FromArgb(232, 171, 48)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.DrawRectangle(outline, 1.5F, 2.5F, 14F, 12F);
                graphics.FillEllipse(sun, 10F, 5F, 3F, 3F);
                graphics.FillPolygon(landscape, new[] { new PointF(3F, 13F),
                    new PointF(7F, 8F), new PointF(10F, 11F),
                    new PointF(13.5F, 8.5F), new PointF(15F, 13F) });
                graphics.DrawLine(accent, 18F, 7F, 18F, 18F);
                graphics.DrawLine(accent, 14.5F, 14.5F, 18F, 18F);
                graphics.DrawLine(accent, 21F, 14.5F, 18F, 18F);
            }
            return bitmap;
        }

        private static Image CreatePanIcon()
        {
            var bitmap = new Bitmap(22, 22, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var fill = new SolidBrush(Color.FromArgb(230, 238, 247)))
            using (var outline = new Pen(Color.FromArgb(45, 70, 94), 1.35F))
            using (var path = new GraphicsPath())
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                path.AddLines(new[] { new PointF(6.5F, 10.5F), new PointF(6.5F, 5F),
                    new PointF(8.5F, 5F), new PointF(8.5F, 9F), new PointF(8.5F, 3.5F),
                    new PointF(10.5F, 3.5F), new PointF(10.5F, 8.5F), new PointF(10.5F, 3F),
                    new PointF(12.5F, 3F), new PointF(12.5F, 8.5F), new PointF(12.5F, 4.5F),
                    new PointF(14.5F, 4.5F), new PointF(14.5F, 11F), new PointF(16.5F, 9.5F),
                    new PointF(18F, 11F), new PointF(15F, 18F), new PointF(9F, 18F),
                    new PointF(4F, 12.5F), new PointF(5.5F, 10.5F), new PointF(8.5F, 13F) });
                path.CloseFigure();
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }
            return bitmap;
        }
    }
}
