using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfStandardFunctionEffect
    {
        Pure,
        Input,
        Output
    }

    public sealed class DdfStandardFunction
    {
        public DdfStandardFunction(string name, string returnType, string documentation, params string[] parameterTypes)
            : this(name, returnType, documentation, LegacyBinding(name), parameterTypes)
        {
        }

        public DdfStandardFunction(string name, string returnType, string documentation, string bindingId,
            IEnumerable<string> parameterTypes)
            : this(name, returnType, documentation, bindingId, parameterTypes,
                DdfHostFunctionBindings.GetEffect(bindingId),
                DdfHostFunctionBindings.GetErrorCodes(bindingId))
        {
        }

        public DdfStandardFunction(string name, string returnType, string documentation, string bindingId,
            IEnumerable<string> parameterTypes, DdfStandardFunctionEffect effect,
            IEnumerable<string> errorCodes)
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
            if (!Enum.IsDefined(effect)) throw new ArgumentOutOfRangeException(nameof(effect));
            string[] errors = (errorCodes ?? Array.Empty<string>()).ToArray();
            if (errors.Distinct(StringComparer.Ordinal).Count() != errors.Length ||
                errors.Any(code => code == null || code.Length != 6 ||
                    !code.StartsWith("DDF", StringComparison.Ordinal) ||
                    code.Skip(3).Any(character => character < '0' || character > '9')))
                throw new ArgumentException("I codici di errore standard devono essere univoci e nel formato DDFnnn.",
                    nameof(errorCodes));
            Effect = effect;
            ErrorCodes = Array.AsReadOnly(errors);
        }

        public string Name { get; }
        public string ReturnType { get; }
        public string Documentation { get; }
        public string BindingId { get; }
        public IReadOnlyList<string> ParameterTypes { get; }
        public DdfStandardFunctionEffect Effect { get; }
        public IReadOnlyList<string> ErrorCodes { get; }
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

        public static DdfStandardFunctionEffect GetEffect(string id)
        {
            if (id == Input) return DdfStandardFunctionEffect.Input;
            if (id == Output) return DdfStandardFunctionEffect.Output;
            if (id == StringLength || id == ToInt || id == ToFloat)
                return DdfStandardFunctionEffect.Pure;
            throw new DdfEngineCapabilityException("Binding di funzione standard non supportato: " + id + ".");
        }

        public static IReadOnlyList<string> GetErrorCodes(string id)
        {
            if (!IsSupported(id))
                throw new DdfEngineCapabilityException("Binding di funzione standard non supportato: " + id + ".");
            return id == Input || id == ToInt || id == ToFloat
                ? Array.AsReadOnly(new[] { "DDF408" })
                : Array.Empty<string>();
        }
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
