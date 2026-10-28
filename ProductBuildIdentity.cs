using System;
using System.IO;
using System.Text.Json;

namespace DDF___Program_Language_Editor
{
    internal sealed class ProductBuildIdentity
    {
        private const string ManifestFileName = "BUILD-INFO.json";
        private const int MaximumManifestBytes = 256 * 1024;

        private ProductBuildIdentity(bool isManifested, string buildId,
            string commit, bool dirty, string configuration, string createdLocal,
            string failure)
        {
            IsManifested = isManifested;
            BuildId = buildId ?? string.Empty;
            Commit = commit ?? string.Empty;
            IsDirty = dirty;
            Configuration = configuration ?? string.Empty;
            CreatedLocal = createdLocal ?? string.Empty;
            Failure = failure ?? string.Empty;
        }

        public static ProductBuildIdentity Current { get; } =
            Load(AppContext.BaseDirectory);

        public bool IsManifested { get; }
        public string BuildId { get; }
        public string Commit { get; }
        public bool IsDirty { get; }
        public string Configuration { get; }
        public string CreatedLocal { get; }
        public string Failure { get; }

        public string ShortDisplay => IsManifested
            ? "Build " + BuildId
            : "Build locale non identificata";

        public string DetailDisplay => IsManifested
            ? ShortDisplay + " · " + Configuration +
              (IsDirty ? " · sorgenti modificati" : " · sorgenti puliti")
            : ShortDisplay + (string.IsNullOrWhiteSpace(Failure)
                ? string.Empty
                : " · " + Failure);

        internal static ProductBuildIdentity Load(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return Unavailable("directory applicazione non disponibile");

            string path = Path.Combine(baseDirectory, ManifestFileName);
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) return Unavailable("BUILD-INFO.json assente");
                if (file.Length <= 0 || file.Length > MaximumManifestBytes)
                    return Unavailable("BUILD-INFO.json fuori quota");

                using (JsonDocument document = JsonDocument.Parse(
                    File.ReadAllText(path)))
                {
                    JsonElement root = document.RootElement;
                    string schemaVersion = ReadString(root, "schemaVersion");
                    string product = ReadString(root, "product");
                    string buildId = ReadString(root, "buildId");
                    string commit = ReadString(root, "gitCommit");
                    string configuration = ReadString(root, "configuration");
                    string createdLocal = ReadString(root, "createdLocal");
                    bool dirty = root.TryGetProperty("gitDirty", out JsonElement dirtyValue) &&
                        (dirtyValue.ValueKind == JsonValueKind.True ||
                         dirtyValue.ValueKind == JsonValueKind.False) &&
                        dirtyValue.GetBoolean();
                    if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal) ||
                        !string.Equals(product, "DDFLanguageEditor", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(buildId) ||
                        string.IsNullOrWhiteSpace(commit) ||
                        string.IsNullOrWhiteSpace(configuration))
                    {
                        return Unavailable("BUILD-INFO.json non valido");
                    }

                    return new ProductBuildIdentity(true, buildId, commit, dirty,
                        configuration, createdLocal, string.Empty);
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return Unavailable("BUILD-INFO.json illeggibile");
            }
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : string.Empty;
        }

        private static ProductBuildIdentity Unavailable(string failure)
        {
            return new ProductBuildIdentity(false, string.Empty, string.Empty,
                false, string.Empty, string.Empty, failure);
        }
    }
}
