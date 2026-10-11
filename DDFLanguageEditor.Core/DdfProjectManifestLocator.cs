using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Posizione testuale di una dichiarazione nel manifest. Gli offset sono
    /// espressi in caratteri .NET e sono quindi utilizzabili direttamente
    /// dall'editor anche quando il JSON contiene testo UTF-8 non ASCII.
    /// </summary>
    public sealed class DdfProjectManifestLocation
    {
        internal DdfProjectManifestLocation(string documentPath, int start,
            int length, int line, int column)
        {
            DocumentPath = documentPath ?? string.Empty;
            Start = start;
            Length = Math.Max(1, length);
            Line = Math.Max(1, line);
            Column = Math.Max(1, column);
        }

        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
    }

    /// <summary>
    /// Localizza una reference senza deserializzare nuovamente il manifest in
    /// un modello permissivo. Il lettore JSON segue esclusivamente l'array
    /// radice "references" e non può confondere ID omonimi in altre sezioni.
    /// </summary>
    public static class DdfProjectManifestLocator
    {
        public static DdfProjectManifestLocation FindExternalReference(
            string projectRoot, string referenceId,
            string preferredProperty = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) return null;
            string path;
            try
            {
                path = Path.Combine(Path.GetFullPath(projectRoot),
                    DdfProjectManifest.FileName);
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                return null;
            }
            return FindExternalReferenceInFile(path, referenceId,
                preferredProperty);
        }

        public static DdfProjectManifestLocation FindExternalReferenceInFile(
            string manifestPath, string referenceId,
            string preferredProperty = null)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) ||
                string.IsNullOrWhiteSpace(referenceId) ||
                !File.Exists(manifestPath)) return null;
            try
            {
                byte[] fileBytes = File.ReadAllBytes(manifestPath);
                int bomLength = fileBytes.Length >= 3 &&
                    fileBytes[0] == 0xEF && fileBytes[1] == 0xBB &&
                    fileBytes[2] == 0xBF ? 3 : 0;
                ReadOnlySpan<byte> json = fileBytes.AsSpan(bomLength);
                var reader = new Utf8JsonReader(json,
                    new JsonReaderOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                bool referencesProperty = false;
                int referencesDepth = -1;
                int objectDepth = -1;
                long objectStart = -1;
                string pendingProperty = null;
                string currentId = null;
                var properties = new Dictionary<string, ByteSpan>(
                    StringComparer.OrdinalIgnoreCase);

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string name = reader.GetString();
                        if (referencesDepth < 0 && reader.CurrentDepth == 1 &&
                            string.Equals(name, "references",
                                StringComparison.OrdinalIgnoreCase))
                            referencesProperty = true;
                        else if (objectDepth >= 0 &&
                                 reader.CurrentDepth == objectDepth + 1)
                            pendingProperty = name;
                        continue;
                    }

                    if (reader.TokenType == JsonTokenType.StartArray &&
                        referencesProperty)
                    {
                        referencesDepth = reader.CurrentDepth;
                        referencesProperty = false;
                        continue;
                    }

                    if (referencesDepth >= 0 && objectDepth < 0 &&
                        reader.TokenType == JsonTokenType.StartObject &&
                        reader.CurrentDepth == referencesDepth + 1)
                    {
                        objectDepth = reader.CurrentDepth;
                        objectStart = reader.TokenStartIndex;
                        currentId = null;
                        pendingProperty = null;
                        properties.Clear();
                        continue;
                    }

                    if (objectDepth >= 0 && pendingProperty != null &&
                        IsPropertyValue(reader.TokenType) &&
                        reader.CurrentDepth == objectDepth + 1)
                    {
                        var span = new ByteSpan(reader.TokenStartIndex,
                            reader.BytesConsumed - reader.TokenStartIndex);
                        properties[pendingProperty] = span;
                        if (string.Equals(pendingProperty, "id",
                                StringComparison.OrdinalIgnoreCase) &&
                            reader.TokenType == JsonTokenType.String)
                            currentId = reader.GetString();
                        pendingProperty = null;
                        continue;
                    }

                    if (objectDepth >= 0 &&
                        reader.TokenType == JsonTokenType.EndObject &&
                        reader.CurrentDepth == objectDepth)
                    {
                        if (string.Equals(currentId, referenceId,
                                StringComparison.Ordinal))
                        {
                            ByteSpan span;
                            if (!string.IsNullOrWhiteSpace(preferredProperty) &&
                                properties.TryGetValue(preferredProperty,
                                    out ByteSpan preferred))
                                span = preferred;
                            else if (properties.TryGetValue("id",
                                         out ByteSpan id))
                                span = id;
                            else
                                span = new ByteSpan(objectStart,
                                    reader.BytesConsumed - objectStart);
                            return ToLocation(Path.GetFullPath(manifestPath),
                                json, span);
                        }
                        objectDepth = -1;
                        objectStart = -1;
                        pendingProperty = null;
                        properties.Clear();
                        continue;
                    }

                    if (referencesDepth >= 0 &&
                        reader.TokenType == JsonTokenType.EndArray &&
                        reader.CurrentDepth == referencesDepth)
                        break;
                }
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is JsonException ||
                                               exception is DecoderFallbackException)
            {
                return null;
            }
            return null;
        }

        private static bool IsPropertyValue(JsonTokenType token) =>
            token == JsonTokenType.String || token == JsonTokenType.Number ||
            token == JsonTokenType.True || token == JsonTokenType.False ||
            token == JsonTokenType.Null || token == JsonTokenType.StartArray ||
            token == JsonTokenType.StartObject;

        private static DdfProjectManifestLocation ToLocation(string path,
            ReadOnlySpan<byte> json, ByteSpan span)
        {
            int byteStart = checked((int)Math.Max(0, span.Start));
            int byteLength = checked((int)Math.Max(1, span.Length));
            byteStart = Math.Min(byteStart, json.Length);
            byteLength = Math.Min(byteLength, json.Length - byteStart);
            int start = Encoding.UTF8.GetCharCount(json.Slice(0, byteStart));
            int length = Encoding.UTF8.GetCharCount(json.Slice(byteStart,
                byteLength));
            string prefix = Encoding.UTF8.GetString(json.Slice(0, byteStart));
            int line = 1;
            int column = 1;
            foreach (char value in prefix)
            {
                if (value == '\n')
                {
                    line++;
                    column = 1;
                }
                else column++;
            }
            return new DdfProjectManifestLocation(path, start,
                Math.Max(1, length), line, column);
        }

        private readonly struct ByteSpan
        {
            internal ByteSpan(long start, long length)
            {
                Start = start;
                Length = length;
            }

            internal long Start { get; }
            internal long Length { get; }
        }
    }
}
