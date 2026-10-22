using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class WorkspaceSessionDocumentSnapshot
    {
        public string Path { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }
        public int? ScrollX { get; set; }
        public int? ScrollY { get; set; }
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
            return ParseVersioned(value).Value;
        }

        public static DdfPersistenceReadResult<WorkspaceSessionSnapshot>
            ParseVersioned(string value)
        {
            DdfPersistencePreparation preparation =
                DdfPersistenceSerialization.PrepareJson(
                    DdfPersistenceArtifactKind.WorkspaceSession, value);
            if (!preparation.Succeeded)
                return DdfPersistenceReadResult<WorkspaceSessionSnapshot>.Failed(
                    value, preparation, preparation.Failure);
            try
            {
                WorkspaceSessionSnapshot snapshot = JsonSerializer
                    .Deserialize<WorkspaceSessionSnapshot>(
                        preparation.PreparedArtifact.Payload);
                if (snapshot == null || snapshot.FormatVersion != 1 || snapshot.Documents == null || snapshot.Documents.Count == 0)
                    return DdfPersistenceReadResult<WorkspaceSessionSnapshot>.Failed(
                        value, preparation,
                        "La sessione workspace non contiene documenti validi.");
                snapshot.Documents = snapshot.Documents.Where(document => document != null).Select(sanitize).ToList();
                if (snapshot.Documents.Count == 0)
                    return DdfPersistenceReadResult<WorkspaceSessionSnapshot>.Failed(
                        value, preparation,
                        "La sessione workspace non contiene documenti leggibili.");
                snapshot.ActiveDocumentIndex = Math.Max(0, Math.Min(snapshot.Documents.Count - 1, snapshot.ActiveDocumentIndex));
                return DdfPersistenceReadResult<WorkspaceSessionSnapshot>.Success(
                    value, preparation, snapshot);
            }
            catch (JsonException exception)
            {
                return DdfPersistenceReadResult<WorkspaceSessionSnapshot>.Failed(
                    value, preparation,
                    "Sessione workspace non valida: " + exception.Message);
            }
        }

        private static WorkspaceSessionDocumentSnapshot sanitize(WorkspaceSessionDocumentSnapshot document)
        {
            document.SelectionStart = Math.Max(0, document.SelectionStart);
            document.SelectionLength = Math.Max(0, document.SelectionLength);
            if (document.ScrollX.HasValue) document.ScrollX = Math.Max(0, document.ScrollX.Value);
            if (document.ScrollY.HasValue) document.ScrollY = Math.Max(0, document.ScrollY.Value);
            document.BreakpointLines = (document.BreakpointLines ?? new List<int>())
                .Where(line => line > 0).Distinct().OrderBy(line => line).ToList();
            return document;
        }
    }
}
