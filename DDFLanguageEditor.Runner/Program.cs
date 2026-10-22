using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Runner
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            bool reducedUserTokenApplied = false;
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    reducedUserTokenApplied = DdfWindowsRestrictedLauncher
                        .IsCurrentTokenRestricted();
                    if (!reducedUserTokenApplied &&
                        !DdfWindowsRestrictedLauncher.IsChild)
                        return DdfWindowsRestrictedLauncher.RunRestrictedChild();
                    reducedUserTokenApplied = DdfWindowsRestrictedLauncher
                        .IsCurrentTokenRestricted();
                }
                catch (Exception exception)
                {
                    await Console.Out.WriteAsync(
                        DdfDotNetWorkerProtocol.SerializeResponse(Failure(
                            string.Empty, "DDFC537",
                            "Avvio o attestazione del token ridotto falliti: " +
                            exception.Message)));
                    return 0;
                }
            }
            DdfDotNetWorkerRequest request = null;
            DdfDotNetWorkerDuplexChannel duplexChannel = null;
            DdfDotNetWorkerResponse response;
            try
            {
                string initialMessage = await ReadInitialMessage();
                DdfDotNetWorkerDuplexFrame startFrame = TryReadStartFrame(
                    initialMessage);
                if (startFrame == null)
                    request = DdfDotNetWorkerProtocol.DeserializeRequest(
                        initialMessage);
                else
                {
                    request = startFrame.StartRequest;
                    duplexChannel = new DdfDotNetWorkerDuplexChannel(
                        startFrame);
                }
                DdfDotNetWorkerPackageResult imported =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied);
                if (!imported.Success)
                {
                    DdfDotNetContractIssue issue = imported.Issues.Count == 0
                        ? new DdfDotNetContractIssue("DDFC530",
                            "Import del package worker fallito.")
                        : imported.Issues[0];
                    response = Failure(request?.CorrelationId, issue.Code,
                        issue.Message);
                }
                else
                    response = await ExecuteAsync(request, imported.Package,
                        reducedUserTokenApplied, duplexChannel);
            }
            catch (Exception exception)
            {
                response = Failure(request?.CorrelationId, "DDFC533",
                    "Errore worker non gestito: " + exception.Message);
            }
            response.ReducedUserTokenApplied = reducedUserTokenApplied;
            if (duplexChannel == null)
                await Console.Out.WriteAsync(
                    DdfDotNetWorkerProtocol.SerializeResponse(response));
            else
                await duplexChannel.WriteCompleteAsync(response);
            return 0;
        }

        private static async Task<DdfDotNetWorkerResponse> ExecuteAsync(
            DdfDotNetWorkerRequest request, DdfDotNetWorkerPackage package,
            bool reducedUserTokenApplied,
            DdfDotNetWorkerDuplexChannel duplexChannel)
        {
            DdfDotNetStandaloneLoadResult loaded =
                DdfDotNetStandaloneLoader.Load(package.Bundle,
                    package.ReferenceSet, package.Environment);
            if (!loaded.Success)
            {
                DdfDotNetContractIssue issue = loaded.Issues[0];
                return Failure(request.CorrelationId, issue.Code, issue.Message);
            }
            using DdfDotNetStandaloneSession session = loaded.Session;
            var output = new List<string>();
            int outputCharacters = 0;
            DdfDotNetWorkerBrokerProvider brokerProvider =
                DdfDotNetWorkerBrokerProvider.Create(package, duplexChannel);
            var input = new Queue<string>(package.BufferedInput);
            int consumedInputCount = 0;
            int interactiveInputCount = 0;
            if (package.InteractiveInput != null && duplexChannel == null)
                return Failure(request.CorrelationId, "DDFC549",
                    "L'input interattivo richiede il canale worker duplex.");
            try
            {
                var debugTrace = new List<DdfDotNetWorkerDebugPause>();
                using var cancellation = new CancellationTokenSource();
                Func<string> readInput = package.BufferedInput.Count == 0 &&
                    package.InteractiveInput == null
                    ? null
                    : () =>
                    {
                        string value;
                        if (input.Count != 0)
                            value = input.Dequeue();
                        else if (package.InteractiveInput != null)
                        {
                            if (interactiveInputCount >=
                                package.InteractiveInput.MaxRequests)
                                throw new InvalidOperationException(
                                    "Quota input interattivo esaurita.");
                            value = duplexChannel.Invoke(
                                DdfDotNetWorkerBrokerCapabilities
                                    .ReadConsoleInput,
                                Array.Empty<object>(), cancellation.Token)
                                as string ?? string.Empty;
                            if (Encoding.UTF8.GetByteCount(value) >
                                package.InteractiveInput.MaxValueBytes)
                                throw new InvalidOperationException(
                                    "Valore input interattivo oltre quota.");
                            interactiveInputCount++;
                        }
                        else
                            throw new InvalidOperationException(
                                "Coda Input worker esaurita.");
                        consumedInputCount++;
                        return value;
                    };
                Action<string> writeOutput = value =>
                {
                    outputCharacters += value?.Length ?? 0;
                    if (output.Count >= 10000 ||
                        outputCharacters > 1024 * 1024)
                        throw new InvalidOperationException(
                            "Output worker oltre il budget IPC.");
                    output.Add(value);
                };
                DdfRuntimeExecutionResult result;
                if (package.DebugPlan == null)
                {
                    result = await session.ExecuteAsync(new DdfRuntimeContext(
                        package.MaxInstructions, cancellation.Token,
                        writeOutput, readInput, brokerProvider));
                }
                else
                {
                    result = await ExecuteDebugPlanAsync(session,
                        package.DebugPlan,
                        package.MaxInstructions, cancellation, writeOutput,
                        readInput, brokerProvider, debugTrace, duplexChannel);
                }
                if (!SupportedResult(result.Value))
                    return Failure(request.CorrelationId, "DDFC534",
                        "Il protocollo worker 1.6 restituisce soltanto valori scalari DDF.");
                return new DdfDotNetWorkerResponse
                {
                    CorrelationId = request.CorrelationId,
                    Success = true,
                    ResultJson = JsonSerializer.Serialize(result.Value),
                    Ended = result.Ended,
                    Instructions = result.Instructions,
                    Output = output,
                    BrokerFileWrites = brokerProvider == null
                        ? new List<DdfDotNetWorkerFileWrite>()
                        : new List<DdfDotNetWorkerFileWrite>(
                            brokerProvider.GetWrites()),
                    ConsumedInputCount = consumedInputCount,
                    DebugTrace = debugTrace,
                    ReducedUserTokenApplied = reducedUserTokenApplied
                };
            }
            catch (DdfRuntimeException exception)
            {
                return Failure(request.CorrelationId, exception);
            }
        }

        private static async Task<DdfRuntimeExecutionResult>
            ExecuteDebugPlanAsync(
            DdfDotNetStandaloneSession session,
            DdfDotNetWorkerDebugPlan plan, int maxInstructions,
            CancellationTokenSource cancellation, Action<string> output,
            Func<string> input, IDdfRuntimeCapabilityProvider capabilities,
            List<DdfDotNetWorkerDebugPause> trace,
            DdfDotNetWorkerDuplexChannel duplexChannel)
        {
            if (plan.Interactive && duplexChannel == null)
                throw new DdfRuntimeException("DDFC550",
                    "Il debug interattivo richiede il canale worker duplex.",
                    default);
            using DdfDotNetCooperativeDebugger debugger =
                session.CreateDebugger();
            debugger.SetBreakpoints(plan.Breakpoints.Select(value =>
                new DdfDotNetSourceBreakpoint(value.SourcePath, value.Start,
                    value.Length)));
            duplexChannel?.SetCooperativeDebugSnapshotProvider(() =>
            {
                DdfDotNetCompiledPause snapshot = debugger.LatestSnapshot;
                return snapshot == null ? null : ProjectPause(snapshot);
            });
            var context = new DdfRuntimeContext(maxInstructions,
                cancellation.Token, output, input, capabilities,
                debugObserver: debugger);
            IReadOnlyList<DdfDotNetDebugCommand> commands = plan.Commands is null
                ? Array.Empty<DdfDotNetDebugCommand>()
                : plan.Commands;
            Task<DdfRuntimeExecutionResult> execution = Task.Run(async () =>
                await session.ExecuteAsync(context), cancellation.Token);
            var wait = Stopwatch.StartNew();
            while (!execution.IsCompleted)
            {
                if (debugger.WaitForPause(TimeSpan.Zero))
                {
                    DdfDotNetCompiledPause pause = debugger.CurrentPause;
                    if (pause == null) continue;
                    DdfDotNetWorkerDebugPause projected = ProjectPause(pause);
                    trace.Add(projected);
                    int commandIndex = trace.Count - 1;
                    DdfDotNetDebugCommand command;
                    if (plan.Interactive)
                    {
                        object value = duplexChannel.Invoke(
                            DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                            new object[] { projected }, cancellation.Token);
                        if (value is not string commandName ||
                            !Enum.TryParse(commandName, ignoreCase: false,
                                out command) || !Enum.IsDefined(command))
                            throw new DdfRuntimeException("DDFC550",
                                "Comando debug interattivo non valido.",
                                default);
                    }
                    else
                        command = commandIndex < commands.Count
                            ? commands[commandIndex]
                            : DdfDotNetDebugCommand.Continue;
                    if (trace.Count >= plan.MaxPauses && command !=
                        DdfDotNetDebugCommand.Stop)
                    {
                        debugger.SetBreakpoints(Array.Empty<
                            DdfDotNetSourceBreakpoint>());
                        command = DdfDotNetDebugCommand.Continue;
                    }
                    debugger.Resume(command);
                    wait.Restart();
                    continue;
                }
                if (wait.ElapsedMilliseconds <=
                    plan.PauseTimeoutMilliseconds)
                {
                    await Task.Delay(20);
                    continue;
                }
                cancellation.Cancel();
                debugger.Dispose();
                try { await execution; }
                catch (Exception) { }
                throw new DdfRuntimeException("DDFC543",
                    "Timeout durante l'esecuzione del piano debug worker.",
                    default);
            }
            DdfRuntimeExecutionResult result;
            try { result = await execution; }
            catch (OperationCanceledException) when (plan.Interactive)
            {
                throw new DdfRuntimeException("DDFC550",
                    "Debug interattivo arrestato dall'host.", default);
            }
            if (trace.Count == 0)
                throw new DdfRuntimeException("DDFC543",
                    "Il piano debug worker non ha raggiunto alcun breakpoint.",
                    default);
            return result;
        }

        private static DdfDotNetWorkerDebugPause ProjectPause(
            DdfDotNetCompiledPause pause) => new()
        {
            Location = ProjectLocation(pause.Location),
            Instructions = pause.Instructions,
            Frames = pause.Frames.Select(frame =>
                new DdfDotNetWorkerDebugFrame
                {
                    SymbolId = frame.SymbolId,
                    Name = frame.Name,
                    Location = ProjectLocation(frame.Location),
                    Variables = frame.Variables.Select(ProjectVariable).ToList()
                }).ToList()
        };

        private static DdfDotNetWorkerSourceLocation ProjectLocation(
            DdfRuntimeSourceLocation location) => new()
        {
            DocumentPath = location.DocumentPath,
            Start = location.Start,
            Length = location.Length,
            SymbolId = location.SymbolId
        };

        private static DdfDotNetWorkerDebugVariable ProjectVariable(
            DdfDotNetCompiledVariable value) => new()
        {
            SymbolId = value.SymbolId,
            Name = value.Name,
            TypeName = value.TypeName,
            Kind = value.Kind,
            DisplayValue = value.DisplayValue,
            Children = value.Children.Select(ProjectVariable).ToList(),
            IsTruncated = value.IsTruncated,
            IsReadOnly = value.IsReadOnly,
            IsFixed = value.IsFixed
        };

        private static bool SupportedResult(object value) => value == null ||
            value is bool or byte or sbyte or short or ushort or int or uint or
                long or ulong or float or double or decimal or char or string;

        private static async Task<string> ReadInitialMessage()
        {
            string value = await Console.In.ReadLineAsync() ?? string.Empty;
            if (value.Length > DdfDotNetWorkerProtocol.MaxRequestCharacters)
                throw new InvalidDataException(
                    "Richiesta worker oltre il limite IPC.");
            return value;
        }

        private static DdfDotNetWorkerDuplexFrame TryReadStartFrame(
            string value)
        {
            try
            {
                DdfDotNetWorkerDuplexFrame frame =
                    DdfDotNetWorkerDuplexProtocol.DeserializeFrame(value);
                return frame.Kind == DdfDotNetWorkerDuplexFrameKind.Start
                    ? frame
                    : throw new InvalidDataException(
                        "Il protocollo duplex deve iniziare con Start.");
            }
            catch (JsonException) { return null; }
            catch (NotSupportedException) { return null; }
        }

        private static DdfDotNetWorkerResponse Failure(string correlationId,
            string code, string message) => new()
        {
            CorrelationId = correlationId ?? string.Empty,
            Success = false,
            ErrorCode = code ?? "DDFC533",
            ErrorMessage = message ?? "Esecuzione worker fallita."
        };

        private static DdfDotNetWorkerResponse Failure(string correlationId,
            DdfRuntimeException exception)
        {
            DdfDotNetWorkerResponse response = Failure(correlationId,
                exception?.Code, exception?.Message);
            if (exception == null) return response;
            response.ErrorLocation = Location(exception.Location);
            response.ErrorStack = exception.Frames.Select(frame =>
                new DdfDotNetWorkerErrorFrame
                {
                    SymbolId = frame.FunctionSymbolId,
                    Declaration = Location(frame.Declaration),
                    Location = Location(frame.Location)
                }).ToList();
            return response;
        }

        private static DdfDotNetWorkerSourceLocation Location(
            DdfRuntimeSourceLocation location) => new()
        {
            DocumentPath = location.DocumentPath,
            Start = location.Start,
            Length = location.Length,
            SymbolId = location.SymbolId
        };
    }
}
