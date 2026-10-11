using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerInteractiveDebugHandler :
        IDdfDotNetWorkerDuplexCapabilityHandler
    {
        private readonly Func<DdfDotNetWorkerDebugPause, CancellationToken,
            ValueTask<DdfDotNetDebugCommand>> nextCommandAsync;
        private int pauseCount;

        public DdfDotNetWorkerInteractiveDebugHandler(
            Func<DdfDotNetWorkerDebugPause, CancellationToken,
                ValueTask<DdfDotNetDebugCommand>> nextCommandAsync)
        {
            this.nextCommandAsync = nextCommandAsync ?? throw new
                ArgumentNullException(nameof(nextCommandAsync));
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
            return DdfDotNetWorkerDuplexCapabilityResult.Succeeded(
                JsonSerializer.Serialize(command.ToString()));
        }

        private static bool ValidPause(DdfDotNetWorkerDebugPause pause) =>
            pause?.Location != null &&
            !string.IsNullOrWhiteSpace(pause.Location.DocumentPath) &&
            pause.Location.Start >= 0 && pause.Location.Length >= 1 &&
            pause.Instructions >= 0 && (pause.Frames?.Count ?? 0) <= 128;
    }
}
