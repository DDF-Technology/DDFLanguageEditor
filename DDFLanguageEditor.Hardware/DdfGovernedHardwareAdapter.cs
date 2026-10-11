using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Hardware
{
    public enum DdfHardwareTransportKind
    {
        Serial,
        Midi,
        Parallel,
        Gpio,
        I2c,
        Spi,
        Pwm,
        UsbHid,
        Can,
        Custom
    }

    public enum DdfHardwareAccessMode
    {
        Exclusive,
        Shared
    }

    [Flags]
    public enum DdfHardwarePermission
    {
        None = 0,
        Probe = 1,
        Read = 2,
        Write = 4,
        Configure = 8
    }

    public enum DdfHardwareOperationKind
    {
        IsAvailable,
        Read,
        Write,
        Exchange,
        ApplyProfile
    }

    public sealed class DdfHardwareDeviceIdentity
    {
        public DdfHardwareDeviceIdentity(string logicalId,
            DdfHardwareTransportKind transport, string providerId,
            string deviceKey, string architecture = "any",
            string driverId = null)
        {
            LogicalId = Identifier(logicalId, nameof(logicalId));
            if (!Enum.IsDefined(typeof(DdfHardwareTransportKind), transport))
                throw new ArgumentOutOfRangeException(nameof(transport));
            Transport = transport;
            ProviderId = Text(providerId, nameof(providerId));
            DeviceKey = Text(deviceKey, nameof(deviceKey));
            Architecture = Text(architecture, nameof(architecture));
            DriverId = (driverId ?? string.Empty).Trim();
        }

        public string LogicalId { get; }
        public DdfHardwareTransportKind Transport { get; }
        public string ProviderId { get; }
        public string DeviceKey { get; }
        public string Architecture { get; }
        public string DriverId { get; }

        internal bool Matches(DdfHardwareDeviceIdentity other) => other !=
            null && LogicalId == other.LogicalId && Transport ==
            other.Transport && ProviderId == other.ProviderId && DeviceKey ==
            other.DeviceKey && Architecture == other.Architecture &&
            DriverId == other.DriverId;

        internal static string Identifier(string value, string parameterName)
        {
            string result = Text(value, parameterName);
            if (!(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character => !(char.IsLetterOrDigit(
                    character) || character == '_' || character == '.')))
                throw new ArgumentException("Identificatore hardware non valido: " +
                    result + ".", parameterName);
            return result;
        }

        private static string Text(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0)
                throw new ArgumentException("Valore hardware obbligatorio.",
                    parameterName);
            return result;
        }
    }

    public sealed class DdfHardwareDeviceDefinition
    {
        private const DdfHardwarePermission AllPermissions =
            DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
            DdfHardwarePermission.Write | DdfHardwarePermission.Configure;

        public DdfHardwareDeviceDefinition(DdfHardwareDeviceIdentity identity,
            DdfHardwareAccessMode accessMode,
            DdfHardwarePermission allowedOperations,
            IEnumerable<string> configurationProfiles = null,
            int maxFrameCharacters = 4096,
            TimeSpan? operationTimeout = null)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (!Enum.IsDefined(typeof(DdfHardwareAccessMode), accessMode))
                throw new ArgumentOutOfRangeException(nameof(accessMode));
            if (allowedOperations == DdfHardwarePermission.None ||
                (allowedOperations & ~AllPermissions) != 0)
                throw new ArgumentOutOfRangeException(nameof(allowedOperations));
            if (maxFrameCharacters <= 0 || maxFrameCharacters > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxFrameCharacters));
            TimeSpan timeout = operationTimeout ?? TimeSpan.FromSeconds(5);
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(1))
                throw new ArgumentOutOfRangeException(nameof(operationTimeout));
            string[] profiles = (configurationProfiles ?? Array.Empty<string>())
                .Select(item => DdfHardwareDeviceIdentity.Identifier(item,
                    nameof(configurationProfiles))).ToArray();
            if (profiles.Distinct(StringComparer.Ordinal).Count() !=
                profiles.Length)
                throw new ArgumentException(
                    "Profilo hardware duplicato.", nameof(configurationProfiles));
            AccessMode = accessMode;
            AllowedOperations = allowedOperations;
            ConfigurationProfiles = Array.AsReadOnly(profiles);
            MaxFrameCharacters = maxFrameCharacters;
            OperationTimeout = timeout;
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public DdfHardwareAccessMode AccessMode { get; }
        public DdfHardwarePermission AllowedOperations { get; }
        public IReadOnlyList<string> ConfigurationProfiles { get; }
        public int MaxFrameCharacters { get; }
        public TimeSpan OperationTimeout { get; }
    }

    public sealed class DdfHardwareAvailabilityChangedEventArgs : EventArgs
    {
        public DdfHardwareAvailabilityChangedEventArgs(string logicalId,
            bool isAvailable, long generation)
        {
            LogicalId = logicalId;
            IsAvailable = isAvailable;
            Generation = generation;
        }

        public string LogicalId { get; }
        public bool IsAvailable { get; }
        public long Generation { get; }
    }

    public interface IDdfHardwareDevice : IAsyncDisposable
    {
        DdfHardwareDeviceIdentity Identity { get; }
        bool IsAvailable { get; }
        event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;
        ValueTask OpenAsync(CancellationToken cancellationToken);
        ValueTask CloseAsync();
        ValueTask<string> ReadAsync(int maxCharacters,
            CancellationToken cancellationToken);
        ValueTask WriteAsync(string frame, CancellationToken cancellationToken);
        ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken);
    }

    public interface IDdfHardwareExchangeDevice
    {
        ValueTask<string> ExchangeAsync(string frame, int maxCharacters,
            CancellationToken cancellationToken);
    }

    public sealed class DdfHardwareOperationDefinition
    {
        public DdfHardwareOperationDefinition(string functionName,
            string bindingId, string deviceId,
            DdfHardwareOperationKind kind, string profileId = null,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null)
        {
            FunctionName = DdfHardwareDeviceIdentity.Identifier(functionName,
                nameof(functionName));
            BindingId = DdfHardwareDeviceIdentity.Identifier(bindingId,
                nameof(bindingId));
            DeviceId = DdfHardwareDeviceIdentity.Identifier(deviceId,
                nameof(deviceId));
            if (!Enum.IsDefined(typeof(DdfHardwareOperationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? string.Empty :
                DdfHardwareDeviceIdentity.Identifier(profileId,
                    nameof(profileId));
            if (kind == DdfHardwareOperationKind.ApplyProfile &&
                ProfileId.Length == 0)
                throw new ArgumentException(
                    "L'operazione configure richiede un profilo host.",
                    nameof(profileId));
            if (kind != DdfHardwareOperationKind.ApplyProfile &&
                ProfileId.Length != 0)
                throw new ArgumentException(
                    "Il profilo è valido soltanto per configure.",
                    nameof(profileId));
            Documentation = documentation ?? string.Empty;
            string[] permissions = (requiredPermissions ??
                    Array.Empty<string>()).Select(item =>
                    DdfHardwareDeviceIdentity.Identifier(item,
                        nameof(requiredPermissions)))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (permissions.Length == 0)
                throw new ArgumentException(
                    "Ogni operazione hardware richiede almeno un permesso.",
                    nameof(requiredPermissions));
            RequiredPermissions = Array.AsReadOnly(permissions);
        }

        public string FunctionName { get; }
        public string BindingId { get; }
        public string DeviceId { get; }
        public DdfHardwareOperationKind Kind { get; }
        public string ProfileId { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }

        internal DdfHardwarePermission RequiredOperation => Kind switch
        {
            DdfHardwareOperationKind.IsAvailable =>
                DdfHardwarePermission.Probe,
            DdfHardwareOperationKind.Read => DdfHardwarePermission.Read,
            DdfHardwareOperationKind.Write => DdfHardwarePermission.Write,
            DdfHardwareOperationKind.Exchange =>
                DdfHardwarePermission.Read | DdfHardwarePermission.Write,
            DdfHardwareOperationKind.ApplyProfile =>
                DdfHardwarePermission.Configure,
            _ => DdfHardwarePermission.None
        };

        internal DdfHostFunctionDefinition ToHostFunction(
            DdfGovernedHardwareAdapter adapter) => new(FunctionName,
                ReturnType, Parameters.Select(item =>
                    new DdfHostParameterDefinition(item.Name, item.TypeName)),
                null, Documentation, RequiredPermissions,
                effect: Kind == DdfHardwareOperationKind.IsAvailable
                    ? DdfHostFunctionEffect.Pure
                    : DdfHostFunctionEffect.HostMutation,
                invokeAsync: (context, arguments) => adapter.InvokeAsync(
                    context, BindingId, arguments));

        internal DdfExternalApiFunctionDefinition ToExternalFunction(
            IEnumerable<string> inheritedPermissions) => new(FunctionName,
                BindingId, ReturnType, Parameters, Documentation,
                (inheritedPermissions ?? Array.Empty<string>()).Concat(
                    RequiredPermissions).Distinct(StringComparer.Ordinal),
                Kind == DdfHardwareOperationKind.IsAvailable
                    ? DdfHostFunctionEffect.Pure
                    : DdfHostFunctionEffect.HostMutation);

        private string ReturnType => Kind == DdfHardwareOperationKind.Read ||
            Kind == DdfHardwareOperationKind.Exchange
            ? "string" : "bool";
        private IReadOnlyList<DdfExternalApiParameterDefinition> Parameters =>
            Kind == DdfHardwareOperationKind.Write ||
            Kind == DdfHardwareOperationKind.Exchange
                ? new[] { new DdfExternalApiParameterDefinition("frame", "string") }
                : Array.Empty<DdfExternalApiParameterDefinition>();
    }

    /// <summary>
    /// Exposes only host-declared devices and operations. It never enumerates
    /// ports, drivers or operating-system devices and never accepts a physical
    /// device identifier from a script.
    /// </summary>
    public sealed class DdfGovernedHardwareAdapter :
        IDdfExternalApiRuntimeAdapter, IAsyncDisposable
    {
        private sealed class DeviceRuntime
        {
            internal DeviceRuntime(DdfHardwareDeviceDefinition definition,
                IDdfHardwareDevice device)
            {
                Definition = definition;
                Device = device;
            }

            internal DdfHardwareDeviceDefinition Definition { get; }
            internal IDdfHardwareDevice Device { get; }
            internal SemaphoreSlim Gate { get; } = new(1, 1);
            internal Dictionary<Guid, ExecutionLease> Leases { get; } = new();
        }

        private sealed class ExecutionLease : IAsyncDisposable
        {
            private DdfGovernedHardwareAdapter owner;
            internal ExecutionLease(DdfGovernedHardwareAdapter owner,
                DeviceRuntime runtime, Guid executionId)
            {
                this.owner = owner;
                Runtime = runtime;
                ExecutionId = executionId;
            }

            internal DeviceRuntime Runtime { get; }
            internal Guid ExecutionId { get; }
            public ValueTask DisposeAsync()
            {
                DdfGovernedHardwareAdapter current = Interlocked.Exchange(
                    ref owner, null);
                return current == null ? ValueTask.CompletedTask :
                    current.ReleaseAsync(this);
            }
        }

        private readonly IReadOnlyDictionary<string, DeviceRuntime> devices;
        private readonly IReadOnlyDictionary<string,
            DdfHardwareOperationDefinition> operations;
        private int disposed;

        public DdfGovernedHardwareAdapter(string adapterId,
            IEnumerable<DdfHardwareDeviceDefinition> definitions,
            IEnumerable<IDdfHardwareDevice> devices,
            IEnumerable<DdfHardwareOperationDefinition> operations)
        {
            AdapterId = DdfHardwareDeviceIdentity.Identifier(adapterId,
                nameof(adapterId));
            DdfHardwareDeviceDefinition[] definitionArray = (definitions ??
                Array.Empty<DdfHardwareDeviceDefinition>()).ToArray();
            IDdfHardwareDevice[] deviceArray = (devices ??
                Array.Empty<IDdfHardwareDevice>()).ToArray();
            DdfHardwareOperationDefinition[] operationArray = (operations ??
                Array.Empty<DdfHardwareOperationDefinition>()).ToArray();
            EnsureValidAndUnique(definitionArray,
                item => item?.Identity.LogicalId, nameof(definitions));
            EnsureValidAndUnique(deviceArray, item => item?.Identity.LogicalId,
                nameof(devices));
            EnsureValidAndUnique(operationArray, item => item?.BindingId,
                nameof(operations));
            if (operationArray.Select(item => item.FunctionName).Distinct(
                    StringComparer.Ordinal).Count() != operationArray.Length)
                throw new ArgumentException(
                    "Nome funzione hardware duplicato.", nameof(operations));

            var deviceById = deviceArray.ToDictionary(item =>
                item.Identity.LogicalId, StringComparer.Ordinal);
            var runtimes = new Dictionary<string, DeviceRuntime>(
                StringComparer.Ordinal);
            foreach (DdfHardwareDeviceDefinition definition in definitionArray)
            {
                if (!deviceById.TryGetValue(definition.Identity.LogicalId,
                        out IDdfHardwareDevice device) ||
                    !definition.Identity.Matches(device.Identity))
                    throw new ArgumentException(
                        "Il dispositivo iniettato non coincide con l'identità autorizzata: " +
                        definition.Identity.LogicalId + ".", nameof(devices));
                var runtime = new DeviceRuntime(definition, device);
                device.AvailabilityChanged += OnDeviceAvailabilityChanged;
                runtimes.Add(definition.Identity.LogicalId, runtime);
            }
            if (deviceById.Count != runtimes.Count)
                throw new ArgumentException(
                    "È stato iniettato un dispositivo non dichiarato.",
                    nameof(devices));
            foreach (DdfHardwareOperationDefinition operation in operationArray)
            {
                if (!runtimes.TryGetValue(operation.DeviceId,
                        out DeviceRuntime runtime))
                    throw new ArgumentException(
                        "Dispositivo logico non dichiarato: " +
                        operation.DeviceId + ".", nameof(operations));
                if ((runtime.Definition.AllowedOperations &
                     operation.RequiredOperation) != operation.RequiredOperation)
                    throw new ArgumentException(
                        "Operazione non autorizzata per il dispositivo " +
                        operation.DeviceId + ".", nameof(operations));
                if (operation.Kind == DdfHardwareOperationKind.ApplyProfile &&
                    !runtime.Definition.ConfigurationProfiles.Contains(
                        operation.ProfileId, StringComparer.Ordinal))
                    throw new ArgumentException(
                        "Profilo hardware non dichiarato: " +
                        operation.ProfileId + ".", nameof(operations));
            }
            this.devices = new ReadOnlyDictionary<string, DeviceRuntime>(runtimes);
            this.operations = new ReadOnlyDictionary<string,
                DdfHardwareOperationDefinition>(operationArray.ToDictionary(
                    item => item.BindingId, StringComparer.Ordinal));
        }

        public string AdapterId { get; }
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            DeviceAvailabilityChanged;
        public IReadOnlyCollection<DdfHardwareDeviceDefinition> Devices =>
            devices.Values.Select(item => item.Definition).ToList().AsReadOnly();
        public IReadOnlyCollection<DdfHardwareOperationDefinition> Operations =>
            operations.Values.ToList().AsReadOnly();

        public DdfHostFunctionLibraryDefinition CreateHostLibrary(string id,
            string displayName, string documentation = null,
            IEnumerable<string> requiredPermissions = null) => new(id,
                displayName, operations.Values.Select(item =>
                    item.ToHostFunction(this)), documentation,
                requiredPermissions);

        public DdfExternalApiDefinition CreateExternalApiDefinition(
            string referenceId, string referenceVersion,
            IEnumerable<DdfHostPermissionDefinition> permissions,
            IEnumerable<string> requiredPermissions = null) => new(referenceId,
                referenceVersion, permissions: permissions,
                functions: operations.Values.Select(item =>
                    item.ToExternalFunction(requiredPermissions)));

        object IDdfExternalApiRuntimeAdapter.ReadObject(string bindingId) =>
            throw new NotSupportedException(
                "L'adapter hardware non espone oggetti globali.");

        void IDdfExternalApiRuntimeAdapter.WriteObject(string bindingId,
            object value) => throw new NotSupportedException(
                "L'adapter hardware non espone oggetti globali.");

        public async ValueTask<object> InvokeAsync(
            DdfHostInvocationContext context, string bindingId,
            IReadOnlyList<object> arguments)
        {
            ThrowIfDisposed();
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!operations.TryGetValue(bindingId ?? string.Empty,
                    out DdfHardwareOperationDefinition operation))
                throw new InvalidOperationException(
                    "Operazione hardware non autorizzata: " + bindingId + ".");
            int expectedArguments = operation.Kind ==
                DdfHardwareOperationKind.Write || operation.Kind ==
                DdfHardwareOperationKind.Exchange ? 1 : 0;
            if ((arguments?.Count ?? 0) != expectedArguments)
                throw new ArgumentException(
                    "Numero di argomenti hardware non valido.",
                    nameof(arguments));
            DeviceRuntime runtime = devices[operation.DeviceId];
            if (operation.Kind == DdfHardwareOperationKind.IsAvailable)
                return runtime.Device.IsAvailable;
            if (!runtime.Device.IsAvailable)
                throw new InvalidOperationException(
                    "Dispositivo hardware logico non disponibile: " +
                    operation.DeviceId + ".");

            using CancellationTokenSource timeout = CreateTimeout(context,
                runtime.Definition.OperationTimeout);
            _ = await AcquireAsync(runtime, context, timeout.Token);
            switch (operation.Kind)
            {
                case DdfHardwareOperationKind.Read:
                    string frame = await runtime.Device.ReadAsync(
                        runtime.Definition.MaxFrameCharacters, timeout.Token);
                    if ((frame?.Length ?? 0) >
                        runtime.Definition.MaxFrameCharacters)
                        throw new InvalidOperationException(
                            "Frame hardware oltre la quota autorizzata.");
                    return frame ?? string.Empty;
                case DdfHardwareOperationKind.Write:
                    string payload = Convert.ToString(arguments[0]) ??
                        string.Empty;
                    if (payload.Length >
                        runtime.Definition.MaxFrameCharacters)
                        throw new InvalidOperationException(
                            "Frame hardware oltre la quota autorizzata.");
                    await runtime.Device.WriteAsync(payload, timeout.Token);
                    return true;
                case DdfHardwareOperationKind.Exchange:
                    if (runtime.Device is not IDdfHardwareExchangeDevice
                        exchangeDevice)
                        throw new InvalidOperationException(
                            "Il dispositivo non supporta scambi atomici.");
                    string request = Convert.ToString(arguments[0]) ??
                        string.Empty;
                    if (request.Length >
                        runtime.Definition.MaxFrameCharacters)
                        throw new InvalidOperationException(
                            "Frame hardware oltre la quota autorizzata.");
                    string response = await exchangeDevice.ExchangeAsync(
                        request, runtime.Definition.MaxFrameCharacters,
                        timeout.Token);
                    if ((response?.Length ?? 0) >
                        runtime.Definition.MaxFrameCharacters)
                        throw new InvalidOperationException(
                            "Risposta hardware oltre la quota autorizzata.");
                    return response ?? string.Empty;
                case DdfHardwareOperationKind.ApplyProfile:
                    await runtime.Device.ApplyProfileAsync(operation.ProfileId,
                        timeout.Token);
                    return true;
                default:
                    throw new InvalidOperationException(
                        "Operazione hardware non supportata.");
            }
        }

        private async ValueTask<ExecutionLease> AcquireAsync(
            DeviceRuntime runtime, DdfHostInvocationContext context,
            CancellationToken cancellationToken)
        {
            await runtime.Gate.WaitAsync(cancellationToken);
            try
            {
                if (runtime.Leases.TryGetValue(context.ExecutionId,
                        out ExecutionLease existing))
                    return existing;
                if (runtime.Definition.AccessMode ==
                        DdfHardwareAccessMode.Exclusive &&
                    runtime.Leases.Count != 0)
                    throw new InvalidOperationException(
                        "Dispositivo hardware già in uso esclusivo: " +
                        runtime.Definition.Identity.LogicalId + ".");
                if (runtime.Leases.Count == 0)
                    await runtime.Device.OpenAsync(cancellationToken);
                var lease = new ExecutionLease(this, runtime,
                    context.ExecutionId);
                runtime.Leases.Add(context.ExecutionId, lease);
                try { context.RegisterAsyncResource(lease); }
                catch
                {
                    runtime.Leases.Remove(context.ExecutionId);
                    if (runtime.Leases.Count == 0)
                        await runtime.Device.CloseAsync();
                    throw;
                }
                return lease;
            }
            finally { runtime.Gate.Release(); }
        }

        private async ValueTask ReleaseAsync(ExecutionLease lease)
        {
            DeviceRuntime runtime = lease.Runtime;
            await runtime.Gate.WaitAsync();
            try
            {
                if (!runtime.Leases.Remove(lease.ExecutionId)) return;
                if (runtime.Leases.Count == 0)
                    await runtime.Device.CloseAsync();
            }
            finally { runtime.Gate.Release(); }
        }

        private static CancellationTokenSource CreateTimeout(
            DdfHostInvocationContext context, TimeSpan operationTimeout)
        {
            var result = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken);
            TimeSpan timeout = operationTimeout;
            if (context.Deadline.HasValue)
            {
                TimeSpan remaining = context.Deadline.Value -
                    DateTimeOffset.UtcNow;
                if (remaining < timeout) timeout = remaining;
            }
            if (timeout <= TimeSpan.Zero) result.Cancel();
            else result.CancelAfter(timeout);
            return result;
        }

        private static void EnsureValidAndUnique<T>(IReadOnlyList<T> values,
            Func<T, string> key, string parameterName)
        {
            if (values.Count == 0 || values.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesta almeno una definizione hardware valida.",
                    parameterName);
            if (values.Select(key).Distinct(StringComparer.Ordinal).Count() !=
                values.Count)
                throw new ArgumentException(
                    "Definizione hardware duplicata.", parameterName);
        }

        private void OnDeviceAvailabilityChanged(object sender,
            DdfHardwareAvailabilityChangedEventArgs args) =>
            DeviceAvailabilityChanged?.Invoke(this, args);

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            foreach (DeviceRuntime runtime in devices.Values)
            {
                ExecutionLease[] leases;
                await runtime.Gate.WaitAsync();
                try { leases = runtime.Leases.Values.ToArray(); }
                finally { runtime.Gate.Release(); }
                foreach (ExecutionLease lease in leases)
                    await lease.DisposeAsync();
                runtime.Device.AvailabilityChanged -=
                    OnDeviceAvailabilityChanged;
                await runtime.Device.DisposeAsync();
                runtime.Gate.Dispose();
            }
        }
    }

    /// <summary>
    /// Deterministic transport used by automated tests, teaching mode and host
    /// prototypes. It never opens a real operating-system device.
    /// </summary>
    public sealed class DdfLoopbackHardwareDevice : IDdfHardwareDevice
    {
        private readonly ConcurrentQueue<string> frames = new();
        private readonly HashSet<string> profiles;
        private int available = 1;
        private int disposed;
        private int openSessions;
        private long generation;

        public DdfLoopbackHardwareDevice(DdfHardwareDeviceIdentity identity,
            IEnumerable<string> profiles = null, bool echoWrites = true,
            TimeSpan? operationDelay = null)
        {
            Identity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            this.profiles = new HashSet<string>((profiles ??
                Array.Empty<string>()).Select(item =>
                DdfHardwareDeviceIdentity.Identifier(item, nameof(profiles))),
                StringComparer.Ordinal);
            EchoWrites = echoWrites;
            OperationDelay = operationDelay ?? TimeSpan.Zero;
            if (OperationDelay < TimeSpan.Zero ||
                OperationDelay > TimeSpan.FromMinutes(1))
                throw new ArgumentOutOfRangeException(nameof(operationDelay));
        }

        public DdfHardwareDeviceIdentity Identity { get; }
        public bool IsAvailable => Volatile.Read(ref available) != 0 &&
            Volatile.Read(ref disposed) == 0;
        public bool EchoWrites { get; }
        public TimeSpan OperationDelay { get; }
        public int OpenSessions => Volatile.Read(ref openSessions);
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public string LastProfile { get; private set; } = string.Empty;
        public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
            AvailabilityChanged;

        public void SetAvailable(bool value)
        {
            ThrowIfDisposed();
            int next = value ? 1 : 0;
            if (Interlocked.Exchange(ref available, next) == next) return;
            long current = Interlocked.Increment(ref generation);
            AvailabilityChanged?.Invoke(this,
                new DdfHardwareAvailabilityChangedEventArgs(
                    Identity.LogicalId, value, current));
        }

        public void EnqueueInput(string frame)
        {
            ThrowIfDisposed();
            frames.Enqueue(frame ?? string.Empty);
        }

        public async ValueTask OpenAsync(
            CancellationToken cancellationToken)
        {
            EnsureAvailable();
            await DelayAsync(cancellationToken);
            Interlocked.Increment(ref openSessions);
            OpenCount++;
        }

        public ValueTask CloseAsync()
        {
            if (Interlocked.CompareExchange(ref openSessions, 0, 0) > 0)
                Interlocked.Decrement(ref openSessions);
            CloseCount++;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<string> ReadAsync(int maxCharacters,
            CancellationToken cancellationToken)
        {
            EnsureOpen();
            await DelayAsync(cancellationToken);
            EnsureAvailable();
            if (!frames.TryDequeue(out string frame)) return string.Empty;
            if (frame.Length > maxCharacters)
                throw new InvalidOperationException(
                    "Frame loopback oltre la quota autorizzata.");
            return frame;
        }

        public async ValueTask WriteAsync(string frame,
            CancellationToken cancellationToken)
        {
            EnsureOpen();
            await DelayAsync(cancellationToken);
            EnsureAvailable();
            if (EchoWrites) frames.Enqueue(frame ?? string.Empty);
        }

        public async ValueTask ApplyProfileAsync(string profileId,
            CancellationToken cancellationToken)
        {
            EnsureOpen();
            await DelayAsync(cancellationToken);
            EnsureAvailable();
            if (!profiles.Contains(profileId ?? string.Empty))
                throw new InvalidOperationException(
                    "Profilo loopback non autorizzato.");
            LastProfile = profileId;
        }

        private async ValueTask DelayAsync(CancellationToken token)
        {
            if (OperationDelay > TimeSpan.Zero)
                await Task.Delay(OperationDelay, token);
            else token.ThrowIfCancellationRequested();
        }

        private void EnsureOpen()
        {
            ThrowIfDisposed();
            if (OpenSessions == 0)
                throw new InvalidOperationException(
                    "Dispositivo loopback non aperto.");
        }

        private void EnsureAvailable()
        {
            ThrowIfDisposed();
            if (!IsAvailable)
                throw new InvalidOperationException(
                    "Dispositivo loopback scollegato.");
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                Interlocked.Exchange(ref available, 0);
                Interlocked.Exchange(ref openSessions, 0);
                while (frames.TryDequeue(out _)) { }
            }
            return ValueTask.CompletedTask;
        }
    }
}
