using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Hardware
{
    public enum DdfSerialParity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    public enum DdfSerialStopBits
    {
        One,
        OnePointFive,
        Two
    }

    public enum DdfSerialHandshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }

    public enum DdfSerialFrameFormat
    {
        Line,
        RawUtf8Chunk
    }

    public enum DdfDifferentialSerialMode
    {
        Rs422FullDuplex,
        Rs485HalfDuplex,
        Rs485FullDuplex
    }

    public enum DdfSerialDirectionControl
    {
        None,
        DriverManaged,
        RtsHighWhileTransmitting,
        RtsLowWhileTransmitting,
        VendorAdapter
    }

    public sealed class DdfDifferentialSerialProfile
    {
        public DdfDifferentialSerialProfile(string id,
            string communicationProfileId, DdfDifferentialSerialMode mode,
            DdfSerialDirectionControl directionControl,
            TimeSpan? preTransmitDelay = null,
            TimeSpan? postTransmitDelay = null,
            string vendorProfileId = null)
        {
            Id = DdfHardwareDeviceIdentity.Identifier(id, nameof(id));
            CommunicationProfileId = DdfHardwareDeviceIdentity.Identifier(
                communicationProfileId, nameof(communicationProfileId));
            if (!Enum.IsDefined(typeof(DdfDifferentialSerialMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(DdfSerialDirectionControl),
                    directionControl))
                throw new ArgumentOutOfRangeException(nameof(directionControl));
            if (mode == DdfDifferentialSerialMode.Rs485HalfDuplex &&
                directionControl == DdfSerialDirectionControl.None)
                throw new ArgumentException(
                    "RS485 half-duplex richiede un controllo direzione esplicito.",
                    nameof(directionControl));
            if (mode != DdfDifferentialSerialMode.Rs485HalfDuplex &&
                (directionControl ==
                     DdfSerialDirectionControl.RtsHighWhileTransmitting ||
                 directionControl ==
                     DdfSerialDirectionControl.RtsLowWhileTransmitting))
                throw new ArgumentException(
                    "Il controllo RTS manuale è valido soltanto per RS485 half-duplex.",
                    nameof(directionControl));
            string vendor = (vendorProfileId ?? string.Empty).Trim();
            if (directionControl == DdfSerialDirectionControl.VendorAdapter &&
                vendor.Length == 0)
                throw new ArgumentException(
                    "Il controllo vendor richiede un profilo adapter.",
                    nameof(vendorProfileId));
            if (directionControl != DdfSerialDirectionControl.VendorAdapter &&
                vendor.Length != 0)
                throw new ArgumentException(
                    "Il profilo vendor è valido soltanto per un adapter vendor.",
                    nameof(vendorProfileId));
            TimeSpan before = preTransmitDelay ?? TimeSpan.Zero;
            TimeSpan after = postTransmitDelay ?? TimeSpan.Zero;
            if (before < TimeSpan.Zero || before > TimeSpan.FromSeconds(1))
                throw new ArgumentOutOfRangeException(nameof(preTransmitDelay));
            if (after < TimeSpan.Zero || after > TimeSpan.FromSeconds(1))
                throw new ArgumentOutOfRangeException(nameof(postTransmitDelay));
            Mode = mode;
            DirectionControl = directionControl;
            PreTransmitDelay = before;
            PostTransmitDelay = after;
            VendorProfileId = vendor;
        }

        public string Id { get; }
        public string CommunicationProfileId { get; }
        public DdfDifferentialSerialMode Mode { get; }
        public DdfSerialDirectionControl DirectionControl { get; }
        public TimeSpan PreTransmitDelay { get; }
        public TimeSpan PostTransmitDelay { get; }
        public string VendorProfileId { get; }
        public bool RequiresTransmitDirection => Mode ==
            DdfDifferentialSerialMode.Rs485HalfDuplex;
    }

    public sealed class DdfSerialCommunicationProfile
    {
        public DdfSerialCommunicationProfile(string id, int baudRate,
            int dataBits = 8, DdfSerialParity parity = DdfSerialParity.None,
            DdfSerialStopBits stopBits = DdfSerialStopBits.One,
            DdfSerialHandshake handshake = DdfSerialHandshake.None,
            bool dtrEnable = false, bool rtsEnable = false)
        {
            Id = DdfHardwareDeviceIdentity.Identifier(id, nameof(id));
            if (baudRate <= 0 || baudRate > 4_000_000)
                throw new ArgumentOutOfRangeException(nameof(baudRate));
            if (dataBits < 5 || dataBits > 8)
                throw new ArgumentOutOfRangeException(nameof(dataBits));
            if (!Enum.IsDefined(typeof(DdfSerialParity), parity))
                throw new ArgumentOutOfRangeException(nameof(parity));
            if (!Enum.IsDefined(typeof(DdfSerialStopBits), stopBits))
                throw new ArgumentOutOfRangeException(nameof(stopBits));
            if (!Enum.IsDefined(typeof(DdfSerialHandshake), handshake))
                throw new ArgumentOutOfRangeException(nameof(handshake));
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            Handshake = handshake;
            DtrEnable = dtrEnable;
            RtsEnable = rtsEnable;
        }

        public string Id { get; }
        public int BaudRate { get; }
        public int DataBits { get; }
        public DdfSerialParity Parity { get; }
        public DdfSerialStopBits StopBits { get; }
        public DdfSerialHandshake Handshake { get; }
        public bool DtrEnable { get; }
        public bool RtsEnable { get; }
    }

    public sealed class DdfSerialEndpointOptions
    {
        public DdfSerialEndpointOptions(string portName,
            DdfSerialCommunicationProfile initialProfile,
            IEnumerable<DdfSerialCommunicationProfile> profiles = null,
            DdfSerialFrameFormat frameFormat = DdfSerialFrameFormat.Line,
            string delimiter = "\n", bool appendDelimiterOnWrite = true,
            int readBufferSize = 4096, int writeBufferSize = 2048,
            int maxFrameBytes = 64 * 1024)
        {
            PortName = Required(portName, nameof(portName));
            InitialProfile = initialProfile ?? throw new ArgumentNullException(
                nameof(initialProfile));
            if (!Enum.IsDefined(typeof(DdfSerialFrameFormat), frameFormat))
                throw new ArgumentOutOfRangeException(nameof(frameFormat));
            DdfSerialCommunicationProfile[] profileArray = (profiles ??
                Array.Empty<DdfSerialCommunicationProfile>())
                .Prepend(initialProfile).ToArray();
            if (profileArray.Any(item => item == null) || profileArray
                    .Select(item => item.Id).Distinct(StringComparer.Ordinal)
                    .Count() != profileArray.Length)
                throw new ArgumentException(
                    "Profili seriali nulli o duplicati.", nameof(profiles));
            if (frameFormat == DdfSerialFrameFormat.Line &&
                string.IsNullOrEmpty(delimiter))
                throw new ArgumentException(
                    "Il framing line richiede un delimitatore.",
                    nameof(delimiter));
            byte[] delimiterBytes = StrictUtf8.GetBytes(delimiter ?? string.Empty);
            if (delimiterBytes.Length > 16)
                throw new ArgumentOutOfRangeException(nameof(delimiter));
            if (readBufferSize < 128 || readBufferSize > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(readBufferSize));
            if (writeBufferSize < 128 || writeBufferSize > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(writeBufferSize));
            if (maxFrameBytes <= 0 || maxFrameBytes > 4 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxFrameBytes));
            Profiles = profileArray.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            FrameFormat = frameFormat;
            Delimiter = delimiter ?? string.Empty;
            DelimiterBytes = delimiterBytes;
            AppendDelimiterOnWrite = appendDelimiterOnWrite;
            ReadBufferSize = readBufferSize;
            WriteBufferSize = writeBufferSize;
            MaxFrameBytes = maxFrameBytes;
        }

        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        public string PortName { get; }
        public DdfSerialCommunicationProfile InitialProfile { get; }
        public IReadOnlyDictionary<string, DdfSerialCommunicationProfile>
            Profiles { get; }
        public DdfSerialFrameFormat FrameFormat { get; }
        public string Delimiter { get; }
        internal byte[] DelimiterBytes { get; }
        public bool AppendDelimiterOnWrite { get; }
        public int ReadBufferSize { get; }
        public int WriteBufferSize { get; }
        public int MaxFrameBytes { get; }

        private static string Required(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0)
                throw new ArgumentException("Valore seriale obbligatorio.",
                    parameterName);
            return result;
        }
    }

    public sealed class DdfSerialDataAvailableEventArgs : EventArgs
    {
        public DdfSerialDataAvailableEventArgs(string logicalId,
            int bytesAvailable)
        {
            LogicalId = logicalId;
            BytesAvailable = Math.Max(0, bytesAvailable);
        }
        public string LogicalId { get; }
        public int BytesAvailable { get; }
    }

    public sealed class DdfSerialFaultEventArgs : EventArgs
    {
        public DdfSerialFaultEventArgs(string logicalId, string code,
            bool disconnected)
        {
            LogicalId = logicalId;
            Code = code ?? "serial.error";
            Disconnected = disconnected;
        }
        public string LogicalId { get; }
        public string Code { get; }
        public bool Disconnected { get; }
    }

    public interface IDdfSerialPortConnection : IAsyncDisposable
    {
        bool IsOpen { get; }
        event EventHandler<int> DataAvailable;
        event EventHandler<DdfSerialFaultEventArgs> Faulted;
        ValueTask OpenAsync(CancellationToken cancellationToken);
        ValueTask CloseAsync();
        ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken);
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken);
        ValueTask ApplyProfileAsync(DdfSerialCommunicationProfile profile,
            CancellationToken cancellationToken);
    }

    public interface IDdfDifferentialSerialPortConnection :
        IDdfSerialPortConnection
    {
        ValueTask ApplyDifferentialProfileAsync(
            DdfDifferentialSerialProfile profile,
            CancellationToken cancellationToken);
        ValueTask SetTransmitDirectionAsync(bool transmitting,
            DdfDifferentialSerialProfile profile,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Explicit System.IO.Ports connection for one host-configured endpoint.
    /// It never calls SerialPort.GetPortNames and never performs discovery.
    /// </summary>
    public sealed class DdfSystemSerialPortConnection :
        IDdfDifferentialSerialPortConnection
    {
        private readonly SerialPort port;
        private int disposed;

        public DdfSystemSerialPortConnection(DdfSerialEndpointOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            port = new SerialPort
            {
                PortName = options.PortName,
                ReadBufferSize = options.ReadBufferSize,
                WriteBufferSize = options.WriteBufferSize,
                Encoding = new UTF8Encoding(false, true)
            };
            Apply(options.InitialProfile);
            port.DataReceived += OnDataReceived;
            port.ErrorReceived += OnErrorReceived;
        }

        public DdfSerialEndpointOptions Options { get; }
        public bool IsOpen => Volatile.Read(ref disposed) == 0 && port.IsOpen;
        public event EventHandler<int> DataAvailable;
        public event EventHandler<DdfSerialFaultEventArgs> Faulted;

        public ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (!port.IsOpen) port.Open();
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync()
        {
            if (Volatile.Read(ref disposed) == 0 && port.IsOpen) port.Close();
            return ValueTask.CompletedTask;
        }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!port.IsOpen) throw new InvalidOperationException(
                "Endpoint seriale non aperto.");
            return await port.BaseStream.ReadAsync(buffer, cancellationToken);
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!port.IsOpen) throw new InvalidOperationException(
                "Endpoint seriale non aperto.");
            await port.BaseStream.WriteAsync(buffer, cancellationToken);
            await port.BaseStream.FlushAsync(cancellationToken);
        }

        public ValueTask ApplyProfileAsync(
            DdfSerialCommunicationProfile profile,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            Apply(profile ?? throw new ArgumentNullException(nameof(profile)));
            return ValueTask.CompletedTask;
        }

        public ValueTask ApplyDifferentialProfileAsync(
            DdfDifferentialSerialProfile profile,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.DirectionControl ==
                DdfSerialDirectionControl.VendorAdapter)
                throw new InvalidOperationException(
                    "Il provider System.IO.Ports non implementa profili vendor.");
            SetDirection(false, profile);
            return ValueTask.CompletedTask;
        }

        public ValueTask SetTransmitDirectionAsync(bool transmitting,
            DdfDifferentialSerialProfile profile,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.DirectionControl ==
                DdfSerialDirectionControl.VendorAdapter)
                throw new InvalidOperationException(
                    "Il provider System.IO.Ports non implementa profili vendor.");
            SetDirection(transmitting, profile);
            return ValueTask.CompletedTask;
        }

        private void SetDirection(bool transmitting,
            DdfDifferentialSerialProfile profile)
        {
            switch (profile.DirectionControl)
            {
                case DdfSerialDirectionControl.None:
                case DdfSerialDirectionControl.DriverManaged:
                    return;
                case DdfSerialDirectionControl.RtsHighWhileTransmitting:
                    port.RtsEnable = transmitting;
                    return;
                case DdfSerialDirectionControl.RtsLowWhileTransmitting:
                    port.RtsEnable = !transmitting;
                    return;
                default:
                    throw new InvalidOperationException(
                        "Controllo direzione seriale non supportato.");
            }
        }

        private void Apply(DdfSerialCommunicationProfile profile)
        {
            port.BaudRate = profile.BaudRate;
            port.DataBits = profile.DataBits;
            port.Parity = profile.Parity switch
            {
                DdfSerialParity.None => Parity.None,
                DdfSerialParity.Odd => Parity.Odd,
                DdfSerialParity.Even => Parity.Even,
                DdfSerialParity.Mark => Parity.Mark,
                DdfSerialParity.Space => Parity.Space,
                _ => throw new ArgumentOutOfRangeException(nameof(profile))
            };
            port.StopBits = profile.StopBits switch
            {
                DdfSerialStopBits.One => StopBits.One,
                DdfSerialStopBits.OnePointFive => StopBits.OnePointFive,
                DdfSerialStopBits.Two => StopBits.Two,
                _ => throw new ArgumentOutOfRangeException(nameof(profile))
            };
            port.Handshake = profile.Handshake switch
            {
                DdfSerialHandshake.None => Handshake.None,
                DdfSerialHandshake.XOnXOff => Handshake.XOnXOff,
                DdfSerialHandshake.RequestToSend => Handshake.RequestToSend,
                DdfSerialHandshake.RequestToSendXOnXOff =>
                    Handshake.RequestToSendXOnXOff,
                _ => throw new ArgumentOutOfRangeException(nameof(profile))
            };
            port.DtrEnable = profile.DtrEnable;
            port.RtsEnable = profile.RtsEnable;
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytes;
            try { bytes = port.BytesToRead; }
            catch { bytes = 0; }
            DataAvailable?.Invoke(this, bytes);
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
            => Faulted?.Invoke(this, new DdfSerialFaultEventArgs(string.Empty,
                "serial.driver." + e.EventType.ToString().ToLowerInvariant(),
                false));

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                port.DataReceived -= OnDataReceived;
                port.ErrorReceived -= OnErrorReceived;
                if (port.IsOpen) port.Close();
                port.Dispose();
            }
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Adapts one explicit serial connection to the governed hardware contract.
    /// Port name and communication settings remain host-side.
    /// </summary>
    public sealed class DdfSerialHardwareDevice : IDdfHardwareDevice
    {
        private sealed class SerialFrameException : InvalidOperationException
        {
            internal SerialFrameException(string message) : base(message) { }
        }

        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly IDdfSerialPortConnection connection;
        private readonly DdfSerialEndpointOptions options;
        private readonly List<byte> pending = new();
        private int available = 1;
        private int disposed;
        private long generation;

        public DdfSerialHardwareDevice(DdfHardwareDeviceIdentity identity,
            DdfSerialEndpointOptions options,
            IDdfSerialPortConnection connection = null)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (identity.Transport != DdfHardwareTransportKind.Serial)
                throw new ArgumentException(
                    "L'identità del provider deve essere seriale.",
                    nameof(identity));
            this.options = options ?? throw new ArgumentNullException(
                nameof(options));
            this.connection = connection ??
                new DdfSystemSerialPortConnection(options);
            this.connection.DataAvailable += OnDataAvailable;
            this.connection.Faulted += OnFaulted;
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public bool IsAvailable => Volatile.Read(ref available) != 0 &&
            Volatile.Read(ref disposed) == 0;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        public event EventHandler<DdfSerialDataAvailableEventArgs> DataAvailable;
        public event EventHandler<DdfSerialFaultEventArgs> Faulted;

        public void NotifyAvailability(bool value)
        {
            ThrowIfDisposed();
            SetAvailability(value);
        }

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                await connection.ApplyProfileAsync(options.InitialProfile,
                    cancellationToken);
                await connection.OpenAsync(cancellationToken);
                SetAvailability(true);
            }
            catch (Exception exception) when (IsEndpointFailure(exception))
            {
                SetAvailability(false);
                throw new InvalidOperationException(
                    "Impossibile aprire l'endpoint seriale autorizzato.");
            }
        }

        public async ValueTask CloseAsync()
        {
            pending.Clear();
            try { await connection.CloseAsync(); }
            catch (Exception exception) when (IsEndpointFailure(exception))
            {
                SetAvailability(false);
                throw new InvalidOperationException(
                    "Impossibile chiudere l'endpoint seriale autorizzato.");
            }
        }

        public async ValueTask<string> ReadAsync(int maxCharacters,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            int maxBytes = Math.Min(options.MaxFrameBytes,
                checked(maxCharacters * 4));
            byte[] payload;
            try
            {
                payload = options.FrameFormat ==
                    DdfSerialFrameFormat.Line
                    ? await ReadLineAsync(maxBytes, cancellationToken)
                    : await ReadChunkAsync(maxBytes, cancellationToken);
            }
            catch (Exception exception) when (IsEndpointFailure(exception))
            {
                SetAvailability(false);
                throw new InvalidOperationException(
                    "Lettura dall'endpoint seriale non riuscita.");
            }
            try
            {
                string result = StrictUtf8.GetString(payload);
                if (result.Length > maxCharacters)
                    throw new SerialFrameException(
                        "Frame seriale oltre la quota autorizzata.");
                return result;
            }
            catch (DecoderFallbackException)
            {
                throw new SerialFrameException(
                    "Il frame seriale non è UTF-8 valido.");
            }
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            byte[] payload;
            try
            {
                payload = StrictUtf8.GetBytes(frame ?? string.Empty);
            }
            catch (EncoderFallbackException)
            {
                throw new InvalidOperationException(
                    "Il frame contiene testo UTF-8 non valido.");
            }
            if (options.AppendDelimiterOnWrite &&
                options.DelimiterBytes.Length != 0)
                payload = payload.Concat(options.DelimiterBytes).ToArray();
            if (payload.Length > options.MaxFrameBytes)
                throw new InvalidOperationException(
                    "Frame seriale oltre la quota autorizzata.");
            try { await connection.WriteAsync(payload, cancellationToken); }
            catch (Exception exception) when (IsEndpointFailure(exception))
            {
                SetAvailability(false);
                throw new InvalidOperationException(
                    "Scrittura sull'endpoint seriale non riuscita.");
            }
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!options.Profiles.TryGetValue(profileId ?? string.Empty,
                    out DdfSerialCommunicationProfile profile))
                throw new InvalidOperationException(
                    "Profilo seriale non autorizzato.");
            try { await connection.ApplyProfileAsync(profile, cancellationToken); }
            catch (Exception exception) when (IsEndpointFailure(exception))
            {
                SetAvailability(false);
                throw new InvalidOperationException(
                    "Configurazione dell'endpoint seriale non riuscita.");
            }
        }

        private async ValueTask<byte[]> ReadLineAsync(int maxBytes,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                int delimiter = FindDelimiter(pending, options.DelimiterBytes);
                if (delimiter >= 0)
                {
                    byte[] result = pending.Take(delimiter).ToArray();
                    pending.RemoveRange(0, delimiter +
                        options.DelimiterBytes.Length);
                    return result;
                }
                if (pending.Count >= maxBytes)
                    throw new SerialFrameException(
                        "Frame seriale oltre la quota autorizzata.");
                byte[] buffer = new byte[Math.Min(256,
                    maxBytes - pending.Count)];
                int read = await connection.ReadAsync(buffer,
                    cancellationToken);
                if (read <= 0) throw new IOException(
                    "Endpoint seriale chiuso durante la lettura.");
                pending.AddRange(buffer.Take(read));
            }
        }

        private async ValueTask<byte[]> ReadChunkAsync(int maxBytes,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[Math.Min(4096, maxBytes)];
            int read = await connection.ReadAsync(buffer, cancellationToken);
            if (read <= 0) throw new IOException(
                "Endpoint seriale chiuso durante la lettura.");
            return buffer.Take(read).ToArray();
        }

        private static int FindDelimiter(IReadOnlyList<byte> source,
            IReadOnlyList<byte> delimiter)
        {
            for (int start = 0; start <= source.Count - delimiter.Count;
                 start++)
            {
                bool match = true;
                for (int index = 0; index < delimiter.Count; index++)
                    if (source[start + index] != delimiter[index])
                    {
                        match = false;
                        break;
                    }
                if (match) return start;
            }
            return -1;
        }

        private void OnDataAvailable(object sender, int bytes) =>
            DataAvailable?.Invoke(this, new DdfSerialDataAvailableEventArgs(
                Identity.LogicalId, bytes));

        private void OnFaulted(object sender, DdfSerialFaultEventArgs args)
        {
            if (args.Disconnected) SetAvailability(false);
            Faulted?.Invoke(this, new DdfSerialFaultEventArgs(
                Identity.LogicalId, args.Code, args.Disconnected));
        }

        private void SetAvailability(bool value)
        {
            int next = value ? 1 : 0;
            if (Interlocked.Exchange(ref available, next) == next) return;
            AvailabilityChanged?.Invoke(this,
                new DdfHardwareAvailabilityChangedEventArgs(
                    Identity.LogicalId, value,
                    Interlocked.Increment(ref generation)));
        }

        private static bool IsEndpointFailure(Exception exception) =>
            exception is not SerialFrameException &&
            (exception is IOException ||
             exception is UnauthorizedAccessException ||
             exception is InvalidOperationException ||
             exception is ObjectDisposedException);

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            connection.DataAvailable -= OnDataAvailable;
            connection.Faulted -= OnFaulted;
            try { await connection.CloseAsync(); }
            catch { }
            await connection.DisposeAsync();
            pending.Clear();
            Interlocked.Exchange(ref available, 0);
        }
    }

    /// <summary>
    /// Adds explicit RS422/RS485 semantics to one serial endpoint. Manual RTS
    /// direction is allowed only for RS485 half-duplex; vendor control requires
    /// a provider implementing the differential connection contract.
    /// </summary>
    public sealed class DdfDifferentialSerialHardwareDevice :
        IDdfHardwareDevice
    {
        private readonly DdfSerialHardwareDevice serial;
        private readonly IDdfDifferentialSerialPortConnection connection;
        private readonly IReadOnlyDictionary<string,
            DdfDifferentialSerialProfile> profiles;
        private DdfDifferentialSerialProfile activeProfile;
        private int disposed;

        public DdfDifferentialSerialHardwareDevice(
            DdfHardwareDeviceIdentity identity,
            DdfSerialEndpointOptions serialOptions,
            IEnumerable<DdfDifferentialSerialProfile> profiles,
            string initialProfileId,
            IDdfDifferentialSerialPortConnection connection = null)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (identity.Transport != DdfHardwareTransportKind.Serial)
                throw new ArgumentException(
                    "L'identità differenziale deve usare il trasporto seriale.",
                    nameof(identity));
            if (serialOptions == null) throw new ArgumentNullException(
                nameof(serialOptions));
            DdfDifferentialSerialProfile[] profileArray = (profiles ??
                Array.Empty<DdfDifferentialSerialProfile>()).ToArray();
            if (profileArray.Length == 0 || profileArray.Any(item => item == null) ||
                profileArray.Select(item => item.Id).Distinct(
                    StringComparer.Ordinal).Count() != profileArray.Length)
                throw new ArgumentException(
                    "È richiesto almeno un profilo differenziale univoco.",
                    nameof(profiles));
            foreach (DdfDifferentialSerialProfile profile in profileArray)
                if (!serialOptions.Profiles.ContainsKey(
                        profile.CommunicationProfileId))
                    throw new ArgumentException(
                        "Il profilo seriale di base non è dichiarato: " +
                        profile.CommunicationProfileId + ".", nameof(profiles));
            this.profiles = profileArray.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            if (!this.profiles.TryGetValue(initialProfileId ?? string.Empty,
                    out activeProfile))
                throw new ArgumentException(
                    "Profilo differenziale iniziale non dichiarato.",
                    nameof(initialProfileId));
            this.connection = connection ??
                new DdfSystemSerialPortConnection(serialOptions);
            serial = new DdfSerialHardwareDevice(identity, serialOptions,
                this.connection);
            serial.AvailabilityChanged += ForwardAvailability;
            serial.DataAvailable += ForwardData;
            serial.Faulted += ForwardFault;
        }

        public DdfHardwareDeviceIdentity Identity => serial.Identity;
        public bool IsAvailable => serial.IsAvailable;
        public DdfDifferentialSerialProfile ActiveProfile => activeProfile;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        public event EventHandler<DdfSerialDataAvailableEventArgs> DataAvailable;
        public event EventHandler<DdfSerialFaultEventArgs> Faulted;

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            bool opened = false;
            try
            {
                await serial.OpenAsync(cancellationToken);
                opened = true;
                await ApplyProfileCoreAsync(activeProfile,
                    cancellationToken);
            }
            catch
            {
                if (opened)
                {
                    try { await serial.CloseAsync(); }
                    catch { }
                }
                throw;
            }
        }

        public ValueTask CloseAsync()
        {
            ThrowIfDisposed();
            return serial.CloseAsync();
        }

        public async ValueTask<string> ReadAsync(int maxCharacters,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (activeProfile.RequiresTransmitDirection)
                await connection.SetTransmitDirectionAsync(false,
                    activeProfile, cancellationToken);
            return await serial.ReadAsync(maxCharacters, cancellationToken);
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!activeProfile.RequiresTransmitDirection)
            {
                await serial.WriteAsync(frame, cancellationToken);
                return;
            }
            bool directionAsserted = false;
            try
            {
                await connection.SetTransmitDirectionAsync(true,
                    activeProfile, cancellationToken);
                directionAsserted = true;
                if (activeProfile.PreTransmitDelay > TimeSpan.Zero)
                    await Task.Delay(activeProfile.PreTransmitDelay,
                        cancellationToken);
                await serial.WriteAsync(frame, cancellationToken);
                if (activeProfile.PostTransmitDelay > TimeSpan.Zero)
                    await Task.Delay(activeProfile.PostTransmitDelay,
                        cancellationToken);
            }
            finally
            {
                if (directionAsserted)
                    await connection.SetTransmitDirectionAsync(false,
                        activeProfile, CancellationToken.None);
            }
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!profiles.TryGetValue(profileId ?? string.Empty,
                    out DdfDifferentialSerialProfile profile))
                throw new InvalidOperationException(
                    "Profilo differenziale non autorizzato.");
            DdfDifferentialSerialProfile previous = activeProfile;
            try
            {
                await ApplyProfileCoreAsync(profile, cancellationToken);
                activeProfile = profile;
            }
            catch
            {
                try
                {
                    await ApplyProfileCoreAsync(previous,
                        CancellationToken.None);
                }
                catch { }
                throw;
            }
        }

        private async ValueTask ApplyProfileCoreAsync(
            DdfDifferentialSerialProfile profile,
            CancellationToken cancellationToken)
        {
            await serial.ApplyProfileAsync(profile.CommunicationProfileId,
                cancellationToken);
            await connection.ApplyDifferentialProfileAsync(profile,
                cancellationToken);
        }

        private void ForwardAvailability(object sender,
            DdfHardwareAvailabilityChangedEventArgs args) =>
            AvailabilityChanged?.Invoke(this, args);
        private void ForwardData(object sender,
            DdfSerialDataAvailableEventArgs args) => DataAvailable?.Invoke(
                this, args);
        private void ForwardFault(object sender, DdfSerialFaultEventArgs args)
            => Faulted?.Invoke(this, args);

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            serial.AvailabilityChanged -= ForwardAvailability;
            serial.DataAvailable -= ForwardData;
            serial.Faulted -= ForwardFault;
            await serial.DisposeAsync();
        }
    }
}
