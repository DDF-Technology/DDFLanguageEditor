using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // Strict transport only: a parsed diagnostic is still untrusted worker data.
    public static class DdfExtensionSemanticResponseCodec
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumWireBytes = 256 * 1024;

        public static string Serialize(DdfExtensionSemanticResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pointId", response.Request.Point.Id);
                writer.WriteString("requestSha256", Fingerprint(response.Request));
                writer.WriteString("status", response.Status.ToString());
                if (response.Status == DdfExtensionCallStatus.Succeeded)
                {
                    writer.WriteStartArray("diagnostics");
                    foreach (DdfExtensionSemanticDiagnostic item in response.Diagnostics)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", item.Id);
                        writer.WriteString("severity", item.Severity.ToString());
                        writer.WriteString("message", item.Message);
                        writer.WriteNumber("start", item.Start);
                        writer.WriteNumber("length", item.Length);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                else writer.WriteString("errorId", response.ErrorId);
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumWireBytes)
                throw new ArgumentException("Risposta semantica serializzata oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static DdfExtensionSemanticResponse Parse(string json,
            DdfExtensionSemanticRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (json == null || json.Length > MaximumWireBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumWireBytes)
                throw new ArgumentException("Risposta semantica assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("status", out JsonElement statusElement) ||
                statusElement.ValueKind != JsonValueKind.String)
                throw new FormatException("Stato della risposta semantica mancante.");
            string status = statusElement.GetString();
            if (status == "Succeeded")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "requestSha256", "status", "diagnostics");
            else if (status == "Failed")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "requestSha256", "status", "errorId");
            else throw new FormatException("Stato della risposta semantica sconosciuto.");
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion ||
                DdfExtensionCallCodec.ReadString(root, "pointId") != request.Point.Id ||
                DdfExtensionCallCodec.ReadString(root, "requestSha256") != Fingerprint(request))
                throw new FormatException("Risposta semantica non associata alla richiesta.");
            if (status == "Failed")
                return DdfExtensionSemanticResponse.Failure(request,
                    DdfExtensionCallCodec.ReadString(root, "errorId"));
            JsonElement diagnostics = root.GetProperty("diagnostics");
            if (diagnostics.ValueKind != JsonValueKind.Array ||
                diagnostics.GetArrayLength() > 128)
                throw new FormatException("Elenco diagnostiche non valido.");
            return DdfExtensionSemanticResponse.Success(request,
                diagnostics.EnumerateArray().Select(item => ReadDiagnostic(item, request)).ToArray());
        }

        private static DdfExtensionSemanticDiagnostic ReadDiagnostic(JsonElement item,
            DdfExtensionSemanticRequest request)
        {
            DdfExtensionCallCodec.RequireFields(item, "id", "severity", "message",
                "start", "length");
            string severityName = DdfExtensionCallCodec.ReadString(item, "severity");
            if (!Enum.TryParse(severityName, false, out DdfExtensionSemanticSeverity severity) ||
                !Enum.IsDefined(typeof(DdfExtensionSemanticSeverity), severity) ||
                severity.ToString() != severityName)
                throw new FormatException("Severità semantica non valida.");
            return new DdfExtensionSemanticDiagnostic(request,
                DdfExtensionCallCodec.ReadString(item, "id"), severity,
                DdfExtensionCallCodec.ReadString(item, "message"),
                ReadInt(item, "start"), ReadInt(item, "length"));
        }

        private static int ReadInt(JsonElement item, string name)
        {
            JsonElement value = item.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
                throw new FormatException("Intervallo diagnostico non valido.");
            return result;
        }

        private static string Fingerprint(DdfExtensionSemanticRequest request)
        {
            string canonical = DdfExtensionSourceRequestCodec.SerializeSemantic(request);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();
        }
    }
}
