using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class EditorEdit
    {
        public EditorEdit(int start, int length, string replacement, int selectionStart, int selectionLength)
        {
            Start = start;
            Length = length;
            Replacement = replacement ?? string.Empty;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
        }

        public int Start { get; }

        public int Length { get; }

        public string Replacement { get; }

        public int SelectionStart { get; }

        public int SelectionLength { get; }
    }

    public static class EditorEditing
    {
        public const int DefaultTabSize = 4;

        public static EditorEdit CreateTabEdit(
            string text,
            int selectionStart,
            int selectionLength,
            bool outdent,
            int tabSize = DefaultTabSize)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            if (tabSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tabSize));
            }

            if (!outdent && selectionLength == 0)
            {
                string spaces = new string(' ', tabSize);
                return new EditorEdit(selectionStart, 0, spaces, selectionStart + spaces.Length, 0);
            }

            if (outdent && selectionLength == 0)
            {
                int lineStart = FindLineStart(text, selectionStart);
                int remove = CountLeadingSpaces(text, lineStart, tabSize);
                int caret = selectionStart <= lineStart + remove
                    ? Math.Max(lineStart, selectionStart - remove)
                    : selectionStart - remove;
                return new EditorEdit(lineStart, remove, string.Empty, caret, 0);
            }

            return CreateBlockIndentEdit(text, selectionStart, selectionLength, outdent, tabSize);
        }

        public static EditorEdit CreateNewLineEdit(
            string text,
            int selectionStart,
            int selectionLength,
            int tabSize = DefaultTabSize)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            if (tabSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tabSize));
            }

            int lineStart = FindLineStart(text, selectionStart);
            string beforeCaret = text.Substring(lineStart, selectionStart - lineStart);
            string indentation = GetLeadingWhitespace(beforeCaret);
            string trimmed = beforeCaret.TrimEnd();
            if (selectionLength == 0 && selectionStart > 0 && selectionStart < text.Length &&
                text[selectionStart - 1] == '{' && text[selectionStart] == '}')
            {
                string innerIndentation = indentation + new string(' ', tabSize);
                string blockReplacement = "\n" + innerIndentation + "\n" + indentation;
                return new EditorEdit(selectionStart, 0, blockReplacement,
                    selectionStart + 1 + innerIndentation.Length, 0);
            }
            if (trimmed.EndsWith("{", StringComparison.Ordinal) ||
                trimmed.EndsWith("(", StringComparison.Ordinal))
            {
                indentation += new string(' ', tabSize);
            }

            string replacement = "\n" + indentation;
            return new EditorEdit(
                selectionStart,
                selectionLength,
                replacement,
                selectionStart + replacement.Length,
                0);
        }

        public static EditorEdit CreateClosingBraceEdit(string text, int selectionStart, int selectionLength)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            if (selectionLength > 0)
            {
                return new EditorEdit(selectionStart, selectionLength, "}", selectionStart + 1, 0);
            }

            int lineStart = FindLineStart(text, selectionStart);
            string beforeCaret = text.Substring(lineStart, selectionStart - lineStart);
            if (beforeCaret.Any(character => character != ' ' && character != '\t'))
            {
                return new EditorEdit(selectionStart, 0, "}", selectionStart + 1, 0);
            }

            int openingBrace = FindUnmatchedOpeningBrace(text, selectionStart);
            if (openingBrace < 0)
            {
                return new EditorEdit(selectionStart, 0, "}", selectionStart + 1, 0);
            }

            int openingLineStart = FindLineStart(text, openingBrace);
            string openingPrefix = text.Substring(openingLineStart, openingBrace - openingLineStart);
            string indentation = GetLeadingWhitespace(openingPrefix);
            return new EditorEdit(
                lineStart,
                selectionStart - lineStart,
                indentation + "}",
                lineStart + indentation.Length + 1,
                0);
        }

        public static EditorEdit CreatePairedCharacterEdit(
            string text,
            int selectionStart,
            int selectionLength,
            char character)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            char closing = GetClosingCharacter(character);
            bool isOpening = closing != '\0';
            bool isClosing = character == ')' || character == ']' || character == '}' || character == '"' || character == '\'';
            if (!isOpening && !isClosing) return null;

            if (selectionLength > 0 && isOpening)
            {
                string selected = text.Substring(selectionStart, selectionLength);
                return new EditorEdit(selectionStart, selectionLength, character + selected + closing,
                    selectionStart + 1, selectionLength);
            }

            if (selectionLength == 0 && isClosing && selectionStart < text.Length && text[selectionStart] == character)
            {
                return new EditorEdit(selectionStart, 0, string.Empty, selectionStart + 1, 0);
            }

            if (isOpening)
            {
                if ((character == '"' || character == '\'') && selectionStart > 0 && text[selectionStart - 1] == '\\')
                    return new EditorEdit(selectionStart, selectionLength, character.ToString(), selectionStart + 1, 0);
                return new EditorEdit(selectionStart, selectionLength, new string(new[] { character, closing }),
                    selectionStart + 1, 0);
            }

            return null;
        }

        public static EditorEdit CreatePairedBackspaceEdit(string text, int selectionStart, int selectionLength)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            if (selectionLength != 0 || selectionStart == 0 || selectionStart >= text.Length) return null;
            char opening = text[selectionStart - 1];
            char closing = text[selectionStart];
            if (GetClosingCharacter(opening) != closing) return null;
            return new EditorEdit(selectionStart - 1, 2, string.Empty, selectionStart - 1, 0);
        }

        public static EditorEdit CreateToggleLineCommentEdit(string text, int selectionStart, int selectionLength)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            int selectionEnd = selectionStart + selectionLength;
            int rangeStart = FindLineStart(text, selectionStart);
            int effectiveEnd = selectionEnd;
            if (selectionLength > 0 && effectiveEnd > 0 && effectiveEnd <= text.Length && text[effectiveEnd - 1] == '\n') effectiveEnd--;
            int rangeEnd = FindLineEnd(text, effectiveEnd);
            string block = text.Substring(rangeStart, rangeEnd - rangeStart);
            string[] lines = block.Split('\n');
            bool uncomment = lines.Where(line => line.Trim().Length > 0)
                .All(line => line.Substring(GetLeadingWhitespace(line).Length).StartsWith("//", StringComparison.Ordinal));
            if (!lines.Any(line => line.Trim().Length > 0)) uncomment = false;

            var replacement = new StringBuilder();
            var lineEdits = new List<LineDelta>();
            int absoluteLineStart = rangeStart;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                int indentationLength = GetLeadingWhitespace(line).Length;
                int editPosition = absoluteLineStart + indentationLength;
                int delta = 0;
                if (line.Trim().Length == 0)
                {
                    replacement.Append(line);
                }
                else if (uncomment)
                {
                    int remove = line.Length > indentationLength + 2 && line[indentationLength + 2] == ' ' ? 3 : 2;
                    replacement.Append(line.Substring(0, indentationLength));
                    replacement.Append(line.Substring(indentationLength + remove));
                    delta = -remove;
                }
                else
                {
                    replacement.Append(line.Substring(0, indentationLength));
                    replacement.Append("// ");
                    replacement.Append(line.Substring(indentationLength));
                    delta = 3;
                }

                if (delta != 0) lineEdits.Add(new LineDelta(editPosition, delta));
                if (index < lines.Length - 1) replacement.Append('\n');
                absoluteLineStart += line.Length + 1;
            }

            int mappedStart = MapPositionAfterLineEdits(selectionStart, lineEdits);
            int mappedEnd = MapPositionAfterLineEdits(selectionEnd, lineEdits);
            if (selectionLength == 0) mappedEnd = mappedStart;
            return new EditorEdit(rangeStart, rangeEnd - rangeStart, replacement.ToString(), mappedStart, Math.Max(0, mappedEnd - mappedStart));
        }

        private static int MapPositionAfterLineEdits(int position, IList<LineDelta> edits)
        {
            int mapped = position;
            foreach (LineDelta edit in edits)
            {
                if (edit.Delta >= 0)
                {
                    if (position >= edit.Position) mapped += edit.Delta;
                    continue;
                }

                int removed = -edit.Delta;
                if (position >= edit.Position + removed) mapped -= removed;
                else if (position >= edit.Position) mapped -= position - edit.Position;
            }
            return mapped;
        }

        private static char GetClosingCharacter(char opening)
        {
            switch (opening)
            {
                case '(': return ')';
                case '[': return ']';
                case '{': return '}';
                case '"': return '"';
                case '\'': return '\'';
                default: return '\0';
            }
        }

        private static EditorEdit CreateBlockIndentEdit(
            string text,
            int selectionStart,
            int selectionLength,
            bool outdent,
            int tabSize)
        {
            int selectionEnd = selectionStart + selectionLength;
            int rangeStart = FindLineStart(text, selectionStart);
            int effectiveEnd = selectionEnd;
            if (effectiveEnd > selectionStart && effectiveEnd > 0 && text[effectiveEnd - 1] == '\n')
            {
                effectiveEnd--;
            }

            List<int> lineStarts = GetLineStarts(text, rangeStart, effectiveEnd);
            int rangeEnd = selectionEnd;
            string original = text.Substring(rangeStart, rangeEnd - rangeStart);
            var replacement = new StringBuilder(original);

            if (!outdent)
            {
                string spaces = new string(' ', tabSize);
                for (int i = lineStarts.Count - 1; i >= 0; i--)
                {
                    replacement.Insert(lineStarts[i] - rangeStart, spaces);
                }

                int newStart = MapAfterInsertions(selectionStart, lineStarts, tabSize, true);
                int newEnd = MapAfterInsertions(selectionEnd, lineStarts, tabSize, false);
                return new EditorEdit(rangeStart, rangeEnd - rangeStart, replacement.ToString(), newStart, newEnd - newStart);
            }

            var removals = new List<Removal>();
            foreach (int lineStart in lineStarts)
            {
                int remove = CountLeadingSpaces(text, lineStart, tabSize);
                if (remove > 0)
                {
                    removals.Add(new Removal(lineStart, remove));
                }
            }

            for (int i = removals.Count - 1; i >= 0; i--)
            {
                replacement.Remove(removals[i].Start - rangeStart, removals[i].Length);
            }

            int mappedStart = MapAfterRemovals(selectionStart, removals);
            int mappedEnd = MapAfterRemovals(selectionEnd, removals);
            return new EditorEdit(rangeStart, rangeEnd - rangeStart, replacement.ToString(), mappedStart, mappedEnd - mappedStart);
        }

        private static List<int> GetLineStarts(string text, int firstLineStart, int effectiveEnd)
        {
            var starts = new List<int> { firstLineStart };
            for (int index = firstLineStart; index < effectiveEnd && index < text.Length; index++)
            {
                if (text[index] == '\n' && index + 1 <= effectiveEnd)
                {
                    starts.Add(index + 1);
                }
            }

            return starts;
        }

        private static int MapAfterInsertions(int position, List<int> insertions, int amount, bool includeEqual)
        {
            int mapped = position;
            foreach (int insertion in insertions)
            {
                if (insertion < position || (includeEqual && insertion == position))
                {
                    mapped += amount;
                }
            }

            return mapped;
        }

        private static int MapAfterRemovals(int position, List<Removal> removals)
        {
            int mapped = position;
            foreach (Removal removal in removals)
            {
                if (position >= removal.Start + removal.Length)
                {
                    mapped -= removal.Length;
                }
                else if (position > removal.Start)
                {
                    mapped -= position - removal.Start;
                }
            }

            return mapped;
        }

        private static int CountLeadingSpaces(string text, int lineStart, int maximum)
        {
            int count = 0;
            while (lineStart + count < text.Length && count < maximum && text[lineStart + count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static int FindLineStart(string text, int position)
        {
            int previousNewLine = position > 0 ? text.LastIndexOf('\n', position - 1) : -1;
            return previousNewLine + 1;
        }

        private static int FindLineEnd(string text, int position)
        {
            int nextNewLine = text.IndexOf('\n', Math.Min(position, text.Length));
            return nextNewLine < 0 ? text.Length : nextNewLine;
        }

        private static string GetLeadingWhitespace(string value)
        {
            int index = 0;
            while (index < value.Length && (value[index] == ' ' || value[index] == '\t'))
            {
                index++;
            }

            return value.Substring(0, index);
        }

        private static int FindUnmatchedOpeningBrace(string text, int position)
        {
            var stack = new List<int>();
            DdfLexResult lexResult = DdfLexer.Lex(text);
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (token.Start >= position) break;
                if (token.Kind != DdfTokenKind.Punctuation || token.Length != 1) continue;
                char character = text[token.Start];
                if (character == '{') stack.Add(token.Start);
                else if (character == '}' && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }

            return stack.Count == 0 ? -1 : stack[stack.Count - 1];
        }

        private static void ValidateSelection(string text, int start, int length)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (start < 0 || length < 0 || start > text.Length - length)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
        }

        private sealed class Removal
        {
            public Removal(int start, int length)
            {
                Start = start;
                Length = length;
            }

            public int Start { get; }

            public int Length { get; }
        }

        private sealed class LineDelta
        {
            public LineDelta(int position, int delta)
            {
                Position = position;
                Delta = delta;
            }

            public int Position { get; }
            public int Delta { get; }
        }
    }
}
