using System;
using System.Collections.Generic;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfFormatResult
    {
        private readonly IReadOnlyList<PositionMapEntry> positions;
        private readonly int sourceLength;

        internal DdfFormatResult(string text, int sourceLength, IReadOnlyList<PositionMapEntry> positions)
        {
            Text = text ?? string.Empty;
            this.sourceLength = sourceLength;
            this.positions = positions ?? throw new ArgumentNullException(nameof(positions));
        }

        public string Text { get; }

        public int MapPosition(int sourcePosition)
        {
            if (sourcePosition < 0 || sourcePosition > sourceLength) throw new ArgumentOutOfRangeException(nameof(sourcePosition));
            PositionMapEntry previous = null;
            foreach (PositionMapEntry entry in positions)
            {
                if (sourcePosition < entry.SourceStart)
                {
                    return previous == null ? entry.OutputStart : previous.OutputEnd;
                }

                if (sourcePosition <= entry.SourceEnd)
                {
                    return entry.OutputStart + Math.Min(sourcePosition - entry.SourceStart, entry.Length);
                }

                previous = entry;
            }

            return previous == null ? 0 : previous.OutputEnd;
        }

        public EditorEdit CreateEdit(string source, int selectionStart, int selectionLength)
        {
            source = source ?? throw new ArgumentNullException(nameof(source));
            if (source.Length != sourceLength) throw new ArgumentException("The source does not match the formatted input.", nameof(source));
            if (selectionStart < 0 || selectionLength < 0 || selectionStart > source.Length - selectionLength)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionStart));
            }

            int mappedStart = Math.Min(MapPosition(selectionStart), Text.Length);
            int mappedEnd = Math.Min(MapPosition(selectionStart + selectionLength), Text.Length);
            return new EditorEdit(0, source.Length, Text, mappedStart, Math.Max(0, mappedEnd - mappedStart));
        }

        internal sealed class PositionMapEntry
        {
            public PositionMapEntry(int sourceStart, int length, int outputStart)
            {
                SourceStart = sourceStart;
                Length = length;
                OutputStart = outputStart;
            }

            public int SourceStart { get; }
            public int Length { get; }
            public int SourceEnd => SourceStart + Length;
            public int OutputStart { get; }
            public int OutputEnd => OutputStart + Length;
        }
    }

    public static class DdfFormatter
    {
        public const int DefaultIndentSize = 4;

        public static DdfFormatResult Format(
            string source,
            DdfLanguageDefinition language = null,
            int indentSize = DefaultIndentSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (indentSize <= 0) throw new ArgumentOutOfRangeException(nameof(indentSize));
            language = language ?? DdfLanguageCatalog.Default;

            DdfLexResult lexResult = DdfLexer.Lex(source, language);
            var writer = new FormatWriter(indentSize);
            IReadOnlyList<DdfToken> tokens = lexResult.Tokens;
            for (int index = 0; index < tokens.Count; index++)
            {
                DdfToken token = tokens[index];
                DdfToken previous = index == 0 ? null : tokens[index - 1];
                DdfToken next = index + 1 < tokens.Count ? tokens[index + 1] : null;
                string text = source.Substring(token.Start, token.Length);
                string previousText = previous == null ? null : source.Substring(previous.Start, previous.Length);
                string nextText = next == null ? null : source.Substring(next.Start, next.Length);

                if (token.Kind == DdfTokenKind.LibraryDirective)
                {
                    writer.NewLine();
                    writer.WriteToken(token, text);
                    writer.NewLine();
                    if (next != null && next.Kind != DdfTokenKind.LibraryDirective) writer.BlankLine();
                    continue;
                }

                if (token.Kind == DdfTokenKind.LineComment)
                {
                    if (IsSameSourceLine(source, previous, token)) writer.ReopenPreviousLine();
                    writer.Space();
                    writer.WriteToken(token, text);
                    writer.NewLine();
                    continue;
                }

                if (token.Kind == DdfTokenKind.BlockComment)
                {
                    if (IsSameSourceLine(source, previous, token)) writer.ReopenPreviousLine();
                    writer.Space();
                    writer.WriteToken(token, text);
                    writer.NewLine();
                    continue;
                }

                if (token.Kind == DdfTokenKind.Punctuation)
                {
                    FormatPunctuation(writer, token, text, nextText);
                    continue;
                }

                if (token.Kind == DdfTokenKind.Operator)
                {
                    FormatOperator(writer, token, text, previous, previousText, language);
                    continue;
                }

                if (NeedsWordSeparator(previous, previousText, language)) writer.Space();
                writer.WriteToken(token, text);
            }

            return writer.Complete(source.Length);
        }

        private static void FormatPunctuation(FormatWriter writer, DdfToken token, string text, string nextText)
        {
            switch (text)
            {
                case "{":
                    writer.NewLine();
                    writer.WriteToken(token, text);
                    writer.IncreaseIndent();
                    writer.NewLine();
                    break;
                case "}":
                    writer.DecreaseIndent();
                    writer.NewLine();
                    writer.WriteToken(token, text);
                    if (!string.Equals(nextText, ";", StringComparison.Ordinal)) writer.NewLine();
                    break;
                case ";":
                    writer.TrimTrailingSpace();
                    writer.WriteToken(token, text);
                    if (writer.ParenthesisDepth > 0) writer.Space();
                    else writer.NewLine();
                    break;
                case ",":
                    writer.TrimTrailingSpace();
                    writer.WriteToken(token, text);
                    writer.Space();
                    break;
                case "(":
                    writer.TrimTrailingSpace();
                    writer.WriteToken(token, text);
                    writer.ParenthesisDepth++;
                    break;
                case ")":
                    writer.TrimTrailingSpace();
                    writer.WriteToken(token, text);
                    writer.ParenthesisDepth = Math.Max(0, writer.ParenthesisDepth - 1);
                    break;
                case "[":
                case "]":
                case ".":
                    writer.TrimTrailingSpace();
                    writer.WriteToken(token, text);
                    break;
                default:
                    writer.WriteToken(token, text);
                    break;
            }
        }

        private static void FormatOperator(
            FormatWriter writer,
            DdfToken token,
            string text,
            DdfToken previous,
            string previousText,
            DdfLanguageDefinition language)
        {
            language.TryGetOperator(text, out DdfOperatorDefinition definition);
            bool postfix = definition != null && definition.IsPostfix && definition.BinaryPrecedence == 0;
            bool prefix = definition != null && definition.IsPrefix &&
                          (definition.BinaryPrecedence == 0 || IsUnaryPosition(previous, previousText));
            if (postfix)
            {
                writer.TrimTrailingSpace();
                writer.WriteToken(token, text);
            }
            else if (prefix)
            {
                if (NeedsWordSeparator(previous, previousText, language)) writer.Space();
                writer.WriteToken(token, text);
            }
            else
            {
                writer.Space();
                writer.WriteToken(token, text);
                writer.Space();
            }
        }

        private static bool NeedsWordSeparator(DdfToken previous, string previousText, DdfLanguageDefinition language)
        {
            if (previous == null) return false;
            if (previous.Kind == DdfTokenKind.Operator &&
                language.TryGetOperator(previousText, out DdfOperatorDefinition definition) &&
                definition.IsPrefix && (definition.BinaryPrecedence == 0 || IsUnaryPosition(null, null)))
            {
                return false;
            }

            return previous.Kind != DdfTokenKind.Punctuation ||
                   (previousText != "(" && previousText != "[" && previousText != ".");
        }

        private static bool IsUnaryPosition(DdfToken previous, string previousText)
        {
            if (previous == null) return true;
            if (previous.Kind == DdfTokenKind.Operator) return true;
            return previous.Kind == DdfTokenKind.Punctuation &&
                   (previousText == "(" || previousText == "[" || previousText == "{" ||
                    previousText == "," || previousText == ";");
        }

        private static bool IsSameSourceLine(string source, DdfToken previous, DdfToken current)
        {
            if (previous == null || current.Start < previous.End) return false;
            for (int index = previous.End; index < current.Start; index++)
            {
                if (source[index] == '\r' || source[index] == '\n') return false;
            }

            return true;
        }

        private sealed class FormatWriter
        {
            private readonly StringBuilder builder = new StringBuilder();
            private readonly List<DdfFormatResult.PositionMapEntry> positions =
                new List<DdfFormatResult.PositionMapEntry>();
            private readonly int indentSize;
            private int indentLevel;
            private bool atLineStart = true;

            public FormatWriter(int indentSize)
            {
                this.indentSize = indentSize;
            }

            public int ParenthesisDepth { get; set; }

            public void IncreaseIndent()
            {
                indentLevel++;
            }

            public void DecreaseIndent()
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            public void WriteToken(DdfToken token, string text)
            {
                EnsureIndent();
                positions.Add(new DdfFormatResult.PositionMapEntry(token.Start, token.Length, builder.Length));
                builder.Append(text);
                int lastNewLine = text.LastIndexOf('\n');
                atLineStart = lastNewLine >= 0 && lastNewLine == text.Length - 1;
            }

            public void Space()
            {
                if (atLineStart || builder.Length == 0 || builder[builder.Length - 1] == ' ') return;
                builder.Append(' ');
            }

            public void TrimTrailingSpace()
            {
                while (builder.Length > 0 && (builder[builder.Length - 1] == ' ' || builder[builder.Length - 1] == '\t'))
                {
                    builder.Length--;
                }
            }

            public void NewLine()
            {
                TrimTrailingSpace();
                if (builder.Length == 0 || atLineStart) return;
                builder.Append('\n');
                atLineStart = true;
            }

            public void BlankLine()
            {
                NewLine();
                if (builder.Length > 0 && atLineStart) builder.Append('\n');
            }

            public void ReopenPreviousLine()
            {
                if (!atLineStart || builder.Length == 0 || builder[builder.Length - 1] != '\n') return;
                builder.Length--;
                atLineStart = false;
            }

            public DdfFormatResult Complete(int sourceLength)
            {
                while (builder.Length > 0 && char.IsWhiteSpace(builder[builder.Length - 1])) builder.Length--;
                return new DdfFormatResult(builder.ToString(), sourceLength, positions.AsReadOnly());
            }

            private void EnsureIndent()
            {
                if (!atLineStart) return;
                builder.Append(' ', indentLevel * indentSize);
                atLineStart = false;
            }
        }
    }
}
