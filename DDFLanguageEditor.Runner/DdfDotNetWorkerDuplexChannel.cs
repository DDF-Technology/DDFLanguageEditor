using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Compiler;

namespace DDFLanguageEditor.Runner
{
    internal sealed class DdfDotNetWorkerDuplexChannel
    {
        private readonly SemaphoreSlim synchronization = new(1, 1);
        private readonly DdfDotNetWorkerDuplexTranscript transcript = new();
        private readonly string correlationId;
        private Func<DdfDotNetWorkerDebugPause> cooperativeDebugSnapshotProvider;
        private int nextSequence = 1;
        private int nextRequestId = 1;

        internal DdfDotNetWorkerDuplexChannel(
            DdfDotNetWorkerDuplexFrame startFrame)
        {
            DdfDotNetContractIssue issue = transcript.Accept(startFrame);
            if (issue != null) throw new InvalidDataException(issue.Message);
            correlationId = startFrame.CorrelationId;
        }

        internal object Invoke(string capabilityId,
            IReadOnlyList<object> arguments, CancellationToken cancellationToken)
            => InvokeAsync(capabilityId, arguments, cancellationToken)
                .AsTask().GetAwaiter().GetResult();

        internal void SetCooperativeDebugSnapshotProvider(
            Func<DdfDotNetWorkerDebugPause> provider) =>
            cooperativeDebugSnapshotProvider = provider;

        internal async ValueTask<object> InvokeAsync(string capabilityId,
            IReadOnlyList<object> arguments, CancellationToken cancellationToken)
        {
            await synchronization.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string requestId = "capability-" + nextRequestId
                    .ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                nextRequestId++;
                var request = new DdfDotNetWorkerDuplexFrame
                {
                    Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityRequest,
                    CorrelationId = correlationId,
                    Sequence = nextSequence,
                    RequestId = requestId,
                    CapabilityId = capabilityId,
                    ArgumentsJson = (arguments ?? Array.Empty<object>())
                        .Select(value => JsonSerializer.Serialize(value))
                        .ToList(),
                    CooperativeDebugSnapshot = capabilityId ==
                        DdfDotNetWorkerBrokerCapabilities.NextDebugCommand
                        ? null : cooperativeDebugSnapshotProvider?.Invoke()
                };
                Accept(request);
                await WriteFrameAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                string line = await Console.In.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (line == null || line.Length >
                        DdfDotNetWorkerDuplexProtocol.MaxFrameCharacters)
                    throw new InvalidDataException(
                        "Risposta capability host assente o oltre quota.");
                DdfDotNetWorkerDuplexFrame response =
                    DdfDotNetWorkerDuplexProtocol.DeserializeFrame(line);
                Accept(response);
                if (response.Kind !=
                        DdfDotNetWorkerDuplexFrameKind.CapabilityResponse ||
                    !string.Equals(response.RequestId, requestId,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Risposta capability host non correlata.");
                if (!response.CapabilitySucceeded)
                    throw new InvalidOperationException(
                        (response.ErrorCode ?? "DDFC548") + ": " +
                        (response.ErrorMessage ??
                         "Capability live rifiutata dall'host."));
                using JsonDocument result = JsonDocument.Parse(
                    response.ResultJson);
                return result.RootElement.ValueKind switch
                {
                    JsonValueKind.String => result.RootElement.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Number when result.RootElement.TryGetInt64(
                        out long integer) => integer,
                    JsonValueKind.Number => result.RootElement.GetDouble(),
                    _ => throw new InvalidDataException(
                        "La capability live ha restituito un tipo non scalare.")
                };
            }
            finally { synchronization.Release(); }
        }

        internal async Task WriteCompleteAsync(
            DdfDotNetWorkerResponse response,
            CancellationToken cancellationToken = default)
        {
            var frame = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.Complete,
                CorrelationId = correlationId,
                Sequence = nextSequence,
                FinalResponse = response
            };
            Accept(frame);
            await Console.Out.WriteLineAsync(
                DdfDotNetWorkerDuplexProtocol.SerializeFrame(frame));
            await Console.Out.FlushAsync(cancellationToken);
        }

        private void Accept(DdfDotNetWorkerDuplexFrame frame)
        {
            DdfDotNetContractIssue issue = transcript.Accept(frame);
            if (issue != null) throw new InvalidDataException(issue.Message);
            nextSequence = frame.Sequence + 1;
        }

        private static async ValueTask WriteFrameAsync(
            DdfDotNetWorkerDuplexFrame frame,
            CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync(
                DdfDotNetWorkerDuplexProtocol.SerializeFrame(frame));
            await Console.Out.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
