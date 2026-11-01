using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    // Metadata only: registering a point never loads or invokes plug-in code.
    public enum DdfExtensionPointKind
    {
        SemanticOperation,
        RuntimeOperation,
        HostFunction,
        BuildBackend
    }

    public sealed class DdfExtensionPointDescriptor
    {
        public const string ApiVersion = "1.0";
        private static readonly Regex IdPattern = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public DdfExtensionPointDescriptor(string id, DdfExtensionPointKind kind,
            string apiVersion, IEnumerable<string> requiredCapabilities)
        {
            if (id == null || id.Length > 160 || !IdPattern.IsMatch(id))
                throw new ArgumentException("ID del punto di estensione non valido.", nameof(id));
            if (!Enum.IsDefined(typeof(DdfExtensionPointKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (apiVersion != ApiVersion)
                throw new ArgumentException("Versione API del punto di estensione non supportata.",
                    nameof(apiVersion));
            string[] capabilities = (requiredCapabilities ??
                throw new ArgumentNullException(nameof(requiredCapabilities))).ToArray();
            if (capabilities.Length > 64 ||
                capabilities.Distinct(StringComparer.Ordinal).Count() != capabilities.Length ||
                capabilities.Any(value => !DdfExtensionPluginCapabilityCatalog.TryGet(value, out _)))
                throw new ArgumentException("Capability non valide o duplicate.",
                    nameof(requiredCapabilities));
            Id = id;
            Kind = kind;
            Version = apiVersion;
            RequiredCapabilities = new ReadOnlyCollection<string>(capabilities);
        }

        public string Id { get; }
        public DdfExtensionPointKind Kind { get; }
        public string Version { get; }
        public IReadOnlyList<string> RequiredCapabilities { get; }
    }

    public sealed class DdfExtensionPointCatalog
    {
        public const string SchemaVersion = "1.0";

        public DdfExtensionPointCatalog(DdfExtensionPluginManifest manifest,
            IEnumerable<DdfExtensionPointDescriptor> points)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            DdfExtensionPointDescriptor[] items = (points ??
                throw new ArgumentNullException(nameof(points))).ToArray();
            if (items.Length > 64 || items.Any(item => item == null) ||
                items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("Punti di estensione nulli, duplicati o oltre quota.",
                    nameof(points));
            string prefix = manifest.Id + ".";
            if (items.Any(item => !item.Id.StartsWith(prefix, StringComparison.Ordinal) ||
                item.RequiredCapabilities.Except(manifest.Capabilities, StringComparer.Ordinal).Any()))
                throw new ArgumentException("Punto esterno all'identità o alle capability del manifest.",
                    nameof(points));
            Points = new ReadOnlyCollection<DdfExtensionPointDescriptor>(items);
        }

        public DdfExtensionPluginManifest Manifest { get; }
        public IReadOnlyList<DdfExtensionPointDescriptor> Points { get; }

        public bool TryGet(string id, out DdfExtensionPointDescriptor point)
        {
            point = Points.FirstOrDefault(item => item.Id == id);
            return point != null;
        }

        public string ToJson() => JsonSerializer.Serialize(new
        {
            schemaVersion = SchemaVersion,
            pluginId = Manifest.Id,
            pluginVersion = Manifest.Version,
            points = Points.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item => new
            {
                id = item.Id,
                kind = item.Kind.ToString(),
                apiVersion = item.Version,
                requiredCapabilities = item.RequiredCapabilities.OrderBy(value => value,
                    StringComparer.Ordinal)
            })
        });
    }
}
