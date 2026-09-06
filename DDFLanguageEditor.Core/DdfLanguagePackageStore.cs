using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfInstalledLanguagePackage
    {
        internal DdfInstalledLanguagePackage(string id, string name, string version, string languageVersion, string path)
        { Id = id; Name = name; Version = version; LanguageVersion = languageVersion; Path = path; }
        public string Id { get; }
        public string Name { get; }
        public string Version { get; }
        public string LanguageVersion { get; }
        public string Path { get; }
        public override string ToString() => Name + " " + Version;
    }

    public sealed class DdfLanguagePackageInstallResult
    {
        internal DdfLanguagePackageInstallResult(DdfInstalledLanguagePackage installed,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics, bool updated)
        { Installed = installed; Diagnostics = diagnostics.ToList().AsReadOnly(); Updated = updated; }
        public DdfInstalledLanguagePackage Installed { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Updated { get; }
        public bool Success => Installed != null;
    }

    public sealed class DdfLanguagePackageStore
    {
        private readonly string root;
        public DdfLanguagePackageStore(string rootPath)
        { root = Path.GetFullPath(rootPath ?? throw new ArgumentNullException(nameof(rootPath))); }
        public string RootPath => root;

        public IReadOnlyList<DdfInstalledLanguagePackage> List()
        {
            if (!Directory.Exists(root)) return Array.Empty<DdfInstalledLanguagePackage>();
            var result = new List<DdfInstalledLanguagePackage>();
            foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .Where(item => File.Exists(Path.Combine(item, "manifest.json"))))
            {
                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(directory);
                if (load.Success) result.Add(ToInstalled(load.Package, directory));
            }
            return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => Version.Parse(item.Version)).ToList().AsReadOnly();
        }

        public DdfLanguagePackageInstallResult Install(string sourcePath)
        {
            DdfLanguagePackageLoadResult validation = DdfLanguagePackageLoader.Load(sourcePath);
            if (!validation.Success)
                return new DdfLanguagePackageInstallResult(null, validation.Diagnostics, false);

            DdfLanguagePackage package = validation.Package;
            string destination = Path.Combine(root, package.Id, package.PackageVersion);
            string staging = destination + ".installing-" + Guid.NewGuid().ToString("N");
            string backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
            bool updated = Directory.Exists(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            try
            {
                Directory.CreateDirectory(staging);
                CopyPackage(sourcePath, staging);
                DdfLanguagePackageLoadResult staged = DdfLanguagePackageLoader.Load(staging);
                if (!staged.Success)
                    return new DdfLanguagePackageInstallResult(null, staged.Diagnostics, updated);
                if (updated) Directory.Move(destination, backup);
                Directory.Move(staging, destination);
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
                return new DdfLanguagePackageInstallResult(ToInstalled(staged.Package, destination), staged.Diagnostics, updated);
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (Directory.Exists(backup))
                {
                    if (!Directory.Exists(destination)) Directory.Move(backup, destination);
                    else Directory.Delete(backup, true);
                }
            }
        }

        private static void CopyPackage(string sourcePath, string destination)
        {
            if (Directory.Exists(sourcePath))
            {
                foreach (string path in Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(path);
                    if (IsPackageFile(name)) File.Copy(path, Path.Combine(destination, name), true);
                }
                return;
            }
            using ZipArchive archive = ZipFile.OpenRead(sourcePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!IsPackageFile(entry.FullName) || entry.FullName != Path.GetFileName(entry.FullName)) continue;
                using Stream input = entry.Open();
                using FileStream output = File.Create(Path.Combine(destination, entry.FullName));
                input.CopyTo(output);
            }
        }

        private static bool IsPackageFile(string name) => !string.IsNullOrWhiteSpace(name) &&
            name == Path.GetFileName(name) &&
            (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ebnf", StringComparison.OrdinalIgnoreCase));

        private static DdfInstalledLanguagePackage ToInstalled(DdfLanguagePackage package, string path) =>
            new DdfInstalledLanguagePackage(package.Id, package.Name, package.PackageVersion,
                package.LanguageVersion, Path.GetFullPath(path));
    }
}
