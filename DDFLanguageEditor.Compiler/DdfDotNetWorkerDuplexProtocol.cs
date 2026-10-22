using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerDuplexCapabilityResult
    {
        private DdfDotNetWorkerDuplexCapabilityResult(bool success,
            string resultJson, string errorCode, string errorMessage)
        {
            Success = success;
            ResultJson = resultJson;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public string ResultJson { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        public static DdfDotNetWorkerDuplexCapabilityResult Succeeded(
            string resultJson) => new(true, resultJson, null, null);

        public static DdfDotNetWorkerDuplexCapabilityResult Failed(
            string errorCode, string errorMessage) => new(false, null,
                errorCode, errorMessage);
    }

    public interface IDdfDotNetWorkerDuplexCapabilityHandler
    {
        ValueTask<DdfDotNetWorkerDuplexCapabilityResult> InvokeAsync(
            string capabilityId, IReadOnlyList<string> argumentsJson,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Receives correlated worker suspension notifications without exposing the
    /// worker task, thread or generated C# state machine to the host.
    /// </summary>
    public interface IDdfDotNetWorkerCooperativeExecutionObserver
    {
        void OnAwaitingCapability(
            DdfDotNetWorkerCooperativeSuspension suspension);
        void OnCapabilityResumed(
            DdfDotNetWorkerCooperativeSuspension suspension);
    }

    public sealed class DdfDotNetWorkerCooperativeSuspension
    {
        internal DdfDotNetWorkerCooperativeSuspension(
            DdfDotNetWorkerDuplexFrame frame)
        {
            CorrelationId = frame.CorrelationId;
            RequestId = frame.RequestId;
            CapabilityId = frame.CapabilityId;
            ArgumentsJson = (frame.ArgumentsJson ?? new List<string>())
                .ToList().AsReadOnly();
            DebugSnapshot = frame.CooperativeDebugSnapshot;
        }

        public string CorrelationId { get; }
        public string RequestId { get; }
        public string CapabilityId { get; }
        public IReadOnlyList<string> ArgumentsJson { get; }
        public DdfDotNetWorkerDebugPause DebugSnapshot { get; }
    }

    /// <summary>
    /// Routes the single duplex channel to explicitly registered host-side
    /// capabilities. Registrations are exact and deny-by-default so an editor can
    /// combine interactive debug, console input and governed network access
    /// without granting a handler visibility of unrelated calls.
    /// </summary>
    public sealed class DdfDotNetWorkerDuplexCapabilityRouter :
        IDdfDotNetWorkerDuplexCapabilityHandler
    {
        private readonly IReadOnlyDictionary<string,
            IDdfDotNetWorkerDuplexCapabilityHandler> handlers;

        public DdfDotNetWorkerDuplexCapabilityRouter(
            IEnumerable<KeyValuePair<string,
                IDdfDotNetWorkerDuplexCapabilityHandler>> registrations)
        {
            var values = new Dictionary<string,
                IDdfDotNetWorkerDuplexCapabilityHandler>(StringComparer.Ordinal);
            foreach (KeyValuePair<string,
                         IDdfDotNetWorkerDuplexCapabilityHandler> registration in
                     registrations ?? Array.Empty<KeyValuePair<string,
                         IDdfDotNetWorkerDuplexCapabilityHandler>>())
            {
                string capabilityId = (registration.Key ?? string.Empty).Trim();
                if (capabilityId.Length == 0 || capabilityId.Length > 512)
                    throw new ArgumentException(
                        "L'identificatore capability duplex non è valido.",
                        nameof(registrations));
                if (registration.Value == null)
                    throw new ArgumentException(
                        "Ogni capability duplex richiede un handler.",
                        nameof(registrations));
                if (!values.TryAdd(capabilityId, registration.Value))
                    throw new ArgumentException(
                        "Capability duplex registrata più volte: " +
                        capabilityId + ".", nameof(registrations));
            }
            handlers = values;
        }

        public IReadOnlyCollection<string> CapabilityIds => handlers.Keys
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public ValueTask<DdfDotNetWorkerDuplexCapabilityResult> InvokeAsync(
            string capabilityId, IReadOnlyList<string> argumentsJson,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(capabilityId) ||
                !handlers.TryGetValue(capabilityId,
                    out IDdfDotNetWorkerDuplexCapabilityHandler handler))
                return ValueTask.FromResult(
                    DdfDotNetWorkerDuplexCapabilityResult.Failed("DDFC551",
                        "Capability duplex non registrata nell'host."));
            return handler.InvokeAsync(capabilityId, argumentsJson,
                cancellationToken);
        }
    }

    public enum DdfDotNetWorkerDuplexFrameKind
    {
        Start,
        CapabilityRequest,
        CapabilityResponse,
        Complete,
        Abort
    }

    public sealed class DdfDotNetWorkerDuplexFrame
    {
        public const string FormatValue = "ddf-dotnet-worker-frame";
        public const string FrameVersionValue = "1.1";

        [JsonRequired]
        public string Format { get; set; } = FormatValue;
        [JsonRequired]
        public string FrameVersion { get; set; } = FrameVersionValue;
        public DdfDotNetWorkerDuplexFrameKind Kind { get; set; }
        public string CorrelationId { get; set; }
        public int Sequence { get; set; }
        public string RequestId { get; set; }
        public DdfDotNetWorkerRequest StartRequest { get; set; }
        public string CapabilityId { get; set; }
        public List<string> ArgumentsJson { get; set; } = new();
        public bool CapabilitySucceeded { get; set; }
        public string ResultJson { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public DdfDotNetWorkerResponse FinalResponse { get; set; }
        public DdfDotNetWorkerDebugPause CooperativeDebugSnapshot { get; set; }
    }

    public static class DdfDotNetWorkerDuplexProtocol
    {
        public const int MaxFrameCharacters = 4 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string SerializeFrame(DdfDotNetWorkerDuplexFrame frame)
        {
            DdfDotNetContractIssue issue = ValidateShape(frame);
            if (issue != null) throw new NotSupportedException(issue.Message);
            string value = JsonSerializer.Serialize(frame, JsonOptions);
            int limit = frame.Kind == DdfDotNetWorkerDuplexFrameKind.Start
                ? DdfDotNetWorkerProtocol.MaxRequestCharacters
                : MaxFrameCharacters;
            if (value.Length > limit)
                throw new NotSupportedException(
                    "Frame IPC duplex oltre il limite consentito.");
            return value;
        }

        public static DdfDotNetWorkerDuplexFrame DeserializeFrame(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > DdfDotNetWorkerProtocol.MaxRequestCharacters ||
                value.Contains('\r') || value.Contains('\n'))
                throw new NotSupportedException(
                    "Frame IPC duplex assente o oltre quota.");
            DdfDotNetWorkerDuplexFrame frame = JsonSerializer.Deserialize<
                DdfDotNetWorkerDuplexFrame>(value, JsonOptions);
            DdfDotNetContractIssue issue = ValidateShape(frame);
            if (issue != null) throw new NotSupportedException(issue.Message);
            int limit = frame.Kind == DdfDotNetWorkerDuplexFrameKind.Start
                ? DdfDotNetWorkerProtocol.MaxRequestCharacters
                : MaxFrameCharacters;
            if (value.Length > limit)
                throw new NotSupportedException(
                    "Frame IPC duplex oltre il limite consentito.");
            return frame;
        }

        internal static DdfDotNetContractIssue ValidateShape(
            DdfDotNetWorkerDuplexFrame frame)
        {
            if (frame == null || frame.Format !=
                    DdfDotNetWorkerDuplexFrame.FormatValue ||
                frame.FrameVersion !=
                    DdfDotNetWorkerDuplexFrame.FrameVersionValue ||
                !Enum.IsDefined(frame.Kind) ||
                string.IsNullOrWhiteSpace(frame.CorrelationId) ||
                frame.CorrelationId.Length > 128 || frame.Sequence < 0)
                return Issue("Frame IPC duplex assente o non supportato.");
            bool hasRequestId = !string.IsNullOrWhiteSpace(frame.RequestId) &&
                frame.RequestId.Length <= 128;
            bool hasCapability = !string.IsNullOrWhiteSpace(
                frame.CapabilityId) && frame.CapabilityId.Length <= 512;
            bool valid = frame.Kind switch
            {
                DdfDotNetWorkerDuplexFrameKind.Start => frame.Sequence == 0 &&
                    ValidStart(frame) && !hasRequestId &&
                    !hasCapability && frame.FinalResponse == null &&
                    EmptyAuxiliaryPayload(frame),
                DdfDotNetWorkerDuplexFrameKind.CapabilityRequest =>
                    frame.StartRequest == null && hasRequestId && hasCapability &&
                    frame.FinalResponse == null &&
                    !frame.CapabilitySucceeded && frame.ResultJson == null &&
                    frame.ErrorCode == null && frame.ErrorMessage == null &&
                    (frame.ArgumentsJson?.Count ?? 0) <= 64 &&
                    (frame.ArgumentsJson ?? new List<string>()).All(IsJsonValue) &&
                    (frame.CooperativeDebugSnapshot == null ||
                     frame.CapabilityId !=
                        DdfDotNetWorkerBrokerCapabilities.NextDebugCommand &&
                     ValidDebugSnapshot(frame.CooperativeDebugSnapshot)),
                DdfDotNetWorkerDuplexFrameKind.CapabilityResponse =>
                    frame.StartRequest == null && hasRequestId &&
                    !hasCapability && frame.FinalResponse == null &&
                    frame.CooperativeDebugSnapshot == null &&
                    (frame.ArgumentsJson?.Count ?? 0) == 0 &&
                    (frame.CapabilitySucceeded
                        ? IsJsonValue(frame.ResultJson) &&
                            string.IsNullOrEmpty(frame.ErrorCode) &&
                            string.IsNullOrEmpty(frame.ErrorMessage)
                        : !string.IsNullOrWhiteSpace(frame.ErrorCode) &&
                            frame.ResultJson == null &&
                            frame.ErrorCode.Length <= 64 &&
                            (frame.ErrorMessage?.Length ?? 0) <= 4096),
                DdfDotNetWorkerDuplexFrameKind.Complete =>
                    frame.StartRequest == null && !hasRequestId &&
                    !hasCapability && ValidComplete(frame) &&
                    EmptyAuxiliaryPayload(frame),
                DdfDotNetWorkerDuplexFrameKind.Abort =>
                    frame.StartRequest == null && !hasRequestId &&
                    !hasCapability && frame.FinalResponse == null &&
                    frame.CooperativeDebugSnapshot == null &&
                    !frame.CapabilitySucceeded && frame.ResultJson == null &&
                    (frame.ArgumentsJson?.Count ?? 0) == 0 &&
                    !string.IsNullOrWhiteSpace(frame.ErrorCode) &&
                    frame.ErrorCode.Length <= 64 &&
                    (frame.ErrorMessage?.Length ?? 0) <= 4096,
                _ => false
            };
            return valid ? null : Issue(
                "Payload del frame IPC duplex incoerente col tipo.");
        }

        private static bool IsJsonValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 * 1024)
                return false;
            try
            {
                using JsonDocument document = JsonDocument.Parse(value);
                return document.RootElement.ValueKind != JsonValueKind.Undefined;
            }
            catch (JsonException) { return false; }
        }

        private static bool ValidDebugSnapshot(
            DdfDotNetWorkerDebugPause snapshot)
        {
            if (snapshot?.Location == null || snapshot.Instructions < 0 ||
                !ValidLocation(snapshot.Location) ||
                (snapshot.Frames?.Count ?? 0) > 128)
                return false;
            return (snapshot.Frames ?? new List<DdfDotNetWorkerDebugFrame>())
                .All(frame => frame != null && ValidLocation(frame.Location) &&
                    (frame.SymbolId?.Length ?? 0) <= 512 &&
                    (frame.Name?.Length ?? 0) <= 512 &&
                    ValidVariables(frame.Variables, 0));
        }

        private static bool ValidVariables(
            IReadOnlyList<DdfDotNetWorkerDebugVariable> variables, int depth)
        {
            if (depth > 8 || (variables?.Count ?? 0) > 512) return false;
            return (variables ?? Array.Empty<DdfDotNetWorkerDebugVariable>())
                .All(value => value != null &&
                    (value.SymbolId?.Length ?? 0) <= 512 &&
                    (value.Name?.Length ?? 0) <= 512 &&
                    (value.TypeName?.Length ?? 0) <= 512 &&
                    (value.DisplayValue?.Length ?? 0) <= 4096 &&
                    ValidVariables(value.Children, depth + 1));
        }

        private static bool ValidLocation(
            DdfDotNetWorkerSourceLocation location) => location != null &&
            (location.DocumentPath?.Length ?? 0) <= 4096 &&
            (location.SymbolId?.Length ?? 0) <= 512 &&
            location.Start >= 0 && location.Length >= 0;

        private static bool ValidStart(DdfDotNetWorkerDuplexFrame frame) =>
            frame.StartRequest != null && frame.StartRequest.Format ==
                DdfDotNetWorkerRequest.FormatValue &&
            frame.StartRequest.ProtocolVersion ==
                DdfDotNetWorkerRequest.ProtocolVersionValue &&
            string.Equals(frame.StartRequest.CorrelationId,
                frame.CorrelationId, StringComparison.Ordinal);

        private static bool ValidComplete(DdfDotNetWorkerDuplexFrame frame) =>
            frame.FinalResponse != null && frame.FinalResponse.Format ==
                DdfDotNetWorkerResponse.FormatValue &&
            frame.FinalResponse.ProtocolVersion ==
                DdfDotNetWorkerResponse.ProtocolVersionValue &&
            string.Equals(frame.FinalResponse.CorrelationId,
                frame.CorrelationId, StringComparison.Ordinal);

        private static bool EmptyAuxiliaryPayload(
            DdfDotNetWorkerDuplexFrame frame) =>
            !frame.CapabilitySucceeded && frame.ResultJson == null &&
            frame.ErrorCode == null && frame.ErrorMessage == null &&
            (frame.ArgumentsJson?.Count ?? 0) == 0 &&
            frame.CooperativeDebugSnapshot == null;

        private static DdfDotNetContractIssue Issue(string message) =>
            new("DDFC546", message);
    }

    public sealed class DdfDotNetWorkerDuplexTranscript
    {
        private readonly HashSet<string> requestIds = new(
            StringComparer.Ordinal);
        private string correlationId;
        private string pendingRequestId;
        private int nextSequence;
        private int capabilityCalls;
        private bool started;
        private bool completed;

        public DdfDotNetContractIssue Accept(
            DdfDotNetWorkerDuplexFrame frame)
        {
            DdfDotNetContractIssue shape = DdfDotNetWorkerDuplexProtocol
                .ValidateShape(frame);
            if (shape != null) return shape;
            if (completed || frame.Sequence != nextSequence || started &&
                !string.Equals(frame.CorrelationId, correlationId,
                    StringComparison.Ordinal))
                return Issue("Sequenza, correlazione o lifecycle duplex non valido.");
            if (!started)
            {
                if (frame.Kind != DdfDotNetWorkerDuplexFrameKind.Start)
                    return Issue("Il transcript duplex deve iniziare con Start.");
                started = true;
                correlationId = frame.CorrelationId;
            }
            else if (frame.Kind ==
                DdfDotNetWorkerDuplexFrameKind.CapabilityRequest)
            {
                if (pendingRequestId != null || capabilityCalls >= 256 ||
                    !requestIds.Add(frame.RequestId))
                    return Issue("Richiesta capability duplex duplicata o oltre quota.");
                pendingRequestId = frame.RequestId;
                capabilityCalls++;
            }
            else if (frame.Kind ==
                DdfDotNetWorkerDuplexFrameKind.CapabilityResponse)
            {
                if (!string.Equals(pendingRequestId, frame.RequestId,
                        StringComparison.Ordinal))
                    return Issue("Risposta capability duplex non correlata.");
                pendingRequestId = null;
            }
            else if (frame.Kind is DdfDotNetWorkerDuplexFrameKind.Complete or
                     DdfDotNetWorkerDuplexFrameKind.Abort)
            {
                if (pendingRequestId != null)
                    return Issue("Il transcript termina con una capability pendente.");
                completed = true;
            }
            else
                return Issue("Transizione del transcript duplex non valida.");
            nextSequence++;
            return null;
        }

        public bool Started => started;
        public bool Completed => completed;
        public int CapabilityCalls => capabilityCalls;

        private static DdfDotNetContractIssue Issue(string message) =>
            new("DDFC546", message);
    }
}
