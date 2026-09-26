using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class DdfEditorPersistedDocument
    {
        public DdfEditorPersistedDocument(string displayName, string filePath,
            string sourceText, bool isDirty)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Senza titolo.ddf"
                : displayName.Trim();
            FilePath = filePath ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            IsDirty = isDirty;
        }

        public string DisplayName { get; }
        public string FilePath { get; }
        public string SourceText { get; }
        public bool IsDirty { get; }
    }

    public sealed class DdfEditorPersistedPreferences
    {
        public DdfEditorPersistedPreferences(DdfEditorHostOptions options,
            IEnumerable<KeyValuePair<string, Keys>> shortcuts,
            bool educationalModeEnabled)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            var validated = new DdfEditorHostConfiguration(options);
            validated.ReplaceShortcuts(shortcuts);
            Shortcuts = new ReadOnlyDictionary<string, Keys>(
                validated.Shortcuts.ToDictionary(item => item.Key, item => item.Value,
                    StringComparer.OrdinalIgnoreCase));
            EducationalModeEnabled = educationalModeEnabled;
        }

        public DdfEditorHostOptions Options { get; }
        public IReadOnlyDictionary<string, Keys> Shortcuts { get; }
        public bool EducationalModeEnabled { get; }
    }

    public sealed class DdfEditorPersistedState
    {
        public DdfEditorPersistedState(
            IEnumerable<DdfEditorPersistedDocument> documents,
            int activeDocumentIndex,
            DdfEditorPersistedPreferences preferences,
            DdfEditorPersistedDebugState debugging = null)
        {
            var copiedDocuments = (documents ?? Array.Empty<DdfEditorPersistedDocument>())
                .Select(document => document ?? throw new ArgumentException(
                    "La raccolta contiene un documento nullo.", nameof(documents)))
                .ToList();
            if ((copiedDocuments.Count == 0 && activeDocumentIndex != -1) ||
                (copiedDocuments.Count > 0 &&
                 (activeDocumentIndex < 0 || activeDocumentIndex >= copiedDocuments.Count)))
                throw new ArgumentOutOfRangeException(nameof(activeDocumentIndex));
            Documents = copiedDocuments.AsReadOnly();
            ActiveDocumentIndex = activeDocumentIndex;
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            Debugging = debugging ?? new DdfEditorPersistedDebugState();
            if (Debugging.Breakpoints.Any(item => item.DocumentIndex >= copiedDocuments.Count))
                throw new ArgumentException(
                    "Un breakpoint fa riferimento a un documento non presente nello snapshot.",
                    nameof(debugging));
        }

        public IReadOnlyList<DdfEditorPersistedDocument> Documents { get; }
        public int ActiveDocumentIndex { get; }
        public DdfEditorPersistedPreferences Preferences { get; }
        public DdfEditorPersistedDebugState Debugging { get; }
    }

    public sealed class DdfEditorPersistedBreakpoint
    {
        public DdfEditorPersistedBreakpoint(int documentIndex, int line,
            bool enabled = true)
        {
            if (documentIndex < 0) throw new ArgumentOutOfRangeException(nameof(documentIndex));
            if (line <= 0) throw new ArgumentOutOfRangeException(nameof(line));
            DocumentIndex = documentIndex;
            Line = line;
            Enabled = enabled;
        }

        public int DocumentIndex { get; }
        public int Line { get; }
        public bool Enabled { get; }
    }

    public sealed class DdfEditorPersistedDebugState
    {
        public DdfEditorPersistedDebugState(
            IEnumerable<DdfEditorPersistedBreakpoint> breakpoints = null,
            IEnumerable<string> watchExpressions = null)
        {
            Breakpoints = (breakpoints ?? Array.Empty<DdfEditorPersistedBreakpoint>())
                .Select(item => item ?? throw new ArgumentException(
                    "La raccolta contiene un breakpoint nullo.", nameof(breakpoints)))
                .ToList().AsReadOnly();
            var expressions = new List<string>();
            foreach (string expression in watchExpressions ?? Array.Empty<string>())
            {
                string value = (expression ?? string.Empty).Trim();
                if (value.Length == 0) continue;
                if (value.Length > 512)
                    throw new ArgumentException(
                        "Ogni espressione Watch può contenere al massimo 512 caratteri.",
                        nameof(watchExpressions));
                if (!expressions.Contains(value, StringComparer.Ordinal))
                    expressions.Add(value);
            }
            WatchExpressions = expressions.AsReadOnly();
        }

        public IReadOnlyList<DdfEditorPersistedBreakpoint> Breakpoints { get; }
        public IReadOnlyList<string> WatchExpressions { get; }
    }

    public interface IDdfEditorPersistenceProvider
    {
        void Save(DdfEditorPersistedState state);
        DdfEditorPersistedState Load();
    }

    /// <summary>
    /// Minimal provider useful to hosts that retain editor state in an existing
    /// session object. It performs no serialization and no file-system access.
    /// </summary>
    public sealed class DdfInMemoryEditorPersistenceProvider : IDdfEditorPersistenceProvider
    {
        public DdfEditorPersistedState State { get; private set; }

        public void Save(DdfEditorPersistedState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public DdfEditorPersistedState Load() => State;
    }
}
