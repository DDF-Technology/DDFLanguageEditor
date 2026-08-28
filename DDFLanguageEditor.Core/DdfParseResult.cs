using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfParseResult
    {
        public DdfParseResult(CompilationUnitSyntax root, IReadOnlyList<DdfDiagnostic> lexicalDiagnostics, IReadOnlyList<DdfDiagnostic> syntaxDiagnostics)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            LexicalDiagnostics = lexicalDiagnostics ?? throw new ArgumentNullException(nameof(lexicalDiagnostics));
            SyntaxDiagnostics = syntaxDiagnostics ?? throw new ArgumentNullException(nameof(syntaxDiagnostics));

            var all = new List<DdfDiagnostic>(lexicalDiagnostics.Count + syntaxDiagnostics.Count);
            all.AddRange(lexicalDiagnostics);
            all.AddRange(syntaxDiagnostics);
            all.Sort((left, right) => left.Start != right.Start
                ? left.Start.CompareTo(right.Start)
                : string.CompareOrdinal(left.Code, right.Code));
            Diagnostics = all.AsReadOnly();
        }

        public CompilationUnitSyntax Root { get; }
        public IReadOnlyList<DdfDiagnostic> LexicalDiagnostics { get; }
        public IReadOnlyList<DdfDiagnostic> SyntaxDiagnostics { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
    }
}
