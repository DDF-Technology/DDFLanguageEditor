using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionCallTicket
    {
        internal DdfExtensionCallTicket(Guid callId, string requestJson)
        {
            CallId = callId;
            RequestJson = requestJson;
        }

        public Guid CallId { get; }
        public string RequestJson { get; }
    }

    public sealed class DdfExtensionRequestFrame
    {
        internal DdfExtensionRequestFrame(Guid callId, DdfExtensionCallRequest request)
        {
            CallId = callId;
            Request = request;
        }

        public Guid CallId { get; }
        public DdfExtensionCallRequest Request { get; }
    }

    // Correlation wrapper around the existing 1.0 call/response contracts.
    public static class DdfExtensionCallFrameCodec
    {
        public const string SchemaVersion = "1.0";
        private const int MaximumBytes = 262144;

        public static string SerializeRequest(Guid callId, DdfExtensionCallRequest request) =>
            Serialize(callId, "request", DdfExtensionCallCodec.Serialize(request));

        public static string SerializeResponse(Guid callId, DdfExtensionCallResponse response) =>
            Serialize(callId, "response", DdfExtensionCallResponseCodec.Serialize(response));

        public static DdfExtensionRequestFrame ParseRequest(string json,
            DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            (Guid id, string payload) = Parse(json, "request");
            return new DdfExtensionRequestFrame(id,
                DdfExtensionCallCodec.Parse(payload, catalog));
        }

        internal static (Guid Id, string Payload) ParseResponse(string json) =>
            Parse(json, "response");

        private static string Serialize(Guid callId, string member, string payload)
        {
            if (callId == Guid.Empty)
                throw new ArgumentException("Call ID richiesto.", nameof(callId));
            using JsonDocument document = JsonDocument.Parse(payload);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("callId", callId.ToString("N"));
                writer.WritePropertyName(member);
                document.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumBytes)
                throw new ArgumentException("Frame oltre quota.", nameof(payload));
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static (Guid Id, string Payload) Parse(string json, string member)
        {
            if (json == null || json.Length > MaximumBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new ArgumentException("Frame assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 36, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "callId", member);
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion)
                throw new FormatException("Versione frame non supportata.");
            string idText = DdfExtensionCallCodec.ReadString(root, "callId");
            if (idText.Length != 32 || !Guid.TryParseExact(idText, "N", out Guid id) ||
                id == Guid.Empty || id.ToString("N") != idText)
                throw new FormatException("Call ID non canonico.");
            JsonElement payload = root.GetProperty(member);
            if (payload.ValueKind != JsonValueKind.Object)
                throw new FormatException("Payload frame non valido.");
            return (id, payload.GetRawText());
        }
    }
}
