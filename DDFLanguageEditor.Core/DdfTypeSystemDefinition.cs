using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public enum DdfPrimitiveTypeCategory
    {
        Void,
        Boolean,
        Integer,
        Floating,
        Character,
        String,
        Dictionary
    }

    public enum DdfConversionContext
    {
        General,
        VariableInitializerSingleCharacterLiteral
    }

    public sealed class DdfPrimitiveTypeDefinition
    {
        public DdfPrimitiveTypeDefinition(string name, DdfPrimitiveTypeCategory category,
            bool canDeclareVariable = true, string indexedElementType = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Type name is required.", nameof(name));
            Name = name;
            Category = category;
            CanDeclareVariable = canDeclareVariable;
            IndexedElementType = indexedElementType ?? string.Empty;
        }

        public string Name { get; }
        public DdfPrimitiveTypeCategory Category { get; }
        public bool CanDeclareVariable { get; }
        public string IndexedElementType { get; }
    }

    public sealed class DdfTypeConversionDefinition
    {
        public DdfTypeConversionDefinition(string from, string to, DdfConversionContext context)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Conversion source and target are required.");
            From = from;
            To = to;
            Context = context;
        }

        public string From { get; }
        public string To { get; }
        public DdfConversionContext Context { get; }
    }

    public sealed class DdfTypeCompatibilityDefinition
    {
        public DdfTypeCompatibilityDefinition(bool arraysInvariant, bool userTypesNominal,
            bool errorTypeCompatible)
        {
            ArraysInvariant = arraysInvariant;
            UserTypesNominal = userTypesNominal;
            ErrorTypeCompatible = errorTypeCompatible;
        }

        public bool ArraysInvariant { get; }
        public bool UserTypesNominal { get; }
        public bool ErrorTypeCompatible { get; }
    }

    public sealed class DdfMutabilityDefinition
    {
        private readonly HashSet<string> assignmentTargets;

        public DdfMutabilityDefinition(bool variablesMutable, bool parametersMutable, bool fieldsMutable,
            IEnumerable<string> assignmentTargets)
        {
            VariablesMutable = variablesMutable;
            ParametersMutable = parametersMutable;
            FieldsMutable = fieldsMutable;
            this.assignmentTargets = new HashSet<string>(assignmentTargets ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (this.assignmentTargets.Count == 0) throw new ArgumentException("At least one assignment target is required.");
        }

        public bool VariablesMutable { get; }
        public bool ParametersMutable { get; }
        public bool FieldsMutable { get; }
        public IReadOnlyCollection<string> AssignmentTargets => assignmentTargets;
        public bool AllowsAssignmentTarget(string kind) => assignmentTargets.Contains(kind ?? string.Empty);
    }

    public sealed class DdfScopeDefinition
    {
        public DdfScopeDefinition(bool functionParameters, bool blocks, bool forInitializers,
            bool structureFields, bool allowShadowing)
        {
            FunctionParameters = functionParameters;
            Blocks = blocks;
            ForInitializers = forInitializers;
            StructureFields = structureFields;
            AllowShadowing = allowShadowing;
        }

        public bool FunctionParameters { get; }
        public bool Blocks { get; }
        public bool ForInitializers { get; }
        public bool StructureFields { get; }
        public bool AllowShadowing { get; }
    }

    public sealed class DdfSignatureDefinition
    {
        public DdfSignatureDefinition(bool exactArity, bool requireReturnForNonVoid,
            string voidType, string parameterOrder)
        {
            if (string.IsNullOrWhiteSpace(voidType)) throw new ArgumentException("Void type is required.", nameof(voidType));
            if (parameterOrder != "declared")
                throw new DdfEngineCapabilityException("Parameter order '" + parameterOrder + "' is not supported by this engine.");
            ExactArity = exactArity;
            RequireReturnForNonVoid = requireReturnForNonVoid;
            VoidType = voidType;
            ParameterOrder = parameterOrder;
        }

        public bool ExactArity { get; }
        public bool RequireReturnForNonVoid { get; }
        public string VoidType { get; }
        public string ParameterOrder { get; }
    }

    public sealed class DdfLiteralTypeDefinition
    {
        public DdfLiteralTypeDefinition(string integer, string decimalType, string boolean, string stringType)
        {
            Integer = require(integer, nameof(integer));
            Decimal = require(decimalType, nameof(decimalType));
            Boolean = require(boolean, nameof(boolean));
            String = require(stringType, nameof(stringType));
        }

        public string Integer { get; }
        public string Decimal { get; }
        public string Boolean { get; }
        public string String { get; }

        private static string require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Literal type is required.", name);
            return value;
        }
    }

    public sealed class DdfTypeSystemDefinition
    {
        private readonly Dictionary<string, DdfPrimitiveTypeDefinition> primitives;
        private readonly IReadOnlyList<DdfTypeConversionDefinition> conversions;
        private readonly HashSet<string> numericTypes;

        public DdfTypeSystemDefinition(
            IEnumerable<DdfPrimitiveTypeDefinition> primitives,
            IEnumerable<DdfTypeConversionDefinition> conversions,
            DdfTypeCompatibilityDefinition compatibility,
            DdfMutabilityDefinition mutability,
            DdfScopeDefinition scopes,
            DdfSignatureDefinition signatures,
            DdfLiteralTypeDefinition literals,
            string numericResultType,
            string numericDominantType,
            string indexType)
        {
            this.primitives = (primitives ?? throw new ArgumentNullException(nameof(primitives)))
                .ToDictionary(item => item.Name, StringComparer.Ordinal);
            if (this.primitives.Count == 0) throw new ArgumentException("At least one primitive type is required.");
            this.conversions = (conversions ?? Array.Empty<DdfTypeConversionDefinition>()).ToList().AsReadOnly();
            Compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            Mutability = mutability ?? throw new ArgumentNullException(nameof(mutability));
            Scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
            Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            Literals = literals ?? throw new ArgumentNullException(nameof(literals));
            NumericResultType = requirePrimitive(numericResultType, nameof(numericResultType));
            NumericDominantType = requirePrimitive(numericDominantType, nameof(numericDominantType));
            IndexType = requirePrimitive(indexType, nameof(indexType));
            requirePrimitive(signatures.VoidType, nameof(signatures.VoidType));
            requirePrimitive(literals.Integer, nameof(literals.Integer));
            requirePrimitive(literals.Decimal, nameof(literals.Decimal));
            requirePrimitive(literals.Boolean, nameof(literals.Boolean));
            requirePrimitive(literals.String, nameof(literals.String));
            foreach (DdfTypeConversionDefinition conversion in this.conversions)
            {
                requirePrimitive(conversion.From, "conversion source");
                requirePrimitive(conversion.To, "conversion target");
            }
            foreach (DdfPrimitiveTypeDefinition primitive in this.primitives.Values)
                if (!string.IsNullOrEmpty(primitive.IndexedElementType))
                    requirePrimitive(primitive.IndexedElementType, "indexed element type");
            numericTypes = new HashSet<string>(this.primitives.Values
                .Where(item => item.Category == DdfPrimitiveTypeCategory.Integer ||
                               item.Category == DdfPrimitiveTypeCategory.Floating ||
                               item.Category == DdfPrimitiveTypeCategory.Character)
                .Select(item => item.Name), StringComparer.Ordinal);
            requireCategory(signatures.VoidType, DdfPrimitiveTypeCategory.Void, "void type");
            requireCategory(literals.Integer, DdfPrimitiveTypeCategory.Integer, "integer literal type");
            requireCategory(literals.Decimal, DdfPrimitiveTypeCategory.Floating, "decimal literal type");
            requireCategory(literals.Boolean, DdfPrimitiveTypeCategory.Boolean, "boolean literal type");
            requireCategory(literals.String, DdfPrimitiveTypeCategory.String, "string literal type");
            if (!numericTypes.Contains(NumericResultType) || !numericTypes.Contains(NumericDominantType))
                throw new ArgumentException("Numeric result and dominant types must have a numeric category.");
        }

        public IReadOnlyCollection<DdfPrimitiveTypeDefinition> Primitives => primitives.Values;
        public IReadOnlyList<DdfTypeConversionDefinition> Conversions => conversions;
        public DdfTypeCompatibilityDefinition Compatibility { get; }
        public DdfMutabilityDefinition Mutability { get; }
        public DdfScopeDefinition Scopes { get; }
        public DdfSignatureDefinition Signatures { get; }
        public DdfLiteralTypeDefinition Literals { get; }
        public string NumericResultType { get; }
        public string NumericDominantType { get; }
        public string IndexType { get; }

        public bool IsKnownPrimitive(string name) => primitives.ContainsKey(name ?? string.Empty);
        public bool IsNumeric(string name) => numericTypes.Contains(name ?? string.Empty);
        public bool IsBoolean(string name) => primitives.TryGetValue(name ?? string.Empty, out DdfPrimitiveTypeDefinition type) &&
                                               type.Category == DdfPrimitiveTypeCategory.Boolean;
        public bool CanDeclareVariable(string name) => primitives.TryGetValue(name ?? string.Empty, out DdfPrimitiveTypeDefinition type) &&
                                                       type.CanDeclareVariable;
        public string GetIndexedElementType(string name) => primitives.TryGetValue(name ?? string.Empty, out DdfPrimitiveTypeDefinition type)
            ? type.IndexedElementType
            : string.Empty;
        public bool CanImplicitlyConvert(string from, string to, DdfConversionContext context = DdfConversionContext.General) =>
            conversions.Any(item => item.From == from && item.To == to &&
                                    (item.Context == DdfConversionContext.General || item.Context == context));

        public static DdfTypeSystemDefinition CreateDdfCompatibleDefault()
        {
            return new DdfTypeSystemDefinition(
                new[]
                {
                    new DdfPrimitiveTypeDefinition("int", DdfPrimitiveTypeCategory.Integer),
                    new DdfPrimitiveTypeDefinition("float", DdfPrimitiveTypeCategory.Floating),
                    new DdfPrimitiveTypeDefinition("char", DdfPrimitiveTypeCategory.Character),
                    new DdfPrimitiveTypeDefinition("bool", DdfPrimitiveTypeCategory.Boolean),
                    new DdfPrimitiveTypeDefinition("void", DdfPrimitiveTypeCategory.Void, false),
                    new DdfPrimitiveTypeDefinition("string", DdfPrimitiveTypeCategory.String, true, "char"),
                    new DdfPrimitiveTypeDefinition("dict", DdfPrimitiveTypeCategory.Dictionary)
                },
                new[]
                {
                    new DdfTypeConversionDefinition("int", "float", DdfConversionContext.General),
                    new DdfTypeConversionDefinition("char", "float", DdfConversionContext.General),
                    new DdfTypeConversionDefinition("string", "char", DdfConversionContext.VariableInitializerSingleCharacterLiteral)
                },
                new DdfTypeCompatibilityDefinition(true, true, true),
                new DdfMutabilityDefinition(true, true, true, new[] { "Name", "Index", "Member" }),
                new DdfScopeDefinition(true, true, true, true, true),
                new DdfSignatureDefinition(true, true, "void", "declared"),
                new DdfLiteralTypeDefinition("int", "float", "bool", "string"),
                "int", "float", "int");
        }

        private string requirePrimitive(string name, string label)
        {
            if (string.IsNullOrWhiteSpace(name) || !primitives.ContainsKey(name))
                throw new ArgumentException("Unknown primitive type '" + name + "'.", label);
            return name;
        }

        private void requireCategory(string name, DdfPrimitiveTypeCategory category, string label)
        {
            if (primitives[name].Category != category)
                throw new ArgumentException("The " + label + " '" + name + "' has category " +
                    primitives[name].Category + " instead of " + category + ".", label);
        }
    }
}
