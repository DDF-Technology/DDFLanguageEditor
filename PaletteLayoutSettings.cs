using System;
using System.Collections.Generic;

namespace DDF___Program_Language_Editor
{
    public sealed class PaletteLayoutSettings
    {
        public PaletteLayoutSettings(int outlineWidth, int diagnosticsHeight, bool outlinePinned, bool diagnosticsPinned)
        {
            OutlineWidth = Math.Max(300, Math.Min(1600, outlineWidth));
            DiagnosticsHeight = Math.Max(116, Math.Min(1200, diagnosticsHeight));
            OutlinePinned = outlinePinned;
            DiagnosticsPinned = diagnosticsPinned;
        }

        public int OutlineWidth { get; }
        public int DiagnosticsHeight { get; }
        public bool OutlinePinned { get; }
        public bool DiagnosticsPinned { get; }

        public static PaletteLayoutSettings Default => new PaletteLayoutSettings(300, 116, true, true);

        public static PaletteLayoutSettings Parse(string value)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator > 0) values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            PaletteLayoutSettings defaults = Default;
            return new PaletteLayoutSettings(
                parseInt(values, "outlineWidth", defaults.OutlineWidth),
                parseInt(values, "diagnosticsHeight", defaults.DiagnosticsHeight),
                parseBool(values, "outlinePinned", defaults.OutlinePinned),
                parseBool(values, "diagnosticsPinned", defaults.DiagnosticsPinned));
        }

        public string Serialize()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "outlineWidth=" + OutlineWidth,
                "diagnosticsHeight=" + DiagnosticsHeight,
                "outlinePinned=" + OutlinePinned,
                "diagnosticsPinned=" + DiagnosticsPinned
            });
        }

        private static int parseInt(IDictionary<string, string> values, string key, int fallback) =>
            values.TryGetValue(key, out string value) && int.TryParse(value, out int parsed) ? parsed : fallback;

        private static bool parseBool(IDictionary<string, string> values, string key, bool fallback) =>
            values.TryGetValue(key, out string value) && bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }
}
