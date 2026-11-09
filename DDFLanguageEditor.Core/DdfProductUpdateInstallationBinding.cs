using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Read-only preflight for a self-contained standalone installation.
    /// It does not create directories or authorize a worker.
    /// </summary>
    public sealed class DdfProductUpdateInstallationBinding
    {
        private static readonly string[] RequiredFiles =
        {
            "DDFLanguageEditor.exe", "DDFLanguageEditor.dll",
            "DDFLanguageEditor.WinForms.dll", "DDFLanguageEditor.Core.dll",
            "DDFLanguageEditor.deps.json",
            "DDFLanguageEditor.runtimeconfig.json", "coreclr.dll",
            "hostfxr.dll", "hostpolicy.dll"
        };

        private DdfProductUpdateInstallationBinding(string targetRoot,
            string stagingRoot, string backupRoot, string installationId)
        {
            TargetRoot = targetRoot;
            StagingRoot = stagingRoot;
            BackupRoot = backupRoot;
            InstallationId = installationId;
        }

        public string TargetRoot { get; }
        public string StagingRoot { get; }
        public string BackupRoot { get; }
        public string InstallationId { get; }
        public string ApplicationExecutable => "DDFLanguageEditor.exe";

        public static DdfProductUpdateInstallationBinding Resolve(
            string processExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(processExecutablePath) ||
                !Path.IsPathFullyQualified(processExecutablePath))
                throw new InvalidDataException(
                    "Percorso dell'eseguibile standalone non disponibile.");
            string executable = Path.GetFullPath(processExecutablePath);
            if (!string.Equals(Path.GetFileName(executable),
                    "DDFLanguageEditor.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "L'applicazione non è avviata dall'EXE standalone.");
            string target = Path.TrimEndingDirectorySeparator(
                Path.GetDirectoryName(executable));
            string parent = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(parent) ||
                string.Equals(target, Path.GetPathRoot(target),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Directory di installazione non valida.");
            RejectProtectedRoot(target);
            for (string current = target; !string.IsNullOrEmpty(current);
                current = Path.GetDirectoryName(current))
            {
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "L'installazione attraversa un reparse point.");
                if (Directory.Exists(Path.Combine(current, ".git")) ||
                    File.Exists(Path.Combine(current, ".git")))
                    throw new InvalidDataException(
                        "Una copia nel repository di sviluppo non è aggiornabile.");
            }
            foreach (string name in RequiredFiles)
            {
                string file = Path.Combine(target, name);
                if (!File.Exists(file) ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "Distribuzione standalone incompleta: " + name + ".");
            }
            string languageParent = Path.Combine(target, "language-packages");
            string languageRoot = Path.Combine(languageParent, "ddf");
            if (!Directory.Exists(languageParent) ||
                (File.GetAttributes(languageParent) & FileAttributes.ReparsePoint) != 0 ||
                !Directory.Exists(languageRoot) ||
                (File.GetAttributes(languageRoot) & FileAttributes.ReparsePoint) != 0 ||
                !File.Exists(Path.Combine(languageRoot, "manifest.json")) ||
                (File.GetAttributes(Path.Combine(languageRoot, "manifest.json")) &
                    FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    "Pacchetto DDF incorporato assente dalla distribuzione.");

            string normalized = target.ToUpperInvariant();
            string identityHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
            string installationId = "portable-" + identityHash.Substring(0, 24);
            string suffix = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            string stem = ".ddf-update-" + suffix;
            string staging = Path.Combine(parent, stem + ".staging");
            string backup = Path.Combine(parent, stem + ".backup");
            if (Directory.Exists(staging) || File.Exists(staging) ||
                Directory.Exists(backup) || File.Exists(backup))
                throw new InvalidDataException(
                    "Percorso di staging o backup già presente.");
            return new DdfProductUpdateInstallationBinding(target, staging,
                backup, installationId);
        }

        private static void RejectProtectedRoot(string target)
        {
            string[] protectedRoots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            if (protectedRoots.Where(path => !string.IsNullOrWhiteSpace(path))
                .Any(path => target.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith(Path.TrimEndingDirectorySeparator(path) +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    "Installazioni di sistema o per tutti gli utenti non supportate.");
        }
    }
}
