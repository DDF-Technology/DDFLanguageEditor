using System;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class ReplaceResult
    {
        public ReplaceResult(string text, int replacementCount)
        {
            Text = text ?? string.Empty;
            ReplacementCount = replacementCount;
        }

        public string Text { get; }

        public int ReplacementCount { get; }
    }

    public static class TextSearch
    {
        public static int FindNext(string text, string value, int startIndex, bool matchCase)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (string.IsNullOrEmpty(value))
            {
                return -1;
            }

            if (startIndex < 0 || startIndex > text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            StringComparison comparison = matchCase
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            int index = text.IndexOf(value, startIndex, comparison);
            if (index < 0 && startIndex > 0)
            {
                index = text.IndexOf(value, 0, startIndex, comparison);
            }

            return index;
        }

        public static ReplaceResult ReplaceAll(string text, string find, string replacement, bool matchCase)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (string.IsNullOrEmpty(find))
            {
                return new ReplaceResult(text, 0);
            }

            replacement = replacement ?? string.Empty;
            StringComparison comparison = matchCase
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            var result = new StringBuilder(text.Length);
            int sourceIndex = 0;
            int count = 0;

            while (sourceIndex < text.Length)
            {
                int match = text.IndexOf(find, sourceIndex, comparison);
                if (match < 0)
                {
                    break;
                }

                result.Append(text, sourceIndex, match - sourceIndex);
                result.Append(replacement);
                sourceIndex = match + find.Length;
                count++;
            }

            if (count == 0)
            {
                return new ReplaceResult(text, 0);
            }

            result.Append(text, sourceIndex, text.Length - sourceIndex);
            return new ReplaceResult(result.ToString(), count);
        }
    }
}
