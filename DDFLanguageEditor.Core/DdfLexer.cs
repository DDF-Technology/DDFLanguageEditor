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
                if (language.LexicalRules.IsWhitespace(text[index]))
                {
                    index++;
                    continue;
                }

                DdfDelimitedTokenRule delimitedRule = language.LexicalRules.FindDelimitedToken(text, index);
                if (delimitedRule != null)
                {
                    int end;
                    bool terminated = true;
                    if (delimitedRule.EndsAtLineBreak)
                    {
                        end = index + delimitedRule.Start.Length;
                        while (end < text.Length && text[end] != '\r' && text[end] != '\n') end++;
                    }
                    else
                    {
                        end = FindDelimitedEnd(text, index + delimitedRule.Start.Length, delimitedRule, out terminated);
                    }

                    tokens.Add(new DdfToken(delimitedRule.TokenKind, index, end - index));
                    if (!terminated)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            text,
                            delimitedRule.UnterminatedCode,
                            delimitedRule.UnterminatedMessage,
                            index,
                            end - index));
                    }

                    index = end;
                    continue;
                }

                if (language.LexicalRules.IsIdentifierStart(text[index]))
                {
                    int end = index + 1;
                    while (end < text.Length && language.LexicalRules.IsIdentifierPart(text[end]))
                    {
                        end++;
                    }

                    string identifier = text.Substring(index, end - index);
                    tokens.Add(new DdfToken(ClassifyIdentifier(identifier, language), index, end - index));
                    index = end;
                    continue;
                }

                if (language.LexicalRules.IsDigit(text[index]))
                {
                    int end = index + 1;
                    while (end < text.Length && language.LexicalRules.IsDigit(text[end]))
                    {
                        end++;
                    }

                    string separator = language.LexicalRules.Number.DecimalSeparator;
                    bool hasSeparator = separator.Length == 1 && end < text.Length && text[end] == separator[0];
                    bool hasFractionDigit = end + 1 < text.Length && language.LexicalRules.IsDigit(text[end + 1]);
                    if (hasSeparator && (!language.LexicalRules.Number.RequireFractionDigits || hasFractionDigit))
                    {
                        end++;
                        while (end < text.Length && language.LexicalRules.IsDigit(text[end]))
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

        private static int FindDelimitedEnd(string text, int index, DdfDelimitedTokenRule rule, out bool terminated)
        {
            while (index < text.Length)
            {
                if (!rule.AllowLineBreak && (text[index] == '\r' || text[index] == '\n'))
                {
                    terminated = false;
                    return index;
                }
                if (rule.EscapeCharacter.Length == 1 && text[index] == rule.EscapeCharacter[0])
                {
                    index += Math.Min(2, text.Length - index);
                    continue;
                }
                if (StartsWith(text, index, rule.End))
                {
                    terminated = true;
                    return index + rule.End.Length;
                }
                index++;
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
