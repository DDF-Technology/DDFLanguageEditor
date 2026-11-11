using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public interface IDdfProductUpdateProcessWaiter
    {
        Task WaitForExitAsync(int processId, DateTimeOffset processStartedUtc,
            TimeSpan timeout, CancellationToken cancellationToken);
    }

    public sealed class DdfProductUpdateProcessWaiter :
        IDdfProductUpdateProcessWaiter
    {
        private readonly Action onAttested;

        public DdfProductUpdateProcessWaiter() : this(null) { }

        public DdfProductUpdateProcessWaiter(Action onAttested)
        {
            this.onAttested = onAttested;
        }

        public async Task WaitForExitAsync(int processId,
            DateTimeOffset processStartedUtc, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
            if (processStartedUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("L'istante deve essere UTC.",
                    nameof(processStartedUtc));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            Process process;
            try { process = Process.GetProcessById(processId); }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "Il processo editor atteso non esiste più.", exception);
            }
            using (process)
            {
                DateTimeOffset actualStartedUtc;
                try
                {
                    actualStartedUtc = new DateTimeOffset(
                        process.StartTime.ToUniversalTime(), TimeSpan.Zero);
                }
                catch (Exception exception) when (exception is InvalidOperationException ||
                    exception is System.ComponentModel.Win32Exception)
                {
                    throw new InvalidOperationException(
                        "Impossibile attestare il processo editor.", exception);
                }
                if (actualStartedUtc != processStartedUtc)
                    throw new InvalidOperationException(
                        "Il PID appartiene a un processo differente.");
                onAttested?.Invoke();
                using var timeoutSource = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(timeout);
                try
                {
                    await process.WaitForExitAsync(timeoutSource.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when
                    (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        "L'editor non si è chiuso entro il timeout.", exception);
                }
            }
        }
    }

    public static class DdfProductUpdateReadySignal
    {
        public static string NameFor(string nonce)
        {
            if (nonce == null || nonce.Length != 64 ||
                nonce.Any(character => !Uri.IsHexDigit(character)))
                throw new ArgumentException("Nonce updater non valido.",
                    nameof(nonce));
            return @"Local\DDFLanguageEditor.Updater.Ready." + nonce;
        }
    }

    public sealed class DdfProductUpdateWorkerOptions
    {
        public DdfProductUpdateWorkerOptions(TimeSpan editorExitTimeout)
        {
            if (editorExitTimeout <= TimeSpan.Zero ||
                editorExitTimeout > TimeSpan.FromHours(1))
                throw new ArgumentOutOfRangeException(nameof(editorExitTimeout));
            EditorExitTimeout = editorExitTimeout;
        }

        public TimeSpan EditorExitTimeout { get; }

        public static DdfProductUpdateWorkerOptions Default { get; } =
            new DdfProductUpdateWorkerOptions(TimeSpan.FromMinutes(5));
    }

    public sealed class DdfProductUpdateHealthCheckResult
    {
        private DdfProductUpdateHealthCheckResult(bool success, string detail)
        {
            Success = success;
            Detail = detail ?? string.Empty;
        }

        public bool Success { get; }
        public string Detail { get; }

        public static DdfProductUpdateHealthCheckResult Passed() =>
            new DdfProductUpdateHealthCheckResult(true, string.Empty);

        public static DdfProductUpdateHealthCheckResult Failed(string detail) =>
            new DdfProductUpdateHealthCheckResult(false,
                string.IsNullOrWhiteSpace(detail)
                    ? "Health check non riuscito."
                    : detail.Trim());
    }

    public interface IDdfProductUpdateHealthCheck
    {
        Task<DdfProductUpdateHealthCheckResult> CheckAsync(
            DdfProductUpdateInstallationPlan plan,
            CancellationToken cancellationToken);
    }

    public sealed class DdfProductUpdateProcessHealthCheck :
        IDdfProductUpdateHealthCheck
    {
        public const string Argument = "--verify-installation";
        public const string SuccessMarker = "DDF INSTALLATION OK";
        private const int MaximumOutputCharacters = 16 * 1024;
        private readonly TimeSpan timeout;

        public DdfProductUpdateProcessHealthCheck(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            this.timeout = timeout;
        }

        public async Task<DdfProductUpdateHealthCheckResult> CheckAsync(
            DdfProductUpdateInstallationPlan plan,
            CancellationToken cancellationToken)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            string executable = Path.GetFullPath(Path.Combine(plan.TargetRoot,
                plan.ApplicationExecutable.Replace('/',
                    Path.DirectorySeparatorChar)));
            if (!File.Exists(executable))
                return DdfProductUpdateHealthCheckResult.Failed(
                    "Eseguibile aggiornato assente.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = Argument,
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string variable in new[]
            {
                "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS",
                "DOTNET_SHARED_STORE", "COREHOST_TRACE", "COREHOST_TRACEFILE"
            })
                startInfo.Environment.Remove(variable);

            using Process process = Process.Start(startInfo);
            if (process == null)
                return DdfProductUpdateHealthCheckResult.Failed(
                    "Avvio del self-test non riuscito.");
            using var timeoutSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            Task<string> outputTask = ReadBoundedAsync(process.StandardOutput,
                timeoutSource.Token);
            Task<string> errorTask = ReadBoundedAsync(process.StandardError,
                timeoutSource.Token);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token)
                    .ConfigureAwait(false);
                string output = await outputTask.ConfigureAwait(false);
                string error = await errorTask.ConfigureAwait(false);
                if (process.ExitCode == 0 &&
                    string.Equals(output.Trim(), SuccessMarker,
                        StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(error))
                    return DdfProductUpdateHealthCheckResult.Passed();
                return DdfProductUpdateHealthCheckResult.Failed(
                    "Il self-test ha restituito un esito non valido.");
            }
            catch (OperationCanceledException) when
                (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return DdfProductUpdateHealthCheckResult.Failed(
                    "Timeout del self-test.");
            }
            catch (InvalidDataException)
            {
                TryKill(process);
                return DdfProductUpdateHealthCheckResult.Failed(
                    "Output del self-test oltre quota.");
            }
        }

        private static async Task<string> ReadBoundedAsync(StreamReader reader,
            CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            char[] buffer = new char[1024];
            while (true)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0) return builder.ToString();
                if (builder.Length + read > MaximumOutputCharacters)
                    throw new InvalidDataException("Output self-test oltre quota.");
                builder.Append(buffer, 0, read);
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    public enum DdfProductUpdateWorkerResultKind
    {
        AwaitingHealthCheck,
        Completed,
        CleanupCompleted,
        RolledBack,
        NoAction
    }

    public sealed class DdfProductUpdateWorkerResult
    {
        internal DdfProductUpdateWorkerResult(
            DdfProductUpdateWorkerResultKind kind, string quarantineRoot)
        {
            Kind = kind;
            QuarantineRoot = quarantineRoot ?? string.Empty;
        }

        public DdfProductUpdateWorkerResultKind Kind { get; }
        public string QuarantineRoot { get; }
    }

    public sealed class DdfProductUpdateStartupRecoveryStatus
    {
        internal DdfProductUpdateStartupRecoveryStatus(bool hasState,
            DdfProductUpdateInstallationState? state,
            DdfProductUpdateRecoveryAction action, string detail)
        {
            HasState = hasState;
            State = state;
            Action = action;
            Detail = detail ?? string.Empty;
        }

        public bool HasState { get; }
        public DdfProductUpdateInstallationState? State { get; }
        public DdfProductUpdateRecoveryAction Action { get; }
        public string Detail { get; }
        public bool RequiresAttention => Action != DdfProductUpdateRecoveryAction.None &&
            Action != DdfProductUpdateRecoveryAction.CleanupDeferred;
    }

    public static class DdfProductUpdateStartupRecovery
    {
        public static DdfProductUpdateStartupRecoveryStatus Inspect(
            string planPath, string journalPath)
        {
            bool planExists = File.Exists(planPath);
            bool journalExists = File.Exists(journalPath);
            if (!planExists && !journalExists)
                return new DdfProductUpdateStartupRecoveryStatus(false, null,
                    DdfProductUpdateRecoveryAction.None, string.Empty);
            if (!planExists || !journalExists)
                return Manual("Piano o journal dell'aggiornamento mancante.");
            try
            {
                DdfProductUpdateInstallationPlan plan =
                    DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
                var store = new DdfProductUpdateInstallationJournalStore(
                    journalPath);
                DdfProductUpdateInstallationJournal journal = store.Read();
                if (!string.Equals(journal.PlanSha256,
                    DdfProductUpdateInstallationJson.ComputePlanSha256(plan),
                    StringComparison.Ordinal))
                    return Manual("Piano e journal non sono correlati.");
                DdfProductUpdateRecoveryAction action =
                    DdfProductUpdateRecoveryPlanner.Plan(journal,
                        Directory.Exists(plan.TargetRoot),
                        Directory.Exists(plan.StagingRoot),
                        Directory.Exists(plan.BackupRoot));
                return new DdfProductUpdateStartupRecoveryStatus(true,
                    journal.State, action, action ==
                        DdfProductUpdateRecoveryAction.None
                        ? string.Empty
                        : "È presente un aggiornamento incompleto o da ripulire.");
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is ArgumentException ||
                exception is UnauthorizedAccessException)
            {
                return Manual("Stato aggiornamento non leggibile: " +
                    exception.Message);
            }
        }

        private static DdfProductUpdateStartupRecoveryStatus Manual(
            string detail) => new DdfProductUpdateStartupRecoveryStatus(true,
                null, DdfProductUpdateRecoveryAction.ManualIntervention, detail);
    }

    public static class DdfProductUpdateInstallationVerifier
    {
        public static void VerifyDirectory(
            DdfProductUpdateInstallationPlan plan, string root)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            string fullRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));
            if (!Directory.Exists(fullRoot) || IsReparsePoint(fullRoot))
                throw new InvalidDataException(
                    "Directory installazione assente o non regolare.");
            var expected = plan.Inventory.ToDictionary(file => file.RelativePath,
                StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(fullRoot);
            while (pending.Count != 0)
            {
                string directory = pending.Pop();
                foreach (string childDirectory in Directory.EnumerateDirectories(
                    directory))
                {
                    if (IsReparsePoint(childDirectory))
                        throw new InvalidDataException(
                            "Reparse point non ammesso nell'installazione.");
                    pending.Push(childDirectory);
                }
                foreach (string filePath in Directory.EnumerateFiles(directory))
                {
                    if (IsReparsePoint(filePath))
                        throw new InvalidDataException(
                            "Link non ammesso nell'installazione.");
                    string relative = Path.GetRelativePath(fullRoot, filePath)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    if (!actual.Add(relative) || !expected.TryGetValue(relative,
                            out DdfProductUpdateInstallationFile expectedFile))
                        throw new InvalidDataException(
                            "File estraneo o duplicato nell'installazione.");
                    var info = new FileInfo(filePath);
                    if (info.Length != expectedFile.Size)
                        throw new InvalidDataException(
                            "Dimensione installazione divergente.");
                    using FileStream stream = new FileStream(filePath, FileMode.Open,
                        FileAccess.Read, FileShare.Read, 64 * 1024,
                        FileOptions.SequentialScan);
                    string hash = Convert.ToHexString(SHA256.HashData(stream))
                        .ToLowerInvariant();
                    if (!string.Equals(hash, expectedFile.Sha256,
                            StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "Hash installazione divergente.");
                }
            }
            if (actual.Count != expected.Count)
                throw new InvalidDataException("Inventario installazione incompleto.");
        }

        public static void VerifyStaging(
            DdfProductUpdateInstallationPlan plan)
        {
            string sealPath = DdfProductUpdateStagingSealCodec.SealPathFor(
                plan.StagingRoot);
            DdfProductUpdateStagingSeal seal =
                DdfProductUpdateStagingSealCodec.Read(sealPath);
            DdfProductUpdateStagingSealCodec.ValidateForPlan(seal, plan);
            VerifyDirectory(plan, plan.StagingRoot);
        }

        private static bool IsReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    public sealed class DdfProductUpdateWorker
    {
        private readonly IDdfProductUpdateProcessWaiter processWaiter;
        private readonly DdfProductUpdateWorkerOptions options;

        public DdfProductUpdateWorker(
            IDdfProductUpdateProcessWaiter processWaiter = null,
            DdfProductUpdateWorkerOptions options = null)
        {
            this.processWaiter = processWaiter ??
                new DdfProductUpdateProcessWaiter();
            this.options = options ?? DdfProductUpdateWorkerOptions.Default;
        }

        public async Task<DdfProductUpdateWorkerResult> ApplyAsync(string planPath,
            string journalPath, string nonce,
            CancellationToken cancellationToken = default)
        {
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            ValidateNonce(plan, nonce);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            string lockPath = Path.GetFullPath(journalPath) + ".lock";
            using FileStream installationLock = AcquireLock(lockPath);
            DdfProductUpdateInstallationJournal journal = ValidateJournal(plan,
                store.Read(), DdfProductUpdateInstallationState.InstallAuthorized);

            try
            {
                DdfProductUpdateInstallationVerifier.VerifyStaging(plan);
                ValidatePreSwapRoots(plan);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is UnauthorizedAccessException)
            {
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Failed,
                    "Validazione pre-swap non riuscita.");
                throw;
            }
            journal = Transition(store, journal,
                DdfProductUpdateInstallationState.WaitingForEditorExit);
            try
            {
                await processWaiter.WaitForExitAsync(plan.ParentProcessId,
                    plan.ParentProcessStartedUtc, options.EditorExitTimeout,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when
                (cancellationToken.IsCancellationRequested)
            {
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Cancelled,
                    "Installazione annullata prima dello swap.");
                throw;
            }
            catch (Exception exception) when (exception is TimeoutException ||
                exception is InvalidOperationException)
            {
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Failed,
                    "Attesa o attestazione del processo editor non riuscita.");
                throw;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Cancelled,
                    "Installazione annullata prima dello swap.");
                cancellationToken.ThrowIfCancellationRequested();
            }

            journal = ValidateJournal(plan, store.Read(),
                DdfProductUpdateInstallationState.WaitingForEditorExit);
            try
            {
                DdfProductUpdateInstallationVerifier.VerifyStaging(plan);
                ValidatePreSwapRoots(plan);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is UnauthorizedAccessException)
            {
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Failed,
                    "Rivalidazione pre-swap non riuscita.");
                throw;
            }
            journal = Transition(store, journal,
                DdfProductUpdateInstallationState.Applying);
            bool backupCreated = false;
            try
            {
                Directory.Move(plan.TargetRoot, plan.BackupRoot);
                backupCreated = true;
                Directory.Move(plan.StagingRoot, plan.TargetRoot);
                DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                    plan.TargetRoot);
                journal = Transition(store, journal,
                    DdfProductUpdateInstallationState.Verifying);
                return new DdfProductUpdateWorkerResult(
                    DdfProductUpdateWorkerResultKind.AwaitingHealthCheck,
                    string.Empty);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is UnauthorizedAccessException)
            {
                return RollBack(plan, store, backupCreated,
                    "Applicazione o verifica strutturale non riuscita.");
            }
        }

        public DdfProductUpdateWorkerResult Recover(string planPath,
            string journalPath, string nonce)
        {
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            ValidateNonce(plan, nonce);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            using FileStream installationLock = AcquireLock(
                Path.GetFullPath(journalPath) + ".lock");
            DdfProductUpdateInstallationJournal journal = ValidateJournalHash(plan,
                store.Read());

            if (journal.State == DdfProductUpdateInstallationState.Applying)
            {
                if (!Directory.Exists(plan.TargetRoot) &&
                    Directory.Exists(plan.BackupRoot))
                    return RestoreBackup(plan, store,
                        "Ripristino dopo interruzione durante lo swap.");
                if (Directory.Exists(plan.TargetRoot) &&
                    Directory.Exists(plan.BackupRoot))
                {
                    try
                    {
                        DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                            plan.TargetRoot);
                        Transition(store, journal,
                            DdfProductUpdateInstallationState.Verifying);
                        return new DdfProductUpdateWorkerResult(
                            DdfProductUpdateWorkerResultKind.AwaitingHealthCheck,
                            string.Empty);
                    }
                    catch (InvalidDataException)
                    {
                        return RollBack(plan, store, true,
                            "Rollback dopo verifica strutturale fallita.");
                    }
                }
                throw new InvalidDataException(
                    "Stato filesystem ambiguo durante il recovery.");
            }
            if (journal.State == DdfProductUpdateInstallationState.Verifying)
            {
                if (!Directory.Exists(plan.TargetRoot) ||
                    !Directory.Exists(plan.BackupRoot))
                    throw new InvalidDataException(
                        "Copie insufficienti per il recovery.");
                try
                {
                    DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                        plan.TargetRoot);
                    return new DdfProductUpdateWorkerResult(
                        DdfProductUpdateWorkerResultKind.AwaitingHealthCheck,
                        string.Empty);
                }
                catch (InvalidDataException)
                {
                    return RollBack(plan, store, true,
                        "Rollback dopo verifica recovery fallita.");
                }
            }
            return new DdfProductUpdateWorkerResult(
                DdfProductUpdateWorkerResultKind.NoAction, string.Empty);
        }

        public async Task<DdfProductUpdateWorkerResult> FinalizeAsync(
            string planPath, string journalPath, string nonce,
            IDdfProductUpdateHealthCheck healthCheck,
            CancellationToken cancellationToken = default)
        {
            if (healthCheck == null) throw new ArgumentNullException(nameof(healthCheck));
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            ValidateNonce(plan, nonce);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            using FileStream installationLock = AcquireLock(
                Path.GetFullPath(journalPath) + ".lock");
            DdfProductUpdateInstallationJournal journal = ValidateJournal(plan,
                store.Read(), DdfProductUpdateInstallationState.Verifying);
            if (!Directory.Exists(plan.TargetRoot) ||
                !Directory.Exists(plan.BackupRoot))
                throw new InvalidDataException(
                    "Target o backup mancanti durante il health check.");
            try
            {
                DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                    plan.TargetRoot);
                DdfProductUpdateHealthCheckResult health =
                    await healthCheck.CheckAsync(plan, cancellationToken)
                        .ConfigureAwait(false);
                if (!health.Success)
                    throw new InvalidDataException(
                        "Health check fallito: " + health.Detail);
                journal = ValidateJournal(plan, store.Read(),
                    DdfProductUpdateInstallationState.Verifying);
                Transition(store, journal,
                    DdfProductUpdateInstallationState.Completed);
                return new DdfProductUpdateWorkerResult(
                    DdfProductUpdateWorkerResultKind.Completed, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return RollBack(plan, store, true,
                    "Rollback dopo annullamento del health check.");
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                return RollBack(plan, store, true,
                    "Rollback dopo errore del health check.");
            }
        }

        public DdfProductUpdateWorkerResult Cleanup(string planPath,
            string journalPath, string nonce)
        {
            DdfProductUpdateInstallationPlan plan =
                DdfProductUpdateInstallationJournalStore.ReadPlan(planPath);
            ValidateNonce(plan, nonce);
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            using FileStream installationLock = AcquireLock(
                Path.GetFullPath(journalPath) + ".lock");
            DdfProductUpdateInstallationJournal journal = ValidateJournalHash(plan,
                store.Read());

            string cleanupRoot;
            if (journal.State == DdfProductUpdateInstallationState.Completed)
            {
                DdfProductUpdateInstallationVerifier.VerifyDirectory(plan,
                    plan.TargetRoot);
                cleanupRoot = plan.BackupRoot;
            }
            else if (journal.State ==
                DdfProductUpdateInstallationState.RolledBack)
            {
                if (!Directory.Exists(plan.TargetRoot))
                    throw new InvalidDataException(
                        "Installazione ripristinata assente durante la pulizia.");
                cleanupRoot = plan.StagingRoot + ".failed-" + plan.Nonce;
            }
            else
            {
                throw new InvalidOperationException(
                    "La pulizia differita non è ammessa nello stato corrente.");
            }

            if (!Directory.Exists(cleanupRoot))
                return new DdfProductUpdateWorkerResult(
                    DdfProductUpdateWorkerResultKind.NoAction, string.Empty);
            DeleteOwnedDirectory(cleanupRoot);
            return new DdfProductUpdateWorkerResult(
                DdfProductUpdateWorkerResultKind.CleanupCompleted,
                string.Empty);
        }

        private static DdfProductUpdateWorkerResult RollBack(
            DdfProductUpdateInstallationPlan plan,
            DdfProductUpdateInstallationJournalStore store, bool backupCreated,
            string detail)
        {
            if (!backupCreated && !Directory.Exists(plan.BackupRoot))
            {
                DdfProductUpdateInstallationJournal current = store.Read();
                if (DdfProductUpdateInstallationTransitions.CanTransition(
                    current.State, DdfProductUpdateInstallationState.Failed))
                    Transition(store, current,
                        DdfProductUpdateInstallationState.Failed,
                        "Backup non disponibile per il rollback.");
                throw new InvalidDataException(
                    "Backup non disponibile per il rollback.");
            }
            return RestoreBackup(plan, store, detail);
        }

        private static DdfProductUpdateWorkerResult RestoreBackup(
            DdfProductUpdateInstallationPlan plan,
            DdfProductUpdateInstallationJournalStore store, string detail)
        {
            string quarantine = plan.StagingRoot + ".failed-" + plan.Nonce;
            if (Directory.Exists(quarantine) || File.Exists(quarantine))
                throw new InvalidDataException(
                    "Quarantena rollback già esistente.");
            if (Directory.Exists(plan.TargetRoot))
                Directory.Move(plan.TargetRoot, quarantine);
            if (!Directory.Exists(plan.BackupRoot))
                throw new InvalidDataException("Backup rollback assente.");
            Directory.Move(plan.BackupRoot, plan.TargetRoot);
            DdfProductUpdateInstallationJournal current = store.Read();
            Transition(store, current,
                DdfProductUpdateInstallationState.RolledBack, detail);
            return new DdfProductUpdateWorkerResult(
                DdfProductUpdateWorkerResultKind.RolledBack,
                Directory.Exists(quarantine) ? quarantine : string.Empty);
        }

        private static void ValidatePreSwapRoots(
            DdfProductUpdateInstallationPlan plan)
        {
            if (!Directory.Exists(plan.TargetRoot) ||
                (File.GetAttributes(plan.TargetRoot) &
                    FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    "Installazione corrente assente o non regolare.");
            string executable = Path.Combine(plan.TargetRoot,
                plan.ApplicationExecutable.Replace('/',
                    Path.DirectorySeparatorChar));
            if (!File.Exists(executable) ||
                (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    "Eseguibile corrente assente o non regolare.");
            if (Directory.Exists(plan.BackupRoot) || File.Exists(plan.BackupRoot))
                throw new InvalidDataException("Backup destinazione già esistente.");
        }

        private static void DeleteOwnedDirectory(string root)
        {
            string fullRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));
            if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    "La directory di pulizia è un reparse point.");
            var pending = new Stack<string>();
            pending.Push(fullRoot);
            while (pending.Count != 0)
            {
                string directory = pending.Pop();
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if ((File.GetAttributes(child) &
                        FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException(
                            "Reparse point non ammesso nella pulizia.");
                    pending.Push(child);
                }
                foreach (string file in Directory.EnumerateFiles(directory))
                    if ((File.GetAttributes(file) &
                        FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException(
                            "Link non ammesso nella pulizia.");
            }
            Directory.Delete(fullRoot, true);
        }

        private static DdfProductUpdateInstallationJournal ValidateJournal(
            DdfProductUpdateInstallationPlan plan,
            DdfProductUpdateInstallationJournal journal,
            DdfProductUpdateInstallationState expected)
        {
            ValidateJournalHash(plan, journal);
            if (journal.State != expected)
                throw new InvalidOperationException(
                    "Stato journal non ammesso per il worker.");
            return journal;
        }

        private static DdfProductUpdateInstallationJournal ValidateJournalHash(
            DdfProductUpdateInstallationPlan plan,
            DdfProductUpdateInstallationJournal journal)
        {
            if (!string.Equals(journal.PlanSha256,
                DdfProductUpdateInstallationJson.ComputePlanSha256(plan),
                StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Journal non correlato al piano worker.");
            return journal;
        }

        private static void ValidateNonce(DdfProductUpdateInstallationPlan plan,
            string nonce)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(plan.Nonce),
                System.Text.Encoding.ASCII.GetBytes(nonce ?? string.Empty)))
                throw new InvalidDataException("Nonce worker non valido.");
        }

        private static FileStream AcquireLock(string lockPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath));
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1,
                    FileOptions.WriteThrough);
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    "Un altro updater possiede già il lock.", exception);
            }
        }

        private static DdfProductUpdateInstallationJournal Transition(
            DdfProductUpdateInstallationJournalStore store,
            DdfProductUpdateInstallationJournal current,
            DdfProductUpdateInstallationState next, string detail = null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < current.UpdatedUtc) now = current.UpdatedUtc;
            return store.Transition(current.Revision, next, now, detail);
        }
    }
}
