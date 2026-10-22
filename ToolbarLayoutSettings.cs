using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class ToolbarLayoutSettings
    {
        private readonly Dictionary<string, ToolbarPlacement> placements;

        private ToolbarLayoutSettings(Dictionary<string, ToolbarPlacement> placements)
        {
            this.placements = placements;
        }

        public static ToolbarLayoutSettings Parse(string value, IEnumerable<string> groupIds)
        {
            string[] ids = groupIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return ParseVersioned(value, ids).Value ?? CreateDefault(ids);
        }

        internal static DdfPersistenceReadResult<ToolbarLayoutSettings>
            ParseVersioned(string value, IEnumerable<string> groupIds)
        {
            string[] ids = groupIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            DdfPersistencePreparation preparation =
                DdfPersistenceSerialization.PrepareKeyValue(
                    DdfPersistenceArtifactKind.ToolbarLayout, value);
            if (!preparation.Succeeded)
                return DdfPersistenceReadResult<ToolbarLayoutSettings>.Failed(
                    value, preparation, preparation.Failure);
            var placements = ids.ToDictionary(id => id,
                id => new ToolbarPlacement(true, null, null, null), StringComparer.OrdinalIgnoreCase);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in preparation.PreparedArtifact.Payload.Split(
                new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            foreach (string id in placements.Keys.ToArray())
            {
                bool visible = readBool(values, id + ".visible", readBool(values, id, true));
                Point? location = readInt(values, id + ".x", out int x) &&
                    readInt(values, id + ".y", out int y)
                    ? new Point(Math.Max(0, x), Math.Max(0, y))
                    : (Point?)null;
                int? row = readInt(values, id + ".row", out int parsedRow)
                    ? Math.Max(0, parsedRow)
                    : (int?)null;
                int? order = readInt(values, id + ".order", out int parsedOrder)
                    ? Math.Max(0, parsedOrder)
                    : (int?)null;
                placements[id] = new ToolbarPlacement(visible, location, row, order);
            }

            migrateCoordinateOnlyLayout(placements, ids);
            return DdfPersistenceReadResult<ToolbarLayoutSettings>.Success(
                value, preparation, new ToolbarLayoutSettings(placements));
        }

        public bool IsVisible(string groupId) =>
            !placements.TryGetValue(groupId, out ToolbarPlacement placement) || placement.Visible;

        public Point? GetLocation(string groupId) =>
            placements.TryGetValue(groupId, out ToolbarPlacement placement) ? placement.Location : null;

        public int? GetRow(string groupId) =>
            placements.TryGetValue(groupId, out ToolbarPlacement placement) ? placement.Row : null;

        public int? GetOrder(string groupId) =>
            placements.TryGetValue(groupId, out ToolbarPlacement placement) ? placement.Order : null;

        public void Set(string groupId, bool visible, Point location, int row, int order)
        {
            placements[groupId] = new ToolbarPlacement(visible,
                new Point(Math.Max(0, location.X), Math.Max(0, location.Y)),
                Math.Max(0, row), Math.Max(0, order));
        }

        public void SetVisible(string groupId, bool visible)
        {
            Point? location = GetLocation(groupId);
            int? row = placements.TryGetValue(groupId, out ToolbarPlacement placement) ? placement.Row : null;
            int? order = placement?.Order;
            placements[groupId] = new ToolbarPlacement(visible, location, row, order);
        }

        public string Serialize()
        {
            var lines = new List<string>
            {
                "schemaVersion=" + DdfPersistenceSchemaCatalog.ToolbarLayoutVersion
            };
            foreach (KeyValuePair<string, ToolbarPlacement> pair in placements
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add(pair.Key + ".visible=" + pair.Value.Visible);
                if (pair.Value.Row.HasValue) lines.Add(pair.Key + ".row=" + pair.Value.Row.Value);
                if (pair.Value.Order.HasValue) lines.Add(pair.Key + ".order=" + pair.Value.Order.Value);
                if (!pair.Value.Location.HasValue) continue;
                lines.Add(pair.Key + ".x=" + pair.Value.Location.Value.X);
                lines.Add(pair.Key + ".y=" + pair.Value.Location.Value.Y);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static ToolbarLayoutSettings CreateDefault(IEnumerable<string> ids) =>
            new ToolbarLayoutSettings(ids.Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(id => id,
                    id => new ToolbarPlacement(true, null, null, null),
                    StringComparer.OrdinalIgnoreCase));

        private static bool readBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
            values.TryGetValue(key, out string value) && bool.TryParse(value, out bool parsed) ? parsed : fallback;

        private static bool readInt(IReadOnlyDictionary<string, string> values, string key, out int parsed)
        {
            parsed = 0;
            return values.TryGetValue(key, out string value) && int.TryParse(value, out parsed);
        }

        private static void migrateCoordinateOnlyLayout(
            IDictionary<string, ToolbarPlacement> placements, IEnumerable<string> ids)
        {
            ToolbarPlacement[] located = ids.Select(id => placements[id])
                .Where(item => item.Location.HasValue).ToArray();
            if (located.Length == 0 || located.All(item => item.Row.HasValue && item.Order.HasValue)) return;

            int[] rowTops = located.Select(item => item.Location.Value.Y).Distinct().OrderBy(value => value).ToArray();
            foreach (int rowTop in rowTops)
            {
                string[] rowIds = ids.Where(id => placements[id].Location?.Y == rowTop)
                    .OrderBy(id => placements[id].Location.Value.X).ToArray();
                for (int order = 0; order < rowIds.Length; order++)
                {
                    string id = rowIds[order];
                    ToolbarPlacement current = placements[id];
                    placements[id] = new ToolbarPlacement(current.Visible, current.Location,
                        Array.IndexOf(rowTops, rowTop), order);
                }
            }
        }

        private sealed class ToolbarPlacement
        {
            public ToolbarPlacement(bool visible, Point? location, int? row, int? order)
            { Visible = visible; Location = location; Row = row; Order = order; }
            public bool Visible { get; }
            public Point? Location { get; }
            public int? Row { get; }
            public int? Order { get; }
        }
    }
}
