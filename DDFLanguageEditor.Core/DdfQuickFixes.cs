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
            if (expected == "{" || expected == "}")
            {
                yield return CreateBraceFix(source, diagnostic, expected, insertion);
                yield break;
            }

            string replacement = expected;
            if (IsWord(expected))
            {
                if (insertion > 0 && !char.IsWhiteSpace(source[insertion - 1])) replacement = " " + replacement;
                if (insertion < source.Length && !char.IsWhiteSpace(source[insertion])) replacement += " ";
            }
            yield return new DdfQuickFix(
                diagnostic,
                "Inserisci " + (IsWord(expected) ? "la parola chiave '" : "il token '") + expected + "'",
                insertion,
                0,
                replacement,
                insertion + replacement.Length);
        }

        private static DdfQuickFix CreateBraceFix(
            string source,
            DdfDiagnostic diagnostic,
            string brace,
            int parserInsertion)
        {
            string newLine = source.Contains("\r\n") ? "\r\n" : "\n";
            int contextStart = Math.Min(
                source.Length,
                diagnostic.ContextStart ?? parserInsertion);
            string indentation = GetLineIndentation(source, contextStart);
            int currentStart = Math.Min(source.Length, diagnostic.Start);

            if (brace == "{" && parserInsertion < currentStart &&
                ContainsLineBreak(source, parserInsertion, currentStart))
            {
                int currentLineStart = FindLineStart(source, currentStart);
                string replacement = indentation + "{" + newLine;
                return new DdfQuickFix(
                    diagnostic,
                    "Apri il blocco con {",
                    currentLineStart,
                    0,
                    replacement,
                    currentLineStart + replacement.Length);
            }

            if (brace == "}" && parserInsertion < currentStart &&
                ContainsLineBreak(source, parserInsertion, currentStart))
            {
                int currentLineStart = FindLineStart(source, currentStart);
                string replacement = indentation + "}" + newLine;
                return new DdfQuickFix(
                    diagnostic,
                    "Chiudi il blocco con }",
                    currentLineStart,
                    0,
                    replacement,
                    currentLineStart + replacement.Length);
            }

            if (brace == "}" && diagnostic.InsertionPosition.HasValue &&
                diagnostic.InsertionPosition.Value >= parserInsertion &&
                diagnostic.InsertionPosition.Value >= source.TrimEnd(' ', '\t', '\r', '\n').Length)
            {
                bool endsWithLineBreak = source.EndsWith("\n", StringComparison.Ordinal);
                string replacement = (endsWithLineBreak ? string.Empty : newLine) + indentation + "}";
                return new DdfQuickFix(
                    diagnostic,
                    "Chiudi il blocco con }",
                    source.Length,
                    0,
                    replacement,
                    source.Length + replacement.Length);
            }

            string inlineReplacement = brace;
            if (parserInsertion > 0 && !char.IsWhiteSpace(source[parserInsertion - 1]))
                inlineReplacement = " " + inlineReplacement;
            if (parserInsertion < source.Length && !char.IsWhiteSpace(source[parserInsertion]))
                inlineReplacement += " ";
            return new DdfQuickFix(
                diagnostic,
                brace == "{" ? "Apri il blocco con {" : "Chiudi il blocco con }",
                parserInsertion,
                0,
                inlineReplacement,
                parserInsertion + inlineReplacement.Length);
        }

        private static int FindLineStart(string source, int position)
        {
            int start = Math.Min(position, source.Length);
            while (start > 0 && source[start - 1] != '\n') start--;
            return start;
        }

        private static string GetLineIndentation(string source, int position)
        {
            int start = FindLineStart(source, position);
            int end = start;
            while (end < source.Length && (source[end] == ' ' || source[end] == '\t')) end++;
            return source.Substring(start, end - start);
        }

        private static bool ContainsLineBreak(string source, int start, int end)
        {
            for (int position = Math.Max(0, start); position < Math.Min(source.Length, end); position++)
                if (source[position] == '\n') return true;
            return false;
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
