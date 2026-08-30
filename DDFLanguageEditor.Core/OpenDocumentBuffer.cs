using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class OpenDocumentBuffer
    {
        internal OpenDocumentBuffer(string id, string path, string source)
        {
            Id = id;
            Session = new DocumentSession();
            Source = source ?? string.Empty;
            BreakpointLines = new HashSet<int>();
            UnboundBreakpointLines = new HashSet<int>();
            if (string.IsNullOrEmpty(path)) Session.SetUntitled();
            else Session.SetLoaded(path);
        }

        public string Id { get; }

        public DocumentSession Session { get; }

        public string Source { get; private set; }

        public ISet<int> BreakpointLines { get; }

        public ISet<int> UnboundBreakpointLines { get; }

        public void UpdateSource(string source, bool markDirty = true)
        {
            Source = source ?? string.Empty;
            if (markDirty) Session.MarkDirty();
        }

        public void MarkSaved(string path)
        {
            Session.MarkSaved(path);
        }
    }

    public sealed class OpenDocumentCollection
    {
        private readonly List<OpenDocumentBuffer> documents = new List<OpenDocumentBuffer>();
        private int nextId;

        public IReadOnlyList<OpenDocumentBuffer> Documents => documents;

        public OpenDocumentBuffer ActiveDocument { get; private set; }

        public OpenDocumentBuffer CreateUntitled()
        {
            return Add(null, string.Empty);
        }

        public OpenDocumentBuffer Open(string path, string source)
        {
            string normalizedPath = DdfDocumentFile.NormalizePath(path);
            OpenDocumentBuffer existing = FindByPath(normalizedPath);
            if (existing != null)
            {
                ActiveDocument = existing;
                return existing;
            }

            return Add(normalizedPath, source);
        }

        public OpenDocumentBuffer FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string normalizedPath = DdfDocumentFile.NormalizePath(path);
            return documents.FirstOrDefault(document => document.Session.HasPath &&
                string.Equals(document.Session.CurrentPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        }

        public bool Activate(string id)
        {
            OpenDocumentBuffer document = documents.FirstOrDefault(item => item.Id == id);
            if (document == null) return false;
            ActiveDocument = document;
            return true;
        }

        public bool Remove(string id)
        {
            int index = documents.FindIndex(item => item.Id == id);
            if (index < 0) return false;
            OpenDocumentBuffer removed = documents[index];
            documents.RemoveAt(index);
            if (ReferenceEquals(ActiveDocument, removed))
            {
                ActiveDocument = documents.Count == 0 ? null : documents[Math.Min(index, documents.Count - 1)];
            }
            return true;
        }

        private OpenDocumentBuffer Add(string path, string source)
        {
            var document = new OpenDocumentBuffer("document-" + (++nextId), path, source);
            documents.Add(document);
            ActiveDocument = document;
            return document;
        }
    }
}
