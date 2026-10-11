using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Hardware
{
    public enum DdfMidiApiProfile
    {
        WindowsMidiServices,
        LegacyMidi1
    }

    public enum DdfMidiDataFormat
    {
        Ump,
        Midi1ByteStream
    }

    public enum DdfMidiFlow
    {
        Input,
        Output,
        Bidirectional
    }

    /// <summary>
    /// Immutable MIDI packet independent from the Windows WinRT projection.
    /// Timestamp zero means immediate delivery; non-zero values stay in the
    /// clock domain of the selected host provider.
    /// </summary>
    public sealed class DdfMidiPacket
    {
        private DdfMidiPacket(DdfMidiDataFormat format, long timestamp,
            uint[] words, byte[] bytes)
        {
            if (timestamp < 0) throw new ArgumentOutOfRangeException(
                nameof(timestamp));
            Format = format;
            Timestamp = timestamp;
            UmpWords = Array.AsReadOnly(words ?? Array.Empty<uint>());
            Midi1Bytes = Array.AsReadOnly(bytes ?? Array.Empty<byte>());
        }

        public DdfMidiDataFormat Format { get; }
        public long Timestamp { get; }
        public IReadOnlyList<uint> UmpWords { get; }
        public IReadOnlyList<byte> Midi1Bytes { get; }
        public bool IsScheduled => Timestamp != 0;
        public bool IsSystemExclusive => Format == DdfMidiDataFormat.Ump
            ? MessageType == 0x3 || MessageType == 0x5
            : Midi1Bytes.Count >= 2 && Midi1Bytes[0] == 0xF0 &&
              Midi1Bytes[Midi1Bytes.Count - 1] == 0xF7;
        public int MessageType => Format == DdfMidiDataFormat.Ump
            ? (int)(UmpWords[0] >> 28) : -1;
        public int Group => Format == DdfMidiDataFormat.Ump &&
            MessageType != 0x0 && MessageType != 0xF
                ? (int)((UmpWords[0] >> 24) & 0xF) : -1;

        public static DdfMidiPacket Ump(long timestamp,
            params uint[] words)
        {
            uint[] copy = (words ?? Array.Empty<uint>()).ToArray();
            if (copy.Length == 0 || copy.Length > 4)
                throw new ArgumentOutOfRangeException(nameof(words));
            int expected = ExpectedUmpWordCount((int)(copy[0] >> 28));
            if (copy.Length != expected)
                throw new ArgumentException("Lunghezza UMP incoerente con " +
                    "il Message Type.", nameof(words));
            return new DdfMidiPacket(DdfMidiDataFormat.Ump, timestamp, copy,
                null);
        }

        public static DdfMidiPacket Midi1(long timestamp,
            params byte[] bytes)
        {
            byte[] copy = (bytes ?? Array.Empty<byte>()).ToArray();
            if (copy.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));
            bool startsSysEx = copy[0] == 0xF0;
            bool endsSysEx = copy[copy.Length - 1] == 0xF7;
            if (startsSysEx != endsSysEx)
                throw new ArgumentException(
                    "Il frame SysEx MIDI 1.0 deve essere completo.",
                    nameof(bytes));
            if (!startsSysEx && copy.Length > 3)
                throw new ArgumentOutOfRangeException(nameof(bytes));
            return new DdfMidiPacket(DdfMidiDataFormat.Midi1ByteStream,
                timestamp, null, copy);
        }

        public string ToCanonicalString()
        {
            string payload = Format == DdfMidiDataFormat.Ump
                ? string.Join(",", UmpWords.Select(item => item.ToString(
                    "X8", CultureInfo.InvariantCulture)))
                : string.Join(",", Midi1Bytes.Select(item => item.ToString(
                    "X2", CultureInfo.InvariantCulture)));
            return (Format == DdfMidiDataFormat.Ump ? "ump:" : "midi1:") +
                Timestamp.ToString(CultureInfo.InvariantCulture) + ":" +
                payload;
        }

        public static DdfMidiPacket Parse(string text)
        {
            string value = (text ?? string.Empty).Trim();
            string[] sections = value.Split(':');
            if (sections.Length != 3 || !long.TryParse(sections[1],
                    NumberStyles.None, CultureInfo.InvariantCulture,
                    out long timestamp) || timestamp < 0)
                throw new FormatException("Frame MIDI canonico non valido.");
            string[] values = sections[2].Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (sections[0] == "ump")
            {
                try
                {
                    return Ump(timestamp, values.Select(item => uint.Parse(
                        item, NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture)).ToArray());
                }
                catch (Exception exception) when (exception is FormatException ||
                    exception is OverflowException ||
                    exception is ArgumentException)
                {
                    throw new FormatException(
                        "Payload UMP canonico non valido.", exception);
                }
            }
            if (sections[0] == "midi1")
            {
                try
                {
                    return Midi1(timestamp, values.Select(item => byte.Parse(
                        item, NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture)).ToArray());
                }
                catch (Exception exception) when (exception is FormatException ||
                    exception is OverflowException ||
                    exception is ArgumentException)
                {
                    throw new FormatException(
                        "Payload MIDI 1.0 canonico non valido.", exception);
                }
            }
            throw new FormatException("Formato MIDI canonico sconosciuto.");
        }

        private static int ExpectedUmpWordCount(int messageType) =>
            messageType switch
            {
                0x0 or 0x1 or 0x2 or 0x6 or 0x7 => 1,
                0x3 or 0x4 or 0x8 or 0x9 or 0xA => 2,
                0xB or 0xC => 3,
                0x5 or 0xD or 0xE or 0xF => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(messageType))
            };
    }

    public sealed class DdfMidiEndpointProfile
    {
        public DdfMidiEndpointProfile(string id, DdfMidiApiProfile apiProfile,
            DdfMidiDataFormat dataFormat, DdfMidiFlow flow,
            IEnumerable<int> allowedGroups = null,
            bool allowSystemExclusive = false,
            int maxSystemExclusiveBytes = 0,
            bool allowScheduledMessages = false,
            int maxQueuedPackets = 256)
        {
            Id = DdfHardwareDeviceIdentity.Identifier(id, nameof(id));
            if (!Enum.IsDefined(typeof(DdfMidiApiProfile), apiProfile))
                throw new ArgumentOutOfRangeException(nameof(apiProfile));
            if (!Enum.IsDefined(typeof(DdfMidiDataFormat), dataFormat))
                throw new ArgumentOutOfRangeException(nameof(dataFormat));
            if (!Enum.IsDefined(typeof(DdfMidiFlow), flow))
                throw new ArgumentOutOfRangeException(nameof(flow));
            if (apiProfile == DdfMidiApiProfile.WindowsMidiServices &&
                dataFormat != DdfMidiDataFormat.Ump)
                throw new ArgumentException(
                    "Windows MIDI Services usa il contratto UMP.",
                    nameof(dataFormat));
            if (apiProfile == DdfMidiApiProfile.LegacyMidi1 &&
                dataFormat != DdfMidiDataFormat.Midi1ByteStream)
                throw new ArgumentException(
                    "Il profilo legacy accetta soltanto byte MIDI 1.0.",
                    nameof(dataFormat));
            int[] groups = (allowedGroups ?? Enumerable.Range(0, 16))
                .Distinct().OrderBy(item => item).ToArray();
            if (groups.Length == 0 || groups.Any(item => item < 0 || item > 15))
                throw new ArgumentOutOfRangeException(nameof(allowedGroups));
            if (allowSystemExclusive)
            {
                if (maxSystemExclusiveBytes <= 0 ||
                    maxSystemExclusiveBytes > 1024 * 1024)
                    throw new ArgumentOutOfRangeException(
                        nameof(maxSystemExclusiveBytes));
            }
            else if (maxSystemExclusiveBytes != 0)
                throw new ArgumentException(
                    "Una quota SysEx richiede l'abilitazione SysEx.",
                    nameof(maxSystemExclusiveBytes));
            if (maxQueuedPackets <= 0 || maxQueuedPackets > 65536)
                throw new ArgumentOutOfRangeException(nameof(maxQueuedPackets));
            ApiProfile = apiProfile;
            DataFormat = dataFormat;
            Flow = flow;
            AllowedGroups = Array.AsReadOnly(groups);
            AllowSystemExclusive = allowSystemExclusive;
            MaxSystemExclusiveBytes = maxSystemExclusiveBytes;
            AllowScheduledMessages = allowScheduledMessages;
            MaxQueuedPackets = maxQueuedPackets;
        }

        public string Id { get; }
        public DdfMidiApiProfile ApiProfile { get; }
        public DdfMidiDataFormat DataFormat { get; }
        public DdfMidiFlow Flow { get; }
        public IReadOnlyList<int> AllowedGroups { get; }
        public bool AllowSystemExclusive { get; }
        public int MaxSystemExclusiveBytes { get; }
        public bool AllowScheduledMessages { get; }
        public int MaxQueuedPackets { get; }
        public bool CanRead => Flow != DdfMidiFlow.Output;
        public bool CanWrite => Flow != DdfMidiFlow.Input;

        internal void Validate(DdfMidiPacket packet, bool writing)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if (packet.Format != DataFormat)
                throw new InvalidOperationException(
                    "Il frame MIDI non corrisponde al profilo attivo.");
            if (writing ? !CanWrite : !CanRead)
                throw new InvalidOperationException(
                    "La direzione MIDI non è autorizzata dal profilo.");
            if (writing && packet.IsScheduled && !AllowScheduledMessages)
                throw new InvalidOperationException(
                    "I timestamp pianificati non sono autorizzati.");
            if (packet.Group >= 0 && !AllowedGroups.Contains(packet.Group))
                throw new InvalidOperationException(
                    "Il gruppo UMP non è autorizzato.");
            if (packet.IsSystemExclusive)
            {
                if (!AllowSystemExclusive)
                    throw new InvalidOperationException(
                        "SysEx non è autorizzato.");
                int size = packet.Format == DdfMidiDataFormat.Ump
                    ? packet.UmpWords.Count * sizeof(uint)
                    : packet.Midi1Bytes.Count;
                if (size > MaxSystemExclusiveBytes)
                    throw new InvalidOperationException(
                        "SysEx supera la quota del profilo.");
            }
        }
    }

    public sealed class DdfMidiPacketAvailableEventArgs : EventArgs
    {
        public DdfMidiPacketAvailableEventArgs(string logicalId,
            int queuedPackets)
        {
            LogicalId = logicalId ?? string.Empty;
            QueuedPackets = Math.Max(0, queuedPackets);
        }
        public string LogicalId { get; }
        public int QueuedPackets { get; }
    }

    public sealed class DdfMidiFaultEventArgs : EventArgs
    {
        public DdfMidiFaultEventArgs(string logicalId, string code,
            bool disconnected)
        {
            LogicalId = logicalId ?? string.Empty;
            Code = string.IsNullOrWhiteSpace(code) ? "midi.error" : code;
            Disconnected = disconnected;
        }
        public string LogicalId { get; }
        public string Code { get; }
        public bool Disconnected { get; }
    }

    /// <summary>
    /// Host-side seam for Windows MIDI Services or the isolated legacy path.
    /// Endpoint enumeration and physical identifiers remain outside DDF.
    /// </summary>
    public interface IDdfMidiEndpointConnection : IAsyncDisposable
    {
        bool IsOpen { get; }
        event EventHandler<int> PacketAvailable;
        event EventHandler<DdfMidiFaultEventArgs> Faulted;
        ValueTask OpenAsync(CancellationToken cancellationToken);
        ValueTask CloseAsync();
        ValueTask<DdfMidiPacket> ReadAsync(CancellationToken cancellationToken);
        ValueTask SendAsync(DdfMidiPacket packet,
            CancellationToken cancellationToken);
        ValueTask ApplyProfileAsync(DdfMidiEndpointProfile profile,
            CancellationToken cancellationToken);
    }

    public sealed class DdfMidiHardwareDevice : IDdfHardwareDevice
    {
        private readonly IDdfMidiEndpointConnection connection;
        private readonly IReadOnlyDictionary<string, DdfMidiEndpointProfile>
            profiles;
        private DdfMidiEndpointProfile activeProfile;
        private long generation;
        private int available = 1;
        private int disposed;

        public DdfMidiHardwareDevice(DdfHardwareDeviceIdentity identity,
            IEnumerable<DdfMidiEndpointProfile> profiles,
            string initialProfileId, IDdfMidiEndpointConnection connection)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (identity.Transport != DdfHardwareTransportKind.Midi)
                throw new ArgumentException(
                    "L'identità MIDI deve usare il trasporto Midi.",
                    nameof(identity));
            this.connection = connection ?? throw new ArgumentNullException(
                nameof(connection));
            DdfMidiEndpointProfile[] values = (profiles ??
                Array.Empty<DdfMidiEndpointProfile>()).ToArray();
            if (values.Length == 0 || values.Any(item => item == null) ||
                values.Select(item => item.Id).Distinct(StringComparer.Ordinal)
                    .Count() != values.Length)
                throw new ArgumentException(
                    "Sono richiesti profili MIDI univoci.", nameof(profiles));
            this.profiles = values.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            if (!this.profiles.TryGetValue(initialProfileId ?? string.Empty,
                    out activeProfile))
                throw new ArgumentException(
                    "Profilo MIDI iniziale non dichiarato.",
                    nameof(initialProfileId));
            connection.PacketAvailable += OnPacketAvailable;
            connection.Faulted += OnFaulted;
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public bool IsAvailable => Volatile.Read(ref available) != 0;
        public DdfMidiEndpointProfile ActiveProfile => activeProfile;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        public event EventHandler<DdfMidiPacketAvailableEventArgs>
            PacketAvailable;
        public event EventHandler<DdfMidiFaultEventArgs> Faulted;

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsAvailable) throw new InvalidOperationException(
                "Endpoint MIDI non disponibile.");
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
                if (opened)
                {
                    try { await connection.CloseAsync(); }
                    catch { }
                }
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
            if (maxCharacters <= 0) throw new ArgumentOutOfRangeException(
                nameof(maxCharacters));
            DdfMidiPacket packet = await connection.ReadAsync(
                cancellationToken);
            activeProfile.Validate(packet, false);
            string result = packet.ToCanonicalString();
            if (result.Length > maxCharacters)
                throw new InvalidOperationException(
                    "Frame MIDI oltre la quota testuale.");
            return result;
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            DdfMidiPacket packet = DdfMidiPacket.Parse(frame);
            activeProfile.Validate(packet, true);
            await connection.SendAsync(packet, cancellationToken);
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!profiles.TryGetValue(profileId ?? string.Empty,
                    out DdfMidiEndpointProfile profile))
                throw new InvalidOperationException(
                    "Profilo MIDI non autorizzato.");
            DdfMidiEndpointProfile previous = activeProfile;
            try
            {
                await connection.ApplyProfileAsync(profile,
                    cancellationToken);
                activeProfile = profile;
            }
            catch
            {
                try
                {
                    await connection.ApplyProfileAsync(previous,
                        CancellationToken.None);
                }
                catch { }
                throw;
            }
        }

        public void NotifyAvailability(bool value)
        {
            ThrowIfDisposed();
            int next = value ? 1 : 0;
            if (Interlocked.Exchange(ref available, next) == next) return;
            AvailabilityChanged?.Invoke(this,
                new DdfHardwareAvailabilityChangedEventArgs(
                    Identity.LogicalId, value,
                    Interlocked.Increment(ref generation)));
        }

        private void OnPacketAvailable(object sender, int queuedPackets)
        {
            int bounded = Math.Min(Math.Max(0, queuedPackets),
                activeProfile.MaxQueuedPackets);
            PacketAvailable?.Invoke(this,
                new DdfMidiPacketAvailableEventArgs(Identity.LogicalId,
                    bounded));
        }

        private void OnFaulted(object sender, DdfMidiFaultEventArgs args)
        {
            if (args.Disconnected) NotifyAvailability(false);
            Faulted?.Invoke(this, new DdfMidiFaultEventArgs(
                Identity.LogicalId, args.Code, args.Disconnected));
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            connection.PacketAvailable -= OnPacketAvailable;
            connection.Faulted -= OnFaulted;
            try { await connection.CloseAsync(); }
            catch { }
            await connection.DisposeAsync();
            Interlocked.Exchange(ref available, 0);
        }
    }
}
