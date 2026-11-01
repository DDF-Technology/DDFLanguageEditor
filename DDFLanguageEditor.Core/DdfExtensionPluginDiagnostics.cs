using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionPluginDiagnosticDomain
    {
        Manifest,
        PublisherTrust,
        Compatibility,
        Dependency,
        Authorization,
        Resource,
        Audit,
        Loading,
        Execution
    }

    public sealed class DdfExtensionPluginDiagnostic
    {
        public DdfExtensionPluginDiagnostic(string code,
            DdfExtensionPluginDiagnosticDomain domain, string message)
        {
            if (code == null || code.Length != 7 ||
                !code.StartsWith("DDFX", StringComparison.Ordinal) ||
                code.Skip(4).Any(character => character < '0' || character > '9'))
                throw new ArgumentException("Codice diagnostico plug-in non valido.", nameof(code));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Messaggio diagnostico richiesto.", nameof(message));
            Code = code;
            Domain = domain;
            Message = message;
        }

        public string Code { get; }
        public DdfExtensionPluginDiagnosticDomain Domain { get; }
        public string Message { get; }
        public override string ToString() => Code + " [Plug-in / " + Domain + "]: " + Message;
    }

    public sealed class DdfExtensionPluginCheck<T> where T : class
    {
        internal DdfExtensionPluginCheck(T value,
            IEnumerable<DdfExtensionPluginDiagnostic> diagnostics)
        {
            Value = value;
            Diagnostics = new ReadOnlyCollection<DdfExtensionPluginDiagnostic>(
                diagnostics.ToArray());
        }

        public T Value { get; }
        public IReadOnlyList<DdfExtensionPluginDiagnostic> Diagnostics { get; }
        public bool Success => Value != null && Diagnostics.Count == 0;
    }

    public static class DdfExtensionPluginPreflight
    {
        public static DdfExtensionPluginCheck<DdfExtensionPluginManifest> ParseManifest(string json) =>
            Parse(() => DdfExtensionPluginManifestCodec.Parse(json), "DDFX001",
                DdfExtensionPluginDiagnosticDomain.Manifest, "Manifest plug-in non valido.");

        public static DdfExtensionPluginCheck<DdfExtensionPluginSignature> ParseSignature(string json) =>
            Parse(() => DdfExtensionPluginSignatureCodec.Parse(json), "DDFX002",
                DdfExtensionPluginDiagnosticDomain.PublisherTrust,
                "Firma plug-in non valida.");

        public static DdfExtensionPluginCheck<DdfExtensionPluginDependencyLockFile> ParseLock(string json) =>
            Parse(() => DdfExtensionPluginDependencyLockCodec.Parse(json), "DDFX003",
                DdfExtensionPluginDiagnosticDomain.Dependency,
                "Lock delle dipendenze plug-in non valido.");

        public static DdfExtensionPluginCheck<DdfExtensionPluginManifest> VerifyArtifact(
            string manifestJson, string signatureJson, byte[] assemblyBytes,
            DdfExtensionPluginPublisherTrust trust, string sdkContract)
        {
            DdfExtensionPluginCheck<DdfExtensionPluginManifest> manifest =
                ParseManifest(manifestJson);
            if (!manifest.Success) return manifest;
            DdfExtensionPluginCheck<DdfExtensionPluginSignature> signature =
                ParseSignature(signatureJson);
            if (!signature.Success)
                return Fail<DdfExtensionPluginManifest>(signature.Diagnostics[0]);
            if (trust == null)
                return Fail<DdfExtensionPluginManifest>("DDFX101",
                    DdfExtensionPluginDiagnosticDomain.PublisherTrust,
                    "Nessun editore attendibile configurato.");
            if (string.IsNullOrWhiteSpace(sdkContract))
                return Fail<DdfExtensionPluginManifest>("DDFX103",
                    DdfExtensionPluginDiagnosticDomain.Compatibility,
                    "Contratto SDK richiesto.");
            bool compatible;
            try { compatible = manifest.Value.SupportsSdkContract(sdkContract); }
            catch (ArgumentException) { compatible = false; }
            if (!compatible)
                return Fail<DdfExtensionPluginManifest>("DDFX103",
                    DdfExtensionPluginDiagnosticDomain.Compatibility,
                    "Contratto SDK incompatibile.");
            DdfExtensionPluginSignatureStatus status =
                DdfExtensionPluginSignatureVerifier.Verify(manifest.Value,
                    signature.Value, trust);
            if (status != DdfExtensionPluginSignatureStatus.Verified)
                return Fail<DdfExtensionPluginManifest>("DDFX101",
                    DdfExtensionPluginDiagnosticDomain.PublisherTrust,
                    "Firma o chiave editore non verificata: " + status + ".");
            if (assemblyBytes == null || assemblyBytes.Length > 256 * 1024 * 1024 ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(assemblyBytes)),
                    manifest.Value.AssemblySha256, StringComparison.OrdinalIgnoreCase))
                return Fail<DdfExtensionPluginManifest>("DDFX102",
                    DdfExtensionPluginDiagnosticDomain.PublisherTrust,
                    "Hash assembly non corrispondente.");
            return manifest;
        }

        public static IReadOnlyList<DdfExtensionPluginDiagnostic> FromDependencyResolution(
            DdfExtensionPluginDependencyResolution result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return ConvertIssues(result.Issues);
        }

        public static IReadOnlyList<DdfExtensionPluginDiagnostic> FromGraphAdmission(
            DdfExtensionPluginGraphAdmissionResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return ConvertIssues(result.Issues);
        }

        public static IReadOnlyList<DdfExtensionPluginDiagnostic> FromOperation(
            DdfExtensionPluginOperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.IsAuthorized) return Array.Empty<DdfExtensionPluginDiagnostic>();
            DdfExtensionPluginDiagnosticDomain domain = result.SecurityDomain switch
            {
                DdfExtensionPluginSecurityDomain.PublisherTrust =>
                    DdfExtensionPluginDiagnosticDomain.PublisherTrust,
                DdfExtensionPluginSecurityDomain.Compatibility =>
                    DdfExtensionPluginDiagnosticDomain.Compatibility,
                DdfExtensionPluginSecurityDomain.Resource =>
                    DdfExtensionPluginDiagnosticDomain.Resource,
                DdfExtensionPluginSecurityDomain.Audit =>
                    DdfExtensionPluginDiagnosticDomain.Audit,
                _ => DdfExtensionPluginDiagnosticDomain.Authorization
            };
            return new[] { new DdfExtensionPluginDiagnostic(result.SecurityCode,
                domain, "Operazione plug-in negata: " + result.Decision + ".") };
        }

        private static DdfExtensionPluginCheck<T> Parse<T>(Func<T> parse, string code,
            DdfExtensionPluginDiagnosticDomain domain, string message) where T : class
        {
            try { return new DdfExtensionPluginCheck<T>(parse(), Array.Empty<DdfExtensionPluginDiagnostic>()); }
            catch (Exception exception) when (exception is JsonException ||
                exception is ArgumentException || exception is FormatException ||
                exception is InvalidOperationException || exception is KeyNotFoundException)
            {
                return Fail<T>(code, domain, message);
            }
        }

        private static IReadOnlyList<DdfExtensionPluginDiagnostic> ConvertIssues(
            IEnumerable<string> issues) => new ReadOnlyCollection<DdfExtensionPluginDiagnostic>(
                issues.Select(issue =>
                {
                    string code = issue.Length >= 7 ? issue.Substring(0, 7) : string.Empty;
                    if (!code.StartsWith("DDFX", StringComparison.Ordinal) ||
                        code.Length != 7 || code.Skip(4).Any(character =>
                            character < '0' || character > '9'))
                        code = "DDFX399";
                    DdfExtensionPluginDiagnosticDomain domain = code[4] == '4'
                        ? DdfExtensionPluginDiagnosticDomain.Authorization
                        : DdfExtensionPluginDiagnosticDomain.Dependency;
                    return new DdfExtensionPluginDiagnostic(code, domain, issue);
                }).ToArray());

        private static DdfExtensionPluginCheck<T> Fail<T>(string code,
            DdfExtensionPluginDiagnosticDomain domain, string message) where T : class =>
            Fail<T>(new DdfExtensionPluginDiagnostic(code, domain, message));

        private static DdfExtensionPluginCheck<T> Fail<T>(
            DdfExtensionPluginDiagnostic diagnostic) where T : class =>
            new DdfExtensionPluginCheck<T>(null, new[] { diagnostic });
    }
}
