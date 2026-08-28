using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfKeywordRole
    {
        None,
        Structure,
        ReturnTypeMarker,
        If,
        While,
        Do,
        For,
        Return,
        Break,
        End
    }

    public enum DdfOperatorRole
    {
        None,
        DeclarationInitializer
    }

    public enum DdfOperatorAssociativity
    {
        Left,
        Right
    }

    public sealed class DdfKeywordDefinition
    {
        public DdfKeywordDefinition(string text, DdfTokenKind tokenKind, DdfKeywordRole role = DdfKeywordRole.None)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Keyword text is required.", nameof(text));
            if (tokenKind != DdfTokenKind.DataTypeKeyword &&
                tokenKind != DdfTokenKind.FunctionKeyword &&
                tokenKind != DdfTokenKind.ControlFlowKeyword)
            {
                throw new ArgumentException("The token kind is not a keyword category.", nameof(tokenKind));
            }

            Text = text;
            TokenKind = tokenKind;
            Role = role;
        }

        public string Text { get; }
        public DdfTokenKind TokenKind { get; }
        public DdfKeywordRole Role { get; }
    }

    public sealed class DdfOperatorDefinition
    {
        public DdfOperatorDefinition(
            string text,
            int binaryPrecedence = 0,
            DdfOperatorAssociativity associativity = DdfOperatorAssociativity.Left,
            int prefixPrecedence = 0,
            int postfixPrecedence = 0,
            DdfOperatorRole role = DdfOperatorRole.None)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Operator text is required.", nameof(text));
            if (binaryPrecedence < 0) throw new ArgumentOutOfRangeException(nameof(binaryPrecedence));
            if (prefixPrecedence < 0) throw new ArgumentOutOfRangeException(nameof(prefixPrecedence));
            if (postfixPrecedence < 0) throw new ArgumentOutOfRangeException(nameof(postfixPrecedence));

            Text = text;
            BinaryPrecedence = binaryPrecedence;
            Associativity = associativity;
            PrefixPrecedence = prefixPrecedence;
            PostfixPrecedence = postfixPrecedence;
            Role = role;
        }

        public string Text { get; }
        public int BinaryPrecedence { get; }
        public DdfOperatorAssociativity Associativity { get; }
        public int PrefixPrecedence { get; }
        public int PostfixPrecedence { get; }
        public bool IsPrefix => PrefixPrecedence > 0;
        public bool IsPostfix => PostfixPrecedence > 0;
        public DdfOperatorRole Role { get; }
    }

    public sealed class DdfLanguageDefinition
    {
        private readonly Dictionary<string, DdfKeywordDefinition> keywords;
        private readonly HashSet<string> booleanLiterals;
        private readonly Dictionary<string, DdfOperatorDefinition> operators;
        private readonly string[] operatorsByLength;
        private readonly HashSet<char> punctuation;
        private readonly IReadOnlyCollection<DdfKeywordDefinition> publicKeywords;
        private readonly IReadOnlyCollection<string> publicBooleanLiterals;
        private readonly IReadOnlyCollection<DdfOperatorDefinition> publicOperators;
        private readonly IReadOnlyCollection<char> publicPunctuation;

        public DdfLanguageDefinition(
            IEnumerable<DdfKeywordDefinition> keywords,
            IEnumerable<string> booleanLiterals,
            IEnumerable<DdfOperatorDefinition> operators,
            IEnumerable<char> punctuation)
        {
            if (keywords == null) throw new ArgumentNullException(nameof(keywords));
            if (booleanLiterals == null) throw new ArgumentNullException(nameof(booleanLiterals));
            if (operators == null) throw new ArgumentNullException(nameof(operators));
            if (punctuation == null) throw new ArgumentNullException(nameof(punctuation));

            this.keywords = ToUniqueDictionary(keywords, item => item.Text, "keyword");
            this.booleanLiterals = new HashSet<string>(booleanLiterals, StringComparer.Ordinal);
            this.operators = ToUniqueDictionary(operators, item => item.Text, "operator");
            operatorsByLength = this.operators.Keys
                .OrderByDescending(value => value.Length)
                .ThenBy(value => value, StringComparer.Ordinal)
                .ToArray();
            this.punctuation = new HashSet<char>(punctuation);
            publicKeywords = new List<DdfKeywordDefinition>(this.keywords.Values).AsReadOnly();
            publicBooleanLiterals = new List<string>(this.booleanLiterals).AsReadOnly();
            publicOperators = new List<DdfOperatorDefinition>(this.operators.Values).AsReadOnly();
            publicPunctuation = new List<char>(this.punctuation).AsReadOnly();
        }

        public IReadOnlyCollection<DdfKeywordDefinition> Keywords => publicKeywords;
        public IReadOnlyCollection<string> BooleanLiterals => publicBooleanLiterals;
        public IReadOnlyCollection<DdfOperatorDefinition> Operators => publicOperators;
        public IReadOnlyCollection<char> Punctuation => publicPunctuation;

        public bool TryGetKeyword(string text, out DdfKeywordDefinition definition)
        {
            return keywords.TryGetValue(text, out definition);
        }

        public bool TryGetOperator(string text, out DdfOperatorDefinition definition)
        {
            return operators.TryGetValue(text, out definition);
        }

        public bool IsBooleanLiteral(string text)
        {
            return booleanLiterals.Contains(text);
        }

        public bool IsPunctuation(char value)
        {
            return punctuation.Contains(value);
        }

        public string FindOperator(string source, int start)
        {
            foreach (string candidate in operatorsByLength)
            {
                if (start + candidate.Length <= source.Length &&
                    string.CompareOrdinal(source, start, candidate, 0, candidate.Length) == 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Dictionary<string, T> ToUniqueDictionary<T>(
            IEnumerable<T> values,
            Func<T, string> keySelector,
            string label)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T value in values)
            {
                if (value == null) throw new ArgumentException("A " + label + " definition cannot be null.");
                string key = keySelector(value);
                if (result.ContainsKey(key)) throw new ArgumentException("Duplicate " + label + ": " + key + ".");
                result.Add(key, value);
            }

            return result;
        }
    }
}
