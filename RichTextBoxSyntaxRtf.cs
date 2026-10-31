using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal static class RichTextBoxSyntaxRtf
    {
        public static double LastBuildMilliseconds { get; private set; }
        public static double LastStreamMilliseconds { get; private set; }

        public static void Apply(RichTextBox editor, string text,
            IReadOnlyList<DdfToken> tokens, Color defaultColor,
            Func<SyntaxKind, Color> colorSelector,
            IReadOnlyList<ClassifiedSpan> semanticSpans = null)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (colorSelector == null) throw new ArgumentNullException(nameof(colorSelector));
            text = text ?? string.Empty;
            tokens = tokens ?? Array.Empty<DdfToken>();

            var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var semanticStarts = new HashSet<int>((semanticSpans ??
                Array.Empty<ClassifiedSpan>()).Select(span => span.Start));
            var classified = tokens
                .Select(token => new { Token = token,
                    Kind = DdfSyntaxClassifier.ToSyntaxKind(token.Kind) })
                .Where(item => item.Kind.HasValue && item.Token.Length > 0 &&
                    !semanticStarts.Contains(item.Token.Start))
                .Select(item => new ClassifiedSpan(item.Token.Start,
                    item.Token.Length, item.Kind.Value))
                .Concat(semanticSpans ?? Array.Empty<ClassifiedSpan>())
                .OrderBy(item => item.Start)
                .ToList();
            var colors = new List<Color> { defaultColor };
            foreach (Color color in classified.Select(item => colorSelector(item.Kind)))
            {
                if (!colors.Contains(color)) colors.Add(color);
            }

            var builder = new StringBuilder(Math.Max(256, text.Length + text.Length / 8));
            builder.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern ");
            AppendEscaped(builder, editor.Font.Name);
            builder.Append(@";}}{\colortbl ;");
            foreach (Color color in colors)
            {
                builder.Append("\\red").Append(color.R)
                    .Append("\\green").Append(color.G)
                    .Append("\\blue").Append(color.B).Append(';');
            }
            builder.Append(@"}\viewkind4\uc1\pard\plain\f0\fs")
                .Append(Math.Max(1, (int)Math.Round(editor.Font.SizeInPoints * 2F)))
                .Append(@"\cf1\ulnone ");

            int position = 0;
            int colorIndex = 1;
            foreach (var item in classified)
            {
                int start = Math.Max(position, Math.Min(item.Start, text.Length));
                int end = Math.Max(start, Math.Min(item.Start + item.Length, text.Length));
                if (start > position) AppendEscaped(builder, text, position, start - position);
                int nextColorIndex = colors.IndexOf(colorSelector(item.Kind)) + 1;
                if (nextColorIndex != colorIndex)
                {
                    builder.Append("\\cf").Append(nextColorIndex).Append(' ');
                    colorIndex = nextColorIndex;
                }
                AppendEscaped(builder, text, start, end - start);
                position = end;
                if (colorIndex != 1)
                {
                    builder.Append(@"\cf1 ");
                    colorIndex = 1;
                }
            }
            if (position < text.Length)
                AppendEscaped(builder, text, position, text.Length - position);
            builder.Append('}');
            buildStopwatch.Stop();
            LastBuildMilliseconds = buildStopwatch.Elapsed.TotalMilliseconds;

            var streamStopwatch = System.Diagnostics.Stopwatch.StartNew();
            editor.Rtf = builder.ToString();
            streamStopwatch.Stop();
            LastStreamMilliseconds = streamStopwatch.Elapsed.TotalMilliseconds;
            // The owning analysis transaction restores the persisted zoom once
            // after the native update scope has completed.
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            AppendEscaped(builder, value ?? string.Empty, 0, value?.Length ?? 0);
        }

        private static void AppendEscaped(StringBuilder builder, string value,
            int start, int length)
        {
            int end = start + length;
            for (int index = start; index < end; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '\\': builder.Append(@"\\"); break;
                    case '{': builder.Append(@"\{"); break;
                    case '}': builder.Append(@"\}"); break;
                    case '\r':
                        if (index + 1 < end && value[index + 1] == '\n') break;
                        builder.Append(@"\line ");
                        break;
                    case '\n': builder.Append(@"\par "); break;
                    case '\t': builder.Append(@"\tab "); break;
                    default:
                        if (character <= 0x7f) builder.Append(character);
                        else builder.Append("\\u").Append((short)character).Append('?');
                        break;
                }
            }
        }
    }
}
