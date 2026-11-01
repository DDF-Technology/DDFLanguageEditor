using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionPluginCapabilityDomain
    {
        File,
        Network,
        Process,
        Device,
        Ui,
        Native
    }

    public sealed class DdfExtensionPluginCapabilityDefinition
    {
        internal DdfExtensionPluginCapabilityDefinition(string name,
            DdfExtensionPluginCapabilityDomain domain, string operation,
            bool requiresDedicatedWorker)
        {
            Name = name;
            Domain = domain;
            Operation = operation;
            RequiresDedicatedWorker = requiresDedicatedWorker;
        }

        public string Name { get; }
        public DdfExtensionPluginCapabilityDomain Domain { get; }
        public string Operation { get; }
        // False does not imply that executing untrusted plug-in code in-process is safe.
        public bool RequiresDedicatedWorker { get; }
    }

    public static class DdfExtensionPluginCapabilityCatalog
    {
        public const string Version = "1.0";
        private static readonly IReadOnlyList<DdfExtensionPluginCapabilityDefinition> Definitions =
            new ReadOnlyCollection<DdfExtensionPluginCapabilityDefinition>(new[]
            {
                Define("device.configure", DdfExtensionPluginCapabilityDomain.Device, "configure endpoint", true),
                Define("device.read", DdfExtensionPluginCapabilityDomain.Device, "read endpoint", false),
                Define("device.write", DdfExtensionPluginCapabilityDomain.Device, "write endpoint", false),
                Define("file.delete", DdfExtensionPluginCapabilityDomain.File, "delete host-owned item", true),
                Define("file.list", DdfExtensionPluginCapabilityDomain.File, "list host-owned collection", false),
                Define("file.read", DdfExtensionPluginCapabilityDomain.File, "read host-owned item", false),
                Define("file.write", DdfExtensionPluginCapabilityDomain.File, "write host-owned item", false),
                Define("native.invoke", DdfExtensionPluginCapabilityDomain.Native, "invoke approved native adapter", true),
                Define("native.load", DdfExtensionPluginCapabilityDomain.Native, "load approved native library", true),
                Define("network.connect", DdfExtensionPluginCapabilityDomain.Network, "connect to host endpoint", false),
                Define("network.listen", DdfExtensionPluginCapabilityDomain.Network, "listen on host endpoint", true),
                Define("network.publish", DdfExtensionPluginCapabilityDomain.Network, "publish to host channel", false),
                Define("network.subscribe", DdfExtensionPluginCapabilityDomain.Network, "subscribe to host channel", false),
                Define("process.inspect", DdfExtensionPluginCapabilityDomain.Process, "inspect approved process", true),
                Define("process.signal", DdfExtensionPluginCapabilityDomain.Process, "signal approved process", true),
                Define("process.start", DdfExtensionPluginCapabilityDomain.Process, "start approved process", true),
                Define("ui.command", DdfExtensionPluginCapabilityDomain.Ui, "register host command", false),
                Define("ui.dialog", DdfExtensionPluginCapabilityDomain.Ui, "request host dialog", false),
                Define("ui.panel", DdfExtensionPluginCapabilityDomain.Ui, "register host panel", false)
            });
        private static readonly IReadOnlyDictionary<string, DdfExtensionPluginCapabilityDefinition> ByName =
            new ReadOnlyDictionary<string, DdfExtensionPluginCapabilityDefinition>(
                Definitions.ToDictionary(item => item.Name, StringComparer.Ordinal));

        public static IReadOnlyList<DdfExtensionPluginCapabilityDefinition> All => Definitions;

        public static bool TryGet(string name, out DdfExtensionPluginCapabilityDefinition definition)
        {
            if (name == null)
            {
                definition = null;
                return false;
            }
            return ByName.TryGetValue(name, out definition);
        }

        public static string ToJson() => JsonSerializer.Serialize(new
        {
            schemaVersion = Version,
            capabilities = Definitions.Select(item => new
            {
                name = item.Name,
                domain = item.Domain.ToString().ToLowerInvariant(),
                operation = item.Operation,
                requiresDedicatedWorker = item.RequiresDedicatedWorker
            })
        });

        private static DdfExtensionPluginCapabilityDefinition Define(string name,
            DdfExtensionPluginCapabilityDomain domain, string operation,
            bool requiresDedicatedWorker) =>
            new DdfExtensionPluginCapabilityDefinition(name, domain, operation,
                requiresDedicatedWorker);
    }
}
