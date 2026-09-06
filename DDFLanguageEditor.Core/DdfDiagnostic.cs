using System;
using System.Globalization;

namespace DDFLanguageEditor.Core
{
    public enum DdfDiagnosticDomain
    {
        Source,
        LanguageDefinition,
        EngineCapability,
        Runtime
    }

    public static class DdfDiagnosticDomainNames
    {
        public static string GetDisplayName(DdfDiagnosticDomain domain)
        {
            switch (domain)
            {
                case DdfDiagnosticDomain.Source: return "Codice sorgente";
                case DdfDiagnosticDomain.LanguageDefinition: return "Definizione linguaggio";
                case DdfDiagnosticDomain.EngineCapability: return "Funzionalità motore";
                case DdfDiagnosticDomain.Runtime: return "Runtime";
                default: return domain.ToString();
            }
        }
    }

    public enum DdfDiagnosticSeverity
    {
        Warning,
        Error
    }

    public sealed class DdfDiagnostic
    {
        public DdfDiagnostic(
            string code,
            string message,
            int start,
            int length,
            int line,
            int column,
            DdfDiagnosticSeverity severity = DdfDiagnosticSeverity.Error,
            int? insertionPosition = null,
            int? contextStart = null,
            DdfDiagnosticDomain domain = DdfDiagnosticDomain.Source,
            string documentPath = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A diagnostic message is required.", nameof(message));
            }

            if (start < 0 || length <= 0 || line <= 0 || column <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (insertionPosition < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(insertionPosition));
            }

            if (contextStart < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contextStart));
            }

            Code = code;
            Message = message;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            Severity = severity;
            InsertionPosition = insertionPosition;
            ContextStart = contextStart;
            Domain = domain;
            DocumentPath = documentPath ?? string.Empty;
        }

        public string Code { get; }

        public string Message { get; }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public int Line { get; }

        public int Column { get; }

        public DdfDiagnosticSeverity Severity { get; }

        public int? InsertionPosition { get; }

        public int? ContextStart { get; }

        public DdfDiagnosticDomain Domain { get; }

        public string DocumentPath { get; }

        public string ToHoverText()
        {
            string severity = Severity == DdfDiagnosticSeverity.Warning ? "Avviso" : "Errore";
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1} — riga {2}, colonna {3}\n[{4}] {5}",
                severity,
                Code,
                Line,
                Column,
                DdfDiagnosticDomainNames.GetDisplayName(Domain),
                Message);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "Riga {0}, colonna {1} — {2} [{3}]: {4}",
                Line,
                Column,
                Code,
                DdfDiagnosticDomainNames.GetDisplayName(Domain),
                Message);
        }
    }
}
