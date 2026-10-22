using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class WatchProjectExpressions
    {
        public string ProjectPath { get; set; }
        public List<string> Expressions { get; set; } = new List<string>();
    }

    public sealed class WatchExpressionSettings
    {
        public int FormatVersion { get; set; } = 1;
        public List<WatchProjectExpressions> Projects { get; set; } = new List<WatchProjectExpressions>();

        public IReadOnlyList<string> GetExpressions(string projectPath)
        {
            string key = NormalizePath(projectPath);
            if (key == null) return Array.Empty<string>();
            List<string> expressions = Projects.FirstOrDefault(item => string.Equals(item.ProjectPath, key,
                StringComparison.OrdinalIgnoreCase))?.Expressions;
            return expressions?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        public void SetExpressions(string projectPath, IEnumerable<string> expressions)
        {
            string key = NormalizePath(projectPath);
            if (key == null) return;
            Projects.RemoveAll(item => string.Equals(item.ProjectPath, key, StringComparison.OrdinalIgnoreCase));
            List<string> values = SanitizeExpressions(expressions);
            if (values.Count > 0) Projects.Add(new WatchProjectExpressions { ProjectPath = key, Expressions = values });
            Projects = Projects.OrderBy(item => item.ProjectPath, StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        }

        public string Serialize() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

        public static WatchExpressionSettings Parse(string value)
        {
            return ParseVersioned(value).Value ?? new WatchExpressionSettings();
        }

        public static DdfPersistenceReadResult<WatchExpressionSettings>
            ParseVersioned(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DdfPersistenceReadResult<WatchExpressionSettings>.Success(
                    value, null, new WatchExpressionSettings());
            DdfPersistencePreparation preparation =
                DdfPersistenceSerialization.PrepareJson(
                    DdfPersistenceArtifactKind.WatchExpressions, value);
            if (!preparation.Succeeded)
                return DdfPersistenceReadResult<WatchExpressionSettings>.Failed(
                    value, preparation, preparation.Failure);
            try
            {
                WatchExpressionSettings result = JsonSerializer
                    .Deserialize<WatchExpressionSettings>(
                        preparation.PreparedArtifact.Payload);
                if (result == null || result.FormatVersion != 1)
                    return DdfPersistenceReadResult<WatchExpressionSettings>.Failed(
                        value, preparation,
                        "Le espressioni Watch non rispettano lo schema corrente.");
                var sanitized = new WatchExpressionSettings();
                foreach (WatchProjectExpressions project in (result.Projects ?? new List<WatchProjectExpressions>()).Take(100))
                    sanitized.SetExpressions(project?.ProjectPath, project?.Expressions);
                return DdfPersistenceReadResult<WatchExpressionSettings>.Success(
                    value, preparation, sanitized);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                                               exception is NotSupportedException || exception is PathTooLongException)
            {
                return DdfPersistenceReadResult<WatchExpressionSettings>.Failed(
                    value, preparation,
                    "Espressioni Watch non valide: " + exception.Message);
            }
        }

        private static List<string> SanitizeExpressions(IEnumerable<string> expressions) =>
            (expressions ?? Array.Empty<string>()).Select(item => (item ?? string.Empty).Trim())
                .Where(item => item.Length > 0 && item.Length <= 512)
                .Distinct(StringComparer.Ordinal).Take(100).ToList();

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException ||
                                               exception is PathTooLongException) { return null; }
        }
    }
}
