using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public static class DdfSyntaxClassifier
    {
        public static IReadOnlyList<ClassifiedSpan> Classify(string text)
        {
            return Classify(text, DdfLanguageCatalog.Default);
        }

        public static IReadOnlyList<ClassifiedSpan> Classify(string text, DdfLanguageDefinition language)
        {
            var spans = new List<ClassifiedSpan>();
            foreach (DdfToken token in DdfLexer.Lex(text, language).Tokens)
            {
                SyntaxKind? kind = ToSyntaxKind(token.Kind);
                if (kind.HasValue)
                {
                    spans.Add(new ClassifiedSpan(token.Start, token.Length, kind.Value));
                }
            }

            return spans;
        }

        public static SyntaxKind? ToSyntaxKind(DdfTokenKind kind)
        {
            switch (kind)
            {
                case DdfTokenKind.LineComment:
                case DdfTokenKind.BlockComment:
                    return SyntaxKind.Comment;
                case DdfTokenKind.LibraryDirective:
                    return SyntaxKind.Library;
                case DdfTokenKind.StringLiteral:
                    return SyntaxKind.String;
                case DdfTokenKind.Punctuation:
                    return SyntaxKind.Grammar;
                case DdfTokenKind.NumberLiteral:
                case DdfTokenKind.BooleanLiteral:
                    return SyntaxKind.Number;
                case DdfTokenKind.DataTypeKeyword:
                    return SyntaxKind.DataType;
                case DdfTokenKind.Operator:
                    return SyntaxKind.Operator;
                case DdfTokenKind.FunctionKeyword:
                    return SyntaxKind.Function;
                case DdfTokenKind.ControlFlowKeyword:
                    return SyntaxKind.ControlFlow;
                case DdfTokenKind.BadToken:
                    return SyntaxKind.Error;
                default:
                    return null;
            }
        }
    }
}
