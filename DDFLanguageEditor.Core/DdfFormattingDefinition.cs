using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfQuickFixRule
    {
        public DdfQuickFixRule(string diagnosticCode, string title, string replacement)
        {
            if (string.IsNullOrWhiteSpace(diagnosticCode)) throw new ArgumentException("Diagnostic code is required.", nameof(diagnosticCode));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Quick Fix title is required.", nameof(title));
            DiagnosticCode = diagnosticCode; Title = title; Replacement = replacement ?? string.Empty;
        }
        public string DiagnosticCode { get; }
        public string Title { get; }
        public string Replacement { get; }
    }

    public sealed class DdfFormattingDefinition
    {
        private readonly IReadOnlyDictionary<char, char> pairs;
        private readonly IReadOnlyDictionary<string, DdfQuickFixRule> quickFixes;

        public DdfFormattingDefinition(string blockOpen, string blockClose, string groupOpen, string groupClose,
            string indexOpen, string indexClose, string statementTerminator, string separator,
            IEnumerable<KeyValuePair<char, char>> autoPairs, IEnumerable<string> indentAfter,
            bool blankLineBetweenStatements, IEnumerable<DdfQuickFixRule> quickFixRules)
        {
            BlockOpen = One(blockOpen, nameof(blockOpen)); BlockClose = One(blockClose, nameof(blockClose));
            GroupOpen = One(groupOpen, nameof(groupOpen)); GroupClose = One(groupClose, nameof(groupClose));
            IndexOpen = One(indexOpen, nameof(indexOpen)); IndexClose = One(indexClose, nameof(indexClose));
            StatementTerminator = One(statementTerminator, nameof(statementTerminator));
            Separator = One(separator, nameof(separator));
            pairs = (autoPairs ?? Array.Empty<KeyValuePair<char, char>>()).ToDictionary(item => item.Key, item => item.Value);
            IndentAfter = new HashSet<string>(indentAfter ?? Array.Empty<string>(), StringComparer.Ordinal);
            BlankLineBetweenStatements = blankLineBetweenStatements;
            quickFixes = (quickFixRules ?? Array.Empty<DdfQuickFixRule>()).ToDictionary(item => item.DiagnosticCode, StringComparer.Ordinal);
        }

        public char BlockOpen { get; }
        public char BlockClose { get; }
        public char GroupOpen { get; }
        public char GroupClose { get; }
        public char IndexOpen { get; }
        public char IndexClose { get; }
        public char StatementTerminator { get; }
        public char Separator { get; }
        public IReadOnlyCollection<string> IndentAfter { get; }
        public bool BlankLineBetweenStatements { get; }
        public IEnumerable<DdfQuickFixRule> QuickFixRules => quickFixes.Values;
        public bool TryGetClosing(char opening, out char closing) => pairs.TryGetValue(opening, out closing);
        public bool IsClosing(char value) => pairs.Values.Contains(value);
        public bool TryGetQuickFix(string code, out DdfQuickFixRule rule) => quickFixes.TryGetValue(code ?? string.Empty, out rule);

        internal static DdfFormattingDefinition CreateLegacyDefault() => new DdfFormattingDefinition(
            "{", "}", "(", ")", "[", "]", ";", ",",
            new[] { Pair('(', ')'), Pair('[', ']'), Pair('{', '}'), Pair('"', '"'), Pair('\'', '\'') },
            new[] { "{", "(" }, true, new[]
            {
                new DdfQuickFixRule("DDF001", "Chiudi la stringa con \"", "\""),
                new DdfQuickFixRule("DDF002", "Chiudi il commento con */", "*/"),
                new DdfQuickFixRule("DDF003", "Chiudi la direttiva libreria con '", "'")
            });

        private static KeyValuePair<char, char> Pair(char open, char close) => new KeyValuePair<char, char>(open, close);
        private static char One(string value, string name)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 1) throw new ArgumentException("A single character is required.", name);
            return value[0];
        }
    }
}
