using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfGrammarFieldDefinition
    {
        public DdfGrammarFieldDefinition(string name, string sourceRule, bool multiple = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Field name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(sourceRule)) throw new ArgumentException("Field source rule is required.", nameof(sourceRule));
            Name = name; SourceRule = sourceRule; Multiple = multiple;
        }
        public string Name { get; }
        public string SourceRule { get; }
        public bool Multiple { get; }
    }

    public sealed class DdfGrammarNodeDefinition
    {
        public DdfGrammarNodeDefinition(string rule, string kind, IEnumerable<DdfGrammarFieldDefinition> fields)
        {
            if (string.IsNullOrWhiteSpace(rule)) throw new ArgumentException("Node rule is required.", nameof(rule));
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Node kind is required.", nameof(kind));
            Rule = rule; Kind = kind;
            Fields = (fields ?? Array.Empty<DdfGrammarFieldDefinition>()).ToList().AsReadOnly();
        }
        public string Rule { get; }
        public string Kind { get; }
        public IReadOnlyList<DdfGrammarFieldDefinition> Fields { get; }
    }

    public sealed class DdfGenericSyntaxNode : DdfSyntaxNode
    {
        internal DdfGenericSyntaxNode(string ruleName, string kind, int start, int length,
            IEnumerable<DdfGenericSyntaxNode> children, bool isMissing,
            IReadOnlyDictionary<string, IReadOnlyList<DdfGenericSyntaxNode>> fields)
            : base(start, length)
        {
            RuleName = ruleName ?? string.Empty;
            Kind = kind ?? ruleName ?? string.Empty;
            Children = (children ?? Array.Empty<DdfGenericSyntaxNode>()).ToList().AsReadOnly();
            IsMissing = isMissing;
            Fields = fields ?? new Dictionary<string, IReadOnlyList<DdfGenericSyntaxNode>>();
        }
        public string RuleName { get; }
        public string Kind { get; }
        public IReadOnlyList<DdfGenericSyntaxNode> Children { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<DdfGenericSyntaxNode>> Fields { get; }
        public bool IsMissing { get; }
    }

    public sealed class DdfGenericParseResult
    {
        internal DdfGenericParseResult(DdfGenericSyntaxNode root, IEnumerable<DdfDiagnostic> diagnostics)
        { Root = root; Diagnostics = diagnostics.ToList().AsReadOnly(); }
        public DdfGenericSyntaxNode Root { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public bool Success => Root != null && Diagnostics.Count == 0;
    }

    public sealed class DdfCompiledGrammar
    {
        private readonly IReadOnlyDictionary<string, GrammarExpression> rules;
        private readonly IReadOnlyDictionary<string, DdfGrammarNodeDefinition> nodes;

        internal DdfCompiledGrammar(string notation, string startRule,
            IDictionary<string, GrammarExpression> rules,
            IEnumerable<DdfGrammarNodeDefinition> nodes)
        {
            Notation = notation; StartRule = startRule;
            this.rules = new Dictionary<string, GrammarExpression>(rules, StringComparer.Ordinal);
            this.nodes = (nodes ?? Array.Empty<DdfGrammarNodeDefinition>())
                .ToDictionary(item => item.Rule, item => item, StringComparer.Ordinal);
        }

        public string Notation { get; }
        public string StartRule { get; }
        public IReadOnlyCollection<string> RuleNames => rules.Keys.ToList().AsReadOnly();
        public IReadOnlyCollection<DdfGrammarNodeDefinition> NodeDefinitions => nodes.Values.ToList().AsReadOnly();

        public DdfGenericParseResult Parse(string source, DdfLexResult lexResult)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (lexResult == null) throw new ArgumentNullException(nameof(lexResult));
            return new GenericParser(source, lexResult, rules, nodes).Parse(StartRule);
        }

        private sealed class GenericParser
        {
            private readonly string source;
            private readonly List<DdfToken> tokens;
            private readonly IReadOnlyDictionary<string, GrammarExpression> rules;
            private readonly IReadOnlyDictionary<string, DdfGrammarNodeDefinition> nodes;
            private readonly Dictionary<string, Match> memo = new Dictionary<string, Match>(StringComparer.Ordinal);
            private readonly HashSet<string> active = new HashSet<string>(StringComparer.Ordinal);
            private int farthest;
            private string expected = "regola valida";

            public GenericParser(string source, DdfLexResult lexResult,
                IReadOnlyDictionary<string, GrammarExpression> rules,
                IReadOnlyDictionary<string, DdfGrammarNodeDefinition> nodes)
            {
                this.source = source; this.rules = rules; this.nodes = nodes;
                tokens = lexResult.Tokens.Where(item => item.Kind != DdfTokenKind.LineComment &&
                    item.Kind != DdfTokenKind.BlockComment).ToList();
            }

            public DdfGenericParseResult Parse(string startRule)
            {
                Match match = MatchRule(startRule, 0);
                if (match.Success && match.Position == tokens.Count)
                    return new DdfGenericParseResult(match.Nodes.Single(), Array.Empty<DdfDiagnostic>());

                int tokenPosition = match.Success ? match.Position : farthest;
                int sourcePosition = tokenPosition < tokens.Count ? tokens[tokenPosition].Start : source.Length;
                int diagnosticStart = source.Length == 0 ? 0 : Math.Min(sourcePosition, source.Length - 1);
                GenericSyntaxUtilities.GetLineAndColumn(source, sourcePosition, out int line, out int column);
                string message = match.Success ? "Token inatteso dopo la regola radice." : "Atteso " + expected + ".";
                var diagnostic = new DdfDiagnostic("DDFG101", message, diagnosticStart,
                    tokenPosition < tokens.Count ? tokens[tokenPosition].Length : 1, line, column,
                    DdfDiagnosticSeverity.Error, sourcePosition);
                var missing = new DdfGenericSyntaxNode(string.Empty, "MissingSyntax", sourcePosition, 0,
                    Array.Empty<DdfGenericSyntaxNode>(), true, null);
                var partialChildren = match.Success ? match.Nodes.Single().Children.Concat(new[] { missing }) : new[] { missing };
                var root = CreateNode(startRule, 0, tokenPosition, partialChildren);
                return new DdfGenericParseResult(root, new[] { diagnostic });
            }

            private Match MatchRule(string name, int position)
            {
                string key = name + "@" + position;
                if (memo.TryGetValue(key, out Match cached)) return cached;
                if (!active.Add(key)) return Match.Failed(position);
                Match inner = MatchExpression(rules[name], position);
                active.Remove(key);
                Match result = inner.Success
                    ? Match.Succeeded(inner.Position, new[] { CreateNode(name, position, inner.Position, inner.Nodes) })
                    : Match.Failed(position);
                memo[key] = result;
                return result;
            }

            private Match MatchExpression(GrammarExpression expression, int position)
            {
                if (expression is EmptyExpression) return Match.Succeeded(position);
                if (expression is TerminalExpression terminal)
                {
                    if (position < tokens.Count && TokenText(tokens[position]) == terminal.Text)
                        return Match.Succeeded(position + 1, new[] { TokenNode(tokens[position]) });
                    RecordFailure(position, "'" + terminal.Text + "'"); return Match.Failed(position);
                }
                if (expression is TokenExpression token)
                {
                    if (position < tokens.Count && tokens[position].Kind == token.Kind)
                        return Match.Succeeded(position + 1, new[] { TokenNode(tokens[position]) });
                    RecordFailure(position, "<" + token.Kind + ">"); return Match.Failed(position);
                }
                if (expression is ReferenceExpression reference) return MatchRule(reference.Name, position);
                if (expression is SequenceExpression sequence)
                {
                    int current = position; var children = new List<DdfGenericSyntaxNode>();
                    foreach (GrammarExpression item in sequence.Items)
                    {
                        Match next = MatchExpression(item, current);
                        if (!next.Success) return Match.Failed(position);
                        current = next.Position; children.AddRange(next.Nodes);
                    }
                    return Match.Succeeded(current, children);
                }
                if (expression is ChoiceExpression choice)
                {
                    Match best = Match.Failed(position);
                    foreach (GrammarExpression item in choice.Alternatives)
                    {
                        Match candidate = MatchExpression(item, position);
                        if (candidate.Success && (!best.Success || candidate.Position > best.Position)) best = candidate;
                    }
                    return best;
                }
                if (expression is OptionalExpression optional)
                {
                    Match candidate = MatchExpression(optional.Inner, position);
                    return candidate.Success ? candidate : Match.Succeeded(position);
                }
                if (expression is RepeatExpression repeat)
                {
                    int current = position; var children = new List<DdfGenericSyntaxNode>();
                    while (true)
                    {
                        Match candidate = MatchExpression(repeat.Inner, current);
                        if (!candidate.Success || candidate.Position == current) break;
                        current = candidate.Position; children.AddRange(candidate.Nodes);
                    }
                    return Match.Succeeded(current, children);
                }
                throw new InvalidOperationException("Unknown grammar expression.");
            }

            private DdfGenericSyntaxNode CreateNode(string rule, int startPosition, int endPosition,
                IEnumerable<DdfGenericSyntaxNode> children)
            {
                List<DdfGenericSyntaxNode> childList = children.ToList();
                int start = startPosition < tokens.Count ? tokens[startPosition].Start : source.Length;
                int end = endPosition > startPosition && endPosition <= tokens.Count ? tokens[endPosition - 1].End : start;
                nodes.TryGetValue(rule, out DdfGrammarNodeDefinition definition);
                var fields = new Dictionary<string, IReadOnlyList<DdfGenericSyntaxNode>>(StringComparer.Ordinal);
                if (definition != null)
                {
                    foreach (DdfGrammarFieldDefinition field in definition.Fields)
                    {
                        List<DdfGenericSyntaxNode> matches = Descendants(childList)
                            .Where(item => item.RuleName == field.SourceRule).ToList();
                        if (!field.Multiple && matches.Count > 1) matches = matches.Take(1).ToList();
                        fields[field.Name] = matches.AsReadOnly();
                    }
                }
                return new DdfGenericSyntaxNode(rule, definition?.Kind ?? rule, start, Math.Max(0, end - start),
                    childList, false, fields);
            }

            private static IEnumerable<DdfGenericSyntaxNode> Descendants(IEnumerable<DdfGenericSyntaxNode> values)
            {
                foreach (DdfGenericSyntaxNode value in values)
                { yield return value; foreach (DdfGenericSyntaxNode child in Descendants(value.Children)) yield return child; }
            }
            private DdfGenericSyntaxNode TokenNode(DdfToken token) => new DdfGenericSyntaxNode(string.Empty,
                token.Kind.ToString(), token.Start, token.Length, null, false, null);
            private string TokenText(DdfToken token) => source.Substring(token.Start, token.Length);
            private void RecordFailure(int position, string value) { if (position >= farthest) { farthest = position; expected = value; } }
        }

        private sealed class Match
        {
            private Match(bool success, int position, IEnumerable<DdfGenericSyntaxNode> nodes)
            { Success = success; Position = position; Nodes = nodes.ToList(); }
            public bool Success { get; }
            public int Position { get; }
            public List<DdfGenericSyntaxNode> Nodes { get; }
            public static Match Failed(int position) => new Match(false, position, Array.Empty<DdfGenericSyntaxNode>());
            public static Match Succeeded(int position, IEnumerable<DdfGenericSyntaxNode> nodes = null) =>
                new Match(true, position, nodes ?? Array.Empty<DdfGenericSyntaxNode>());
        }
    }

    internal abstract class GrammarExpression { }
    internal sealed class EmptyExpression : GrammarExpression { }
    internal sealed class TerminalExpression : GrammarExpression { public TerminalExpression(string text) { Text = text; } public string Text { get; } }
    internal sealed class TokenExpression : GrammarExpression { public TokenExpression(DdfTokenKind kind) { Kind = kind; } public DdfTokenKind Kind { get; } }
    internal sealed class ReferenceExpression : GrammarExpression { public ReferenceExpression(string name) { Name = name; } public string Name { get; } }
    internal sealed class SequenceExpression : GrammarExpression { public SequenceExpression(IEnumerable<GrammarExpression> items) { Items = items.ToList(); } public List<GrammarExpression> Items { get; } }
    internal sealed class ChoiceExpression : GrammarExpression { public ChoiceExpression(IEnumerable<GrammarExpression> alternatives) { Alternatives = alternatives.ToList(); } public List<GrammarExpression> Alternatives { get; } }
    internal sealed class OptionalExpression : GrammarExpression { public OptionalExpression(GrammarExpression inner) { Inner = inner; } public GrammarExpression Inner { get; } }
    internal sealed class RepeatExpression : GrammarExpression { public RepeatExpression(GrammarExpression inner) { Inner = inner; } public GrammarExpression Inner { get; } }

    internal static class DdfEbnfCompiler
    {
        public static DdfCompiledGrammar Compile(string source, string startRule,
            IEnumerable<DdfGrammarNodeDefinition> nodes)
        {
            var parser = new EbnfParser(source ?? throw new ArgumentNullException(nameof(source)));
            Dictionary<string, GrammarExpression> rules = parser.Parse();
            if (!rules.ContainsKey(startRule)) throw new ArgumentException("Start rule not found: " + startRule + ".");
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (GrammarExpression expression in rules.Values) CollectReferences(expression, references);
            string missing = references.FirstOrDefault(item => !rules.ContainsKey(item));
            if (missing != null) throw new ArgumentException("Unknown grammar rule: " + missing + ".");
            foreach (string rule in rules.Keys)
                if (StartsWithReference(rules[rule], rule, rules, new HashSet<string>(StringComparer.Ordinal)))
                    throw new ArgumentException("Unsupported left recursion in rule: " + rule + ".");
            foreach (DdfGrammarNodeDefinition node in nodes ?? Array.Empty<DdfGrammarNodeDefinition>())
            {
                if (!rules.ContainsKey(node.Rule)) throw new ArgumentException("Node metadata references unknown rule: " + node.Rule + ".");
                foreach (DdfGrammarFieldDefinition field in node.Fields)
                    if (!rules.ContainsKey(field.SourceRule)) throw new ArgumentException("Field references unknown rule: " + field.SourceRule + ".");
            }
            return new DdfCompiledGrammar("ebnf-1.0", startRule, rules, nodes);
        }

        private static void CollectReferences(GrammarExpression expression, ISet<string> result)
        {
            if (expression is ReferenceExpression reference) result.Add(reference.Name);
            else if (expression is SequenceExpression sequence) foreach (GrammarExpression item in sequence.Items) CollectReferences(item, result);
            else if (expression is ChoiceExpression choice) foreach (GrammarExpression item in choice.Alternatives) CollectReferences(item, result);
            else if (expression is OptionalExpression optional) CollectReferences(optional.Inner, result);
            else if (expression is RepeatExpression repeat) CollectReferences(repeat.Inner, result);
        }

        private static bool StartsWithReference(GrammarExpression expression, string target,
            IReadOnlyDictionary<string, GrammarExpression> rules, ISet<string> visiting)
        {
            if (expression is ReferenceExpression reference)
            {
                if (reference.Name == target) return true;
                return visiting.Add(reference.Name) && StartsWithReference(rules[reference.Name], target, rules, visiting);
            }
            if (expression is SequenceExpression sequence && sequence.Items.Count > 0)
                return StartsWithReference(sequence.Items[0], target, rules, visiting);
            if (expression is ChoiceExpression choice)
                return choice.Alternatives.Any(item => StartsWithReference(item, target, rules, new HashSet<string>(visiting, StringComparer.Ordinal)));
            if (expression is OptionalExpression optional) return StartsWithReference(optional.Inner, target, rules, visiting);
            if (expression is RepeatExpression repeat) return StartsWithReference(repeat.Inner, target, rules, visiting);
            return false;
        }

        private sealed class EbnfParser
        {
            private readonly List<GrammarToken> tokens;
            private int position;
            public EbnfParser(string source) { tokens = Tokenize(source); }
            public Dictionary<string, GrammarExpression> Parse()
            {
                var result = new Dictionary<string, GrammarExpression>(StringComparer.Ordinal);
                while (Current.Kind != GrammarTokenKind.End)
                {
                    string name = Match(GrammarTokenKind.Identifier).Text;
                    Match(GrammarTokenKind.Equals);
                    GrammarExpression expression = ParseChoice(GrammarTokenKind.Semicolon);
                    Match(GrammarTokenKind.Semicolon);
                    if (!result.TryAdd(name, expression)) throw Error("Duplicate grammar rule: " + name + ".");
                }
                return result;
            }
            private GrammarExpression ParseChoice(params GrammarTokenKind[] stops)
            {
                var alternatives = new List<GrammarExpression> { ParseSequence(stops.Concat(new[] { GrammarTokenKind.Pipe }).ToArray()) };
                while (TryMatch(GrammarTokenKind.Pipe)) alternatives.Add(ParseSequence(stops.Concat(new[] { GrammarTokenKind.Pipe }).ToArray()));
                return alternatives.Count == 1 ? alternatives[0] : new ChoiceExpression(alternatives);
            }
            private GrammarExpression ParseSequence(params GrammarTokenKind[] stops)
            {
                var items = new List<GrammarExpression>();
                while (!stops.Contains(Current.Kind) && Current.Kind != GrammarTokenKind.End)
                {
                    if (TryMatch(GrammarTokenKind.Comma)) continue;
                    items.Add(ParsePrimary());
                }
                return items.Count == 0 ? new EmptyExpression() : items.Count == 1 ? items[0] : new SequenceExpression(items);
            }
            private GrammarExpression ParsePrimary()
            {
                if (Current.Kind == GrammarTokenKind.String) return new TerminalExpression(Next().Text);
                if (Current.Kind == GrammarTokenKind.TokenKind)
                {
                    string value = Next().Text;
                    if (!Enum.TryParse(value, true, out DdfTokenKind kind) || !Enum.IsDefined(kind)) throw Error("Unknown token kind: " + value + ".");
                    return new TokenExpression(kind);
                }
                if (Current.Kind == GrammarTokenKind.Identifier) return new ReferenceExpression(Next().Text);
                if (TryMatch(GrammarTokenKind.LParen)) { GrammarExpression value = ParseChoice(GrammarTokenKind.RParen); Match(GrammarTokenKind.RParen); return value; }
                if (TryMatch(GrammarTokenKind.LBracket)) { GrammarExpression value = ParseChoice(GrammarTokenKind.RBracket); Match(GrammarTokenKind.RBracket); return new OptionalExpression(value); }
                if (TryMatch(GrammarTokenKind.LBrace)) { GrammarExpression value = ParseChoice(GrammarTokenKind.RBrace); Match(GrammarTokenKind.RBrace); return new RepeatExpression(value); }
                throw Error("Expected terminal, token kind, rule or group.");
            }
            private GrammarToken Match(GrammarTokenKind kind) { if (Current.Kind != kind) throw Error("Expected " + kind + "."); return Next(); }
            private bool TryMatch(GrammarTokenKind kind) { if (Current.Kind != kind) return false; position++; return true; }
            private GrammarToken Next() => tokens[position++];
            private GrammarToken Current => tokens[position];
            private FormatException Error(string message) => new FormatException(message + " EBNF position " + Current.Position + ".");
            private static List<GrammarToken> Tokenize(string source)
            {
                var result = new List<GrammarToken>(); int index = 0;
                while (index < source.Length)
                {
                    if (char.IsWhiteSpace(source[index])) { index++; continue; }
                    if (source[index] == '#') { while (index < source.Length && source[index] != '\n') index++; continue; }
                    int start = index; char current = source[index++];
                    if (char.IsLetter(current) || current == '_')
                    {
                        while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_' || source[index] == '-')) index++;
                        result.Add(new GrammarToken(GrammarTokenKind.Identifier, source.Substring(start, index - start), start)); continue;
                    }
                    if (current == '"')
                    {
                        var value = new System.Text.StringBuilder(); bool closed = false;
                        while (index < source.Length)
                        {
                            char item = source[index++];
                            if (item == '"') { closed = true; break; }
                            if (item == '\\' && index < source.Length) item = source[index++];
                            value.Append(item);
                        }
                        if (!closed) throw new FormatException("Unterminated EBNF terminal at position " + start + ".");
                        result.Add(new GrammarToken(GrammarTokenKind.String, value.ToString(), start)); continue;
                    }
                    if (current == '<')
                    {
                        int end = source.IndexOf('>', index); if (end < 0) throw new FormatException("Unterminated token kind at position " + start + ".");
                        result.Add(new GrammarToken(GrammarTokenKind.TokenKind, source.Substring(index, end - index), start)); index = end + 1; continue;
                    }
                    string symbols = "=;|,()[]{}"; int symbolIndex = symbols.IndexOf(current);
                    if (symbolIndex < 0) throw new FormatException("Unexpected EBNF character at position " + start + ".");
                    result.Add(new GrammarToken((GrammarTokenKind)(symbolIndex + 3), current.ToString(), start));
                }
                result.Add(new GrammarToken(GrammarTokenKind.End, string.Empty, source.Length)); return result;
            }
        }
        private enum GrammarTokenKind { End, Identifier, String, Equals, Semicolon, Pipe, Comma, LParen, RParen, LBracket, RBracket, LBrace, RBrace, TokenKind }
        private sealed class GrammarToken { public GrammarToken(GrammarTokenKind kind, string text, int position) { Kind = kind; Text = text; Position = position; } public GrammarTokenKind Kind { get; } public string Text { get; } public int Position { get; } }
    }

    internal static class GenericSyntaxUtilities
    {
        public static void GetLineAndColumn(string source, int position, out int line, out int column)
        {
            line = 1; column = 1;
            for (int index = 0; index < Math.Min(position, source.Length); index++)
            { if (source[index] == '\n') { line++; column = 1; } else if (source[index] != '\r') column++; }
        }
    }
}
