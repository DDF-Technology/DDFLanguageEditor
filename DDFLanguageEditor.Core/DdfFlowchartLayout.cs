using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfFlowchartLayoutNode
    {
        internal DdfFlowchartLayoutNode(DdfFlowchartNode node, double x, double y, double width,
            double height, int rank, int lane)
        { Node = node; X = x; Y = y; Width = width; Height = height; Rank = rank; Lane = lane; }
        public DdfFlowchartNode Node { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public int Rank { get; }
        public int Lane { get; }
    }

    public sealed class DdfFlowchartLayout
    {
        internal DdfFlowchartLayout(DdfFlowchart flowchart, IEnumerable<DdfFlowchartLayoutNode> nodes,
            double width, double height)
        { Flowchart = flowchart; Nodes = nodes.ToList().AsReadOnly(); Width = width; Height = height; }
        public DdfFlowchart Flowchart { get; }
        public IReadOnlyList<DdfFlowchartLayoutNode> Nodes { get; }
        public double Width { get; }
        public double Height { get; }
        public DdfFlowchartLayoutNode FindNode(string id) => Nodes.FirstOrDefault(item => item.Node.Id == id);
    }

    public static class DdfFlowchartLayouter
    {
        public static DdfFlowchartLayout Layout(DdfFlowchart flowchart)
        {
            if (flowchart == null) throw new ArgumentNullException(nameof(flowchart));
            const double nodeWidth = 190;
            const double nodeHeight = 64;
            const double horizontalGap = 48;
            const double verticalGap = 56;
            const double padding = 32;

            var ranks = flowchart.Nodes.ToDictionary(node => node.Id, node => -1, StringComparer.Ordinal);
            ranks[flowchart.EntryNodeId] = 0;
            IReadOnlyList<DdfFlowchartEdge> forward = flowchart.Edges.Where(edge => !edge.IsBackEdge).ToList();
            for (int pass = 0; pass < flowchart.Nodes.Count; pass++)
            {
                bool changed = false;
                foreach (DdfFlowchartEdge edge in forward)
                {
                    if (!ranks.TryGetValue(edge.From, out int fromRank) || fromRank < 0 || !ranks.ContainsKey(edge.To)) continue;
                    int candidate = fromRank + 1;
                    if (candidate <= ranks[edge.To]) continue;
                    ranks[edge.To] = candidate;
                    changed = true;
                }
                if (!changed) break;
            }

            int fallbackRank = ranks.Values.Where(value => value >= 0).DefaultIfEmpty(0).Max() + 1;
            foreach (DdfFlowchartNode node in flowchart.Nodes.Where(node => ranks[node.Id] < 0)
                .OrderBy(node => node.Start).ThenBy(node => node.Id, StringComparer.Ordinal))
                ranks[node.Id] = fallbackRank++;

            var lanes = flowchart.Nodes.ToDictionary(node => node.Id, node => int.MaxValue, StringComparer.Ordinal);
            lanes[flowchart.EntryNodeId] = 0;
            foreach (DdfFlowchartNode from in flowchart.Nodes.OrderBy(node => ranks[node.Id]).ThenBy(node => node.Start))
            {
                int lane = lanes[from.Id] == int.MaxValue ? 0 : lanes[from.Id];
                foreach (DdfFlowchartEdge edge in flowchart.Edges.Where(item => item.From == from.Id && !item.IsBackEdge)
                    .OrderBy(item => item.Kind))
                {
                    DdfFlowchartNode target = flowchart.FindNode(edge.To);
                    if (target == null || target.Kind == DdfFlowchartNodeKind.Exit) continue;
                    int candidate = lane;
                    if (from.Kind == DdfFlowchartNodeKind.LoopDecision && edge.Kind == DdfFlowchartEdgeKind.True)
                        candidate = lane + 1;
                    else if (from.Kind == DdfFlowchartNodeKind.Decision && edge.Kind == DdfFlowchartEdgeKind.True)
                        candidate = lane - 1;
                    else if (from.Kind == DdfFlowchartNodeKind.Decision && edge.Kind == DdfFlowchartEdgeKind.False)
                        candidate = lane + 1;
                    if (lanes[target.Id] == int.MaxValue || edge.Kind == DdfFlowchartEdgeKind.Next ||
                        (from.Kind == DdfFlowchartNodeKind.LoopDecision && edge.Kind == DdfFlowchartEdgeKind.False))
                        lanes[target.Id] = candidate;
                }
            }
            lanes[flowchart.ExitNodeId] = 0;
            foreach (string id in lanes.Where(pair => pair.Value == int.MaxValue).Select(pair => pair.Key).ToList()) lanes[id] = 0;
            foreach (DdfFlowchartNode merge in flowchart.Nodes.Where(node => node.Kind != DdfFlowchartNodeKind.Exit))
            {
                List<DdfFlowchartEdge> incoming = forward.Where(edge => edge.To == merge.Id).ToList();
                if (incoming.Count < 2) continue;
                lanes[merge.Id] = (int)Math.Round(incoming.Average(edge => (double)lanes[edge.From]),
                    MidpointRounding.AwayFromZero);
            }

            var groups = flowchart.Nodes.GroupBy(node => ranks[node.Id]).OrderBy(group => group.Key).ToList();
            var assignedLanes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (IGrouping<int, DdfFlowchartNode> rankGroup in groups)
            {
                var occupied = new HashSet<int>();
                foreach (DdfFlowchartNode node in rankGroup.OrderBy(item => lanes[item.Id])
                    .ThenBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal))
                {
                    int lane = lanes[node.Id];
                    while (occupied.Contains(lane)) lane++;
                    occupied.Add(lane);
                    assignedLanes[node.Id] = lane;
                }
            }
            int minimumLane = Math.Min(0, assignedLanes.Values.Min());
            int maximumLane = Math.Max(0, assignedLanes.Values.Max());
            int laneCount = maximumLane - minimumLane + 1;
            double contentWidth = laneCount * nodeWidth + Math.Max(0, laneCount - 1) * horizontalGap;
            var result = new List<DdfFlowchartLayoutNode>();
            foreach (IGrouping<int, DdfFlowchartNode> rankGroup in groups)
            {
                foreach (DdfFlowchartNode node in rankGroup.OrderBy(item => assignedLanes[item.Id])
                    .ThenBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal))
                {
                    int lane = assignedLanes[node.Id];
                    result.Add(new DdfFlowchartLayoutNode(node,
                        padding + (lane - minimumLane) * (nodeWidth + horizontalGap),
                        padding + ranks[node.Id] * (nodeHeight + verticalGap), nodeWidth, nodeHeight,
                        ranks[node.Id], lane));
                }
            }

            int maximumRank = ranks.Values.DefaultIfEmpty(0).Max();
            return new DdfFlowchartLayout(flowchart, result, contentWidth + padding * 2,
                padding * 2 + nodeHeight + maximumRank * (nodeHeight + verticalGap));
        }
    }
}
