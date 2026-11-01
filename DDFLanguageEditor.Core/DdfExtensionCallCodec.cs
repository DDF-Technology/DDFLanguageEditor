using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // Wire format only. Parsing a request never dispatches an operation.
    public static class DdfExtensionCallCodec
    {
        public const string SchemaVersion = "1.0";
        internal const int MaximumBytes = 131072;

        public static string Serialize(DdfExtensionCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", SchemaVersion);
                writer.WriteString("pointId", request.Point.Id);
                writer.WriteStartObject("arguments");
                foreach (KeyValuePair<string, DdfExtensionValue> argument in
                    request.Arguments.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(argument.Key);
                    WriteValue(writer, argument.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            if (stream.Length > MaximumBytes)
                throw new ArgumentException("Richiesta serializzata oltre quota.", nameof(request));
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static DdfExtensionCallRequest Parse(string json,
            DdfExtensionPointCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (json == null || json.Length > MaximumBytes ||
                Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new ArgumentException("Richiesta assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 32,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            RequireFields(root, "schemaVersion", "pointId", "arguments");
            if (ReadString(root, "schemaVersion") != SchemaVersion)
                throw new FormatException("Versione richiesta non supportata.");
            string pointId = ReadString(root, "pointId");
            if (!catalog.TryGet(pointId, out DdfExtensionPointDescriptor point))
                throw new FormatException("Punto di estensione assente dal catalogo.");
            JsonElement arguments = root.GetProperty("arguments");
            if (arguments.ValueKind != JsonValueKind.Object)
                throw new FormatException("Argomenti non validi.");
            var values = new List<KeyValuePair<string, DdfExtensionValue>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int remaining = 1024;
            foreach (JsonProperty property in arguments.EnumerateObject())
            {
                if (values.Count == 64 || !seen.Add(property.Name))
                    throw new FormatException("Argomenti duplicati o oltre quota.");
                values.Add(new KeyValuePair<string, DdfExtensionValue>(property.Name,
                    ReadValue(property.Value, 1, ref remaining)));
            }
            return new DdfExtensionCallRequest(point, values);
        }

        internal static void WriteValue(Utf8JsonWriter writer, DdfExtensionValue value)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", value.Kind.ToString());
            switch (value.Kind)
            {
                case DdfExtensionValueKind.Null:
                    break;
                case DdfExtensionValueKind.Boolean:
                    writer.WriteBoolean("value", value.Boolean);
                    break;
                case DdfExtensionValueKind.Integer:
                    writer.WriteNumber("value", value.Integer);
                    break;
                case DdfExtensionValueKind.Float:
                    writer.WriteNumber("value", value.Float);
                    break;
                case DdfExtensionValueKind.String:
                    writer.WriteString("value", value.String);
                    break;
                case DdfExtensionValueKind.Array:
                    writer.WriteStartArray("value");
                    foreach (DdfExtensionValue element in value.Elements)
                        WriteValue(writer, element);
                    writer.WriteEndArray();
                    break;
                default:
                    throw new InvalidOperationException("Tipo di valore sconosciuto.");
            }
            writer.WriteEndObject();
        }

        internal static DdfExtensionValue ReadValue(JsonElement element, int depth,
            ref int remaining)
        {
            if (depth > 8 || --remaining < 0)
                throw new FormatException("Valore oltre quota di profondità o dimensione.");
            if (element.ValueKind != JsonValueKind.Object)
                throw new FormatException("Valore tipizzato richiesto.");
            JsonElement kindElement = default;
            JsonElement payload = default;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if ((property.Name != "kind" && property.Name != "value") ||
                    !seen.Add(property.Name))
                    throw new FormatException("Campo del valore sconosciuto o duplicato.");
                if (property.Name == "kind") kindElement = property.Value;
                else payload = property.Value;
            }
            if (kindElement.ValueKind != JsonValueKind.String)
                throw new FormatException("Tipo del valore mancante.");
            string kindName = kindElement.GetString();
            if (!Enum.TryParse(kindName, false, out DdfExtensionValueKind kind) ||
                !Enum.IsDefined(typeof(DdfExtensionValueKind), kind) ||
                kind.ToString() != kindName)
                throw new FormatException("Tipo del valore sconosciuto.");
            if (kind == DdfExtensionValueKind.Null)
            {
                if (seen.Count != 1) throw new FormatException("Null non ha payload.");
                return DdfExtensionValue.Null;
            }
            if (seen.Count != 2) throw new FormatException("Payload del valore mancante.");
            switch (kind)
            {
                case DdfExtensionValueKind.Boolean:
                    if (payload.ValueKind != JsonValueKind.True &&
                        payload.ValueKind != JsonValueKind.False)
                        throw new FormatException("Boolean non valido.");
                    return DdfExtensionValue.FromBoolean(payload.GetBoolean());
                case DdfExtensionValueKind.Integer:
                    if (payload.ValueKind != JsonValueKind.Number ||
                        !payload.TryGetInt32(out int integer))
                        throw new FormatException("Int32 non valido.");
                    return DdfExtensionValue.FromInteger(integer);
                case DdfExtensionValueKind.Float:
                    if (payload.ValueKind != JsonValueKind.Number ||
                        !payload.TryGetDouble(out double floating) ||
                        !double.IsFinite(floating))
                        throw new FormatException("Float finito richiesto.");
                    return DdfExtensionValue.FromFloat(floating);
                case DdfExtensionValueKind.String:
                    if (payload.ValueKind != JsonValueKind.String)
                        throw new FormatException("Stringa non valida.");
                    return DdfExtensionValue.FromString(payload.GetString());
                case DdfExtensionValueKind.Array:
                    if (payload.ValueKind != JsonValueKind.Array ||
                        payload.GetArrayLength() > 256)
                        throw new FormatException("Array non valido.");
                    var items = new List<DdfExtensionValue>();
                    foreach (JsonElement item in payload.EnumerateArray())
                        items.Add(ReadValue(item, depth + 1, ref remaining));
                    return DdfExtensionValue.FromArray(items);
                default:
                    throw new FormatException("Tipo di valore non supportato.");
            }
        }

        internal static string ReadString(JsonElement element, string name)
        {
            JsonElement value = element.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String)
                throw new FormatException("Campo testuale non valido: " + name);
            return value.GetString();
        }

        internal static void RequireFields(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new FormatException("Oggetto richiesto.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!names.Contains(property.Name, StringComparer.Ordinal) ||
                    !seen.Add(property.Name))
                    throw new FormatException("Campo sconosciuto o duplicato.");
            if (seen.Count != names.Length)
                throw new FormatException("Campo obbligatorio mancante.");
        }
    }
}
