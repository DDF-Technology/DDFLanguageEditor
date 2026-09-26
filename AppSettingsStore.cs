using System;
using System.IO;
using System.Text;

namespace DDF___Program_Language_Editor
{
    internal static class AppSettingsStore
    {
        private const string RecentFilesSettingsFileName = "recent-files.txt";
        private const string RecentWorkspacesSettingsFileName = "recent-workspaces.txt";
        private const string EditorSettingsFileName = "editor-settings.txt";
        private const string PaletteLayoutSettingsFileName = "palette-layout.txt";
        private const string ToolbarLayoutSettingsFileName = "toolbar-layout.txt";
        private const string ShortcutSettingsFileName = "shortcut-settings.txt";
        private const string LanguagePackageSettingsFileName = "language-package.txt";
        private const string HotExitSnapshotFileName = "hot-exit.json";
        private const string WorkspaceSessionFileName = "workspace-session.json";
        private const string CrashRecoveryFileName = "crash-recovery.json";
        private const string WatchExpressionsFileName = "watch-expressions.json";
        private const string EducationalModeSettingsFileName = "educational-mode.txt";

        public static string LoadRecentFiles()
        {
            try
            {
                string path = GetSettingsPath(RecentFilesSettingsFileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return string.Empty;
            }
        }

        public static void SaveRecentFiles(string value)
        {
            Save(RecentFilesSettingsFileName, value);
        }

        public static string LoadRecentWorkspaces()
        {
            try
            {
                string path = GetSettingsPath(RecentWorkspacesSettingsFileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return string.Empty;
            }
        }

        public static void SaveRecentWorkspaces(string value)
        {
            Save(RecentWorkspacesSettingsFileName, value);
        }

        public static string LoadEditorSettings()
        {
            return Load(EditorSettingsFileName);
        }

        public static void SaveEditorSettings(string value)
        {
            Save(EditorSettingsFileName, value);
        }

        public static string LoadPaletteLayout()
        {
            return Load(PaletteLayoutSettingsFileName);
        }

        public static void SavePaletteLayout(string value)
        {
            Save(PaletteLayoutSettingsFileName, value);
        }

        public static string LoadToolbarLayout()
        {
            return Load(ToolbarLayoutSettingsFileName);
        }

        public static void SaveToolbarLayout(string value)
        {
            Save(ToolbarLayoutSettingsFileName, value);
        }

        public static string LoadShortcutSettings()
        {
            return Load(ShortcutSettingsFileName);
        }

        public static void SaveShortcutSettings(string value)
        {
            Save(ShortcutSettingsFileName, value);
        }

        public static string LoadLanguagePackagePath()
        {
            return Load(LanguagePackageSettingsFileName);
        }

        public static void SaveLanguagePackagePath(string value)
        {
            Save(LanguagePackageSettingsFileName, value ?? string.Empty);
        }

        public static string GetLanguagePackagesDirectory()
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localData, "DDF.Technology", "DDFLanguageEditor", "language-packages");
        }

        public static string LoadHotExitSnapshot()
        {
            return Load(HotExitSnapshotFileName);
        }

        public static void SaveHotExitSnapshot(string value)
        {
            Save(HotExitSnapshotFileName, value);
        }

        public static void ClearHotExitSnapshot()
        {
            string path = GetSettingsPath(HotExitSnapshotFileName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static string LoadWorkspaceSession()
        {
            return Load(WorkspaceSessionFileName);
        }

        public static void SaveWorkspaceSession(string value)
        {
            Save(WorkspaceSessionFileName, value);
        }

        public static void ClearWorkspaceSession()
        {
            string path = GetSettingsPath(WorkspaceSessionFileName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static string LoadCrashRecovery()
        {
            return Load(CrashRecoveryFileName);
        }

        public static void SaveCrashRecovery(string value)
        {
            Save(CrashRecoveryFileName, value);
        }

        public static void ClearCrashRecovery()
        {
            string path = GetSettingsPath(CrashRecoveryFileName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static string LoadWatchExpressions()
        {
            return Load(WatchExpressionsFileName);
        }

        public static void SaveWatchExpressions(string value)
        {
            Save(WatchExpressionsFileName, value);
        }

        public static string LoadEducationalMode()
        {
            return Load(EducationalModeSettingsFileName);
        }

        public static void SaveEducationalMode(string value)
        {
            Save(EducationalModeSettingsFileName, value);
        }

        private static string Load(string fileName)
        {
            try
            {
                string path = GetSettingsPath(fileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return string.Empty;
            }
        }

        private static void Save(string fileName, string value)
        {
            string path = GetSettingsPath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, value ?? string.Empty, new UTF8Encoding(false));
                if (File.Exists(path)) File.Copy(temporaryPath, path, true);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string GetSettingsPath(string fileName)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localData, "DDF.Technology", "DDFLanguageEditor", fileName);
        }
    }
}
