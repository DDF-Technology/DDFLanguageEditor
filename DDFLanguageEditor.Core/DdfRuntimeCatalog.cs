using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfStandardFunction
    {
        public DdfStandardFunction(string name, string returnType, params string[] parameterTypes)
        {
            Name = name;
            ReturnType = returnType;
            ParameterTypes = Array.AsReadOnly(parameterTypes ?? new string[0]);
        }

        public string Name { get; }
        public string ReturnType { get; }
        public IReadOnlyList<string> ParameterTypes { get; }
        public string Signature => Name + "(" + string.Join(", ", ParameterTypes) + ") out " + ReturnType;
    }

    public static class DdfRuntimeCatalog
    {
        public const string DefaultEntryPoint = "main";
        public const string ConsoleOutput = "Console";
        public const int DefaultInstructionLimit = 100000;

        private static readonly IReadOnlyList<DdfStandardFunction> standardFunctions = Array.AsReadOnly(new[]
        {
            new DdfStandardFunction("print", "void", "string"),
            new DdfStandardFunction("readLine", "string"),
            new DdfStandardFunction("length", "int", "string"),
            new DdfStandardFunction("toInt", "int", "string"),
            new DdfStandardFunction("toFloat", "float", "string")
        });

        public static IReadOnlyList<DdfStandardFunction> StandardFunctions => standardFunctions;

        public static bool TryGetStandardFunction(string name, out DdfStandardFunction function)
        {
            foreach (DdfStandardFunction candidate in standardFunctions)
            {
                if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    function = candidate;
                    return true;
                }
            }
            function = null;
            return false;
        }
    }
}
