using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    /// <summary>Semantic function-name spans for the editor's syntax projection.</summary>
    public static class DdfFunctionSyntaxClassifier
    {
        public static IReadOnlyList<ClassifiedSpan> Classify(
            string source, DdfLexResult lexResult, DdfParseResult parseResult,
            DdfSemanticModel semanticModel, DdfLanguageDefinition language,
            IEnumerable<CompilationUnitSyntax> externalRoots = null,
            DdfHostCatalogSnapshot hostCatalog = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (lexResult == null) throw new ArgumentNullException(nameof(lexResult));
            if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
            if (semanticModel == null) throw new ArgumentNullException(nameof(semanticModel));
            if (language == null) throw new ArgumentNullException(nameof(language));

            var callableNames = new HashSet<string>(StringComparer.Ordinal);
            var spans = new List<ClassifiedSpan>();
            var seenStarts = new HashSet<int>();
            foreach (FunctionDeclarationSyntax function in parseResult.Root.Members
                .OfType<FunctionDeclarationSyntax>())
            {
                if (function.NameLength <= 0 || function.NameStart + function.NameLength > source.Length)
                    continue;
                callableNames.Add(function.Name);
                spans.Add(new ClassifiedSpan(function.NameStart, function.NameLength,
                    SyntaxKind.Function));
                seenStarts.Add(function.NameStart);
            }
            foreach (CompilationUnitSyntax root in externalRoots ??
                Array.Empty<CompilationUnitSyntax>())
            {
                foreach (FunctionDeclarationSyntax function in root.Members
                    .OfType<FunctionDeclarationSyntax>())
                    callableNames.Add(function.Name);
            }
            if (language.StandardLibrary != null)
                foreach (DdfStandardFunction function in language.StandardLibrary.Functions)
                    callableNames.Add(function.Name);
            if (hostCatalog != null)
                foreach (DdfHostFunctionDefinition function in hostCatalog.Functions)
                    callableNames.Add(function.Name);

            var occurrences = semanticModel.Occurrences
                .GroupBy(item => item.Start)
                .ToDictionary(group => group.Key, group => group.First());
            IReadOnlyList<DdfToken> tokens = lexResult.Tokens;
            for (int index = 0; index < tokens.Count; index++)
            {
                DdfToken token = tokens[index];
                if (token.Kind != DdfTokenKind.Identifier || token.End > source.Length)
                    continue;
                int previous = index - 1;
                while (previous >= 0 && (tokens[previous].Kind ==
                    DdfTokenKind.LineComment || tokens[previous].Kind ==
                    DdfTokenKind.BlockComment)) previous--;
                if (previous >= 0 && tokens[previous].Length == 1 &&
                    tokens[previous].Start < source.Length &&
                    source[tokens[previous].Start] == '.') continue;
                int next = index + 1;
                while (next < tokens.Count && (tokens[next].Kind == DdfTokenKind.LineComment ||
                    tokens[next].Kind == DdfTokenKind.BlockComment)) next++;
                if (next >= tokens.Count || tokens[next].Start >= source.Length ||
                    source[tokens[next].Start] != '(' ||
                    tokens[next].Kind != DdfTokenKind.Punctuation)
                    continue;

                if (occurrences.TryGetValue(token.Start, out DdfSymbolOccurrence occurrence))
                {
                    if (occurrence.Symbol.Kind != DdfSymbolKind.Function) continue;
                }
                else if (!callableNames.Contains(source.Substring(token.Start, token.Length)))
                    continue;
                if (!seenStarts.Add(token.Start)) continue;
                spans.Add(new ClassifiedSpan(token.Start, token.Length,
                    SyntaxKind.Function));
            }
            return spans.OrderBy(item => item.Start).ToList().AsReadOnly();
        }
    }
}
