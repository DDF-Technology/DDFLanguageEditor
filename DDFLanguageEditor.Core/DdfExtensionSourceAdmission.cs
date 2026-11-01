using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionHostMode { Standalone, Embedded }

    public enum DdfExtensionSourceAdmissionDecision
    {
        PointNotInCatalog,
        EmbeddedBuildDenied,
        ReadDenied,
        WriteDenied,
        Authorized
    }

    public sealed class DdfExtensionSourceAdmissionResult
    {
        internal DdfExtensionSourceAdmissionResult(DdfExtensionSourceAdmissionDecision decision,
            DdfExtensionPluginOperationResult read,
            DdfExtensionPluginOperationResult write)
        {
            Decision = decision;
            Read = read;
            Write = write;
        }

        public DdfExtensionSourceAdmissionDecision Decision { get; }
        public DdfExtensionPluginOperationResult Read { get; }
        public DdfExtensionPluginOperationResult Write { get; }
        public bool IsAuthorized => Decision == DdfExtensionSourceAdmissionDecision.Authorized;
    }

    // Admission snapshot only. The physical host boundary must recheck live grants.
    public static class DdfExtensionSourceAdmission
    {
        public static DdfExtensionSourceAdmissionResult EvaluateSemantic(
            DdfExtensionSemanticRequest request, DdfVerifiedExtensionCatalog verified,
            DdfExtensionPluginSignature signature, DdfExtensionPluginPublisherTrust trust,
            byte[] assemblyBytes, string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string readResourceId) => EvaluateSemantic(request,
                (verified ?? throw new ArgumentNullException(nameof(verified))).Points,
                signature, trust, assemblyBytes, sdkContract, grant,
                resourceGrants, readResourceId);

        public static DdfExtensionSourceAdmissionResult EvaluateBuild(
            DdfExtensionBuildRequest request, DdfExtensionHostMode mode,
            DdfVerifiedExtensionCatalog verified,
            DdfExtensionPluginSignature signature, DdfExtensionPluginPublisherTrust trust,
            byte[] assemblyBytes, string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string readResourceId, string writeResourceId) => EvaluateBuild(request,
                mode, (verified ?? throw new ArgumentNullException(nameof(verified))).Points,
                signature, trust, assemblyBytes, sdkContract, grant, resourceGrants,
                readResourceId, writeResourceId);

        public static DdfExtensionSourceAdmissionResult EvaluateSemantic(
            DdfExtensionSemanticRequest request, DdfExtensionPointCatalog catalog,
            DdfExtensionPluginSignature signature, DdfExtensionPluginPublisherTrust trust,
            byte[] assemblyBytes, string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string readResourceId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!ContainsPoint(catalog, request.Point, build: false))
                return Deny(DdfExtensionSourceAdmissionDecision.PointNotInCatalog);
            DdfExtensionPluginResourceGrant[] scopes = Snapshot(resourceGrants);
            DdfExtensionPluginOperationResult read = Check(catalog, signature, trust,
                assemblyBytes, sdkContract, grant, scopes,
                "file.read", readResourceId);
            return new DdfExtensionSourceAdmissionResult(read?.IsAuthorized == true
                ? DdfExtensionSourceAdmissionDecision.Authorized
                : DdfExtensionSourceAdmissionDecision.ReadDenied, read, null);
        }

        public static DdfExtensionSourceAdmissionResult EvaluateBuild(
            DdfExtensionBuildRequest request, DdfExtensionHostMode mode,
            DdfExtensionPointCatalog catalog, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust, byte[] assemblyBytes,
            string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string readResourceId, string writeResourceId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!Enum.IsDefined(typeof(DdfExtensionHostMode), mode) ||
                mode != DdfExtensionHostMode.Standalone)
                return Deny(DdfExtensionSourceAdmissionDecision.EmbeddedBuildDenied);
            if (!ContainsPoint(catalog, request.Point, build: true))
                return Deny(DdfExtensionSourceAdmissionDecision.PointNotInCatalog);
            DdfExtensionPluginResourceGrant[] scopes = Snapshot(resourceGrants);
            DdfExtensionPluginOperationResult read = Check(catalog, signature, trust,
                assemblyBytes, sdkContract, grant, scopes,
                "file.read", readResourceId);
            if (read?.IsAuthorized != true)
                return new DdfExtensionSourceAdmissionResult(
                    DdfExtensionSourceAdmissionDecision.ReadDenied, read, null);
            DdfExtensionPluginOperationResult write = Check(catalog, signature, trust,
                assemblyBytes, sdkContract, grant, scopes,
                "file.write", writeResourceId);
            return new DdfExtensionSourceAdmissionResult(write?.IsAuthorized == true
                ? DdfExtensionSourceAdmissionDecision.Authorized
                : DdfExtensionSourceAdmissionDecision.WriteDenied, read, write);
        }

        private static DdfExtensionPluginOperationResult Check(
            DdfExtensionPointCatalog catalog, DdfExtensionPluginSignature signature,
            DdfExtensionPluginPublisherTrust trust, byte[] assemblyBytes,
            string sdkContract, DdfExtensionPluginGrant grant,
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants,
            string capability, string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return null;
            return DdfExtensionPluginResourcePolicy.Evaluate(catalog.Manifest,
                signature, trust, assemblyBytes, sdkContract, grant,
                resourceGrants, capability, resourceId);
        }

        private static bool ContainsPoint(DdfExtensionPointCatalog catalog,
            DdfExtensionPointDescriptor point, bool build) =>
            catalog.TryGet(point.Id, out DdfExtensionPointDescriptor declared) &&
            ReferenceEquals(declared, point) &&
            point.RequiredCapabilities.All(capability =>
                capability == "file.read" || (build && capability == "file.write"));

        private static DdfExtensionPluginResourceGrant[] Snapshot(
            IEnumerable<DdfExtensionPluginResourceGrant> resourceGrants)
        {
            DdfExtensionPluginResourceGrant[] scopes =
                (resourceGrants ?? Array.Empty<DdfExtensionPluginResourceGrant>())
                .Take(129).ToArray();
            if (scopes.Length > 128 || scopes.Any(item => item == null))
                return Array.Empty<DdfExtensionPluginResourceGrant>();
            return scopes;
        }

        private static DdfExtensionSourceAdmissionResult Deny(
            DdfExtensionSourceAdmissionDecision decision) =>
            new DdfExtensionSourceAdmissionResult(decision, null, null);
    }
}
