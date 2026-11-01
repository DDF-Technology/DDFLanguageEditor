using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionSourceOperationKind { Semantic, Build }

    public sealed class DdfExtensionSourceTicket
    {
        internal DdfExtensionSourceTicket(Guid callId, DdfExtensionSourceOperationKind kind,
            string requestJson)
        {
            CallId = callId;
            Kind = kind;
            RequestJson = requestJson;
        }

        public Guid CallId { get; }
        public DdfExtensionSourceOperationKind Kind { get; }
        public string RequestJson { get; }
    }

    public sealed class DdfExtensionSourceRequestFrame
    {
        internal DdfExtensionSourceRequestFrame(Guid callId,
            DdfExtensionSourceOperationKind kind,
            DdfExtensionSemanticRequest semantic,
            DdfExtensionBuildRequest build)
        {
            CallId = callId;
            Kind = kind;
            Semantic = semantic;
            Build = build;
        }

        public Guid CallId { get; }
        public DdfExtensionSourceOperationKind Kind { get; }
        public DdfExtensionSemanticRequest Semantic { get; }
        public DdfExtensionBuildRequest Build { get; }
    }

    // Correlation envelope, not transport authentication or execution permission.
    public static class DdfExtensionSourceFrameCodec
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumWireBytes = 34 * 1024 * 1024;

        public static string SerializeRequest(Guid callId,
            DdfExtensionSemanticRequest request) =>
            Serialize(callId, DdfExtensionSourceOperationKind.Semantic, "request",
                DdfExtensionSourceRequestCodec.SerializeSemantic(request));

        public static string SerializeRequest(Guid callId,
            DdfExtensionBuildRequest request) =>
            Serialize(callId, DdfExtensionSourceOperationKind.Build, "request",
                DdfExtensionSourceRequestCodec.SerializeBuild(request));

        public static string SerializeResponse(Guid callId,
            DdfExtensionSemanticResponse response) =>
            Serialize(callId, DdfExtensionSourceOperationKind.Semantic, "response",
                DdfExtensionSemanticResponseCodec.Serialize(response));

        public static string SerializeResponse(Guid callId,
            DdfExtensionBuildResponse response) =>
            Serialize(callId, DdfExtensionSourceOperationKind.Build, "response",
                DdfExtensionBuildResponseCodec.Serialize(response));

        public static DdfExtensionSourceRequestFrame ParseRequest(string json,
            DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            (Guid id, DdfExtensionSourceOperationKind kind, string payload) =
                Parse(json, "request");
            return kind == DdfExtensionSourceOperationKind.Semantic
                ? new DdfExtensionSourceRequestFrame(id, kind,
                    DdfExtensionSourceRequestCodec.ParseSemantic(payload, catalog), null)
                : new DdfExtensionSourceRequestFrame(id, kind, null,
                    DdfExtensionSourceRequestCodec.ParseBuild(payload, catalog));
        }

        internal static (Guid Id, DdfExtensionSourceOperationKind Kind, string Payload)
            ParseResponse(string json) => Parse(json, "response");

        private static string Serialize(Guid callId, DdfExtensionSourceOperationKind kind,
            string member, string payload)
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
                writer.WriteString("kind", kind.ToString());
                writer.WritePropertyName(member);
                document.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumWireBytes)
                throw new ArgumentException("Frame sorgente oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static (Guid Id, DdfExtensionSourceOperationKind Kind, string Payload)
            Parse(string json, string member)
        {
            if (json == null || json.Length > MaximumWireBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumWireBytes)
                throw new ArgumentException("Frame sorgente assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 12, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "callId",
                "kind", member);
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion)
                throw new FormatException("Versione frame sorgente non supportata.");
            string idText = DdfExtensionCallCodec.ReadString(root, "callId");
            if (idText.Length != 32 || !Guid.TryParseExact(idText, "N", out Guid id) ||
                id == Guid.Empty || id.ToString("N") != idText)
                throw new FormatException("Call ID non canonico.");
            string kindName = DdfExtensionCallCodec.ReadString(root, "kind");
            if (!Enum.TryParse(kindName, false, out DdfExtensionSourceOperationKind kind) ||
                !Enum.IsDefined(typeof(DdfExtensionSourceOperationKind), kind) ||
                kind.ToString() != kindName)
                throw new FormatException("Tipo richiesta sorgente non valido.");
            JsonElement payload = root.GetProperty(member);
            if (payload.ValueKind != JsonValueKind.Object)
                throw new FormatException("Payload frame sorgente non valido.");
            return (id, kind, payload.GetRawText());
        }
    }
}
