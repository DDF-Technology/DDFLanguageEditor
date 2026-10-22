using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DDFLanguageEditor.Runtime
{
    /// <summary>
    /// Backend-neutral persisted value. Only the portable DDF scalar subset is
    /// admitted; compiler implementation types never cross this boundary.
    /// </summary>
    public sealed class DdfPersistedStateEntry
    {
        public DdfPersistedStateEntry(string key, string typeName, object value)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 2048)
                throw new ArgumentException("La chiave state non è valida.",
                    nameof(key));
            if (typeName is not ("int" or "float" or "char" or "bool" or
                    "string"))
                throw new ArgumentException(
                    "Il tipo state persistito non è supportato.",
                    nameof(typeName));
            bool valid = typeName switch
            {
                "int" => value is int,
                "float" => value is double number && !double.IsNaN(number) &&
                    !double.IsInfinity(number),
                "char" => value is char,
                "bool" => value is bool,
                "string" => value is string text && text.Length <= 1048576,
                _ => false
            };
            if (!valid)
                throw new ArgumentException(
                    "Il valore state non corrisponde al tipo dichiarato.",
                    nameof(value));
            Key = key;
            TypeName = typeName;
            Value = value;
        }

        public string Key { get; }
        public string TypeName { get; }
        public object Value { get; }

        public object RequireValue(string expectedTypeName)
        {
            if (!string.Equals(TypeName, expectedTypeName,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il tipo state persistito per " + Key + " è " + TypeName +
                    ", ma il programma richiede " + expectedTypeName + ".");
            return Value;
        }
    }

    /// <summary>
    /// Immutable state transaction exchanged by compiled DDF sessions. A caller
    /// commits a captured snapshot only after a successful execution.
    /// </summary>
    public sealed class DdfPersistedStateSnapshot
    {
        public const string CurrentSchemaVersion = "1.0";

        public DdfPersistedStateSnapshot(string schemaVersion, string languageId,
            string languagePackageVersion,
            IEnumerable<DdfPersistedStateEntry> entries)
        {
            if (!string.Equals(schemaVersion, CurrentSchemaVersion,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "La versione schema state non è supportata.",
                    nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(languageId))
                throw new ArgumentException(
                    "L'identità del linguaggio è obbligatoria.",
                    nameof(languageId));
            if (string.IsNullOrWhiteSpace(languagePackageVersion))
                throw new ArgumentException(
                    "La versione del pacchetto è obbligatoria.",
                    nameof(languagePackageVersion));
            DdfPersistedStateEntry[] materialized = (entries ??
                    Enumerable.Empty<DdfPersistedStateEntry>()).ToArray();
            long payloadSize = materialized.Sum(item => item == null ? 0L :
                item.Key.Length + item.TypeName.Length +
                (item.Value is string text ? text.Length : 16));
            if (materialized.Length > 4096 ||
                materialized.Any(item => item == null) ||
                materialized.Select(item => item.Key)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    materialized.Length || payloadSize > 4L * 1024 * 1024)
                throw new ArgumentException(
                    "Lo snapshot state supera i budget o contiene voci nulle o duplicate.",
                    nameof(entries));
            SchemaVersion = schemaVersion;
            LanguageId = languageId;
            LanguagePackageVersion = languagePackageVersion;
            Entries = Array.AsReadOnly(materialized);
            ByKey = new ReadOnlyDictionary<string, DdfPersistedStateEntry>(
                materialized.ToDictionary(item => item.Key,
                    StringComparer.Ordinal));
        }

        public string SchemaVersion { get; }
        public string LanguageId { get; }
        public string LanguagePackageVersion { get; }
        public IReadOnlyList<DdfPersistedStateEntry> Entries { get; }
        public IReadOnlyDictionary<string, DdfPersistedStateEntry> ByKey { get; }
    }
}
