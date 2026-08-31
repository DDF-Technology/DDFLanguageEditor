using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfStandardFunction
    {
        public DdfStandardFunction(string name, string returnType, string documentation, params string[] parameterTypes)
        {
            Name = name;
            ReturnType = returnType;
            Documentation = documentation ?? string.Empty;
            ParameterTypes = Array.AsReadOnly(parameterTypes ?? new string[0]);
        }

        public string Name { get; }
        public string ReturnType { get; }
        public string Documentation { get; }
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
            new DdfStandardFunction("print", "void", "Scrive una stringa nella palette Output.", "string"),
            new DdfStandardFunction("readLine", "string", "Legge una riga di testo dalla finestra di input."),
            new DdfStandardFunction("length", "int", "Restituisce il numero di caratteri di una stringa.", "string"),
            new DdfStandardFunction("toInt", "int", "Converte una stringa in un valore intero.", "string"),
            new DdfStandardFunction("toFloat", "float", "Converte una stringa in un valore decimale.", "string")
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
