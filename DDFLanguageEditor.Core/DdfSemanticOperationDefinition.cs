using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfEngineCapabilityException : NotSupportedException
    {
        public DdfEngineCapabilityException(string message) : base(message) { }
    }

    public sealed class DdfSemanticOperationDescriptor
    {
        internal DdfSemanticOperationDescriptor(string id, IEnumerable<string> requiredArguments,
            IEnumerable<string> optionalArguments)
        {
            Id = id;
            RequiredArguments = (requiredArguments ?? Array.Empty<string>()).ToList().AsReadOnly();
            OptionalArguments = (optionalArguments ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public string Id { get; }
        public IReadOnlyList<string> RequiredArguments { get; }
        public IReadOnlyList<string> OptionalArguments { get; }
    }

    public static class DdfSafeSemanticOperationCatalog
    {
        public const string OperationSet = "ddf-safe-semantics";
        public const string Version = "1.0";

        private static readonly IReadOnlyDictionary<string, DdfSemanticOperationDescriptor> descriptors =
            new[]
            {
                Define("VisitCompilationUnit", new[] { "members" }),
                Define("ImportLibrary"),
                Define("DeclareStructure", new[] { "name", "body" }),
                Define("DeclareEnumeration", new[] { "name", "values" }),
                Define("DeclareFunction", new[] { "name", "parameters", "returnType", "body" }),
                Define("DeclareParameter", new[] { "type", "name" }),
                Define("Branch", new[] { "condition", "branches" }),
                Define("Select", new[] { "expression", "cases" },
                    new[] { "fallback" }),
                Define("PreconditionLoop", new[] { "condition", "body" }),
                Define("PostconditionLoop", new[] { "condition", "body" }),
                Define("CountedLoop", new[] { "body" }),
                Define("Break"),
                Define("Continue"),
                Define("Terminate"),
                Define("CreateScope", new[] { "statements" }),
                Define("DeclareVariable", new[] { "type", "name" }, new[] { "initializer" }),
                Define("Return", null, new[] { "expression" }),
                Define("EvaluateStatement", new[] { "expression" }),
                Define("InferExpression")
            }.ToDictionary(item => item.Id, StringComparer.Ordinal);

        public static IReadOnlyCollection<DdfSemanticOperationDescriptor> Operations =>
            descriptors.Values.ToList().AsReadOnly();

        public static bool TryGet(string id, out DdfSemanticOperationDescriptor descriptor) =>
            descriptors.TryGetValue(id ?? string.Empty, out descriptor);

        internal static void Validate(string id, IEnumerable<string> argumentNames)
        {
            if (!TryGet(id, out DdfSemanticOperationDescriptor descriptor))
                throw new DdfEngineCapabilityException("Unsupported safe semantic operation '" + id + "'.");
            var actual = new HashSet<string>(argumentNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string required in descriptor.RequiredArguments)
                if (!actual.Contains(required))
                    throw new ArgumentException("Operation '" + id + "' requires argument '" + required + "'.");
            var allowed = new HashSet<string>(descriptor.RequiredArguments.Concat(descriptor.OptionalArguments),
                StringComparer.Ordinal);
            string unknown = actual.FirstOrDefault(item => !allowed.Contains(item));
            if (unknown != null)
                throw new ArgumentException("Operation '" + id + "' does not support argument '" + unknown + "'.");
        }

        private static DdfSemanticOperationDescriptor Define(string id,
            IEnumerable<string> required = null, IEnumerable<string> optional = null) =>
            new DdfSemanticOperationDescriptor(id, required, optional);
    }

    public sealed class DdfSemanticOperationDefinition
    {
        private readonly IReadOnlyDictionary<string, string> arguments;

        public DdfSemanticOperationDefinition(string id, IEnumerable<KeyValuePair<string, string>> arguments = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Operation id is required.", nameof(id));
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> argument in arguments ??
                     Array.Empty<KeyValuePair<string, string>>())
            {
                if (string.IsNullOrWhiteSpace(argument.Key) || string.IsNullOrWhiteSpace(argument.Value))
                    throw new ArgumentException("Semantic argument names and grammar fields are required.");
                if (!values.TryAdd(argument.Key, argument.Value))
                    throw new ArgumentException("Duplicate semantic argument '" + argument.Key + "'.");
            }
            DdfSafeSemanticOperationCatalog.Validate(id, values.Keys);
            Id = id;
            this.arguments = values;
        }

        public string Id { get; }
        public IReadOnlyDictionary<string, string> Arguments => arguments;
    }

    public sealed class DdfSemanticBindingDefinition
    {
        public DdfSemanticBindingDefinition(string rule, IEnumerable<DdfSemanticOperationDefinition> operations)
        {
            if (string.IsNullOrWhiteSpace(rule)) throw new ArgumentException("Grammar rule is required.", nameof(rule));
            Rule = rule;
            Operations = (operations ?? throw new ArgumentNullException(nameof(operations))).ToList().AsReadOnly();
            if (Operations.Count == 0) throw new ArgumentException("At least one semantic operation is required.");
            if (Operations.Any(item => item == null))
                throw new ArgumentException("Semantic operations cannot contain null entries.");
            if (Operations.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != Operations.Count)
                throw new ArgumentException("A semantic binding cannot repeat the same operation.");
        }

        public string Rule { get; }
        public IReadOnlyList<DdfSemanticOperationDefinition> Operations { get; }
    }

    public sealed class DdfSemanticDefinition
    {
        private readonly IReadOnlyDictionary<string, DdfSemanticBindingDefinition> bindings;

        public DdfSemanticDefinition(string operationSet, string operationSetVersion,
            IEnumerable<DdfSemanticBindingDefinition> bindings)
        {
            if (operationSet != DdfSafeSemanticOperationCatalog.OperationSet)
                throw new DdfEngineCapabilityException("Unsupported semantic operation set '" + operationSet + "'.");
            if (operationSetVersion != DdfSafeSemanticOperationCatalog.Version)
                throw new DdfEngineCapabilityException("Unsupported semantic operation-set version '" + operationSetVersion + "'.");
            OperationSet = operationSet;
            OperationSetVersion = operationSetVersion;
            List<DdfSemanticBindingDefinition> values = (bindings ??
                Array.Empty<DdfSemanticBindingDefinition>()).ToList();
            if (values.Any(item => item == null))
                throw new ArgumentException("Semantic bindings cannot contain null entries.");
            this.bindings = values.ToDictionary(item => item.Rule, StringComparer.Ordinal);
        }

        public string OperationSet { get; }
        public string OperationSetVersion { get; }
        public IReadOnlyCollection<DdfSemanticBindingDefinition> Bindings => bindings.Values.ToList().AsReadOnly();

        public bool TryGetBinding(string rule, out DdfSemanticBindingDefinition binding) =>
            bindings.TryGetValue(rule ?? string.Empty, out binding);

        public static DdfSemanticDefinition CreateEmpty() => new DdfSemanticDefinition(
            DdfSafeSemanticOperationCatalog.OperationSet, DdfSafeSemanticOperationCatalog.Version,
            Array.Empty<DdfSemanticBindingDefinition>());
    }

    public sealed class DdfPlannedSemanticOperation
    {
        internal DdfPlannedSemanticOperation(string id, string rule, int start, int length,
            IReadOnlyDictionary<string, IReadOnlyList<DdfGenericSyntaxNode>> arguments)
        {
            Id = id;
            Rule = rule;
            Start = start;
            Length = length;
            Arguments = arguments;
        }

        public string Id { get; }
        public string Rule { get; }
        public int Start { get; }
        public int Length { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<DdfGenericSyntaxNode>> Arguments { get; }
    }

    public static class DdfSemanticOperationPlanner
    {
        public static IReadOnlyList<DdfPlannedSemanticOperation> Create(
            DdfGenericSyntaxNode root, DdfSemanticDefinition semantics)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (semantics == null) throw new ArgumentNullException(nameof(semantics));
            var result = new List<DdfPlannedSemanticOperation>();
            Visit(root, semantics, result);
            return result.AsReadOnly();
        }

        private static void Visit(DdfGenericSyntaxNode node, DdfSemanticDefinition semantics,
            ICollection<DdfPlannedSemanticOperation> result)
        {
            if (semantics.TryGetBinding(node.RuleName, out DdfSemanticBindingDefinition binding))
            {
                foreach (DdfSemanticOperationDefinition operation in binding.Operations)
                {
                    var arguments = new Dictionary<string, IReadOnlyList<DdfGenericSyntaxNode>>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, string> argument in operation.Arguments)
                        arguments[argument.Key] = node.Fields.TryGetValue(argument.Value,
                            out IReadOnlyList<DdfGenericSyntaxNode> values)
                            ? values
                            : Array.Empty<DdfGenericSyntaxNode>();
                    result.Add(new DdfPlannedSemanticOperation(operation.Id, node.RuleName,
                        node.Start, node.Length, arguments));
                }
            }
            foreach (DdfGenericSyntaxNode child in node.Children) Visit(child, semantics, result);
        }
    }
}
