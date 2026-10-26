using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Stable identifiers for additive SDK services. Hosts must compare identifiers,
    /// not implementation types, when negotiating optional editor functionality.
    /// </summary>
    public static class DdfEditorCapabilityIds
    {
        public const string CooperativeExecution = "editor.execution.cooperative";
        public const string AsyncLifecycle = "editor.execution.async-lifecycle";
        public const string ScriptState = "editor.script-state";
        public const string ScriptStatePersistence = "editor.script-state.persistence";
        public const string ProductUpdates = "editor.product-updates.verify";
    }

    public sealed class DdfEditorCapabilityDescriptor
    {
        public DdfEditorCapabilityDescriptor(string id, string displayName,
            string introducedInContractVersion)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("L'identificatore capability è obbligatorio.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Il nome capability è obbligatorio.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(introducedInContractVersion))
                throw new ArgumentException("La versione SDK è obbligatoria.",
                    nameof(introducedInContractVersion));
            Id = id;
            DisplayName = displayName;
            IntroducedInContractVersion = introducedInContractVersion;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string IntroducedInContractVersion { get; }
    }

    /// <summary>
    /// Result of a non-throwing capability negotiation. Issue is null only when
    /// the requested capability is available.
    /// </summary>
    public sealed class DdfEditorCapabilityResult
    {
        internal DdfEditorCapabilityResult(string requestedId,
            DdfEditorCapabilityDescriptor descriptor, object service,
            DdfBuildIssue issue)
        {
            RequestedId = requestedId ?? string.Empty;
            Descriptor = descriptor;
            Service = service;
            Issue = issue;
        }

        public string RequestedId { get; }
        public DdfEditorCapabilityDescriptor Descriptor { get; }
        public object Service { get; }
        public DdfBuildIssue Issue { get; }
        public bool IsAvailable => Descriptor != null && Service != null && Issue == null;

        public bool TryGetService<TService>(out TService service)
            where TService : class
        {
            service = Service as TService;
            return IsAvailable && service != null;
        }
    }

    /// <summary>
    /// Optional interface implemented by SDK surfaces that support capability
    /// discovery. It is deliberately not a member of IDdfEditorSurface so binaries
    /// compiled against an earlier contract remain valid.
    /// </summary>
    public interface IDdfEditorCapabilityProvider
    {
        IReadOnlyList<DdfEditorCapabilityDescriptor> KnownCapabilities { get; }
        DdfEditorCapabilityResult InspectCapability(string capabilityId);
    }

    public static class DdfEditorCapabilityCatalog
    {
        public const string UnavailableDiagnosticCode = "DDFH006";
        public const string UnknownDiagnosticCode = "DDFH007";

        private static readonly IReadOnlyList<DdfEditorCapabilityDescriptor> known =
            new ReadOnlyCollection<DdfEditorCapabilityDescriptor>(new[]
            {
                new DdfEditorCapabilityDescriptor(
                    DdfEditorCapabilityIds.ScriptState,
                    "Committed script state", "0.35"),
                new DdfEditorCapabilityDescriptor(
                    DdfEditorCapabilityIds.ScriptStatePersistence,
                    "Script state persistence", "0.36"),
                new DdfEditorCapabilityDescriptor(
                    DdfEditorCapabilityIds.CooperativeExecution,
                    "Cooperative execution", "0.40"),
                new DdfEditorCapabilityDescriptor(
                    DdfEditorCapabilityIds.AsyncLifecycle,
                    "Asynchronous lifecycle", "0.41"),
                new DdfEditorCapabilityDescriptor(
                    DdfEditorCapabilityIds.ProductUpdates,
                    "Host-controlled product update verification", "0.42")
            });

        public static IReadOnlyList<DdfEditorCapabilityDescriptor> Known => known;

        public static DdfEditorCapabilityDescriptor Find(string capabilityId) =>
            known.FirstOrDefault(item => string.Equals(item.Id,
                capabilityId, StringComparison.Ordinal));

        public static DdfEditorCapabilityResult Available(string capabilityId,
            object service)
        {
            DdfEditorCapabilityDescriptor descriptor = Find(capabilityId);
            if (descriptor == null) return Unknown(capabilityId);
            if (service == null) return Unavailable(capabilityId);
            return new DdfEditorCapabilityResult(capabilityId, descriptor,
                service, null);
        }

        public static DdfEditorCapabilityResult Unavailable(string capabilityId)
        {
            DdfEditorCapabilityDescriptor descriptor = Find(capabilityId);
            if (descriptor == null) return Unknown(capabilityId);
            return new DdfEditorCapabilityResult(capabilityId, descriptor, null,
                DdfBuildIssue.Configuration(UnavailableDiagnosticCode,
                    "La capability SDK '" + capabilityId +
                    "' non è disponibile nel servizio associato.", null,
                    DdfDiagnosticDomain.EngineCapability));
        }

        public static DdfEditorCapabilityResult Unknown(string capabilityId) =>
            new DdfEditorCapabilityResult(capabilityId, null, null,
                DdfBuildIssue.Configuration(UnknownDiagnosticCode,
                    "La capability SDK richiesta non è riconosciuta: '" +
                    (capabilityId ?? string.Empty) + "'.", null,
                    DdfDiagnosticDomain.EngineCapability));
    }

    /// <summary>
    /// Typed failure used only by imperative compatibility methods. New hosts should
    /// call InspectCapability and handle its stable DDF issue without exceptions.
    /// </summary>
    public sealed class DdfEditorCapabilityUnavailableException : NotSupportedException
    {
        public DdfEditorCapabilityUnavailableException(
            DdfEditorCapabilityResult result) : base(
                (result ?? throw new ArgumentNullException(nameof(result))).Issue?.Message ??
                "La capability SDK richiesta non è disponibile.")
        {
            Result = result;
        }

        public DdfEditorCapabilityResult Result { get; }
        public string Code => Result.Issue?.Code ?? string.Empty;
        public string CapabilityId => Result.RequestedId;
    }
}
