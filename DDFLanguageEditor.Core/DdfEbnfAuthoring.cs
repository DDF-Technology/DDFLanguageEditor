using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfEbnfAuthoringDiagnosticSeverity { Information, Warning, Error }

    public sealed class DdfEbnfAuthoringDiagnostic
    {
        internal DdfEbnfAuthoringDiagnostic(string code, string message,
            DdfEbnfAuthoringDiagnosticSeverity severity, int start, int length,
            int line, int column)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
            Line = Math.Max(1, line);
            Column = Math.Max(1, column);
        }

        public string Code { get; }
        public string Message { get; }
        public DdfEbnfAuthoringDiagnosticSeverity Severity { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public override string ToString() => Code + " — riga " + Line +
            ", colonna " + Column + ": " + Message;
    }

    public sealed class DdfEbnfRuleReference
    {
        internal DdfEbnfRuleReference(string sourceRule, string targetRule,
            int start, int length, int line, int column)
        {
            SourceRule = sourceRule ?? string.Empty;
            TargetRule = targetRule ?? string.Empty;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
        }

        public string SourceRule { get; }
        public string TargetRule { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
    }

    public sealed class DdfEbnfRuleAnalysis
    {
        internal DdfEbnfRuleAnalysis(string name, int start, int length,
            int line, int column, IEnumerable<DdfEbnfRuleReference> references,
            IEnumerable<string> referencedBy)
        {
            Name = name;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            References = (references ?? Array.Empty<DdfEbnfRuleReference>())
                .ToList().AsReadOnly();
            ReferencedBy = (referencedBy ?? Array.Empty<string>()).Distinct(
                StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public string Name { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public IReadOnlyList<DdfEbnfRuleReference> References { get; }
        public IReadOnlyList<string> ReferencedBy { get; }
    }

    public sealed class DdfEbnfAuthoringAnalysis
    {
        internal DdfEbnfAuthoringAnalysis(string startRule,
            IEnumerable<DdfEbnfRuleAnalysis> rules,
            IEnumerable<DdfEbnfRuleReference> references,
            IEnumerable<DdfEbnfAuthoringDiagnostic> diagnostics)
        {
            StartRule = startRule ?? string.Empty;
            Rules = (rules ?? Array.Empty<DdfEbnfRuleAnalysis>()).ToList()
                .AsReadOnly();
            References = (references ?? Array.Empty<DdfEbnfRuleReference>())
                .ToList().AsReadOnly();
            Diagnostics = (diagnostics ??
                Array.Empty<DdfEbnfAuthoringDiagnostic>()).ToList().AsReadOnly();
        }

        public string StartRule { get; }
        public IReadOnlyList<DdfEbnfRuleAnalysis> Rules { get; }
        public IReadOnlyList<DdfEbnfRuleReference> References { get; }
        public IReadOnlyList<DdfEbnfAuthoringDiagnostic> Diagnostics { get; }
        public bool Success => !Diagnostics.Any(item => item.Severity ==
            DdfEbnfAuthoringDiagnosticSeverity.Error);
    }

    public static class DdfEbnfAuthoringAnalyzer
    {
        private static readonly Regex PositionPattern = new Regex(
            @"position\s+(?<position>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static DdfEbnfAuthoringAnalysis Analyze(string source,
            string startRule)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            startRule = (startRule ?? string.Empty).Trim();
            var declarations = new List<Token>();
            var references = new List<DdfEbnfRuleReference>();
            Scan(source, declarations, references);
            var diagnostics = new List<DdfEbnfAuthoringDiagnostic>();

            if (startRule.Length == 0)
                diagnostics.Add(Diagnostic(source, "DDFEBNF001",
                    "La regola iniziale non e specificata.", 0, 0));
            else
            {
                try
                {
                    DdfEbnfCompiler.Compile(source, startRule,
                        Array.Empty<DdfGrammarNodeDefinition>());
                }
                catch (Exception exception) when (exception is FormatException ||
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    int position = ResolveErrorPosition(exception.Message,
                        declarations, references);
                    diagnostics.Add(Diagnostic(source, "DDFEBNF002",
                        exception.Message, position,
                        position < source.Length ? 1 : 0));
                }
            }

            var declarationNames = new HashSet<string>(declarations.Select(item =>
                item.Text), StringComparer.Ordinal);
            if (!diagnostics.Any(item => item.Severity ==
                    DdfEbnfAuthoringDiagnosticSeverity.Error) &&
                declarationNames.Contains(startRule))
            {
                var outgoing = references.GroupBy(item => item.SourceRule,
                    StringComparer.Ordinal).ToDictionary(group => group.Key,
                    group => group.Select(item => item.TargetRule).Distinct(
                        StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
                var reachable = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<string>();
                pending.Push(startRule);
                while (pending.Count != 0)
                {
                    string current = pending.Pop();
                    if (!reachable.Add(current) || !outgoing.TryGetValue(current,
                            out List<string> targets)) continue;
                    foreach (string target in targets)
                        if (declarationNames.Contains(target)) pending.Push(target);
                }
                foreach (Token declaration in declarations.Where(item =>
                    !reachable.Contains(item.Text)))
                    diagnostics.Add(Diagnostic(source, "DDFEBNF003",
                        "Regola non raggiungibile dalla regola iniziale '" +
                        startRule + "': " + declaration.Text + ".",
                        declaration.Start, declaration.Length,
                        DdfEbnfAuthoringDiagnosticSeverity.Warning));
            }

            var incoming = references.GroupBy(item => item.TargetRule,
                StringComparer.Ordinal).ToDictionary(group => group.Key,
                group => group.Select(item => item.SourceRule),
                StringComparer.Ordinal);
            var bySource = references.GroupBy(item => item.SourceRule,
                StringComparer.Ordinal).ToDictionary(group => group.Key,
                group => group.AsEnumerable(), StringComparer.Ordinal);
            var rules = new List<DdfEbnfRuleAnalysis>();
            foreach (Token declaration in declarations)
            {
                GetLineAndColumn(source, declaration.Start, out int line,
                    out int column);
                rules.Add(new DdfEbnfRuleAnalysis(declaration.Text,
                    declaration.Start, declaration.Length, line, column,
                    bySource.TryGetValue(declaration.Text, out var outgoing)
                        ? outgoing : Array.Empty<DdfEbnfRuleReference>(),
                    incoming.TryGetValue(declaration.Text, out var callers)
                        ? callers : Array.Empty<string>()));
            }
            return new DdfEbnfAuthoringAnalysis(startRule, rules, references,
                diagnostics.OrderBy(item => item.Start).ThenBy(item => item.Code));
        }

        private static void Scan(string source, IList<Token> declarations,
            IList<DdfEbnfRuleReference> references)
        {
            string currentRule = string.Empty;
            int index = 0;
            while (index < source.Length)
            {
                char current = source[index];
                if (char.IsWhiteSpace(current)) { index++; continue; }
                if (current == '#')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    continue;
                }
                if (current == '"')
                {
                    index++;
                    while (index < source.Length)
                    {
                        char item = source[index++];
                        if (item == '\\' && index < source.Length) { index++; continue; }
                        if (item == '"') break;
                    }
                    continue;
                }
                if (current == '<')
                {
                    int end = source.IndexOf('>', index + 1);
                    index = end < 0 ? source.Length : end + 1;
                    continue;
                }
                if (!char.IsLetter(current) && current != '_')
                {
                    index++;
                    continue;
                }

                int start = index++;
                while (index < source.Length &&
                    (char.IsLetterOrDigit(source[index]) || source[index] == '_' ||
                     source[index] == '-')) index++;
                string name = source.Substring(start, index - start);
                int lookahead = SkipTrivia(source, index);
                if (lookahead < source.Length && source[lookahead] == '=')
                {
                    declarations.Add(new Token(name, start, index - start));
                    currentRule = name;
                }
                else if (currentRule.Length != 0)
                {
                    GetLineAndColumn(source, start, out int line, out int column);
                    references.Add(new DdfEbnfRuleReference(currentRule, name,
                        start, index - start, line, column));
                }
            }
        }

        private static int SkipTrivia(string source, int index)
        {
            while (index < source.Length)
            {
                if (char.IsWhiteSpace(source[index])) { index++; continue; }
                if (source[index] != '#') break;
                while (index < source.Length && source[index] != '\n') index++;
            }
            return index;
        }

        private static int ResolveErrorPosition(string message,
            IEnumerable<Token> declarations,
            IEnumerable<DdfEbnfRuleReference> references)
        {
            Match match = PositionPattern.Match(message ?? string.Empty);
            if (match.Success && int.TryParse(match.Groups["position"].Value,
                    out int position)) return Math.Max(0, position);
            foreach (Token declaration in declarations)
                if ((message ?? string.Empty).Contains(declaration.Text,
                        StringComparison.Ordinal)) return declaration.Start;
            foreach (DdfEbnfRuleReference reference in references)
                if ((message ?? string.Empty).Contains(reference.TargetRule,
                        StringComparison.Ordinal)) return reference.Start;
            return 0;
        }

        private static DdfEbnfAuthoringDiagnostic Diagnostic(string source,
            string code, string message, int start, int length,
            DdfEbnfAuthoringDiagnosticSeverity severity =
                DdfEbnfAuthoringDiagnosticSeverity.Error)
        {
            start = Math.Max(0, Math.Min(start, source.Length));
            GetLineAndColumn(source, start, out int line, out int column);
            return new DdfEbnfAuthoringDiagnostic(code, message, severity,
                start, length, line, column);
        }

        private static void GetLineAndColumn(string source, int position,
            out int line, out int column)
        {
            line = 1;
            column = 1;
            for (int index = 0; index < Math.Min(position, source.Length); index++)
            {
                if (source[index] == '\n') { line++; column = 1; }
                else if (source[index] != '\r') column++;
            }
        }

        private sealed class Token
        {
            public Token(string text, int start, int length)
            { Text = text; Start = start; Length = length; }
            public string Text { get; }
            public int Start { get; }
            public int Length { get; }
        }
    }
}
