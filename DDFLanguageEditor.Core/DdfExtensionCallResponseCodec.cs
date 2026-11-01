using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // A response is data from an untrusted worker, not a security decision.
    public static class DdfExtensionCallResponseCodec
    {
        public const string SchemaVersion = "1.0";

        public static string Serialize(DdfExtensionCallResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pointId", response.Point.Id);
                writer.WriteString("status", response.Status.ToString());
                if (response.Status == DdfExtensionCallStatus.Succeeded)
                {
                    writer.WritePropertyName("value");
                    DdfExtensionCallCodec.WriteValue(writer, response.Value);
                }
                else writer.WriteString("errorId", response.ErrorId);
                writer.WriteEndObject();
            }
            if (stream.Length > DdfExtensionCallCodec.MaximumBytes)
                throw new ArgumentException("Risposta serializzata oltre quota.", nameof(response));
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static DdfExtensionCallResponse Parse(string json,
            DdfExtensionCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (json == null || json.Length > DdfExtensionCallCodec.MaximumBytes ||
                Encoding.UTF8.GetByteCount(json) > DdfExtensionCallCodec.MaximumBytes)
                throw new ArgumentException("Risposta assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 32, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Risposta non valida.");
            if (!root.TryGetProperty("status", out JsonElement statusElement) ||
                statusElement.ValueKind != JsonValueKind.String)
                throw new FormatException("Stato della risposta mancante.");
            string status = statusElement.GetString();
            if (status == "Succeeded")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "status", "value");
            else if (status == "Failed")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "status", "errorId");
            else throw new FormatException("Stato della risposta sconosciuto.");
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion ||
                DdfExtensionCallCodec.ReadString(root, "pointId") != request.Point.Id)
                throw new FormatException("Risposta non associata al punto richiesto.");
            if (status == "Succeeded")
            {
                int remaining = 1024;
                DdfExtensionValue value = DdfExtensionCallCodec.ReadValue(
                    root.GetProperty("value"), 1, ref remaining);
                return DdfExtensionCallResponse.Success(request.Point, value);
            }
            return DdfExtensionCallResponse.Failure(request.Point,
                DdfExtensionCallCodec.ReadString(root, "errorId"));
        }
    }
}
