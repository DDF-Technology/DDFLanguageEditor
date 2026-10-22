using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguageMigrationSeverity
    {
        Information,
        Warning,
        Error
    }

    public enum DdfLanguageMigrationIssueKind
    {
        IdentifierCollision,
        MissingSemanticRole,
        AmbiguousSemanticRole,
        SemanticRoleChanged
    }

    public sealed class DdfLanguageMigrationDocument
    {
        public DdfLanguageMigrationDocument(string documentPath, string text)
        {
            DocumentPath = documentPath ?? string.Empty;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string DocumentPath { get; }
        public string Text { get; }
    }

    public sealed class DdfLanguageMigrationDiagnostic
    {
        internal DdfLanguageMigrationDiagnostic(string code,
            DdfLanguageMigrationIssueKind kind,
            DdfLanguageMigrationSeverity severity, string message,
            string documentPath = null, int start = -1, int length = 0,
            int line = 0, int column = 0, string originalText = null,
            string suggestedText = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Kind = kind;
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            DocumentPath = documentPath ?? string.Empty;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            OriginalText = originalText ?? string.Empty;
            SuggestedText = suggestedText ?? string.Empty;
        }

        public string Code { get; }
        public DdfLanguageMigrationIssueKind Kind { get; }
        public DdfLanguageMigrationSeverity Severity { get; }
        public string Message { get; }
        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public string OriginalText { get; }
        public string SuggestedText { get; }
        public bool HasSourceLocation => Start >= 0;

        public override string ToString() => Code + ": " + Message +
            (HasSourceLocation ? " [" + DocumentPath + ":" + Line + ":" +
                Column + "]" : string.Empty);
    }

    public sealed class DdfLanguageMigrationPreview
    {
        internal DdfLanguageMigrationPreview(string documentPath,
            string originalText, DdfSyntaxProfileConversionResult conversion)
        {
            DocumentPath = documentPath ?? string.Empty;
            OriginalText = originalText ?? string.Empty;
            ConvertedText = conversion?.Text ?? OriginalText;
            ReplacementCount = conversion?.ReplacementCount ?? 0;
        }

        public string DocumentPath { get; }
        public string OriginalText { get; }
        public string ConvertedText { get; }
        public int ReplacementCount { get; }
        public bool Changed => ReplacementCount > 0;
    }

    public sealed class DdfLanguageMigrationPlan
    {
        internal DdfLanguageMigrationPlan(DdfLanguagePackage sourcePackage,
            DdfLanguagePackage targetPackage,
            IEnumerable<DdfLanguageMigrationPreview> previews,
            IEnumerable<DdfLanguageMigrationDiagnostic> diagnostics)
        {
            SourcePackageId = sourcePackage.Id;
            SourcePackageVersion = sourcePackage.PackageVersion;
            TargetPackageId = targetPackage.Id;
            TargetPackageVersion = targetPackage.PackageVersion;
            Previews = (previews ?? Array.Empty<DdfLanguageMigrationPreview>())
                .ToList().AsReadOnly();
            Diagnostics = (diagnostics ??
                    Array.Empty<DdfLanguageMigrationDiagnostic>())
                .ToList().AsReadOnly();
        }

        public string SourcePackageId { get; }
        public string SourcePackageVersion { get; }
        public string TargetPackageId { get; }
        public string TargetPackageVersion { get; }
        public IReadOnlyList<DdfLanguageMigrationPreview> Previews { get; }
        public IReadOnlyList<DdfLanguageMigrationDiagnostic> Diagnostics { get; }
        public bool CanActivate => Diagnostics.All(item =>
            item.Severity != DdfLanguageMigrationSeverity.Error);
        public bool RequiresReview => Diagnostics.Count > 0;
        public bool HasAutomaticConversions => Previews.Any(item => item.Changed);
    }

    /// <summary>
    /// Builds a read-only migration preview before a language package switch.
    /// Semantic-role translations are safe candidates; identifier collisions
    /// are blocking and are never rewritten automatically.
    /// </summary>
    public static class DdfLanguageMigrationAnalyzer
    {
        public static DdfLanguageMigrationPlan Analyze(
            IEnumerable<DdfLanguageMigrationDocument> documents,
            DdfLanguagePackage sourcePackage,
            DdfLanguagePackage targetPackage)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (sourcePackage == null)
                throw new ArgumentNullException(nameof(sourcePackage));
            if (targetPackage == null)
                throw new ArgumentNullException(nameof(targetPackage));
            DdfLanguageMigrationDocument[] sourceDocuments = documents.ToArray();
            if (sourceDocuments.Any(item => item == null))
                throw new ArgumentException(
                    "I documenti di migrazione non possono contenere valori nulli.",
                    nameof(documents));

            var diagnostics = new List<DdfLanguageMigrationDiagnostic>();
            ValidateSemanticRoles(sourcePackage.Language,
                targetPackage.Language, diagnostics);
            var previews = new List<DdfLanguageMigrationPreview>();
            foreach (DdfLanguageMigrationDocument document in sourceDocuments)
            {
                DdfSyntaxProfileConversionResult conversion =
                    DdfSyntaxProfileConverter.Convert(document.Text,
                        sourcePackage.Language, targetPackage.Language);
                previews.Add(new DdfLanguageMigrationPreview(
                    document.DocumentPath, document.Text, conversion));
                DetectIdentifierCollisions(document, sourcePackage.Language,
                    targetPackage.Language, diagnostics);
            }
            return new DdfLanguageMigrationPlan(sourcePackage, targetPackage,
                previews, diagnostics);
        }

        private static void ValidateSemanticRoles(DdfLanguageDefinition source,
            DdfLanguageDefinition target,
            ICollection<DdfLanguageMigrationDiagnostic> diagnostics)
        {
            foreach (DdfKeywordDefinition keyword in source.Keywords.Where(item =>
                         !string.Equals(item.RoleId, DdfSyntaxRoles.None,
                             StringComparison.OrdinalIgnoreCase)))
            {
                DdfKeywordDefinition[] candidates = target.Keywords.Where(item =>
                        string.Equals(item.RoleId, keyword.RoleId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (candidates.Length == 0)
                    diagnostics.Add(new DdfLanguageMigrationDiagnostic(
                        "DDFM002", DdfLanguageMigrationIssueKind
                            .MissingSemanticRole,
                        DdfLanguageMigrationSeverity.Error,
                        "Il pacchetto destinazione non espone il ruolo semantico '" +
                        keyword.RoleId + "' richiesto da '" + keyword.Text + "'.",
                        originalText: keyword.Text));
                else if (candidates.Length > 1)
                    diagnostics.Add(new DdfLanguageMigrationDiagnostic(
                        "DDFM003", DdfLanguageMigrationIssueKind
                            .AmbiguousSemanticRole,
                        DdfLanguageMigrationSeverity.Error,
                        "Il ruolo semantico '" + keyword.RoleId +
                        "' ha più grafie di destinazione e non è migrabile in modo univoco.",
                        originalText: keyword.Text));

                if (target.TryGetKeyword(keyword.Text,
                        out DdfKeywordDefinition sameText) &&
                    !string.Equals(sameText.RoleId, keyword.RoleId,
                        StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new DdfLanguageMigrationDiagnostic(
                        "DDFM004", DdfLanguageMigrationIssueKind
                            .SemanticRoleChanged,
                        DdfLanguageMigrationSeverity.Error,
                        "La grafia '" + keyword.Text + "' cambia ruolo da '" +
                        keyword.RoleId + "' a '" + sameText.RoleId +
                        "': i profili sintattici non possono cambiare semantica.",
                        originalText: keyword.Text));
            }
        }

        private static void DetectIdentifierCollisions(
            DdfLanguageMigrationDocument document, DdfLanguageDefinition source,
            DdfLanguageDefinition target,
            ICollection<DdfLanguageMigrationDiagnostic> diagnostics)
        {
            foreach (DdfToken token in DdfLexer.Lex(document.Text, source).Tokens)
            {
                if (token.Kind != DdfTokenKind.Identifier) continue;
                string text = document.Text.Substring(token.Start, token.Length);
                if (!target.TryGetKeyword(text, out _) &&
                    !target.IsBooleanLiteral(text)) continue;
                (int line, int column) = LineAndColumn(document.Text, token.Start);
                string suggestion = SuggestIdentifier(text, target);
                diagnostics.Add(new DdfLanguageMigrationDiagnostic(
                    "DDFM001",
                    DdfLanguageMigrationIssueKind.IdentifierCollision,
                    DdfLanguageMigrationSeverity.Error,
                    "L'identificatore '" + text +
                    "' diventa una parola riservata nel pacchetto destinazione. " +
                    "Rinominarlo esplicitamente prima dell'attivazione" +
                    (suggestion.Length == 0 ? "." : ", ad esempio in '" +
                        suggestion + "'."),
                    document.DocumentPath, token.Start, token.Length, line,
                    column, text, suggestion));
            }
        }

        private static string SuggestIdentifier(string value,
            DdfLanguageDefinition target)
        {
            string basis = value + "_value";
            for (int suffix = 0; suffix < 100; suffix++)
            {
                string candidate = suffix == 0 ? basis : basis + suffix;
                if (target.LexicalRules.IsValidIdentifier(candidate) &&
                    !target.TryGetKeyword(candidate, out _) &&
                    !target.IsBooleanLiteral(candidate)) return candidate;
            }
            return string.Empty;
        }

        private static (int Line, int Column) LineAndColumn(string text,
            int position)
        {
            int line = 1;
            int column = 1;
            for (int index = 0; index < position; index++)
            {
                if (text[index] == '\r')
                {
                    if (index + 1 < position && text[index + 1] == '\n')
                        index++;
                    line++;
                    column = 1;
                }
                else if (text[index] == '\n')
                {
                    line++;
                    column = 1;
                }
                else column++;
            }
            return (line, column);
        }
    }
}
