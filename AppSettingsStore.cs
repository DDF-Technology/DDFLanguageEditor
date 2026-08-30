using System;
using System.IO;
using System.Text;

namespace DDF___Program_Language_Editor
{
    internal static class AppSettingsStore
    {
        private const string SettingsFileName = "recent-files.txt";

        public static string LoadRecentFiles()
        {
            try
            {
                string path = GetSettingsPath();
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
            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value ?? string.Empty, new UTF8Encoding(false));
        }

        private static string GetSettingsPath()
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localData, "DDF.Technology", "DDFLanguageEditor", SettingsFileName);
        }
    }
}
