using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public static class DdfLexer
    {
        public static DdfLexResult Lex(string text)
        {
            return Lex(text, 0, DdfLanguageCatalog.Default);
        }

        public static DdfLexResult Lex(string text, int startIndex)
        {
            return Lex(text, startIndex, DdfLanguageCatalog.Default);
        }

        public static DdfLexResult Lex(string text, DdfLanguageDefinition language)
        {
            return Lex(text, 0, language);
        }

        public static DdfLexResult Lex(string text, int startIndex, DdfLanguageDefinition language)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (startIndex < 0 || startIndex > text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            var tokens = new List<DdfToken>();
            var diagnostics = new List<DdfDiagnostic>();
            int index = startIndex;
            while (index < text.Length)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    index++;
                    continue;
                }

                if (StartsWith(text, index, "//"))
                {
                    int end = index + 2;
                    while (end < text.Length && text[end] != '\r' && text[end] != '\n')
                    {
                        end++;
                    }

                    tokens.Add(new DdfToken(DdfTokenKind.LineComment, index, end - index));
                    index = end;
                    continue;
                }

                if (StartsWith(text, index, "/*"))
                {
                    int endMarker = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    int end = endMarker < 0 ? text.Length : endMarker + 2;
                    tokens.Add(new DdfToken(DdfTokenKind.BlockComment, index, end - index));
                    if (endMarker < 0)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            text,
                            "DDF002",
                            "Commento a blocchi non terminato.",
                            index,
                            end - index));
                    }

                    index = end;
                    continue;
                }

                if (StartsWith(text, index, "@@'"))
                {
                    int endMarker = text.IndexOf('\'', index + 3);
                    int end = endMarker < 0 ? text.Length : endMarker + 1;
                    tokens.Add(new DdfToken(DdfTokenKind.LibraryDirective, index, end - index));
                    if (endMarker < 0)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            text,
                            "DDF003",
                            "Inclusione di libreria non terminata.",
                            index,
                            end - index));
                    }

                    index = end;
                    continue;
                }

                if (text[index] == '"')
                {
                    int end = FindStringEnd(text, index + 1, out bool terminated);
                    tokens.Add(new DdfToken(DdfTokenKind.StringLiteral, index, end - index));
                    if (!terminated)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            text,
                            "DDF001",
                            "Stringa non terminata.",
                            index,
                            end - index));
                    }

                    index = end;
                    continue;
                }

                if (char.IsLetter(text[index]) || text[index] == '_')
                {
                    int end = index + 1;
                    while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                    {
                        end++;
                    }

                    string identifier = text.Substring(index, end - index);
                    tokens.Add(new DdfToken(ClassifyIdentifier(identifier, language), index, end - index));
                    index = end;
                    continue;
                }

                if (char.IsDigit(text[index]))
                {
                    int end = index + 1;
                    while (end < text.Length && char.IsDigit(text[end]))
                    {
                        end++;
                    }

                    if (end + 1 < text.Length && text[end] == '.' && char.IsDigit(text[end + 1]))
                    {
                        end++;
                        while (end < text.Length && char.IsDigit(text[end]))
                        {
                            end++;
                        }
                    }

                    tokens.Add(new DdfToken(DdfTokenKind.NumberLiteral, index, end - index));
                    index = end;
                    continue;
                }

                string matchedOperator = language.FindOperator(text, index);
                if (matchedOperator != null)
                {
                    tokens.Add(new DdfToken(DdfTokenKind.Operator, index, matchedOperator.Length));
                    index += matchedOperator.Length;
                    continue;
                }

                if (language.IsPunctuation(text[index]))
                {
                    tokens.Add(new DdfToken(DdfTokenKind.Punctuation, index, 1));
                    index++;
                    continue;
                }

                int badLength = char.IsHighSurrogate(text[index]) &&
                                index + 1 < text.Length &&
                                char.IsLowSurrogate(text[index + 1])
                    ? 2
                    : 1;
                tokens.Add(new DdfToken(DdfTokenKind.BadToken, index, badLength));
                diagnostics.Add(CreateDiagnostic(
                    text,
                    "DDF004",
                    "Carattere non riconosciuto.",
                    index,
                    badLength));
                index += badLength;
            }

            return new DdfLexResult(tokens, diagnostics);
        }

        private static DdfTokenKind ClassifyIdentifier(string identifier, DdfLanguageDefinition language)
        {
            if (language.TryGetKeyword(identifier, out DdfKeywordDefinition keyword))
            {
                return keyword.TokenKind;
            }

            if (language.IsBooleanLiteral(identifier))
            {
                return DdfTokenKind.BooleanLiteral;
            }

            return DdfTokenKind.Identifier;
        }

        private static int FindStringEnd(string text, int index, out bool terminated)
        {
            bool escaped = false;
            while (index < text.Length)
            {
                char current = text[index++];
                if (current == '"' && !escaped)
                {
                    terminated = true;
                    return index;
                }

                escaped = current == '\\' && !escaped;
            }

            terminated = false;
            return text.Length;
        }

        private static bool StartsWith(string text, int index, string value)
        {
            return index + value.Length <= text.Length &&
                   string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }

        private static DdfDiagnostic CreateDiagnostic(
            string text,
            string code,
            string message,
            int start,
            int length)
        {
            GetLineAndColumn(text, start, out int line, out int column);
            return new DdfDiagnostic(code, message, start, Math.Max(1, length), line, column);
        }

        private static void GetLineAndColumn(string text, int position, out int line, out int column)
        {
            line = 1;
            column = 1;
            for (int index = 0; index < position; index++)
            {
                if (text[index] == '\n')
                {
                    line++;
                    column = 1;
                }
                else if (text[index] != '\r')
                {
                    column++;
                }
            }
        }
    }
}
