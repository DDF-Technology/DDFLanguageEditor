using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class OpenDocumentBuffer : IDdfEditorDocument
    {
        internal OpenDocumentBuffer(string id, string path, string source, string virtualDisplayName = null)
        {
            Id = id;
            Session = new DocumentSession();
            Source = source ?? string.Empty;
            BreakpointLines = new HashSet<int>();
            UnboundBreakpointLines = new HashSet<int>();
            Version = 1;
            if (string.IsNullOrEmpty(path)) Session.SetUntitled(virtualDisplayName);
            else Session.SetLoaded(path);
        }

        public string Id { get; }

        public DocumentSession Session { get; }

        public string Source { get; private set; }

        public string DisplayName => Session.DisplayName;

        public string FilePath => Session.CurrentPath;

        public bool IsVirtual => !Session.HasPath;

        public string SourceText => Source;

        public bool IsDirty => Session.IsDirty;

        public long Version { get; private set; }

        public ISet<int> BreakpointLines { get; }

        public ISet<int> UnboundBreakpointLines { get; }

        public void UpdateSource(string source, bool markDirty = true)
        {
            string next = source ?? string.Empty;
            bool sourceChanged = !string.Equals(Source, next, StringComparison.Ordinal);
            if (!sourceChanged && (!markDirty || Session.IsDirty)) return;
            Source = next;
            if (sourceChanged) Version++;
            if (markDirty) Session.MarkDirty();
            Changed?.Invoke(this, new DdfEditorDocumentChangedEventArgs(this, sourceChanged));
        }

        public void MarkSaved(string path)
        {
            Session.MarkSaved(path);
            Changed?.Invoke(this, new DdfEditorDocumentChangedEventArgs(this, false));
        }

        public void MarkPersisted()
        {
            Session.MarkPersisted();
        }

        internal event EventHandler<DdfEditorDocumentChangedEventArgs> Changed;
    }

    public sealed class OpenDocumentCollection : IDdfEditorDocumentService
    {
        private readonly List<OpenDocumentBuffer> documents = new List<OpenDocumentBuffer>();
        private int nextId;

        public IReadOnlyList<OpenDocumentBuffer> Documents => documents;

        public IReadOnlyList<IDdfEditorDocument> OpenDocuments => documents;

        public OpenDocumentBuffer ActiveDocument { get; private set; }

        public IDdfEditorDocument ActiveDocumentInfo => ActiveDocument;

        public event EventHandler<DdfEditorDocumentEventArgs> DocumentAdded;
        public event EventHandler<DdfEditorDocumentEventArgs> ActiveDocumentChanged;
        public event EventHandler<DdfEditorDocumentChangedEventArgs> DocumentChanged;
        public event EventHandler<DdfEditorDocumentEventArgs> DocumentClosed;

        public OpenDocumentBuffer CreateUntitled()
        {
            return Add(null, string.Empty);
        }

        public IDdfEditorDocument CreateVirtual(string displayName = null, string sourceText = null)
        {
            return Add(null, sourceText ?? string.Empty, displayName);
        }

        public OpenDocumentBuffer Open(string path, string source)
        {
            string normalizedPath = DdfDocumentFile.NormalizePath(path);
            OpenDocumentBuffer existing = FindByPath(normalizedPath);
            if (existing != null)
            {
                Activate(existing.Id);
                return existing;
            }

            return Add(normalizedPath, source);
        }

        public IDdfEditorDocument OpenDocument(string path, string sourceText) => Open(path, sourceText);

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
            if (ReferenceEquals(ActiveDocument, document)) return true;
            ActiveDocument = document;
            ActiveDocumentChanged?.Invoke(this, new DdfEditorDocumentEventArgs(document));
            return true;
        }

        public bool ActivateDocument(string documentId) => Activate(documentId);

        public bool UpdateDocument(string documentId, string sourceText, bool markDirty = true)
        {
            OpenDocumentBuffer document = FindById(documentId);
            if (document == null) return false;
            document.UpdateSource(sourceText, markDirty);
            return true;
        }

        public bool MarkDocumentSaved(string documentId, string path)
        {
            OpenDocumentBuffer document = FindById(documentId);
            if (document == null) return false;
            document.MarkSaved(path);
            return true;
        }

        public bool TryCloseDocument(string documentId, bool discardUnsavedChanges = false)
        {
            OpenDocumentBuffer document = FindById(documentId);
            if (document == null || (document.Session.IsDirty && !discardUnsavedChanges)) return false;
            return Remove(documentId);
        }

        public bool Remove(string id)
        {
            int index = documents.FindIndex(item => item.Id == id);
            if (index < 0) return false;
            OpenDocumentBuffer removed = documents[index];
            documents.RemoveAt(index);
            removed.Changed -= document_Changed;
            bool activeChanged = ReferenceEquals(ActiveDocument, removed);
            if (ReferenceEquals(ActiveDocument, removed))
            {
                ActiveDocument = documents.Count == 0 ? null : documents[Math.Min(index, documents.Count - 1)];
            }
            DocumentClosed?.Invoke(this, new DdfEditorDocumentEventArgs(removed));
            if (activeChanged)
                ActiveDocumentChanged?.Invoke(this, new DdfEditorDocumentEventArgs(ActiveDocument));
            return true;
        }

        private OpenDocumentBuffer FindById(string id) =>
            documents.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

        private OpenDocumentBuffer Add(string path, string source, string virtualDisplayName = null)
        {
            var document = new OpenDocumentBuffer("document-" + (++nextId), path, source, virtualDisplayName);
            documents.Add(document);
            document.Changed += document_Changed;
            ActiveDocument = document;
            DocumentAdded?.Invoke(this, new DdfEditorDocumentEventArgs(document));
            ActiveDocumentChanged?.Invoke(this, new DdfEditorDocumentEventArgs(document));
            return document;
        }

        private void document_Changed(object sender, DdfEditorDocumentChangedEventArgs e) =>
            DocumentChanged?.Invoke(this, e);
    }
}
