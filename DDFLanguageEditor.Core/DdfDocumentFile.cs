using System;
using System.IO;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public static class DdfDocumentFile
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

        public static string Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(NormalizePath(path));
            int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                ? 3
                : 0;
            return Utf8WithoutBom.GetString(bytes, offset, bytes.Length - offset);
        }

        public static void Save(string path, string content)
        {
            string targetPath = NormalizePath(path);
            string directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("The document path must include a directory.", nameof(path));
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(temporaryPath, content ?? string.Empty, Utf8WithoutBom);
                if (File.Exists(targetPath))
                {
                    File.Copy(temporaryPath, targetPath, true);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A document path is required.", nameof(path));
            }

            return Path.GetFullPath(path);
        }
    }
}
