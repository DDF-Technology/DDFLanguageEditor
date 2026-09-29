using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfModulePackageDiagnostic
    {
        internal DdfModulePackageDiagnostic(string code, string message)
        { Code = code; Message = message; }
        public string Code { get; }
        public string Message { get; }
        public override string ToString() => Code + ": " + Message;
    }

    public sealed class DdfInstalledModule
    {
        internal DdfInstalledModule(DdfProjectManifest manifest, string path)
        {
            Id = manifest.Module.Id;
            Name = manifest.Name;
            Version = manifest.Module.Version;
            LanguageId = manifest.LanguageId;
            LanguagePackageVersion = manifest.LanguagePackageVersion;
            Exports = manifest.Module.Exports.OrderBy(item => item, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            Path = System.IO.Path.GetFullPath(path);
        }

        public string Id { get; }
        public string Name { get; }
        public string Version { get; }
        public string LanguageId { get; }
        public string LanguagePackageVersion { get; }
        public IReadOnlyList<string> Exports { get; }
        public string Path { get; }
        public override string ToString() => Id + "@" + Version;
    }

    public sealed class DdfModulePackageResult
    {
        internal DdfModulePackageResult(DdfInstalledModule module, string packagePath,
            IEnumerable<DdfModulePackageDiagnostic> diagnostics, bool replaced)
        {
            Module = module;
            PackagePath = packagePath;
            Diagnostics = (diagnostics ?? Array.Empty<DdfModulePackageDiagnostic>()).ToList().AsReadOnly();
            Replaced = replaced;
        }

        public DdfInstalledModule Module { get; }
        public string PackagePath { get; }
        public IReadOnlyList<DdfModulePackageDiagnostic> Diagnostics { get; }
        public bool Replaced { get; }
        public bool Success => Module != null && Diagnostics.Count == 0;
    }

    /// <summary>
    /// Creates and manages versioned DDF source-module packages. Paths are storage details and
    /// never contribute to the module identity or symbol namespace.
    /// </summary>
    public sealed class DdfModulePackageStore
    {
        public const string PackageExtension = ".ddfmod";
        private const int MaximumFileCount = 4096;
        private const long MaximumFileLength = 16L * 1024 * 1024;
        private const long MaximumPackageLength = 64L * 1024 * 1024;
        private static readonly DateTimeOffset DeterministicTimestamp =
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private readonly string root;

        public DdfModulePackageStore(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentNullException(nameof(projectRoot));
            ProjectRoot = Path.GetFullPath(projectRoot);
            root = Path.GetFullPath(Path.Combine(ProjectRoot, ".ddf", "modules"));
        }

        public string ProjectRoot { get; }
        public string RootPath => root;

        public IReadOnlyList<DdfInstalledModule> List()
        {
            if (!Directory.Exists(root)) return Array.Empty<DdfInstalledModule>();
            var modules = new List<DdfInstalledModule>();
            foreach (string idDirectory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string versionDirectory in Directory.EnumerateDirectories(idDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(versionDirectory);
                    if (!loaded.Success ||
                        !string.Equals(loaded.Manifest.Module.Id, Path.GetFileName(idDirectory), StringComparison.Ordinal) ||
                        !string.Equals(loaded.Manifest.Module.Version, Path.GetFileName(versionDirectory), StringComparison.Ordinal))
                        continue;
                    modules.Add(new DdfInstalledModule(loaded.Manifest, versionDirectory));
                }
            }
            return modules.OrderBy(item => item.Id, StringComparer.Ordinal)
                .ThenByDescending(item => Version.Parse(item.Version)).ToList().AsReadOnly();
        }

        public DdfModulePackageResult Pack(string moduleRoot, string packagePath)
        {
            var diagnostics = new List<DdfModulePackageDiagnostic>();
            string sourceRoot;
            string destination;
            try
            {
                sourceRoot = Path.GetFullPath(moduleRoot ?? throw new ArgumentNullException(nameof(moduleRoot)));
                destination = Path.GetFullPath(packagePath ?? throw new ArgumentNullException(nameof(packagePath)));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            { return Failure("DDFM001", exception.Message, diagnostics); }

            if (!Directory.Exists(sourceRoot)) return Failure("DDFM001", "Cartella modulo non trovata.", diagnostics);
            if (!destination.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase))
                return Failure("DDFM002", "Il pacchetto deve usare l'estensione " + PackageExtension + ".", diagnostics);
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(sourceRoot);
            if (!loaded.Success) return Failure("DDFM003", Describe(loaded.Issues), diagnostics);

            IReadOnlyList<string> files;
            try { files = ResolvePayloadFiles(sourceRoot, loaded.Manifest); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            { return Failure("DDFM004", exception.Message, diagnostics); }
            if (files.Count == 1)
                return Failure("DDFM004", "Il manifesto non seleziona alcun sorgente esistente.", diagnostics);

            string directory = Path.GetDirectoryName(destination);
            string temporary = destination + ".creating-" + Guid.NewGuid().ToString("N");
            try
            {
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                using (ZipArchive archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    foreach (string file in files.OrderBy(item => Relative(sourceRoot, item), StringComparer.Ordinal))
                    {
                        string relative = Relative(sourceRoot, file);
                        var info = new FileInfo(file);
                        if (info.Length > MaximumFileLength)
                            throw new InvalidDataException("File troppo grande nel modulo: " + relative + ".");
                        ZipArchiveEntry entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                        entry.LastWriteTime = DeterministicTimestamp;
                        using Stream input = File.OpenRead(file);
                        using Stream output = entry.Open();
                        input.CopyTo(output);
                    }
                }
                File.Move(temporary, destination, true);
                return new DdfModulePackageResult(new DdfInstalledModule(loaded.Manifest, sourceRoot),
                    destination, diagnostics, false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            { return Failure("DDFM005", exception.Message, diagnostics); }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public DdfModulePackageResult Install(string sourcePath, bool replaceExisting = false)
        {
            var diagnostics = new List<DdfModulePackageDiagnostic>();
            string source;
            try { source = Path.GetFullPath(sourcePath ?? throw new ArgumentNullException(nameof(sourcePath))); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            { return Failure("DDFM001", exception.Message, diagnostics); }
            if (!Directory.Exists(source) && !File.Exists(source))
                return Failure("DDFM001", "Pacchetto o cartella modulo non trovati.", diagnostics);
            if (File.Exists(source) && !source.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase))
                return Failure("DDFM002", "Il pacchetto deve usare l'estensione " + PackageExtension + ".", diagnostics);

            Directory.CreateDirectory(root);
            string staging = Path.Combine(root, ".installing-" + Guid.NewGuid().ToString("N"));
            string backup = null;
            string destination = null;
            bool replaced = false;
            try
            {
                Directory.CreateDirectory(staging);
                if (Directory.Exists(source)) CopyDirectoryPayload(source, staging);
                else ExtractArchive(source, staging);

                DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(staging);
                if (!loaded.Success) return Failure("DDFM003", Describe(loaded.Issues), diagnostics);
                ValidatePayloadSources(staging, loaded.Manifest);
                destination = Path.Combine(root, loaded.Manifest.Module.Id, loaded.Manifest.Module.Version);
                EnsureInside(root, destination);
                replaced = Directory.Exists(destination);
                if (replaced && !replaceExisting)
                    return Failure("DDFM006", "Il modulo " + loaded.Manifest.Module.Id + "@" +
                        loaded.Manifest.Module.Version + " è già installato; la sostituzione deve essere esplicita.", diagnostics);

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (replaced)
                {
                    backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
                    Directory.Move(destination, backup);
                }
                Directory.Move(staging, destination);
                if (backup != null && Directory.Exists(backup)) Directory.Delete(backup, true);
                return new DdfModulePackageResult(new DdfInstalledModule(loaded.Manifest, destination),
                    null, diagnostics, replaced);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                                exception is InvalidDataException || exception is InvalidOperationException)
            { return Failure("DDFM007", exception.Message, diagnostics); }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (backup != null && Directory.Exists(backup))
                {
                    if (destination != null && !Directory.Exists(destination)) Directory.Move(backup, destination);
                    else Directory.Delete(backup, true);
                }
            }
        }

        public bool Remove(string id, string version)
        {
            string destination = Path.GetFullPath(Path.Combine(root, id ?? string.Empty, version ?? string.Empty));
            EnsureInside(root, destination);
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(destination);
            if (!loaded.Success || loaded.Manifest.Module.Id != id || loaded.Manifest.Module.Version != version)
                return false;
            string removing = destination + ".removing-" + Guid.NewGuid().ToString("N");
            Directory.Move(destination, removing);
            Directory.Delete(removing, true);
            string idDirectory = Path.GetDirectoryName(destination);
            if (Directory.Exists(idDirectory) && !Directory.EnumerateFileSystemEntries(idDirectory).Any())
                Directory.Delete(idDirectory);
            return true;
        }

        private static IReadOnlyList<string> ResolvePayloadFiles(string sourceRoot, DdfProjectManifest manifest)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(sourceRoot, DdfProjectManifest.FileName)
            };
            foreach (string pattern in manifest.Sources)
            {
                if (pattern.StartsWith("**/*.", StringComparison.Ordinal))
                {
                    string extension = Path.GetExtension(pattern.Substring(3));
                    foreach (string path in Directory.EnumerateFiles(sourceRoot, "*" + extension, SearchOption.AllDirectories))
                        if (!IsReservedPath(sourceRoot, manifest, path)) files.Add(Path.GetFullPath(path));
                }
                else
                {
                    string path = Path.GetFullPath(Path.Combine(sourceRoot, pattern));
                    EnsureInside(sourceRoot, path);
                    if (!File.Exists(path)) throw new InvalidDataException("Sorgente non trovato: " + pattern + ".");
                    files.Add(path);
                }
            }
            if (files.Count > MaximumFileCount) throw new InvalidDataException("Il modulo supera il limite di file consentito.");
            long total = files.Sum(path => new FileInfo(path).Length);
            if (total > MaximumPackageLength) throw new InvalidDataException("Il modulo supera la dimensione totale consentita.");
            return files.ToList().AsReadOnly();
        }

        private static void ValidatePayloadSources(string moduleRoot, DdfProjectManifest manifest)
        {
            if (ResolvePayloadFiles(moduleRoot, manifest).Count == 1)
                throw new InvalidDataException("Il manifesto non seleziona alcun sorgente esistente.");
        }

        private static void CopyDirectoryPayload(string source, string staging)
        {
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(source);
            if (!loaded.Success) throw new InvalidDataException(Describe(loaded.Issues));
            foreach (string file in ResolvePayloadFiles(source, loaded.Manifest))
            {
                string relative = Relative(source, file);
                string destination = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        private static void ExtractArchive(string source, string staging)
        {
            long total = 0;
            int count = 0;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using ZipArchive archive = ZipFile.OpenRead(source);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                count++;
                total += entry.Length;
                if (count > MaximumFileCount || entry.Length > MaximumFileLength || total > MaximumPackageLength)
                    throw new InvalidDataException("Il pacchetto supera i limiti di sicurezza consentiti.");
                string relative = entry.FullName.Replace('\\', '/');
                if (!SafeArchivePath(relative) || !paths.Add(relative))
                    throw new InvalidDataException("Percorso non sicuro o duplicato nel pacchetto: " + entry.FullName + ".");
                string destination = Path.GetFullPath(Path.Combine(staging,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                EnsureInside(staging, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using Stream input = entry.Open();
                using FileStream output = File.Create(destination);
                input.CopyTo(output);
            }
        }

        private static bool SafeArchivePath(string value) =>
            !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && value.IndexOf(':') < 0 &&
            value.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");

        private static bool IsReservedPath(string rootPath, DdfProjectManifest manifest, string path)
        {
            string relative = Relative(rootPath, path);
            string output = (manifest.Output?.Directory ?? ".ddf-build").Replace('\\', '/').Trim('/') + "/";
            return relative.StartsWith(output, StringComparison.OrdinalIgnoreCase) ||
                   relative.StartsWith(".ddf/modules/", StringComparison.OrdinalIgnoreCase);
        }

        private static string Relative(string rootPath, string path) =>
            Path.GetRelativePath(rootPath, path).Replace('\\', '/');

        private static void EnsureInside(string rootPath, string path)
        {
            string prefix = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Il percorso esce dalla radice consentita: " + path + ".");
        }

        private static string Describe(IEnumerable<DdfBuildIssue> issues) =>
            string.Join(" | ", issues.Select(item => item.Code + ": " + item.Message));

        private static DdfModulePackageResult Failure(string code, string message,
            ICollection<DdfModulePackageDiagnostic> diagnostics)
        {
            diagnostics.Add(new DdfModulePackageDiagnostic(code, message));
            return new DdfModulePackageResult(null, null, diagnostics, false);
        }
    }
}
