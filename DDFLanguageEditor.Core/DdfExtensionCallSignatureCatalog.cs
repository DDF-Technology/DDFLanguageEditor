using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionCallSignatureCatalog
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumBytes = 1048576;

        public DdfExtensionCallSignatureCatalog(DdfExtensionPointCatalog points,
            IEnumerable<DdfExtensionCallSignature> signatures)
        {
            Points = points ?? throw new ArgumentNullException(nameof(points));
            DdfExtensionCallSignature[] copy = (signatures ??
                throw new ArgumentNullException(nameof(signatures))).ToArray();
            DdfExtensionPointDescriptor[] callable = points.Points.Where(item =>
                item.Kind == DdfExtensionPointKind.RuntimeOperation ||
                item.Kind == DdfExtensionPointKind.HostFunction).ToArray();
            if (copy.Length != callable.Length || copy.Any(item => item == null) ||
                copy.Select(item => item.Point.Id).Distinct(StringComparer.Ordinal).Count() !=
                    copy.Length || copy.Any(item => !points.TryGet(item.Point.Id,
                    out DdfExtensionPointDescriptor point) || !item.MatchesPoint(point)))
                throw new ArgumentException("Le firme devono coprire esattamente i punti runtime/host.",
                    nameof(signatures));
            Signatures = new ReadOnlyCollection<DdfExtensionCallSignature>(copy);
            PointsCatalogSha256 = Convert.ToHexString(SHA256.HashData(
                DdfExtensionPointAttestation.CanonicalPayload(points))).ToLowerInvariant();
        }

        public DdfExtensionPointCatalog Points { get; }
        public string PointsCatalogSha256 { get; }
        public IReadOnlyList<DdfExtensionCallSignature> Signatures { get; }

        public bool TryGet(string pointId, out DdfExtensionCallSignature signature)
        {
            signature = Signatures.FirstOrDefault(item => item.Point.Id == pointId);
            return signature != null;
        }

        public string ToJson()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pluginId", Points.Manifest.Id);
                writer.WriteString("pluginVersion", Points.Manifest.Version);
                writer.WriteString("pointsCatalogSha256", PointsCatalogSha256);
                writer.WriteStartArray("signatures");
                foreach (DdfExtensionCallSignature signature in Signatures.OrderBy(item =>
                    item.Point.Id, StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("pointId", signature.Point.Id);
                    writer.WriteStartArray("parameters");
                    foreach (DdfExtensionCallParameter parameter in signature.Parameters)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", parameter.Name);
                        writer.WritePropertyName("type");
                        WriteType(writer, parameter.Type);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WritePropertyName("resultType");
                    WriteType(writer, signature.ResultType);
                    writer.WriteString("effect", signature.Effect.ToString());
                    writer.WriteStartArray("declaredErrors");
                    foreach (string error in signature.DeclaredErrors.OrderBy(item => item,
                        StringComparer.Ordinal)) writer.WriteStringValue(error);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumBytes)
                throw new ArgumentException("Catalogo firme oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteType(Utf8JsonWriter writer, DdfExtensionTypeShape type)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", type.Kind.ToString());
            if (type.Kind == DdfExtensionValueKind.Array)
            {
                writer.WritePropertyName("elementType");
                WriteType(writer, type.ElementType);
            }
            writer.WriteEndObject();
        }
    }

    public static class DdfExtensionCallSignatureCatalogCodec
    {
        public static DdfExtensionCallSignatureCatalog Parse(string json,
            DdfExtensionPointCatalog points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (json == null || json.Length > DdfExtensionCallSignatureCatalog.MaximumBytes ||
                Encoding.UTF8.GetByteCount(json) >
                    DdfExtensionCallSignatureCatalog.MaximumBytes)
                throw new ArgumentException("Catalogo firme assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 32, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pluginId",
                "pluginVersion", "pointsCatalogSha256", "signatures");
            string expectedHash = Convert.ToHexString(SHA256.HashData(
                DdfExtensionPointAttestation.CanonicalPayload(points))).ToLowerInvariant();
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") !=
                    DdfExtensionCallSignatureCatalog.SchemaVersion ||
                DdfExtensionCallCodec.ReadString(root, "pluginId") != points.Manifest.Id ||
                DdfExtensionCallCodec.ReadString(root, "pluginVersion") !=
                    points.Manifest.Version ||
                DdfExtensionCallCodec.ReadString(root, "pointsCatalogSha256") !=
                    expectedHash)
                throw new FormatException("Catalogo firme non associato ai punti attesi.");
            JsonElement entries = root.GetProperty("signatures");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 64)
                throw new FormatException("Elenco delle firme non valido.");
            var signatures = new List<DdfExtensionCallSignature>();
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                DdfExtensionCallCodec.RequireFields(entry, "pointId", "parameters",
                    "resultType", "effect", "declaredErrors");
                string pointId = DdfExtensionCallCodec.ReadString(entry, "pointId");
                if (!points.TryGet(pointId, out DdfExtensionPointDescriptor point))
                    throw new FormatException("Firma per punto sconosciuto.");
                JsonElement parameters = entry.GetProperty("parameters");
                if (parameters.ValueKind != JsonValueKind.Array ||
                    parameters.GetArrayLength() > 64)
                    throw new FormatException("Parametri della firma non validi.");
                var parsedParameters = new List<DdfExtensionCallParameter>();
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    DdfExtensionCallCodec.RequireFields(parameter, "name", "type");
                    parsedParameters.Add(new DdfExtensionCallParameter(
                        DdfExtensionCallCodec.ReadString(parameter, "name"),
                        ReadType(parameter.GetProperty("type"), 1)));
                }
                string effectName = DdfExtensionCallCodec.ReadString(entry, "effect");
                if (!Enum.TryParse(effectName, false, out DdfExtensionCallEffect effect) ||
                    !Enum.IsDefined(typeof(DdfExtensionCallEffect), effect) ||
                    effect.ToString() != effectName)
                    throw new FormatException("Effetto della firma non supportato.");
                JsonElement errors = entry.GetProperty("declaredErrors");
                if (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() > 64)
                    throw new FormatException("Errori della firma non validi.");
                string[] errorIds = errors.EnumerateArray().Select(item =>
                    item.ValueKind == JsonValueKind.String ? item.GetString() :
                        throw new FormatException("ID errore non testuale.")).ToArray();
                signatures.Add(new DdfExtensionCallSignature(point, parsedParameters,
                    ReadType(entry.GetProperty("resultType"), 1), effect, errorIds));
            }
            return new DdfExtensionCallSignatureCatalog(points, signatures);
        }

        private static DdfExtensionTypeShape ReadType(JsonElement element, int depth)
        {
            if (depth > 8) throw new FormatException("Tipo oltre quota di profondità.");
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("kind", out JsonElement kindValue) ||
                kindValue.ValueKind != JsonValueKind.String)
                throw new FormatException("Tipo della firma non valido.");
            string kindName = kindValue.GetString();
            if (!Enum.TryParse(kindName, false, out DdfExtensionValueKind kind) ||
                !Enum.IsDefined(typeof(DdfExtensionValueKind), kind) ||
                kind.ToString() != kindName)
                throw new FormatException("Tipo della firma sconosciuto.");
            if (kind == DdfExtensionValueKind.Array)
            {
                DdfExtensionCallCodec.RequireFields(element, "kind", "elementType");
                return new DdfExtensionTypeShape(kind,
                    ReadType(element.GetProperty("elementType"), depth + 1));
            }
            DdfExtensionCallCodec.RequireFields(element, "kind");
            return new DdfExtensionTypeShape(kind);
        }
    }
}
