using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionTypeShape
    {
        public DdfExtensionTypeShape(DdfExtensionValueKind kind,
            DdfExtensionTypeShape elementType = null)
        {
            if (!Enum.IsDefined(typeof(DdfExtensionValueKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if ((kind == DdfExtensionValueKind.Array) != (elementType != null))
                throw new ArgumentException("Solo un array richiede il tipo dell'elemento.",
                    nameof(elementType));
            if (elementType != null && elementType.Depth >= 8)
                throw new ArgumentException("Tipo array oltre quota di profondità.",
                    nameof(elementType));
            Kind = kind;
            ElementType = elementType;
            Depth = elementType == null ? 1 : elementType.Depth + 1;
        }

        public DdfExtensionValueKind Kind { get; }
        public DdfExtensionTypeShape ElementType { get; }
        public int Depth { get; }

        public bool Matches(DdfExtensionValue value)
        {
            if (value == null || value.Kind != Kind) return false;
            return Kind != DdfExtensionValueKind.Array ||
                value.Elements.All(ElementType.Matches);
        }
    }

    public sealed class DdfExtensionCallParameter
    {
        private static readonly Regex NamePattern = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        public DdfExtensionCallParameter(string name, DdfExtensionTypeShape type)
        {
            if (name == null || name.Length > 128 || !NamePattern.IsMatch(name))
                throw new ArgumentException("Nome del parametro non valido.", nameof(name));
            Name = name;
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public string Name { get; }
        public DdfExtensionTypeShape Type { get; }
    }

    public enum DdfExtensionCallEffect
    {
        Pure,
        HostMutation,
        ExternalEffect
    }

    public enum DdfExtensionCallContractStatus
    {
        Valid,
        WrongPoint,
        MissingArgument,
        UnexpectedArgument,
        ArgumentTypeMismatch,
        ResultTypeMismatch,
        UndeclaredError
    }

    // Immutable signature for the 1.0 scalar/array protocol.
    public sealed class DdfExtensionCallSignature
    {
        private static readonly Regex ErrorPattern = new Regex(
            "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public DdfExtensionCallSignature(DdfExtensionPointDescriptor point,
            IEnumerable<DdfExtensionCallParameter> parameters,
            DdfExtensionTypeShape resultType, DdfExtensionCallEffect effect,
            IEnumerable<string> declaredErrors)
        {
            Point = point ?? throw new ArgumentNullException(nameof(point));
            if (point.Kind != DdfExtensionPointKind.RuntimeOperation &&
                point.Kind != DdfExtensionPointKind.HostFunction)
                throw new ArgumentException("Firma disponibile solo per runtime/funzione host.",
                    nameof(point));
            ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
            if (!Enum.IsDefined(typeof(DdfExtensionCallEffect), effect))
                throw new ArgumentOutOfRangeException(nameof(effect));
            Effect = effect;
            DdfExtensionCallParameter[] entries = (parameters ??
                throw new ArgumentNullException(nameof(parameters))).ToArray();
            if (entries.Length > 64 || entries.Any(item => item == null) ||
                entries.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() !=
                    entries.Length)
                throw new ArgumentException("Parametri nulli, duplicati o oltre quota.",
                    nameof(parameters));
            Parameters = new ReadOnlyCollection<DdfExtensionCallParameter>(entries);
            string[] errors = (declaredErrors ??
                throw new ArgumentNullException(nameof(declaredErrors))).ToArray();
            if (errors.Length > 64 || errors.Any(id => id == null || id.Length > 200 ||
                !ErrorPattern.IsMatch(id) ||
                !id.StartsWith(point.Id + ".error.", StringComparison.Ordinal)) ||
                errors.Distinct(StringComparer.Ordinal).Count() != errors.Length)
                throw new ArgumentException("Errori non dichiarati nel namespace del punto.",
                    nameof(declaredErrors));
            DeclaredErrors = new ReadOnlyCollection<string>(errors);
        }

        public string ContractVersion => DdfExtensionValue.ContractVersion;
        public DdfExtensionPointDescriptor Point { get; }
        public IReadOnlyList<DdfExtensionCallParameter> Parameters { get; }
        public DdfExtensionTypeShape ResultType { get; }
        public DdfExtensionCallEffect Effect { get; }
        public IReadOnlyList<string> DeclaredErrors { get; }

        public DdfExtensionCallContractStatus CheckRequest(DdfExtensionCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!MatchesPoint(request.Point))
                return DdfExtensionCallContractStatus.WrongPoint;
            foreach (DdfExtensionCallParameter parameter in Parameters)
            {
                if (!request.Arguments.TryGetValue(parameter.Name, out DdfExtensionValue value))
                    return DdfExtensionCallContractStatus.MissingArgument;
                if (!parameter.Type.Matches(value))
                    return DdfExtensionCallContractStatus.ArgumentTypeMismatch;
            }
            return request.Arguments.Count == Parameters.Count
                ? DdfExtensionCallContractStatus.Valid
                : DdfExtensionCallContractStatus.UnexpectedArgument;
        }

        public DdfExtensionCallContractStatus CheckResponse(DdfExtensionCallResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (!MatchesPoint(response.Point))
                return DdfExtensionCallContractStatus.WrongPoint;
            if (response.Status == DdfExtensionCallStatus.Succeeded)
                return ResultType.Matches(response.Value)
                    ? DdfExtensionCallContractStatus.Valid
                    : DdfExtensionCallContractStatus.ResultTypeMismatch;
            return DeclaredErrors.Contains(response.ErrorId, StringComparer.Ordinal)
                ? DdfExtensionCallContractStatus.Valid
                : DdfExtensionCallContractStatus.UndeclaredError;
        }

        internal bool MatchesPoint(DdfExtensionPointDescriptor other) =>
            Point.Id == other.Id && Point.Kind == other.Kind &&
            Point.Version == other.Version &&
            Point.RequiredCapabilities.Count == other.RequiredCapabilities.Count &&
            !Point.RequiredCapabilities.Except(other.RequiredCapabilities,
                StringComparer.Ordinal).Any();
    }
}
