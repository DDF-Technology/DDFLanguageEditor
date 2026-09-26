using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Read-only identity and state exposed to an editor host. Text changes must be
    /// performed through <see cref="IDdfEditorDocumentService"/>.
    /// </summary>
    public interface IDdfEditorDocument
    {
        string Id { get; }
        string DisplayName { get; }
        string FilePath { get; }
        bool IsVirtual { get; }
        string SourceText { get; }
        bool IsDirty { get; }
        long Version { get; }
    }

    /// <summary>
    /// Host-facing, UI-independent document lifecycle. Implementations never write
    /// files implicitly and refuse to close dirty documents unless explicitly told
    /// to discard their changes.
    /// </summary>
    public interface IDdfEditorDocumentService
    {
        IReadOnlyList<IDdfEditorDocument> OpenDocuments { get; }
        IDdfEditorDocument ActiveDocumentInfo { get; }

        event EventHandler<DdfEditorDocumentEventArgs> DocumentAdded;
        event EventHandler<DdfEditorDocumentEventArgs> ActiveDocumentChanged;
        event EventHandler<DdfEditorDocumentChangedEventArgs> DocumentChanged;
        event EventHandler<DdfEditorDocumentEventArgs> DocumentClosed;

        IDdfEditorDocument CreateVirtual(string displayName = null, string sourceText = null);
        IDdfEditorDocument OpenDocument(string path, string sourceText);
        bool ActivateDocument(string documentId);
        bool UpdateDocument(string documentId, string sourceText, bool markDirty = true);
        bool MarkDocumentSaved(string documentId, string path);
        bool TryCloseDocument(string documentId, bool discardUnsavedChanges = false);
    }

    public class DdfEditorDocumentEventArgs : EventArgs
    {
        public DdfEditorDocumentEventArgs(IDdfEditorDocument document)
        {
            Document = document;
        }

        public IDdfEditorDocument Document { get; }
    }

    public sealed class DdfEditorDocumentChangedEventArgs : DdfEditorDocumentEventArgs
    {
        public DdfEditorDocumentChangedEventArgs(IDdfEditorDocument document, bool sourceChanged)
            : base(document)
        {
            SourceChanged = sourceChanged;
        }

        public bool SourceChanged { get; }
    }
}
