using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    // Source-only boundary: never exposes parser nodes, mutable IR or CLR symbols.
    public sealed class DdfExtensionSourceSnapshot
    {
        public const string ContractVersion = "1.0";
        public const int MaximumUtf8Bytes = 1048576;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public DdfExtensionSourceSnapshot(string relativePath, string sourceText)
        {
            RelativePath = RequireRelativePath(relativePath, nameof(relativePath));
            if (sourceText == null || sourceText.Length > MaximumUtf8Bytes)
                throw new ArgumentException("Sorgente assente o oltre quota.", nameof(sourceText));
            byte[] bytes = StrictUtf8.GetBytes(sourceText);
            if (bytes.Length > MaximumUtf8Bytes)
                throw new ArgumentException("Sorgente oltre quota UTF-8.", nameof(sourceText));
            SourceText = sourceText;
            Utf8ByteCount = bytes.Length;
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        public string RelativePath { get; }
        public string SourceText { get; }
        public string Sha256 { get; }
        internal int Utf8ByteCount { get; }

        internal static string RequireRelativePath(string value, string name)
        {
            if (value == null || value.Length == 0 || value.Length > 512 ||
                value[0] == '/' || value.Contains('\\') || value.Contains(':') ||
                value.Any(char.IsControl) || value.Split('/').Any(segment =>
                    segment.Length == 0 || segment == "." || segment == ".."))
                throw new ArgumentException("Percorso sorgente relativo non valido.", name);
            return value;
        }
    }

    public sealed class DdfExtensionSemanticRequest
    {
        private static readonly Regex RulePattern = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        public DdfExtensionSemanticRequest(DdfExtensionPointDescriptor point,
            DdfExtensionSourceSnapshot document, string ruleId, int start, int length)
        {
            Point = point ?? throw new ArgumentNullException(nameof(point));
            if (point.Kind != DdfExtensionPointKind.SemanticOperation ||
                !point.RequiredCapabilities.Contains("file.read", StringComparer.Ordinal))
                throw new ArgumentException("Punto semantico con file.read richiesto.", nameof(point));
            Document = document ?? throw new ArgumentNullException(nameof(document));
            if (ruleId == null || ruleId.Length > 128 || !RulePattern.IsMatch(ruleId))
                throw new ArgumentException("Regola grammaticale non valida.", nameof(ruleId));
            if (start < 0 || length < 0 || start > document.SourceText.Length ||
                length > document.SourceText.Length - start)
                throw new ArgumentOutOfRangeException(nameof(start),
                    "Intervallo semantico fuori dal sorgente.");
            RuleId = ruleId;
            Start = start;
            Length = length;
        }

        public string ContractVersion => DdfExtensionSourceSnapshot.ContractVersion;
        public DdfExtensionPointDescriptor Point { get; }
        public DdfExtensionSourceSnapshot Document { get; }
        public string RuleId { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public enum DdfExtensionBuildTargetKind
    {
        Executable,
        Library
    }

    public sealed class DdfExtensionBuildRequest
    {
        public DdfExtensionBuildRequest(DdfExtensionPointDescriptor point,
            IEnumerable<DdfExtensionSourceSnapshot> sources,
            string entrySource, DdfExtensionBuildTargetKind targetKind)
        {
            Point = point ?? throw new ArgumentNullException(nameof(point));
            if (point.Kind != DdfExtensionPointKind.BuildBackend ||
                !point.RequiredCapabilities.Contains("file.read", StringComparer.Ordinal) ||
                !point.RequiredCapabilities.Contains("file.write", StringComparer.Ordinal))
                throw new ArgumentException("Backend con file.read/file.write richiesto.",
                    nameof(point));
            if (!Enum.IsDefined(typeof(DdfExtensionBuildTargetKind), targetKind))
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            DdfExtensionSourceSnapshot[] copy = (sources ??
                throw new ArgumentNullException(nameof(sources))).ToArray();
            if (copy.Length == 0 || copy.Length > 128 || copy.Any(item => item == null) ||
                copy.Select(item => item.RelativePath).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() != copy.Length ||
                copy.Sum(item => (long)item.Utf8ByteCount) >
                    8L * DdfExtensionSourceSnapshot.MaximumUtf8Bytes)
                throw new ArgumentException("Sorgenti build vuoti, duplicati o oltre quota.",
                    nameof(sources));
            EntrySource = DdfExtensionSourceSnapshot.RequireRelativePath(entrySource,
                nameof(entrySource));
            if (!copy.Any(item => string.Equals(item.RelativePath, EntrySource,
                StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Entry source non presente nei sorgenti.",
                    nameof(entrySource));
            Sources = new ReadOnlyCollection<DdfExtensionSourceSnapshot>(copy);
            TargetKind = targetKind;
        }

        public string ContractVersion => DdfExtensionSourceSnapshot.ContractVersion;
        public DdfExtensionPointDescriptor Point { get; }
        public IReadOnlyList<DdfExtensionSourceSnapshot> Sources { get; }
        public string EntrySource { get; }
        public DdfExtensionBuildTargetKind TargetKind { get; }
    }
}
