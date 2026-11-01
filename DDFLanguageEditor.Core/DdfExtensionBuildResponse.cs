using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionBuildArtifactKind { Assembly, Symbols, Manifest }

    // Untrusted bytes, not a validated PE or an authorized publication target.
    public sealed class DdfExtensionBuildArtifact
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        private readonly byte[] payload;

        public DdfExtensionBuildArtifact(DdfExtensionBuildArtifactKind kind,
            string relativePath, byte[] bytes)
        {
            if (!Enum.IsDefined(typeof(DdfExtensionBuildArtifactKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            RelativePath = DdfExtensionSourceSnapshot.RequireRelativePath(relativePath,
                nameof(relativePath));
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new ArgumentException("Artefatto vuoto o oltre quota.", nameof(bytes));
            payload = (byte[])bytes.Clone();
            Sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        }

        public DdfExtensionBuildArtifactKind Kind { get; }
        public string RelativePath { get; }
        public int ByteCount => payload.Length;
        public string Sha256 { get; }
        public byte[] GetBytes() => (byte[])payload.Clone();
        internal ReadOnlySpan<byte> Payload => payload;
    }

    public sealed class DdfExtensionBuildResponse
    {
        private static readonly Regex ErrorPattern = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private DdfExtensionBuildResponse(DdfExtensionBuildRequest request,
            DdfExtensionCallStatus status,
            IReadOnlyList<DdfExtensionBuildArtifact> artifacts, string errorId)
        {
            Request = request;
            Status = status;
            Artifacts = artifacts;
            ErrorId = errorId;
        }

        public static DdfExtensionBuildResponse Success(DdfExtensionBuildRequest request,
            IEnumerable<DdfExtensionBuildArtifact> artifacts)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            DdfExtensionBuildArtifact[] copy = (artifacts ??
                throw new ArgumentNullException(nameof(artifacts))).ToArray();
            if (copy.Length == 0 || copy.Length > 3 || copy.Any(item => item == null) ||
                copy.Count(item => item.Kind == DdfExtensionBuildArtifactKind.Assembly) != 1 ||
                copy.Select(item => item.Kind).Distinct().Count() != copy.Length ||
                copy.Select(item => item.RelativePath).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() != copy.Length ||
                copy.Sum(item => (long)item.ByteCount) > 16L * 1024 * 1024)
                throw new ArgumentException("Artefatti build assenti, duplicati o oltre quota.",
                    nameof(artifacts));
            return new DdfExtensionBuildResponse(request, DdfExtensionCallStatus.Succeeded,
                new ReadOnlyCollection<DdfExtensionBuildArtifact>(copy), null);
        }

        public static DdfExtensionBuildResponse Failure(DdfExtensionBuildRequest request,
            string errorId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (errorId == null || errorId.Length > 200 || !ErrorPattern.IsMatch(errorId) ||
                !errorId.StartsWith(request.Point.Id + ".error.", StringComparison.Ordinal))
                throw new ArgumentException("Errore fuori dal namespace del backend.", nameof(errorId));
            return new DdfExtensionBuildResponse(request, DdfExtensionCallStatus.Failed,
                Array.Empty<DdfExtensionBuildArtifact>(), errorId);
        }

        public string ContractVersion => DdfExtensionSourceSnapshot.ContractVersion;
        public DdfExtensionBuildRequest Request { get; }
        public DdfExtensionCallStatus Status { get; }
        public IReadOnlyList<DdfExtensionBuildArtifact> Artifacts { get; }
        public string ErrorId { get; }
    }
}
