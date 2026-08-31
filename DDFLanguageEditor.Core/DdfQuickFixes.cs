using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfQuickFix
    {
        public DdfQuickFix(
            DdfDiagnostic diagnostic,
            string title,
            int start,
            int length,
            string replacement,
            int selectionStart,
            int selectionLength = 0)
        {
            Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required.", nameof(title));
            if (start < 0 || length < 0 || selectionStart < 0 || selectionLength < 0)
                throw new ArgumentOutOfRangeException(nameof(start));

            Title = title;
            Start = start;
            Length = length;
            Replacement = replacement ?? string.Empty;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
        }

        public DdfDiagnostic Diagnostic { get; }

        public string Title { get; }

        public int Start { get; }

        public int Length { get; }

        public string Replacement { get; }

        public int SelectionStart { get; }

        public int SelectionLength { get; }

        public EditorEdit ToEditorEdit()
        {
            return new EditorEdit(Start, Length, Replacement, SelectionStart, SelectionLength);
        }

        public override string ToString()
        {
            return Title;
        }
    }

    public interface IDdfQuickFixProvider
    {
        IEnumerable<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic);
    }

    public sealed class DdfQuickFixService
    {
        private readonly IReadOnlyList<IDdfQuickFixProvider> providers;

        public DdfQuickFixService(IEnumerable<IDdfQuickFixProvider> providers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            this.providers = providers.Where(provider => provider != null).ToList().AsReadOnly();
        }

        public static DdfQuickFixService CreateDefault()
        {
            return new DdfQuickFixService(new IDdfQuickFixProvider[]
            {
                new DdfTerminatorQuickFixProvider(),
                new DdfBadTokenQuickFixProvider(),
                new DdfMissingTokenQuickFixProvider()
            });
        }

        public IReadOnlyList<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (diagnostic == null) throw new ArgumentNullException(nameof(diagnostic));

            return providers
                .SelectMany(provider => provider.GetFixes(source, diagnostic) ?? Enumerable.Empty<DdfQuickFix>())
                .Where(fix => IsValid(source, fix))
                .GroupBy(fix => new { fix.Title, fix.Start, fix.Length, fix.Replacement })
                .Select(group => group.First())
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<DdfQuickFix> GetFixesAt(
            string source,
            IEnumerable<DdfDiagnostic> diagnostics,
            int position)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));

            return diagnostics
                .Where(diagnostic => position >= diagnostic.Start && position <= Math.Min(source.Length, diagnostic.End))
                .SelectMany(diagnostic => GetFixes(source, diagnostic))
                .GroupBy(fix => new { fix.Title, fix.Start, fix.Length, fix.Replacement })
                .Select(group => group.First())
                .ToList()
                .AsReadOnly();
        }

        private static bool IsValid(string source, DdfQuickFix fix)
        {
            return fix != null &&
                   fix.Start <= source.Length &&
                   fix.Length <= source.Length - fix.Start &&
                   fix.SelectionStart <= source.Length - fix.Length + fix.Replacement.Length &&
                   fix.SelectionLength <= source.Length - fix.Length + fix.Replacement.Length - fix.SelectionStart;
        }
    }

    public sealed class DdfTerminatorQuickFixProvider : IDdfQuickFixProvider
    {
        public IEnumerable<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic)
        {
            string terminator;
            string title;
            switch (diagnostic.Code)
            {
                case "DDF001": terminator = "\""; title = "Chiudi la stringa con \""; break;
                case "DDF002": terminator = "*/"; title = "Chiudi il commento con */"; break;
                case "DDF003": terminator = "'"; title = "Chiudi la direttiva libreria con '"; break;
                default: yield break;
            }

            int insertion = Math.Min(source.Length, diagnostic.End);
            yield return new DdfQuickFix(
                diagnostic,
                title,
                insertion,
                0,
                terminator,
                insertion + terminator.Length);
        }
    }

    public sealed class DdfBadTokenQuickFixProvider : IDdfQuickFixProvider
    {
        public IEnumerable<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic)
        {
            if (diagnostic.Code != "DDF004") yield break;
            int start = Math.Min(source.Length, diagnostic.Start);
            int length = Math.Min(diagnostic.Length, source.Length - start);
            if (length <= 0) yield break;
            yield return new DdfQuickFix(
                diagnostic,
                "Rimuovi il carattere non riconosciuto",
                start,
                length,
                string.Empty,
                start);
        }
    }

    public sealed class DdfMissingTokenQuickFixProvider : IDdfQuickFixProvider
    {
        public IEnumerable<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic)
        {
            if (diagnostic.Code != "DDF102" || !TryExtractExpectedText(diagnostic.Message, out string expected))
                yield break;

            int insertion = diagnostic.InsertionPosition.HasValue
                ? Math.Min(source.Length, diagnostic.InsertionPosition.Value)
                : (diagnostic.End >= source.Length ? source.Length : diagnostic.Start);
            string replacement = IsWord(expected) ? expected + " " : expected;
            yield return new DdfQuickFix(
                diagnostic,
                "Inserisci " + (IsWord(expected) ? "la parola chiave '" : "il token '") + expected + "'",
                insertion,
                0,
                replacement,
                insertion + replacement.Length);
        }

        private static bool TryExtractExpectedText(string message, out string expected)
        {
            expected = null;
            if (string.IsNullOrEmpty(message)) return false;
            int firstQuote = message.IndexOf('\'');
            int lastQuote = message.LastIndexOf('\'');
            if (firstQuote < 0 || lastQuote <= firstQuote + 1) return false;
            expected = message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
            return expected.Length > 0;
        }

        private static bool IsWord(string value)
        {
            return value.All(character => char.IsLetterOrDigit(character) || character == '_');
        }
    }
}
