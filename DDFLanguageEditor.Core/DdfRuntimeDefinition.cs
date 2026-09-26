using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public static class DdfVmOperations
    {
        public const string DeclareVariable = "value.declare";
        public const string LoadValue = "value.load";
        public const string StoreValue = "value.store";
        public const string CreateScope = "scope.create";
        public const string Call = "call.invoke";
        public const string Branch = "flow.branch";
        public const string PreconditionLoop = "flow.while";
        public const string PostconditionLoop = "flow.do-while";
        public const string CountedLoop = "flow.for";
        public const string Return = "flow.return";
        public const string Break = "flow.break";
        public const string End = "flow.end";
        public const string CreateArray = "array.create";
        public const string IndexArray = "array.index";
        public const string CreateStructure = "structure.create";
        public const string AccessMember = "structure.member";
        public const string HostCall = "host.call";

        private static readonly IReadOnlyCollection<string> all = Array.AsReadOnly(new[]
        {
            DeclareVariable, LoadValue, StoreValue, CreateScope, Call, Branch,
            PreconditionLoop, PostconditionLoop, CountedLoop, Return, Break, End,
            CreateArray, IndexArray, CreateStructure, AccessMember, HostCall
        });

        public static IReadOnlyCollection<string> All => all;
    }

    public static class DdfVmExpressionOperations
    {
        public const string Assign = "assign";
        public const string Output = "output";
        public const string LogicalOr = "logical.or";
        public const string LogicalXor = "logical.xor";
        public const string LogicalAnd = "logical.and";
        public const string Equal = "compare.equal";
        public const string NotEqual = "compare.not-equal";
        public const string BetweenExclusive = "compare.between-exclusive";
        public const string OutsideExclusive = "compare.outside-exclusive";
        public const string Less = "compare.less";
        public const string LessOrEqual = "compare.less-or-equal";
        public const string Greater = "compare.greater";
        public const string GreaterOrEqual = "compare.greater-or-equal";
        public const string Add = "numeric.add";
        public const string Subtract = "numeric.subtract";
        public const string Multiply = "numeric.multiply";
        public const string Divide = "numeric.divide";
        public const string Power = "numeric.power";
        public const string Root = "numeric.root";
        public const string LogicalNot = "logical.not";
        public const string Positive = "numeric.positive";
        public const string Negate = "numeric.negate";
        public const string Increment = "numeric.increment";
        public const string Decrement = "numeric.decrement";

        private static readonly HashSet<string> all = new HashSet<string>(new[]
        {
            Assign, Output, LogicalOr, LogicalXor, LogicalAnd, Equal, NotEqual,
            BetweenExclusive, OutsideExclusive, Less, LessOrEqual, Greater, GreaterOrEqual,
            Add, Subtract, Multiply, Divide, Power, Root, LogicalNot, Positive, Negate,
            Increment, Decrement
        }, StringComparer.Ordinal);

        private static readonly HashSet<string> unary = new HashSet<string>(new[]
        { LogicalNot, Positive, Negate }, StringComparer.Ordinal);
        private static readonly HashSet<string> postfix = new HashSet<string>(new[]
        { Increment, Decrement }, StringComparer.Ordinal);

        public static bool IsSupported(string id) => !string.IsNullOrWhiteSpace(id) && all.Contains(id);
        public static bool IsSupportedUnary(string id) => !string.IsNullOrWhiteSpace(id) && unary.Contains(id);
        public static bool IsSupportedPostfix(string id) => !string.IsNullOrWhiteSpace(id) && postfix.Contains(id);
        public static bool IsSupportedBinary(string id) => IsSupported(id) && !unary.Contains(id) && !postfix.Contains(id);
    }

    public sealed class DdfRuntimeDefinition
    {
        private readonly IReadOnlyDictionary<string, string> unaryOperations;
        private readonly IReadOnlyDictionary<string, string> binaryOperations;
        private readonly IReadOnlyDictionary<string, string> postfixOperations;
        private readonly IReadOnlyCollection<string> vmOperations;

        public DdfRuntimeDefinition(string instructionSet, string entryPoint, int defaultInstructionLimit,
            string outputTarget, IEnumerable<string> vmOperations,
            IEnumerable<KeyValuePair<string, string>> unaryOperations,
            IEnumerable<KeyValuePair<string, string>> binaryOperations,
            IEnumerable<KeyValuePair<string, string>> postfixOperations)
        {
            if (instructionSet != "ddf-ir-vm-1.0")
                throw new DdfEngineCapabilityException("Instruction set runtime non supportato: " + instructionSet + ".");
            if (string.IsNullOrWhiteSpace(entryPoint)) throw new ArgumentException("Entry point runtime richiesto.", nameof(entryPoint));
            if (defaultInstructionLimit <= 0) throw new ArgumentOutOfRangeException(nameof(defaultInstructionLimit));
            if (string.IsNullOrWhiteSpace(outputTarget)) throw new ArgumentException("Destinazione output richiesta.", nameof(outputTarget));

            this.vmOperations = Array.AsReadOnly((vmOperations ?? throw new ArgumentNullException(nameof(vmOperations))).ToArray());
            if (this.vmOperations.Count == 0 || this.vmOperations.Distinct(StringComparer.Ordinal).Count() != this.vmOperations.Count)
                throw new ArgumentException("Le operazioni VM devono essere presenti e univoche.", nameof(vmOperations));
            string unsupportedVm = this.vmOperations.FirstOrDefault(item => !DdfVmOperations.All.Contains(item));
            if (unsupportedVm != null) throw new DdfEngineCapabilityException("Operazione VM non supportata: " + unsupportedVm + ".");
            string missingVm = DdfVmOperations.All.FirstOrDefault(item => !this.vmOperations.Contains(item));
            if (missingVm != null) throw new DdfEngineCapabilityException("Operazione VM richiesta mancante: " + missingVm + ".");

            this.unaryOperations = CreateMap(unaryOperations, "unary");
            this.binaryOperations = CreateMap(binaryOperations, "binary");
            this.postfixOperations = CreateMap(postfixOperations, "postfix");
            InstructionSet = instructionSet;
            EntryPoint = entryPoint;
            DefaultInstructionLimit = defaultInstructionLimit;
            OutputTarget = outputTarget;
        }

        public string InstructionSet { get; }
        public string EntryPoint { get; }
        public int DefaultInstructionLimit { get; }
        public string OutputTarget { get; }
        public IReadOnlyCollection<string> VmOperations => vmOperations;
        public IReadOnlyDictionary<string, string> UnaryOperations => unaryOperations;
        public IReadOnlyDictionary<string, string> BinaryOperations => binaryOperations;
        public IReadOnlyDictionary<string, string> PostfixOperations => postfixOperations;
        public bool TryGetUnaryOperation(string token, out string operation) => unaryOperations.TryGetValue(token, out operation);
        public bool TryGetBinaryOperation(string token, out string operation) => binaryOperations.TryGetValue(token, out operation);
        public bool TryGetPostfixOperation(string token, out string operation) => postfixOperations.TryGetValue(token, out operation);

        private static IReadOnlyDictionary<string, string> CreateMap(IEnumerable<KeyValuePair<string, string>> values, string category)
        {
            if (values == null) throw new ArgumentNullException(category + "Operations");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> value in values)
            {
                bool supported = category == "unary" ? DdfVmExpressionOperations.IsSupportedUnary(value.Value) :
                    category == "postfix" ? DdfVmExpressionOperations.IsSupportedPostfix(value.Value) :
                    DdfVmExpressionOperations.IsSupportedBinary(value.Value);
                if (string.IsNullOrWhiteSpace(value.Key) || !supported)
                    throw new DdfEngineCapabilityException("Binding " + category + " non supportato: " + value.Key + " -> " + value.Value + ".");
                if (!result.TryAdd(value.Key, value.Value)) throw new ArgumentException("Binding " + category + " duplicato: " + value.Key + ".");
            }
            return result;
        }

        public static DdfRuntimeDefinition CreateDdfCompatibleDefault() => new DdfRuntimeDefinition(
            "ddf-ir-vm-1.0", "main", 100000, "Console", DdfVmOperations.All,
            new Dictionary<string, string> { ["!"] = DdfVmExpressionOperations.LogicalNot, ["+"] = DdfVmExpressionOperations.Positive, ["-"] = DdfVmExpressionOperations.Negate },
            new Dictionary<string, string>
            {
                ["<<"] = DdfVmExpressionOperations.Assign, [">>"] = DdfVmExpressionOperations.Output,
                ["||"] = DdfVmExpressionOperations.LogicalOr, ["^^"] = DdfVmExpressionOperations.LogicalXor,
                ["&&"] = DdfVmExpressionOperations.LogicalAnd, ["=="] = DdfVmExpressionOperations.Equal,
                ["!="] = DdfVmExpressionOperations.NotEqual, ["<"] = DdfVmExpressionOperations.Less,
                ["<="] = DdfVmExpressionOperations.LessOrEqual, [">"] = DdfVmExpressionOperations.Greater,
                [">="] = DdfVmExpressionOperations.GreaterOrEqual, ["+"] = DdfVmExpressionOperations.Add,
                ["-"] = DdfVmExpressionOperations.Subtract, ["*"] = DdfVmExpressionOperations.Multiply,
                ["/"] = DdfVmExpressionOperations.Divide, ["^"] = DdfVmExpressionOperations.Power
            },
            new Dictionary<string, string> { ["++"] = DdfVmExpressionOperations.Increment, ["--"] = DdfVmExpressionOperations.Decrement });
    }

    public sealed class DdfStandardLibraryDefinition
    {
        private readonly IReadOnlyList<DdfStandardFunction> functions;
        private readonly IReadOnlyDictionary<string, DdfStandardFunction> functionsByName;

        public DdfStandardLibraryDefinition(IEnumerable<DdfStandardFunction> functions)
        {
            var list = (functions ?? throw new ArgumentNullException(nameof(functions))).ToList();
            if (list.Count != list.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count())
                throw new ArgumentException("Le funzioni standard devono avere nomi univoci.", nameof(functions));
            this.functions = list.AsReadOnly();
            functionsByName = list.ToDictionary(item => item.Name, StringComparer.Ordinal);
        }

        public IReadOnlyList<DdfStandardFunction> Functions => functions;
        public bool TryGetFunction(string name, out DdfStandardFunction function) => functionsByName.TryGetValue(name, out function);

        public static DdfStandardLibraryDefinition CreateDdfCompatibleDefault() => new DdfStandardLibraryDefinition(new[]
        {
            new DdfStandardFunction("print", "void", "Scrive una stringa nella palette Output.", DdfHostFunctionBindings.Output, new[] { "string" }),
            new DdfStandardFunction("readLine", "string", "Legge una riga di testo dalla finestra di input.", DdfHostFunctionBindings.Input, Array.Empty<string>()),
            new DdfStandardFunction("length", "int", "Restituisce il numero di caratteri di una stringa.", DdfHostFunctionBindings.StringLength, new[] { "string" }),
            new DdfStandardFunction("toInt", "int", "Converte una stringa in un valore intero.", DdfHostFunctionBindings.ToInt, new[] { "string" }),
            new DdfStandardFunction("toFloat", "float", "Converte una stringa in un valore decimale.", DdfHostFunctionBindings.ToFloat, new[] { "string" })
        });
    }
}
