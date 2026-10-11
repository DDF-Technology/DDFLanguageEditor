using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using DDFLanguageEditor.Hardware;

namespace DDFLanguageEditor.Hardware.Tests
{
    internal static class PhysicalIotSmoke
    {
        internal static void RunIfRequested()
        {
            string kind = Environment.GetEnvironmentVariable(
                "DDF_IOT_SMOKE_KIND");
            if (string.IsNullOrWhiteSpace(kind)) return;

            Console.WriteLine("PHYSICAL IoT smoke requested: " + kind);
            switch (kind.Trim().ToLowerInvariant())
            {
                case "gpio-input":
                    RunGpioInput();
                    break;
                case "gpio-output":
                    RequireWriteConsent();
                    RunGpioOutput();
                    break;
                case "i2c-read":
                    RunI2cRead();
                    break;
                case "spi-loopback":
                    RequireWriteConsent();
                    RunSpiLoopback();
                    break;
                case "pwm":
                    RequireWriteConsent();
                    RunPwm();
                    break;
                default:
                    throw new InvalidOperationException(
                        "DDF_IOT_SMOKE_KIND non riconosciuto.");
            }
            Console.WriteLine("PASS physical .NET IoT smoke: " + kind);
        }

        private static void RunGpioInput()
        {
            var profile = new DdfGpioProfile("physical",
                RequiredInt("DDF_IOT_PIN"), DdfGpioDirection.Input,
                ParsePull(Environment.GetEnvironmentVariable(
                    "DDF_IOT_PULL")), OptionalBool("DDF_IOT_ACTIVE_LOW"));
            WithConnection(profile, connection => Console.WriteLine(
                "GPIO=" + connection.ReadAsync(32, CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult().ToCanonicalString()));
        }

        private static void RunGpioOutput()
        {
            bool level = OptionalBool("DDF_IOT_LEVEL");
            var profile = new DdfGpioProfile("physical",
                RequiredInt("DDF_IOT_PIN"), DdfGpioDirection.Output,
                DdfGpioPullMode.None,
                OptionalBool("DDF_IOT_ACTIVE_LOW"), null, level);
            WithConnection(profile, connection => connection.WriteAsync(
                DdfIotFrame.Digital(level),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());
        }

        private static void RunI2cRead()
        {
            int length = RequiredInt("DDF_IOT_LENGTH");
            var profile = new DdfI2cProfile("physical",
                RequiredInt("DDF_IOT_BUS"), RequiredInt("DDF_IOT_ADDRESS"),
                length, length);
            WithConnection(profile, connection => Console.WriteLine(
                "I2C=" + connection.ReadAsync(length * 3 + 5,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult()
                    .ToCanonicalString()));
        }

        private static void RunSpiLoopback()
        {
            byte[] request = RequiredBytes("DDF_IOT_BYTES");
            var profile = new DdfSpiProfile("physical",
                RequiredInt("DDF_IOT_BUS"), RequiredInt("DDF_IOT_CS"),
                RequiredInt("DDF_IOT_CLOCK"), (DdfSpiMode)OptionalInt(
                    "DDF_IOT_MODE", 0), 8, request.Length, request.Length);
            WithConnection(profile, connection => Console.WriteLine(
                "SPI=" + connection.ExchangeAsync(DdfIotFrame.Data(request),
                    CancellationToken.None).AsTask().GetAwaiter().GetResult()
                    .ToCanonicalString()));
        }

        private static void RunPwm()
        {
            int frequency = RequiredInt("DDF_IOT_FREQUENCY");
            double duty = RequiredDouble("DDF_IOT_DUTY");
            var profile = new DdfPwmProfile("physical",
                OptionalInt("DDF_IOT_CHIP", 0),
                RequiredInt("DDF_IOT_CHANNEL"), 1, frequency, frequency,
                duty);
            WithConnection(profile, _ => Thread.Sleep(Math.Clamp(
                OptionalInt("DDF_IOT_DURATION_MS", 250), 1, 5000)));
        }

        private static void WithConnection(DdfIotEndpointProfile profile,
            Action<DdfSystemDeviceIotConnection> action)
        {
            var connection = new DdfSystemDeviceIotConnection();
            try
            {
                connection.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                connection.ApplyProfileAsync(profile, CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                action(connection);
            }
            finally
            {
                connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private static void RequireWriteConsent()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(
                    "DDF_IOT_SMOKE_ALLOW_WRITE"), "YES",
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il test fisico attivo richiede " +
                    "DDF_IOT_SMOKE_ALLOW_WRITE=YES.");
        }

        private static DdfGpioPullMode ParsePull(string value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "up" => DdfGpioPullMode.PullUp,
                "down" => DdfGpioPullMode.PullDown,
                "" or "none" => DdfGpioPullMode.None,
                _ => throw new InvalidOperationException(
                    "DDF_IOT_PULL deve essere none, up o down.")
            };

        private static bool OptionalBool(string name) => string.Equals(
            Environment.GetEnvironmentVariable(name), "true",
            StringComparison.OrdinalIgnoreCase) ||
            Environment.GetEnvironmentVariable(name) == "1";

        private static int RequiredInt(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new
                InvalidOperationException(name + " è obbligatoria.");
            return ParseInt(name, value);
        }

        private static int OptionalInt(string name, int fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback :
                ParseInt(name, value);
        }

        private static int ParseInt(string name, string value)
        {
            NumberStyles style = NumberStyles.Integer;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
                style = NumberStyles.AllowHexSpecifier;
            }
            if (!int.TryParse(value, style, CultureInfo.InvariantCulture,
                    out int result))
                throw new InvalidOperationException(name + " non valida.");
            return result;
        }

        private static double RequiredDouble(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double result))
                throw new InvalidOperationException(name + " non valida.");
            return result;
        }

        private static byte[] RequiredBytes(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new
                InvalidOperationException(name + " è obbligatoria.");
            try
            {
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries).Select(item => byte.Parse(
                        item, NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture)).ToArray();
            }
            catch (Exception exception) when (exception is FormatException ||
                exception is OverflowException)
            {
                throw new InvalidOperationException(
                    name + " deve contenere byte esadecimali separati da virgola.",
                    exception);
            }
        }
    }
}
