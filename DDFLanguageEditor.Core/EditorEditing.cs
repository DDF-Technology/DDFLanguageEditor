using System;
using System.Collections.Generic;
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

        private static string GetLeadingWhitespace(string value)
        {
            int index = 0;
            while (index < value.Length && (value[index] == ' ' || value[index] == '\t'))
            {
                index++;
            }

            return value.Substring(0, index);
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
    }
}
