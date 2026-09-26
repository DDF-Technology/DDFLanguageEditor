using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public static class DdfFlowchartProjection
    {
        public static DdfFlowchart CollapseLinearSequences(DdfFlowchart flowchart,
            int minimumGroupSize = 3, IEnumerable<string> expandedGroupIds = null)
        {
            if (flowchart == null) throw new ArgumentNullException(nameof(flowchart));
            if (minimumGroupSize < 2) throw new ArgumentOutOfRangeException(nameof(minimumGroupSize));
            var expanded = new HashSet<string>(expandedGroupIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var byId = flowchart.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var incoming = flowchart.Edges.Where(edge => !edge.IsBackEdge)
                .GroupBy(edge => edge.To).ToDictionary(group => group.Key,
                    group => group.ToList(), StringComparer.Ordinal);
            var outgoing = flowchart.Edges.Where(edge => !edge.IsBackEdge)
                .GroupBy(edge => edge.From).ToDictionary(group => group.Key,
                    group => group.ToList(), StringComparer.Ordinal);
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            var groups = new List<IReadOnlyList<DdfFlowchartNode>>();

            foreach (DdfFlowchartNode node in flowchart.Nodes.Where(IsCollapsible))
            {
                if (assigned.Contains(node.Id) || HasLinearPredecessor(node, byId, incoming, outgoing))
                    continue;
                var chain = new List<DdfFlowchartNode>();
                DdfFlowchartNode current = node;
                while (current != null && IsCollapsible(current) && assigned.Add(current.Id))
                {
                    chain.Add(current);
                    current = LinearSuccessor(current, byId, incoming, outgoing);
                }
                if (chain.Count >= minimumGroupSize) groups.Add(chain.AsReadOnly());
            }

            var collapsedGroups = groups.Where(group => !expanded.Contains(GroupId(group))).ToList();
            if (collapsedGroups.Count == 0) return flowchart;
            var replacementByNode = new Dictionary<string, string>(StringComparer.Ordinal);
            var groupByFirst = new Dictionary<string, DdfFlowchartNode>(StringComparer.Ordinal);
            foreach (IReadOnlyList<DdfFlowchartNode> group in collapsedGroups)
            {
                string id = GroupId(group);
                int start = group.Min(node => node.Start);
                int end = group.Max(node => node.Start + node.Length);
                var projected = new DdfFlowchartNode(id, DdfFlowchartNodeKind.Group,
                    group.Count + " istruzioni (doppio clic per espandere)", start,
                    Math.Max(0, end - start), group.SelectMany(node => node.CalledFunctions),
                    group.Select(node => node.Id));
                groupByFirst[group[0].Id] = projected;
                foreach (DdfFlowchartNode child in group) replacementByNode[child.Id] = id;
            }

            var nodes = new List<DdfFlowchartNode>();
            foreach (DdfFlowchartNode node in flowchart.Nodes)
            {
                if (!replacementByNode.ContainsKey(node.Id)) nodes.Add(node);
                else if (groupByFirst.TryGetValue(node.Id, out DdfFlowchartNode group)) nodes.Add(group);
            }

            var edges = flowchart.Edges.Select(edge => new DdfFlowchartEdge(
                    replacementByNode.TryGetValue(edge.From, out string from) ? from : edge.From,
                    replacementByNode.TryGetValue(edge.To, out string to) ? to : edge.To,
                    edge.Kind, edge.IsBackEdge))
                .Where(edge => !string.Equals(edge.From, edge.To, StringComparison.Ordinal))
                .GroupBy(edge => new { edge.From, edge.To, edge.Kind, edge.IsBackEdge })
                .Select(group => group.First()).ToList();
            return new DdfFlowchart(flowchart.DocumentPath, flowchart.FunctionName,
                flowchart.EntryNodeId, flowchart.ExitNodeId, nodes, edges);
        }

        private static bool IsCollapsible(DdfFlowchartNode node) =>
            node != null && (node.Kind == DdfFlowchartNodeKind.Process ||
                node.Kind == DdfFlowchartNodeKind.Call || node.Kind == DdfFlowchartNodeKind.Partial);

        private static bool HasLinearPredecessor(DdfFlowchartNode node,
            IReadOnlyDictionary<string, DdfFlowchartNode> byId,
            IReadOnlyDictionary<string, List<DdfFlowchartEdge>> incoming,
            IReadOnlyDictionary<string, List<DdfFlowchartEdge>> outgoing)
        {
            if (!incoming.TryGetValue(node.Id, out List<DdfFlowchartEdge> edges) || edges.Count != 1)
                return false;
            DdfFlowchartEdge edge = edges[0];
            return edge.Kind == DdfFlowchartEdgeKind.Next && byId.TryGetValue(edge.From, out DdfFlowchartNode previous) &&
                IsCollapsible(previous) && outgoing.TryGetValue(previous.Id, out List<DdfFlowchartEdge> previousEdges) &&
                previousEdges.Count == 1;
        }

        private static DdfFlowchartNode LinearSuccessor(DdfFlowchartNode node,
            IReadOnlyDictionary<string, DdfFlowchartNode> byId,
            IReadOnlyDictionary<string, List<DdfFlowchartEdge>> incoming,
            IReadOnlyDictionary<string, List<DdfFlowchartEdge>> outgoing)
        {
            if (!outgoing.TryGetValue(node.Id, out List<DdfFlowchartEdge> edges) || edges.Count != 1 ||
                edges[0].Kind != DdfFlowchartEdgeKind.Next ||
                !byId.TryGetValue(edges[0].To, out DdfFlowchartNode next) || !IsCollapsible(next) ||
                !incoming.TryGetValue(next.Id, out List<DdfFlowchartEdge> nextEdges) || nextEdges.Count != 1)
                return null;
            return next;
        }

        private static string GroupId(IReadOnlyList<DdfFlowchartNode> group) =>
            "group:" + group[0].Id + ":" + group[group.Count - 1].Id;
    }
}
