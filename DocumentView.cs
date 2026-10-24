using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Keeps the shell-specific view state associated with one public document.
    /// This adapter is intentionally outside MainForm so document identity and UI
    /// state can evolve without becoming fields of the application shell.
    /// </summary>
    internal sealed class DocumentView
    {
        public DocumentView(OpenDocumentBuffer buffer, RichTextBox editor, TabPage tab)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Tab = tab ?? throw new ArgumentNullException(nameof(tab));
        }

        public OpenDocumentBuffer Buffer { get; }
        public RichTextBox Editor { get; }
        public TabPage Tab { get; }
        public Stack<Tuple<int, int>> SelectionHistory { get; } = new Stack<Tuple<int, int>>();
        public List<DdfTextRange> MultiSelections { get; } = new List<DdfTextRange>();
        public int? LastEditPosition { get; set; }
        public DateTime? LastEditUtc { get; set; }
        public Point ScrollPosition { get; set; }
        public bool HasScrollPosition { get; set; }
        public string SyntaxFormattedSource { get; set; } = string.Empty;
        public long SyntaxFormattedLanguageRevision { get; set; } = -1;
        public string SyntaxFormattingStyle { get; set; } = string.Empty;
        public bool HasDiagnosticDecorations { get; set; }
    }

    /// <summary>
    /// Maps public document identities to the editor and tab owned by the standalone
    /// shell. No document content is duplicated here; the document service remains
    /// the single source of truth.
    /// </summary>
    internal sealed class DdfDocumentViewCoordinator
    {
        private readonly OpenDocumentCollection documents;
        private readonly Dictionary<string, DocumentView> views =
            new Dictionary<string, DocumentView>(StringComparer.Ordinal);

        public DdfDocumentViewCoordinator(OpenDocumentCollection documents)
        {
            this.documents = documents ?? throw new ArgumentNullException(nameof(documents));
        }

        public DocumentView ActiveView => documents.ActiveDocument == null
            ? null
            : this[documents.ActiveDocument.Id];

        public ICollection<DocumentView> Values => views.Values;

        public DocumentView this[string documentId] => views[documentId];

        public DocumentView Register(OpenDocumentBuffer buffer, RichTextBox editor, TabPage tab)
        {
            if (views.ContainsKey(buffer.Id))
                throw new InvalidOperationException("Il documento è già associato a una vista: " + buffer.Id);
            var view = new DocumentView(buffer, editor, tab);
            views.Add(buffer.Id, view);
            return view;
        }

        public bool Unregister(string documentId) => views.Remove(documentId);

        public bool TryGetValue(string documentId, out DocumentView view) =>
            views.TryGetValue(documentId, out view);

        public bool ContainsKey(string documentId) => views.ContainsKey(documentId);

        public DocumentView FindByEditor(RichTextBox editor) =>
            views.Values.FirstOrDefault(view => ReferenceEquals(view.Editor, editor));
    }
}
