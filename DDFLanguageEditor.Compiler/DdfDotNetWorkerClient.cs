using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerExecutionResult
    {
        internal DdfDotNetWorkerExecutionResult(
            DdfDotNetWorkerResponse response,
            IEnumerable<DdfDotNetContractIssue> issues, bool timedOut,
            int? exitCode, bool osResourceLimitsApplied = false,
            bool isolatedWorkingDirectory = false,
            bool sanitizedEnvironment = false,
            bool workingDirectoryAccessControlApplied = false,
            IEnumerable<string> committedBrokerFiles = null,
            IEnumerable<DdfDotNetWorkerFileProtectionReceipt>
                brokerFileProtections = null)
        {
            Response = response;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
            TimedOut = timedOut;
            ExitCode = exitCode;
            OsResourceLimitsApplied = osResourceLimitsApplied;
            IsolatedWorkingDirectory = isolatedWorkingDirectory;
            SanitizedEnvironment = sanitizedEnvironment;
            WorkingDirectoryAccessControlApplied =
                workingDirectoryAccessControlApplied;
            CommittedBrokerFiles = (committedBrokerFiles ??
                Array.Empty<string>()).ToList().AsReadOnly();
            BrokerFileProtections = (brokerFileProtections ?? Array.Empty<
                DdfDotNetWorkerFileProtectionReceipt>()).ToList().AsReadOnly();
        }

        public DdfDotNetWorkerResponse Response { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool TimedOut { get; }
        public int? ExitCode { get; }
        public bool OsResourceLimitsApplied { get; }
        public bool IsolatedWorkingDirectory { get; }
        public bool SanitizedEnvironment { get; }
        public bool WorkingDirectoryAccessControlApplied { get; }
        public IReadOnlyList<string> CommittedBrokerFiles { get; }
        public IReadOnlyList<DdfDotNetWorkerFileProtectionReceipt>
            BrokerFileProtections { get; }
        public bool ReducedUserTokenApplied =>
            Response?.ReducedUserTokenApplied == true;
        public bool Success => Response?.Success == true && Issues.Count == 0 &&
            !TimedOut && ExitCode == 0;
    }

    public static class DdfDotNetWorkerClient
    {
        public static async Task<DdfDotNetWorkerExecutionResult> ExecuteAsync(
            string workerPath, DdfDotNetWorkerRequest request,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(workerPath, request,
                new DdfDotNetWorkerResourceLimits(timeout),
                cancellationToken);
        }

        public static async Task<DdfDotNetWorkerExecutionResult> ExecuteAsync(
            string workerPath, DdfDotNetWorkerRequest request,
            DdfDotNetWorkerResourceLimits limits,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCoreAsync(workerPath, request, limits,
                Array.Empty<DdfDotNetWorkerFileCommitTarget>(),
                null, null, cancellationToken);
        }

        public static async Task<DdfDotNetWorkerExecutionResult> ExecuteAsync(
            string workerPath, DdfDotNetWorkerRequest request,
            DdfDotNetWorkerResourceLimits limits,
            IEnumerable<DdfDotNetWorkerFileCommitTarget> commitTargets,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCoreAsync(workerPath, request, limits,
                commitTargets, null, null, cancellationToken);
        }

        public static async Task<DdfDotNetWorkerExecutionResult>
            ExecuteDuplexAsync(string workerPath,
                DdfDotNetWorkerRequest request,
                DdfDotNetWorkerResourceLimits limits,
                IDdfDotNetWorkerDuplexCapabilityHandler capabilityHandler,
                CancellationToken cancellationToken = default)
        {
            if (capabilityHandler == null) throw new ArgumentNullException(
                nameof(capabilityHandler));
            return await ExecuteCoreAsync(workerPath, request, limits,
                Array.Empty<DdfDotNetWorkerFileCommitTarget>(),
                capabilityHandler, null, cancellationToken);
        }

        public static async Task<DdfDotNetWorkerExecutionResult>
            ExecuteDuplexAsync(string workerPath,
                DdfDotNetWorkerRequest request,
                DdfDotNetWorkerResourceLimits limits,
                IDdfDotNetWorkerDuplexCapabilityHandler capabilityHandler,
                IDdfDotNetWorkerCooperativeExecutionObserver
                    cooperativeObserver,
                CancellationToken cancellationToken = default)
        {
            if (capabilityHandler == null) throw new ArgumentNullException(
                nameof(capabilityHandler));
            return await ExecuteCoreAsync(workerPath, request, limits,
                Array.Empty<DdfDotNetWorkerFileCommitTarget>(),
                capabilityHandler, cooperativeObserver, cancellationToken);
        }

        private static async Task<DdfDotNetWorkerExecutionResult>
            ExecuteCoreAsync(string workerPath, DdfDotNetWorkerRequest request,
                DdfDotNetWorkerResourceLimits limits,
                IEnumerable<DdfDotNetWorkerFileCommitTarget> commitTargets,
                IDdfDotNetWorkerDuplexCapabilityHandler capabilityHandler,
                IDdfDotNetWorkerCooperativeExecutionObserver cooperativeObserver,
                CancellationToken cancellationToken)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (string.IsNullOrWhiteSpace(workerPath) ||
                !Path.IsPathRooted(workerPath) || !File.Exists(workerPath))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC531",
                    "Percorso assoluto del worker non valido."));
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    null);
            }
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            IReadOnlyList<DdfDotNetWorkerFileCommitTarget> targets =
                (commitTargets ?? Array.Empty<
                    DdfDotNetWorkerFileCommitTarget>()).ToList().AsReadOnly();
            DdfDotNetContractIssue targetIssue = DdfDotNetWorkerFileCommitter
                .ValidateTargets(request, targets);
            if (targetIssue != null)
            {
                issues.Add(targetIssue);
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    null);
            }
            DdfWorkerTemporaryDirectory workingDirectory;
            try
            {
                workingDirectory = DdfWorkerTemporaryDirectory.Create();
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is InvalidOperationException ||
                                               exception is PlatformNotSupportedException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC538",
                    "Creazione della directory worker protetta fallita: " +
                    exception.Message));
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    null);
            }
            using (workingDirectory)
            {
            if (limits.RequireProtectedWorkingDirectory &&
                !workingDirectory.AccessControlApplied)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC538",
                    "La directory worker protetta è richiesta ma il sistema " +
                    "non consente di applicarne la ACL dedicata."));
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    null, false, true, false, false);
            }
            DdfWindowsJobObject job;
            try
            {
                job = DdfWindowsJobObject.Create(limits);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC535",
                    "Applicazione del profilo risorse Windows fallita: " +
                    exception.Message));
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    null, false, true, false,
                    workingDirectory.AccessControlApplied);
            }
            using (job)
            {
            var start = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory.Path
            };
            if (string.Equals(Path.GetExtension(workerPath), ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                start.FileName = Environment.GetEnvironmentVariable(
                    "DOTNET_HOST_PATH") ?? "dotnet";
                start.ArgumentList.Add(workerPath);
            }
            else
                start.FileName = workerPath;
            ConfigureSanitizedEnvironment(start, workingDirectory.Path,
                request.AccessPolicy?.RequireReducedUserToken == false);
            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new InvalidOperationException(
                    "Il processo worker non è stato avviato.");
                try
                {
                    job?.Assign(process);
                }
                catch (System.ComponentModel.Win32Exception exception)
                {
                    TryKill(process);
                    await process.WaitForExitAsync(CancellationToken.None);
                    issues.Add(new DdfDotNetContractIssue("DDFC535",
                        "Assegnazione del worker al profilo risorse Windows " +
                        "fallita: " + exception.Message));
                    return new DdfDotNetWorkerExecutionResult(null, issues,
                        false, process.ExitCode, false, true, true,
                        workingDirectory.AccessControlApplied);
                }
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                using var timeoutSource = new CancellationTokenSource(
                    limits.Timeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutSource.Token);
                Task<string> outputTask;
                if (capabilityHandler == null)
                {
                    outputTask = process.StandardOutput.ReadToEndAsync();
                    await process.StandardInput.WriteAsync(
                        DdfDotNetWorkerProtocol.SerializeRequest(request));
                    process.StandardInput.Close();
                }
                else
                    outputTask = ExchangeDuplexAsync(process, request,
                        capabilityHandler, cooperativeObserver, linked.Token);
                try
                {
                    Task exitTask = process.WaitForExitAsync(linked.Token);
                    if (capabilityHandler != null)
                    {
                        Task first = await Task.WhenAny(exitTask, outputTask);
                        if (first == outputTask && outputTask.IsFaulted)
                        {
                            TryKill(process);
                            await process.WaitForExitAsync(
                                CancellationToken.None);
                            await outputTask;
                        }
                    }
                    await exitTask;
                }
                catch (OperationCanceledException) when (
                    timeoutSource.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    await process.WaitForExitAsync(CancellationToken.None);
                    await DrainAfterTermination(outputTask, errorTask);
                    issues.Add(new DdfDotNetContractIssue("DDFC531",
                        "Timeout del worker; processo terminato forzatamente."));
                    return new DdfDotNetWorkerExecutionResult(null, issues,
                        true, process.ExitCode, job != null, true, true,
                        workingDirectory.AccessControlApplied);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    await process.WaitForExitAsync(CancellationToken.None);
                    await DrainAfterTermination(outputTask, errorTask);
                    throw;
                }
                string output = await outputTask;
                string error = await errorTask;
                if (process.ExitCode != 0)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC532",
                        "Worker terminato con codice " + process.ExitCode +
                        (string.IsNullOrWhiteSpace(error) ? "." : ": " +
                         error.Trim())));
                    return new DdfDotNetWorkerExecutionResult(null, issues,
                        false, process.ExitCode, job != null, true, true,
                        workingDirectory.AccessControlApplied);
                }
                DdfDotNetWorkerResponse response;
                try
                {
                    response = DdfDotNetWorkerProtocol.DeserializeResponse(output);
                }
                catch (Exception exception) when (exception is System.Text.Json
                           .JsonException || exception is NotSupportedException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC532",
                        "Risposta worker non leggibile: " + exception.Message));
                    return new DdfDotNetWorkerExecutionResult(null, issues,
                        false, process.ExitCode, job != null, true, true,
                        workingDirectory.AccessControlApplied);
                }
                if (response == null || response.Format !=
                        DdfDotNetWorkerResponse.FormatValue ||
                    response.ProtocolVersion !=
                        DdfDotNetWorkerResponse.ProtocolVersionValue)
                    issues.Add(new DdfDotNetContractIssue("DDFC532",
                        "Risposta worker incompatibile."));
                else if (response.CorrelationId != request.CorrelationId)
                {
                    if (response.ErrorCode == "DDFC537" &&
                        string.IsNullOrEmpty(response.CorrelationId))
                        issues.Add(new DdfDotNetContractIssue("DDFC537",
                            response.ErrorMessage ??
                            "Avvio con token ridotto fallito."));
                    else
                        issues.Add(new DdfDotNetContractIssue("DDFC532",
                            "Risposta worker non correlata."));
                }
                else if (request.AccessPolicy?.RequireReducedUserToken == true &&
                    !response.ReducedUserTokenApplied)
                    issues.Add(new DdfDotNetContractIssue("DDFC537",
                        "Il worker non ha attestato il token utente ridotto."));
                else if (!response.Success)
                    issues.Add(new DdfDotNetContractIssue(
                        string.IsNullOrWhiteSpace(response.ErrorCode)
                            ? "DDFC532" : response.ErrorCode,
                        response.ErrorMessage ?? "Esecuzione worker fallita."));
                IReadOnlyList<string> committed = Array.Empty<string>();
                IReadOnlyList<DdfDotNetWorkerFileProtectionReceipt>
                    protections = Array.Empty<
                        DdfDotNetWorkerFileProtectionReceipt>();
                if (issues.Count == 0 && response.Success)
                {
                    DdfDotNetWorkerFileCommitResult commit =
                        DdfDotNetWorkerFileCommitter.Commit(request, response,
                            targets);
                    if (!commit.Success) issues.Add(commit.Issue);
                    else
                    {
                        committed = commit.CommittedLogicalNames;
                        protections = commit.Protections;
                    }
                }
                return new DdfDotNetWorkerExecutionResult(response, issues,
                    false, process.ExitCode, job != null, true, true,
                    workingDirectory.AccessControlApplied, committed,
                    protections);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is ArgumentException ||
                                               exception is InvalidOperationException ||
                                               exception is InvalidDataException ||
                                               exception is NotSupportedException ||
                                               exception is JsonException ||
                                               exception is System.ComponentModel
                                                   .Win32Exception)
            {
                bool exited = false;
                try { exited = process.HasExited; }
                catch (InvalidOperationException) { }
                if (!exited) TryKill(process);
                issues.Add(new DdfDotNetContractIssue("DDFC531",
                    "Avvio o comunicazione col worker falliti: " +
                    exception.Message));
                return new DdfDotNetWorkerExecutionResult(null, issues, false,
                    exited ? process.ExitCode : null, job != null, true, true,
                    workingDirectory.AccessControlApplied);
            }
            }
            }
        }

        private static async Task<string> ExchangeDuplexAsync(Process process,
            DdfDotNetWorkerRequest request,
            IDdfDotNetWorkerDuplexCapabilityHandler capabilityHandler,
            IDdfDotNetWorkerCooperativeExecutionObserver cooperativeObserver,
            CancellationToken cancellationToken)
        {
            var transcript = new DdfDotNetWorkerDuplexTranscript();
            var start = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.Start,
                CorrelationId = request.CorrelationId,
                Sequence = 0,
                StartRequest = request
            };
            DdfDotNetContractIssue startIssue = transcript.Accept(start);
            if (startIssue != null) throw new InvalidOperationException(
                startIssue.Message);
            await process.StandardInput.WriteLineAsync(
                DdfDotNetWorkerDuplexProtocol.SerializeFrame(start));
            await process.StandardInput.FlushAsync(cancellationToken);
            while (true)
            {
                string line = await process.StandardOutput.ReadLineAsync(
                    cancellationToken);
                if (line == null)
                    throw new InvalidDataException(
                        "Worker duplex terminato senza frame finale.");
                DdfDotNetWorkerDuplexFrame frame;
                try
                {
                    frame = DdfDotNetWorkerDuplexProtocol.DeserializeFrame(line);
                }
                catch (Exception exception) when (exception is JsonException ||
                                                   exception is NotSupportedException)
                {
                    DdfDotNetWorkerResponse legacy = TryDeserializeLegacy(line);
                    if (legacy != null)
                        return DdfDotNetWorkerProtocol.SerializeResponse(legacy);
                    throw new InvalidDataException(
                        "Frame worker duplex non leggibile.", exception);
                }
                DdfDotNetContractIssue issue = transcript.Accept(frame);
                if (issue != null) throw new InvalidDataException(issue.Message);
                if (frame.Kind ==
                    DdfDotNetWorkerDuplexFrameKind.CapabilityRequest)
                {
                    DdfDotNetWorkerDuplexCapabilityResult result =
                        await InvokeLiveCapability(request, frame,
                            capabilityHandler, cooperativeObserver,
                            cancellationToken);
                    var response = new DdfDotNetWorkerDuplexFrame
                    {
                        Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityResponse,
                        CorrelationId = request.CorrelationId,
                        Sequence = frame.Sequence + 1,
                        RequestId = frame.RequestId,
                        CapabilitySucceeded = result.Success,
                        ResultJson = result.ResultJson,
                        ErrorCode = result.ErrorCode,
                        ErrorMessage = result.ErrorMessage
                    };
                    string encoded;
                    try
                    {
                        encoded = DdfDotNetWorkerDuplexProtocol.SerializeFrame(
                            response);
                    }
                    catch (NotSupportedException exception)
                    {
                        response.CapabilitySucceeded = false;
                        response.ResultJson = null;
                        response.ErrorCode = "DDFC548";
                        response.ErrorMessage = "Risposta capability host " +
                            "non valida: " + exception.Message;
                        encoded = DdfDotNetWorkerDuplexProtocol.SerializeFrame(
                            response);
                    }
                    issue = transcript.Accept(response);
                    if (issue != null) throw new InvalidDataException(
                        issue.Message);
                    await process.StandardInput.WriteLineAsync(encoded);
                    await process.StandardInput.FlushAsync(cancellationToken);
                    continue;
                }
                process.StandardInput.Close();
                if (frame.Kind == DdfDotNetWorkerDuplexFrameKind.Complete)
                    return DdfDotNetWorkerProtocol.SerializeResponse(
                        frame.FinalResponse);
                if (frame.Kind == DdfDotNetWorkerDuplexFrameKind.Abort)
                    return DdfDotNetWorkerProtocol.SerializeResponse(
                        new DdfDotNetWorkerResponse
                        {
                            CorrelationId = request.CorrelationId,
                            Success = false,
                            ErrorCode = frame.ErrorCode,
                            ErrorMessage = frame.ErrorMessage
                        });
                throw new InvalidDataException(
                    "Transizione worker duplex inattesa.");
            }
        }

        private static async ValueTask<
            DdfDotNetWorkerDuplexCapabilityResult> InvokeLiveCapability(
                DdfDotNetWorkerRequest request,
                DdfDotNetWorkerDuplexFrame frame,
                IDdfDotNetWorkerDuplexCapabilityHandler capabilityHandler,
                IDdfDotNetWorkerCooperativeExecutionObserver cooperativeObserver,
                CancellationToken cancellationToken)
        {
            bool network = frame.CapabilityId ==
                DdfDotNetWorkerBrokerCapabilities.FetchNetworkResponse;
            bool input = frame.CapabilityId ==
                DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput;
            bool debug = frame.CapabilityId ==
                DdfDotNetWorkerBrokerCapabilities.NextDebugCommand;
            bool authorized = network && request.AccessPolicy
                    .AllowedBrokerCapabilities.Contains(frame.CapabilityId,
                        StringComparer.Ordinal) &&
                    frame.ArgumentsJson.Count == 1 &&
                    TryReadString(frame.ArgumentsJson[0],
                        out string logicalName) &&
                    request.LiveNetworkResources.Contains(logicalName,
                        StringComparer.Ordinal) ||
                input && request.AccessPolicy.ConsoleInputAccess ==
                    DdfDotNetWorkerConsoleInputAccess.BrokeredInteractive &&
                    request.InteractiveInput != null &&
                    frame.ArgumentsJson.Count == 0 ||
                debug && request.DebugPlan?.Interactive == true &&
                    frame.ArgumentsJson.Count == 1 &&
                    TryReadDebugPause(frame.ArgumentsJson[0]);
            if (!authorized)
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    debug ? "DDFC550" : "DDFC548",
                    "Capability live o risorsa non autorizzata.");
            try
            {
                DdfDotNetWorkerCooperativeSuspension suspension =
                    frame.CooperativeDebugSnapshot == null ? null :
                        new DdfDotNetWorkerCooperativeSuspension(frame);
                if (suspension != null)
                    TryNotifyAwaiting(cooperativeObserver, suspension);
                DdfDotNetWorkerDuplexCapabilityResult result =
                    await capabilityHandler.InvokeAsync(frame.CapabilityId,
                        frame.ArgumentsJson, cancellationToken);
                if (result == null)
                    return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                        debug ? "DDFC550" : input ? "DDFC549" : "DDFC548",
                        "Handler capability live privo di risposta.");
                if (input && result.Success &&
                    (!TryReadString(result.ResultJson, out string inputValue) ||
                     System.Text.Encoding.UTF8.GetByteCount(inputValue) >
                        request.InteractiveInput.MaxValueBytes))
                    return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                        "DDFC549", "Valore input interattivo non valido o " +
                        "oltre quota.");
                if (debug && result.Success &&
                    (!TryReadString(result.ResultJson,
                        out string commandName) ||
                     !Enum.TryParse(commandName, ignoreCase: false,
                         out DdfDotNetDebugCommand command) ||
                     !Enum.IsDefined(command)))
                    return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                        "DDFC550", "Comando debug interattivo non valido.");
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    debug ? "DDFC550" : "DDFC548",
                    "Handler capability live fallito: " +
                    exception.Message);
            }
            finally
            {
                if (frame.CooperativeDebugSnapshot != null)
                    TryNotifyResumed(cooperativeObserver,
                        new DdfDotNetWorkerCooperativeSuspension(frame));
            }
        }

        private static void TryNotifyAwaiting(
            IDdfDotNetWorkerCooperativeExecutionObserver observer,
            DdfDotNetWorkerCooperativeSuspension suspension)
        {
            if (observer == null) return;
            try { observer.OnAwaitingCapability(suspension); }
            catch (Exception) { }
        }

        private static void TryNotifyResumed(
            IDdfDotNetWorkerCooperativeExecutionObserver observer,
            DdfDotNetWorkerCooperativeSuspension suspension)
        {
            if (observer == null) return;
            try { observer.OnCapabilityResumed(suspension); }
            catch (Exception) { }
        }

        private static bool TryReadDebugPause(string json)
        {
            try
            {
                DdfDotNetWorkerDebugPause pause = JsonSerializer.Deserialize<
                    DdfDotNetWorkerDebugPause>(json);
                return pause?.Location != null && pause.Location.Start >= 0 &&
                    pause.Location.Length >= 1 && pause.Instructions >= 0 &&
                    (pause.Frames?.Count ?? 0) <= 128;
            }
            catch (JsonException) { return false; }
        }

        private static bool TryReadString(string json, out string value)
        {
            value = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.String)
                    return false;
                value = document.RootElement.GetString();
                return value != null;
            }
            catch (JsonException) { return false; }
        }

        private static DdfDotNetWorkerResponse TryDeserializeLegacy(string value)
        {
            try { return DdfDotNetWorkerProtocol.DeserializeResponse(value); }
            catch (Exception exception) when (exception is JsonException ||
                                               exception is NotSupportedException)
            {
                return null;
            }
        }

        private static async Task DrainAfterTermination(Task<string> outputTask,
            Task<string> errorTask)
        {
            await DrainBounded(outputTask);
            await DrainBounded(errorTask);
        }

        private static async Task DrainBounded(Task task)
        {
            if (task == null) return;
            try { await task.WaitAsync(TimeSpan.FromMilliseconds(500)); }
            catch (TimeoutException)
            {
                ObserveLateTask(task);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is InvalidDataException ||
                                               exception is OperationCanceledException ||
                                               exception is InvalidOperationException)
            { }
        }

        private static void ObserveLateTask(Task task)
        {
            _ = task.ContinueWith(completed =>
                {
                    _ = completed.Exception;
                }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void ConfigureSanitizedEnvironment(ProcessStartInfo start,
            string workingDirectory, bool allowUnrestrictedWindowsWorker)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            string systemRoot = Environment.GetEnvironmentVariable(
                "SystemRoot");
            start.Environment.Clear();
            start.Environment["TEMP"] = workingDirectory;
            start.Environment["TMP"] = workingDirectory;
            start.Environment["HOME"] = workingDirectory;
            start.Environment["USERPROFILE"] = workingDirectory;
            start.Environment["DOTNET_CLI_HOME"] = workingDirectory;
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            start.Environment["DOTNET_EnableDiagnostics"] = "0";
            start.Environment["COMPlus_EnableDiagnostics"] = "0";
            if (OperatingSystem.IsWindows() &&
                allowUnrestrictedWindowsWorker)
                start.Environment["DDF_DOTNET_RESTRICTED_CHILD"] = "1";
            if (!string.IsNullOrWhiteSpace(systemRoot))
                start.Environment["SystemRoot"] = systemRoot;
            if (!Path.IsPathRooted(start.FileName) &&
                !string.IsNullOrWhiteSpace(path))
                start.Environment["PATH"] = path;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
