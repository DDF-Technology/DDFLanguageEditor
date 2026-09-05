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

    public static class DdfSyntaxRoles
    {
        public const string None = "None";
        public const string Structure = "Structure";
        public const string ReturnTypeMarker = "ReturnTypeMarker";
        public const string If = "If";
        public const string Else = "Else";
        public const string While = "While";
        public const string Do = "Do";
        public const string For = "For";
        public const string Return = "Return";
        public const string Break = "Break";
        public const string End = "End";
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
            : this(text, tokenKind, role.ToString())
        {
        }

        public DdfKeywordDefinition(string text, DdfTokenKind tokenKind, string roleId)
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
            RoleId = string.IsNullOrWhiteSpace(roleId) ? DdfSyntaxRoles.None : roleId;
        }

        public string Text { get; }
        public DdfTokenKind TokenKind { get; }
        public string RoleId { get; }
        public DdfKeywordRole Role => Enum.TryParse(RoleId, true, out DdfKeywordRole role) && Enum.IsDefined(role)
            ? role
            : DdfKeywordRole.None;
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
            IEnumerable<char> punctuation,
            DdfLexicalDefinition lexicalRules = null)
        {
            if (keywords == null) throw new ArgumentNullException(nameof(keywords));
            if (booleanLiterals == null) throw new ArgumentNullException(nameof(booleanLiterals));
            if (operators == null) throw new ArgumentNullException(nameof(operators));
            if (punctuation == null) throw new ArgumentNullException(nameof(punctuation));

            var declaredOperators = operators.ToList();
            LexicalRules = lexicalRules ?? DdfLexicalDefinition.CreateDdfCompatibleDefault();
            this.keywords = ToUniqueDictionary(keywords, item => item.Text, "keyword");
            this.booleanLiterals = new HashSet<string>(booleanLiterals, StringComparer.Ordinal);
            this.operators = ToUniqueDictionary(declaredOperators, item => item.Text, "operator");
            operatorsByLength = (LexicalRules.LongestMatch
                ? declaredOperators.Select(item => item.Text).OrderByDescending(value => value.Length)
                    .ThenBy(value => value, StringComparer.Ordinal)
                : declaredOperators.Select(item => item.Text)).ToArray();
            this.punctuation = new HashSet<char>(punctuation);
            publicKeywords = new List<DdfKeywordDefinition>(this.keywords.Values).AsReadOnly();
            publicBooleanLiterals = new List<string>(this.booleanLiterals).AsReadOnly();
            publicOperators = new List<DdfOperatorDefinition>(this.operators.Values).AsReadOnly();
            publicPunctuation = new List<char>(this.punctuation).AsReadOnly();
            Intellisense = DdfIntellisenseDefinition.CreateLegacyDefault();
            Formatting = DdfFormattingDefinition.CreateLegacyDefault();
            Types = DdfTypeSystemDefinition.CreateDdfCompatibleDefault();
            Semantics = DdfSemanticDefinition.CreateEmpty();
        }

        public IReadOnlyCollection<DdfKeywordDefinition> Keywords => publicKeywords;
        public IReadOnlyCollection<string> BooleanLiterals => publicBooleanLiterals;
        public IReadOnlyCollection<DdfOperatorDefinition> Operators => publicOperators;
        public IReadOnlyCollection<char> Punctuation => publicPunctuation;
        public DdfLexicalDefinition LexicalRules { get; }
        public DdfCompiledGrammar Grammar { get; internal set; }
        public DdfIntellisenseDefinition Intellisense { get; internal set; }
        public DdfFormattingDefinition Formatting { get; internal set; }
        public DdfTypeSystemDefinition Types { get; internal set; }
        public DdfSemanticDefinition Semantics { get; internal set; }

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
