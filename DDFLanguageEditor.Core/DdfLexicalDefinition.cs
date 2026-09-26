using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfCharacterSetDefinition
    {
        public DdfCharacterSetDefinition(
            bool unicodeLetters = false,
            bool unicodeDigits = false,
            bool unicodeWhitespace = false,
            string characters = null)
        {
            UnicodeLetters = unicodeLetters;
            UnicodeDigits = unicodeDigits;
            UnicodeWhitespace = unicodeWhitespace;
            Characters = characters ?? string.Empty;
        }

        public bool UnicodeLetters { get; }
        public bool UnicodeDigits { get; }
        public bool UnicodeWhitespace { get; }
        public string Characters { get; }

        public bool Contains(char value)
        {
            return (UnicodeLetters && char.IsLetter(value)) ||
                   (UnicodeDigits && char.IsDigit(value)) ||
                   (UnicodeWhitespace && char.IsWhiteSpace(value)) ||
                   Characters.IndexOf(value) >= 0;
        }
    }

    public sealed class DdfNumberRuleDefinition
    {
        public DdfNumberRuleDefinition(
            DdfCharacterSetDefinition digits,
            string decimalSeparator = null,
            bool requireFractionDigits = true)
        {
            Digits = digits ?? throw new ArgumentNullException(nameof(digits));
            if (decimalSeparator != null && decimalSeparator.Length > 1)
                throw new ArgumentException("The decimal separator must contain at most one character.", nameof(decimalSeparator));
            DecimalSeparator = decimalSeparator ?? string.Empty;
            RequireFractionDigits = requireFractionDigits;
        }

        public DdfCharacterSetDefinition Digits { get; }
        public string DecimalSeparator { get; }
        public bool RequireFractionDigits { get; }
    }

    public sealed class DdfDelimitedTokenRule
    {
        public DdfDelimitedTokenRule(
            DdfTokenKind tokenKind,
            string start,
            string end = null,
            bool endsAtLineBreak = false,
            bool allowLineBreak = true,
            string escapeCharacter = null,
            string unterminatedCode = null,
            string unterminatedMessage = null)
            : this(tokenKind, start, end, endsAtLineBreak, allowLineBreak,
                  escapeCharacter, unterminatedCode, unterminatedMessage, null)
        {
        }

        public DdfDelimitedTokenRule(
            DdfTokenKind tokenKind,
            string start,
            string end,
            bool endsAtLineBreak,
            bool allowLineBreak,
            string escapeCharacter,
            string unterminatedCode,
            string unterminatedMessage,
            string allowedEscapeCharacters)
        {
            if (tokenKind != DdfTokenKind.LineComment && tokenKind != DdfTokenKind.BlockComment &&
                tokenKind != DdfTokenKind.StringLiteral && tokenKind != DdfTokenKind.LibraryDirective)
                throw new ArgumentException("Unsupported delimited token kind.", nameof(tokenKind));
            if (string.IsNullOrEmpty(start)) throw new ArgumentException("A start marker is required.", nameof(start));
            if (endsAtLineBreak != (tokenKind == DdfTokenKind.LineComment))
                throw new ArgumentException("Only line comments must end at a line break.", nameof(endsAtLineBreak));
            if (endsAtLineBreak && !string.IsNullOrEmpty(end))
                throw new ArgumentException("A line-delimited rule cannot also define an end marker.", nameof(end));
            if (!endsAtLineBreak && string.IsNullOrEmpty(end))
                throw new ArgumentException("An end marker is required.", nameof(end));
            if (escapeCharacter != null && escapeCharacter.Length > 1)
                throw new ArgumentException("The escape character must contain at most one character.", nameof(escapeCharacter));
            if (!string.IsNullOrEmpty(allowedEscapeCharacters) && string.IsNullOrEmpty(escapeCharacter))
                throw new ArgumentException("Allowed escapes require an escape character.", nameof(allowedEscapeCharacters));
            if (!endsAtLineBreak && (string.IsNullOrWhiteSpace(unterminatedCode) || string.IsNullOrWhiteSpace(unterminatedMessage)))
                throw new ArgumentException("Delimited rules require an unterminated diagnostic.");

            TokenKind = tokenKind;
            Start = start;
            End = end ?? string.Empty;
            EndsAtLineBreak = endsAtLineBreak;
            AllowLineBreak = allowLineBreak;
            EscapeCharacter = escapeCharacter ?? string.Empty;
            AllowedEscapeCharacters = allowedEscapeCharacters ?? string.Empty;
            UnterminatedCode = unterminatedCode ?? string.Empty;
            UnterminatedMessage = unterminatedMessage ?? string.Empty;
        }

        public DdfTokenKind TokenKind { get; }
        public string Start { get; }
        public string End { get; }
        public bool EndsAtLineBreak { get; }
        public bool AllowLineBreak { get; }
        public string EscapeCharacter { get; }
        public string AllowedEscapeCharacters { get; }
        public string UnterminatedCode { get; }
        public string UnterminatedMessage { get; }
    }

    public sealed class DdfLexicalDefinition
    {
        private readonly IReadOnlyList<DdfDelimitedTokenRule> matchingRules;

        public DdfLexicalDefinition(
            DdfCharacterSetDefinition whitespace,
            DdfCharacterSetDefinition identifierStart,
            DdfCharacterSetDefinition identifierPart,
            DdfNumberRuleDefinition number,
            IEnumerable<DdfDelimitedTokenRule> delimitedTokens,
            bool longestMatch = true)
        {
            Whitespace = whitespace ?? throw new ArgumentNullException(nameof(whitespace));
            IdentifierStart = identifierStart ?? throw new ArgumentNullException(nameof(identifierStart));
            IdentifierPart = identifierPart ?? throw new ArgumentNullException(nameof(identifierPart));
            Number = number ?? throw new ArgumentNullException(nameof(number));
            var declaredRules = (delimitedTokens ?? throw new ArgumentNullException(nameof(delimitedTokens))).ToList();
            if (declaredRules.Any(item => item == null)) throw new ArgumentException("Delimited token rules cannot be null.", nameof(delimitedTokens));
            if (declaredRules.Select(item => item.Start).Distinct(StringComparer.Ordinal).Count() != declaredRules.Count)
                throw new ArgumentException("Delimited token start markers must be unique.", nameof(delimitedTokens));

            DelimitedTokens = declaredRules.AsReadOnly();
            LongestMatch = longestMatch;
            IEnumerable<DdfDelimitedTokenRule> orderedRules = longestMatch
                ? declaredRules.OrderByDescending(item => item.Start.Length).ThenBy(item => item.Start, StringComparer.Ordinal)
                : declaredRules;
            matchingRules = orderedRules.ToList().AsReadOnly();
        }

        public DdfCharacterSetDefinition Whitespace { get; }
        public DdfCharacterSetDefinition IdentifierStart { get; }
        public DdfCharacterSetDefinition IdentifierPart { get; }
        public DdfNumberRuleDefinition Number { get; }
        public IReadOnlyList<DdfDelimitedTokenRule> DelimitedTokens { get; }
        public bool LongestMatch { get; }

        public bool IsWhitespace(char value) => Whitespace.Contains(value);
        public bool IsIdentifierStart(char value) => IdentifierStart.Contains(value);
        public bool IsIdentifierPart(char value) => IdentifierPart.Contains(value);
        public bool IsDigit(char value) => Number.Digits.Contains(value);

        public bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsIdentifierStart(value[0])) return false;
            for (int index = 1; index < value.Length; index++)
                if (!IsIdentifierPart(value[index])) return false;
            return true;
        }

        public DdfDelimitedTokenRule FindDelimitedToken(string source, int position)
        {
            foreach (DdfDelimitedTokenRule rule in matchingRules)
                if (StartsWith(source, position, rule.Start)) return rule;
            return null;
        }

        public static DdfLexicalDefinition CreateDdfCompatibleDefault()
        {
            var unicodeWhitespace = new DdfCharacterSetDefinition(unicodeWhitespace: true);
            var identifierStart = new DdfCharacterSetDefinition(unicodeLetters: true, characters: "_");
            var identifierPart = new DdfCharacterSetDefinition(unicodeLetters: true, unicodeDigits: true, characters: "_");
            var digits = new DdfCharacterSetDefinition(characters: "0123456789");
            return new DdfLexicalDefinition(
                unicodeWhitespace,
                identifierStart,
                identifierPart,
                new DdfNumberRuleDefinition(digits, ".", true),
                new[]
                {
                    new DdfDelimitedTokenRule(DdfTokenKind.LineComment, "//", endsAtLineBreak: true),
                    new DdfDelimitedTokenRule(DdfTokenKind.BlockComment, "/*", "*/", unterminatedCode: "DDF002", unterminatedMessage: "Commento a blocchi non terminato."),
                    new DdfDelimitedTokenRule(DdfTokenKind.LibraryDirective, "@@'", "'", unterminatedCode: "DDF003", unterminatedMessage: "Inclusione di libreria non terminata."),
                    new DdfDelimitedTokenRule(DdfTokenKind.StringLiteral, "\"", "\"", false, false,
                        "\\", "DDF001", "Stringa non terminata.", "\"\\nrt")
                });
        }

        private static bool StartsWith(string source, int position, string value)
        {
            return position + value.Length <= source.Length &&
                   string.CompareOrdinal(source, position, value, 0, value.Length) == 0;
        }
    }
}
