using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionSemanticSeverity { Info, Warning, Error }

    // Data reported by a worker; never an authoritative host/security diagnostic.
    public sealed class DdfExtensionSemanticDiagnostic
    {
        private static readonly Regex IdPattern = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public DdfExtensionSemanticDiagnostic(DdfExtensionSemanticRequest request,
            string id, DdfExtensionSemanticSeverity severity, string message,
            int start, int length)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (id == null || id.Length > 200 || !IdPattern.IsMatch(id) ||
                !id.StartsWith(request.Point.Id + ".diagnostic.", StringComparison.Ordinal))
                throw new ArgumentException("ID diagnostica fuori dal namespace del punto.", nameof(id));
            if (!Enum.IsDefined(typeof(DdfExtensionSemanticSeverity), severity))
                throw new ArgumentOutOfRangeException(nameof(severity));
            if (string.IsNullOrWhiteSpace(message) || message.Length > 1024 ||
                message.Any(char.IsControl))
                throw new ArgumentException("Messaggio diagnostico non valido.", nameof(message));
            if (start < 0 || length < 0 || start > request.Document.SourceText.Length ||
                length > request.Document.SourceText.Length - start)
                throw new ArgumentOutOfRangeException(nameof(start));
            Id = id;
            Request = request;
            Severity = severity;
            Message = message;
            Start = start;
            Length = length;
        }

        public string Id { get; }
        internal DdfExtensionSemanticRequest Request { get; }
        public DdfExtensionSemanticSeverity Severity { get; }
        public string Message { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfExtensionSemanticResponse
    {
        private DdfExtensionSemanticResponse(DdfExtensionSemanticRequest request,
            DdfExtensionCallStatus status,
            IReadOnlyList<DdfExtensionSemanticDiagnostic> diagnostics, string errorId)
        {
            Request = request;
            Status = status;
            Diagnostics = diagnostics;
            ErrorId = errorId;
        }

        public static DdfExtensionSemanticResponse Success(DdfExtensionSemanticRequest request,
            IEnumerable<DdfExtensionSemanticDiagnostic> diagnostics)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            DdfExtensionSemanticDiagnostic[] copy = (diagnostics ??
                throw new ArgumentNullException(nameof(diagnostics))).ToArray();
            if (copy.Length > 128 || copy.Any(item => item == null ||
                !ReferenceEquals(item.Request, request)))
                throw new ArgumentException("Diagnostiche assenti o oltre quota.", nameof(diagnostics));
            return new DdfExtensionSemanticResponse(request, DdfExtensionCallStatus.Succeeded,
                new ReadOnlyCollection<DdfExtensionSemanticDiagnostic>(copy), null);
        }

        public static DdfExtensionSemanticResponse Failure(DdfExtensionSemanticRequest request,
            string errorId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (errorId == null || errorId.Length > 200 ||
                !Regex.IsMatch(errorId, "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
                    RegexOptions.CultureInvariant) ||
                !errorId.StartsWith(request.Point.Id + ".error.", StringComparison.Ordinal))
                throw new ArgumentException("Errore fuori dal namespace del punto.", nameof(errorId));
            return new DdfExtensionSemanticResponse(request, DdfExtensionCallStatus.Failed,
                Array.Empty<DdfExtensionSemanticDiagnostic>(), errorId);
        }

        public string ContractVersion => DdfExtensionSourceSnapshot.ContractVersion;
        public DdfExtensionSemanticRequest Request { get; }
        public DdfExtensionCallStatus Status { get; }
        public IReadOnlyList<DdfExtensionSemanticDiagnostic> Diagnostics { get; }
        public string ErrorId { get; }
    }
}
