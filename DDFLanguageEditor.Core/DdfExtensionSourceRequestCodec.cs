using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // Strict source-data transport only. It does not execute semantic or build extensions.
    public static class DdfExtensionSourceRequestCodec
    {
        public const string SchemaVersion = "1.0";
        public const int MaximumWireBytes = 32 * 1024 * 1024;

        public static string SerializeSemantic(DdfExtensionSemanticRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pointId", request.Point.Id);
                writer.WritePropertyName("document");
                WriteSource(writer, request.Document);
                writer.WriteString("ruleId", request.RuleId);
                writer.WriteNumber("start", request.Start);
                writer.WriteNumber("length", request.Length);
                writer.WriteEndObject();
            }
            return Finish(stream);
        }

        public static DdfExtensionSemanticRequest ParseSemantic(string json,
            DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            using JsonDocument document = ReadDocument(json);
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                "document", "ruleId", "start", "length");
            RequireVersion(root);
            DdfExtensionPointDescriptor point = RequirePoint(root, catalog);
            return new DdfExtensionSemanticRequest(point,
                ReadSource(root.GetProperty("document")),
                DdfExtensionCallCodec.ReadString(root, "ruleId"),
                ReadInt(root, "start"), ReadInt(root, "length"));
        }

        public static string SerializeBuild(DdfExtensionBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pointId", request.Point.Id);
                writer.WriteString("entrySource", request.EntrySource);
                writer.WriteString("targetKind", request.TargetKind.ToString());
                writer.WriteStartArray("sources");
                foreach (DdfExtensionSourceSnapshot source in request.Sources.OrderBy(
                    item => item.RelativePath, StringComparer.Ordinal))
                    WriteSource(writer, source);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return Finish(stream);
        }

        public static DdfExtensionBuildRequest ParseBuild(string json,
            DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            using JsonDocument document = ReadDocument(json);
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "pointId",
                "entrySource", "targetKind", "sources");
            RequireVersion(root);
            DdfExtensionPointDescriptor point = RequirePoint(root, catalog);
            string kindName = DdfExtensionCallCodec.ReadString(root, "targetKind");
            if (!Enum.TryParse(kindName, false, out DdfExtensionBuildTargetKind kind) ||
                !Enum.IsDefined(typeof(DdfExtensionBuildTargetKind), kind) ||
                kind.ToString() != kindName)
                throw new FormatException("Target di build non supportato.");
            JsonElement sources = root.GetProperty("sources");
            if (sources.ValueKind != JsonValueKind.Array || sources.GetArrayLength() > 128)
                throw new FormatException("Elenco sorgenti build non valido.");
            return new DdfExtensionBuildRequest(point,
                sources.EnumerateArray().Select(ReadSource).ToArray(),
                DdfExtensionCallCodec.ReadString(root, "entrySource"), kind);
        }

        private static void WriteSource(Utf8JsonWriter writer,
            DdfExtensionSourceSnapshot source)
        {
            writer.WriteStartObject();
            writer.WriteString("relativePath", source.RelativePath);
            writer.WriteString("sha256", source.Sha256);
            writer.WriteString("sourceText", source.SourceText);
            writer.WriteEndObject();
        }

        private static DdfExtensionSourceSnapshot ReadSource(JsonElement element)
        {
            DdfExtensionCallCodec.RequireFields(element, "relativePath", "sha256",
                "sourceText");
            var snapshot = new DdfExtensionSourceSnapshot(
                DdfExtensionCallCodec.ReadString(element, "relativePath"),
                DdfExtensionCallCodec.ReadString(element, "sourceText"));
            if (DdfExtensionCallCodec.ReadString(element, "sha256") != snapshot.Sha256)
                throw new FormatException("Hash del sorgente non corrispondente.");
            return snapshot;
        }

        private static JsonDocument ReadDocument(string json)
        {
            if (json == null || json.Length > MaximumWireBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumWireBytes)
                throw new ArgumentException("Richiesta sorgente assente o oltre quota.",
                    nameof(json));
            return JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
        }

        private static string Finish(MemoryStream stream)
        {
            if (stream.Length > MaximumWireBytes)
                throw new ArgumentException("Richiesta sorgente serializzata oltre quota.");
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void RequireVersion(JsonElement root)
        {
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion)
                throw new FormatException("Versione richiesta sorgente non supportata.");
        }

        private static DdfExtensionPointDescriptor RequirePoint(JsonElement root,
            DdfExtensionPointCatalog catalog)
        {
            string pointId = DdfExtensionCallCodec.ReadString(root, "pointId");
            if (!catalog.TryGet(pointId, out DdfExtensionPointDescriptor point))
                throw new FormatException("Punto sorgente assente dal catalogo.");
            return point;
        }

        private static int ReadInt(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt32(out int number))
                throw new FormatException("Intervallo sorgente non valido: " + name);
            return number;
        }
    }
}
