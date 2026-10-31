using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfLanguagePackageArchiveResult
    {
        internal DdfLanguagePackageArchiveResult(string outputPath, string sha256,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            OutputPath = outputPath ?? string.Empty;
            Sha256 = sha256 ?? string.Empty;
            Diagnostics = Array.AsReadOnly((diagnostics ??
                Array.Empty<DdfLanguagePackageDiagnostic>()).ToArray());
        }

        public string OutputPath { get; }
        public string Sha256 { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => OutputPath.Length != 0 && Sha256.Length != 0 &&
            !Diagnostics.Any(item => item.Severity ==
                DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public static class DdfLanguagePackageArchiveBuilder
    {
        public const string ChecksumsFile = "ddflang-checksums.json";
        public const string ValidationFile = "ddflang-validation.json";
        private const int MaximumFiles = 64;
        private const long MaximumFileBytes = 16L * 1024 * 1024;
        private const long MaximumBytes = 64L * 1024 * 1024;
        private static readonly DateTimeOffset StableTimestamp =
            new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions { WriteIndented = true };

        public static DdfLanguagePackageArchiveResult Generate(
            DdfLanguageAuthoringProject project, string destination)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(destination) ||
                !string.Equals(Path.GetExtension(destination), ".ddflang",
                    StringComparison.OrdinalIgnoreCase))
                return Failure("DDFPA030", "Destinazione .ddflang richiesta.", destination);
            string output;
            try { output = Path.GetFullPath(destination); }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is NotSupportedException || exception is PathTooLongException)
            { return Failure("DDFPA030", exception.Message, destination); }
            if (File.Exists(output) || Directory.Exists(output))
                return Failure("DDFPA031", "La destinazione esiste già; non viene sovrascritta.", output);

            DdfLanguagePackageLoadResult validation =
                DdfLanguagePackageAuthoring.Validate(project);
            if (!validation.Success)
                return new DdfLanguagePackageArchiveResult("", "", validation.Diagnostics);

            string temporary = output + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                var payload = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (DdfLanguageAuthoringDocument document in project.Documents.Values)
                {
                    string path = document.RelativePath;
                    if (string.Equals(path, ChecksumsFile, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(path, ValidationFile, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!SafeEntry(path))
                        return Failure("DDFPA032", "Percorso non sicuro nell'archivio: " + path,
                            path);
                    if (!names.Add(path))
                        return Failure("DDFPA032", "Nome file duplicato nell'archivio: " + path,
                            path);
                    byte[] bytes = Utf8.GetBytes(document.Content);
                    total += bytes.Length;
                    if (bytes.Length > MaximumFileBytes || total > MaximumBytes ||
                        payload.Count + 3 > MaximumFiles)
                        return Failure("DDFPA033", "Archivio oltre la quota consentita.", output);
                    payload.Add(path, bytes);
                }

                var files = new JsonArray();
                foreach (KeyValuePair<string, byte[]> item in payload)
                    files.Add(new JsonObject
                    {
                        ["path"] = item.Key,
                        ["size"] = item.Value.Length,
                        ["sha256"] = Hash(item.Value)
                    });
                var checksums = new JsonObject
                {
                    ["schemaVersion"] = "1.0",
                    ["algorithm"] = "SHA-256",
                    ["packageId"] = validation.Package.Id,
                    ["packageVersion"] = validation.Package.PackageVersion,
                    ["files"] = files
                };
                int testCount = validation.Package.Tests?.Cases?.Count ?? 0;
                var report = new JsonObject
                {
                    ["schemaVersion"] = "1.0",
                    ["state"] = "passed",
                    ["packageId"] = validation.Package.Id,
                    ["packageVersion"] = validation.Package.PackageVersion,
                    ["declaredTestsPassed"] = testCount,
                    ["validationDiagnostics"] = new JsonArray(
                        validation.Diagnostics.Select(item =>
                            (JsonNode)JsonValue.Create(item.ToString())).ToArray())
                };
                payload.Add(ChecksumsFile, Utf8.GetBytes(checksums.ToJsonString(JsonOptions) + "\n"));
                payload.Add(ValidationFile, Utf8.GetBytes(report.ToJsonString(JsonOptions) + "\n"));
                if (payload.Count > MaximumFiles || payload.Values.Sum(item => (long)item.Length) >
                    MaximumBytes)
                    return Failure("DDFPA033", "Archivio oltre la quota consentita.", output);

                string parent = Path.GetDirectoryName(output);
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    return Failure("DDFPA034", "La cartella di destinazione non esiste.", output);
                using (var file = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, false,
                    Encoding.UTF8))
                    foreach (KeyValuePair<string, byte[]> item in payload)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(item.Key,
                            CompressionLevel.Optimal);
                        entry.LastWriteTime = StableTimestamp;
                        entry.ExternalAttributes = 0;
                        using Stream stream = entry.Open();
                        stream.Write(item.Value, 0, item.Value.Length);
                    }
                Verify(temporary, payload);
                DdfLanguagePackageLoadResult archived =
                    DdfLanguagePackageLoader.Load(temporary);
                if (!archived.Success)
                    return new DdfLanguagePackageArchiveResult("", "", archived.Diagnostics);
                string sha256 = Hash(File.ReadAllBytes(temporary));
                File.Move(temporary, output);
                return new DdfLanguagePackageArchiveResult(output, sha256,
                    Array.Empty<DdfLanguagePackageDiagnostic>());
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is JsonException)
            { return Failure("DDFPA035", exception.Message, output); }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static IReadOnlyList<DdfLanguagePackageDiagnostic> VerifyArchive(
            string path)
        {
            var issues = new List<DdfLanguagePackageDiagnostic>();
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(path);
                ZipArchiveEntry checksumsEntry = archive.GetEntry(ChecksumsFile);
                ZipArchiveEntry reportEntry = archive.GetEntry(ValidationFile);
                if (checksumsEntry == null && reportEntry == null)
                    return issues.AsReadOnly(); // Archivio legacy: il loader esegue comunque i test.
                if (checksumsEntry == null || reportEntry == null)
                    throw new InvalidDataException("Metadati .ddflang incompleti.");
                if (archive.Entries.Count > MaximumFiles ||
                    checksumsEntry.Length > MaximumFileBytes ||
                    reportEntry.Length > MaximumFileBytes)
                    throw new InvalidDataException("Metadati .ddflang oltre quota.");
                JsonObject checksums;
                using (var reader = new StreamReader(checksumsEntry.Open(), Utf8))
                    checksums = JsonNode.Parse(reader.ReadToEnd()).AsObject();
                JsonObject report;
                using (var reader = new StreamReader(reportEntry.Open(), Utf8))
                    report = JsonNode.Parse(reader.ReadToEnd()).AsObject();
                if (checksums["schemaVersion"]?.GetValue<string>() != "1.0" ||
                    checksums["algorithm"]?.GetValue<string>() != "SHA-256" ||
                    report["schemaVersion"]?.GetValue<string>() != "1.0" ||
                    report["state"]?.GetValue<string>() != "passed" ||
                    checksums["packageId"]?.GetValue<string>() !=
                        report["packageId"]?.GetValue<string>() ||
                    checksums["packageVersion"]?.GetValue<string>() !=
                        report["packageVersion"]?.GetValue<string>())
                    throw new InvalidDataException("Metadati .ddflang non coerenti.");
                JsonArray files = checksums["files"] as JsonArray ??
                    throw new InvalidDataException("Inventario SHA-256 mancante.");
                var expected = new Dictionary<string, (long Size, string Hash)>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (JsonNode node in files)
                {
                    JsonObject item = node as JsonObject ??
                        throw new InvalidDataException("Inventario SHA-256 non valido.");
                    string name = item["path"]?.GetValue<string>();
                    long size = item["size"]?.GetValue<long>() ?? -1;
                    string hash = item["sha256"]?.GetValue<string>();
                    if (!SafeEntry(name) || size < 0 || size > MaximumFileBytes ||
                        hash?.Length != 64 || !expected.TryAdd(name, (size, hash)))
                        throw new InvalidDataException("Voce inventario non valida: " + name);
                }
                if (archive.Entries.Count != expected.Count + 2 ||
                    archive.Entries.Count > MaximumFiles ||
                    expected.Values.Sum(item => item.Size) > MaximumBytes)
                    throw new InvalidDataException("Inventario archivio non coerente.");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!SafeEntry(entry.FullName) || !seen.Add(entry.FullName))
                        throw new InvalidDataException("Entry archivio non sicura o duplicata.");
                    if (entry.FullName == ChecksumsFile || entry.FullName == ValidationFile)
                        continue;
                    if (!expected.TryGetValue(entry.FullName, out var item) ||
                        entry.Length != item.Size || entry.Length > MaximumFileBytes)
                        throw new InvalidDataException("File archivio inatteso: " + entry.FullName);
                    using Stream stream = entry.Open();
                    string actual = Convert.ToHexString(SHA256.HashData(stream))
                        .ToLowerInvariant();
                    if (!string.Equals(actual, item.Hash, StringComparison.Ordinal))
                        throw new InvalidDataException("Checksum non valido: " + entry.FullName);
                }
                ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json") ??
                    throw new InvalidDataException("manifest.json mancante.");
                using (var reader = new StreamReader(manifestEntry.Open(), Utf8))
                {
                    JsonObject manifest = JsonNode.Parse(reader.ReadToEnd()).AsObject();
                    if (manifest["id"]?.GetValue<string>() !=
                            report["packageId"]?.GetValue<string>() ||
                        manifest["packageVersion"]?.GetValue<string>() !=
                            report["packageVersion"]?.GetValue<string>())
                        throw new InvalidDataException("Identità manifest/report non coerente.");
                }
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException || exception is JsonException ||
                exception is ArgumentException || exception is InvalidOperationException ||
                exception is NotSupportedException)
            {
                issues.Add(new DdfLanguagePackageDiagnostic("DDFPA036",
                    "Verifica archivio non riuscita: " + exception.Message, path));
            }
            return issues.AsReadOnly();
        }

        private static void Verify(string path, IReadOnlyDictionary<string, byte[]> expected)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count != expected.Count)
                throw new InvalidDataException("Inventario archivio non coerente.");
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!expected.TryGetValue(entry.FullName, out byte[] bytes) ||
                    entry.Length != bytes.Length)
                    throw new InvalidDataException("File archivio inatteso: " + entry.FullName);
                using Stream stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                if (!memory.ToArray().SequenceEqual(bytes))
                    throw new InvalidDataException("Checksum archivio non coerente: " + entry.FullName);
            }
        }

        private static bool SafeEntry(string path) =>
            DdfLanguageAuthoringPaths.TryNormalize(path, out string normalized) &&
            string.Equals(normalized, path, StringComparison.Ordinal) &&
            !normalized.Contains('/');

        private static string Hash(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private static DdfLanguagePackageArchiveResult Failure(string code,
            string message, string path) => new DdfLanguagePackageArchiveResult("", "",
                new[] { new DdfLanguagePackageDiagnostic(code, message, path) });
    }
}
