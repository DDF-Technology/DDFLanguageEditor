using System;
using System.IO;

namespace DDFLanguageEditor.Core
{
    public sealed class DocumentSession
    {
        public string CurrentPath { get; private set; }

        public bool IsDirty { get; private set; }

        public bool HasPath => !string.IsNullOrEmpty(CurrentPath);

        public string DisplayName => HasPath ? Path.GetFileName(CurrentPath) : "Senza titolo.ddf";

        public void MarkDirty()
        {
            IsDirty = true;
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

        public void SetUntitled()
        {
            CurrentPath = null;
            IsDirty = false;
        }
    }
}
