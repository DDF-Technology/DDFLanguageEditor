using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DDFLanguageEditor.Compiler
{
    internal static class DdfDotNetDiagnosticMapper
    {
        internal static DdfDotNetContractIssue Create(string code,
            DdfDotNetCompilationWorkItem workItem, Diagnostic diagnostic,
            string prefix = null)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            if (diagnostic == null) throw new ArgumentNullException(nameof(diagnostic));
            string detail = (prefix ?? string.Empty) + diagnostic.Id + " " +
                diagnostic.GetMessage(CultureInfo.InvariantCulture);
            if (diagnostic.Location == Location.None ||
                !diagnostic.Location.IsInSource)
                return new DdfDotNetContractIssue(code,
                    "Errore interno del backend generato: " + detail);
            FileLinePositionSpan mapped =
                diagnostic.Location.GetMappedLineSpan();
            string path = (mapped.Path ?? string.Empty).Replace('\\', '/');
            bool known = mapped.IsValid && workItem.Unit.Sources.Any(source =>
                string.Equals(source.RelativePath, path,
                    StringComparison.Ordinal));
            if (!known)
                return new DdfDotNetContractIssue(code,
                    "Errore interno del backend generato: " + detail);
            int line = mapped.StartLinePosition.Line + 1;
            int column = mapped.StartLinePosition.Character + 1;
            return new DdfDotNetContractIssue(code,
                path + "(" + line.ToString(CultureInfo.InvariantCulture) +
                "," + column.ToString(CultureInfo.InvariantCulture) +
                "): " + detail, path, line, column);
        }
    }
}
