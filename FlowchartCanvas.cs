using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class FlowchartCanvas : ScrollableControl
    {
        private DdfFlowchartLayout diagram;
        private string selectedNodeId;
        private string executionNodeId;
        private float zoomFactor = 1F;
        private bool selectionToolEnabled = true;
        private bool panToolEnabled;
        private bool educationalModeEnabled;
        private bool isPanning;
        private string viewNotice = string.Empty;
        private bool viewNoticeIsWarning;
        private Point panStartMouse;
        private Point panStartScroll;
        private string trueLabel = "Sì";
        private string falseLabel = "No";

        public FlowchartCanvas()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = AppTheme.Surface;
            ForeColor = AppTheme.Text;
            TabStop = true;
            Cursor = Cursors.Default;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public event EventHandler<DdfFlowchartNode> NodeActivated;
        public event EventHandler<DdfFlowchartNode> GroupActivated;
        public event EventHandler<string> FunctionActivated;
        public event EventHandler<DdfFlowchartNode> SelectedNodeChanged;
        public event EventHandler<DdfFlowchartNode> ExecutionNodeChanged;
        public DdfFlowchartLayout Diagram => diagram;
        public DdfFlowchartNode SelectedNode => FindSelectedNode(diagram)?.Node;
        public DdfFlowchartNode ExecutionNode => diagram?.Flowchart.FindNode(executionNodeId);
        public float ZoomFactor => zoomFactor;
        public string ViewNotice => viewNotice;
        public string TrueLabel => trueLabel;
        public string FalseLabel => falseLabel;
        public void SetDecisionLabels(string whenTrue, string whenFalse)
        {
            trueLabel = string.IsNullOrWhiteSpace(whenTrue) ? "Sì" : whenTrue;
            falseLabel = string.IsNullOrWhiteSpace(whenFalse) ? "No" : whenFalse;
            Invalidate();
        }
        [DefaultValue(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SelectionToolEnabled
        {
            get => selectionToolEnabled;
            set { selectionToolEnabled = value; UpdateToolCursor(); }
        }
        [DefaultValue(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool PanToolEnabled
        {
            get => panToolEnabled;
            set
            {
                panToolEnabled = value;
                if (!value) EndPan();
                UpdateToolCursor();
            }
        }
        [DefaultValue(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EducationalModeEnabled
        {
            get => educationalModeEnabled;
            set
            {
                if (educationalModeEnabled == value) return;
                educationalModeEnabled = value;
                Invalidate();
            }
        }

        public void SetDiagram(DdfFlowchartLayout value)
        {
            diagram = value;
            selectedNodeId = null;
            UpdateScrollSize();
            Invalidate();
            SelectedNodeChanged?.Invoke(this, null);
            ExecutionNodeChanged?.Invoke(this, ExecutionNode);
        }

        public void SetViewNotice(string text, bool warning)
        {
            string next = text ?? string.Empty;
            if (string.Equals(viewNotice, next, StringComparison.Ordinal) &&
                viewNoticeIsWarning == warning) return;
            viewNotice = next;
            viewNoticeIsWarning = warning;
            Invalidate();
        }

        public void SetDiagramPreservingViewport(DdfFlowchartLayout value, int sourcePosition)
        {
            DdfFlowchartLayout previous = diagram;
            DdfFlowchartLayoutNode previousAnchor = FindSelectedNode(previous) ??
                FindNodeAtSource(previous, sourcePosition);
            Point previousScroll = AutoScrollPosition;
            Size previousExtent = AutoScrollMinSize;
            bool sameFunction = previous != null && value != null && string.Equals(
                previous.Flowchart.FunctionName, value.Flowchart.FunctionName, StringComparison.Ordinal) &&
                string.Equals(previous.Flowchart.DocumentPath, value.Flowchart.DocumentPath,
                    StringComparison.OrdinalIgnoreCase);
            PointF anchorInClient = previousAnchor == null ? PointF.Empty : new PointF(
                (float)((previousAnchor.X + previousAnchor.Width / 2) * zoomFactor) + previousScroll.X,
                (float)((previousAnchor.Y + previousAnchor.Height / 2) * zoomFactor) + previousScroll.Y);
            PointF relativeCenter = RelativeViewportCenter(previousExtent, previousScroll);

            diagram = value;
            DdfFlowchartLayoutNode nextAnchor = FindNodeAtSource(value, sourcePosition);
            if (nextAnchor == null && sameFunction && previousAnchor != null)
                nextAnchor = FindEquivalentNode(value, previousAnchor);
            selectedNodeId = nextAnchor?.Node.Id;
            UpdateScrollSize();

            if (nextAnchor != null)
            {
                PointF desiredClient = sameFunction && previousAnchor != null
                    ? anchorInClient
                    : new PointF(ClientSize.Width / 2F, ClientSize.Height / 2F);
                RestoreScrollOffset(
                    (float)((nextAnchor.X + nextAnchor.Width / 2) * zoomFactor) - desiredClient.X,
                    (float)((nextAnchor.Y + nextAnchor.Height / 2) * zoomFactor) - desiredClient.Y);
            }
            else if (sameFunction && previous != null)
            {
                RestoreScrollOffset(relativeCenter.X * AutoScrollMinSize.Width - ClientSize.Width / 2F,
                    relativeCenter.Y * AutoScrollMinSize.Height - ClientSize.Height / 2F);
            }
            else RestoreScrollOffset(0, 0);
            Invalidate();
            SelectedNodeChanged?.Invoke(this, SelectedNode);
            ExecutionNodeChanged?.Invoke(this, ExecutionNode);
        }

        public void ZoomIn() => SetZoom(zoomFactor + 0.15F);
        public void ZoomOut() => SetZoom(zoomFactor - 0.15F);

        public void SaveJpeg(string path)
        {
            if (diagram == null) throw new InvalidOperationException("Nessun diagramma da esportare.");
            int width = Math.Max(1, (int)Math.Ceiling(RenderWidth()));
            int height = Math.Max(1, (int)Math.Ceiling(diagram.Height));
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                DrawDiagram(graphics, Point.Empty, 1F);
                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        public void SaveSvg(string path)
        {
            if (diagram == null) throw new InvalidOperationException("Nessun diagramma da esportare.");
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                NewLineChars = "\n"
            };
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
                writer.WriteAttributeString("width", SvgNumber(diagram.Width));
                writer.WriteAttributeString("height", SvgNumber(diagram.Height));
                writer.WriteAttributeString("viewBox", "0 0 " + SvgNumber(diagram.Width) + " " +
                    SvgNumber(diagram.Height));
                writer.WriteAttributeString("role", "img");
                writer.WriteAttributeString("aria-label", "Flowchart della funzione " +
                    diagram.Flowchart.FunctionName);
                writer.WriteElementString("title", "Flowchart " + diagram.Flowchart.FunctionName + "()");
                writer.WriteStartElement("metadata");
                writer.WriteString("DDFLanguageEditor flowchart; educationalMode=" +
                    educationalModeEnabled.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                WriteSvgDefinitions(writer);
                writer.WriteStartElement("rect");
                WriteSvgRectangle(writer, 0, 0, diagram.Width, diagram.Height);
                writer.WriteAttributeString("fill", "#ffffff");
                writer.WriteEndElement();

                var byId = diagram.Nodes.ToDictionary(item => item.Node.Id, StringComparer.Ordinal);
                IReadOnlyList<RoutedEdge> routes = BuildRoutes(byId);
                List<LineSegment> solidSegments = routes.Where(item => !item.Edge.IsBackEdge)
                    .SelectMany(item => Segments(item.Points)).ToList();
                foreach (RoutedEdge route in routes.Where(item => !item.Edge.IsBackEdge))
                    WriteSvgForwardRoute(writer, route);
                foreach (RoutedEdge route in routes.Where(item => item.Edge.IsBackEdge))
                    WriteSvgBackRoute(writer, route, solidSegments);
                foreach (MergeArrow arrow in BuildMergeArrows(routes)) WriteSvgMergeArrow(writer, arrow);
                foreach (DdfFlowchartLayoutNode node in diagram.Nodes) WriteSvgNode(writer, node);
                foreach (RoutedEdge route in routes)
                {
                    if (route.Edge.Kind == DdfFlowchartEdgeKind.True)
                        WriteSvgEdgeLabel(writer, trueLabel, route.LabelPoint, true);
                    else if (route.Edge.Kind == DdfFlowchartEdgeKind.False)
                        WriteSvgEdgeLabel(writer, falseLabel, route.LabelPoint, false);
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteSvgDefinitions(XmlWriter writer)
        {
            writer.WriteStartElement("defs");
            WriteSvgMarker(writer, "arrow-gray", "#697078");
            WriteSvgMarker(writer, "arrow-blue", "#5678a6");
            WriteSvgMarker(writer, "arrow-green", "#168242");
            WriteSvgMarker(writer, "arrow-red", "#be3737");
            writer.WriteEndElement();
        }

        private static void WriteSvgMarker(XmlWriter writer, string id, string color)
        {
            writer.WriteStartElement("marker");
            writer.WriteAttributeString("id", id);
            writer.WriteAttributeString("viewBox", "0 0 10 10");
            writer.WriteAttributeString("refX", "9");
            writer.WriteAttributeString("refY", "5");
            writer.WriteAttributeString("markerWidth", "6");
            writer.WriteAttributeString("markerHeight", "6");
            writer.WriteAttributeString("orient", "auto-start-reverse");
            writer.WriteStartElement("path");
            writer.WriteAttributeString("d", "M 0 0 L 10 5 L 0 10 z");
            writer.WriteAttributeString("fill", color);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private void WriteSvgForwardRoute(XmlWriter writer, RoutedEdge route)
        {
            bool teaching = educationalModeEnabled &&
                string.Equals(route.Edge.From, selectedNodeId, StringComparison.Ordinal) &&
                (route.Edge.Kind == DdfFlowchartEdgeKind.True ||
                 route.Edge.Kind == DdfFlowchartEdgeKind.False);
            string color = !teaching ? "#697078" :
                route.Edge.Kind == DdfFlowchartEdgeKind.True ? "#168242" : "#be3737";
            string marker = !teaching ? "arrow-gray" :
                route.Edge.Kind == DdfFlowchartEdgeKind.True ? "arrow-green" : "arrow-red";
            WriteSvgPolyline(writer, route.Points, color, teaching ? 3F : 1.5F, null, marker);
        }

        private static void WriteSvgBackRoute(XmlWriter writer, RoutedEdge route,
            IReadOnlyList<LineSegment> solidSegments)
        {
            List<LineSegment> dashed = Segments(route.Points).ToList();
            for (int index = 0; index < dashed.Count; index++)
            {
                List<LineSegment> visible = SubtractOverlaps(dashed[index], solidSegments);
                for (int part = 0; part < visible.Count; part++)
                {
                    bool final = index == dashed.Count - 1 && part == visible.Count - 1 &&
                        SamePoint(visible[part].End, dashed[index].End);
                    WriteSvgLine(writer, visible[part], "#5678a6", 1.5F, "5 4",
                        final ? "arrow-blue" : null);
                }
            }
        }

        private static void WriteSvgPolyline(XmlWriter writer, IReadOnlyList<PointF> points,
            string color, float width, string dash, string marker)
        {
            writer.WriteStartElement("polyline");
            writer.WriteAttributeString("points", string.Join(" ", points.Select(point =>
                SvgNumber(point.X) + "," + SvgNumber(point.Y))));
            WriteSvgStroke(writer, color, width, dash, marker);
            writer.WriteEndElement();
        }

        private static void WriteSvgLine(XmlWriter writer, LineSegment line, string color,
            float width, string dash, string marker)
        {
            writer.WriteStartElement("line");
            writer.WriteAttributeString("x1", SvgNumber(line.Start.X));
            writer.WriteAttributeString("y1", SvgNumber(line.Start.Y));
            writer.WriteAttributeString("x2", SvgNumber(line.End.X));
            writer.WriteAttributeString("y2", SvgNumber(line.End.Y));
            WriteSvgStroke(writer, color, width, dash, marker);
            writer.WriteEndElement();
        }

        private static void WriteSvgStroke(XmlWriter writer, string color, float width,
            string dash, string marker)
        {
            writer.WriteAttributeString("fill", "none");
            writer.WriteAttributeString("stroke", color);
            writer.WriteAttributeString("stroke-width", SvgNumber(width));
            writer.WriteAttributeString("stroke-linejoin", "round");
            if (!string.IsNullOrEmpty(dash)) writer.WriteAttributeString("stroke-dasharray", dash);
            if (!string.IsNullOrEmpty(marker)) writer.WriteAttributeString("marker-end", "url(#" + marker + ")");
        }

        private static void WriteSvgMergeArrow(XmlWriter writer, MergeArrow arrow)
        {
            LineSegment arrival = arrow.Arrival;
            float dx = arrival.End.X - arrival.Start.X;
            float dy = arrival.End.Y - arrival.Start.Y;
            float length = Math.Max(1F, (float)Math.Sqrt(dx * dx + dy * dy));
            float ux = dx / length;
            float uy = dy / length;
            float distance = Math.Min(6F, length * 0.25F);
            float arrowLength = Math.Min(7F, Math.Max(4F, length - distance));
            PointF tip = new PointF(arrival.End.X - ux * distance, arrival.End.Y - uy * distance);
            PointF center = new PointF(tip.X - ux * arrowLength, tip.Y - uy * arrowLength);
            PointF perpendicular = new PointF(-uy * 3.5F, ux * 3.5F);
            writer.WriteStartElement("polygon");
            writer.WriteAttributeString("points", SvgPoints(new[] { tip,
                new PointF(center.X + perpendicular.X, center.Y + perpendicular.Y),
                new PointF(center.X - perpendicular.X, center.Y - perpendicular.Y) }));
            writer.WriteAttributeString("fill", arrow.BackEdge ? "#43699b" : "#525962");
            writer.WriteEndElement();
        }

        private void WriteSvgNode(XmlWriter writer, DdfFlowchartLayoutNode item)
        {
            bool selected = string.Equals(item.Node.Id, selectedNodeId, StringComparison.Ordinal);
            bool executing = string.Equals(item.Node.Id, executionNodeId, StringComparison.Ordinal);
            Color fill = executing ? Color.FromArgb(255, 238, 153) :
                selected ? Color.FromArgb(221, 235, 252) : NodeColor(item.Node.Kind);
            string border = executing ? "#c47c00" : selected ? "#2369b2" : "#5c6876";
            float borderWidth = executing ? 3F : selected ? 2.5F : 1.3F;
            if (item.Node.Kind == DdfFlowchartNodeKind.Entry || item.Node.Kind == DdfFlowchartNodeKind.Exit)
            {
                writer.WriteStartElement("ellipse");
                writer.WriteAttributeString("cx", SvgNumber(item.X + item.Width / 2));
                writer.WriteAttributeString("cy", SvgNumber(item.Y + item.Height / 2));
                writer.WriteAttributeString("rx", SvgNumber(item.Width / 2));
                writer.WriteAttributeString("ry", SvgNumber(item.Height / 2));
            }
            else if (item.Node.Kind == DdfFlowchartNodeKind.Decision ||
                     item.Node.Kind == DdfFlowchartNodeKind.LoopDecision)
            {
                writer.WriteStartElement("polygon");
                writer.WriteAttributeString("points", SvgPoints(new[] {
                    new PointF((float)(item.X + item.Width / 2), (float)item.Y),
                    new PointF((float)(item.X + item.Width), (float)(item.Y + item.Height / 2)),
                    new PointF((float)(item.X + item.Width / 2), (float)(item.Y + item.Height)),
                    new PointF((float)item.X, (float)(item.Y + item.Height / 2)) }));
            }
            else
            {
                writer.WriteStartElement("rect");
                WriteSvgRectangle(writer, item.X, item.Y, item.Width, item.Height);
                writer.WriteAttributeString("rx", "9");
                writer.WriteAttributeString("ry", "9");
            }
            writer.WriteAttributeString("fill", SvgColor(fill));
            writer.WriteAttributeString("stroke", border);
            writer.WriteAttributeString("stroke-width", SvgNumber(borderWidth));
            writer.WriteEndElement();
            WriteSvgNodeText(writer, item);
            if (item.Node.IsCollapsedGroup) WriteSvgGroupBadge(writer, item);
            if (executing) WriteSvgExecutionBadge(writer, item);
        }

        private static void WriteSvgNodeText(XmlWriter writer, DdfFlowchartLayoutNode item)
        {
            IReadOnlyList<string> lines = SplitSvgLines(item.Node.Label, 32, 3);
            double lineHeight = 13;
            double startY = item.Y + item.Height / 2 - (lines.Count - 1) * lineHeight / 2;
            writer.WriteStartElement("text");
            writer.WriteAttributeString("x", SvgNumber(item.X + item.Width / 2));
            writer.WriteAttributeString("y", SvgNumber(startY));
            writer.WriteAttributeString("text-anchor", "middle");
            writer.WriteAttributeString("dominant-baseline", "middle");
            writer.WriteAttributeString("font-family", "Segoe UI, sans-serif");
            writer.WriteAttributeString("font-size", "11");
            writer.WriteAttributeString("fill", "#202020");
            for (int index = 0; index < lines.Count; index++)
            {
                writer.WriteStartElement("tspan");
                writer.WriteAttributeString("x", SvgNumber(item.X + item.Width / 2));
                if (index > 0) writer.WriteAttributeString("dy", SvgNumber(lineHeight));
                writer.WriteString(lines[index]);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static IReadOnlyList<string> SplitSvgLines(string text, int maximum, int maximumLines)
        {
            string source = (text ?? string.Empty).Trim();
            if (source.Length == 0) return new[] { string.Empty };
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (string word in source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > maximum)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    if (lines.Count == maximumLines - 1) break;
                }
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
            if (current.Length > 0) lines.Add(current.ToString());
            if (lines.Count == maximumLines && string.Join(" ", lines).Length < source.Length)
                lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd('.') + "…";
            return lines.AsReadOnly();
        }

        private static void WriteSvgGroupBadge(XmlWriter writer, DdfFlowchartLayoutNode item)
        {
            double x = item.X + item.Width - 18;
            double y = item.Y + 5;
            writer.WriteStartElement("rect");
            WriteSvgRectangle(writer, x, y, 12, 12);
            writer.WriteAttributeString("fill", "#ffffff");
            writer.WriteAttributeString("stroke", "#3f6376");
            writer.WriteEndElement();
            WriteSvgLine(writer, new LineSegment(new PointF((float)x + 3, (float)y + 6),
                new PointF((float)x + 9, (float)y + 6)), "#3f6376", 1, null, null);
            WriteSvgLine(writer, new LineSegment(new PointF((float)x + 6, (float)y + 3),
                new PointF((float)x + 6, (float)y + 9)), "#3f6376", 1, null, null);
        }

        private static void WriteSvgExecutionBadge(XmlWriter writer, DdfFlowchartLayoutNode item)
        {
            double cx = item.X + 13;
            double cy = item.Y + 13;
            writer.WriteStartElement("circle");
            writer.WriteAttributeString("cx", SvgNumber(cx));
            writer.WriteAttributeString("cy", SvgNumber(cy));
            writer.WriteAttributeString("r", "8");
            writer.WriteAttributeString("fill", "#1f7a47");
            writer.WriteEndElement();
            writer.WriteStartElement("polygon");
            writer.WriteAttributeString("points", SvgPoints(new[] {
                new PointF((float)cx - 2, (float)cy - 4), new PointF((float)cx + 5, (float)cy),
                new PointF((float)cx - 2, (float)cy + 4) }));
            writer.WriteAttributeString("fill", "#ffffff");
            writer.WriteEndElement();
        }

        private static void WriteSvgEdgeLabel(XmlWriter writer, string text, PointF center, bool positive)
        {
            double width = Math.Max(24, 12 + (text ?? string.Empty).Length * 7);
            const double height = 18;
            writer.WriteStartElement("rect");
            WriteSvgRectangle(writer, center.X - width / 2, center.Y - height / 2, width, height);
            writer.WriteAttributeString("fill", positive ? "#eef9f1" : "#fdf0f0");
            writer.WriteAttributeString("stroke", positive ? "#166837" : "#9b2e2e");
            writer.WriteEndElement();
            writer.WriteStartElement("text");
            writer.WriteAttributeString("x", SvgNumber(center.X));
            writer.WriteAttributeString("y", SvgNumber(center.Y));
            writer.WriteAttributeString("text-anchor", "middle");
            writer.WriteAttributeString("dominant-baseline", "middle");
            writer.WriteAttributeString("font-family", "Segoe UI, sans-serif");
            writer.WriteAttributeString("font-size", "11");
            writer.WriteAttributeString("fill", positive ? "#166837" : "#9b2e2e");
            writer.WriteString(text);
            writer.WriteEndElement();
        }

        private static void WriteSvgRectangle(XmlWriter writer, double x, double y,
            double width, double height)
        {
            writer.WriteAttributeString("x", SvgNumber(x));
            writer.WriteAttributeString("y", SvgNumber(y));
            writer.WriteAttributeString("width", SvgNumber(width));
            writer.WriteAttributeString("height", SvgNumber(height));
        }

        private static string SvgPoints(IEnumerable<PointF> points) => string.Join(" ",
            points.Select(point => SvgNumber(point.X) + "," + SvgNumber(point.Y)));
        private static string SvgColor(Color color) => "#" + color.R.ToString("x2") +
            color.G.ToString("x2") + color.B.ToString("x2");
        private static string SvgNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private void SetZoom(float value)
        {
            float next = Math.Max(0.4F, Math.Min(2.5F, value));
            if (Math.Abs(next - zoomFactor) < 0.001F) return;
            zoomFactor = next;
            UpdateScrollSize();
            Invalidate();
        }

        private void UpdateScrollSize()
        {
            AutoScrollMinSize = diagram == null ? Size.Empty : new Size(
                (int)Math.Ceiling(RenderWidth() * zoomFactor),
                (int)Math.Ceiling(diagram.Height * zoomFactor));
        }

        private double RenderWidth()
        {
            if (diagram == null) return 0;
            return diagram.Width;
        }

        public void SetSourcePosition(int position)
        {
            DdfFlowchartLayoutNode node = FindNodeAtSource(diagram, position);
            string next = node?.Node.Id;
            if (string.Equals(next, selectedNodeId, StringComparison.Ordinal)) return;
            selectedNodeId = next;
            EnsureNodeVisible(node);
            Invalidate();
            SelectedNodeChanged?.Invoke(this, SelectedNode);
        }

        public void SetExecutionSourcePosition(int start, int length)
        {
            DdfFlowchartLayoutNode node = FindNodeAtSourceRange(diagram, start, length);
            string next = node?.Node.Id;
            if (string.Equals(next, executionNodeId, StringComparison.Ordinal))
            {
                EnsureNodeVisible(node);
                return;
            }
            executionNodeId = next;
            EnsureNodeVisible(node);
            Invalidate();
            ExecutionNodeChanged?.Invoke(this, ExecutionNode);
        }

        public void ClearExecutionPosition()
        {
            if (executionNodeId == null) return;
            executionNodeId = null;
            Invalidate();
            ExecutionNodeChanged?.Invoke(this, null);
        }

        private DdfFlowchartLayoutNode FindSelectedNode(DdfFlowchartLayout layout) =>
            layout?.Nodes.FirstOrDefault(item => string.Equals(item.Node.Id, selectedNodeId,
                StringComparison.Ordinal));

        private static DdfFlowchartLayoutNode FindNodeAtSource(DdfFlowchartLayout layout, int position)
        {
            if (layout == null) return null;
            DdfFlowchartLayoutNode exact = layout.Nodes.Where(item => item.Node.Length == 0
                    ? position == item.Node.Start
                    : position >= item.Node.Start && position < item.Node.Start + item.Node.Length)
                .OrderBy(item => item.Node.Length).ThenBy(item => item.Node.Start).FirstOrDefault();
            if (exact != null) return exact;
            return layout.Nodes.Where(item => item.Node.Length > 0)
                .OrderBy(item => SourceDistance(item.Node, position))
                .ThenBy(item => item.Node.Length)
                .FirstOrDefault();
        }

        private static int SourceDistance(DdfFlowchartNode node, int position)
        {
            if (position < node.Start) return node.Start - position;
            int end = node.Start + node.Length;
            return position > end ? position - end : 0;
        }

        private static DdfFlowchartLayoutNode FindNodeAtSourceRange(DdfFlowchartLayout layout,
            int start, int length)
        {
            if (layout == null) return null;
            int safeLength = Math.Max(1, length);
            int end = start > int.MaxValue - safeLength ? int.MaxValue : start + safeLength;
            DdfFlowchartLayoutNode contained = layout.Nodes.Where(item => item.Node.Length > 0 &&
                    item.Node.Start >= start && item.Node.Start < end)
                .OrderBy(item => item.Node.Kind == DdfFlowchartNodeKind.Decision ||
                    item.Node.Kind == DdfFlowchartNodeKind.LoopDecision ? 0 : 1)
                .ThenBy(item => item.Node.Start).ThenBy(item => item.Node.Length).FirstOrDefault();
            return contained ?? FindNodeAtSource(layout, start);
        }

        private static DdfFlowchartLayoutNode FindEquivalentNode(DdfFlowchartLayout layout,
            DdfFlowchartLayoutNode previous) =>
            layout?.Nodes.Where(item => item.Node.Kind == previous.Node.Kind &&
                    string.Equals(item.Node.Label, previous.Node.Label, StringComparison.Ordinal))
                .OrderBy(item => Math.Abs(item.Node.Start - previous.Node.Start)).FirstOrDefault();

        private PointF RelativeViewportCenter(Size extent, Point scroll)
        {
            if (extent.Width <= 0 || extent.Height <= 0) return new PointF(0.5F, 0.5F);
            return new PointF(
                Math.Max(0F, Math.Min(1F, (-scroll.X + ClientSize.Width / 2F) / extent.Width)),
                Math.Max(0F, Math.Min(1F, (-scroll.Y + ClientSize.Height / 2F) / extent.Height)));
        }

        private void RestoreScrollOffset(float horizontal, float vertical)
        {
            int maximumX = Math.Max(0, AutoScrollMinSize.Width - ClientSize.Width);
            int maximumY = Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);
            AutoScrollPosition = new Point(
                Math.Max(0, Math.Min(maximumX, (int)Math.Round(horizontal))),
                Math.Max(0, Math.Min(maximumY, (int)Math.Round(vertical))));
        }

        private void EnsureNodeVisible(DdfFlowchartLayoutNode node)
        {
            if (node == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            const int margin = 18;
            float currentX = -AutoScrollPosition.X;
            float currentY = -AutoScrollPosition.Y;
            float left = (float)(node.X * zoomFactor);
            float top = (float)(node.Y * zoomFactor);
            float right = (float)((node.X + node.Width) * zoomFactor);
            float bottom = (float)((node.Y + node.Height) * zoomFactor);
            float nextX = currentX;
            float nextY = currentY;
            if (left < currentX + margin) nextX = left - margin;
            else if (right > currentX + ClientSize.Width - margin)
                nextX = right - ClientSize.Width + margin;
            if (top < currentY + margin) nextY = top - margin;
            else if (bottom > currentY + ClientSize.Height - margin)
                nextY = bottom - ClientSize.Height + margin;
            if (Math.Abs(nextX - currentX) > 0.5F || Math.Abs(nextY - currentY) > 0.5F)
                RestoreScrollOffset(nextX, nextY);
        }

        private void SelectNode(DdfFlowchartLayoutNode node, bool activate)
        {
            if (node == null) return;
            bool changed = !string.Equals(selectedNodeId, node.Node.Id, StringComparison.Ordinal);
            selectedNodeId = node.Node.Id;
            EnsureNodeVisible(node);
            Invalidate();
            if (changed) SelectedNodeChanged?.Invoke(this, node.Node);
            if (activate) NodeActivated?.Invoke(this, node.Node);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!selectionToolEnabled || e.Button != MouseButtons.Left || diagram == null) return;
            DdfFlowchartLayoutNode hit = HitTest(e.Location);
            SelectNode(hit, true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (selectionToolEnabled && diagram != null &&
                (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right))
            {
                DdfFlowchartLayoutNode current = FindSelectedNode(diagram) ??
                    diagram.FindNode(diagram.Flowchart.EntryNodeId);
                SelectNode(FindAdjacentNode(current, key) ?? current, false);
                return true;
            }
            if (selectionToolEnabled && diagram != null &&
                (key == Keys.Enter || key == Keys.Space))
            {
                DdfFlowchartLayoutNode current = FindSelectedNode(diagram) ??
                    diagram.FindNode(diagram.Flowchart.EntryNodeId);
                SelectNode(current, true);
                return current != null;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private DdfFlowchartLayoutNode FindAdjacentNode(DdfFlowchartLayoutNode current, Keys key)
        {
            if (current == null) return diagram?.FindNode(diagram.Flowchart.EntryNodeId);
            double centerX = current.X + current.Width / 2;
            double centerY = current.Y + current.Height / 2;
            return diagram.Nodes.Where(item => !ReferenceEquals(item, current)).Select(item => new
                {
                    Node = item,
                    DeltaX = item.X + item.Width / 2 - centerX,
                    DeltaY = item.Y + item.Height / 2 - centerY
                })
                .Where(item => key == Keys.Up ? item.DeltaY < -0.5 :
                    key == Keys.Down ? item.DeltaY > 0.5 :
                    key == Keys.Left ? item.DeltaX < -0.5 : item.DeltaX > 0.5)
                .OrderBy(item => (key == Keys.Up || key == Keys.Down)
                    ? Math.Abs(item.DeltaY) * 4 + Math.Abs(item.DeltaX)
                    : Math.Abs(item.DeltaX) * 4 + Math.Abs(item.DeltaY))
                .Select(item => item.Node).FirstOrDefault();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!selectionToolEnabled || e.Button != MouseButtons.Left || diagram == null) return;
            DdfFlowchartNode node = HitTest(e.Location)?.Node;
            if (node?.IsCollapsedGroup == true) GroupActivated?.Invoke(this, node);
            else if (node?.CalledFunctions.Count > 0)
                FunctionActivated?.Invoke(this, node.CalledFunctions[0]);
        }

        private DdfFlowchartLayoutNode HitTest(Point location)
        {
            PointF translated = new PointF((location.X - AutoScrollPosition.X) / zoomFactor,
                (location.Y - AutoScrollPosition.Y) / zoomFactor);
            return diagram?.Nodes.FirstOrDefault(item =>
                new RectangleF((float)item.X, (float)item.Y, (float)item.Width, (float)item.Height)
                    .Contains(translated));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!panToolEnabled || e.Button != MouseButtons.Left) return;
            Focus();
            isPanning = true;
            panStartMouse = e.Location;
            panStartScroll = new Point(-AutoScrollPosition.X, -AutoScrollPosition.Y);
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!isPanning) return;
            int horizontal = panStartScroll.X - (e.X - panStartMouse.X);
            int vertical = panStartScroll.Y - (e.Y - panStartMouse.Y);
            RestoreScrollOffset(horizontal, vertical);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) EndPan();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) EndPan();
        }

        private void EndPan()
        {
            isPanning = false;
            if (Capture) Capture = false;
            UpdateToolCursor();
        }

        private void UpdateToolCursor()
        {
            if (isPanning) Cursor = Cursors.SizeAll;
            else Cursor = panToolEnabled ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawDiagram(e.Graphics, AutoScrollPosition, zoomFactor);
            DrawViewNotice(e.Graphics);
        }

        private void DrawViewNotice(Graphics graphics)
        {
            if (string.IsNullOrWhiteSpace(viewNotice)) return;
            Size proposed = new Size(Math.Max(80, ClientSize.Width - 24), 40);
            Size measured = TextRenderer.MeasureText(graphics, viewNotice, Font, proposed,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            int width = Math.Min(proposed.Width, measured.Width + 18);
            var bounds = new Rectangle(8, 8, width, 25);
            Color background = viewNoticeIsWarning
                ? Color.FromArgb(255, 244, 204)
                : Color.FromArgb(230, 237, 245);
            Color border = viewNoticeIsWarning
                ? Color.FromArgb(181, 129, 0)
                : Color.FromArgb(91, 118, 148);
            using (var brush = new SolidBrush(background))
            using (var pen = new Pen(border))
            {
                graphics.FillRectangle(brush, bounds);
                graphics.DrawRectangle(pen, bounds);
            }
            TextRenderer.DrawText(graphics, viewNotice, Font,
                new Rectangle(bounds.X + 8, bounds.Y, bounds.Width - 12, bounds.Height),
                AppTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawDiagram(Graphics graphics, Point offset, float scale)
        {
            if (diagram == null) return;
            GraphicsState state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(offset.X, offset.Y);
            graphics.ScaleTransform(scale, scale);
            var byId = diagram.Nodes.ToDictionary(item => item.Node.Id, StringComparer.Ordinal);
            IReadOnlyList<RoutedEdge> routes = BuildRoutes(byId);
            IReadOnlyList<MergeArrow> mergeArrows = BuildMergeArrows(routes);
            var labels = new List<EdgeLabel>();
            using (var edgePen = new Pen(Color.FromArgb(105, 112, 120), 1.5F) { EndCap = LineCap.ArrowAnchor })
            using (var backPen = new Pen(Color.FromArgb(86, 120, 166), 1.5F) { DashStyle = DashStyle.Dash })
            using (var backSegmentPen = new Pen(Color.FromArgb(86, 120, 166), 1.5F) { DashStyle = DashStyle.Dash })
            using (var trueTeachingPen = new Pen(Color.FromArgb(22, 130, 66), 3F) { EndCap = LineCap.ArrowAnchor })
            using (var falseTeachingPen = new Pen(Color.FromArgb(190, 55, 55), 3F) { EndCap = LineCap.ArrowAnchor })
            {
                foreach (RoutedEdge route in routes.Where(item => !item.Edge.IsBackEdge))
                {
                    bool selectedDecisionEdge = educationalModeEnabled &&
                        string.Equals(route.Edge.From, selectedNodeId, StringComparison.Ordinal) &&
                        (route.Edge.Kind == DdfFlowchartEdgeKind.True ||
                         route.Edge.Kind == DdfFlowchartEdgeKind.False);
                    Pen routePen = !selectedDecisionEdge ? edgePen :
                        route.Edge.Kind == DdfFlowchartEdgeKind.True ? trueTeachingPen : falseTeachingPen;
                    graphics.DrawLines(routePen, route.Points);
                }

                List<LineSegment> solidSegments = routes.Where(item => !item.Edge.IsBackEdge)
                    .SelectMany(item => Segments(item.Points)).ToList();
                foreach (RoutedEdge route in routes.Where(item => item.Edge.IsBackEdge))
                    DrawDashedRoute(graphics, route.Points, solidSegments, backSegmentPen, backPen);

                foreach (RoutedEdge route in routes)
                {
                    string label = route.Edge.Kind == DdfFlowchartEdgeKind.True ? trueLabel :
                        route.Edge.Kind == DdfFlowchartEdgeKind.False ? falseLabel : string.Empty;
                    if (label.Length > 0) labels.Add(new EdgeLabel(label, route.LabelPoint,
                        route.Edge.Kind == DdfFlowchartEdgeKind.True));
                }
            }

            foreach (MergeArrow arrow in mergeArrows) DrawMergeArrow(graphics, arrow);
            foreach (DdfFlowchartLayoutNode item in diagram.Nodes) DrawNode(graphics, item);
            foreach (EdgeLabel label in labels)
                DrawEdgeLabel(graphics, label.Text, label.Center, label.Positive);
            graphics.Restore(state);
        }

        private IReadOnlyList<RoutedEdge> BuildRoutes(
            IReadOnlyDictionary<string, DdfFlowchartLayoutNode> byId)
        {
            var routes = new List<RoutedEdge>();
            var occupiedSegments = new List<LineSegment>();
            var incoming = diagram.Flowchart.Edges.GroupBy(edge => edge.To)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            foreach (DdfFlowchartEdge edge in diagram.Flowchart.Edges.Where(item => !item.IsBackEdge))
            {
                if (!byId.TryGetValue(edge.From, out DdfFlowchartLayoutNode from) ||
                    !byId.TryGetValue(edge.To, out DdfFlowchartLayoutNode to)) continue;
                RoutedEdge route = CreateForwardRoute(edge, from, to,
                    incoming.TryGetValue(edge.To, out int count) && count > 1,
                    occupiedSegments);
                routes.Add(route);
                occupiedSegments.AddRange(Segments(route.Points));
            }

            foreach (DdfFlowchartEdge edge in diagram.Flowchart.Edges.Where(item => item.IsBackEdge))
            {
                if (!byId.TryGetValue(edge.From, out DdfFlowchartLayoutNode from) ||
                    !byId.TryGetValue(edge.To, out DdfFlowchartLayoutNode to)) continue;
                bool solidSibling = diagram.Flowchart.Edges.Any(item => item.From == edge.From && !item.IsBackEdge);
                RoutedEdge route = CreateBackRoute(edge, from, to, solidSibling,
                    occupiedSegments);
                routes.Add(route);
                occupiedSegments.AddRange(Segments(route.Points));
            }
            return routes.AsReadOnly();
        }

        private static IReadOnlyList<MergeArrow> BuildMergeArrows(IReadOnlyList<RoutedEdge> routes)
        {
            var arrows = new List<MergeArrow>();
            foreach (IGrouping<string, RoutedEdge> group in routes.GroupBy(item => item.Edge.To)
                .Where(item => item.Count() > 1))
            {
                List<RoutedEdge> incoming = group.ToList();
                foreach (RoutedEdge route in incoming)
                {
                    IReadOnlyList<PointF> points = route.Points;
                    for (int index = 1; index < points.Count; index++)
                    {
                        bool finalPoint = index == points.Count - 1;
                        if (finalPoint && !route.Edge.IsBackEdge) continue;
                        PointF junction = points[index];
                        bool touchesSharedLine = incoming.Where(item => !ReferenceEquals(item, route))
                            .SelectMany(item => Segments(item.Points))
                            .Any(segment => PointOnSegment(junction, segment));
                        if (!touchesSharedLine) continue;
                        LineSegment arrival = new LineSegment(points[index - 1], junction);
                        if (arrival.Length < 4F) continue;
                        arrows.Add(new MergeArrow(arrival, route.Edge.IsBackEdge));
                        break;
                    }
                }
            }
            return arrows.AsReadOnly();
        }

        private static bool PointOnSegment(PointF point, LineSegment segment)
        {
            const float tolerance = 0.01F;
            if (segment.IsVertical)
                return Math.Abs(point.X - segment.Start.X) < tolerance &&
                    point.Y >= segment.Minimum - tolerance && point.Y <= segment.Maximum + tolerance;
            return Math.Abs(point.Y - segment.Start.Y) < tolerance &&
                point.X >= segment.Minimum - tolerance && point.X <= segment.Maximum + tolerance;
        }

        private static void DrawMergeArrow(Graphics graphics, MergeArrow arrow)
        {
            LineSegment arrival = arrow.Arrival;
            float dx = arrival.End.X - arrival.Start.X;
            float dy = arrival.End.Y - arrival.Start.Y;
            float length = Math.Max(1F, (float)Math.Sqrt(dx * dx + dy * dy));
            float ux = dx / length;
            float uy = dy / length;
            float distanceFromJunction = Math.Min(6F, length * 0.25F);
            float arrowLength = Math.Min(7F, Math.Max(4F, length - distanceFromJunction));
            PointF tip = new PointF(arrival.End.X - ux * distanceFromJunction,
                arrival.End.Y - uy * distanceFromJunction);
            PointF baseCenter = new PointF(tip.X - ux * arrowLength, tip.Y - uy * arrowLength);
            float halfWidth = 3.5F;
            PointF perpendicular = new PointF(-uy * halfWidth, ux * halfWidth);
            Color color = arrow.BackEdge ? Color.FromArgb(67, 105, 155) : Color.FromArgb(82, 89, 98);
            using (var brush = new SolidBrush(color))
                graphics.FillPolygon(brush, new[] { tip,
                    new PointF(baseCenter.X + perpendicular.X, baseCenter.Y + perpendicular.Y),
                    new PointF(baseCenter.X - perpendicular.X, baseCenter.Y - perpendicular.Y) });
        }

        private RoutedEdge CreateForwardRoute(DdfFlowchartEdge edge, DdfFlowchartLayoutNode from,
            DdfFlowchartLayoutNode to, bool mergesAtTarget,
            IReadOnlyList<LineSegment> occupiedSegments)
        {
            PointF start = EdgeStart(from);
            PointF end = EdgeEnd(to);
            if (Math.Abs(start.X - end.X) < 1F)
                return new RoutedEdge(edge, new[] { start, end }, VerticalLabelPoint(edge, start));

            const float departureGap = 24F;
            float departureY = start.Y + departureGap;
            float approachY = end.Y - (mergesAtTarget ? 32F : 12F);
            PointF[] points;
            if (to.Rank <= from.Rank + 1)
            {
                float turnY = mergesAtTarget ? approachY : Math.Min(departureY, approachY);
                points = SimplifyRoute(new[] { start, new PointF(start.X, turnY),
                    new PointF(end.X, turnY), end });
            }
            else if (IsVerticalChannelFree(start.X, start.Y, approachY, from, to))
            {
                PointF[] direct = SimplifyRoute(new[] { start, new PointF(start.X, approachY),
                    new PointF(end.X, approachY), end });
                points = HasImproperIntersection(direct, occupiedSegments) ? null : direct;
            }
            else
            {
                points = null;
            }
            if (points == null)
            {
                float rail = FindNearestFreeRail(start.X, end.X, departureY, approachY,
                    from, to, false, occupiedSegments);
                points = SimplifyRoute(new[] { start, new PointF(start.X, departureY),
                    new PointF(rail, departureY), new PointF(rail, approachY),
                    new PointF(end.X, approachY), end });
            }
            return new RoutedEdge(edge, points, FirstHorizontalLabelPoint(points, start));
        }

        private RoutedEdge CreateBackRoute(DdfFlowchartEdge edge, DdfFlowchartLayoutNode from,
            DdfFlowchartLayoutNode to, bool solidSibling,
            IReadOnlyList<LineSegment> occupiedSegments)
        {
            PointF start = EdgeStart(from);
            PointF end = EdgeEnd(to);
            float sharedDepartureY = start.Y + 24F;
            float privateDepartureY = sharedDepartureY + (solidSibling ? 12F : 0F);
            float approachY = end.Y - 18F;
            PointF visibleStart = solidSibling ? new PointF(start.X, sharedDepartureY) : start;
            float rail = FindNearestFreeRail(start.X, end.X, privateDepartureY, approachY,
                from, to, true, occupiedSegments);
            var points = new List<PointF> { visibleStart };
            points.Add(new PointF(start.X, privateDepartureY));
            points.Add(new PointF(rail, privateDepartureY));
            points.Add(new PointF(rail, approachY));
            points.Add(new PointF(end.X, approachY));
            PointF[] simplified = SimplifyRoute(points);
            return new RoutedEdge(edge, simplified, FirstHorizontalLabelPoint(simplified, visibleStart));
        }

        private float FindNearestFreeRail(float startX, float endX, float startY, float endY,
            DdfFlowchartLayoutNode from, DdfFlowchartLayoutNode to, bool dashed,
            IReadOnlyList<LineSegment> occupiedSegments)
        {
            List<float> centers = diagram.Nodes.Select(item => (float)(item.X + item.Width / 2))
                .Distinct().OrderBy(value => value).ToList();
            var candidates = new List<float>();
            for (int index = 0; index + 1 < centers.Count; index++)
            {
                float channel = (centers[index] + centers[index + 1]) / 2F;
                candidates.Add(channel + (dashed ? 6F : -6F));
                candidates.Add(channel + (dashed ? -6F : 6F));
            }
            candidates.Add(Math.Max(14F, centers.FirstOrDefault() - 119F));
            candidates.Add(Math.Min((float)diagram.Width - 14F, centers.LastOrDefault() + 119F));

            PointF routeStart = new PointF(startX, startY);
            IEnumerable<float> ordered = candidates.Distinct().Where(candidate =>
                    IsVerticalChannelFree(candidate, startY, endY, from, to))
                .OrderBy(candidate => Math.Abs(candidate - startX) + Math.Abs(candidate - endX) +
                    Math.Abs(candidate - startX) * 0.05F);
            if (occupiedSegments != null)
            {
                float withoutIntersection = ordered.FirstOrDefault(candidate =>
                {
                    PointF[] proposed = SimplifyRoute(new[] { routeStart,
                        new PointF(candidate, startY), new PointF(candidate, endY),
                        new PointF(endX, endY) });
                    return !HasImproperIntersection(proposed, occupiedSegments) &&
                        (!dashed || !Segments(proposed).Any(segment => occupiedSegments.Any(existing =>
                            SegmentsOverlap(segment, existing))));
                });
                if (Math.Abs(withoutIntersection) > 0.01F) return withoutIntersection;
            }
            float selected = ordered.FirstOrDefault();
            if (Math.Abs(selected) > 0.01F) return selected;
            return Math.Max(14F, Math.Min((float)diagram.Width - 14F,
                startX <= endX ? Math.Min(startX, endX) - 24F : Math.Max(startX, endX) + 24F));
        }

        private bool IsVerticalChannelFree(float x, float firstY, float secondY,
            DdfFlowchartLayoutNode from, DdfFlowchartLayoutNode to)
        {
            float top = Math.Min(firstY, secondY);
            float bottom = Math.Max(firstY, secondY);
            const float clearance = 8F;
            return !diagram.Nodes.Where(item => !ReferenceEquals(item, from) && !ReferenceEquals(item, to))
                .Any(item => x > item.X - clearance && x < item.X + item.Width + clearance &&
                    bottom > item.Y - clearance && top < item.Y + item.Height + clearance);
        }

        private static PointF EdgeEnd(DdfFlowchartLayoutNode to) =>
            new PointF((float)(to.X + to.Width / 2), (float)to.Y);

        private static PointF VerticalLabelPoint(DdfFlowchartEdge edge, PointF start) =>
            new PointF(start.X + (edge.Kind == DdfFlowchartEdgeKind.True ? 20F : -20F), start.Y + 14F);

        private static PointF FirstHorizontalLabelPoint(IReadOnlyList<PointF> points, PointF fallback)
        {
            for (int index = 0; index + 1 < points.Count; index++)
                if (Math.Abs(points[index].Y - points[index + 1].Y) < 0.01F &&
                    Math.Abs(points[index].X - points[index + 1].X) > 0.01F)
                    return new PointF((points[index].X + points[index + 1].X) / 2F,
                        points[index].Y - 11F);
            return new PointF(fallback.X + 20F, fallback.Y + 14F);
        }

        private static PointF[] SimplifyRoute(IEnumerable<PointF> source)
        {
            var result = new List<PointF>();
            foreach (PointF point in source)
            {
                if (result.Count > 0 && SamePoint(result[result.Count - 1], point)) continue;
                while (result.Count >= 2 && AreCollinear(result[result.Count - 2],
                    result[result.Count - 1], point)) result.RemoveAt(result.Count - 1);
                result.Add(point);
            }
            return result.ToArray();
        }

        private static bool SamePoint(PointF left, PointF right) =>
            Math.Abs(left.X - right.X) < 0.01F && Math.Abs(left.Y - right.Y) < 0.01F;

        private static bool AreCollinear(PointF first, PointF second, PointF third) =>
            (Math.Abs(first.X - second.X) < 0.01F && Math.Abs(second.X - third.X) < 0.01F) ||
            (Math.Abs(first.Y - second.Y) < 0.01F && Math.Abs(second.Y - third.Y) < 0.01F);

        private static IEnumerable<LineSegment> Segments(IReadOnlyList<PointF> points)
        {
            for (int index = 0; index + 1 < points.Count; index++)
                if (!SamePoint(points[index], points[index + 1]))
                    yield return new LineSegment(points[index], points[index + 1]);
        }

        private static bool SegmentsOverlap(LineSegment left, LineSegment right)
        {
            const float tolerance = 0.01F;
            if (left.IsVertical && right.IsVertical && Math.Abs(left.Start.X - right.Start.X) < tolerance)
                return Math.Min(left.Maximum, right.Maximum) - Math.Max(left.Minimum, right.Minimum) > tolerance;
            if (!left.IsVertical && !right.IsVertical && Math.Abs(left.Start.Y - right.Start.Y) < tolerance)
                return Math.Min(left.Maximum, right.Maximum) - Math.Max(left.Minimum, right.Minimum) > tolerance;
            return false;
        }

        private static bool HasImproperIntersection(IReadOnlyList<PointF> points,
            IReadOnlyList<LineSegment> occupiedSegments) =>
            occupiedSegments != null && Segments(points).Any(segment =>
                occupiedSegments.Any(existing => SegmentsCross(segment, existing)));

        private static bool SegmentsCross(LineSegment left, LineSegment right)
        {
            if (left.IsVertical == right.IsVertical) return false;
            LineSegment vertical = left.IsVertical ? left : right;
            LineSegment horizontal = left.IsVertical ? right : left;
            const float tolerance = 0.01F;
            float x = vertical.Start.X;
            float y = horizontal.Start.Y;
            return x > horizontal.Minimum + tolerance && x < horizontal.Maximum - tolerance &&
                y > vertical.Minimum + tolerance && y < vertical.Maximum - tolerance;
        }

        private static void DrawDashedRoute(Graphics graphics, IReadOnlyList<PointF> points,
            IReadOnlyList<LineSegment> solidSegments, Pen segmentPen, Pen arrowPen)
        {
            List<LineSegment> dashedSegments = Segments(points).ToList();
            for (int index = 0; index < dashedSegments.Count; index++)
            {
                List<LineSegment> visible = SubtractOverlaps(dashedSegments[index], solidSegments);
                for (int part = 0; part < visible.Count; part++)
                {
                    bool finalPart = index == dashedSegments.Count - 1 && part == visible.Count - 1 &&
                        SamePoint(visible[part].End, dashedSegments[index].End);
                    graphics.DrawLine(finalPart ? arrowPen : segmentPen,
                        visible[part].Start, visible[part].End);
                }
            }
        }

        private static List<LineSegment> SubtractOverlaps(LineSegment source,
            IReadOnlyList<LineSegment> obstacles)
        {
            var ranges = new List<Tuple<float, float>>();
            foreach (LineSegment obstacle in obstacles.Where(item => SegmentsOverlap(source, item)))
                ranges.Add(Tuple.Create(Math.Max(source.Minimum, obstacle.Minimum),
                    Math.Min(source.Maximum, obstacle.Maximum)));
            if (ranges.Count == 0) return new List<LineSegment> { source };
            ranges = ranges.OrderBy(item => item.Item1).ToList();
            var visibleRanges = new List<Tuple<float, float>>();
            float cursor = source.Minimum;
            foreach (Tuple<float, float> range in ranges)
            {
                if (range.Item1 > cursor + 0.01F) visibleRanges.Add(Tuple.Create(cursor, range.Item1));
                cursor = Math.Max(cursor, range.Item2);
            }
            if (cursor < source.Maximum - 0.01F)
                visibleRanges.Add(Tuple.Create(cursor, source.Maximum));
            if (source.IsForward) return visibleRanges.Select(range => source.WithRange(range.Item1, range.Item2)).ToList();
            visibleRanges.Reverse();
            return visibleRanges.Select(range => source.WithRange(range.Item2, range.Item1)).ToList();
        }

        private static PointF EdgeStart(DdfFlowchartLayoutNode from)
        {
            float centerX = (float)(from.X + from.Width / 2);
            return new PointF(centerX, (float)(from.Y + from.Height));
        }

        private void DrawEdgeLabel(Graphics graphics, string text, PointF center, bool positive)
        {
            SizeF measured = graphics.MeasureString(text, Font);
            var bounds = new RectangleF(center.X - measured.Width / 2 - 3,
                center.Y - measured.Height / 2 - 1, measured.Width + 6, measured.Height + 2);
            Color foreground = positive ? Color.FromArgb(22, 104, 55) : Color.FromArgb(155, 46, 46);
            Color background = positive ? Color.FromArgb(238, 249, 241) : Color.FromArgb(253, 240, 240);
            using (var fill = new SolidBrush(background))
            using (var border = new Pen(Color.FromArgb(190, foreground), 1F))
            using (var brush = new SolidBrush(foreground))
            {
                graphics.FillRectangle(fill, bounds);
                graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                graphics.DrawString(text, Font, brush, center.X - measured.Width / 2,
                    center.Y - measured.Height / 2);
            }
        }

        private readonly struct EdgeLabel
        {
            public EdgeLabel(string text, PointF center, bool positive)
            { Text = text; Center = center; Positive = positive; }
            public string Text { get; }
            public PointF Center { get; }
            public bool Positive { get; }
        }

        private sealed class RoutedEdge
        {
            public RoutedEdge(DdfFlowchartEdge edge, PointF[] points, PointF labelPoint)
            { Edge = edge; Points = points; LabelPoint = labelPoint; }
            public DdfFlowchartEdge Edge { get; }
            public PointF[] Points { get; }
            public PointF LabelPoint { get; }
        }

        private sealed class MergeArrow
        {
            public MergeArrow(LineSegment arrival, bool backEdge)
            { Arrival = arrival; BackEdge = backEdge; }
            public LineSegment Arrival { get; }
            public bool BackEdge { get; }
        }

        private readonly struct LineSegment
        {
            public LineSegment(PointF start, PointF end) { Start = start; End = end; }
            public PointF Start { get; }
            public PointF End { get; }
            public bool IsVertical => Math.Abs(Start.X - End.X) < 0.01F;
            public bool IsForward => IsVertical ? End.Y >= Start.Y : End.X >= Start.X;
            public float Minimum => IsVertical ? Math.Min(Start.Y, End.Y) : Math.Min(Start.X, End.X);
            public float Maximum => IsVertical ? Math.Max(Start.Y, End.Y) : Math.Max(Start.X, End.X);
            public float Length => IsVertical ? Math.Abs(End.Y - Start.Y) : Math.Abs(End.X - Start.X);
            public LineSegment WithRange(float first, float second) => IsVertical
                ? new LineSegment(new PointF(Start.X, first), new PointF(Start.X, second))
                : new LineSegment(new PointF(first, Start.Y), new PointF(second, Start.Y));
        }

        private void DrawNode(Graphics graphics, DdfFlowchartLayoutNode item)
        {
            RectangleF bounds = new RectangleF((float)item.X, (float)item.Y, (float)item.Width, (float)item.Height);
            bool selected = string.Equals(item.Node.Id, selectedNodeId, StringComparison.Ordinal);
            bool executing = string.Equals(item.Node.Id, executionNodeId, StringComparison.Ordinal);
            Color fill = executing ? Color.FromArgb(255, 238, 153) :
                selected ? Color.FromArgb(221, 235, 252) : NodeColor(item.Node.Kind);
            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(executing ? Color.FromArgb(196, 124, 0) :
                selected ? Color.FromArgb(35, 105, 178) : Color.FromArgb(92, 104, 118),
                executing ? 3F : selected ? 2.5F : 1.3F))
            using (var textBrush = new SolidBrush(AppTheme.Text))
            using (GraphicsPath path = NodePath(bounds, item.Node.Kind))
            using (var format = new StringFormat { Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord })
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                RectangleF textBounds = RectangleF.Inflate(bounds, -10, -6);
                graphics.DrawString(item.Node.Label, Font, textBrush, textBounds, format);
                if (item.Node.IsCollapsedGroup)
                {
                    var badge = new RectangleF(bounds.Right - 18F, bounds.Top + 5F, 12F, 12F);
                    using (var badgeBrush = new SolidBrush(Color.White))
                    using (var badgePen = new Pen(Color.FromArgb(63, 99, 118), 1F))
                    {
                        graphics.FillRectangle(badgeBrush, badge);
                        graphics.DrawRectangle(badgePen, badge.X, badge.Y, badge.Width, badge.Height);
                        graphics.DrawLine(badgePen, badge.Left + 3F, badge.Top + 6F,
                            badge.Right - 3F, badge.Top + 6F);
                        graphics.DrawLine(badgePen, badge.Left + 6F, badge.Top + 3F,
                            badge.Left + 6F, badge.Bottom - 3F);
                    }
                }
                if (executing) DrawExecutionBadge(graphics, bounds);
            }
        }

        private static void DrawExecutionBadge(Graphics graphics, RectangleF bounds)
        {
            var badge = new RectangleF(bounds.Left + 5F, bounds.Top + 5F, 16F, 16F);
            using (var fill = new SolidBrush(Color.FromArgb(31, 122, 71)))
            using (var triangle = new SolidBrush(Color.White))
            {
                graphics.FillEllipse(fill, badge);
                graphics.FillPolygon(triangle, new[]
                {
                    new PointF(badge.Left + 6F, badge.Top + 4F),
                    new PointF(badge.Right - 4F, badge.Top + badge.Height / 2F),
                    new PointF(badge.Left + 6F, badge.Bottom - 4F)
                });
            }
        }

        private static Color NodeColor(DdfFlowchartNodeKind kind)
        {
            if (kind == DdfFlowchartNodeKind.Entry || kind == DdfFlowchartNodeKind.Exit)
                return Color.FromArgb(220, 237, 225);
            if (kind == DdfFlowchartNodeKind.Decision || kind == DdfFlowchartNodeKind.LoopDecision)
                return Color.FromArgb(224, 235, 250);
            if (kind == DdfFlowchartNodeKind.Return || kind == DdfFlowchartNodeKind.Terminate)
                return Color.FromArgb(250, 229, 224);
            if (kind == DdfFlowchartNodeKind.Group) return Color.FromArgb(232, 242, 246);
            if (kind == DdfFlowchartNodeKind.Call) return Color.FromArgb(239, 230, 250);
            return Color.FromArgb(247, 247, 247);
        }

        private static GraphicsPath NodePath(RectangleF bounds, DdfFlowchartNodeKind kind)
        {
            var path = new GraphicsPath();
            if (kind == DdfFlowchartNodeKind.Decision || kind == DdfFlowchartNodeKind.LoopDecision)
            {
                path.AddPolygon(new[] { new PointF(bounds.Left + bounds.Width / 2, bounds.Top),
                    new PointF(bounds.Right, bounds.Top + bounds.Height / 2),
                    new PointF(bounds.Left + bounds.Width / 2, bounds.Bottom),
                    new PointF(bounds.Left, bounds.Top + bounds.Height / 2) });
            }
            else if (kind == DdfFlowchartNodeKind.Entry || kind == DdfFlowchartNodeKind.Exit)
                path.AddEllipse(bounds);
            else
            {
                const float radius = 9;
                path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
                path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
                path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
            }
            return path;
        }
    }
}
