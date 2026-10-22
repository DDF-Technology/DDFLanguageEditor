using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public enum DdfBoundSymbolKind
    {
        Structure,
        Enumeration,
        EnumerationMember,
        Function,
        Global,
        Field,
        Parameter,
        Local,
        StandardFunction,
        HostFunction,
        HostObject,
        HostType
    }

    public enum DdfBoundValueCategory
    {
        Value,
        MutableLocation,
        ReadOnlyLocation,
        Callable,
        Type,
        Error
    }

    public enum DdfBoundConversionKind
    {
        Identity,
        Implicit,
        None
    }

    public sealed class DdfBoundType : IEquatable<DdfBoundType>
    {
        public static readonly DdfBoundType Error = new DdfBoundType("?", 0, true);

        public DdfBoundType(string name, int arrayRank = 0)
            : this(name, arrayRank, false)
        {
        }

        private DdfBoundType(string name, int arrayRank, bool isError)
        {
            Name = name ?? string.Empty;
            ArrayRank = Math.Max(0, arrayRank);
            IsError = isError;
        }

        public string Name { get; }
        public int ArrayRank { get; }
        public bool IsError { get; }
        public string DisplayName => IsError
            ? "tipo sconosciuto"
            : Name + string.Concat(Enumerable.Repeat("[]", ArrayRank));

        public bool Equals(DdfBoundType other) => other != null &&
            Name == other.Name && ArrayRank == other.ArrayRank && IsError == other.IsError;
        public override bool Equals(object obj) => Equals(obj as DdfBoundType);
        public override int GetHashCode() => HashCode.Combine(Name, ArrayRank, IsError);
        public override string ToString() => DisplayName;
    }

    public sealed class DdfBoundConversion
    {
        internal DdfBoundConversion(DdfBoundConversionKind kind,
            DdfBoundType sourceType, DdfBoundType targetType)
        {
            Kind = kind;
            SourceType = sourceType ?? DdfBoundType.Error;
            TargetType = targetType ?? DdfBoundType.Error;
        }

        public DdfBoundConversionKind Kind { get; }
        public DdfBoundType SourceType { get; }
        public DdfBoundType TargetType { get; }
    }

    public sealed class DdfBoundSymbol
    {
        internal DdfBoundSymbol(string id, string name, DdfBoundSymbolKind kind,
            string moduleIdentity, string relativePath, int start, int length,
            DdfBoundType type, IEnumerable<DdfBoundType> parameterTypes = null,
            bool isWritable = true, string semanticBindingId = null,
            bool hasSideEffects = false, bool isState = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? string.Empty;
            Kind = kind;
            ModuleIdentity = moduleIdentity ?? string.Empty;
            RelativePath = Normalize(relativePath);
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
            Type = type ?? DdfBoundType.Error;
            ParameterTypes = (parameterTypes ?? Array.Empty<DdfBoundType>())
                .ToList().AsReadOnly();
            IsWritable = isWritable;
            SemanticBindingId = semanticBindingId ?? string.Empty;
            HasSideEffects = hasSideEffects;
            IsState = isState;
        }

        public string Id { get; }
        public string Name { get; }
        public DdfBoundSymbolKind Kind { get; }
        public string ModuleIdentity { get; }
        public string RelativePath { get; }
        public int Start { get; }
        public int Length { get; }
        public DdfBoundType Type { get; }
        public IReadOnlyList<DdfBoundType> ParameterTypes { get; }
        public bool IsWritable { get; }
        public string SemanticBindingId { get; }
        public bool HasSideEffects { get; }
        public bool IsState { get; }
        public bool IsCallable => Kind == DdfBoundSymbolKind.Function ||
            Kind == DdfBoundSymbolKind.StandardFunction ||
            Kind == DdfBoundSymbolKind.HostFunction;

        private static string Normalize(string path) =>
            (path ?? string.Empty).Replace('\\', '/');
    }

    /// <summary>
    /// Semantic annotation for one structural IR expression. NodeId is stable for
    /// the same source snapshot because it follows deterministic preorder traversal.
    /// </summary>
    public sealed class DdfBoundExpression
    {
        internal DdfBoundExpression(string nodeId, int start, int length,
            DdfBoundType type, DdfBoundValueCategory valueCategory,
            string referencedSymbolId, string callTargetSymbolId,
            DdfBoundConversion contextualConversion)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
            Type = type ?? DdfBoundType.Error;
            ValueCategory = valueCategory;
            ReferencedSymbolId = referencedSymbolId ?? string.Empty;
            CallTargetSymbolId = callTargetSymbolId ?? string.Empty;
            ContextualConversion = contextualConversion ?? new DdfBoundConversion(
                DdfBoundConversionKind.Identity, Type, Type);
        }

        public string NodeId { get; }
        public int Start { get; }
        public int Length { get; }
        public DdfBoundType Type { get; }
        public DdfBoundValueCategory ValueCategory { get; }
        public string ReferencedSymbolId { get; }
        public string CallTargetSymbolId { get; }
        public DdfBoundConversion ContextualConversion { get; }
        public bool IsAssignable => ValueCategory == DdfBoundValueCategory.MutableLocation;
        public bool IsResolvedCall => !string.IsNullOrEmpty(CallTargetSymbolId);
    }

    public sealed class DdfBoundFunctionFlow
    {
        internal DdfBoundFunctionFlow(string functionSymbolId, bool alwaysReturns,
            bool canFallThrough, bool containsEnd)
        {
            FunctionSymbolId = functionSymbolId ?? string.Empty;
            AlwaysReturns = alwaysReturns;
            CanFallThrough = canFallThrough;
            ContainsEnd = containsEnd;
        }

        public string FunctionSymbolId { get; }
        public bool AlwaysReturns { get; }
        public bool CanFallThrough { get; }
        public bool ContainsEnd { get; }
    }

    public sealed class DdfBoundIrDocument
    {
        internal DdfBoundIrDocument(string relativePath, DdfIrProgram structuralProgram,
            IEnumerable<DdfBoundExpression> expressions,
            IEnumerable<DdfBoundFunctionFlow> functionFlows)
        {
            RelativePath = (relativePath ?? string.Empty).Replace('\\', '/');
            StructuralProgram = structuralProgram ??
                throw new ArgumentNullException(nameof(structuralProgram));
            Expressions = (expressions ?? Array.Empty<DdfBoundExpression>())
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            FunctionFlows = (functionFlows ?? Array.Empty<DdfBoundFunctionFlow>())
                .OrderBy(item => item.FunctionSymbolId, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        public string RelativePath { get; }
        public DdfIrProgram StructuralProgram { get; }
        public IReadOnlyList<DdfBoundExpression> Expressions { get; }
        public IReadOnlyList<DdfBoundFunctionFlow> FunctionFlows { get; }
    }

    public sealed class DdfBoundCompilationUnit
    {
        public const string FormatVersion = "2.1";

        internal DdfBoundCompilationUnit(string moduleIdentity,
            IEnumerable<DdfBoundSymbol> symbols,
            IEnumerable<DdfBoundIrDocument> documents)
        {
            ModuleIdentity = moduleIdentity ?? throw new ArgumentNullException(nameof(moduleIdentity));
            Symbols = (symbols ?? Array.Empty<DdfBoundSymbol>())
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToList().AsReadOnly();
            Documents = (documents ?? Array.Empty<DdfBoundIrDocument>())
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            Fingerprint = ComputeFingerprint();
        }

        public string Version => FormatVersion;
        public string ModuleIdentity { get; }
        public IReadOnlyList<DdfBoundSymbol> Symbols { get; }
        public IReadOnlyList<DdfBoundIrDocument> Documents { get; }
        public string Fingerprint { get; }

        private string ComputeFingerprint()
        {
            var value = new StringBuilder(FormatVersion).Append('\n')
                .Append(ModuleIdentity).Append('\n');
            foreach (DdfBoundSymbol symbol in Symbols)
            {
                value.Append("symbol:").Append(symbol.Id).Append(':')
                    .Append(symbol.Kind).Append(':').Append(symbol.Type.DisplayName)
                    .Append(':').Append(symbol.IsWritable).Append(':')
                    .Append(symbol.SemanticBindingId).Append(':')
                    .Append(symbol.HasSideEffects).Append('\n');
                foreach (DdfBoundType parameter in symbol.ParameterTypes)
                    value.Append("parameter:").Append(parameter.DisplayName).Append('\n');
            }
            foreach (DdfBoundIrDocument document in Documents)
            {
                value.Append("document:").Append(document.RelativePath).Append('\n');
                foreach (DdfBoundExpression expression in document.Expressions)
                    value.Append(expression.NodeId).Append(':')
                        .Append(expression.Type.DisplayName).Append(':')
                        .Append(expression.ValueCategory).Append(':')
                        .Append(expression.ReferencedSymbolId).Append(':')
                        .Append(expression.CallTargetSymbolId).Append(':')
                        .Append(expression.ContextualConversion.Kind).Append(':')
                        .Append(expression.ContextualConversion.TargetType.DisplayName)
                        .Append('\n');
                foreach (DdfBoundFunctionFlow flow in document.FunctionFlows)
                    value.Append("flow:").Append(flow.FunctionSymbolId).Append(':')
                        .Append(flow.AlwaysReturns).Append(':')
                        .Append(flow.CanFallThrough).Append(':')
                        .Append(flow.ContainsEnd).Append('\n');
            }
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }
    }

    internal static class DdfBoundIrBuilder
    {
        private sealed class ExpressionDraft
        {
            internal string NodeId;
            internal DdfIrExpression Expression;
            internal DdfBoundType Type;
            internal DdfBoundValueCategory Category;
            internal string ReferencedSymbolId = string.Empty;
            internal string CallTargetSymbolId = string.Empty;
            internal DdfBoundConversion Conversion;
        }

        private sealed class DocumentBinder
        {
            private readonly string moduleIdentity;
            private readonly string relativePath;
            private readonly DdfIrProgram program;
            private readonly IReadOnlyList<DdfTypedSpan> typedSpans;
            private readonly DdfTypeSystemDefinition typeSystem;
            private readonly Dictionary<string, DdfBoundSymbol> visibleSymbols;
            private readonly Dictionary<string, Dictionary<string, DdfBoundSymbol>> fieldsByType;
            private readonly List<Dictionary<string, DdfBoundSymbol>> scopes =
                new List<Dictionary<string, DdfBoundSymbol>>();
            private readonly List<DdfBoundSymbol> localSymbols = new List<DdfBoundSymbol>();
            private readonly List<ExpressionDraft> expressions = new List<ExpressionDraft>();
            private readonly List<DdfBoundFunctionFlow> functionFlows =
                new List<DdfBoundFunctionFlow>();
            private int expressionOrdinal;

            internal DocumentBinder(string moduleIdentity, string relativePath,
                DdfIrProgram program, IReadOnlyList<DdfTypedSpan> typedSpans,
                DdfTypeSystemDefinition typeSystem,
                Dictionary<string, DdfBoundSymbol> visibleSymbols,
                Dictionary<string, Dictionary<string, DdfBoundSymbol>> fieldsByType)
            {
                this.moduleIdentity = moduleIdentity;
                this.relativePath = relativePath;
                this.program = program;
                this.typedSpans = typedSpans ?? Array.Empty<DdfTypedSpan>();
                this.typeSystem = typeSystem;
                this.visibleSymbols = visibleSymbols;
                this.fieldsByType = fieldsByType;
            }

            internal DdfBoundIrDocument Bind()
            {
                foreach (DdfIrMember member in program.Members)
                {
                    if (member is DdfIrFunction function) BindFunction(function);
                    else if (member is DdfIrStructure structure) BindStructure(structure);
                    else if (member is DdfIrGlobal global &&
                             global.Statement is DdfIrVariableDeclaration variable)
                        BindDeclaredValue(variable);
                }
                return new DdfBoundIrDocument(relativePath, program,
                    expressions.OrderBy(item => item.NodeId, StringComparer.Ordinal).Select(Freeze),
                    functionFlows);
            }

            internal IReadOnlyList<DdfBoundSymbol> LocalSymbols => localSymbols;

            private void BindFunction(DdfIrFunction function)
            {
                PushScope();
                BindType(function.ReturnType);
                for (int index = 0; index < function.Parameters.Count; index++)
                {
                    DdfIrParameter parameter = function.Parameters[index];
                    BindType(parameter.Type);
                    DdfBoundSymbol symbol = CreateDeclaredSymbol(parameter.Name,
                        DdfBoundSymbolKind.Parameter, parameter.NameStart,
                        parameter.NameLength, Type(parameter.Type));
                    scopes[scopes.Count - 1][parameter.Name] = symbol;
                }
                BindStatement(function.Body, Type(function.ReturnType));
                DdfBoundSymbol functionSymbol = visibleSymbols.TryGetValue(function.Name,
                    out DdfBoundSymbol resolved) ? resolved : null;
                functionFlows.Add(new DdfBoundFunctionFlow(functionSymbol?.Id,
                    AlwaysReturns(function.Body), !AlwaysTerminates(function.Body),
                    ContainsEnd(function.Body)));
                PopScope();
            }

            private void BindStructure(DdfIrStructure structure)
            {
                foreach (DdfIrVariableDeclaration field in structure.Fields)
                    BindDeclaredValue(field);
            }

            private void BindStatement(DdfIrStatement statement, DdfBoundType returnType)
            {
                if (statement == null) return;
                if (statement is DdfIrBlock block)
                {
                    PushScope();
                    foreach (DdfIrStatement child in block.Statements)
                        BindStatement(child, returnType);
                    PopScope();
                }
                else if (statement is DdfIrVariableDeclaration variable)
                    BindVariable(variable, DdfBoundSymbolKind.Local);
                else if (statement is DdfIrExpressionStatement expression)
                    BindExpression(expression.Expression);
                else if (statement is DdfIrIf conditional)
                {
                    BindExpression(conditional.Condition);
                    BindStatement(conditional.Body, returnType);
                    BindStatement(conditional.ElseBody, returnType);
                }
                else if (statement is DdfIrSelect selection)
                {
                    BindExpression(selection.Expression);
                    foreach (DdfIrSelectClause clause in selection.Clauses)
                    {
                        BindExpression(clause.Value);
                        BindStatement(clause.Body, returnType);
                    }
                }
                else if (statement is DdfIrWhile loop)
                {
                    BindExpression(loop.Condition);
                    BindStatement(loop.Body, returnType);
                }
                else if (statement is DdfIrDoWhile postLoop)
                {
                    BindStatement(postLoop.Body, returnType);
                    BindExpression(postLoop.Condition);
                }
                else if (statement is DdfIrFor counted)
                {
                    PushScope();
                    BindStatement(counted.Initializer, returnType);
                    BindExpression(counted.Condition);
                    BindExpression(counted.Increment);
                    BindStatement(counted.Body, returnType);
                    PopScope();
                }
                else if (statement is DdfIrReturn returned)
                {
                    ExpressionDraft returnedValue = BindExpression(returned.Expression);
                    ApplyConversion(returnedValue, returnType);
                }
            }

            private void BindVariable(DdfIrVariableDeclaration variable,
                DdfBoundSymbolKind kind)
            {
                DdfBoundType declaredType = Type(variable.Type);
                BindType(variable.Type);
                ExpressionDraft initializer = BindExpression(variable.Initializer);
                ApplyConversion(initializer, declaredType);
                if (kind != DdfBoundSymbolKind.Field)
                {
                    DdfBoundSymbol symbol = CreateDeclaredSymbol(variable.Name, kind,
                        variable.Start, Math.Max(1, variable.Name.Length), declaredType,
                        !variable.IsFixed, variable.IsState);
                    if (scopes.Count != 0)
                        scopes[scopes.Count - 1][variable.Name] = symbol;
                }
            }

            private void BindDeclaredValue(DdfIrVariableDeclaration variable)
            {
                DdfBoundType declaredType = Type(variable.Type);
                BindType(variable.Type);
                ExpressionDraft initializer = BindExpression(variable.Initializer);
                ApplyConversion(initializer, declaredType);
            }

            private void BindType(DdfIrType type)
            {
                if (type == null) return;
                foreach (DdfIrExpression length in type.ArrayLengths)
                    BindExpression(length);
            }

            private ExpressionDraft BindExpression(DdfIrExpression expression)
            {
                if (expression == null) return null;
                var draft = new ExpressionDraft
                {
                    NodeId = "e" + (++expressionOrdinal).ToString("D6"),
                    Expression = expression,
                    Type = FindType(expression),
                    Category = DdfBoundValueCategory.Value
                };
                expressions.Add(draft);
                if (expression is DdfIrMissingExpression)
                    draft.Category = DdfBoundValueCategory.Error;
                else if (expression is DdfIrName name)
                    BindName(draft, name.Name);
                else if (expression is DdfIrGroup group)
                {
                    ExpressionDraft inner = BindExpression(group.Expression);
                    if (inner != null) draft.Category = inner.Category;
                }
                else if (expression is DdfIrUnary unary)
                    BindExpression(unary.Operand);
                else if (expression is DdfIrPostfix postfix)
                {
                    ExpressionDraft operand = BindExpression(postfix.Operand);
                    if (operand == null || operand.Category != DdfBoundValueCategory.MutableLocation)
                        draft.Category = DdfBoundValueCategory.Error;
                }
                else if (expression is DdfIrBinary binary)
                {
                    ExpressionDraft left = BindExpression(binary.Left);
                    ExpressionDraft right = BindExpression(binary.Right);
                    if (binary.OperationId == DdfVmExpressionOperations.Assign)
                    {
                        ApplyConversion(right, left?.Type);
                        if (left == null || left.Category != DdfBoundValueCategory.MutableLocation)
                            draft.Category = DdfBoundValueCategory.Error;
                    }
                    else if (left?.Type != null && right?.Type != null &&
                        !left.Type.IsError && !right.Type.IsError &&
                        left.Type.ArrayRank == 0 && right.Type.ArrayRank == 0 &&
                        typeSystem.IsNumeric(left.Type.Name) &&
                        typeSystem.IsNumeric(right.Type.Name))
                    {
                        DdfBoundType numericTarget = new DdfBoundType(
                            left.Type.Name == typeSystem.NumericDominantType ||
                            right.Type.Name == typeSystem.NumericDominantType
                                ? typeSystem.NumericDominantType
                                : typeSystem.NumericResultType);
                        ApplyNumericConversion(left, numericTarget);
                        ApplyNumericConversion(right, numericTarget);
                    }
                }
                else if (expression is DdfIrCall call)
                {
                    ExpressionDraft target = BindExpression(call.Target);
                    var arguments = call.Arguments.Select(BindExpression).ToList();
                    DdfBoundSymbol symbol = target == null ? null : FindSymbol(target.ReferencedSymbolId);
                    if (symbol != null && symbol.IsCallable)
                    {
                        draft.CallTargetSymbolId = symbol.Id;
                        for (int index = 0; index < Math.Min(arguments.Count,
                                 symbol.ParameterTypes.Count); index++)
                            ApplyConversion(arguments[index], symbol.ParameterTypes[index]);
                    }
                    else draft.Category = DdfBoundValueCategory.Error;
                }
                else if (expression is DdfIrIndex index)
                {
                    ExpressionDraft target = BindExpression(index.Target);
                    BindExpression(index.Index);
                    draft.Category = target?.Type?.Name == "string" &&
                        target.Type.ArrayRank == 0
                        ? DdfBoundValueCategory.ReadOnlyLocation
                        : target?.Category == DdfBoundValueCategory.ReadOnlyLocation
                            ? DdfBoundValueCategory.ReadOnlyLocation
                            : DdfBoundValueCategory.MutableLocation;
                }
                else if (expression is DdfIrMemberAccess member)
                {
                    ExpressionDraft target = BindExpression(member.Target);
                    if (target != null && fieldsByType.TryGetValue(target.Type.Name,
                            out Dictionary<string, DdfBoundSymbol> fields) &&
                        fields.TryGetValue(member.Name, out DdfBoundSymbol field))
                    {
                        draft.ReferencedSymbolId = field.Id;
                        draft.Category = !field.IsWritable ||
                            target.Category == DdfBoundValueCategory.ReadOnlyLocation
                            ? DdfBoundValueCategory.ReadOnlyLocation
                            : DdfBoundValueCategory.MutableLocation;
                    }
                    else draft.Category = DdfBoundValueCategory.Error;
                }
                draft.Conversion ??= new DdfBoundConversion(
                    DdfBoundConversionKind.Identity, draft.Type, draft.Type);
                return draft;
            }

            private void BindName(ExpressionDraft draft, string name)
            {
                DdfBoundSymbol symbol = Resolve(name);
                if (symbol == null)
                {
                    draft.Category = DdfBoundValueCategory.Error;
                    return;
                }
                draft.ReferencedSymbolId = symbol.Id;
                if (draft.Type.IsError) draft.Type = symbol.Type;
                draft.Category = symbol.IsCallable
                    ? DdfBoundValueCategory.Callable
                        : symbol.Kind == DdfBoundSymbolKind.Structure ||
                          symbol.Kind == DdfBoundSymbolKind.Enumeration ||
                          symbol.Kind == DdfBoundSymbolKind.HostType
                        ? DdfBoundValueCategory.Type
                            : symbol.IsWritable
                                ? DdfBoundValueCategory.MutableLocation
                                : DdfBoundValueCategory.ReadOnlyLocation;
            }

            private DdfBoundSymbol Resolve(string name)
            {
                for (int index = scopes.Count - 1; index >= 0; index--)
                    if (scopes[index].TryGetValue(name, out DdfBoundSymbol local))
                        return local;
                return visibleSymbols.TryGetValue(name, out DdfBoundSymbol symbol)
                    ? symbol : null;
            }

            private DdfBoundSymbol FindSymbol(string id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                foreach (Dictionary<string, DdfBoundSymbol> scope in scopes)
                {
                    DdfBoundSymbol local = scope.Values.FirstOrDefault(item => item.Id == id);
                    if (local != null) return local;
                }
                return visibleSymbols.Values.FirstOrDefault(item => item.Id == id);
            }

            private DdfBoundSymbol CreateDeclaredSymbol(string name,
                DdfBoundSymbolKind kind, int start, int length, DdfBoundType type,
                bool isWritable = true, bool isState = false)
            {
                string id = StableId(kind, moduleIdentity, relativePath, start, name);
                var symbol = new DdfBoundSymbol(id, name, kind, moduleIdentity,
                    relativePath, start, length, type, isWritable: isWritable,
                    isState: isState);
                localSymbols.Add(symbol);
                return symbol;
            }

            private DdfBoundType FindType(DdfIrExpression expression)
            {
                DdfTypedSpan span = typedSpans.LastOrDefault(item =>
                    item.Start == expression.Start && item.Length == expression.Length);
                return span == null ? DdfBoundType.Error : Type(span.Type);
            }

            private void ApplyNumericConversion(ExpressionDraft expression,
                DdfBoundType target)
            {
                if (expression?.Type == null || expression.Type.IsError ||
                    expression.Type.ArrayRank != 0 ||
                    !typeSystem.IsNumeric(expression.Type.Name)) return;
                ApplyConversion(expression, target);
            }

            private static void ApplyConversion(ExpressionDraft expression,
                DdfBoundType target)
            {
                if (expression == null || target == null || target.IsError ||
                    expression.Type == null || expression.Type.IsError) return;
                expression.Conversion = new DdfBoundConversion(
                    expression.Type.Equals(target)
                        ? DdfBoundConversionKind.Identity
                        : DdfBoundConversionKind.Implicit,
                    expression.Type, target);
            }

            private DdfBoundExpression Freeze(ExpressionDraft draft) =>
                new DdfBoundExpression(draft.NodeId, draft.Expression.Start,
                    draft.Expression.Length, draft.Type, draft.Category,
                    draft.ReferencedSymbolId, draft.CallTargetSymbolId,
                    draft.Conversion);

            private void PushScope() => scopes.Add(
                new Dictionary<string, DdfBoundSymbol>(StringComparer.Ordinal));
            private void PopScope() => scopes.RemoveAt(scopes.Count - 1);

            private static bool AlwaysReturns(DdfIrStatement statement)
            {
                if (statement == null) return false;
                if (statement is DdfIrReturn) return true;
                if (statement is DdfIrBlock block)
                    return block.Statements.Any(AlwaysReturns);
                if (statement is DdfIrIf conditional)
                    return conditional.ElseBody != null &&
                        AlwaysReturns(conditional.Body) &&
                        AlwaysReturns(conditional.ElseBody);
                if (statement is DdfIrSelect selection)
                    return selection.Clauses.Count > 0 &&
                        selection.Clauses.All(clause => AlwaysReturns(clause.Body));
                if (statement is DdfIrDoWhile postLoop)
                    return AlwaysReturns(postLoop.Body);
                return false;
            }

            private static bool AlwaysTerminates(DdfIrStatement statement)
            {
                if (statement == null) return false;
                if (statement is DdfIrReturn || statement is DdfIrEnd) return true;
                if (statement is DdfIrBlock block)
                    return block.Statements.Any(AlwaysTerminates);
                if (statement is DdfIrIf conditional)
                    return conditional.ElseBody != null &&
                        AlwaysTerminates(conditional.Body) &&
                        AlwaysTerminates(conditional.ElseBody);
                if (statement is DdfIrSelect selection)
                    return selection.Clauses.Count > 0 &&
                        selection.Clauses.All(clause => AlwaysTerminates(clause.Body));
                if (statement is DdfIrDoWhile postLoop)
                    return AlwaysTerminates(postLoop.Body);
                return false;
            }

            private static bool ContainsEnd(DdfIrStatement statement)
            {
                if (statement == null) return false;
                if (statement is DdfIrEnd) return true;
                if (statement is DdfIrBlock block)
                    return block.Statements.Any(ContainsEnd);
                if (statement is DdfIrIf conditional)
                    return ContainsEnd(conditional.Body) ||
                        ContainsEnd(conditional.ElseBody);
                if (statement is DdfIrSelect selection)
                    return selection.Clauses.Any(clause => ContainsEnd(clause.Body));
                if (statement is DdfIrWhile loop) return ContainsEnd(loop.Body);
                if (statement is DdfIrDoWhile postLoop) return ContainsEnd(postLoop.Body);
                if (statement is DdfIrFor counted) return ContainsEnd(counted.Body);
                return false;
            }
        }

        internal static DdfBoundCompilationUnit Create(string moduleId,
            string moduleVersion, IReadOnlyList<DdfBuildSource> sources,
            IReadOnlyList<DdfIrModule> currentModules,
            IReadOnlyList<DdfIrModule> availableModules,
            IReadOnlyDictionary<string, DdfTypeCheckResult> typeResults,
            DdfLanguageDefinition language, DdfHostCatalogSnapshot hostCatalog)
        {
            string identity = moduleId + "@" + moduleVersion;
            var symbols = new List<DdfBoundSymbol>();
            var visible = new Dictionary<string, DdfBoundSymbol>(StringComparer.Ordinal);
            var fields = new Dictionary<string, Dictionary<string, DdfBoundSymbol>>(
                StringComparer.Ordinal);
            CollectDeclarations(identity, currentModules, symbols, visible, fields, true);
            foreach (DdfIrModule dependency in availableModules.Where(item =>
                         item.ModuleId != moduleId || item.ModuleVersion != moduleVersion))
                CollectDeclarations(dependency.ModuleId + "@" + dependency.ModuleVersion,
                    new[] { dependency }, symbols, visible, fields, false);
            foreach (DdfStandardFunction function in language.StandardLibrary.Functions)
            {
                var symbol = new DdfBoundSymbol(StableId(
                        DdfBoundSymbolKind.StandardFunction,
                        language.StandardLibrary.Id, string.Empty,
                        0, function.Name), function.Name,
                    DdfBoundSymbolKind.StandardFunction,
                    language.StandardLibrary.Id, string.Empty,
                    0, 0, Type(function.ReturnType),
                    function.ParameterTypes.Select(Type),
                    semanticBindingId: function.BindingId);
                visible.TryAdd(function.Name, symbol);
                symbols.Add(symbol);
            }
            if (!string.IsNullOrWhiteSpace(language.Runtime.OutputTarget) &&
                !visible.ContainsKey(language.Runtime.OutputTarget))
            {
                var output = new DdfBoundSymbol(StableId(
                        DdfBoundSymbolKind.HostObject, "runtime", string.Empty, 0,
                        language.Runtime.OutputTarget), language.Runtime.OutputTarget,
                    DdfBoundSymbolKind.HostObject, "runtime", string.Empty, 0, 0,
                    new DdfBoundType(language.Types.Signatures.VoidType),
                    isWritable: false);
                visible.Add(output.Name, output);
                symbols.Add(output);
            }
            if (hostCatalog != null)
            {
                foreach (DdfHostTypeDefinition hostType in hostCatalog.Types)
                {
                    var typeSymbol = new DdfBoundSymbol(StableId(
                            DdfBoundSymbolKind.HostType, "host", string.Empty, 0,
                            hostType.Name), hostType.Name,
                        DdfBoundSymbolKind.HostType, "host", string.Empty, 0, 0,
                        Type(hostType.Name), isWritable: false);
                    visible.TryAdd(hostType.Name, typeSymbol);
                    symbols.Add(typeSymbol);
                    var hostFields = new Dictionary<string, DdfBoundSymbol>(
                        StringComparer.Ordinal);
                    foreach (DdfHostFieldDefinition field in hostType.Fields)
                    {
                        var fieldSymbol = new DdfBoundSymbol(StableId(
                                DdfBoundSymbolKind.Field, "host", hostType.Name, 0,
                                field.Name), field.Name, DdfBoundSymbolKind.Field,
                            "host", hostType.Name, 0, 0, Type(field.TypeName),
                            isWritable: field.CanWrite);
                        hostFields[field.Name] = fieldSymbol;
                        symbols.Add(fieldSymbol);
                    }
                    fields.TryAdd(hostType.Name, hostFields);
                }
                foreach (DdfHostFunctionDefinition function in hostCatalog.Functions)
                {
                    var symbol = new DdfBoundSymbol(StableId(
                            DdfBoundSymbolKind.HostFunction, "host", string.Empty,
                            0, function.Name), function.Name,
                        DdfBoundSymbolKind.HostFunction, "host", string.Empty,
                        0, 0, Type(function.ReturnType),
                        function.Parameters.Select(item => Type(item.TypeName)),
                        hasSideEffects: function.Effect ==
                            DdfHostFunctionEffect.HostMutation);
                    visible.TryAdd(function.Name, symbol);
                    symbols.Add(symbol);
                }
                foreach (DdfHostObjectDefinition hostObject in hostCatalog.Objects)
                {
                    var symbol = new DdfBoundSymbol(StableId(
                            DdfBoundSymbolKind.HostObject, "host", string.Empty,
                            0, hostObject.Name), hostObject.Name,
                        DdfBoundSymbolKind.HostObject, "host", string.Empty,
                        0, 0, Type(hostObject.TypeName),
                        isWritable: hostObject.CanWrite);
                    visible.TryAdd(hostObject.Name, symbol);
                    symbols.Add(symbol);
                }
            }

            var documents = new List<DdfBoundIrDocument>();
            foreach (DdfBuildSource source in sources)
            {
                DdfIrModule module = currentModules.Single(item =>
                    string.Equals(item.DocumentPath, source.Path,
                        StringComparison.OrdinalIgnoreCase));
                typeResults.TryGetValue(source.Path, out DdfTypeCheckResult types);
                var binder = new DocumentBinder(identity, source.RelativePath,
                    module.Program, types?.TypedSpans, language.Types, visible, fields);
                documents.Add(binder.Bind());
                symbols.AddRange(binder.LocalSymbols);
            }
            return new DdfBoundCompilationUnit(identity,
                symbols.GroupBy(item => item.Id, StringComparer.Ordinal)
                    .Select(group => group.First()), documents);
        }

        private static void CollectDeclarations(string identity,
            IEnumerable<DdfIrModule> modules, List<DdfBoundSymbol> symbols,
            Dictionary<string, DdfBoundSymbol> visible,
            Dictionary<string, Dictionary<string, DdfBoundSymbol>> fieldsByType,
            bool includePrivate)
        {
            foreach (DdfIrModule module in modules)
            {
                string path = module.RelativePath;
                foreach (DdfIrMember member in module.Program.Members)
                {
                    string name;
                    DdfBoundSymbol symbol = null;
                    Dictionary<string, DdfBoundSymbol> declaredFields = null;
                    if (member is DdfIrFunction function)
                    {
                        name = function.Name;
                        symbol = new DdfBoundSymbol(StableId(DdfBoundSymbolKind.Function,
                                identity, path, function.NameStart, name), name,
                            DdfBoundSymbolKind.Function, identity, path,
                            function.NameStart, function.NameLength, Type(function.ReturnType),
                            function.Parameters.Select(parameter => Type(parameter.Type)));
                    }
                    else if (member is DdfIrStructure structure)
                    {
                        name = structure.Name;
                        symbol = new DdfBoundSymbol(StableId(DdfBoundSymbolKind.Structure,
                                identity, path, structure.NameStart, name), name,
                            DdfBoundSymbolKind.Structure, identity, path,
                            structure.NameStart, structure.NameLength, Type(name));
                        declaredFields = new Dictionary<string, DdfBoundSymbol>(StringComparer.Ordinal);
                        foreach (DdfIrVariableDeclaration field in structure.Fields)
                        {
                            DdfBoundSymbol fieldSymbol = new DdfBoundSymbol(StableId(
                                    DdfBoundSymbolKind.Field, identity, path, field.Start,
                                    structure.Name + "." + field.Name), field.Name,
                                DdfBoundSymbolKind.Field, identity, path, field.Start,
                                Math.Max(1, field.Name.Length), Type(field.Type),
                                isWritable: !field.IsFixed);
                            declaredFields[field.Name] = fieldSymbol;
                        }
                    }
                    else if (member is DdfIrEnum enumeration)
                    {
                        name = enumeration.Name;
                        symbol = new DdfBoundSymbol(StableId(DdfBoundSymbolKind.Enumeration,
                                identity, path, enumeration.NameStart, name), name,
                            DdfBoundSymbolKind.Enumeration, identity, path,
                            enumeration.NameStart, enumeration.NameLength, Type(name),
                            isWritable: false);
                        declaredFields = new Dictionary<string, DdfBoundSymbol>(StringComparer.Ordinal);
                        foreach (DdfIrEnumMember value in enumeration.Members)
                        {
                            DdfBoundSymbol valueSymbol = new DdfBoundSymbol(StableId(
                                    DdfBoundSymbolKind.EnumerationMember, identity, path,
                                    value.Start, enumeration.Name + "." + value.Name), value.Name,
                                DdfBoundSymbolKind.EnumerationMember, identity, path,
                                value.Start, Math.Max(1, value.Length), Type(enumeration.Name),
                                isWritable: false);
                            declaredFields[value.Name] = valueSymbol;
                        }
                    }
                    else if (member is DdfIrGlobal global &&
                             global.Statement is DdfIrVariableDeclaration variable)
                    {
                        name = variable.Name;
                        symbol = new DdfBoundSymbol(StableId(DdfBoundSymbolKind.Global,
                                identity, path, variable.Start, name), name,
                            DdfBoundSymbolKind.Global, identity, path, variable.Start,
                            Math.Max(1, name.Length), Type(variable.Type),
                            isWritable: !variable.IsFixed, isState: variable.IsState);
                    }
                    if (symbol == null) continue;
                    bool exported = module.Exports.Contains(symbol.Name,
                        StringComparer.Ordinal);
                    if (includePrivate || exported)
                    {
                        symbols.Add(symbol);
                        visible.TryAdd(symbol.Name, symbol);
                        if (declaredFields != null)
                        {
                            fieldsByType.TryAdd(symbol.Name, declaredFields);
                            symbols.AddRange(declaredFields.Values);
                        }
                    }
                }
            }
        }

        private static DdfBoundType Type(DdfIrType type) => type == null
            ? DdfBoundType.Error
            : new DdfBoundType(type.Name, type.ArrayLengths.Count);
        private static DdfBoundType Type(DdfTypeSymbol type) => type == null || type.IsError
            ? DdfBoundType.Error
            : new DdfBoundType(type.Name, type.ArrayRank);
        private static DdfBoundType Type(string displayName)
        {
            string name = displayName ?? string.Empty;
            int rank = 0;
            while (name.EndsWith("[]", StringComparison.Ordinal))
            {
                rank++;
                name = name.Substring(0, name.Length - 2);
            }
            return string.IsNullOrEmpty(name) ? DdfBoundType.Error :
                new DdfBoundType(name, rank);
        }

        private static string StableId(DdfBoundSymbolKind kind, string identity,
            string path, int start, string name)
        {
            string value = kind + "|" + identity + "|" +
                (path ?? string.Empty).Replace('\\', '/') + "|" + start + "|" + name;
            using SHA256 sha = SHA256.Create();
            return "s_" + Convert.ToHexString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(value))).ToLowerInvariant().Substring(0, 24);
        }
    }
}
