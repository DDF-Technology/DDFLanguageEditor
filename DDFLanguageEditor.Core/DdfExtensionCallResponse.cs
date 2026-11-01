using System;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionCallStatus
    {
        Succeeded,
        Failed
    }

    // A plug-in may report a value or its own namespaced error, never a host DDFX diagnostic.
    public sealed class DdfExtensionCallResponse
    {
        private static readonly Regex ErrorPattern = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private DdfExtensionCallResponse(DdfExtensionPointDescriptor point,
            DdfExtensionCallStatus status, DdfExtensionValue value, string errorId)
        {
            Point = point ?? throw new ArgumentNullException(nameof(point));
            if (point.Kind != DdfExtensionPointKind.RuntimeOperation &&
                point.Kind != DdfExtensionPointKind.HostFunction)
                throw new ArgumentException("Risposta supportata solo per runtime o funzioni host.",
                    nameof(point));
            Status = status;
            Value = value;
            ErrorId = errorId;
        }

        public static DdfExtensionCallResponse Success(DdfExtensionPointDescriptor point,
            DdfExtensionValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value.ValidateTree();
            return new DdfExtensionCallResponse(point, DdfExtensionCallStatus.Succeeded,
                value, null);
        }

        public static DdfExtensionCallResponse Failure(DdfExtensionPointDescriptor point,
            string errorId)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (errorId == null || errorId.Length > 200 ||
                !ErrorPattern.IsMatch(errorId) ||
                !errorId.StartsWith(point.Id + ".error.", StringComparison.Ordinal))
                throw new ArgumentException("Errore non appartenente al punto di estensione.",
                    nameof(errorId));
            return new DdfExtensionCallResponse(point, DdfExtensionCallStatus.Failed,
                null, errorId);
        }

        public string ContractVersion => DdfExtensionValue.ContractVersion;
        public DdfExtensionPointDescriptor Point { get; }
        public DdfExtensionCallStatus Status { get; }
        public DdfExtensionValue Value { get; }
        public string ErrorId { get; }
    }
}
