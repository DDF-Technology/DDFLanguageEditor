using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public sealed class ShortcutCommandDefinition
    {
        public ShortcutCommandDefinition(string id, string category, string title, Keys defaultShortcut)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Category = category ?? string.Empty;
            Title = title ?? id;
            DefaultShortcut = defaultShortcut;
        }

        public string Id { get; }
        public string Category { get; }
        public string Title { get; }
        public Keys DefaultShortcut { get; }
    }

    public sealed class ShortcutSettings
    {
        private readonly Dictionary<string, Keys> shortcuts;

        public ShortcutSettings(IEnumerable<ShortcutCommandDefinition> definitions, IDictionary<string, Keys> values)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            shortcuts = definitions.ToDictionary(
                definition => definition.Id,
                definition => values != null && values.TryGetValue(definition.Id, out Keys value)
                    ? value
                    : definition.DefaultShortcut,
                StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, Keys> Values => shortcuts;

        public Keys Get(string commandId) => commandId != null && shortcuts.TryGetValue(commandId, out Keys value)
            ? value
            : Keys.None;

        public static ShortcutSettings Default(IEnumerable<ShortcutCommandDefinition> definitions)
        {
            var list = definitions.ToList();
            return new ShortcutSettings(list, list.ToDictionary(item => item.Id, item => item.DefaultShortcut,
                StringComparer.OrdinalIgnoreCase));
        }

        public static ShortcutSettings Parse(string value, IEnumerable<ShortcutCommandDefinition> definitions)
        {
            var list = definitions.ToList();
            var parsed = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                string id = line.Substring(0, separator).Trim();
                if (!int.TryParse(line.Substring(separator + 1).Trim(), out int raw)) continue;
                Keys shortcut = (Keys)raw;
                if (IsBindable(shortcut)) parsed[id] = shortcut;
            }

            var result = new ShortcutSettings(list, parsed);
            return result.FindConflicts(list).Count == 0 ? result : Default(list);
        }

        public string Serialize()
        {
            return string.Join(Environment.NewLine, shortcuts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + "=" + (int)pair.Value));
        }

        public IReadOnlyList<string> FindConflicts(IEnumerable<ShortcutCommandDefinition> definitions)
        {
            return definitions
                .Where(definition => Get(definition.Id) != Keys.None)
                .GroupBy(definition => Get(definition.Id))
                .Where(group => group.Count() > 1)
                .Select(group => Format(group.Key) + ": " + string.Join(", ", group.Select(item => item.Title)))
                .ToList()
                .AsReadOnly();
        }

        public static bool IsBindable(Keys shortcut)
        {
            if (shortcut == Keys.None) return true;
            Keys keyCode = shortcut & Keys.KeyCode;
            if (keyCode == Keys.None || keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu)
                return false;
            bool hasCommandModifier = (shortcut & (Keys.Control | Keys.Alt)) != Keys.None;
            bool isFunctionKey = keyCode >= Keys.F1 && keyCode <= Keys.F24;
            return hasCommandModifier || isFunctionKey;
        }

        public static string Format(Keys shortcut)
        {
            if (shortcut == Keys.None) return "Non assegnata";
            return new KeysConverter().ConvertToString(shortcut) ?? shortcut.ToString();
        }
    }
}
