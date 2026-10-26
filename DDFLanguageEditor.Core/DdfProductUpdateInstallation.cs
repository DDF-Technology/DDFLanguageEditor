using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfProductUpdateInstallationState
    {
        Checked,
        DownloadAuthorized,
        Downloading,
        Staged,
        InstallAuthorized,
        WaitingForEditorExit,
        Applying,
        Verifying,
        Completed,
        RolledBack,
        Failed,
        Cancelled
    }

    public enum DdfProductUpdateRecoveryAction
    {
        None,
        DiscardStaging,
        RestoreBackup,
        VerifyOrRollback,
        CleanupDeferred,
        ManualIntervention
    }

    public sealed class DdfProductUpdateInstallationFile
    {
        public DdfProductUpdateInstallationFile(string relativePath, long size,
            string sha256)
        {
            RelativePath = RequireSafeRelativePath(relativePath);
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            Sha256 = RequireHash(sha256, nameof(sha256));
        }

        public string RelativePath { get; }
        public long Size { get; }
        public string Sha256 { get; }

        internal static string RequireSafeRelativePath(string value)
        {
            string path = (value ?? string.Empty).Replace('\\', '/');
            if (path.Length == 0 || Path.IsPathRooted(path) || path.Contains(':') ||
                path.IndexOf('\0') >= 0 || path.StartsWith("/", StringComparison.Ordinal) ||
                path.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("Percorso inventario non valido.",
                    nameof(value));
            string[] parts = path.Split('/');
            if (parts.Any(part => part.Length == 0 || part == "." || part == ".." ||
                part.EndsWith(" ", StringComparison.Ordinal) ||
                part.EndsWith(".", StringComparison.Ordinal) ||
                IsReservedWindowsName(part)))
                throw new ArgumentException("Percorso inventario non sicuro.",
                    nameof(value));
            return string.Join("/", parts);
        }

        internal static string RequireHash(string value, string parameterName)
        {
            string hash = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (hash.Length != 64 || hash.Any(character =>
                !Uri.IsHexDigit(character)))
                throw new ArgumentException("SHA-256 non valido.", parameterName);
            return hash;
        }

        private static bool IsReservedWindowsName(string segment)
        {
            string name = segment.Split('.')[0].TrimEnd(' ', '.');
            return Regex.IsMatch(name,
                "^(con|prn|aux|nul|com[1-9]|lpt[1-9])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }

    public sealed class DdfProductUpdateInstallationPlan
    {
        public const string SchemaVersionValue = "1.0";
        public const string ProtocolVersionValue = "1.0";
        private const int MaximumInventoryFiles = 4096;

        public DdfProductUpdateInstallationPlan(string installationId,
            string componentId, string currentVersion, string targetVersion,
            string runtimeIdentifier, string architecture,
            string manifestSha256, string signingKeyId, Uri assetUri,
            long assetSize, string assetSha256, string targetRoot,
            string stagingRoot, string backupRoot, string applicationExecutable,
            int parentProcessId, DateTimeOffset parentProcessStartedUtc,
            string nonce, bool restartRequested,
            IEnumerable<DdfProductUpdateInstallationFile> inventory,
            DateTimeOffset createdUtc)
        {
            InstallationId = RequireIdentifier(installationId,
                nameof(installationId));
            ComponentId = DdfProductUpdateResult.RequireComponentId(componentId);
            CurrentVersion = DdfProductUpdateResult.RequireVersion(currentVersion,
                nameof(currentVersion));
            TargetVersion = DdfProductUpdateResult.RequireVersion(targetVersion,
                nameof(targetVersion));
            if (DdfSemanticVersionOrder.Compare(TargetVersion, CurrentVersion) <= 0)
                throw new ArgumentException(
                    "La versione destinazione deve essere successiva a quella corrente.",
                    nameof(targetVersion));
            RuntimeIdentifier = RequireToken(runtimeIdentifier,
                nameof(runtimeIdentifier));
            Architecture = RequireToken(architecture, nameof(architecture));
            ManifestSha256 = DdfProductUpdateInstallationFile.RequireHash(
                manifestSha256, nameof(manifestSha256));
            SigningKeyId = RequireIdentifier(signingKeyId, nameof(signingKeyId));
            AssetUri = RequireHttpsUri(assetUri);
            if (assetSize <= 0) throw new ArgumentOutOfRangeException(nameof(assetSize));
            AssetSize = assetSize;
            AssetSha256 = DdfProductUpdateInstallationFile.RequireHash(assetSha256,
                nameof(assetSha256));
            TargetRoot = RequireAbsoluteDirectory(targetRoot, nameof(targetRoot));
            StagingRoot = RequireAbsoluteDirectory(stagingRoot, nameof(stagingRoot));
            BackupRoot = RequireAbsoluteDirectory(backupRoot, nameof(backupRoot));
            ValidateRoots(TargetRoot, StagingRoot, BackupRoot);
            ApplicationExecutable =
                DdfProductUpdateInstallationFile.RequireSafeRelativePath(
                    applicationExecutable);
            if (parentProcessId <= 0)
                throw new ArgumentOutOfRangeException(nameof(parentProcessId));
            ParentProcessId = parentProcessId;
            ParentProcessStartedUtc = RequireUtc(parentProcessStartedUtc,
                nameof(parentProcessStartedUtc));
            Nonce = RequireNonce(nonce);
            RestartRequested = restartRequested;
            CreatedUtc = RequireUtc(createdUtc, nameof(createdUtc));

            var files = (inventory ?? throw new ArgumentNullException(
                nameof(inventory))).ToList();
            if (files.Count == 0 || files.Count > MaximumInventoryFiles ||
                files.Any(file => file == null) ||
                files.Select(file => file.RelativePath).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() != files.Count)
                throw new ArgumentException("Inventario vuoto, duplicato o oltre quota.",
                    nameof(inventory));
            if (!files.Any(file => string.Equals(file.RelativePath,
                ApplicationExecutable, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    "L'eseguibile principale non è presente nell'inventario.",
                    nameof(inventory));
            Inventory = new ReadOnlyCollection<DdfProductUpdateInstallationFile>(
                files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToList());
        }

        public string SchemaVersion => SchemaVersionValue;
        public string ProtocolVersion => ProtocolVersionValue;
        public string InstallationId { get; }
        public string ComponentId { get; }
        public string CurrentVersion { get; }
        public string TargetVersion { get; }
        public string RuntimeIdentifier { get; }
        public string Architecture { get; }
        public string ManifestSha256 { get; }
        public string SigningKeyId { get; }
        public Uri AssetUri { get; }
        public long AssetSize { get; }
        public string AssetSha256 { get; }
        public string TargetRoot { get; }
        public string StagingRoot { get; }
        public string BackupRoot { get; }
        public string ApplicationExecutable { get; }
        public int ParentProcessId { get; }
        public DateTimeOffset ParentProcessStartedUtc { get; }
        public string Nonce { get; }
        public bool RestartRequested { get; }
        public IReadOnlyList<DdfProductUpdateInstallationFile> Inventory { get; }
        public DateTimeOffset CreatedUtc { get; }

        private static string RequireIdentifier(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0 || result.Length > 128 ||
                !Regex.IsMatch(result, "^[A-Za-z0-9][A-Za-z0-9._-]*$"))
                throw new ArgumentException("Identificatore non valido.", parameterName);
            return result;
        }

        private static string RequireToken(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (!Regex.IsMatch(result, "^[a-z0-9][a-z0-9._-]*$"))
                throw new ArgumentException("Token non valido.", parameterName);
            return result;
        }

        private static Uri RequireHttpsUri(Uri value)
        {
            if (value == null || !value.IsAbsoluteUri ||
                value.Scheme != Uri.UriSchemeHttps || value.UserInfo.Length != 0 ||
                value.Fragment.Length != 0)
                throw new ArgumentException(
                    "L'asset deve usare una URI HTTPS verificata senza credenziali o frammenti.",
                    nameof(value));
            return value;
        }

        private static string RequireAbsoluteDirectory(string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
                throw new ArgumentException("Directory assoluta richiesta.", parameterName);
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
            if (string.Equals(full, Path.GetPathRoot(full),
                StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La radice del volume non è ammessa.",
                    parameterName);
            return full;
        }

        private static void ValidateRoots(params string[] roots)
        {
            if (roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Length)
                throw new ArgumentException("Le directory del piano devono essere distinte.");
            string volume = Path.GetPathRoot(roots[0]);
            if (roots.Any(root => !string.Equals(Path.GetPathRoot(root), volume,
                    StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    "Target, staging e backup devono essere sullo stesso volume.");
            for (int left = 0; left < roots.Length; left++)
            for (int right = left + 1; right < roots.Length; right++)
            {
                string leftPrefix = roots[left] + Path.DirectorySeparatorChar;
                string rightPrefix = roots[right] + Path.DirectorySeparatorChar;
                if (roots[left].StartsWith(rightPrefix,
                        StringComparison.OrdinalIgnoreCase) ||
                    roots[right].StartsWith(leftPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        "Le directory del piano non possono essere annidate.");
            }
        }

        private static DateTimeOffset RequireUtc(DateTimeOffset value,
            string parameterName)
        {
            if (value.Offset != TimeSpan.Zero)
                throw new ArgumentException("L'istante deve essere UTC.", parameterName);
            return value;
        }

        private static string RequireNonce(string value)
        {
            string result = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (result.Length < 32 || result.Length > 128 ||
                result.Any(character => !Uri.IsHexDigit(character)))
                throw new ArgumentException("Nonce monouso non valido.", nameof(value));
            return result;
        }
    }

    public sealed class DdfProductUpdateInstallationJournal
    {
        public const string SchemaVersionValue = "1.0";

        internal DdfProductUpdateInstallationJournal(string planSha256,
            DdfProductUpdateInstallationState state, long revision,
            DateTimeOffset createdUtc, DateTimeOffset updatedUtc, string detail)
        {
            PlanSha256 = DdfProductUpdateInstallationFile.RequireHash(planSha256,
                nameof(planSha256));
            if (!Enum.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (createdUtc.Offset != TimeSpan.Zero || updatedUtc.Offset != TimeSpan.Zero ||
                updatedUtc < createdUtc)
                throw new ArgumentException("Istanti journal non validi.");
            if ((state == DdfProductUpdateInstallationState.Failed ||
                 state == DdfProductUpdateInstallationState.RolledBack) &&
                string.IsNullOrWhiteSpace(detail))
                throw new ArgumentException(
                    "Uno stato di errore o rollback richiede un dettaglio.",
                    nameof(detail));
            State = state;
            Revision = revision;
            CreatedUtc = createdUtc;
            UpdatedUtc = updatedUtc;
            Detail = detail ?? string.Empty;
        }

        public string SchemaVersion => SchemaVersionValue;
        public string PlanSha256 { get; }
        public DdfProductUpdateInstallationState State { get; }
        public long Revision { get; }
        public DateTimeOffset CreatedUtc { get; }
        public DateTimeOffset UpdatedUtc { get; }
        public string Detail { get; }
        public bool IsTerminal => State == DdfProductUpdateInstallationState.Completed ||
            State == DdfProductUpdateInstallationState.RolledBack ||
            State == DdfProductUpdateInstallationState.Failed ||
            State == DdfProductUpdateInstallationState.Cancelled;
    }

    public static class DdfProductUpdateInstallationJson
    {
        private const int MaximumDocumentBytes = 2 * 1024 * 1024;
        private static readonly JsonSerializerOptions Options = CreateOptions();

        public static string SerializePlan(DdfProductUpdateInstallationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            return JsonSerializer.Serialize(ToDocument(plan), Options);
        }

        public static DdfProductUpdateInstallationPlan ParsePlan(string json)
        {
            PlanDocument document = Deserialize<PlanDocument>(json, "piano");
            if (document.SchemaVersion !=
                    DdfProductUpdateInstallationPlan.SchemaVersionValue ||
                document.ProtocolVersion !=
                    DdfProductUpdateInstallationPlan.ProtocolVersionValue)
                throw new InvalidDataException(
                    "Versione del piano di installazione non supportata.");
            try
            {
                return new DdfProductUpdateInstallationPlan(document.InstallationId,
                    document.ComponentId, document.CurrentVersion,
                    document.TargetVersion, document.RuntimeIdentifier,
                    document.Architecture, document.ManifestSha256,
                    document.SigningKeyId, new Uri(document.AssetUri,
                        UriKind.Absolute), document.AssetSize, document.AssetSha256,
                    document.TargetRoot, document.StagingRoot, document.BackupRoot,
                    document.ApplicationExecutable, document.ParentProcessId,
                    document.ParentProcessStartedUtc, document.Nonce,
                    document.RestartRequested,
                    (document.Inventory ?? Array.Empty<FileDocument>()).Select(file =>
                        new DdfProductUpdateInstallationFile(file.RelativePath,
                            file.Size, file.Sha256)), document.CreatedUtc);
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is UriFormatException || exception is OverflowException)
            {
                throw new InvalidDataException("Piano di installazione non valido.",
                    exception);
            }
        }

        public static string ComputePlanSha256(
            DdfProductUpdateInstallationPlan plan) => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(SerializePlan(plan))))
                .ToLowerInvariant();

        public static string SerializeJournal(
            DdfProductUpdateInstallationJournal journal)
        {
            if (journal == null) throw new ArgumentNullException(nameof(journal));
            return JsonSerializer.Serialize(new JournalDocument
            {
                SchemaVersion = journal.SchemaVersion,
                PlanSha256 = journal.PlanSha256,
                State = journal.State,
                Revision = journal.Revision,
                CreatedUtc = journal.CreatedUtc,
                UpdatedUtc = journal.UpdatedUtc,
                Detail = journal.Detail.Length == 0 ? null : journal.Detail
            }, Options);
        }

        public static DdfProductUpdateInstallationJournal ParseJournal(string json)
        {
            JournalDocument document = Deserialize<JournalDocument>(json, "journal");
            if (document.SchemaVersion !=
                DdfProductUpdateInstallationJournal.SchemaVersionValue)
                throw new InvalidDataException("Versione journal non supportata.");
            try
            {
                return new DdfProductUpdateInstallationJournal(document.PlanSha256,
                    document.State, document.Revision, document.CreatedUtc,
                    document.UpdatedUtc, document.Detail);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Journal non valido.", exception);
            }
        }

        internal static byte[] Utf8(string text) => new UTF8Encoding(false)
            .GetBytes(text);

        private static T Deserialize<T>(string json, string label)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
                throw new InvalidDataException("Documento " + label + " vuoto o oltre quota.");
            try
            {
                T result = JsonSerializer.Deserialize<T>(json, Options);
                return result ?? throw new InvalidDataException(
                    "Documento " + label + " nullo.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Documento " + label + " non valido.",
                    exception);
            }
        }

        private static PlanDocument ToDocument(
            DdfProductUpdateInstallationPlan plan) => new PlanDocument
        {
            SchemaVersion = plan.SchemaVersion,
            ProtocolVersion = plan.ProtocolVersion,
            InstallationId = plan.InstallationId,
            ComponentId = plan.ComponentId,
            CurrentVersion = plan.CurrentVersion,
            TargetVersion = plan.TargetVersion,
            RuntimeIdentifier = plan.RuntimeIdentifier,
            Architecture = plan.Architecture,
            ManifestSha256 = plan.ManifestSha256,
            SigningKeyId = plan.SigningKeyId,
            AssetUri = plan.AssetUri.AbsoluteUri,
            AssetSize = plan.AssetSize,
            AssetSha256 = plan.AssetSha256,
            TargetRoot = plan.TargetRoot,
            StagingRoot = plan.StagingRoot,
            BackupRoot = plan.BackupRoot,
            ApplicationExecutable = plan.ApplicationExecutable,
            ParentProcessId = plan.ParentProcessId,
            ParentProcessStartedUtc = plan.ParentProcessStartedUtc,
            Nonce = plan.Nonce,
            RestartRequested = plan.RestartRequested,
            Inventory = plan.Inventory.Select(file => new FileDocument
            {
                RelativePath = file.RelativePath,
                Size = file.Size,
                Sha256 = file.Sha256
            }).ToArray(),
            CreatedUtc = plan.CreatedUtc
        };

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
            options.Converters.Add(new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            return options;
        }

        private sealed class PlanDocument
        {
            public string SchemaVersion { get; set; }
            public string ProtocolVersion { get; set; }
            public string InstallationId { get; set; }
            public string ComponentId { get; set; }
            public string CurrentVersion { get; set; }
            public string TargetVersion { get; set; }
            public string RuntimeIdentifier { get; set; }
            public string Architecture { get; set; }
            public string ManifestSha256 { get; set; }
            public string SigningKeyId { get; set; }
            public string AssetUri { get; set; }
            public long AssetSize { get; set; }
            public string AssetSha256 { get; set; }
            public string TargetRoot { get; set; }
            public string StagingRoot { get; set; }
            public string BackupRoot { get; set; }
            public string ApplicationExecutable { get; set; }
            public int ParentProcessId { get; set; }
            public DateTimeOffset ParentProcessStartedUtc { get; set; }
            public string Nonce { get; set; }
            public bool RestartRequested { get; set; }
            public FileDocument[] Inventory { get; set; }
            public DateTimeOffset CreatedUtc { get; set; }
        }

        private sealed class FileDocument
        {
            public string RelativePath { get; set; }
            public long Size { get; set; }
            public string Sha256 { get; set; }
        }

        private sealed class JournalDocument
        {
            public string SchemaVersion { get; set; }
            public string PlanSha256 { get; set; }
            public DdfProductUpdateInstallationState State { get; set; }
            public long Revision { get; set; }
            public DateTimeOffset CreatedUtc { get; set; }
            public DateTimeOffset UpdatedUtc { get; set; }
            public string Detail { get; set; }
        }
    }

    public sealed class DdfProductUpdateInstallationJournalStore
    {
        private const int MaximumFileBytes = 2 * 1024 * 1024;
        private readonly object sync = new object();
        private readonly string path;

        public DdfProductUpdateInstallationJournalStore(string journalPath)
        {
            if (string.IsNullOrWhiteSpace(journalPath))
                throw new ArgumentException("Percorso journal richiesto.",
                    nameof(journalPath));
            path = System.IO.Path.GetFullPath(journalPath);
        }

        public string Path => path;

        public DdfProductUpdateInstallationJournal Create(string planSha256,
            DateTimeOffset nowUtc)
        {
            lock (sync)
            {
                if (File.Exists(path))
                    throw new InvalidOperationException("Il journal esiste già.");
                var journal = new DdfProductUpdateInstallationJournal(planSha256,
                    DdfProductUpdateInstallationState.Checked, 0, nowUtc, nowUtc,
                    null);
                WriteAtomic(path,
                    DdfProductUpdateInstallationJson.SerializeJournal(journal), false);
                return journal;
            }
        }

        public DdfProductUpdateInstallationJournal Read()
        {
            lock (sync) return ReadCore();
        }

        public DdfProductUpdateInstallationJournal Transition(long expectedRevision,
            DdfProductUpdateInstallationState next, DateTimeOffset nowUtc,
            string detail = null)
        {
            lock (sync)
            {
                DdfProductUpdateInstallationJournal current = ReadCore();
                if (current.Revision != expectedRevision)
                    throw new InvalidOperationException(
                        "La revisione del journal è cambiata.");
                if (!DdfProductUpdateInstallationTransitions.CanTransition(
                    current.State, next))
                    throw new InvalidOperationException("Transizione journal non ammessa: " +
                        current.State + " → " + next + ".");
                var updated = new DdfProductUpdateInstallationJournal(
                    current.PlanSha256, next, checked(current.Revision + 1),
                    current.CreatedUtc, nowUtc, detail);
                WriteAtomic(path,
                    DdfProductUpdateInstallationJson.SerializeJournal(updated), true);
                return updated;
            }
        }

        public static void WritePlan(string planPath,
            DdfProductUpdateInstallationPlan plan)
        {
            if (string.IsNullOrWhiteSpace(planPath))
                throw new ArgumentException("Percorso piano richiesto.",
                    nameof(planPath));
            WriteAtomic(System.IO.Path.GetFullPath(planPath),
                DdfProductUpdateInstallationJson.SerializePlan(plan), false);
        }

        public static DdfProductUpdateInstallationPlan ReadPlan(string planPath)
        {
            string full = System.IO.Path.GetFullPath(planPath);
            return DdfProductUpdateInstallationJson.ParsePlan(ReadLimited(full));
        }

        private DdfProductUpdateInstallationJournal ReadCore() =>
            DdfProductUpdateInstallationJson.ParseJournal(ReadLimited(path));

        private static string ReadLimited(string file)
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length <= 0 || info.Length > MaximumFileBytes)
                throw new InvalidDataException("File assente, vuoto o oltre quota.");
            return File.ReadAllText(file, Encoding.UTF8);
        }

        private static void WriteAtomic(string destination, string content,
            bool overwrite)
        {
            string directory = System.IO.Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = DdfProductUpdateInstallationJson.Utf8(content);
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (!overwrite && File.Exists(destination))
                    throw new IOException("Il file destinazione esiste già.");
                File.Move(temporary, destination, overwrite);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    public static class DdfProductUpdateInstallationTransitions
    {
        private static readonly IReadOnlyDictionary<
            DdfProductUpdateInstallationState,
            IReadOnlyCollection<DdfProductUpdateInstallationState>> Allowed =
            BuildAllowed();

        public static bool CanTransition(DdfProductUpdateInstallationState current,
            DdfProductUpdateInstallationState next) =>
            Allowed.TryGetValue(current, out var states) && states.Contains(next);

        private static IReadOnlyDictionary<DdfProductUpdateInstallationState,
            IReadOnlyCollection<DdfProductUpdateInstallationState>> BuildAllowed()
        {
            var map = new Dictionary<DdfProductUpdateInstallationState,
                IReadOnlyCollection<DdfProductUpdateInstallationState>>();
            Add(map, DdfProductUpdateInstallationState.Checked,
                DdfProductUpdateInstallationState.DownloadAuthorized,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.DownloadAuthorized,
                DdfProductUpdateInstallationState.Downloading,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.Downloading,
                DdfProductUpdateInstallationState.Staged,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.Staged,
                DdfProductUpdateInstallationState.InstallAuthorized,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.InstallAuthorized,
                DdfProductUpdateInstallationState.WaitingForEditorExit,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.WaitingForEditorExit,
                DdfProductUpdateInstallationState.Applying,
                DdfProductUpdateInstallationState.Cancelled,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.Applying,
                DdfProductUpdateInstallationState.Verifying,
                DdfProductUpdateInstallationState.RolledBack,
                DdfProductUpdateInstallationState.Failed);
            Add(map, DdfProductUpdateInstallationState.Verifying,
                DdfProductUpdateInstallationState.Completed,
                DdfProductUpdateInstallationState.RolledBack,
                DdfProductUpdateInstallationState.Failed);
            return new ReadOnlyDictionary<DdfProductUpdateInstallationState,
                IReadOnlyCollection<DdfProductUpdateInstallationState>>(map);
        }

        private static void Add(Dictionary<DdfProductUpdateInstallationState,
            IReadOnlyCollection<DdfProductUpdateInstallationState>> map,
            DdfProductUpdateInstallationState from,
            params DdfProductUpdateInstallationState[] to) =>
            map[from] = Array.AsReadOnly(to);
    }

    public static class DdfProductUpdateRecoveryPlanner
    {
        public static DdfProductUpdateRecoveryAction Plan(
            DdfProductUpdateInstallationJournal journal, bool targetExists,
            bool stagingExists, bool backupExists)
        {
            if (journal == null) throw new ArgumentNullException(nameof(journal));
            switch (journal.State)
            {
                case DdfProductUpdateInstallationState.Checked:
                case DdfProductUpdateInstallationState.DownloadAuthorized:
                case DdfProductUpdateInstallationState.Downloading:
                case DdfProductUpdateInstallationState.Staged:
                case DdfProductUpdateInstallationState.InstallAuthorized:
                case DdfProductUpdateInstallationState.WaitingForEditorExit:
                case DdfProductUpdateInstallationState.Cancelled:
                    return stagingExists
                        ? DdfProductUpdateRecoveryAction.DiscardStaging
                        : DdfProductUpdateRecoveryAction.None;
                case DdfProductUpdateInstallationState.Failed:
                    if (!targetExists && backupExists)
                        return DdfProductUpdateRecoveryAction.RestoreBackup;
                    if (targetExists && backupExists)
                        return DdfProductUpdateRecoveryAction.VerifyOrRollback;
                    return stagingExists
                        ? DdfProductUpdateRecoveryAction.DiscardStaging
                        : DdfProductUpdateRecoveryAction.None;
                case DdfProductUpdateInstallationState.Applying:
                    if (!targetExists && backupExists)
                        return DdfProductUpdateRecoveryAction.RestoreBackup;
                    if (targetExists && backupExists)
                        return DdfProductUpdateRecoveryAction.VerifyOrRollback;
                    return DdfProductUpdateRecoveryAction.ManualIntervention;
                case DdfProductUpdateInstallationState.Verifying:
                    return targetExists && backupExists
                        ? DdfProductUpdateRecoveryAction.VerifyOrRollback
                        : DdfProductUpdateRecoveryAction.ManualIntervention;
                case DdfProductUpdateInstallationState.Completed:
                    return backupExists
                        ? DdfProductUpdateRecoveryAction.CleanupDeferred
                        : DdfProductUpdateRecoveryAction.None;
                case DdfProductUpdateInstallationState.RolledBack:
                    return targetExists
                        ? DdfProductUpdateRecoveryAction.CleanupDeferred
                        : DdfProductUpdateRecoveryAction.ManualIntervention;
                default:
                    return DdfProductUpdateRecoveryAction.ManualIntervention;
            }
        }
    }
}
