using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSyntaxProfileConversionResult
    {
        private readonly IReadOnlyList<Replacement> replacements;
        private readonly int sourceLength;

        internal DdfSyntaxProfileConversionResult(string text, int sourceLength,
            IReadOnlyList<Replacement> replacements)
        {
            Text = text ?? string.Empty;
            this.sourceLength = sourceLength;
            this.replacements = replacements ?? Array.Empty<Replacement>();
        }

        public string Text { get; }
        public int ReplacementCount => replacements.Count;
        public bool Changed => ReplacementCount > 0;

        public int MapPosition(int sourcePosition)
        {
            if (sourcePosition < 0 || sourcePosition > sourceLength)
                throw new ArgumentOutOfRangeException(nameof(sourcePosition));
            int delta = 0;
            foreach (Replacement replacement in replacements)
            {
                if (sourcePosition < replacement.Start) break;
                if (sourcePosition <= replacement.Start + replacement.Length)
                    return replacement.Start + delta + Math.Min(
                        sourcePosition - replacement.Start, replacement.Text.Length);
                delta += replacement.Text.Length - replacement.Length;
            }
            return sourcePosition + delta;
        }

        public EditorEdit CreateEdit(string source, int selectionStart, int selectionLength)
        {
            source = source ?? throw new ArgumentNullException(nameof(source));
            if (source.Length != sourceLength)
                throw new ArgumentException("The source does not match the converted input.", nameof(source));
            if (selectionStart < 0 || selectionLength < 0 || selectionStart > source.Length - selectionLength)
                throw new ArgumentOutOfRangeException(nameof(selectionStart));
            int mappedStart = MapPosition(selectionStart);
            int mappedEnd = MapPosition(selectionStart + selectionLength);
            return new EditorEdit(0, source.Length, Text, mappedStart,
                Math.Max(0, mappedEnd - mappedStart));
        }

        internal sealed class Replacement
        {
            public Replacement(int start, int length, string text)
            {
                Start = start;
                Length = length;
                Text = text;
            }

            public int Start { get; }
            public int Length { get; }
            public string Text { get; }
        }
    }

    /// <summary>
    /// Converts syntax between semantically equivalent language profiles. Only
    /// tokens carrying a stable semantic role are rewritten; identifiers,
    /// comments, strings and library directives remain byte-for-byte unchanged.
    /// </summary>
    public static class DdfSyntaxProfileConverter
    {
        public static DdfSyntaxProfileConversionResult Convert(string source,
            DdfLanguageDefinition sourceLanguage, DdfLanguageDefinition targetLanguage)
        {
            source = source ?? throw new ArgumentNullException(nameof(source));
            sourceLanguage = sourceLanguage ?? throw new ArgumentNullException(nameof(sourceLanguage));
            targetLanguage = targetLanguage ?? throw new ArgumentNullException(nameof(targetLanguage));

            var replacements = new List<DdfSyntaxProfileConversionResult.Replacement>();
            foreach (DdfToken token in DdfLexer.Lex(source, sourceLanguage).Tokens)
            {
                string current = source.Substring(token.Start, token.Length);
                string replacement = FindReplacement(current, token.Kind, sourceLanguage, targetLanguage);
                if (string.IsNullOrEmpty(replacement) || string.Equals(current, replacement, StringComparison.Ordinal))
                    continue;
                replacements.Add(new DdfSyntaxProfileConversionResult.Replacement(
                    token.Start, token.Length, replacement));
            }

            if (replacements.Count == 0)
                return new DdfSyntaxProfileConversionResult(source, source.Length, replacements.AsReadOnly());

            var output = new StringBuilder(source.Length);
            int position = 0;
            foreach (DdfSyntaxProfileConversionResult.Replacement replacement in replacements)
            {
                output.Append(source, position, replacement.Start - position);
                output.Append(replacement.Text);
                position = replacement.Start + replacement.Length;
            }
            output.Append(source, position, source.Length - position);
            return new DdfSyntaxProfileConversionResult(output.ToString(), source.Length, replacements.AsReadOnly());
        }

        private static string FindReplacement(string text, DdfTokenKind kind,
            DdfLanguageDefinition source, DdfLanguageDefinition target)
        {
            if (kind == DdfTokenKind.DataTypeKeyword || kind == DdfTokenKind.FunctionKeyword ||
                kind == DdfTokenKind.ControlFlowKeyword)
            {
                if (!source.TryGetKeyword(text, out DdfKeywordDefinition keyword) ||
                    string.Equals(keyword.RoleId, DdfSyntaxRoles.None, StringComparison.OrdinalIgnoreCase)) return null;
                return UniqueText(target.Keywords.Where(candidate =>
                    string.Equals(candidate.RoleId, keyword.RoleId, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.Text));
            }

            if (kind == DdfTokenKind.BooleanLiteral && source.TryGetBooleanLiteralValue(text, out bool value))
                return UniqueText(target.BooleanLiterals.Where(candidate =>
                    target.TryGetBooleanLiteralValue(candidate, out bool candidateValue) && candidateValue == value));

            if (kind == DdfTokenKind.Operator && source.TryGetOperator(text, out DdfOperatorDefinition op) &&
                op.Role != DdfOperatorRole.None)
                return UniqueText(target.Operators.Where(candidate => candidate.Role == op.Role)
                    .Select(candidate => candidate.Text));

            return null;
        }

        private static string UniqueText(IEnumerable<string> candidates)
        {
            List<string> values = candidates.Distinct(StringComparer.Ordinal).Take(2).ToList();
            return values.Count == 1 ? values[0] : null;
        }
    }
}
