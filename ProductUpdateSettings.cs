using System;
using System.Collections.Generic;
using System.Globalization;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public sealed class ProductUpdateSettings
    {
        public const string SchemaVersion = "1.0";
        public const int DefaultCadenceDays = 7;

        public ProductUpdateSettings(bool automaticCheckEnabled,
            DdfProductUpdateChannel channel, int cadenceDays,
            DateTimeOffset? lastCheckUtc)
        {
            if (cadenceDays < 1 || cadenceDays > 30)
                throw new ArgumentOutOfRangeException(nameof(cadenceDays));
            AutomaticCheckEnabled = automaticCheckEnabled;
            Channel = channel;
            CadenceDays = cadenceDays;
            LastCheckUtc = lastCheckUtc?.ToUniversalTime();
        }

        public bool AutomaticCheckEnabled { get; }
        public DdfProductUpdateChannel Channel { get; }
        public int CadenceDays { get; }
        public DateTimeOffset? LastCheckUtc { get; }

        public static ProductUpdateSettings Default
        {
            get
            {
                DdfProductUpdateChannel channel =
                    DdfProductVersionCatalog.Current.DefaultChannel == "stable"
                        ? DdfProductUpdateChannel.Stable
                        : DdfProductUpdateChannel.Beta;
                return new ProductUpdateSettings(false, channel,
                    DefaultCadenceDays, null);
            }
        }

        public static ProductUpdateSettings Parse(string value)
        {
            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string line in (value ?? string.Empty).Split(
                new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator > 0)
                    values[line.Substring(0, separator).Trim()] =
                        line.Substring(separator + 1).Trim();
            }

            ProductUpdateSettings defaults = Default;
            if (values.TryGetValue("schemaVersion", out string schema) &&
                schema != SchemaVersion)
                return defaults;
            bool automatic = values.TryGetValue("automaticCheckEnabled",
                out string automaticText) && bool.TryParse(automaticText,
                out bool automaticValue) ? automaticValue :
                defaults.AutomaticCheckEnabled;
            DdfProductUpdateChannel channel = values.TryGetValue("channel",
                out string channelText) && Enum.TryParse(channelText, true,
                out DdfProductUpdateChannel channelValue) ? channelValue :
                defaults.Channel;
            int cadence = values.TryGetValue("cadenceDays", out string cadenceText) &&
                int.TryParse(cadenceText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int cadenceValue) &&
                cadenceValue >= 1 && cadenceValue <= 30
                    ? cadenceValue : defaults.CadenceDays;
            DateTimeOffset? lastCheck = values.TryGetValue("lastCheckUtc",
                out string lastCheckText) && DateTimeOffset.TryParse(lastCheckText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsedLastCheck)
                ? parsedLastCheck : null;
            return new ProductUpdateSettings(automatic, channel, cadence,
                lastCheck);
        }

        public ProductUpdateSettings WithPreferences(bool automaticCheckEnabled,
            DdfProductUpdateChannel channel, int cadenceDays) =>
            new ProductUpdateSettings(automaticCheckEnabled, channel,
                cadenceDays, LastCheckUtc);

        public ProductUpdateSettings WithLastCheck(DateTimeOffset value) =>
            new ProductUpdateSettings(AutomaticCheckEnabled, Channel,
                CadenceDays, value);

        public string Serialize() => string.Join(Environment.NewLine, new[]
        {
            "schemaVersion=" + SchemaVersion,
            "automaticCheckEnabled=" + AutomaticCheckEnabled,
            "channel=" + Channel,
            "cadenceDays=" + CadenceDays.ToString(CultureInfo.InvariantCulture),
            "lastCheckUtc=" + (LastCheckUtc?.ToString("O",
                CultureInfo.InvariantCulture) ?? string.Empty)
        });
    }
}
