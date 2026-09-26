using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Immutable project payload exchanged with a storage container supplied by
    /// the host. The SDK assigns no meaning to the container or its location.
    /// </summary>
    public sealed class DdfEditorProjectState
    {
        public DdfEditorProjectState(string projectId, string displayName,
            string languagePackageId, string languagePackageVersion,
            DdfEditorPersistedState editorState,
            IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            ProjectId = RequireText(projectId, nameof(projectId), 256);
            DisplayName = RequireText(displayName, nameof(displayName), 256);
            LanguagePackageId = RequireText(languagePackageId,
                nameof(languagePackageId), 256);
            LanguagePackageVersion = RequireText(languagePackageVersion,
                nameof(languagePackageVersion), 64);
            EditorState = editorState ?? throw new ArgumentNullException(nameof(editorState));

            var copiedMetadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> item in metadata ??
                Array.Empty<KeyValuePair<string, string>>())
            {
                string key = RequireText(item.Key, nameof(metadata), 256);
                if (copiedMetadata.ContainsKey(key))
                    throw new ArgumentException(
                        "I metadati contengono una chiave duplicata: " + key + ".",
                        nameof(metadata));
                copiedMetadata.Add(key, item.Value ?? string.Empty);
            }
            Metadata = new ReadOnlyDictionary<string, string>(copiedMetadata);
        }

        public string ProjectId { get; }
        public string DisplayName { get; }
        public string LanguagePackageId { get; }
        public string LanguagePackageVersion { get; }
        public DdfEditorPersistedState EditorState { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        private static string RequireText(string value, string parameterName,
            int maximumLength)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0)
                throw new ArgumentException("Il valore è obbligatorio.", parameterName);
            if (result.Length > maximumLength)
                throw new ArgumentException("Il valore supera " + maximumLength +
                    " caratteri.", parameterName);
            return result;
        }
    }

    /// <summary>
    /// Represents one project container selected by the host. Implementations may
    /// use a file, archive, database record, cloud object or any other storage.
    /// </summary>
    public interface IDdfEditorProjectContainer
    {
        void Save(DdfEditorProjectState project);
        DdfEditorProjectState Load();
    }

    public sealed class DdfInMemoryEditorProjectContainer : IDdfEditorProjectContainer
    {
        public DdfEditorProjectState Project { get; private set; }

        public void Save(DdfEditorProjectState project)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public DdfEditorProjectState Load() => Project;
    }

    public sealed class DdfEditorProjectEventArgs : EventArgs
    {
        public DdfEditorProjectEventArgs(DdfEditorProjectState project)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public DdfEditorProjectState Project { get; }
    }
}
