using System;
using System.Collections.Generic;
using System.Globalization;

namespace DDF___Program_Language_Editor
{
    public enum EditorTheme
    {
        Dark,
        Light
    }

    public enum EditorLineEnding
    {
        Lf,
        CrLf
    }

    public sealed class EditorSettings
    {
        public const string DefaultFontFamily = "Consolas";
        public const float DefaultFontSize = 10F;
        public const int DefaultZoomPercent = 100;
        public const int DefaultIndentSize = 4;

        public EditorSettings(string fontFamily, float fontSize, int zoomPercent, EditorTheme theme,
            bool useTabs, int indentSize, EditorLineEnding lineEnding, bool formatOnSave)
        {
            if (string.IsNullOrWhiteSpace(fontFamily)) throw new ArgumentException("A font family is required.", nameof(fontFamily));
            if (fontSize < 8F || fontSize > 32F) throw new ArgumentOutOfRangeException(nameof(fontSize));
            if (zoomPercent < 50 || zoomPercent > 200) throw new ArgumentOutOfRangeException(nameof(zoomPercent));
            if (indentSize < 1 || indentSize > 8) throw new ArgumentOutOfRangeException(nameof(indentSize));
            FontFamily = fontFamily.Trim();
            FontSize = fontSize;
            ZoomPercent = zoomPercent;
            Theme = theme;
            UseTabs = useTabs;
            IndentSize = indentSize;
            LineEnding = lineEnding;
            FormatOnSave = formatOnSave;
        }

        public string FontFamily { get; }
        public float FontSize { get; }
        public int ZoomPercent { get; }
        public EditorTheme Theme { get; }
        public bool UseTabs { get; }
        public int IndentSize { get; }
        public EditorLineEnding LineEnding { get; }
        public bool FormatOnSave { get; }

        public static EditorSettings Default => new EditorSettings(DefaultFontFamily, DefaultFontSize,
            DefaultZoomPercent, EditorTheme.Dark, false, DefaultIndentSize, EditorLineEnding.Lf, false);

        public static EditorSettings Parse(string value)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator > 0) values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }

            EditorSettings defaults = Default;
            string font = get(values, "font", defaults.FontFamily);
            float fontSize = parseFloat(values, "fontSize", defaults.FontSize, 8F, 32F);
            int zoom = parseInt(values, "zoom", defaults.ZoomPercent, 50, 200);
            int indent = parseInt(values, "indentSize", defaults.IndentSize, 1, 8);
            bool tabs = parseBool(values, "useTabs", defaults.UseTabs);
            bool formatOnSave = parseBool(values, "formatOnSave", defaults.FormatOnSave);
            EditorTheme theme = parseEnum(values, "theme", defaults.Theme);
            EditorLineEnding lineEnding = parseEnum(values, "lineEnding", defaults.LineEnding);
            return new EditorSettings(font, fontSize, zoom, theme, tabs, indent, lineEnding, formatOnSave);
        }

        public string Serialize()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "font=" + FontFamily,
                "fontSize=" + FontSize.ToString(CultureInfo.InvariantCulture),
                "zoom=" + ZoomPercent,
                "theme=" + Theme,
                "useTabs=" + UseTabs,
                "indentSize=" + IndentSize,
                "lineEnding=" + LineEnding,
                "formatOnSave=" + FormatOnSave
            });
        }

        public string ApplyLineEndings(string text)
        {
            string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            return LineEnding == EditorLineEnding.CrLf ? normalized.Replace("\n", "\r\n") : normalized;
        }

        private static string get(IDictionary<string, string> values, string key, string fallback) =>
            values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        private static int parseInt(IDictionary<string, string> values, string key, int fallback, int minimum, int maximum) =>
            values.TryGetValue(key, out string value) && int.TryParse(value, out int parsed) && parsed >= minimum && parsed <= maximum
                ? parsed : fallback;

        private static float parseFloat(IDictionary<string, string> values, string key, float fallback, float minimum, float maximum) =>
            values.TryGetValue(key, out string value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) &&
            parsed >= minimum && parsed <= maximum ? parsed : fallback;

        private static bool parseBool(IDictionary<string, string> values, string key, bool fallback) =>
            values.TryGetValue(key, out string value) && bool.TryParse(value, out bool parsed) ? parsed : fallback;

        private static T parseEnum<T>(IDictionary<string, string> values, string key, T fallback) where T : struct =>
            values.TryGetValue(key, out string value) && Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }
}
