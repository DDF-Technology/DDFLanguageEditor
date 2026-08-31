using System;
using System.IO;
using System.Text;

namespace DDF___Program_Language_Editor
{
    internal static class AppSettingsStore
    {
        private const string RecentFilesSettingsFileName = "recent-files.txt";
        private const string RecentWorkspacesSettingsFileName = "recent-workspaces.txt";

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

        private static void Save(string fileName, string value)
        {
            string path = GetSettingsPath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value ?? string.Empty, new UTF8Encoding(false));
        }

        private static string GetSettingsPath(string fileName)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localData, "DDF.Technology", "DDFLanguageEditor", fileName);
        }
    }
}
