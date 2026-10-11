using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Hardware
{
    public enum DdfGpioDirection { Input, Output }
    public enum DdfGpioPullMode { None, PullUp, PullDown }
    public enum DdfSpiMode { Mode0, Mode1, Mode2, Mode3 }
    public enum DdfIotFrameKind { DigitalLevel, Bytes, Pwm }

    public sealed class DdfIotFrame
    {
        private DdfIotFrame(DdfIotFrameKind kind, bool level, byte[] bytes,
            double frequency, double dutyCycle)
        {
            Kind = kind;
            Level = level;
            Bytes = Array.AsReadOnly(bytes ?? Array.Empty<byte>());
            Frequency = frequency;
            DutyCycle = dutyCycle;
        }

        public DdfIotFrameKind Kind { get; }
        public bool Level { get; }
        public IReadOnlyList<byte> Bytes { get; }
        public double Frequency { get; }
        public double DutyCycle { get; }

        public static DdfIotFrame Digital(bool value) => new(
            DdfIotFrameKind.DigitalLevel, value, null, 0, 0);
        public static DdfIotFrame Data(params byte[] bytes)
        {
            byte[] copy = (bytes ?? Array.Empty<byte>()).ToArray();
            if (copy.Length == 0) throw new ArgumentOutOfRangeException(
                nameof(bytes));
            return new DdfIotFrame(DdfIotFrameKind.Bytes, false, copy, 0, 0);
        }
        public static DdfIotFrame Pwm(double frequency, double dutyCycle)
        {
            if (!double.IsFinite(frequency) || frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency));
            if (!double.IsFinite(dutyCycle) || dutyCycle < 0 || dutyCycle > 1)
                throw new ArgumentOutOfRangeException(nameof(dutyCycle));
            return new DdfIotFrame(DdfIotFrameKind.Pwm, false, null,
                frequency, dutyCycle);
        }

        public string ToCanonicalString() => Kind switch
        {
            DdfIotFrameKind.DigitalLevel => Level ? "level:1" : "level:0",
            DdfIotFrameKind.Bytes => "data:" + string.Join(",", Bytes.Select(
                item => item.ToString("X2", CultureInfo.InvariantCulture))),
            DdfIotFrameKind.Pwm => "pwm:" + Frequency.ToString("R",
                CultureInfo.InvariantCulture) + ":" + DutyCycle.ToString("R",
                CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Frame IoT sconosciuto.")
        };

        public static DdfIotFrame Parse(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (value == "level:0") return Digital(false);
            if (value == "level:1") return Digital(true);
            if (value.StartsWith("data:", StringComparison.Ordinal))
            {
                string[] parts = value.Substring(5).Split(',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
                try
                {
                    return Data(parts.Select(item => byte.Parse(item,
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture)).ToArray());
                }
                catch (Exception exception) when (exception is FormatException ||
                    exception is OverflowException ||
                    exception is ArgumentException)
                {
                    throw new FormatException("Payload IoT non valido.",
                        exception);
                }
            }
            string[] pwm = value.Split(':');
            if (pwm.Length == 3 && pwm[0] == "pwm" && double.TryParse(pwm[1],
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double frequency) && double.TryParse(pwm[2],
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double duty))
                return Pwm(frequency, duty);
            throw new FormatException("Frame IoT canonico non valido.");
        }
    }

    public abstract class DdfIotEndpointProfile
    {
        protected DdfIotEndpointProfile(string id,
            DdfHardwareTransportKind transport, int maxTransferBytes)
        {
            Id = DdfHardwareDeviceIdentity.Identifier(id, nameof(id));
            Transport = transport;
            if (maxTransferBytes <= 0 || maxTransferBytes > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxTransferBytes));
            MaxTransferBytes = maxTransferBytes;
        }
        public string Id { get; }
        public DdfHardwareTransportKind Transport { get; }
        public int MaxTransferBytes { get; }
        internal abstract void Validate(DdfIotFrame frame, bool writing,
            bool exchange);
    }

    public sealed class DdfGpioProfile : DdfIotEndpointProfile
    {
        public DdfGpioProfile(string id, int pinNumber,
            DdfGpioDirection direction, DdfGpioPullMode pullMode,
            bool activeLow = false, TimeSpan? debounce = null,
            bool initialOutputLevel = false)
            : base(id, DdfHardwareTransportKind.Gpio, 1)
        {
            if (pinNumber < 0) throw new ArgumentOutOfRangeException(
                nameof(pinNumber));
            if (!Enum.IsDefined(typeof(DdfGpioDirection), direction))
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (!Enum.IsDefined(typeof(DdfGpioPullMode), pullMode))
                throw new ArgumentOutOfRangeException(nameof(pullMode));
            if (direction == DdfGpioDirection.Output &&
                pullMode != DdfGpioPullMode.None)
                throw new ArgumentException(
                    "Un output GPIO non usa una resistenza di pull input.",
                    nameof(pullMode));
            TimeSpan filter = debounce ?? TimeSpan.Zero;
            if (filter < TimeSpan.Zero || filter > TimeSpan.FromSeconds(10))
                throw new ArgumentOutOfRangeException(nameof(debounce));
            PinNumber = pinNumber;
            Direction = direction;
            PullMode = pullMode;
            ActiveLow = activeLow;
            Debounce = filter;
            InitialOutputLevel = initialOutputLevel;
        }
        public int PinNumber { get; }
        public DdfGpioDirection Direction { get; }
        public DdfGpioPullMode PullMode { get; }
        public bool ActiveLow { get; }
        public TimeSpan Debounce { get; }
        public bool InitialOutputLevel { get; }
        internal override void Validate(DdfIotFrame frame, bool writing,
            bool exchange)
        {
            if (exchange || frame.Kind != DdfIotFrameKind.DigitalLevel)
                throw new InvalidOperationException("Frame GPIO non valido.");
            if (writing && Direction != DdfGpioDirection.Output)
                throw new InvalidOperationException("GPIO configurato input.");
            if (!writing && Direction != DdfGpioDirection.Input)
                throw new InvalidOperationException("GPIO configurato output.");
        }
    }

    public sealed class DdfI2cProfile : DdfIotEndpointProfile
    {
        public DdfI2cProfile(string id, int busId, int deviceAddress,
            int maxTransferBytes = 256, int readTransferBytes = 1) : base(id,
                DdfHardwareTransportKind.I2c, maxTransferBytes)
        {
            if (busId < 0) throw new ArgumentOutOfRangeException(nameof(busId));
            if (deviceAddress < 0x03 || deviceAddress > 0x77)
                throw new ArgumentOutOfRangeException(nameof(deviceAddress));
            if (readTransferBytes <= 0 ||
                readTransferBytes > maxTransferBytes)
                throw new ArgumentOutOfRangeException(
                    nameof(readTransferBytes));
            BusId = busId;
            DeviceAddress = deviceAddress;
            ReadTransferBytes = readTransferBytes;
        }
        public int BusId { get; }
        public int DeviceAddress { get; }
        public int ReadTransferBytes { get; }
        internal override void Validate(DdfIotFrame frame, bool writing,
            bool exchange)
        {
            if (frame.Kind != DdfIotFrameKind.Bytes ||
                frame.Bytes.Count > MaxTransferBytes)
                throw new InvalidOperationException("Frame I2C non valido.");
        }
    }

    public sealed class DdfSpiProfile : DdfIotEndpointProfile
    {
        public DdfSpiProfile(string id, int busId, int chipSelectLine,
            int clockFrequency, DdfSpiMode mode, int dataBitLength = 8,
            int maxTransferBytes = 4096, int readTransferBytes = 1) : base(id,
                DdfHardwareTransportKind.Spi, maxTransferBytes)
        {
            if (busId < 0) throw new ArgumentOutOfRangeException(nameof(busId));
            if (chipSelectLine < 0) throw new ArgumentOutOfRangeException(
                nameof(chipSelectLine));
            if (clockFrequency <= 0 || clockFrequency > 100_000_000)
                throw new ArgumentOutOfRangeException(nameof(clockFrequency));
            if (!Enum.IsDefined(typeof(DdfSpiMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (dataBitLength < 4 || dataBitLength > 32)
                throw new ArgumentOutOfRangeException(nameof(dataBitLength));
            if (readTransferBytes <= 0 ||
                readTransferBytes > maxTransferBytes)
                throw new ArgumentOutOfRangeException(
                    nameof(readTransferBytes));
            BusId = busId;
            ChipSelectLine = chipSelectLine;
            ClockFrequency = clockFrequency;
            Mode = mode;
            DataBitLength = dataBitLength;
            ReadTransferBytes = readTransferBytes;
        }
        public int BusId { get; }
        public int ChipSelectLine { get; }
        public int ClockFrequency { get; }
        public DdfSpiMode Mode { get; }
        public int DataBitLength { get; }
        public int ReadTransferBytes { get; }
        internal override void Validate(DdfIotFrame frame, bool writing,
            bool exchange)
        {
            if (frame.Kind != DdfIotFrameKind.Bytes ||
                frame.Bytes.Count > MaxTransferBytes)
                throw new InvalidOperationException("Frame SPI non valido.");
        }
    }

    public sealed class DdfPwmProfile : DdfIotEndpointProfile
    {
        public DdfPwmProfile(string id, int chip, int channel,
            double minimumFrequency, double maximumFrequency,
            double initialFrequency, double initialDutyCycle,
            bool allowDynamicChanges = true) : base(id,
                DdfHardwareTransportKind.Pwm, 1)
        {
            if (chip < 0) throw new ArgumentOutOfRangeException(nameof(chip));
            if (channel < 0) throw new ArgumentOutOfRangeException(
                nameof(channel));
            if (!double.IsFinite(minimumFrequency) || minimumFrequency <= 0 ||
                !double.IsFinite(maximumFrequency) ||
                maximumFrequency < minimumFrequency)
                throw new ArgumentOutOfRangeException(nameof(minimumFrequency));
            DdfIotFrame initial = DdfIotFrame.Pwm(initialFrequency,
                initialDutyCycle);
            if (initial.Frequency < minimumFrequency ||
                initial.Frequency > maximumFrequency)
                throw new ArgumentOutOfRangeException(nameof(initialFrequency));
            Chip = chip;
            Channel = channel;
            MinimumFrequency = minimumFrequency;
            MaximumFrequency = maximumFrequency;
            InitialFrequency = initialFrequency;
            InitialDutyCycle = initialDutyCycle;
            AllowDynamicChanges = allowDynamicChanges;
        }
        public int Chip { get; }
        public int Channel { get; }
        public double MinimumFrequency { get; }
        public double MaximumFrequency { get; }
        public double InitialFrequency { get; }
        public double InitialDutyCycle { get; }
        public bool AllowDynamicChanges { get; }
        internal override void Validate(DdfIotFrame frame, bool writing,
            bool exchange)
        {
            if (!writing || exchange || frame.Kind != DdfIotFrameKind.Pwm ||
                !AllowDynamicChanges || frame.Frequency < MinimumFrequency ||
                frame.Frequency > MaximumFrequency)
                throw new InvalidOperationException("Frame PWM non valido.");
        }
    }

    public interface IDdfIotEndpointConnection : IAsyncDisposable
    {
        bool IsOpen { get; }
        event EventHandler<DdfIotFrame> InputChanged;
        event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        ValueTask OpenAsync(CancellationToken cancellationToken);
        ValueTask CloseAsync();
        ValueTask<DdfIotFrame> ReadAsync(int maxBytes,
            CancellationToken cancellationToken);
        ValueTask WriteAsync(DdfIotFrame frame,
            CancellationToken cancellationToken);
        ValueTask<DdfIotFrame> ExchangeAsync(DdfIotFrame frame,
            CancellationToken cancellationToken);
        ValueTask ApplyProfileAsync(DdfIotEndpointProfile profile,
            CancellationToken cancellationToken);
    }

    public sealed class DdfIotHardwareDevice : IDdfHardwareDevice,
        IDdfHardwareExchangeDevice
    {
        private readonly IDdfIotEndpointConnection connection;
        private readonly IReadOnlyDictionary<string, DdfIotEndpointProfile>
            profiles;
        private DdfIotEndpointProfile activeProfile;
        private int available = 1;
        private int disposed;

        public DdfIotHardwareDevice(DdfHardwareDeviceIdentity identity,
            IEnumerable<DdfIotEndpointProfile> profiles,
            string initialProfileId, IDdfIotEndpointConnection connection)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (identity.Transport != DdfHardwareTransportKind.Gpio &&
                identity.Transport != DdfHardwareTransportKind.I2c &&
                identity.Transport != DdfHardwareTransportKind.Spi &&
                identity.Transport != DdfHardwareTransportKind.Pwm)
                throw new ArgumentException("Trasporto IoT non valido.",
                    nameof(identity));
            this.connection = connection ?? throw new ArgumentNullException(
                nameof(connection));
            DdfIotEndpointProfile[] values = (profiles ??
                Array.Empty<DdfIotEndpointProfile>()).ToArray();
            if (values.Length == 0 || values.Any(item => item == null ||
                    item.Transport != identity.Transport) || values.Select(
                    item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
                values.Length)
                throw new ArgumentException(
                    "Profili IoT assenti, duplicati o di altro trasporto.",
                    nameof(profiles));
            this.profiles = values.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            if (!this.profiles.TryGetValue(initialProfileId ?? string.Empty,
                    out activeProfile))
                throw new ArgumentException("Profilo IoT iniziale assente.",
                    nameof(initialProfileId));
            connection.InputChanged += OnInputChanged;
            connection.AvailabilityChanged += OnAvailabilityChanged;
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public bool IsAvailable => Volatile.Read(ref available) != 0;
        public DdfIotEndpointProfile ActiveProfile => activeProfile;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        public event EventHandler<DdfIotFrame> InputChanged;

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsAvailable) throw new InvalidOperationException(
                "Endpoint IoT non disponibile.");
            bool opened = false;
            try
            {
                await connection.OpenAsync(cancellationToken);
                opened = true;
                await connection.ApplyProfileAsync(activeProfile,
                    cancellationToken);
            }
            catch
            {
                if (opened) try { await connection.CloseAsync(); } catch { }
                throw;
            }
        }

        public ValueTask CloseAsync()
        {
            ThrowIfDisposed();
            return connection.CloseAsync();
        }

        public async ValueTask<string> ReadAsync(int maxCharacters,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            DdfIotFrame frame = await connection.ReadAsync(
                activeProfile.MaxTransferBytes, cancellationToken);
            activeProfile.Validate(frame, false, false);
            return Bounded(frame, maxCharacters);
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            DdfIotFrame value = DdfIotFrame.Parse(frame);
            activeProfile.Validate(value, true, false);
            await connection.WriteAsync(value, cancellationToken);
        }

        public async ValueTask<string> ExchangeAsync(string frame,
            int maxCharacters, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (activeProfile.Transport != DdfHardwareTransportKind.I2c &&
                activeProfile.Transport != DdfHardwareTransportKind.Spi)
                throw new InvalidOperationException(
                    "Lo scambio atomico è disponibile soltanto per I2C/SPI.");
            DdfIotFrame request = DdfIotFrame.Parse(frame);
            activeProfile.Validate(request, true, true);
            DdfIotFrame response = await connection.ExchangeAsync(request,
                cancellationToken);
            activeProfile.Validate(response, false, true);
            return Bounded(response, maxCharacters);
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!profiles.TryGetValue(profileId ?? string.Empty,
                    out DdfIotEndpointProfile profile))
                throw new InvalidOperationException(
                    "Profilo IoT non autorizzato.");
            DdfIotEndpointProfile previous = activeProfile;
            try
            {
                await connection.ApplyProfileAsync(profile,
                    cancellationToken);
                activeProfile = profile;
            }
            catch
            {
                try { await connection.ApplyProfileAsync(previous,
                    CancellationToken.None); } catch { }
                throw;
            }
        }

        private string Bounded(DdfIotFrame frame, int maxCharacters)
        {
            if (maxCharacters <= 0) throw new ArgumentOutOfRangeException(
                nameof(maxCharacters));
            string result = frame.ToCanonicalString();
            if (result.Length > maxCharacters)
                throw new InvalidOperationException(
                    "Frame IoT oltre la quota testuale.");
            return result;
        }

        private void OnInputChanged(object sender, DdfIotFrame frame)
            => InputChanged?.Invoke(this, frame);
        private void OnAvailabilityChanged(object sender,
            DdfHardwareAvailabilityChangedEventArgs args)
        {
            Interlocked.Exchange(ref available, args.IsAvailable ? 1 : 0);
            AvailabilityChanged?.Invoke(this,
                new DdfHardwareAvailabilityChangedEventArgs(
                    Identity.LogicalId, args.IsAvailable, args.Generation));
        }
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            connection.InputChanged -= OnInputChanged;
            connection.AvailabilityChanged -= OnAvailabilityChanged;
            try { await connection.CloseAsync(); } catch { }
            await connection.DisposeAsync();
            Interlocked.Exchange(ref available, 0);
        }
    }
}
