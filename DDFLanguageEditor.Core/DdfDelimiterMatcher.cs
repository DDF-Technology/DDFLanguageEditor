using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfDelimiterMatch
    {
        public DdfDelimiterMatch(int openStart, char openCharacter, int closeStart, char closeCharacter)
        {
            OpenStart = openStart;
            OpenCharacter = openCharacter;
            CloseStart = closeStart;
            CloseCharacter = closeCharacter;
        }

        public int OpenStart { get; }
        public char OpenCharacter { get; }
        public int CloseStart { get; }
        public char CloseCharacter { get; }
    }

    public static class DdfDelimiterMatcher
    {
        public static DdfDelimiterMatch FindMatch(string text, int caretPosition)
        {
            return FindMatch(text, caretPosition, DdfLexer.Lex(text));
        }

        public static DdfDelimiterMatch FindMatch(string text, int caretPosition, DdfLexResult lexResult)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (lexResult == null) throw new ArgumentNullException(nameof(lexResult));
            if (caretPosition < 0 || caretPosition > text.Length) throw new ArgumentOutOfRangeException(nameof(caretPosition));

            IReadOnlyList<DdfDelimiterMatch> pairs = FindPairs(text, lexResult);
            if (caretPosition < text.Length)
            {
                DdfDelimiterMatch atCaret = FindPairAt(pairs, caretPosition);
                if (atCaret != null) return atCaret;
            }

            return caretPosition > 0 ? FindPairAt(pairs, caretPosition - 1) : null;
        }

        public static IReadOnlyList<DdfDelimiterMatch> FindPairs(string text, DdfLexResult lexResult)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (lexResult == null) throw new ArgumentNullException(nameof(lexResult));

            var stack = new Stack<DdfToken>();
            var pairs = new List<DdfDelimiterMatch>();
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (token.Kind != DdfTokenKind.Punctuation || token.Length != 1 ||
                    token.Start < 0 || token.Start >= text.Length) continue;
                char value = text[token.Start];
                if (IsOpening(value))
                {
                    stack.Push(token);
                }
                else if (IsClosing(value) && stack.Count > 0)
                {
                    DdfToken open = stack.Peek();
                    if (open.Start < 0 || open.Start >= text.Length)
                    {
                        stack.Pop();
                        continue;
                    }

                    char openValue = text[open.Start];
                    if (IsPair(openValue, value))
                    {
                        stack.Pop();
                        pairs.Add(new DdfDelimiterMatch(open.Start, openValue, token.Start, value));
                    }
                }
            }

            pairs.Sort((left, right) => left.OpenStart.CompareTo(right.OpenStart));
            return pairs.AsReadOnly();
        }

        private static DdfDelimiterMatch FindPairAt(IReadOnlyList<DdfDelimiterMatch> pairs, int position)
        {
            foreach (DdfDelimiterMatch pair in pairs)
            {
                if (pair.OpenStart == position || pair.CloseStart == position) return pair;
            }

            return null;
        }

        private static bool IsOpening(char value)
        {
            return value == '(' || value == '[' || value == '{';
        }

        private static bool IsClosing(char value)
        {
            return value == ')' || value == ']' || value == '}';
        }

        private static bool IsPair(char open, char close)
        {
            return (open == '(' && close == ')') ||
                   (open == '[' && close == ']') ||
                   (open == '{' && close == '}');
        }
    }
}
