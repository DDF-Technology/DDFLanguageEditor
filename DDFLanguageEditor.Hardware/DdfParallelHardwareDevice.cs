using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Hardware
{
    public enum DdfParallelProviderKind
    {
        WindowsIeee1284,
        VendorSdk,
        Simulated
    }

    public enum DdfParallelMode
    {
        Compatibility,
        Nibble,
        Byte,
        Epp,
        Ecp
    }

    public enum DdfParallelFlow
    {
        Output,
        Bidirectional
    }

    [Flags]
    public enum DdfParallelStatus
    {
        None = 0,
        Busy = 1,
        PaperEmpty = 2,
        Selected = 4,
        Offline = 8,
        PowerOff = 16,
        NotConnected = 32,
        Error = 64
    }

    public sealed class DdfParallelPortProfile
    {
        public DdfParallelPortProfile(string id,
            DdfParallelProviderKind providerKind, DdfParallelMode mode,
            DdfParallelFlow flow, int maxTransferBytes = 4096,
            int? daisyChainAddress = null, bool allowDeviceReset = false,
            string vendorProfileId = null)
        {
            Id = DdfHardwareDeviceIdentity.Identifier(id, nameof(id));
            if (!Enum.IsDefined(typeof(DdfParallelProviderKind), providerKind))
                throw new ArgumentOutOfRangeException(nameof(providerKind));
            if (!Enum.IsDefined(typeof(DdfParallelMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(DdfParallelFlow), flow))
                throw new ArgumentOutOfRangeException(nameof(flow));
            if (maxTransferBytes <= 0 || maxTransferBytes > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxTransferBytes));
            if (daisyChainAddress.HasValue && (daisyChainAddress.Value < 0 ||
                daisyChainAddress.Value > 2))
                throw new ArgumentOutOfRangeException(nameof(daisyChainAddress));
            string vendor = (vendorProfileId ?? string.Empty).Trim();
            if (providerKind == DdfParallelProviderKind.VendorSdk &&
                vendor.Length == 0)
                throw new ArgumentException(
                    "Il provider vendor richiede un profilo SDK nominato.",
                    nameof(vendorProfileId));
            if (providerKind != DdfParallelProviderKind.VendorSdk &&
                vendor.Length != 0)
                throw new ArgumentException(
                    "Il profilo vendor è valido soltanto per VendorSdk.",
                    nameof(vendorProfileId));
            ProviderKind = providerKind;
            Mode = mode;
            Flow = flow;
            MaxTransferBytes = maxTransferBytes;
            DaisyChainAddress = daisyChainAddress;
            AllowDeviceReset = allowDeviceReset;
            VendorProfileId = vendor;
        }

        public string Id { get; }
        public DdfParallelProviderKind ProviderKind { get; }
        public DdfParallelMode Mode { get; }
        public DdfParallelFlow Flow { get; }
        public int MaxTransferBytes { get; }
        public int? DaisyChainAddress { get; }
        public bool AllowDeviceReset { get; }
        public string VendorProfileId { get; }
        public bool CanRead => Flow == DdfParallelFlow.Bidirectional;
    }

    public static class DdfParallelFrameCodec
    {
        public static string Encode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0) throw new ArgumentOutOfRangeException(
                nameof(bytes));
            return "data:" + string.Join(",", bytes.ToArray().Select(item =>
                item.ToString("X2", CultureInfo.InvariantCulture)));
        }

        public static byte[] Decode(string frame)
        {
            string value = (frame ?? string.Empty).Trim();
            if (!value.StartsWith("data:", StringComparison.Ordinal))
                throw new FormatException(
                    "Il frame parallelo deve usare il formato data:XX,YY.");
            string[] values = value.Substring(5).Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (values.Length == 0)
                throw new FormatException("Il frame parallelo è vuoto.");
            try
            {
                return values.Select(item => byte.Parse(item,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture)).ToArray();
            }
            catch (Exception exception) when (exception is FormatException ||
                exception is OverflowException)
            {
                throw new FormatException(
                    "Il payload parallelo non è esadecimale valido.",
                    exception);
            }
        }
    }

    public sealed class DdfParallelStatusChangedEventArgs : EventArgs
    {
        public DdfParallelStatusChangedEventArgs(string logicalId,
            DdfParallelStatus status)
        {
            LogicalId = logicalId ?? string.Empty;
            Status = status;
        }
        public string LogicalId { get; }
        public DdfParallelStatus Status { get; }
    }

    public sealed class DdfParallelFaultEventArgs : EventArgs
    {
        public DdfParallelFaultEventArgs(string logicalId, string code,
            bool disconnected)
        {
            LogicalId = logicalId ?? string.Empty;
            Code = string.IsNullOrWhiteSpace(code) ? "parallel.error" : code;
            Disconnected = disconnected;
        }
        public string LogicalId { get; }
        public string Code { get; }
        public bool Disconnected { get; }
    }

    /// <summary>
    /// Host-owned bridge to a signed Windows driver, a documented system
    /// device interface or an explicitly approved vendor SDK. It deliberately
    /// exposes no I/O address, register or arbitrary control-code operation.
    /// </summary>
    public interface IDdfParallelPortConnection : IAsyncDisposable
    {
        bool IsOpen { get; }
        event EventHandler<DdfParallelStatus> StatusChanged;
        event EventHandler<DdfParallelFaultEventArgs> Faulted;
        ValueTask OpenAsync(CancellationToken cancellationToken);
        ValueTask CloseAsync();
        ValueTask<byte[]> ReadAsync(int maxBytes,
            CancellationToken cancellationToken);
        ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken);
        ValueTask<DdfParallelStatus> QueryStatusAsync(
            CancellationToken cancellationToken);
        ValueTask ApplyProfileAsync(DdfParallelPortProfile profile,
            CancellationToken cancellationToken);
        ValueTask ResetDeviceAsync(CancellationToken cancellationToken);
    }

    public sealed class DdfParallelHardwareDevice : IDdfHardwareDevice
    {
        private readonly IDdfParallelPortConnection connection;
        private readonly IReadOnlyDictionary<string, DdfParallelPortProfile>
            profiles;
        private DdfParallelPortProfile activeProfile;
        private long generation;
        private int available = 1;
        private int disposed;

        public DdfParallelHardwareDevice(DdfHardwareDeviceIdentity identity,
            IEnumerable<DdfParallelPortProfile> profiles,
            string initialProfileId, IDdfParallelPortConnection connection)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (identity.Transport != DdfHardwareTransportKind.Parallel)
                throw new ArgumentException(
                    "L'identità parallela deve usare il trasporto Parallel.",
                    nameof(identity));
            this.connection = connection ?? throw new ArgumentNullException(
                nameof(connection));
            DdfParallelPortProfile[] values = (profiles ??
                Array.Empty<DdfParallelPortProfile>()).ToArray();
            if (values.Length == 0 || values.Any(item => item == null) ||
                values.Select(item => item.Id).Distinct(StringComparer.Ordinal)
                    .Count() != values.Length)
                throw new ArgumentException(
                    "Sono richiesti profili paralleli univoci.",
                    nameof(profiles));
            this.profiles = values.ToDictionary(item => item.Id,
                StringComparer.Ordinal);
            if (!this.profiles.TryGetValue(initialProfileId ?? string.Empty,
                    out activeProfile))
                throw new ArgumentException(
                    "Profilo parallelo iniziale non dichiarato.",
                    nameof(initialProfileId));
            connection.StatusChanged += OnStatusChanged;
            connection.Faulted += OnFaulted;
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public bool IsAvailable => Volatile.Read(ref available) != 0;
        public DdfParallelPortProfile ActiveProfile => activeProfile;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        public event EventHandler<DdfParallelStatusChangedEventArgs>
            StatusChanged;
        public event EventHandler<DdfParallelFaultEventArgs> Faulted;

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsAvailable) throw new InvalidOperationException(
                "Dispositivo parallelo non disponibile.");
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
            if (!activeProfile.CanRead)
                throw new InvalidOperationException(
                    "Il profilo parallelo è solo output.");
            if (maxCharacters <= 0) throw new ArgumentOutOfRangeException(
                nameof(maxCharacters));
            int maxBytes = Math.Min(activeProfile.MaxTransferBytes,
                Math.Max(1, (maxCharacters - 5 + 1) / 3));
            byte[] bytes = await connection.ReadAsync(maxBytes,
                cancellationToken);
            if (bytes == null || bytes.Length == 0 ||
                bytes.Length > activeProfile.MaxTransferBytes)
                throw new InvalidOperationException(
                    "Lettura parallela vuota o oltre quota.");
            string result = DdfParallelFrameCodec.Encode(bytes);
            if (result.Length > maxCharacters)
                throw new InvalidOperationException(
                    "Frame parallelo oltre la quota testuale.");
            return result;
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            byte[] bytes = DdfParallelFrameCodec.Decode(frame);
            if (bytes.Length > activeProfile.MaxTransferBytes)
                throw new InvalidOperationException(
                    "Scrittura parallela oltre quota.");
            await connection.WriteAsync(bytes, cancellationToken);
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!profiles.TryGetValue(profileId ?? string.Empty,
                    out DdfParallelPortProfile profile))
                throw new InvalidOperationException(
                    "Profilo parallelo non autorizzato.");
            DdfParallelPortProfile previous = activeProfile;
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

        public async ValueTask<DdfParallelStatus> QueryStatusAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return await connection.QueryStatusAsync(cancellationToken);
        }

        public async ValueTask ResetDeviceAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!activeProfile.AllowDeviceReset)
                throw new InvalidOperationException(
                    "Reset parallelo non autorizzato dal profilo.");
            await connection.ResetDeviceAsync(cancellationToken);
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

        private void OnStatusChanged(object sender, DdfParallelStatus status)
            => StatusChanged?.Invoke(this,
                new DdfParallelStatusChangedEventArgs(Identity.LogicalId,
                    status));

        private void OnFaulted(object sender, DdfParallelFaultEventArgs args)
        {
            if (args.Disconnected) NotifyAvailability(false);
            Faulted?.Invoke(this, new DdfParallelFaultEventArgs(
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
            connection.StatusChanged -= OnStatusChanged;
            connection.Faulted -= OnFaulted;
            try { await connection.CloseAsync(); }
            catch { }
            await connection.DisposeAsync();
            Interlocked.Exchange(ref available, 0);
        }
    }
}
