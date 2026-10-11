using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerInteractiveInputHandler :
        IDdfDotNetWorkerDuplexCapabilityHandler
    {
        private readonly Func<CancellationToken, ValueTask<string>> readAsync;
        private int requestCount;

        public DdfDotNetWorkerInteractiveInputHandler(
            Func<CancellationToken, ValueTask<string>> readAsync)
        {
            this.readAsync = readAsync ?? throw new ArgumentNullException(
                nameof(readAsync));
        }

        public int RequestCount => Volatile.Read(ref requestCount);

        public async ValueTask<DdfDotNetWorkerDuplexCapabilityResult>
            InvokeAsync(string capabilityId,
                IReadOnlyList<string> argumentsJson,
                CancellationToken cancellationToken)
        {
            if (!string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                    StringComparison.Ordinal) ||
                (argumentsJson?.Count ?? 0) != 0)
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC549", "Richiesta input interattivo non valida.");
            Interlocked.Increment(ref requestCount);
            string value = await readAsync(cancellationToken);
            return DdfDotNetWorkerDuplexCapabilityResult.Succeeded(
                JsonSerializer.Serialize(value ?? string.Empty));
        }
    }
}
