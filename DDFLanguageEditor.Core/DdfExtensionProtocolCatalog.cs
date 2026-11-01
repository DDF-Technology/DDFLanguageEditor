using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionProtocolDescriptor
    {
        internal DdfExtensionProtocolDescriptor(DdfExtensionPointKind kind,
            string requestSchema, string responseSchema, string frameSchema,
            IEnumerable<DdfExtensionHostMode> hostModes,
            IEnumerable<string> minimumCapabilities)
        {
            Kind = kind;
            ApiVersion = DdfExtensionPointDescriptor.ApiVersion;
            RequestSchema = requestSchema;
            ResponseSchema = responseSchema;
            FrameSchema = frameSchema;
            HostModes = new ReadOnlyCollection<DdfExtensionHostMode>(hostModes.ToArray());
            MinimumCapabilities = new ReadOnlyCollection<string>(
                minimumCapabilities.ToArray());
        }

        public DdfExtensionPointKind Kind { get; }
        public string ApiVersion { get; }
        public string RequestSchema { get; }
        public string ResponseSchema { get; }
        public string FrameSchema { get; }
        public IReadOnlyList<DdfExtensionHostMode> HostModes { get; }
        public IReadOnlyList<string> MinimumCapabilities { get; }
    }

    // Published data contract inventory; no loader, dispatch or permission grant.
    public static class DdfExtensionProtocolCatalog
    {
        public const string SchemaVersion = "1.0";
        public const string ExecutionState = "contracts-only";
        private static readonly IReadOnlyList<DdfExtensionProtocolDescriptor> Descriptors =
            new ReadOnlyCollection<DdfExtensionProtocolDescriptor>(new[]
            {
                new DdfExtensionProtocolDescriptor(DdfExtensionPointKind.SemanticOperation,
                    "ddf-extension-source-request.schema.json",
                    "ddf-extension-semantic-response.schema.json",
                    "ddf-extension-source-frame.schema.json",
                    new[] { DdfExtensionHostMode.Standalone, DdfExtensionHostMode.Embedded },
                    new[] { "file.read" }),
                new DdfExtensionProtocolDescriptor(DdfExtensionPointKind.RuntimeOperation,
                    "ddf-extension-call.schema.json",
                    "ddf-extension-call-response.schema.json",
                    "ddf-extension-call-frame.schema.json",
                    new[] { DdfExtensionHostMode.Standalone, DdfExtensionHostMode.Embedded },
                    Array.Empty<string>()),
                new DdfExtensionProtocolDescriptor(DdfExtensionPointKind.HostFunction,
                    "ddf-extension-call.schema.json",
                    "ddf-extension-call-response.schema.json",
                    "ddf-extension-call-frame.schema.json",
                    new[] { DdfExtensionHostMode.Standalone, DdfExtensionHostMode.Embedded },
                    Array.Empty<string>()),
                new DdfExtensionProtocolDescriptor(DdfExtensionPointKind.BuildBackend,
                    "ddf-extension-source-request.schema.json",
                    "ddf-extension-build-response.schema.json",
                    "ddf-extension-source-frame.schema.json",
                    new[] { DdfExtensionHostMode.Standalone },
                    new[] { "file.read", "file.write" })
            });

        public static IReadOnlyList<DdfExtensionProtocolDescriptor> All => Descriptors;

        public static bool TryGet(DdfExtensionPointKind kind,
            out DdfExtensionProtocolDescriptor descriptor)
        {
            descriptor = Descriptors.FirstOrDefault(item => item.Kind == kind);
            return descriptor != null;
        }

        public static string ToJson() => JsonSerializer.Serialize(new
        {
            schemaVersion = SchemaVersion,
            executionState = ExecutionState,
            protocols = Descriptors.Select(item => new
            {
                pointKind = item.Kind.ToString(),
                apiVersion = item.ApiVersion,
                requestSchema = item.RequestSchema,
                responseSchema = item.ResponseSchema,
                frameSchema = item.FrameSchema,
                hostModes = item.HostModes.Select(mode => mode.ToString()),
                minimumCapabilities = item.MinimumCapabilities
            })
        });

        public static void ValidateJson(string json)
        {
            if (json == null || json.Length > 32768 ||
                Encoding.UTF8.GetByteCount(json) > 32768)
                throw new ArgumentException("Manifesto protocollo assente o oltre quota.", nameof(json));
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            JsonElement root = document.RootElement;
            DdfExtensionCallCodec.RequireFields(root, "schemaVersion", "executionState",
                "protocols");
            if (DdfExtensionCallCodec.ReadString(root, "schemaVersion") != SchemaVersion ||
                DdfExtensionCallCodec.ReadString(root, "executionState") != ExecutionState)
                throw new FormatException("Versione o stato del protocollo non supportati.");
            JsonElement protocols = root.GetProperty("protocols");
            if (protocols.ValueKind != JsonValueKind.Array ||
                protocols.GetArrayLength() != Descriptors.Count)
                throw new FormatException("Catalogo protocolli incompleto.");
            var seen = new HashSet<DdfExtensionPointKind>();
            foreach (JsonElement entry in protocols.EnumerateArray())
            {
                DdfExtensionCallCodec.RequireFields(entry, "pointKind", "apiVersion",
                    "requestSchema", "responseSchema", "frameSchema", "hostModes",
                    "minimumCapabilities");
                string name = DdfExtensionCallCodec.ReadString(entry, "pointKind");
                if (!Enum.TryParse(name, false, out DdfExtensionPointKind kind) ||
                    !TryGet(kind, out DdfExtensionProtocolDescriptor expected) ||
                    kind.ToString() != name || !seen.Add(kind) ||
                    DdfExtensionCallCodec.ReadString(entry, "apiVersion") != expected.ApiVersion ||
                    DdfExtensionCallCodec.ReadString(entry, "requestSchema") != expected.RequestSchema ||
                    DdfExtensionCallCodec.ReadString(entry, "responseSchema") != expected.ResponseSchema ||
                    DdfExtensionCallCodec.ReadString(entry, "frameSchema") != expected.FrameSchema ||
                    !ReadStrings(entry, "hostModes").SequenceEqual(
                        expected.HostModes.Select(mode => mode.ToString())) ||
                    !ReadStrings(entry, "minimumCapabilities").SequenceEqual(
                        expected.MinimumCapabilities))
                    throw new FormatException("Descrittore di protocollo non corrispondente al Core.");
            }
        }

        private static string[] ReadStrings(JsonElement entry, string name)
        {
            JsonElement values = entry.GetProperty(name);
            if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 8)
                throw new FormatException("Elenco del protocollo non valido.");
            return values.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString() : throw new FormatException("Valore del protocollo non testuale."))
                .ToArray();
        }
    }
}
