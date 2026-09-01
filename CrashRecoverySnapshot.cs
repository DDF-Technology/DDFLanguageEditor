using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DDF___Program_Language_Editor
{
    public sealed class CrashRecoverySnapshot
    {
        public int FormatVersion { get; set; } = 1;
        public DateTime SavedAtUtc { get; set; }
        public int ActiveDocumentIndex { get; set; }
        public List<HotExitDocumentSnapshot> Documents { get; set; } = new List<HotExitDocumentSnapshot>();

        public string Serialize()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        public static CrashRecoverySnapshot Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                CrashRecoverySnapshot snapshot = JsonSerializer.Deserialize<CrashRecoverySnapshot>(value);
                if (snapshot == null || snapshot.FormatVersion != 1 || snapshot.Documents == null ||
                    snapshot.Documents.Count == 0) return null;
                snapshot.Documents = snapshot.Documents.Where(document => document != null && document.Source != null)
                    .Select(sanitize).ToList();
                if (snapshot.Documents.Count == 0) return null;
                snapshot.ActiveDocumentIndex = Math.Max(0, Math.Min(snapshot.Documents.Count - 1, snapshot.ActiveDocumentIndex));
                return snapshot;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static HotExitDocumentSnapshot sanitize(HotExitDocumentSnapshot document)
        {
            document.SessionDocumentIndex = Math.Max(-1, document.SessionDocumentIndex);
            int length = document.Source.Length;
            document.SelectionStart = Math.Max(0, Math.Min(length, document.SelectionStart));
            document.SelectionLength = Math.Max(0, Math.Min(length - document.SelectionStart, document.SelectionLength));
            document.BreakpointLines = (document.BreakpointLines ?? new List<int>())
                .Where(line => line > 0).Distinct().OrderBy(line => line).ToList();
            return document;
        }
    }
}
