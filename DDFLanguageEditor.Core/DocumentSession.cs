using System;
using System.IO;

namespace DDFLanguageEditor.Core
{
    public sealed class DocumentSession
    {
        public string CurrentPath { get; private set; }

        public bool IsDirty { get; private set; }

        public bool HasPath => !string.IsNullOrEmpty(CurrentPath);

        public string DisplayName => HasPath ? Path.GetFileName(CurrentPath) : UntitledDisplayName;

        public string UntitledDisplayName { get; private set; } = "Senza titolo.ddf";

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void MarkPersisted()
        {
            IsDirty = false;
        }

        public void MarkSaved(string path)
        {
            CurrentPath = DdfDocumentFile.NormalizePath(path);
            IsDirty = false;
        }

        public void SetLoaded(string path)
        {
            MarkSaved(path);
        }

        public void SetUntitled(string displayName = null)
        {
            CurrentPath = null;
            IsDirty = false;
            UntitledDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Senza titolo.ddf"
                : displayName.Trim();
        }
    }
}
