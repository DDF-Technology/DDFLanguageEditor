using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Hardware;

namespace DDFLanguageEditor.Hardware.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("Hardware exposes only declared logical devices and operations",
                ExposesOnlyDeclaredSurface);
            Run("Loopback executes frames profiles and deterministic cleanup",
                ExecutesLoopbackAndCleansUp);
            Run("Hardware observes hot-plug quotas and timeout",
                ObservesHotPlugQuotasAndTimeout);
            Run("Hardware enforces exclusive execution leases",
                EnforcesExclusiveLeases);
            Run("Serial keeps physical configuration outside the DDF API",
                KeepsSerialConfigurationHostSide);
            Run("Serial frames strict UTF-8 with governed delimiters",
                FramesSerialUtf8);
            Run("Serial integrates profiles leases and cleanup",
                IntegratesSerialProfilesAndLifecycle);
            Run("Serial observes signals disconnection and cancellation",
                ObservesSerialSignalsAndCancellation);
            Run("Differential serial validates modes and profiles",
                ValidatesDifferentialProfiles);
            Run("RS485 controls direction and turnaround",
                ControlsRs485DirectionAndTurnaround);
            Run("RS485 restores receive direction after cancellation",
                RestoresReceiveDirectionAfterCancellation);
            Run("RS422 and vendor direction remain explicit",
                KeepsRs422AndVendorSemanticsExplicit);
            Run("MIDI keeps endpoints and profiles host-side",
                KeepsMidiEndpointHostSide);
            Run("MIDI exchanges canonical UMP packets",
                ExchangesCanonicalUmpPackets);
            Run("MIDI governs timestamp groups and SysEx",
                GovernsMidiTimestampGroupsAndSysEx);
            Run("MIDI keeps legacy lifecycle and hot-plug isolated",
                KeepsLegacyMidiLifecycleIsolated);
            Run("Parallel keeps ports and driver controls host-side",
                KeepsParallelPortHostSide);
            Run("Parallel negotiates modes and exchanges bounded data",
                NegotiatesParallelModesAndData);
            Run("Parallel governs status reset and hot-plug",
                GovernsParallelStatusResetAndHotPlug);
            Run("Parallel rolls back profiles and cleans failed opens",
                RollsBackParallelProfilesAndFailedOpens);
            Run("IoT keeps pins buses and chip-select host-side",
                KeepsIotTopologyHostSide);
            Run("GPIO enforces direction and observes input changes",
                EnforcesGpioDirectionAndInputChanges);
            Run("I2C and SPI use bounded atomic exchanges",
                UsesBoundedAtomicBusExchanges);
            Run("PWM governs ranges and IoT lifecycle rollback",
                GovernsPwmAndIotLifecycleRollback);
            Run(".NET IoT bridge keeps provider lifecycle isolated",
                BridgesSystemDeviceLifecycle);
            Run(".NET IoT bridge preserves the active endpoint on failure",
                PreservesSystemDeviceEndpointOnProfileFailure);
            Run("IoT bus profiles govern physical read lengths",
                GovernsPhysicalReadLengths);
            PhysicalIotSmoke.RunIfRequested();
            Console.WriteLine(failures == 0
                ? "All 27 governed hardware adapter tests passed."
                : failures + " governed hardware adapter test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void ExposesOnlyDeclaredSurface()
        {
            Fixture fixture = CreateFixture();
            try
            {
                DdfExternalApiDefinition api = fixture.Adapter
                    .CreateExternalApiDefinition("ddf.hardware.demo", "1.0.0",
                        Permissions(), new[] { "hardware.access" });
                Equal(4, api.Functions.Count);
                Equal(true, api.Functions.All(item => item.RequiredPermissions
                    .Contains("hardware.access")));
                Equal(true, api.Functions.Single(item =>
                    item.Name == "readFrame").ReturnType == "string");
                string contract = api.Fingerprint + "|" + string.Join("|",
                    api.Functions.Select(item => item.Name + ":" +
                        item.BindingId + ":" + item.Documentation));
                Equal(false, contract.Contains("COM99",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("driver.private",
                    StringComparison.OrdinalIgnoreCase));
                DdfHostCatalogSnapshot analysis =
                    DdfExternalApiCatalogProjector.CreateAnalysisCatalog(api,
                        Permissions().Select(item => item.Id));
                Equal(true, analysis.Functions.Any(item =>
                    item.Name == "writeFrame" && item.Parameters.Count == 1));
                Throws<ArgumentException>(() => new
                    DdfGovernedHardwareAdapter("invalid", fixture.Definitions,
                        fixture.Devices.Concat(new[] { fixture.Device }),
                        fixture.Operations));
            }
            finally { fixture.Dispose(); }
        }

        private static void ExecutesLoopbackAndCleansUp()
        {
            Fixture fixture = CreateFixture();
            try
            {
                using DdfEditorExecutionService execution =
                    CreateExecution(fixture.Adapter);
                Equal("hello", ReturnString(Invoke(execution, "echo",
                    new Dictionary<string, object> { ["frame"] = "hello" })));
                Equal(0, fixture.Device.OpenSessions);
                Equal(1, fixture.Device.OpenCount);
                Equal(1, fixture.Device.CloseCount);
                Equal(true, ReturnBool(Invoke(execution, "configure")));
                Equal("fast", fixture.Device.LastProfile);
                Equal(0, fixture.Device.OpenSessions);
                Equal(true, ReturnBool(Invoke(execution, "available")));
            }
            finally { fixture.Dispose(); }
        }

        private static void ObservesHotPlugQuotasAndTimeout()
        {
            Fixture fixture = CreateFixture(maxFrameCharacters: 8);
            try
            {
                int changes = 0;
                fixture.Adapter.DeviceAvailabilityChanged += (_, args) =>
                {
                    Equal("panel.serial", args.LogicalId);
                    changes++;
                };
                using DdfEditorExecutionService execution =
                    CreateExecution(fixture.Adapter);
                fixture.Device.SetAvailable(false);
                Equal(1, changes);
                Equal(false, ReturnBool(Invoke(execution, "available")));
                Equal(false, Invoke(execution, "echo",
                    new Dictionary<string, object> { ["frame"] = "hello" })
                    .Succeeded);
                fixture.Device.SetAvailable(true);
                Equal(2, changes);
                Equal(false, Invoke(execution, "echo",
                    new Dictionary<string, object>
                    {
                        ["frame"] = "123456789"
                    }).Succeeded);
            }
            finally { fixture.Dispose(); }

            Fixture slow = CreateFixture(operationTimeout:
                TimeSpan.FromMilliseconds(20), operationDelay:
                TimeSpan.FromMilliseconds(200));
            try
            {
                using DdfEditorExecutionService execution =
                    CreateExecution(slow.Adapter);
                Equal(false, Invoke(execution, "echo",
                    new Dictionary<string, object> { ["frame"] = "slow" })
                    .Succeeded);
                Equal(0, slow.Device.OpenSessions);
            }
            finally { slow.Dispose(); }
        }

        private static void EnforcesExclusiveLeases()
        {
            Fixture fixture = CreateFixture();
            try
            {
                var firstResources = new List<IAsyncDisposable>();
                var first = Context(resource => firstResources.Add(resource));
                Equal(true, Convert.ToBoolean(DdfExternalApiRuntimeInvoker
                    .InvokeAsync(fixture.Adapter, "loop.write",
                        new object[] { "one" }, first).AsTask().GetAwaiter()
                    .GetResult()));
                Equal(1, fixture.Device.OpenSessions);

                var secondResources = new List<IAsyncDisposable>();
                var second = Context(resource => secondResources.Add(resource));
                Throws<InvalidOperationException>(() =>
                    DdfExternalApiRuntimeInvoker.InvokeAsync(fixture.Adapter,
                        "loop.write", new object[] { "two" }, second).AsTask()
                    .GetAwaiter().GetResult());
                firstResources.Single().DisposeAsync().AsTask().GetAwaiter()
                    .GetResult();
                Equal(0, fixture.Device.OpenSessions);

                Equal(true, Convert.ToBoolean(DdfExternalApiRuntimeInvoker
                    .InvokeAsync(fixture.Adapter, "loop.write",
                        new object[] { "two" }, second).AsTask().GetAwaiter()
                    .GetResult()));
                secondResources.Single().DisposeAsync().AsTask().GetAwaiter()
                    .GetResult();
                Equal(0, fixture.Device.OpenSessions);
            }
            finally { fixture.Dispose(); }
        }

        private static void KeepsSerialConfigurationHostSide()
        {
            SerialFixture fixture = CreateSerialFixture();
            try
            {
                Equal(115200, fixture.Options.InitialProfile.BaudRate);
                Equal(DdfSerialParity.None,
                    fixture.Options.InitialProfile.Parity);
                DdfExternalApiDefinition api = fixture.Adapter
                    .CreateExternalApiDefinition("ddf.serial.demo", "1.0.0",
                        Permissions(), new[] { "hardware.access" });
                string contract = api.Fingerprint + "|" + string.Join("|",
                    api.Functions.Select(item => item.Name + item.BindingId +
                        item.Documentation));
                Equal(false, contract.Contains("COM77",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("115200",
                    StringComparison.Ordinal));
                Equal(false, contract.Contains("usb.serial.private",
                    StringComparison.Ordinal));
                Throws<ArgumentOutOfRangeException>(() => new
                    DdfSerialCommunicationProfile("bad", 9600, dataBits: 9));
                Throws<ArgumentException>(() => new DdfSerialEndpointOptions(
                    "COM1", fixture.Options.InitialProfile,
                    new[] { fixture.Options.InitialProfile }));
            }
            finally { fixture.Dispose(); }
        }

        private static void FramesSerialUtf8()
        {
            SerialFixture fixture = CreateSerialFixture();
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Connection.Enqueue("ciao\nseconda");
                Equal("ciao", fixture.Device.ReadAsync(64,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult());
                fixture.Connection.Enqueue("\n");
                Equal("seconda", fixture.Device.ReadAsync(64,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult());
                fixture.Device.WriteAsync("è-ok", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Equal("è-ok\n", StrictUtf8.GetString(
                    fixture.Connection.Writes.Single()));
                fixture.Device.CloseAsync().AsTask().GetAwaiter().GetResult();
                Equal(1, fixture.Connection.CloseCount);
            }
            finally { fixture.Dispose(); }
        }

        private static void IntegratesSerialProfilesAndLifecycle()
        {
            SerialFixture fixture = CreateSerialFixture();
            try
            {
                fixture.Connection.Enqueue("reply\n");
                var resources = new List<IAsyncDisposable>();
                DdfExternalApiInvocationContext context = Context(
                    resource => resources.Add(resource));
                Equal(true, Convert.ToBoolean(DdfExternalApiRuntimeInvoker
                    .InvokeAsync(fixture.Adapter, "serial.configure.fast",
                        Array.Empty<object>(), context).AsTask().GetAwaiter()
                    .GetResult()));
                Equal("fast", fixture.Connection.LastProfile.Id);
                Equal(true, Convert.ToBoolean(DdfExternalApiRuntimeInvoker
                    .InvokeAsync(fixture.Adapter, "serial.write",
                        new object[] { "request" }, context).AsTask()
                    .GetAwaiter().GetResult()));
                Equal("reply", Convert.ToString(DdfExternalApiRuntimeInvoker
                    .InvokeAsync(fixture.Adapter, "serial.read",
                        Array.Empty<object>(), context).AsTask().GetAwaiter()
                    .GetResult()));
                Equal(1, fixture.Connection.OpenCount);
                Equal(true, fixture.Connection.IsOpen);
                resources.Single().DisposeAsync().AsTask().GetAwaiter()
                    .GetResult();
                Equal(false, fixture.Connection.IsOpen);
                Equal(1, fixture.Connection.CloseCount);
            }
            finally { fixture.Dispose(); }
        }

        private static void ObservesSerialSignalsAndCancellation()
        {
            SerialFixture fixture = CreateSerialFixture();
            try
            {
                int dataSignals = 0;
                int faults = 0;
                int disconnects = 0;
                fixture.Device.DataAvailable += (_, args) =>
                {
                    Equal("serial.console", args.LogicalId);
                    Equal(4, args.BytesAvailable);
                    dataSignals++;
                };
                fixture.Device.Faulted += (_, args) =>
                {
                    Equal("serial.console", args.LogicalId);
                    faults++;
                };
                fixture.Device.AvailabilityChanged += (_, args) =>
                {
                    if (!args.IsAvailable) disconnects++;
                };
                fixture.Connection.RaiseDataAvailable(4);
                fixture.Connection.RaiseFault("serial.disconnected", true);
                Equal(1, dataSignals);
                Equal(1, faults);
                Equal(1, disconnects);
                Equal(false, fixture.Device.IsAvailable);

                fixture.Device.NotifyAvailability(true);
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Connection.ReadDelay = TimeSpan.FromMilliseconds(200);
                fixture.Connection.Enqueue("late\n");
                using var cancellation = new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(20));
                Throws<OperationCanceledException>(() => fixture.Device
                    .ReadAsync(64, cancellation.Token).AsTask().GetAwaiter()
                    .GetResult());
            }
            finally { fixture.Dispose(); }
        }

        private static void ValidatesDifferentialProfiles()
        {
            Throws<ArgumentException>(() => new DdfDifferentialSerialProfile(
                "half", "normal", DdfDifferentialSerialMode.Rs485HalfDuplex,
                DdfSerialDirectionControl.None));
            Throws<ArgumentException>(() => new DdfDifferentialSerialProfile(
                "full", "normal", DdfDifferentialSerialMode.Rs422FullDuplex,
                DdfSerialDirectionControl.RtsHighWhileTransmitting));
            Throws<ArgumentException>(() => new DdfDifferentialSerialProfile(
                "vendor", "normal",
                DdfDifferentialSerialMode.Rs485HalfDuplex,
                DdfSerialDirectionControl.VendorAdapter));

            SerialFixture serial = CreateSerialFixture();
            try
            {
                var unknown = new DdfDifferentialSerialProfile("unknown",
                    "missing", DdfDifferentialSerialMode.Rs422FullDuplex,
                    DdfSerialDirectionControl.DriverManaged);
                Throws<ArgumentException>(() => new
                    DdfDifferentialSerialHardwareDevice(
                        serial.Device.Identity, serial.Options,
                        new[] { unknown }, unknown.Id,
                        serial.Connection));
            }
            finally { serial.Dispose(); }

            DifferentialFixture rejected = CreateDifferentialFixture(
                DdfDifferentialSerialMode.Rs485HalfDuplex,
                DdfSerialDirectionControl.DriverManaged);
            try
            {
                rejected.Connection.DifferentialApplyFailuresRemaining = 1;
                Throws<InvalidOperationException>(() => rejected.Device
                    .OpenAsync(CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
                Equal(false, rejected.Connection.IsOpen);
                Equal(1, rejected.Connection.CloseCount);
            }
            finally { rejected.Dispose(); }
        }

        private static void ControlsRs485DirectionAndTurnaround()
        {
            DifferentialFixture fixture = CreateDifferentialFixture(
                DdfDifferentialSerialMode.Rs485HalfDuplex,
                DdfSerialDirectionControl.RtsHighWhileTransmitting,
                TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Device.WriteAsync("frame", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Equal("frame\n", StrictUtf8.GetString(
                    fixture.Connection.Writes.Single()));
                Equal("True,False", string.Join(",",
                    fixture.Connection.DirectionChanges));
                Equal("differential", fixture.Connection
                    .LastDifferentialProfile.Id);
            }
            finally { fixture.Dispose(); }
        }

        private static void RestoresReceiveDirectionAfterCancellation()
        {
            DifferentialFixture fixture = CreateDifferentialFixture(
                DdfDifferentialSerialMode.Rs485HalfDuplex,
                DdfSerialDirectionControl.RtsLowWhileTransmitting);
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Connection.WriteDelay = TimeSpan.FromMilliseconds(200);
                using var cancellation = new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(20));
                Throws<OperationCanceledException>(() => fixture.Device
                    .WriteAsync("cancel", cancellation.Token).AsTask()
                    .GetAwaiter().GetResult());
                Equal("True,False", string.Join(",",
                    fixture.Connection.DirectionChanges));
            }
            finally { fixture.Dispose(); }
        }

        private static void KeepsRs422AndVendorSemanticsExplicit()
        {
            DifferentialFixture rs422 = CreateDifferentialFixture(
                DdfDifferentialSerialMode.Rs422FullDuplex,
                DdfSerialDirectionControl.DriverManaged);
            try
            {
                rs422.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                rs422.Device.WriteAsync("full", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Equal(0, rs422.Connection.DirectionChanges.Count);
            }
            finally { rs422.Dispose(); }

            SerialFixture serial = CreateSerialFixture();
            try
            {
                var vendor = new DdfDifferentialSerialProfile("vendor",
                    "normal", DdfDifferentialSerialMode.Rs485HalfDuplex,
                    DdfSerialDirectionControl.VendorAdapter,
                    vendorProfileId: "adapter.acme.rs485");
                var system = new DdfSystemSerialPortConnection(serial.Options);
                try
                {
                    Throws<InvalidOperationException>(() => system
                        .ApplyDifferentialProfileAsync(vendor,
                            CancellationToken.None).AsTask().GetAwaiter()
                        .GetResult());
                }
                finally
                {
                    system.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally { serial.Dispose(); }
        }

        private static void KeepsMidiEndpointHostSide()
        {
            MidiFixture fixture = CreateMidiFixture();
            try
            {
                DdfExternalApiDefinition api = fixture.Adapter
                    .CreateExternalApiDefinition("ddf.midi.demo", "1.0.0",
                        Permissions(), new[] { "hardware.access" });
                string contract = api.Fingerprint + "|" + string.Join("|",
                    api.Functions.Select(item => item.Name + item.BindingId +
                        item.Documentation));
                Equal(false, contract.Contains("endpoint.private",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("Windows.Devices.Midi2",
                    StringComparison.Ordinal));
                Equal(DdfMidiApiProfile.WindowsMidiServices,
                    fixture.Device.ActiveProfile.ApiProfile);
                Throws<ArgumentException>(() => new DdfMidiEndpointProfile(
                    "bad", DdfMidiApiProfile.WindowsMidiServices,
                    DdfMidiDataFormat.Midi1ByteStream,
                    DdfMidiFlow.Bidirectional));
                Throws<ArgumentException>(() => new DdfMidiEndpointProfile(
                    "bad", DdfMidiApiProfile.LegacyMidi1,
                    DdfMidiDataFormat.Ump, DdfMidiFlow.Bidirectional));
            }
            finally { fixture.Dispose(); }
        }

        private static void ExchangesCanonicalUmpPackets()
        {
            MidiFixture fixture = CreateMidiFixture();
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                DdfMidiPacket packet = DdfMidiPacket.Ump(42, 0x22903C7Fu);
                fixture.Connection.Enqueue(packet);
                Equal("ump:42:22903C7F", fixture.Device.ReadAsync(64,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult());
                fixture.Device.WriteAsync("ump:0:22803C00",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal("ump:0:22803C00", fixture.Connection.Sent.Single()
                    .ToCanonicalString());
                Throws<FormatException>(() => DdfMidiPacket.Parse(
                    "ump:0:40903C7F"));
            }
            finally { fixture.Dispose(); }
        }

        private static void GovernsMidiTimestampGroupsAndSysEx()
        {
            MidiFixture fixture = CreateMidiFixture();
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("ump:99:22903C7F", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("ump:0:23903C7F", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("ump:0:32010000,00000000",
                        CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());

                fixture.Device.ApplyProfileAsync("expressive",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                fixture.Device.WriteAsync("ump:99:32010000,00000000",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal(true, fixture.Connection.Sent.Single().IsSystemExclusive);
                Equal("expressive", fixture.Connection.LastProfile.Id);
            }
            finally { fixture.Dispose(); }
        }

        private static void KeepsLegacyMidiLifecycleIsolated()
        {
            MidiFixture fixture = CreateMidiFixture(legacy: true);
            try
            {
                int disconnects = 0;
                int packets = 0;
                fixture.Device.AvailabilityChanged += (_, args) =>
                {
                    if (!args.IsAvailable) disconnects++;
                };
                fixture.Device.PacketAvailable += (_, args) =>
                {
                    Equal("midi.keyboard", args.LogicalId);
                    Equal(4, args.QueuedPackets);
                    packets++;
                };
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Device.WriteAsync("midi1:0:90,3C,7F",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal(DdfMidiDataFormat.Midi1ByteStream,
                    fixture.Connection.Sent.Single().Format);
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("ump:0:20903C7F", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
                fixture.Connection.RaisePacketAvailable(4);
                fixture.Connection.RaiseFault("midi.disconnected", true);
                Equal(1, packets);
                Equal(1, disconnects);
                Equal(false, fixture.Device.IsAvailable);
            }
            finally
            {
                fixture.Dispose();
                Equal(false, fixture.Connection.IsOpen);
                Equal(1, fixture.Connection.CloseCount);
            }
        }

        private static void KeepsParallelPortHostSide()
        {
            ParallelFixture fixture = CreateParallelFixture();
            try
            {
                DdfExternalApiDefinition api = fixture.Adapter
                    .CreateExternalApiDefinition("ddf.parallel.demo", "1.0.0",
                        Permissions(), new[] { "hardware.access" });
                string contract = api.Fingerprint + "|" + string.Join("|",
                    api.Functions.Select(item => item.Name + item.BindingId +
                        item.Documentation));
                Equal(false, contract.Contains("LPT1",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("0x378",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("parallel.private",
                    StringComparison.OrdinalIgnoreCase));
                Throws<ArgumentException>(() => new DdfParallelPortProfile(
                    "vendor", DdfParallelProviderKind.VendorSdk,
                    DdfParallelMode.Ecp, DdfParallelFlow.Bidirectional));
                Throws<ArgumentException>(() => new DdfParallelPortProfile(
                    "system", DdfParallelProviderKind.WindowsIeee1284,
                    DdfParallelMode.Ecp, DdfParallelFlow.Bidirectional,
                    vendorProfileId: "unsafe.raw"));
            }
            finally { fixture.Dispose(); }
        }

        private static void NegotiatesParallelModesAndData()
        {
            ParallelFixture fixture = CreateParallelFixture();
            try
            {
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Device.WriteAsync("data:01,7F,FF",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal("01,7F,FF", string.Join(",", fixture.Connection.Writes
                    .Single().Select(item => item.ToString("X2"))));
                Throws<InvalidOperationException>(() => fixture.Device
                    .ReadAsync(64, CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult());

                fixture.Device.ApplyProfileAsync("bidirectional",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                fixture.Connection.Enqueue(0x10, 0x20, 0x30);
                Equal("data:10,20,30", fixture.Device.ReadAsync(64,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult());
                Equal(DdfParallelMode.Ecp,
                    fixture.Connection.LastProfile.Mode);
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("data:00,01,02,03,04,05,06,07,08",
                        CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
            }
            finally { fixture.Dispose(); }
        }

        private static void GovernsParallelStatusResetAndHotPlug()
        {
            ParallelFixture fixture = CreateParallelFixture();
            try
            {
                int statuses = 0;
                int disconnects = 0;
                fixture.Device.StatusChanged += (_, args) =>
                {
                    Equal("parallel.printer", args.LogicalId);
                    Equal(DdfParallelStatus.Busy |
                        DdfParallelStatus.Selected, args.Status);
                    statuses++;
                };
                fixture.Device.AvailabilityChanged += (_, args) =>
                {
                    if (!args.IsAvailable) disconnects++;
                };
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                Throws<InvalidOperationException>(() => fixture.Device
                    .ResetDeviceAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult());
                fixture.Device.ApplyProfileAsync("bidirectional",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                fixture.Device.ResetDeviceAsync(CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Equal(1, fixture.Connection.ResetCount);
                fixture.Connection.Status = DdfParallelStatus.Busy |
                    DdfParallelStatus.Selected;
                fixture.Connection.RaiseStatus();
                Equal(fixture.Connection.Status, fixture.Device
                    .QueryStatusAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult());
                fixture.Connection.RaiseFault("parallel.disconnected", true);
                Equal(1, statuses);
                Equal(1, disconnects);
                Equal(false, fixture.Device.IsAvailable);
            }
            finally { fixture.Dispose(); }
        }

        private static void RollsBackParallelProfilesAndFailedOpens()
        {
            ParallelFixture rejected = CreateParallelFixture();
            try
            {
                rejected.Connection.ApplyFailuresRemaining = 1;
                Throws<InvalidOperationException>(() => rejected.Device
                    .OpenAsync(CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
                Equal(false, rejected.Connection.IsOpen);
                Equal(1, rejected.Connection.CloseCount);
            }
            finally { rejected.Dispose(); }

            ParallelFixture rollback = CreateParallelFixture();
            try
            {
                rollback.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                rollback.Connection.ApplyFailuresRemaining = 1;
                Throws<InvalidOperationException>(() => rollback.Device
                    .ApplyProfileAsync("bidirectional", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
                Equal("output", rollback.Device.ActiveProfile.Id);
                Equal("output", rollback.Connection.LastProfile.Id);
            }
            finally { rollback.Dispose(); }
        }

        private static void KeepsIotTopologyHostSide()
        {
            IotFixture fixture = CreateGpioFixture();
            try
            {
                DdfExternalApiDefinition api = fixture.Adapter
                    .CreateExternalApiDefinition("ddf.iot.demo", "1.0.0",
                        Permissions(), new[] { "hardware.access" });
                string contract = api.Fingerprint + "|" + string.Join("|",
                    api.Functions.Select(item => item.Name + item.BindingId +
                        item.Documentation));
                Equal(false, contract.Contains("pin17",
                    StringComparison.OrdinalIgnoreCase));
                Equal(false, contract.Contains("gpiochip",
                    StringComparison.OrdinalIgnoreCase));
                Throws<ArgumentException>(() => new DdfGpioProfile("bad", 1,
                    DdfGpioDirection.Output, DdfGpioPullMode.PullUp));
                Throws<ArgumentOutOfRangeException>(() => new DdfI2cProfile(
                    "bad", 1, 0x80));
                Throws<ArgumentException>(() => new DdfIotHardwareDevice(
                    fixture.Device.Identity,
                    new DdfIotEndpointProfile[] { new DdfI2cProfile(
                        "i2c", 1, 0x20) }, "i2c", fixture.Connection));
            }
            finally { fixture.Dispose(); }
        }

        private static void EnforcesGpioDirectionAndInputChanges()
        {
            IotFixture fixture = CreateGpioFixture();
            try
            {
                int changes = 0;
                fixture.Device.InputChanged += (_, frame) =>
                {
                    Equal("level:1", frame.ToCanonicalString());
                    changes++;
                };
                fixture.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                fixture.Connection.ReadFrame = DdfIotFrame.Digital(true);
                Equal("level:1", fixture.Device.ReadAsync(32,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult());
                Throws<InvalidOperationException>(() => fixture.Device
                    .WriteAsync("level:1", CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult());
                fixture.Connection.RaiseInput(DdfIotFrame.Digital(true));
                fixture.Device.ApplyProfileAsync("output",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                fixture.Device.WriteAsync("level:1", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Equal("level:1", fixture.Connection.Writes.Single()
                    .ToCanonicalString());
                Equal(1, changes);
            }
            finally { fixture.Dispose(); }
        }

        private static void UsesBoundedAtomicBusExchanges()
        {
            IotFixture i2c = CreateBusFixture(DdfHardwareTransportKind.I2c);
            try
            {
                i2c.Connection.ExchangeResponse = DdfIotFrame.Data(0x2A);
                var resources = new List<IAsyncDisposable>();
                DdfExternalApiInvocationContext context = Context(
                    resource => resources.Add(resource));
                object result = DdfExternalApiRuntimeInvoker.InvokeAsync(
                    i2c.Adapter, "iot.exchange", new object[]
                    { "data:00" }, context).AsTask().GetAwaiter().GetResult();
                Equal("data:2A", Convert.ToString(result));
                Equal("data:00", i2c.Connection.Exchanges.Single()
                    .ToCanonicalString());
                resources.Single().DisposeAsync().AsTask().GetAwaiter()
                    .GetResult();
            }
            finally { i2c.Dispose(); }

            IotFixture spi = CreateBusFixture(DdfHardwareTransportKind.Spi);
            try
            {
                spi.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                spi.Connection.ExchangeResponse = DdfIotFrame.Data(0xAA,
                    0x55);
                Equal("data:AA,55", spi.Device.ExchangeAsync("data:01,02",
                    64, CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
                Throws<InvalidOperationException>(() => spi.Device
                    .ExchangeAsync("data:00,01,02,03,04", 64,
                        CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
            }
            finally { spi.Dispose(); }
        }

        private static void GovernsPwmAndIotLifecycleRollback()
        {
            IotFixture pwm = CreatePwmFixture();
            try
            {
                pwm.Device.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                pwm.Device.WriteAsync("pwm:1000:0.25",
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal("pwm:1000:0.25", pwm.Connection.Writes.Single()
                    .ToCanonicalString());
                Throws<InvalidOperationException>(() => pwm.Device
                    .WriteAsync("pwm:50000:0.5", CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
            }
            finally { pwm.Dispose(); }

            IotFixture failed = CreateGpioFixture();
            try
            {
                failed.Connection.ApplyFailuresRemaining = 1;
                Throws<InvalidOperationException>(() => failed.Device
                    .OpenAsync(CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
                Equal(false, failed.Connection.IsOpen);
                Equal(1, failed.Connection.CloseCount);
            }
            finally { failed.Dispose(); }
        }

        private static void BridgesSystemDeviceLifecycle()
        {
            var factory = new FakeSystemDeviceEndpointFactory();
            var first = new FakeSystemDeviceEndpoint
            {
                ReadFrame = DdfIotFrame.Data(0x2A),
                ExchangeFrame = DdfIotFrame.Data(0xAA)
            };
            var second = new FakeSystemDeviceEndpoint();
            factory.Enqueue(first);
            factory.Enqueue(second);
            var connection = new DdfSystemDeviceIotConnection(factory);
            try
            {
                int inputChanges = 0;
                connection.InputChanged += (_, frame) =>
                {
                    Equal("level:1", frame.ToCanonicalString());
                    inputChanges++;
                };
                connection.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                connection.ApplyProfileAsync(new DdfI2cProfile("sensor", 1,
                    0x20, 32, 4), CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                Equal("data:2A", connection.ReadAsync(32,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult()
                    .ToCanonicalString());
                connection.WriteAsync(DdfIotFrame.Data(1, 2),
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Equal("data:01,02", first.Writes.Single()
                    .ToCanonicalString());
                Equal("data:AA", connection.ExchangeAsync(
                    DdfIotFrame.Data(3), CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult().ToCanonicalString());
                first.RaiseInput(DdfIotFrame.Digital(true));
                connection.ApplyProfileAsync(new DdfI2cProfile("sensor2", 2,
                    0x21), CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult();
                Equal(true, first.Disposed);
                Equal(false, second.Disposed);
                Equal(1, inputChanges);
                connection.CloseAsync().AsTask().GetAwaiter().GetResult();
                Equal(true, second.Disposed);
            }
            finally
            {
                connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private static void PreservesSystemDeviceEndpointOnProfileFailure()
        {
            var factory = new FakeSystemDeviceEndpointFactory();
            var endpoint = new FakeSystemDeviceEndpoint
            {
                ReadFrame = DdfIotFrame.Data(0x44)
            };
            factory.Enqueue(endpoint);
            var connection = new DdfSystemDeviceIotConnection(factory);
            try
            {
                bool unavailable = false;
                connection.AvailabilityChanged += (_, args) =>
                    unavailable |= !args.IsAvailable;
                connection.OpenAsync(CancellationToken.None).AsTask()
                    .GetAwaiter().GetResult();
                connection.ApplyProfileAsync(new DdfI2cProfile("ok", 1,
                    0x20), CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult();
                factory.Failure = new InvalidOperationException(
                    "Provider rejected profile.");
                Throws<InvalidOperationException>(() => connection
                    .ApplyProfileAsync(new DdfI2cProfile("bad", 2, 0x21),
                        CancellationToken.None).AsTask().GetAwaiter()
                    .GetResult());
                Equal(false, endpoint.Disposed);
                Equal("data:44", connection.ReadAsync(8,
                    CancellationToken.None).AsTask().GetAwaiter().GetResult()
                    .ToCanonicalString());
                Equal(true, unavailable);
            }
            finally
            {
                connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private static void GovernsPhysicalReadLengths()
        {
            var i2c = new DdfI2cProfile("i2c", 1, 0x20, 64, 6);
            var spi = new DdfSpiProfile("spi", 0, 1, 1_000_000,
                DdfSpiMode.Mode0, 8, 128, 12);
            var gpio = new DdfGpioProfile("gpio", 17,
                DdfGpioDirection.Output, DdfGpioPullMode.None,
                initialOutputLevel: true);
            Equal(6, i2c.ReadTransferBytes);
            Equal(12, spi.ReadTransferBytes);
            Equal(true, gpio.InitialOutputLevel);
            Throws<ArgumentOutOfRangeException>(() => new DdfI2cProfile(
                "bad", 1, 0x20, 4, 5));
            Throws<ArgumentOutOfRangeException>(() => new DdfSpiProfile(
                "bad", 0, 1, 1_000_000, DdfSpiMode.Mode0, 8, 4, 5));
        }

        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        private static SerialFixture CreateSerialFixture()
        {
            var normal = new DdfSerialCommunicationProfile("normal", 115200,
                8, DdfSerialParity.None, DdfSerialStopBits.One,
                DdfSerialHandshake.None);
            var fast = new DdfSerialCommunicationProfile("fast", 230400, 8,
                DdfSerialParity.Even, DdfSerialStopBits.One,
                DdfSerialHandshake.RequestToSend);
            var options = new DdfSerialEndpointOptions("COM77", normal,
                new[] { fast }, delimiter: "\n", maxFrameBytes: 256);
            var identity = new DdfHardwareDeviceIdentity("serial.console",
                DdfHardwareTransportKind.Serial, "system.io.ports",
                "usb.serial.private", "any", "system.io.ports");
            var connection = new FakeSerialConnection();
            var device = new DdfSerialHardwareDevice(identity, options,
                connection);
            var definition = new DdfHardwareDeviceDefinition(identity,
                DdfHardwareAccessMode.Exclusive,
                DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
                DdfHardwarePermission.Write |
                DdfHardwarePermission.Configure,
                options.Profiles.Keys, maxFrameCharacters: 64,
                operationTimeout: TimeSpan.FromSeconds(1));
            DdfHardwareOperationDefinition[] operations =
            {
                new("serialAvailable", "serial.available", "serial.console",
                    DdfHardwareOperationKind.IsAvailable,
                    requiredPermissions: new[] { "hardware.probe" }),
                new("serialRead", "serial.read", "serial.console",
                    DdfHardwareOperationKind.Read,
                    requiredPermissions: new[] { "hardware.read" }),
                new("serialWrite", "serial.write", "serial.console",
                    DdfHardwareOperationKind.Write,
                    requiredPermissions: new[] { "hardware.write" }),
                new("serialUseFast", "serial.configure.fast",
                    "serial.console", DdfHardwareOperationKind.ApplyProfile,
                    "fast", requiredPermissions: new[]
                    {
                        "hardware.configure"
                    })
            };
            var adapter = new DdfGovernedHardwareAdapter("hardware.serial",
                new[] { definition }, new IDdfHardwareDevice[] { device },
                operations);
            return new SerialFixture(adapter, device, connection, options);
        }

        private static DifferentialFixture CreateDifferentialFixture(
            DdfDifferentialSerialMode mode,
            DdfSerialDirectionControl directionControl,
            TimeSpan? preTransmitDelay = null,
            TimeSpan? postTransmitDelay = null)
        {
            var communication = new DdfSerialCommunicationProfile("normal",
                115200);
            var options = new DdfSerialEndpointOptions("COM78", communication,
                delimiter: "\n", maxFrameBytes: 256);
            var identity = new DdfHardwareDeviceIdentity(
                "serial.differential", DdfHardwareTransportKind.Serial,
                "differential.test", "serial.private", "any",
                "fake.differential");
            var profile = new DdfDifferentialSerialProfile("differential",
                communication.Id, mode, directionControl, preTransmitDelay,
                postTransmitDelay);
            var connection = new FakeSerialConnection();
            var device = new DdfDifferentialSerialHardwareDevice(identity,
                options, new[] { profile }, profile.Id, connection);
            return new DifferentialFixture(device, connection);
        }

        private static MidiFixture CreateMidiFixture(bool legacy = false)
        {
            var normal = new DdfMidiEndpointProfile("normal",
                legacy ? DdfMidiApiProfile.LegacyMidi1 :
                    DdfMidiApiProfile.WindowsMidiServices,
                legacy ? DdfMidiDataFormat.Midi1ByteStream :
                    DdfMidiDataFormat.Ump,
                DdfMidiFlow.Bidirectional, new[] { 0, 2 });
            var expressive = new DdfMidiEndpointProfile("expressive",
                DdfMidiApiProfile.WindowsMidiServices,
                DdfMidiDataFormat.Ump, DdfMidiFlow.Bidirectional,
                new[] { 0, 2 }, allowSystemExclusive: true,
                maxSystemExclusiveBytes: 16,
                allowScheduledMessages: true);
            DdfMidiEndpointProfile[] profiles = legacy
                ? new[] { normal }
                : new[] { normal, expressive };
            var identity = new DdfHardwareDeviceIdentity("midi.keyboard",
                DdfHardwareTransportKind.Midi,
                legacy ? "windows.midi.legacy" :
                    "windows.midi.services",
                "endpoint.private", "win-x64",
                legacy ? "winmm" : "Windows.Devices.Midi2");
            var connection = new FakeMidiConnection();
            var device = new DdfMidiHardwareDevice(identity, profiles,
                normal.Id, connection);
            var definition = new DdfHardwareDeviceDefinition(identity,
                DdfHardwareAccessMode.Shared,
                DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
                DdfHardwarePermission.Write |
                DdfHardwarePermission.Configure,
                profiles.Select(item => item.Id), maxFrameCharacters: 128,
                operationTimeout: TimeSpan.FromSeconds(1));
            DdfHardwareOperationDefinition[] operations =
            {
                new("midiAvailable", "midi.available", "midi.keyboard",
                    DdfHardwareOperationKind.IsAvailable,
                    requiredPermissions: new[] { "hardware.probe" }),
                new("midiRead", "midi.read", "midi.keyboard",
                    DdfHardwareOperationKind.Read,
                    requiredPermissions: new[] { "hardware.read" }),
                new("midiWrite", "midi.write", "midi.keyboard",
                    DdfHardwareOperationKind.Write,
                    requiredPermissions: new[] { "hardware.write" })
            };
            var adapter = new DdfGovernedHardwareAdapter("hardware.midi",
                new[] { definition }, new IDdfHardwareDevice[] { device },
                operations);
            return new MidiFixture(adapter, device, connection);
        }

        private static ParallelFixture CreateParallelFixture()
        {
            var output = new DdfParallelPortProfile("output",
                DdfParallelProviderKind.Simulated,
                DdfParallelMode.Compatibility, DdfParallelFlow.Output,
                maxTransferBytes: 4);
            var bidirectional = new DdfParallelPortProfile("bidirectional",
                DdfParallelProviderKind.Simulated, DdfParallelMode.Ecp,
                DdfParallelFlow.Bidirectional, maxTransferBytes: 8,
                daisyChainAddress: 1, allowDeviceReset: true);
            var identity = new DdfHardwareDeviceIdentity("parallel.printer",
                DdfHardwareTransportKind.Parallel,
                "windows.ieee1284.bridge", "parallel.private", "win-x64",
                "signed.driver.private");
            var connection = new FakeParallelConnection();
            var device = new DdfParallelHardwareDevice(identity,
                new[] { output, bidirectional }, output.Id, connection);
            var definition = new DdfHardwareDeviceDefinition(identity,
                DdfHardwareAccessMode.Exclusive,
                DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
                DdfHardwarePermission.Write |
                DdfHardwarePermission.Configure,
                new[] { output.Id, bidirectional.Id },
                maxFrameCharacters: 64,
                operationTimeout: TimeSpan.FromSeconds(1));
            DdfHardwareOperationDefinition[] operations =
            {
                new("parallelAvailable", "parallel.available",
                    "parallel.printer", DdfHardwareOperationKind.IsAvailable,
                    requiredPermissions: new[] { "hardware.probe" }),
                new("parallelRead", "parallel.read", "parallel.printer",
                    DdfHardwareOperationKind.Read,
                    requiredPermissions: new[] { "hardware.read" }),
                new("parallelWrite", "parallel.write", "parallel.printer",
                    DdfHardwareOperationKind.Write,
                    requiredPermissions: new[] { "hardware.write" })
            };
            var adapter = new DdfGovernedHardwareAdapter("hardware.parallel",
                new[] { definition }, new IDdfHardwareDevice[] { device },
                operations);
            return new ParallelFixture(adapter, device, connection);
        }

        private static IotFixture CreateGpioFixture()
        {
            DdfIotEndpointProfile[] profiles =
            {
                new DdfGpioProfile("input", 17,
                    DdfGpioDirection.Input, DdfGpioPullMode.PullUp,
                    debounce: TimeSpan.FromMilliseconds(10)),
                new DdfGpioProfile("output", 17,
                    DdfGpioDirection.Output, DdfGpioPullMode.None)
            };
            return CreateIotFixture(DdfHardwareTransportKind.Gpio,
                profiles, "input");
        }

        private static IotFixture CreateBusFixture(
            DdfHardwareTransportKind transport)
        {
            DdfIotEndpointProfile profile = transport ==
                DdfHardwareTransportKind.I2c
                ? new DdfI2cProfile("bus", 1, 0x20,
                    maxTransferBytes: 4)
                : new DdfSpiProfile("bus", 0, 1, 1_000_000,
                    DdfSpiMode.Mode0, maxTransferBytes: 4);
            return CreateIotFixture(transport, new[] { profile }, profile.Id);
        }

        private static IotFixture CreatePwmFixture()
        {
            var profile = new DdfPwmProfile("pwm", 0, 1, 10, 20_000,
                1000, 0.5);
            return CreateIotFixture(DdfHardwareTransportKind.Pwm,
                new DdfIotEndpointProfile[] { profile }, profile.Id);
        }

        private static IotFixture CreateIotFixture(
            DdfHardwareTransportKind transport,
            IReadOnlyList<DdfIotEndpointProfile> profiles,
            string initialProfileId)
        {
            string suffix = transport.ToString().ToLowerInvariant();
            var identity = new DdfHardwareDeviceIdentity("iot." + suffix,
                transport, "dotnet.iot.bridge", suffix + ".private", "any",
                "provider.private");
            var connection = new FakeIotConnection();
            var device = new DdfIotHardwareDevice(identity, profiles,
                initialProfileId, connection);
            var definition = new DdfHardwareDeviceDefinition(identity,
                DdfHardwareAccessMode.Exclusive,
                DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
                DdfHardwarePermission.Write |
                DdfHardwarePermission.Configure,
                profiles.Select(item => item.Id), maxFrameCharacters: 128,
                operationTimeout: TimeSpan.FromSeconds(1));
            var operations = new List<DdfHardwareOperationDefinition>
            {
                new("iotAvailable", "iot.available", identity.LogicalId,
                    DdfHardwareOperationKind.IsAvailable,
                    requiredPermissions: new[] { "hardware.probe" }),
                new("iotRead", "iot.read", identity.LogicalId,
                    DdfHardwareOperationKind.Read,
                    requiredPermissions: new[] { "hardware.read" }),
                new("iotWrite", "iot.write", identity.LogicalId,
                    DdfHardwareOperationKind.Write,
                    requiredPermissions: new[] { "hardware.write" })
            };
            if (transport == DdfHardwareTransportKind.I2c ||
                transport == DdfHardwareTransportKind.Spi)
                operations.Add(new DdfHardwareOperationDefinition(
                    "iotExchange", "iot.exchange", identity.LogicalId,
                    DdfHardwareOperationKind.Exchange,
                    requiredPermissions: new[]
                    {
                        "hardware.read", "hardware.write"
                    }));
            var adapter = new DdfGovernedHardwareAdapter("hardware.iot." +
                suffix, new[] { definition },
                new IDdfHardwareDevice[] { device }, operations);
            return new IotFixture(adapter, device, connection);
        }

        private static DdfExternalApiInvocationContext Context(
            Action<IAsyncDisposable> register) => new(Guid.NewGuid(),
                CancellationToken.None, DateTimeOffset.UtcNow,
                registerAsyncResource: register);

        private static Fixture CreateFixture(int maxFrameCharacters = 64,
            TimeSpan? operationTimeout = null,
            TimeSpan? operationDelay = null)
        {
            var identity = new DdfHardwareDeviceIdentity("panel.serial",
                DdfHardwareTransportKind.Serial, "loopback.provider",
                "COM99::VID_0000", "any", "driver.private");
            var definition = new DdfHardwareDeviceDefinition(identity,
                DdfHardwareAccessMode.Exclusive,
                DdfHardwarePermission.Probe | DdfHardwarePermission.Read |
                DdfHardwarePermission.Write |
                DdfHardwarePermission.Configure,
                new[] { "fast" }, maxFrameCharacters, operationTimeout);
            var device = new DdfLoopbackHardwareDevice(identity,
                new[] { "fast" }, operationDelay: operationDelay);
            DdfHardwareOperationDefinition[] operations =
            {
                new("deviceAvailable", "loop.available", "panel.serial",
                    DdfHardwareOperationKind.IsAvailable,
                    requiredPermissions: new[] { "hardware.probe" }),
                new("readFrame", "loop.read", "panel.serial",
                    DdfHardwareOperationKind.Read,
                    requiredPermissions: new[] { "hardware.read" }),
                new("writeFrame", "loop.write", "panel.serial",
                    DdfHardwareOperationKind.Write,
                    requiredPermissions: new[] { "hardware.write" }),
                new("applyFastProfile", "loop.configure", "panel.serial",
                    DdfHardwareOperationKind.ApplyProfile, "fast",
                    requiredPermissions: new[] { "hardware.configure" })
            };
            var adapter = new DdfGovernedHardwareAdapter("hardware.loopback",
                new[] { definition }, new[] { device }, operations);
            return new Fixture(adapter, device, new[] { definition },
                new IDdfHardwareDevice[] { device }, operations);
        }

        private static DdfEditorExecutionService CreateExecution(
            DdfGovernedHardwareAdapter adapter)
        {
            var catalog = new DdfEditorHostCatalog();
            foreach (DdfHostPermissionDefinition permission in Permissions())
            {
                catalog.RegisterPermission(permission);
                catalog.SetPermission(permission.Id, true);
            }
            catalog.RegisterFunctionLibrary(adapter.CreateHostLibrary(
                "demo.hardware", "Hardware loopback",
                requiredPermissions: new[] { "hardware.access" }));
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("hardware.ddf",
                "main() out int { ret 0; } " +
                "echo(string frame) out string { writeFrame(frame); ret readFrame(); } " +
                "configure() out bool { ret applyFastProfile(); } " +
                "available() out bool { ret deviceAvailable(); }");
            var language = new DdfLanguagePackageSession();
            return new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);
        }

        private static IReadOnlyList<DdfHostPermissionDefinition> Permissions()
            => new[]
            {
                new DdfHostPermissionDefinition("hardware.access",
                    "Hardware"),
                new DdfHostPermissionDefinition("hardware.probe",
                    "Stato dispositivo"),
                new DdfHostPermissionDefinition("hardware.read",
                    "Lettura dispositivo"),
                new DdfHostPermissionDefinition("hardware.write",
                    "Scrittura dispositivo"),
                new DdfHostPermissionDefinition("hardware.configure",
                    "Configurazione dispositivo")
            };

        private static DdfEditorExecutionCompletion Invoke(
            DdfEditorExecutionService execution, string procedure,
            IReadOnlyDictionary<string, object> arguments = null) =>
            execution.InvokeAsync(new DdfEditorProcedureRequest(procedure,
                arguments)).GetAwaiter().GetResult();

        private static string ReturnString(
            DdfEditorExecutionCompletion completion)
        {
            if (!completion.Succeeded) throw new InvalidOperationException(
                string.Join(" | ", completion.ExecutionResult.Diagnostics
                    .Select(item => item.Code + ": " + item.Message)));
            return Convert.ToString(completion.ExecutionResult.ReturnValue);
        }

        private static bool ReturnBool(
            DdfEditorExecutionCompletion completion)
        {
            if (!completion.Succeeded) throw new InvalidOperationException(
                string.Join(" | ", completion.ExecutionResult.Diagnostics
                    .Select(item => item.Code + ": " + item.Message)));
            return Convert.ToBoolean(completion.ExecutionResult.ReturnValue);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": " + exception);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected '" + expected +
                    "', got '" + actual + "'.");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name +
                ".");
        }

        private sealed class Fixture : IDisposable
        {
            internal Fixture(DdfGovernedHardwareAdapter adapter,
                DdfLoopbackHardwareDevice device,
                IReadOnlyList<DdfHardwareDeviceDefinition> definitions,
                IReadOnlyList<IDdfHardwareDevice> devices,
                IReadOnlyList<DdfHardwareOperationDefinition> operations)
            {
                Adapter = adapter;
                Device = device;
                Definitions = definitions;
                Devices = devices;
                Operations = operations;
            }

            internal DdfGovernedHardwareAdapter Adapter { get; }
            internal DdfLoopbackHardwareDevice Device { get; }
            internal IReadOnlyList<DdfHardwareDeviceDefinition> Definitions {
                get;
            }
            internal IReadOnlyList<IDdfHardwareDevice> Devices { get; }
            internal IReadOnlyList<DdfHardwareOperationDefinition> Operations {
                get;
            }
            public void Dispose() => Adapter.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class SerialFixture : IDisposable
        {
            internal SerialFixture(DdfGovernedHardwareAdapter adapter,
                DdfSerialHardwareDevice device,
                FakeSerialConnection connection,
                DdfSerialEndpointOptions options)
            {
                Adapter = adapter;
                Device = device;
                Connection = connection;
                Options = options;
            }
            internal DdfGovernedHardwareAdapter Adapter { get; }
            internal DdfSerialHardwareDevice Device { get; }
            internal FakeSerialConnection Connection { get; }
            internal DdfSerialEndpointOptions Options { get; }
            public void Dispose() => Adapter.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class DifferentialFixture : IDisposable
        {
            internal DifferentialFixture(
                DdfDifferentialSerialHardwareDevice device,
                FakeSerialConnection connection)
            {
                Device = device;
                Connection = connection;
            }
            internal DdfDifferentialSerialHardwareDevice Device { get; }
            internal FakeSerialConnection Connection { get; }
            public void Dispose() => Device.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class MidiFixture : IDisposable
        {
            internal MidiFixture(DdfGovernedHardwareAdapter adapter,
                DdfMidiHardwareDevice device, FakeMidiConnection connection)
            {
                Adapter = adapter;
                Device = device;
                Connection = connection;
            }
            internal DdfGovernedHardwareAdapter Adapter { get; }
            internal DdfMidiHardwareDevice Device { get; }
            internal FakeMidiConnection Connection { get; }
            public void Dispose() => Adapter.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class ParallelFixture : IDisposable
        {
            internal ParallelFixture(DdfGovernedHardwareAdapter adapter,
                DdfParallelHardwareDevice device,
                FakeParallelConnection connection)
            {
                Adapter = adapter;
                Device = device;
                Connection = connection;
            }
            internal DdfGovernedHardwareAdapter Adapter { get; }
            internal DdfParallelHardwareDevice Device { get; }
            internal FakeParallelConnection Connection { get; }
            public void Dispose() => Adapter.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class IotFixture : IDisposable
        {
            internal IotFixture(DdfGovernedHardwareAdapter adapter,
                DdfIotHardwareDevice device, FakeIotConnection connection)
            {
                Adapter = adapter;
                Device = device;
                Connection = connection;
            }
            internal DdfGovernedHardwareAdapter Adapter { get; }
            internal DdfIotHardwareDevice Device { get; }
            internal FakeIotConnection Connection { get; }
            public void Dispose() => Adapter.DisposeAsync().AsTask()
                .GetAwaiter().GetResult();
        }

        private sealed class FakeSerialConnection :
            IDdfDifferentialSerialPortConnection
        {
            private readonly Queue<byte[]> reads = new();
            internal List<byte[]> Writes { get; } = new();
            internal List<bool> DirectionChanges { get; } = new();
            internal DdfSerialCommunicationProfile LastProfile { get;
                private set;
            }
            internal DdfDifferentialSerialProfile LastDifferentialProfile {
                get; private set;
            }
            internal int OpenCount { get; private set; }
            internal int CloseCount { get; private set; }
            internal TimeSpan ReadDelay { get; set; }
            internal TimeSpan WriteDelay { get; set; }
            internal int DifferentialApplyFailuresRemaining { get; set; }
            public bool IsOpen { get; private set; }
            public event EventHandler<int> DataAvailable;
            public event EventHandler<DdfSerialFaultEventArgs> Faulted;

            internal void Enqueue(string value) => reads.Enqueue(
                StrictUtf8.GetBytes(value));
            internal void RaiseDataAvailable(int bytes) => DataAvailable?
                .Invoke(this, bytes);
            internal void RaiseFault(string code, bool disconnected) => Faulted?
                .Invoke(this, new DdfSerialFaultEventArgs(string.Empty, code,
                    disconnected));

            public ValueTask OpenAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsOpen = true;
                OpenCount++;
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync()
            {
                if (IsOpen)
                {
                    IsOpen = false;
                    CloseCount++;
                }
                return ValueTask.CompletedTask;
            }

            public async ValueTask<int> ReadAsync(Memory<byte> buffer,
                CancellationToken cancellationToken)
            {
                if (ReadDelay > TimeSpan.Zero)
                    await Task.Delay(ReadDelay, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!reads.TryDequeue(out byte[] value)) return 0;
                if (value.Length > buffer.Length)
                    throw new InvalidOperationException(
                        "Fake serial read buffer too small.");
                value.AsMemory().CopyTo(buffer);
                return value.Length;
            }

            public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken)
            {
                if (WriteDelay > TimeSpan.Zero)
                    await Task.Delay(WriteDelay, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Writes.Add(buffer.ToArray());
            }

            public ValueTask ApplyProfileAsync(
                DdfSerialCommunicationProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastProfile = profile;
                return ValueTask.CompletedTask;
            }

            public ValueTask ApplyDifferentialProfileAsync(
                DdfDifferentialSerialProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DifferentialApplyFailuresRemaining > 0)
                {
                    DifferentialApplyFailuresRemaining--;
                    throw new InvalidOperationException(
                        "Differential profile rejected by fake driver.");
                }
                LastDifferentialProfile = profile;
                return ValueTask.CompletedTask;
            }

            public ValueTask SetTransmitDirectionAsync(bool transmitting,
                DdfDifferentialSerialProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DirectionChanges.Add(transmitting);
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                IsOpen = false;
                reads.Clear();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeMidiConnection : IDdfMidiEndpointConnection
        {
            private readonly Queue<DdfMidiPacket> received = new();
            internal List<DdfMidiPacket> Sent { get; } = new();
            internal DdfMidiEndpointProfile LastProfile { get; private set; }
            internal int OpenCount { get; private set; }
            internal int CloseCount { get; private set; }
            public bool IsOpen { get; private set; }
            public event EventHandler<int> PacketAvailable;
            public event EventHandler<DdfMidiFaultEventArgs> Faulted;

            internal void Enqueue(DdfMidiPacket packet) => received.Enqueue(
                packet);
            internal void RaisePacketAvailable(int count) => PacketAvailable?
                .Invoke(this, count);
            internal void RaiseFault(string code, bool disconnected) => Faulted?
                .Invoke(this, new DdfMidiFaultEventArgs(string.Empty, code,
                    disconnected));

            public ValueTask OpenAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsOpen = true;
                OpenCount++;
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync()
            {
                if (IsOpen)
                {
                    IsOpen = false;
                    CloseCount++;
                }
                return ValueTask.CompletedTask;
            }

            public ValueTask<DdfMidiPacket> ReadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!received.TryDequeue(out DdfMidiPacket packet))
                    throw new InvalidOperationException(
                        "No fake MIDI packet is available.");
                return ValueTask.FromResult(packet);
            }

            public ValueTask SendAsync(DdfMidiPacket packet,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Sent.Add(packet);
                return ValueTask.CompletedTask;
            }

            public ValueTask ApplyProfileAsync(
                DdfMidiEndpointProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastProfile = profile;
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                IsOpen = false;
                received.Clear();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeParallelConnection :
            IDdfParallelPortConnection
        {
            private readonly Queue<byte[]> reads = new();
            internal List<byte[]> Writes { get; } = new();
            internal DdfParallelPortProfile LastProfile { get; private set; }
            internal DdfParallelStatus Status { get; set; }
            internal int ApplyFailuresRemaining { get; set; }
            internal int OpenCount { get; private set; }
            internal int CloseCount { get; private set; }
            internal int ResetCount { get; private set; }
            public bool IsOpen { get; private set; }
            public event EventHandler<DdfParallelStatus> StatusChanged;
            public event EventHandler<DdfParallelFaultEventArgs> Faulted;

            internal void Enqueue(params byte[] bytes) => reads.Enqueue(
                bytes.ToArray());
            internal void RaiseStatus() => StatusChanged?.Invoke(this, Status);
            internal void RaiseFault(string code, bool disconnected) => Faulted?
                .Invoke(this, new DdfParallelFaultEventArgs(string.Empty, code,
                    disconnected));

            public ValueTask OpenAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsOpen = true;
                OpenCount++;
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync()
            {
                if (IsOpen)
                {
                    IsOpen = false;
                    CloseCount++;
                }
                return ValueTask.CompletedTask;
            }

            public ValueTask<byte[]> ReadAsync(int maxBytes,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!reads.TryDequeue(out byte[] bytes))
                    throw new InvalidOperationException(
                        "No fake parallel frame is available.");
                if (bytes.Length > maxBytes)
                    throw new InvalidOperationException(
                        "Fake parallel frame exceeds requested quota.");
                return ValueTask.FromResult(bytes);
            }

            public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Writes.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            }

            public ValueTask<DdfParallelStatus> QueryStatusAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(Status);
            }

            public ValueTask ApplyProfileAsync(
                DdfParallelPortProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ApplyFailuresRemaining > 0)
                {
                    ApplyFailuresRemaining--;
                    throw new InvalidOperationException(
                        "Parallel profile rejected by fake driver.");
                }
                LastProfile = profile;
                return ValueTask.CompletedTask;
            }

            public ValueTask ResetDeviceAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ResetCount++;
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                IsOpen = false;
                reads.Clear();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeIotConnection : IDdfIotEndpointConnection
        {
            internal List<DdfIotFrame> Writes { get; } = new();
            internal List<DdfIotFrame> Exchanges { get; } = new();
            internal DdfIotFrame ReadFrame { get; set; } =
                DdfIotFrame.Digital(false);
            internal DdfIotFrame ExchangeResponse { get; set; } =
                DdfIotFrame.Data(0);
            internal DdfIotEndpointProfile LastProfile { get; private set; }
            internal int ApplyFailuresRemaining { get; set; }
            internal int CloseCount { get; private set; }
            public bool IsOpen { get; private set; }
            public event EventHandler<DdfIotFrame> InputChanged;
            public event EventHandler<DdfHardwareAvailabilityChangedEventArgs>
                AvailabilityChanged;

            internal void RaiseInput(DdfIotFrame frame) => InputChanged?
                .Invoke(this, frame);
            internal void RaiseAvailability(string logicalId, bool available,
                long generation) => AvailabilityChanged?.Invoke(this,
                    new DdfHardwareAvailabilityChangedEventArgs(logicalId,
                        available, generation));

            public ValueTask OpenAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsOpen = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync()
            {
                if (IsOpen)
                {
                    IsOpen = false;
                    CloseCount++;
                }
                return ValueTask.CompletedTask;
            }

            public ValueTask<DdfIotFrame> ReadAsync(int maxBytes,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(ReadFrame);
            }

            public ValueTask WriteAsync(DdfIotFrame frame,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Writes.Add(frame);
                return ValueTask.CompletedTask;
            }

            public ValueTask<DdfIotFrame> ExchangeAsync(DdfIotFrame frame,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exchanges.Add(frame);
                return ValueTask.FromResult(ExchangeResponse);
            }

            public ValueTask ApplyProfileAsync(DdfIotEndpointProfile profile,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ApplyFailuresRemaining > 0)
                {
                    ApplyFailuresRemaining--;
                    throw new InvalidOperationException(
                        "IoT profile rejected by fake provider.");
                }
                LastProfile = profile;
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                IsOpen = false;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeSystemDeviceEndpointFactory :
            IDdfSystemDeviceEndpointFactory
        {
            private readonly Queue<IDdfSystemDeviceEndpoint> endpoints =
                new();
            internal Exception Failure { get; set; }
            internal void Enqueue(IDdfSystemDeviceEndpoint endpoint) =>
                endpoints.Enqueue(endpoint);
            public IDdfSystemDeviceEndpoint Create(
                DdfIotEndpointProfile profile)
            {
                if (Failure != null)
                {
                    Exception failure = Failure;
                    Failure = null;
                    throw failure;
                }
                return endpoints.Dequeue();
            }
        }

        private sealed class FakeSystemDeviceEndpoint :
            IDdfSystemDeviceEndpoint
        {
            internal List<DdfIotFrame> Writes { get; } = new();
            internal DdfIotFrame ReadFrame { get; set; } =
                DdfIotFrame.Data(0);
            internal DdfIotFrame ExchangeFrame { get; set; } =
                DdfIotFrame.Data(0);
            internal bool Disposed { get; private set; }
            public event EventHandler<DdfIotFrame> InputChanged;
            internal void RaiseInput(DdfIotFrame frame) => InputChanged?
                .Invoke(this, frame);
            public DdfIotFrame Read(int maxBytes) => ReadFrame;
            public void Write(DdfIotFrame frame) => Writes.Add(frame);
            public DdfIotFrame Exchange(DdfIotFrame frame) => ExchangeFrame;
            public void Dispose() => Disposed = true;
        }
    }
}
