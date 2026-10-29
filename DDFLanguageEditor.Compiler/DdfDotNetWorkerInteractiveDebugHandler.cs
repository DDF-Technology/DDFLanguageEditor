using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerDebugResume
    {
        public DdfDotNetDebugCommand Command { get; set; }
        public List<DdfDotNetWorkerDebugBreakpoint> Breakpoints { get; set; }

        public static bool TryParse(string value, out DdfDotNetWorkerDebugResume resume)
        {
            resume = null;
            if (Enum.TryParse(value, false, out DdfDotNetDebugCommand legacy) &&
                Enum.IsDefined(legacy))
            {
                resume = new DdfDotNetWorkerDebugResume { Command = legacy };
                return true;
            }
            try { resume = JsonSerializer.Deserialize<DdfDotNetWorkerDebugResume>(value); }
            catch (Exception) { return false; }
            if (resume == null || !Enum.IsDefined(resume.Command) ||
                resume.Breakpoints == null || resume.Breakpoints.Count > 128 ||
                resume.Breakpoints.Any(point => point == null ||
                    string.IsNullOrWhiteSpace(point.SourcePath) ||
                    Path.IsPathRooted(point.SourcePath) ||
                    point.SourcePath.Split('/', '\\').Any(segment =>
                        segment == ".." || segment == ".") ||
                    point.Start < 0 || point.Length < 1))
            {
                resume = null;
                return false;
            }
            return true;
        }
    }

    public sealed class DdfDotNetWorkerInteractiveDebugHandler :
        IDdfDotNetWorkerDuplexCapabilityHandler
    {
        private readonly Func<DdfDotNetWorkerDebugPause, CancellationToken,
            ValueTask<DdfDotNetDebugCommand>> nextCommandAsync;
        private readonly Func<IReadOnlyList<DdfDotNetWorkerDebugBreakpoint>> breakpointProvider;
        private int pauseCount;

        public DdfDotNetWorkerInteractiveDebugHandler(
            Func<DdfDotNetWorkerDebugPause, CancellationToken,
                ValueTask<DdfDotNetDebugCommand>> nextCommandAsync,
            Func<IReadOnlyList<DdfDotNetWorkerDebugBreakpoint>> breakpointProvider = null)
        {
            this.nextCommandAsync = nextCommandAsync ?? throw new
                ArgumentNullException(nameof(nextCommandAsync));
            this.breakpointProvider = breakpointProvider;
        }

        public int PauseCount => Volatile.Read(ref pauseCount);

        public async ValueTask<DdfDotNetWorkerDuplexCapabilityResult>
            InvokeAsync(string capabilityId,
                IReadOnlyList<string> argumentsJson,
                CancellationToken cancellationToken)
        {
            if (!string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                    StringComparison.Ordinal) || argumentsJson?.Count != 1)
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC550", "Richiesta debug interattiva non valida.");
            DdfDotNetWorkerDebugPause pause;
            try
            {
                pause = JsonSerializer.Deserialize<DdfDotNetWorkerDebugPause>(
                    argumentsJson[0]);
            }
            catch (JsonException exception)
            {
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC550", "Snapshot debug interattivo non leggibile: " +
                    exception.Message);
            }
            if (!ValidPause(pause))
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC550", "Snapshot debug interattivo non valido.");
            Interlocked.Increment(ref pauseCount);
            DdfDotNetDebugCommand command = await nextCommandAsync(pause,
                cancellationToken);
            if (!Enum.IsDefined(command))
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC550", "Comando debug interattivo non valido.");
            string response = breakpointProvider == null
                ? command.ToString()
                : JsonSerializer.Serialize(new DdfDotNetWorkerDebugResume
                {
                    Command = command,
                    Breakpoints = breakpointProvider().ToList()
                });
            if (!DdfDotNetWorkerDebugResume.TryParse(response, out _))
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC550", "Breakpoint dinamici non validi.");
            return DdfDotNetWorkerDuplexCapabilityResult.Succeeded(
                JsonSerializer.Serialize(response));
        }

        private static bool ValidPause(DdfDotNetWorkerDebugPause pause) =>
            pause?.Location != null &&
            !string.IsNullOrWhiteSpace(pause.Location.DocumentPath) &&
            pause.Location.Start >= 0 && pause.Location.Length >= 1 &&
            pause.Instructions >= 0 && (pause.Frames?.Count ?? 0) <= 128;
    }
}
