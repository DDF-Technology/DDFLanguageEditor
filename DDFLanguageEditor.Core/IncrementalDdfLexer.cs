using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class IncrementalDdfLexer
    {
        private readonly DdfLanguageDefinition language;
        private string previousText = string.Empty;
        private DdfLexResult previousResult = new DdfLexResult(
            new List<DdfToken>(),
            new List<DdfDiagnostic>());

        public IncrementalDdfLexer()
            : this(DdfLanguageCatalog.Default)
        {
        }

        public IncrementalDdfLexer(DdfLanguageDefinition language)
        {
            this.language = language ?? throw new ArgumentNullException(nameof(language));
        }

        public DdfLexUpdate Update(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (string.Equals(text, previousText, StringComparison.Ordinal))
            {
                return new DdfLexUpdate(previousResult, text.Length);
            }

            int changeStart = FindCommonPrefixLength(previousText, text);
            int relexStart = FindSafeRelexStart(changeStart);
            DdfLexResult suffix = DdfLexer.Lex(text, relexStart, language);
            var tokens = new List<DdfToken>();
            var diagnostics = new List<DdfDiagnostic>();

            foreach (DdfToken token in previousResult.Tokens)
            {
                if (token.End <= relexStart)
                {
                    tokens.Add(token);
                }
            }

            foreach (DdfDiagnostic diagnostic in previousResult.Diagnostics)
            {
                if (diagnostic.End <= relexStart)
                {
                    diagnostics.Add(diagnostic);
                }
            }

            tokens.AddRange(suffix.Tokens);
            diagnostics.AddRange(suffix.Diagnostics);
            previousText = text;
            previousResult = new DdfLexResult(tokens, diagnostics);
            return new DdfLexUpdate(previousResult, relexStart);
        }

        public void Reset()
        {
            previousText = string.Empty;
            previousResult = new DdfLexResult(new List<DdfToken>(), new List<DdfDiagnostic>());
        }

        private int FindSafeRelexStart(int changeStart)
        {
            int safeStart = 0;
            int precedingStart = 0;
            foreach (DdfToken token in previousResult.Tokens)
            {
                if (token.Start >= changeStart)
                {
                    break;
                }

                precedingStart = safeStart;
                safeStart = token.Start;
                if (token.End >= changeStart)
                {
                    break;
                }
            }

            // A newly inserted prefix can combine with more than one old token.
            // For example, inserting @@'Math' before an existing @@ makes the
            // textual common prefix end after the old @@ even though the new
            // library token starts at its first @. Retaining one extra token of
            // context prevents stale tokens at that boundary.
            return precedingStart;
        }

        private static int FindCommonPrefixLength(string first, string second)
        {
            int length = Math.Min(first.Length, second.Length);
            int index = 0;
            while (index < length && first[index] == second[index])
            {
                index++;
            }

            return index;
        }
    }
}
