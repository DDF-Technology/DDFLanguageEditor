using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // No disk I/O, PE validation, worker authentication or execution.
    public static class DdfExtensionBuildResponseCodec
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumWireBytes = 24 * 1024 * 1024;

        public static string Serialize(DdfExtensionBuildResponse response)
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
                    writer.WriteStartArray("artifacts");
                    foreach (DdfExtensionBuildArtifact artifact in response.Artifacts.OrderBy(
                        item => item.Kind))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("kind", artifact.Kind.ToString());
                        writer.WriteString("relativePath", artifact.RelativePath);
                        writer.WriteString("sha256", artifact.Sha256);
                        writer.WriteBase64String("bytesBase64", artifact.Payload);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                else writer.WriteString("errorId", response.ErrorId);
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumWireBytes)
                throw new ArgumentException("Risposta build serializzata oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static DdfExtensionBuildResponse Parse(string json,
            DdfExtensionBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (json == null || json.Length > MaximumWireBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumWireBytes)
                throw new ArgumentException("Risposta build assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("status", out JsonElement statusElement) ||
                statusElement.ValueKind != JsonValueKind.String)
                throw new FormatException("Stato risposta build mancante.");
            string status = statusElement.GetString();
            if (status == "Succeeded")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "requestSha256", "status", "artifacts");
            else if (status == "Failed")
                DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                    "requestSha256", "status", "errorId");
            else throw new FormatException("Stato risposta build sconosciuto.");
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion ||
                DdfExtensionCallCodec.ReadString(root, "pointId") != request.Point.Id ||
                DdfExtensionCallCodec.ReadString(root, "requestSha256") != Fingerprint(request))
                throw new FormatException("Risposta build non associata alla richiesta.");
            if (status == "Failed")
                return DdfExtensionBuildResponse.Failure(request,
                    DdfExtensionCallCodec.ReadString(root, "errorId"));
            JsonElement artifacts = root.GetProperty("artifacts");
            if (artifacts.ValueKind != JsonValueKind.Array || artifacts.GetArrayLength() > 3)
                throw new FormatException("Elenco artefatti non valido.");
            return DdfExtensionBuildResponse.Success(request,
                artifacts.EnumerateArray().Select(ReadArtifact).ToArray());
        }

        private static DdfExtensionBuildArtifact ReadArtifact(JsonElement element)
        {
            DdfExtensionCallCodec.RequireFields(element, "kind", "relativePath",
                "sha256", "bytesBase64");
            string kindName = DdfExtensionCallCodec.ReadString(element, "kind");
            if (!Enum.TryParse(kindName, false, out DdfExtensionBuildArtifactKind kind) ||
                !Enum.IsDefined(typeof(DdfExtensionBuildArtifactKind), kind) ||
                kind.ToString() != kindName)
                throw new FormatException("Tipo artefatto non valido.");
            string encoded = DdfExtensionCallCodec.ReadString(element, "bytesBase64");
            if (encoded.Length == 0 ||
                encoded.Length > ((DdfExtensionBuildArtifact.MaximumBytes + 2) / 3) * 4)
                throw new FormatException("Payload artefatto oltre quota.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { throw new FormatException("Payload artefatto non valido."); }
            if (Convert.ToBase64String(bytes) != encoded)
                throw new FormatException("Payload artefatto non canonico.");
            var artifact = new DdfExtensionBuildArtifact(kind,
                DdfExtensionCallCodec.ReadString(element, "relativePath"), bytes);
            if (DdfExtensionCallCodec.ReadString(element, "sha256") != artifact.Sha256)
                throw new FormatException("Hash artefatto non corrispondente.");
            return artifact;
        }

        private static string Fingerprint(DdfExtensionBuildRequest request)
        {
            string canonical = DdfExtensionSourceRequestCodec.SerializeBuild(request);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();
        }
    }
}
