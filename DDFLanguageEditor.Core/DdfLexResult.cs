using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfLexResult
    {
        public DdfLexResult(
            IReadOnlyList<DdfToken> tokens,
            IReadOnlyList<DdfDiagnostic> diagnostics)
        {
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public IReadOnlyList<DdfToken> Tokens { get; }

        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
    }

    public sealed class DdfLexUpdate
    {
        public DdfLexUpdate(DdfLexResult result, int relexStart)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            RelexStart = relexStart;
        }

        public DdfLexResult Result { get; }

        public int RelexStart { get; }
    }
}
