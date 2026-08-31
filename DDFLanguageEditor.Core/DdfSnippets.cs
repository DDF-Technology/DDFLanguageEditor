using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSnippetTemplate
    {
        public DdfSnippetTemplate(string prefix, string displayText, string description, string body, bool requiresKeyword = false)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("A prefix is required.", nameof(prefix));
            if (string.IsNullOrWhiteSpace(displayText)) throw new ArgumentException("Display text is required.", nameof(displayText));
            if (string.IsNullOrEmpty(body)) throw new ArgumentException("A body is required.", nameof(body));
            Prefix = prefix;
            DisplayText = displayText;
            Description = description ?? string.Empty;
            Body = body;
            RequiresKeyword = requiresKeyword;
        }

        public string Prefix { get; }
        public string DisplayText { get; }
        public string Description { get; }
        public string Body { get; }
        public bool RequiresKeyword { get; }
    }

    public sealed class DdfSnippetPlaceholder
    {
        public DdfSnippetPlaceholder(int index, int start, int length)
        {
            if (index <= 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (start < 0 || length < 0) throw new ArgumentOutOfRangeException(nameof(start));
            Index = index;
            Start = start;
            Length = length;
        }

        public int Index { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfSnippetExpansion
    {
        internal DdfSnippetExpansion(string text, IReadOnlyList<DdfSnippetPlaceholder> placeholders, int finalCaret)
        {
            Text = text ?? string.Empty;
            Placeholders = placeholders ?? throw new ArgumentNullException(nameof(placeholders));
            FinalCaret = finalCaret;
        }

        public string Text { get; }
        public IReadOnlyList<DdfSnippetPlaceholder> Placeholders { get; }
        public int FinalCaret { get; }
    }

    public static class DdfSnippetCatalog
    {
        private static readonly IReadOnlyList<DdfSnippetTemplate> templates = Array.AsReadOnly(new[]
        {
            new DdfSnippetTemplate("if", "if — blocco", "Blocco condizionale",
                "if(${1:condition})\n{\n    ${2:statement};\n}${0}", true),
            new DdfSnippetTemplate("while", "while — ciclo", "Ciclo con condizione iniziale",
                "while(${1:condition})\n{\n    ${2:statement};\n}${0}", true),
            new DdfSnippetTemplate("do", "do — ciclo", "Ciclo con condizione finale",
                "do\n{\n    ${1:statement};\n} while(${2:condition});${0}", true),
            new DdfSnippetTemplate("for", "for — ciclo", "Ciclo indicizzato",
                "for(int ${1:index}; ${2:index} < ${3:limit}; ${4:index}++)\n{\n    ${5:statement};\n}${0}", true),
            new DdfSnippetTemplate("function", "function — dichiarazione", "Dichiarazione di funzione",
                "${1:name}(${2:int} ${3:parameter}) out ${4:void}\n{\n    ${5:statement};\n}${0}"),
            new DdfSnippetTemplate("main", "main — entry point", "Funzione principale",
                "main() out ${1:int}\n{\n    ${2:ret 0};\n}${0}"),
            new DdfSnippetTemplate("struct", "struct — dichiarazione", "Dichiarazione di struttura",
                "struct ${1:Name}\n{\n    ${2:int} ${3:field};\n}${0}", true)
        });

        public static IReadOnlyList<DdfSnippetTemplate> Templates => templates;
    }

    public static class DdfSnippetService
    {
        public static DdfSnippetExpansion Expand(DdfSnippetTemplate template, string baseIndent)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            baseIndent = baseIndent ?? string.Empty;
            var builder = new StringBuilder();
            var placeholders = new List<DdfSnippetPlaceholder>();
            int finalCaret = -1;

            for (int position = 0; position < template.Body.Length;)
            {
                if (position + 2 < template.Body.Length && template.Body[position] == '$' && template.Body[position + 1] == '{')
                {
                    int close = template.Body.IndexOf('}', position + 2);
                    if (close < 0) throw new FormatException("Segnaposto snippet non terminato: " + template.DisplayText);
                    string marker = template.Body.Substring(position + 2, close - position - 2);
                    int separator = marker.IndexOf(':');
                    string indexText = separator < 0 ? marker : marker.Substring(0, separator);
                    if (!int.TryParse(indexText, out int index) || index < 0)
                        throw new FormatException("Indice snippet non valido: " + marker);
                    string defaultText = separator < 0 ? string.Empty : marker.Substring(separator + 1);
                    if (index == 0)
                    {
                        finalCaret = builder.Length;
                    }
                    else
                    {
                        int start = builder.Length;
                        builder.Append(defaultText);
                        placeholders.Add(new DdfSnippetPlaceholder(index, start, defaultText.Length));
                    }
                    position = close + 1;
                    continue;
                }

                char value = template.Body[position++];
                builder.Append(value);
                if (value == '\n' && position < template.Body.Length) builder.Append(baseIndent);
            }

            placeholders.Sort((left, right) => left.Index.CompareTo(right.Index));
            if (finalCaret < 0) finalCaret = builder.Length;
            return new DdfSnippetExpansion(builder.ToString(), placeholders.AsReadOnly(), finalCaret);
        }

        public static string GetLineIndent(string source, int position)
        {
            source = source ?? string.Empty;
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            int lineStart = position == 0 ? 0 : source.LastIndexOf('\n', position - 1) + 1;
            int end = lineStart;
            while (end < position && (source[end] == ' ' || source[end] == '\t')) end++;
            return source.Substring(lineStart, end - lineStart);
        }
    }
}
