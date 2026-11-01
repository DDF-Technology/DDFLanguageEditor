using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionValueKind
    {
        Null,
        Boolean,
        Integer,
        Float,
        String,
        Array
    }

    // A deliberately small, Roslyn-free value boundary. No host or VM object crosses it.
    public sealed class DdfExtensionValue
    {
        public const string ContractVersion = "1.0";
        private const int MaximumNodes = 1024;
        private readonly bool boolean;
        private readonly int integer;
        private readonly double floating;
        private readonly string text;
        private readonly IReadOnlyList<DdfExtensionValue> elements;

        private DdfExtensionValue(DdfExtensionValueKind kind, bool boolean = false,
            int integer = 0, double floating = 0, string text = null,
            IReadOnlyList<DdfExtensionValue> elements = null)
        {
            Kind = kind;
            this.boolean = boolean;
            this.integer = integer;
            this.floating = floating;
            this.text = text;
            this.elements = elements;
        }

        public static DdfExtensionValue Null { get; } = new DdfExtensionValue(DdfExtensionValueKind.Null);
        public static DdfExtensionValue FromBoolean(bool value) =>
            new DdfExtensionValue(DdfExtensionValueKind.Boolean, boolean: value);
        public static DdfExtensionValue FromInteger(int value) =>
            new DdfExtensionValue(DdfExtensionValueKind.Integer, integer: value);
        public static DdfExtensionValue FromFloat(double value) => double.IsFinite(value)
            ? new DdfExtensionValue(DdfExtensionValueKind.Float, floating: value)
            : throw new ArgumentOutOfRangeException(nameof(value), "Valore non finito.");
        public static DdfExtensionValue FromString(string value) =>
            value != null && value.Length <= 4096
                ? new DdfExtensionValue(DdfExtensionValueKind.String, text: value)
                : throw new ArgumentException("Stringa nulla o oltre quota.", nameof(value));

        public static DdfExtensionValue FromArray(IEnumerable<DdfExtensionValue> values)
        {
            DdfExtensionValue[] copy = (values ?? throw new ArgumentNullException(nameof(values)))
                .ToArray();
            if (copy.Length > 256 || copy.Any(value => value == null))
                throw new ArgumentException("Array con elementi nulli o oltre quota.", nameof(values));
            var candidate = new DdfExtensionValue(DdfExtensionValueKind.Array,
                elements: new ReadOnlyCollection<DdfExtensionValue>(copy));
            candidate.ValidateTree();
            return candidate;
        }

        public DdfExtensionValueKind Kind { get; }
        public bool Boolean => Kind == DdfExtensionValueKind.Boolean ? boolean : WrongKind<bool>();
        public int Integer => Kind == DdfExtensionValueKind.Integer ? integer : WrongKind<int>();
        public double Float => Kind == DdfExtensionValueKind.Float ? floating : WrongKind<double>();
        public string String => Kind == DdfExtensionValueKind.String ? text : WrongKind<string>();
        public IReadOnlyList<DdfExtensionValue> Elements => Kind == DdfExtensionValueKind.Array
            ? elements : WrongKind<IReadOnlyList<DdfExtensionValue>>();

        internal void ValidateTree()
        {
            int remaining = MaximumNodes;
            ValidateTree(1, ref remaining);
        }

        internal void ValidateTree(ref int remaining) => ValidateTree(1, ref remaining);

        private void ValidateTree(int depth, ref int remaining)
        {
            if (depth > 8 || --remaining < 0)
                throw new ArgumentException("Valore DDF oltre quota di profondità o dimensione.");
            if (Kind == DdfExtensionValueKind.Array)
                foreach (DdfExtensionValue element in elements)
                    element.ValidateTree(depth + 1, ref remaining);
        }

        private static T WrongKind<T>() => throw new InvalidOperationException(
            "Il valore DDF non ha il tipo richiesto.");
    }

    // Description of a call only. There is no dispatcher or callback in this contract.
    public sealed class DdfExtensionCallRequest
    {
        private static readonly Regex NamePattern = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        public DdfExtensionCallRequest(DdfExtensionPointDescriptor point,
            IEnumerable<KeyValuePair<string, DdfExtensionValue>> arguments)
        {
            Point = point ?? throw new ArgumentNullException(nameof(point));
            if (point.Kind != DdfExtensionPointKind.RuntimeOperation &&
                point.Kind != DdfExtensionPointKind.HostFunction)
                throw new ArgumentException("Questa richiesta accetta solo operazioni runtime o funzioni host.",
                    nameof(point));
            KeyValuePair<string, DdfExtensionValue>[] copy = (arguments ??
                throw new ArgumentNullException(nameof(arguments))).ToArray();
            if (copy.Length > 64 || copy.Any(item => item.Key == null ||
                item.Key.Length > 128 || !NamePattern.IsMatch(item.Key) || item.Value == null) ||
                copy.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Argomenti nulli, duplicati o oltre quota.",
                    nameof(arguments));
            int remaining = 1024;
            foreach (KeyValuePair<string, DdfExtensionValue> item in copy)
                item.Value.ValidateTree(ref remaining);
            Arguments = new ReadOnlyDictionary<string, DdfExtensionValue>(
                copy.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        }

        public string ContractVersion => DdfExtensionValue.ContractVersion;
        public DdfExtensionPointDescriptor Point { get; }
        public IReadOnlyDictionary<string, DdfExtensionValue> Arguments { get; }
    }
}
