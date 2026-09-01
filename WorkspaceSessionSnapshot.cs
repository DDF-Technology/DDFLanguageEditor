using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DDF___Program_Language_Editor
{
    public sealed class WorkspaceSessionDocumentSnapshot
    {
        public string Path { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }
        public List<int> BreakpointLines { get; set; } = new List<int>();
    }

    public sealed class WorkspaceSessionSnapshot
    {
        public int FormatVersion { get; set; } = 1;
        public string WorkspacePath { get; set; }
        public int ActiveDocumentIndex { get; set; }
        public List<WorkspaceSessionDocumentSnapshot> Documents { get; set; } =
            new List<WorkspaceSessionDocumentSnapshot>();

        public string Serialize()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        public static WorkspaceSessionSnapshot Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                WorkspaceSessionSnapshot snapshot = JsonSerializer.Deserialize<WorkspaceSessionSnapshot>(value);
                if (snapshot == null || snapshot.FormatVersion != 1 || snapshot.Documents == null || snapshot.Documents.Count == 0)
                    return null;
                snapshot.Documents = snapshot.Documents.Where(document => document != null).Select(sanitize).ToList();
                if (snapshot.Documents.Count == 0) return null;
                snapshot.ActiveDocumentIndex = Math.Max(0, Math.Min(snapshot.Documents.Count - 1, snapshot.ActiveDocumentIndex));
                return snapshot;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static WorkspaceSessionDocumentSnapshot sanitize(WorkspaceSessionDocumentSnapshot document)
        {
            document.SelectionStart = Math.Max(0, document.SelectionStart);
            document.SelectionLength = Math.Max(0, document.SelectionLength);
            document.BreakpointLines = (document.BreakpointLines ?? new List<int>())
                .Where(line => line > 0).Distinct().OrderBy(line => line).ToList();
            return document;
        }
    }
}
