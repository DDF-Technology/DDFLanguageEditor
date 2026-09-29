using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    [Flags]
    public enum DdfEmbeddedModuleStoreOperations
    {
        None = 0,
        BrowseAndValidate = 1,
        Install = 2,
        Remove = 4,
        CreatePackage = 8,
        All = BrowseAndValidate | Install | Remove | CreatePackage
    }

    /// <summary>
    /// Explicit, revocable authorization supplied by an embedding host for one
    /// project-local module store. Without this capability the embedded shell
    /// does not expose module-store commands.
    /// </summary>
    public sealed class DdfEmbeddedModuleStoreCapability
    {
        public DdfEmbeddedModuleStoreCapability(string projectRoot,
            DdfEmbeddedModuleStoreOperations operations)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
                throw new ArgumentException("È richiesta una radice progetto assoluta.", nameof(projectRoot));
            string fullPath = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException("La radice progetto autorizzata non esiste: " + fullPath);
            if (operations == DdfEmbeddedModuleStoreOperations.None ||
                (operations & ~DdfEmbeddedModuleStoreOperations.All) != 0 ||
                (operations & DdfEmbeddedModuleStoreOperations.BrowseAndValidate) == 0)
                throw new ArgumentOutOfRangeException(nameof(operations),
                    "Ogni capability deve includere BrowseAndValidate e soltanto operazioni note.");
            ProjectRoot = fullPath;
            Operations = operations;
        }

        public string ProjectRoot { get; }
        public DdfEmbeddedModuleStoreOperations Operations { get; }

        public bool Allows(DdfEmbeddedModuleStoreOperations operation) =>
            operation != DdfEmbeddedModuleStoreOperations.None &&
            (operation & ~DdfEmbeddedModuleStoreOperations.All) == 0 &&
            (Operations & operation) == operation;
    }

    public sealed class DdfEditorHostOptions
    {
        public DdfEditorHostOptions(string fontFamily = EditorSettings.DefaultFontFamily,
            float fontSize = EditorSettings.DefaultFontSize,
            int zoomPercent = EditorSettings.DefaultZoomPercent,
            EditorTheme theme = EditorTheme.Dark,
            bool wordWrap = false,
            bool useTabs = false,
            int indentSize = EditorSettings.DefaultIndentSize)
        {
            if (string.IsNullOrWhiteSpace(fontFamily)) throw new ArgumentException("È richiesto un font.", nameof(fontFamily));
            if (fontSize < 8F || fontSize > 32F) throw new ArgumentOutOfRangeException(nameof(fontSize));
            if (zoomPercent < 50 || zoomPercent > 200) throw new ArgumentOutOfRangeException(nameof(zoomPercent));
            if (indentSize < 1 || indentSize > 8) throw new ArgumentOutOfRangeException(nameof(indentSize));
            FontFamily = fontFamily.Trim();
            FontSize = fontSize;
            ZoomPercent = zoomPercent;
            Theme = theme;
            WordWrap = wordWrap;
            UseTabs = useTabs;
            IndentSize = indentSize;
        }

        public string FontFamily { get; }
        public float FontSize { get; }
        public int ZoomPercent { get; }
        public EditorTheme Theme { get; }
        public bool WordWrap { get; }
        public bool UseTabs { get; }
        public int IndentSize { get; }
    }

    public enum DdfEditorHostConfigurationChangeKind
    {
        Options,
        Shortcut
    }

    public sealed class DdfEditorHostConfigurationChangedEventArgs : EventArgs
    {
        public DdfEditorHostConfigurationChangedEventArgs(
            DdfEditorHostConfigurationChangeKind changeKind, string commandId = null)
        {
            ChangeKind = changeKind;
            CommandId = commandId;
        }

        public DdfEditorHostConfigurationChangeKind ChangeKind { get; }
        public string CommandId { get; }
    }

    public interface IDdfEditorHostConfiguration
    {
        DdfEditorHostOptions Options { get; }
        IReadOnlyDictionary<string, Keys> Shortcuts { get; }
        event EventHandler<DdfEditorHostConfigurationChangedEventArgs> ConfigurationChanged;

        void ApplyOptions(DdfEditorHostOptions options);
        bool SetShortcut(string commandId, Keys shortcut);
        void ReplaceShortcuts(IEnumerable<KeyValuePair<string, Keys>> shortcuts);
    }

    /// <summary>
    /// Per-instance configuration with no file-system or profile side effects.
    /// Persistence is deliberately delegated to the embedding host.
    /// </summary>
    public sealed class DdfEditorHostConfiguration : IDdfEditorHostConfiguration
    {
        private readonly Dictionary<string, Keys> shortcuts =
            new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);

        public DdfEditorHostConfiguration(DdfEditorHostOptions options = null)
        {
            Options = options ?? new DdfEditorHostOptions();
        }

        public DdfEditorHostOptions Options { get; private set; }

        public IReadOnlyDictionary<string, Keys> Shortcuts =>
            new ReadOnlyDictionary<string, Keys>(shortcuts);

        public event EventHandler<DdfEditorHostConfigurationChangedEventArgs> ConfigurationChanged;

        public void ApplyOptions(DdfEditorHostOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            ConfigurationChanged?.Invoke(this, new DdfEditorHostConfigurationChangedEventArgs(
                DdfEditorHostConfigurationChangeKind.Options));
        }

        public bool SetShortcut(string commandId, Keys shortcut)
        {
            if (string.IsNullOrWhiteSpace(commandId) || !ShortcutSettings.IsBindable(shortcut)) return false;
            string id = commandId.Trim();
            if (shortcuts.TryGetValue(id, out Keys current) && current == shortcut) return true;
            if (shortcut != Keys.None && shortcuts.Any(item =>
                !string.Equals(item.Key, id, StringComparison.OrdinalIgnoreCase) && item.Value == shortcut))
                return false;
            shortcuts[id] = shortcut;
            ConfigurationChanged?.Invoke(this, new DdfEditorHostConfigurationChangedEventArgs(
                DdfEditorHostConfigurationChangeKind.Shortcut, id));
            return true;
        }

        public void ReplaceShortcuts(IEnumerable<KeyValuePair<string, Keys>> values)
        {
            var replacement = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Keys> item in values ?? Array.Empty<KeyValuePair<string, Keys>>())
            {
                if (string.IsNullOrWhiteSpace(item.Key) || !ShortcutSettings.IsBindable(item.Value))
                    throw new ArgumentException("La raccolta contiene una scorciatoia non valida.", nameof(values));
                string id = item.Key.Trim();
                if (item.Value != Keys.None && replacement.Any(existing => existing.Value == item.Value))
                    throw new ArgumentException("La raccolta contiene scorciatoie duplicate.", nameof(values));
                replacement[id] = item.Value;
            }
            shortcuts.Clear();
            foreach (KeyValuePair<string, Keys> item in replacement) shortcuts.Add(item.Key, item.Value);
            ConfigurationChanged?.Invoke(this, new DdfEditorHostConfigurationChangedEventArgs(
                DdfEditorHostConfigurationChangeKind.Shortcut));
        }
    }
}
