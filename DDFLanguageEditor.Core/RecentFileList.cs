using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public static class RecentFileList
    {
        public const int DefaultLimit = 10;

        public static IReadOnlyList<string> Parse(string value, int limit = DefaultLimit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return Normalize(value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), limit);
        }

        public static IReadOnlyList<string> Add(
            IEnumerable<string> current,
            string path,
            int limit = DefaultLimit)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            string normalizedPath = DdfDocumentFile.NormalizePath(path);
            return Normalize(new[] { normalizedPath }.Concat(current), limit);
        }

        public static string Serialize(IEnumerable<string> paths, int limit = DefaultLimit)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            return string.Join(Environment.NewLine, Normalize(paths, limit));
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> paths, int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            var result = new List<string>();
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string normalized;
                try
                {
                    normalized = DdfDocumentFile.NormalizePath(path.Trim());
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is System.Security.SecurityException)
                {
                    continue;
                }

                if (result.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count == limit)
                {
                    break;
                }
            }

            return result;
        }
    }
}
