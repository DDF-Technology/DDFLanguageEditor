using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfProductUpdateComponentKind
    {
        Standalone,
        EmbeddedSdk,
        LanguagePackage,
        Module,
        Plugin
    }

    public enum DdfProductUpdateChannel
    {
        Stable,
        Beta
    }

    public enum DdfProductUpdateState
    {
        Unknown,
        UpToDate,
        UpdateAvailable,
        Incompatible
    }

    public enum DdfProductUpdateCompatibility
    {
        Unknown,
        Compatible,
        Incompatible
    }

    public sealed class DdfProductUpdateResult
    {
        public const string SchemaVersionValue = "1.0";

        private static readonly Regex SemanticVersion = new Regex(
            "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)" +
            "(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$",
            RegexOptions.CultureInvariant);

        private DdfProductUpdateResult(DdfProductUpdateComponentKind componentKind,
            string componentId, string currentVersion, string availableVersion,
            DdfProductUpdateChannel channel, DdfProductUpdateState state,
            DdfProductUpdateCompatibility compatibility, DateTimeOffset checkedAtUtc,
            DateTimeOffset? releasedAtUtc, string releaseNotes, Uri verifiedReleaseUri,
            string statusDetail, IEnumerable<string> compatibilityReasons)
        {
            ComponentKind = componentKind;
            ComponentId = RequireComponentId(componentId);
            CurrentVersion = RequireVersion(currentVersion, nameof(currentVersion));
            AvailableVersion = string.IsNullOrWhiteSpace(availableVersion)
                ? string.Empty
                : RequireVersion(availableVersion, nameof(availableVersion));
            Channel = channel;
            State = state;
            Compatibility = compatibility;
            CheckedAtUtc = checkedAtUtc.ToUniversalTime();
            ReleasedAtUtc = releasedAtUtc?.ToUniversalTime();
            ReleaseNotes = releaseNotes ?? string.Empty;
            VerifiedReleaseUri = ValidateUri(verifiedReleaseUri);
            StatusDetail = statusDetail ?? string.Empty;
            CompatibilityReasons = new ReadOnlyCollection<string>(
                (compatibilityReasons ?? Array.Empty<string>())
                    .Select(value => (value ?? string.Empty).Trim())
                    .Where(value => value.Length != 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList());
            ValidateState();
        }

        public string SchemaVersion => SchemaVersionValue;
        public DdfProductUpdateComponentKind ComponentKind { get; }
        public string ComponentId { get; }
        public string CurrentVersion { get; }
        public string AvailableVersion { get; }
        public DdfProductUpdateChannel Channel { get; }
        public DdfProductUpdateState State { get; }
        public DdfProductUpdateCompatibility Compatibility { get; }
        public DateTimeOffset CheckedAtUtc { get; }
        public DateTimeOffset? ReleasedAtUtc { get; }
        public string ReleaseNotes { get; }
        public Uri VerifiedReleaseUri { get; }
        public string StatusDetail { get; }
        public IReadOnlyList<string> CompatibilityReasons { get; }
        public bool IsActionable => State == DdfProductUpdateState.UpdateAvailable &&
            Compatibility == DdfProductUpdateCompatibility.Compatible &&
            VerifiedReleaseUri != null;

        public static DdfProductUpdateResult Unknown(
            DdfProductUpdateComponentKind componentKind, string componentId,
            string currentVersion, DdfProductUpdateChannel channel,
            DateTimeOffset checkedAtUtc, string statusDetail) =>
            new DdfProductUpdateResult(componentKind, componentId,
                currentVersion, null, channel, DdfProductUpdateState.Unknown,
                DdfProductUpdateCompatibility.Unknown, checkedAtUtc, null,
                null, null, statusDetail, null);

        public static DdfProductUpdateResult UpToDate(
            DdfProductUpdateComponentKind componentKind, string componentId,
            string currentVersion, string latestVersion,
            DdfProductUpdateChannel channel, DateTimeOffset checkedAtUtc) =>
            new DdfProductUpdateResult(componentKind, componentId,
                currentVersion, latestVersion, channel,
                DdfProductUpdateState.UpToDate,
                DdfProductUpdateCompatibility.Compatible, checkedAtUtc, null,
                null, null, null, null);

        public static DdfProductUpdateResult Available(
            DdfProductUpdateComponentKind componentKind, string componentId,
            string currentVersion, string availableVersion,
            DdfProductUpdateChannel channel,
            DdfProductUpdateCompatibility compatibility,
            DateTimeOffset checkedAtUtc, DateTimeOffset releasedAtUtc,
            string releaseNotes, Uri verifiedReleaseUri,
            IEnumerable<string> compatibilityReasons = null) =>
            new DdfProductUpdateResult(componentKind, componentId,
                currentVersion, availableVersion, channel,
                compatibility == DdfProductUpdateCompatibility.Compatible
                    ? DdfProductUpdateState.UpdateAvailable
                    : DdfProductUpdateState.Incompatible,
                compatibility, checkedAtUtc, releasedAtUtc, releaseNotes,
                verifiedReleaseUri, null, compatibilityReasons);

        internal static DdfProductUpdateResult Rehydrate(ResultDocument document)
        {
            if (document == null || document.SchemaVersion != SchemaVersionValue)
                throw new InvalidDataException("Versione del risultato aggiornamenti non supportata.");
            return new DdfProductUpdateResult(document.ComponentKind,
                document.ComponentId, document.CurrentVersion,
                document.AvailableVersion, document.Channel, document.State,
                document.Compatibility, document.CheckedAtUtc,
                document.ReleasedAtUtc, document.ReleaseNotes,
                string.IsNullOrWhiteSpace(document.VerifiedReleaseUri)
                    ? null
                    : new Uri(document.VerifiedReleaseUri, UriKind.Absolute),
                document.StatusDetail, document.CompatibilityReasons);
        }

        internal ResultDocument ToDocument() => new ResultDocument
        {
            SchemaVersion = SchemaVersionValue,
            ComponentKind = ComponentKind,
            ComponentId = ComponentId,
            CurrentVersion = CurrentVersion,
            AvailableVersion = AvailableVersion.Length == 0 ? null : AvailableVersion,
            Channel = Channel,
            State = State,
            Compatibility = Compatibility,
            CheckedAtUtc = CheckedAtUtc,
            ReleasedAtUtc = ReleasedAtUtc,
            ReleaseNotes = ReleaseNotes.Length == 0 ? null : ReleaseNotes,
            VerifiedReleaseUri = VerifiedReleaseUri?.AbsoluteUri,
            StatusDetail = StatusDetail.Length == 0 ? null : StatusDetail,
            CompatibilityReasons = CompatibilityReasons.Count == 0
                ? null
                : CompatibilityReasons.ToArray()
        };

        private void ValidateState()
        {
            if (State == DdfProductUpdateState.Unknown)
            {
                if (Compatibility != DdfProductUpdateCompatibility.Unknown ||
                    AvailableVersion.Length != 0 || VerifiedReleaseUri != null ||
                    ReleasedAtUtc.HasValue || StatusDetail.Length == 0)
                {
                    throw new ArgumentException("Risultato unknown incoerente.");
                }
                return;
            }
            if (State == DdfProductUpdateState.UpToDate)
            {
                if (Compatibility != DdfProductUpdateCompatibility.Compatible ||
                    AvailableVersion.Length == 0 || VerifiedReleaseUri != null ||
                    ReleasedAtUtc.HasValue)
                {
                    throw new ArgumentException("Risultato up-to-date incoerente.");
                }
                return;
            }
            if (AvailableVersion.Length == 0 || VerifiedReleaseUri == null ||
                !ReleasedAtUtc.HasValue)
                throw new ArgumentException("Una release candidata richiede versione, data e URI verificata.");
            if (State == DdfProductUpdateState.UpdateAvailable &&
                Compatibility != DdfProductUpdateCompatibility.Compatible)
                throw new ArgumentException("Aggiornamento disponibile non compatibile.");
            if (State == DdfProductUpdateState.Incompatible &&
                (Compatibility != DdfProductUpdateCompatibility.Incompatible ||
                 CompatibilityReasons.Count == 0))
                throw new ArgumentException("Aggiornamento incompatibile privo di motivazione.");
        }

        internal static string RequireComponentId(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Any(character =>
                !(char.IsLower(character) || char.IsDigit(character) ||
                  character == '-' || character == '_' || character == '.')))
                throw new ArgumentException("Identità componente non valida.", nameof(value));
            return normalized;
        }

        internal static string RequireVersion(string value, string parameterName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (!SemanticVersion.IsMatch(normalized))
                throw new ArgumentException("Versione SemVer non valida.", parameterName);
            return normalized;
        }

        private static Uri ValidateUri(Uri value)
        {
            if (value == null) return null;
            if (!value.IsAbsoluteUri || value.Scheme != Uri.UriSchemeHttps ||
                value.UserInfo.Length != 0)
                throw new ArgumentException("La URI verificata deve essere HTTPS assoluta e priva di credenziali.", nameof(value));
            return value;
        }

        internal sealed class ResultDocument
        {
            public string SchemaVersion { get; set; }
            public DdfProductUpdateComponentKind ComponentKind { get; set; }
            public string ComponentId { get; set; }
            public string CurrentVersion { get; set; }
            public string AvailableVersion { get; set; }
            public DdfProductUpdateChannel Channel { get; set; }
            public DdfProductUpdateState State { get; set; }
            public DdfProductUpdateCompatibility Compatibility { get; set; }
            public DateTimeOffset CheckedAtUtc { get; set; }
            public DateTimeOffset? ReleasedAtUtc { get; set; }
            public string ReleaseNotes { get; set; }
            public string VerifiedReleaseUri { get; set; }
            public string StatusDetail { get; set; }
            public string[] CompatibilityReasons { get; set; }
        }
    }

    public static class DdfProductUpdateResultJson
    {
        private static readonly JsonSerializerOptions Options = CreateOptions();

        public static string Serialize(DdfProductUpdateResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return JsonSerializer.Serialize(result.ToDocument(), Options);
        }

        public static DdfProductUpdateResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Risultato aggiornamenti vuoto.");
            try
            {
                DdfProductUpdateResult.ResultDocument document =
                    JsonSerializer.Deserialize<DdfProductUpdateResult.ResultDocument>(
                        json, Options);
                return DdfProductUpdateResult.Rehydrate(document);
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is ArgumentException || exception is UriFormatException ||
                exception is InvalidOperationException)
            {
                throw new InvalidDataException("Risultato aggiornamenti non valido.", exception);
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
