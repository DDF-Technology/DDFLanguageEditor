using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfMultiEditResult
    {
        public DdfMultiEditResult(string text, IReadOnlyList<DdfTextRange> selections)
        {
            Text = text;
            Selections = selections;
        }

        public string Text { get; }
        public IReadOnlyList<DdfTextRange> Selections { get; }
    }

    public static class DdfMultiSelectionService
    {
        public static IReadOnlyList<DdfTextRange> FindOccurrences(string text, int selectionStart, int selectionLength)
        {
            ValidateSelection(text, selectionStart, selectionLength);
            if (text.Length == 0) return Array.Empty<DdfTextRange>();

            DdfLexResult lex = DdfLexer.Lex(text);
            DdfToken selectedToken = FindToken(lex.Tokens, selectionStart, selectionLength);
            if (selectedToken != null && (selectionLength == 0 ||
                selectionStart == selectedToken.Start && selectionLength == selectedToken.Length))
            {
                string tokenText = text.Substring(selectedToken.Start, selectedToken.Length);
                return lex.Tokens
                    .Where(token => token.Kind == selectedToken.Kind && token.Length == selectedToken.Length &&
                        string.CompareOrdinal(text, token.Start, tokenText, 0, tokenText.Length) == 0)
                    .Select(token => new DdfTextRange(token.Start, token.Length))
                    .ToList().AsReadOnly();
            }

            if (selectionLength == 0) return Array.Empty<DdfTextRange>();
            string selectedText = text.Substring(selectionStart, selectionLength);
            var ranges = new List<DdfTextRange>();
            int position = 0;
            while (position <= text.Length - selectedText.Length)
            {
                int match = text.IndexOf(selectedText, position, StringComparison.Ordinal);
                if (match < 0) break;
                ranges.Add(new DdfTextRange(match, selectedText.Length));
                position = match + selectedText.Length;
            }
            return ranges.AsReadOnly();
        }

        public static DdfMultiEditResult Replace(string text, IReadOnlyList<DdfTextRange> selections, string replacement)
        {
            if (replacement == null) replacement = string.Empty;
            return ApplyEdits(text, selections.Select(range =>
                new EditorEdit(range.Start, range.Length, replacement, range.Start + replacement.Length, 0)).ToList());
        }

        public static DdfMultiEditResult Backspace(string text, IReadOnlyList<DdfTextRange> selections)
        {
            return ApplyEdits(text, selections.Select(range => range.Length > 0
                ? new EditorEdit(range.Start, range.Length, string.Empty, range.Start, 0)
                : new EditorEdit(Math.Max(0, range.Start - 1), range.Start > 0 ? 1 : 0,
                    string.Empty, Math.Max(0, range.Start - 1), 0)).ToList());
        }

        public static DdfMultiEditResult Delete(string text, IReadOnlyList<DdfTextRange> selections)
        {
            return ApplyEdits(text, selections.Select(range => range.Length > 0
                ? new EditorEdit(range.Start, range.Length, string.Empty, range.Start, 0)
                : new EditorEdit(range.Start, range.Start < text.Length ? 1 : 0,
                    string.Empty, range.Start, 0)).ToList());
        }

        public static DdfMultiEditResult ApplyEdits(string text, IReadOnlyList<EditorEdit> edits)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (edits == null) throw new ArgumentNullException(nameof(edits));
            if (edits.Count == 0) return new DdfMultiEditResult(text, Array.Empty<DdfTextRange>());

            var indexed = edits.Select((edit, index) => new IndexedEdit(edit, index))
                .OrderBy(item => item.Edit.Start).ThenBy(item => item.Index).ToList();
            int previousEnd = -1;
            foreach (IndexedEdit item in indexed)
            {
                EditorEdit edit = item.Edit;
                if (edit.Start < 0 || edit.Length < 0 || edit.Start + edit.Length > text.Length)
                    throw new ArgumentOutOfRangeException(nameof(edits));
                if (edit.Start < previousEnd || (edit.Start == previousEnd && edit.Length == 0 && previousEnd >= 0))
                    throw new ArgumentException("Le modifiche multi-cursore non possono sovrapporsi.", nameof(edits));
                previousEnd = edit.Start + edit.Length;
            }

            var builder = new StringBuilder(text.Length);
            var selections = new DdfTextRange[edits.Count];
            int sourcePosition = 0;
            int delta = 0;
            foreach (IndexedEdit item in indexed)
            {
                EditorEdit edit = item.Edit;
                builder.Append(text, sourcePosition, edit.Start - sourcePosition);
                builder.Append(edit.Replacement);
                selections[item.Index] = new DdfTextRange(edit.SelectionStart + delta, edit.SelectionLength);
                sourcePosition = edit.Start + edit.Length;
                delta += edit.Replacement.Length - edit.Length;
            }
            builder.Append(text, sourcePosition, text.Length - sourcePosition);
            return new DdfMultiEditResult(builder.ToString(), Array.AsReadOnly(selections));
        }

        private static DdfToken FindToken(IReadOnlyList<DdfToken> tokens, int start, int length)
        {
            int end = start + length;
            DdfToken left = null;
            foreach (DdfToken token in tokens)
            {
                if (length == 0)
                {
                    if (token.Start <= start && start < token.End) return token;
                    if (token.End == start) left = token;
                }
                else if (token.Start <= start && token.End >= end) return token;
            }
            return left;
        }

        private static void ValidateSelection(string text, int start, int length)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (start < 0 || length < 0 || start + length > text.Length)
                throw new ArgumentOutOfRangeException(nameof(start));
        }

        private sealed class IndexedEdit
        {
            public IndexedEdit(EditorEdit edit, int index) { Edit = edit; Index = index; }
            public EditorEdit Edit { get; }
            public int Index { get; }
        }
    }
}
