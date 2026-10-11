using System;
using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Pwm;
using System.Device.Spi;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Hardware
{
    /// <summary>
    /// Physical .NET IoT bridge. Provider types remain confined to this
    /// assembly; scripts and the common DDF contract only see canonical frames.
    /// </summary>
    public sealed class DdfSystemDeviceIotConnection :
        IDdfIotEndpointConnection
    {
        private readonly IDdfSystemDeviceEndpointFactory factory;
        private readonly object sync = new();
        private IDdfSystemDeviceEndpoint endpoint;
        private bool open;
        private int disposed;
        private long availabilityGeneration;

        public DdfSystemDeviceIotConnection()
            : this(new DdfSystemDeviceEndpointFactory()) { }

        internal DdfSystemDeviceIotConnection(
            IDdfSystemDeviceEndpointFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(
                nameof(factory));
        }

        public bool IsOpen
        {
            get { lock (sync) return open; }
        }

        public event EventHandler<DdfIotFrame> InputChanged;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;

        public ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync) open = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync()
        {
            IDdfSystemDeviceEndpoint previous;
            lock (sync)
            {
                previous = endpoint;
                endpoint = null;
                open = false;
            }
            Release(previous);
            return ValueTask.CompletedTask;
        }

        public ValueTask ApplyProfileAsync(DdfIotEndpointProfile profile,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (profile == null) throw new ArgumentNullException(
                nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();

            lock (sync)
            {
                if (!open) throw new InvalidOperationException(
                    "La connessione .NET IoT non è aperta.");
            }

            IDdfSystemDeviceEndpoint replacement;
            try
            {
                replacement = factory.Create(profile);
                replacement.InputChanged += OnInputChanged;
            }
            catch (Exception exception) when (IsProviderFailure(exception))
            {
                PublishAvailability(false);
                throw;
            }

            IDdfSystemDeviceEndpoint previous;
            lock (sync)
            {
                if (!open || Volatile.Read(ref disposed) != 0)
                {
                    Release(replacement);
                    throw new ObjectDisposedException(GetType().Name);
                }
                previous = endpoint;
                endpoint = replacement;
            }
            Release(previous);
            PublishAvailability(true);
            return ValueTask.CompletedTask;
        }

        public ValueTask<DdfIotFrame> ReadAsync(int maxBytes,
            CancellationToken cancellationToken)
        {
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(
                nameof(maxBytes));
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Invoke(item => item.Read(maxBytes)));
        }

        public ValueTask WriteAsync(DdfIotFrame frame,
            CancellationToken cancellationToken)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            cancellationToken.ThrowIfCancellationRequested();
            Invoke(item => { item.Write(frame); return true; });
            return ValueTask.CompletedTask;
        }

        public ValueTask<DdfIotFrame> ExchangeAsync(DdfIotFrame frame,
            CancellationToken cancellationToken)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Invoke(item => item.Exchange(frame)));
        }

        private T Invoke<T>(Func<IDdfSystemDeviceEndpoint, T> action)
        {
            ThrowIfDisposed();
            try
            {
                lock (sync)
                {
                    if (!open || endpoint == null)
                        throw new InvalidOperationException(
                            "Endpoint .NET IoT non configurato.");
                    return action(endpoint);
                }
            }
            catch (Exception exception) when (IsProviderFailure(exception))
            {
                PublishAvailability(false);
                throw;
            }
        }

        private void OnInputChanged(object sender, DdfIotFrame frame)
            => InputChanged?.Invoke(this, frame);

        private void PublishAvailability(bool isAvailable)
        {
            long generation = Interlocked.Increment(
                ref availabilityGeneration);
            AvailabilityChanged?.Invoke(this,
                new DdfHardwareAvailabilityChangedEventArgs(
                    "system-device", isAvailable, generation));
        }

        private static bool IsProviderFailure(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException ||
            exception is PlatformNotSupportedException ||
            exception is InvalidOperationException;

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        private void Release(IDdfSystemDeviceEndpoint value)
        {
            if (value == null) return;
            value.InputChanged -= OnInputChanged;
            value.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            await CloseAsync();
        }
    }

    internal interface IDdfSystemDeviceEndpointFactory
    {
        IDdfSystemDeviceEndpoint Create(DdfIotEndpointProfile profile);
    }

    internal interface IDdfSystemDeviceEndpoint : IDisposable
    {
        event EventHandler<DdfIotFrame> InputChanged;
        DdfIotFrame Read(int maxBytes);
        void Write(DdfIotFrame frame);
        DdfIotFrame Exchange(DdfIotFrame frame);
    }

    internal sealed class DdfSystemDeviceEndpointFactory :
        IDdfSystemDeviceEndpointFactory
    {
        public IDdfSystemDeviceEndpoint Create(
            DdfIotEndpointProfile profile) => profile switch
        {
            DdfGpioProfile gpio => new DdfSystemGpioEndpoint(gpio),
            DdfI2cProfile i2c => new DdfSystemI2cEndpoint(i2c),
            DdfSpiProfile spi => new DdfSystemSpiEndpoint(spi),
            DdfPwmProfile pwm => new DdfSystemPwmEndpoint(pwm),
            _ => throw new NotSupportedException(
                "Profilo non supportato dal bridge .NET IoT.")
        };
    }

    internal sealed class DdfSystemGpioEndpoint : IDdfSystemDeviceEndpoint
    {
        private readonly DdfGpioProfile profile;
        private readonly GpioController controller;
        private readonly PinChangeEventHandler callback;
        private long lastEventTicks;

        public DdfSystemGpioEndpoint(DdfGpioProfile profile)
        {
            this.profile = profile;
            controller = new GpioController();
            controller.OpenPin(profile.PinNumber, ToPinMode(profile));
            if (profile.Direction == DdfGpioDirection.Output)
            {
                bool physical = profile.InitialOutputLevel ^
                    profile.ActiveLow;
                controller.Write(profile.PinNumber,
                    physical ? PinValue.High : PinValue.Low);
            }
            if (profile.Direction == DdfGpioDirection.Input)
            {
                callback = OnPinChanged;
                controller.RegisterCallbackForPinValueChangedEvent(
                    profile.PinNumber, PinEventTypes.Rising |
                    PinEventTypes.Falling, callback);
            }
        }

        public event EventHandler<DdfIotFrame> InputChanged;

        public DdfIotFrame Read(int maxBytes)
        {
            bool physical = controller.Read(profile.PinNumber) == PinValue.High;
            return DdfIotFrame.Digital(physical ^ profile.ActiveLow);
        }

        public void Write(DdfIotFrame frame)
        {
            bool physical = frame.Level ^ profile.ActiveLow;
            controller.Write(profile.PinNumber,
                physical ? PinValue.High : PinValue.Low);
        }

        public DdfIotFrame Exchange(DdfIotFrame frame) => throw new
            NotSupportedException("GPIO non supporta lo scambio atomico.");

        private void OnPinChanged(object sender, PinValueChangedEventArgs args)
        {
            long now = Environment.TickCount64;
            long debounce = (long)profile.Debounce.TotalMilliseconds;
            long previous = Interlocked.Read(ref lastEventTicks);
            if (debounce > 0 && previous != 0 && now - previous < debounce)
                return;
            Interlocked.Exchange(ref lastEventTicks, now);
            bool physical = controller.Read(profile.PinNumber) == PinValue.High;
            InputChanged?.Invoke(this,
                DdfIotFrame.Digital(physical ^ profile.ActiveLow));
        }

        private static PinMode ToPinMode(DdfGpioProfile profile)
        {
            if (profile.Direction == DdfGpioDirection.Output)
                return PinMode.Output;
            return profile.PullMode switch
            {
                DdfGpioPullMode.PullUp => PinMode.InputPullUp,
                DdfGpioPullMode.PullDown => PinMode.InputPullDown,
                _ => PinMode.Input
            };
        }

        public void Dispose()
        {
            if (callback != null)
                controller.UnregisterCallbackForPinValueChangedEvent(
                    profile.PinNumber, callback);
            controller.Dispose();
        }
    }

    internal sealed class DdfSystemI2cEndpoint : IDdfSystemDeviceEndpoint
    {
        private readonly DdfI2cProfile profile;
        private readonly I2cDevice device;

        public DdfSystemI2cEndpoint(DdfI2cProfile profile)
        {
            this.profile = profile;
            device = I2cDevice.Create(new I2cConnectionSettings(profile.BusId,
                profile.DeviceAddress));
        }

        public event EventHandler<DdfIotFrame> InputChanged { add { } remove { } }

        public DdfIotFrame Read(int maxBytes)
        {
            byte[] result = new byte[Math.Min(maxBytes,
                profile.ReadTransferBytes)];
            device.Read(result);
            return DdfIotFrame.Data(result);
        }

        public void Write(DdfIotFrame frame) => device.Write(
            frame.Bytes.ToArray());

        public DdfIotFrame Exchange(DdfIotFrame frame)
        {
            byte[] result = new byte[profile.ReadTransferBytes];
            device.WriteRead(frame.Bytes.ToArray(), result);
            return DdfIotFrame.Data(result);
        }

        public void Dispose() => device.Dispose();
    }

    internal sealed class DdfSystemSpiEndpoint : IDdfSystemDeviceEndpoint
    {
        private readonly DdfSpiProfile profile;
        private readonly SpiDevice device;

        public DdfSystemSpiEndpoint(DdfSpiProfile profile)
        {
            this.profile = profile;
            SpiConnectionSettings settings = new(profile.BusId,
                profile.ChipSelectLine)
            {
                ClockFrequency = profile.ClockFrequency,
                DataBitLength = profile.DataBitLength,
                Mode = (SpiMode)(int)profile.Mode
            };
            device = SpiDevice.Create(settings);
        }

        public event EventHandler<DdfIotFrame> InputChanged { add { } remove { } }

        public DdfIotFrame Read(int maxBytes)
        {
            byte[] result = new byte[Math.Min(maxBytes,
                profile.ReadTransferBytes)];
            device.Read(result);
            return DdfIotFrame.Data(result);
        }

        public void Write(DdfIotFrame frame) => device.Write(
            frame.Bytes.ToArray());

        public DdfIotFrame Exchange(DdfIotFrame frame)
        {
            byte[] request = frame.Bytes.ToArray();
            byte[] result = new byte[request.Length];
            device.TransferFullDuplex(request, result);
            return DdfIotFrame.Data(result);
        }

        public void Dispose() => device.Dispose();
    }

    internal sealed class DdfSystemPwmEndpoint : IDdfSystemDeviceEndpoint
    {
        private readonly PwmChannel channel;

        public DdfSystemPwmEndpoint(DdfPwmProfile profile)
        {
            channel = PwmChannel.Create(profile.Chip, profile.Channel,
                ToProviderFrequency(profile.InitialFrequency),
                profile.InitialDutyCycle);
            channel.Start();
        }

        public event EventHandler<DdfIotFrame> InputChanged { add { } remove { } }
        public DdfIotFrame Read(int maxBytes) => throw new NotSupportedException(
            "PWM non supporta la lettura.");
        public void Write(DdfIotFrame frame)
        {
            channel.Frequency = ToProviderFrequency(frame.Frequency);
            channel.DutyCycle = frame.DutyCycle;
        }
        public DdfIotFrame Exchange(DdfIotFrame frame) => throw new
            NotSupportedException("PWM non supporta lo scambio atomico.");
        public void Dispose()
        {
            channel.Stop();
            channel.Dispose();
        }

        private static int ToProviderFrequency(double frequency)
        {
            if (frequency > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(frequency));
            return checked((int)Math.Round(frequency,
                MidpointRounding.AwayFromZero));
        }
    }
}
