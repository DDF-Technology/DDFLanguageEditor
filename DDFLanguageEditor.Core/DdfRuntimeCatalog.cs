using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfStandardFunction
    {
        public DdfStandardFunction(string name, string returnType, string documentation, params string[] parameterTypes)
            : this(name, returnType, documentation, LegacyBinding(name), parameterTypes)
        {
        }

        public DdfStandardFunction(string name, string returnType, string documentation, string bindingId,
            IEnumerable<string> parameterTypes)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Function name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(returnType)) throw new ArgumentException("Return type is required.", nameof(returnType));
            if (!DdfHostFunctionBindings.IsSupported(bindingId))
                throw new DdfEngineCapabilityException("Binding di funzione standard non supportato: " + bindingId + ".");
            Name = name;
            ReturnType = returnType;
            Documentation = documentation ?? string.Empty;
            BindingId = bindingId;
            ParameterTypes = Array.AsReadOnly((parameterTypes ?? Array.Empty<string>()).ToArray());
        }

        public string Name { get; }
        public string ReturnType { get; }
        public string Documentation { get; }
        public string BindingId { get; }
        public IReadOnlyList<string> ParameterTypes { get; }
        public string Signature => Name + "(" + string.Join(", ", ParameterTypes) + ") out " + ReturnType;

        private static string LegacyBinding(string name)
        {
            if (name == "print") return DdfHostFunctionBindings.Output;
            if (name == "readLine") return DdfHostFunctionBindings.Input;
            if (name == "length") return DdfHostFunctionBindings.StringLength;
            if (name == "toInt") return DdfHostFunctionBindings.ToInt;
            if (name == "toFloat") return DdfHostFunctionBindings.ToFloat;
            throw new DdfEngineCapabilityException("Funzione standard legacy non supportata: " + name + ".");
        }
    }

    public static class DdfHostFunctionBindings
    {
        public const string Output = "host.output";
        public const string Input = "host.input";
        public const string StringLength = "core.string-length";
        public const string ToInt = "core.to-int";
        public const string ToFloat = "core.to-float";

        private static readonly HashSet<string> supported = new HashSet<string>(new[]
        { Output, Input, StringLength, ToInt, ToFloat }, StringComparer.Ordinal);

        public static bool IsSupported(string id) => !string.IsNullOrWhiteSpace(id) && supported.Contains(id);
    }

    public static class DdfRuntimeCatalog
    {
        public const string DefaultEntryPoint = "main";
        public const string ConsoleOutput = "Console";
        public const int DefaultInstructionLimit = 100000;

        public static IReadOnlyList<DdfStandardFunction> StandardFunctions =>
            DdfLanguageCatalog.Default.StandardLibrary.Functions;

        public static bool TryGetStandardFunction(string name, out DdfStandardFunction function)
        {
            return DdfLanguageCatalog.Default.StandardLibrary.TryGetFunction(name, out function);
        }
    }
}
