using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfTypeSymbol : IEquatable<DdfTypeSymbol>
    {
        public static readonly DdfTypeSymbol Error = new DdfTypeSymbol("?", 0, true);

        public DdfTypeSymbol(string name, int arrayRank = 0) : this(name, arrayRank, false) { }

        private DdfTypeSymbol(string name, int arrayRank, bool isError)
        {
            Name = name ?? string.Empty;
            ArrayRank = Math.Max(0, arrayRank);
            IsError = isError;
        }

        public string Name { get; }
        public int ArrayRank { get; }
        public bool IsError { get; }
        public bool IsNumeric => ArrayRank == 0 && (Name == "int" || Name == "float" || Name == "char");
        public bool IsBoolean => ArrayRank == 0 && Name == "bool";
        public string DisplayName => IsError ? "tipo sconosciuto" : Name + string.Concat(Enumerable.Repeat("[]", ArrayRank));

        public DdfTypeSymbol ElementType => ArrayRank > 0 ? new DdfTypeSymbol(Name, ArrayRank - 1) : Error;

        public bool Equals(DdfTypeSymbol other)
        {
            return other != null && IsError == other.IsError && Name == other.Name && ArrayRank == other.ArrayRank;
        }

        public override bool Equals(object obj) => Equals(obj as DdfTypeSymbol);
        public override int GetHashCode() => (Name.GetHashCode() * 397) ^ ArrayRank;
        public override string ToString() => DisplayName;
    }

    public sealed class DdfTypedSpan
    {
        internal DdfTypedSpan(int start, int length, DdfTypeSymbol type)
        {
            Start = start;
            Length = length;
            Type = type;
        }

        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public DdfTypeSymbol Type { get; }
    }

    public sealed class DdfTypeCheckResult
    {
        internal DdfTypeCheckResult(IReadOnlyList<DdfDiagnostic> diagnostics, IReadOnlyList<DdfTypedSpan> typedSpans)
        {
            Diagnostics = diagnostics;
            TypedSpans = typedSpans;
        }

        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DdfTypedSpan> TypedSpans { get; }

        public DdfTypedSpan FindTypeAt(int position)
        {
            DdfTypedSpan exact = TypedSpans
                .Where(span => position >= span.Start && position < span.End)
                .OrderBy(span => span.Length)
                .FirstOrDefault();
            return exact ?? TypedSpans.FirstOrDefault(span => position == span.End);
        }
    }

    public static class DdfTypeChecker
    {
        private sealed class FunctionInfo
        {
            public string Name;
            public DdfTypeSymbol ReturnType;
            public IReadOnlyList<DdfTypeSymbol> Parameters;
        }

        private sealed class StructureInfo
        {
            public string Name;
            public Dictionary<string, DdfTypeSymbol> Fields;
        }

        private sealed class EnumInfo
        {
            public string Name;
            public HashSet<string> Members;
        }

        private sealed class Checker
        {
            private readonly string source;
            private readonly CompilationUnitSyntax root;
            private readonly IReadOnlyList<CompilationUnitSyntax> allRoots;
            private readonly DdfTypeSystemDefinition typeSystem;
            private readonly DdfStandardLibraryDefinition standardLibrary;
            private readonly DdfRuntimeDefinition runtime;
            private readonly DdfHostCatalogSnapshot hostCatalog;
            private readonly Dictionary<string, FunctionInfo> functions = new Dictionary<string, FunctionInfo>(StringComparer.Ordinal);
            private readonly Dictionary<string, StructureInfo> structures = new Dictionary<string, StructureInfo>(StringComparer.Ordinal);
            private readonly Dictionary<string, EnumInfo> enums = new Dictionary<string, EnumInfo>(StringComparer.Ordinal);
            private readonly HashSet<string> ddfStructures = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, DdfTypeSymbol> globals = new Dictionary<string, DdfTypeSymbol>(StringComparer.Ordinal);
            private readonly HashSet<string> fixedGlobals = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, DdfHostObjectDefinition> hostObjects = new Dictionary<string, DdfHostObjectDefinition>(StringComparer.Ordinal);
            private readonly List<Dictionary<string, DdfTypeSymbol>> scopes = new List<Dictionary<string, DdfTypeSymbol>>();
            private readonly List<HashSet<string>> fixedScopes = new List<HashSet<string>>();
            private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
            private readonly List<DdfTypedSpan> spans = new List<DdfTypedSpan>();
            private readonly Dictionary<ExpressionSyntax, DdfTypeSymbol> expressionTypes = new Dictionary<ExpressionSyntax, DdfTypeSymbol>();
            private DdfTypeSymbol currentReturnType = DdfTypeSymbol.Error;
            private int loopDepth;

            public Checker(string source, CompilationUnitSyntax root, IEnumerable<CompilationUnitSyntax> externalRoots,
                DdfTypeSystemDefinition typeSystem, DdfStandardLibraryDefinition standardLibrary,
                DdfHostCatalogSnapshot hostCatalog, DdfRuntimeDefinition runtime)
            {
                this.source = source;
                this.root = root;
                this.typeSystem = typeSystem;
                this.standardLibrary = standardLibrary;
                this.hostCatalog = hostCatalog;
                this.runtime = runtime;
                var roots = new List<CompilationUnitSyntax> { root };
                if (externalRoots != null) roots.AddRange(externalRoots.Where(item => item != null));
                allRoots = roots.AsReadOnly();
                CollectDeclarations(roots);
            }

            public DdfTypeCheckResult Check()
            {
                ValidateTopLevelDeclarations();
                ValidateStructureContainment();
                foreach (MemberSyntax member in root.Members)
                {
                    if (member is FunctionDeclarationSyntax function) CheckFunction(function);
                    else if (member is StructDeclarationSyntax structure) CheckStructure(structure);
                    else if (member is EnumDeclarationSyntax enumeration) CheckEnum(enumeration);
                    else if (member is GlobalStatementSyntax global)
                    {
                        if (global.Statement is VariableDeclarationStatementSyntax variable)
                            CheckVariable(variable, allowState: true);
                        else CheckStatement(global.Statement);
                    }
                }
                diagnostics.Sort((left, right) => left.Start.CompareTo(right.Start));
                spans.Sort((left, right) => left.Start.CompareTo(right.Start));
                return new DdfTypeCheckResult(diagnostics.AsReadOnly(), spans.AsReadOnly());
            }

            private void CollectDeclarations(IEnumerable<CompilationUnitSyntax> roots)
            {
                foreach (CompilationUnitSyntax unit in roots)
                {
                    foreach (StructDeclarationSyntax structure in unit.Members.OfType<StructDeclarationSyntax>())
                    {
                        if (!structures.ContainsKey(structure.Name))
                            structures.Add(structure.Name, new StructureInfo { Name = structure.Name, Fields = new Dictionary<string, DdfTypeSymbol>(StringComparer.Ordinal) });
                        ddfStructures.Add(structure.Name);
                    }
                    foreach (EnumDeclarationSyntax enumeration in unit.Members.OfType<EnumDeclarationSyntax>())
                    {
                        if (!enums.ContainsKey(enumeration.Name))
                            enums.Add(enumeration.Name, new EnumInfo
                            {
                                Name = enumeration.Name,
                                Members = new HashSet<string>(StringComparer.Ordinal)
                            });
                    }
                }
                foreach (CompilationUnitSyntax unit in roots)
                {
                    foreach (MemberSyntax member in unit.Members)
                    {
                        if (member is StructDeclarationSyntax structure && structures.TryGetValue(structure.Name, out StructureInfo info))
                        {
                            foreach (VariableDeclarationStatementSyntax field in structure.Fields)
                                if (!string.IsNullOrEmpty(field.Name) && !info.Fields.ContainsKey(field.Name)) info.Fields.Add(field.Name, ResolveType(field.Type, false));
                        }
                        else if (member is EnumDeclarationSyntax enumeration &&
                                 enums.TryGetValue(enumeration.Name, out EnumInfo enumInfo))
                        {
                            foreach (EnumMemberSyntax value in enumeration.Members)
                                if (!string.IsNullOrEmpty(value.Name)) enumInfo.Members.Add(value.Name);
                        }
                        else if (member is FunctionDeclarationSyntax function && !functions.ContainsKey(function.Name))
                        {
                            functions.Add(function.Name, new FunctionInfo
                            {
                                Name = function.Name,
                                ReturnType = ResolveType(function.ReturnType, false),
                                Parameters = function.Parameters.Select(parameter => ResolveType(parameter.Type, false)).ToList().AsReadOnly()
                            });
                        }
                        else if (member is GlobalStatementSyntax global && global.Statement is VariableDeclarationStatementSyntax variable && !globals.ContainsKey(variable.Name))
                        {
                            globals.Add(variable.Name, ResolveType(variable.Type, false));
                            if (variable.IsFixed) fixedGlobals.Add(variable.Name);
                        }
                    }
                }
                foreach (DdfStandardFunction standard in standardLibrary.Functions)
                {
                    if (!functions.ContainsKey(standard.Name))
                        functions.Add(standard.Name, new FunctionInfo
                        {
                            Name = standard.Name,
                            ReturnType = new DdfTypeSymbol(standard.ReturnType),
                            Parameters = standard.ParameterTypes.Select(type => new DdfTypeSymbol(type)).ToList().AsReadOnly()
                        });
                }
                if (hostCatalog != null)
                {
                    foreach (DdfHostTypeDefinition hostType in hostCatalog.Types)
                    {
                        if (structures.ContainsKey(hostType.Name)) continue;
                        structures.Add(hostType.Name, new StructureInfo
                        {
                            Name = hostType.Name,
                            Fields = hostType.Fields.ToDictionary(field => field.Name,
                                field => HostType(field.TypeName), StringComparer.Ordinal)
                        });
                    }
                    foreach (DdfHostObjectDefinition hostObject in hostCatalog.Objects)
                        if (!globals.ContainsKey(hostObject.Name))
                        {
                            globals.Add(hostObject.Name, HostType(hostObject.TypeName));
                            hostObjects.Add(hostObject.Name, hostObject);
                        }
                    foreach (DdfHostFunctionDefinition hostFunction in hostCatalog.Functions)
                        if (!functions.ContainsKey(hostFunction.Name))
                            functions.Add(hostFunction.Name, new FunctionInfo
                            {
                                Name = hostFunction.Name,
                                ReturnType = HostType(hostFunction.ReturnType),
                                Parameters = hostFunction.Parameters.Select(parameter =>
                                    HostType(parameter.TypeName)).ToList().AsReadOnly()
                            });
                }
            }

            private static DdfTypeSymbol HostType(string typeName)
            {
                int rank = 0;
                string name = typeName ?? string.Empty;
                while (name.EndsWith("[]", StringComparison.Ordinal))
                {
                    rank++;
                    name = name.Substring(0, name.Length - 2);
                }
                return new DdfTypeSymbol(name, rank);
            }

            private void CheckStructure(StructDeclarationSyntax structure)
            {
                PushScope();
                foreach (VariableDeclarationStatementSyntax field in structure.Fields)
                {
                    if (field.IsFixed)
                        Report("DDF317", "fixed è consentito soltanto per valori locali o di modulo.", field);
                    CheckVariable(field);
                }
                PopScope();
            }

            private void CheckEnum(EnumDeclarationSyntax enumeration)
            {
                if (enumeration.Members.Count == 0)
                    Report("DDF324", "Un enum deve dichiarare almeno un valore.",
                        enumeration.NameStart, Math.Max(1, enumeration.NameLength));
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (EnumMemberSyntax value in enumeration.Members)
                {
                    if (!names.Add(value.Name))
                        Report("DDF325", "Il valore enum '" + value.Name +
                            "' è già dichiarato.", value.Start, Math.Max(1, value.Length));
                    AddSpan(value.Start, value.Length,
                        new DdfTypeSymbol(enumeration.Name));
                }
            }

            private void CheckFunction(FunctionDeclarationSyntax function)
            {
                currentReturnType = ResolveType(function.ReturnType, true);
                AddSpan(function.ReturnType, currentReturnType);
                PushScope();
                foreach (ParameterSyntax parameter in function.Parameters)
                {
                    DdfTypeSymbol type = ResolveType(parameter.Type, true);
                    AddSpan(parameter.Type, type);
                    Declare(parameter.Name, type, parameter.NameStart, parameter.NameLength);
                    AddSpan(parameter.NameStart, parameter.NameLength, type);
                }
                CheckStatement(function.Body);
                if (typeSystem.Signatures.RequireReturnForNonVoid && !currentReturnType.IsError &&
                    currentReturnType.Name != typeSystem.Signatures.VoidType && !AlwaysReturns(function.Body))
                {
                    Report("DDF307", "La funzione '" + function.Name + "' deve restituire un valore " + currentReturnType + ".", function.NameStart, Math.Max(1, function.NameLength));
                }
                PopScope();
                currentReturnType = DdfTypeSymbol.Error;
            }

            private void CheckStatement(StatementSyntax statement)
            {
                if (statement == null) return;
                if (statement is BlockStatementSyntax block)
                {
                    PushScope();
                    foreach (StatementSyntax child in block.Statements) CheckStatement(child);
                    PopScope();
                }
                else if (statement is VariableDeclarationStatementSyntax variable) CheckVariable(variable);
                else if (statement is IfStatementSyntax conditional)
                {
                    RequireBoolean(CheckExpression(conditional.Condition), conditional.Condition);
                    CheckStatement(conditional.Body);
                    CheckStatement(conditional.ElseBody);
                }
                else if (statement is SelectStatementSyntax selection)
                {
                    CheckSelect(selection);
                }
                else if (statement is WhileStatementSyntax loop)
                {
                    RequireBoolean(CheckExpression(loop.Condition), loop.Condition);
                    loopDepth++;
                    try { CheckStatement(loop.Body); }
                    finally { loopDepth--; }
                }
                else if (statement is DoWhileStatementSyntax doLoop)
                {
                    loopDepth++;
                    try { CheckStatement(doLoop.Body); }
                    finally { loopDepth--; }
                    RequireBoolean(CheckExpression(doLoop.Condition), doLoop.Condition);
                }
                else if (statement is ForStatementSyntax forLoop)
                {
                    PushScope();
                    CheckStatement(forLoop.Initializer);
                    if (forLoop.Condition != null) RequireBoolean(CheckExpression(forLoop.Condition), forLoop.Condition);
                    CheckExpression(forLoop.Increment);
                    loopDepth++;
                    try { CheckStatement(forLoop.Body); }
                    finally { loopDepth--; }
                    PopScope();
                }
                else if (statement is ReturnStatementSyntax returnStatement)
                {
                    DdfTypeSymbol actual = returnStatement.Expression == null
                        ? new DdfTypeSymbol(typeSystem.Signatures.VoidType)
                        : CheckExpression(returnStatement.Expression);
                    if (!CanAssign(currentReturnType, actual)) Report("DDF304", "Il valore restituito è " + actual + ", ma la funzione richiede " + currentReturnType + ".", returnStatement);
                }
                else if ((statement is BreakStatementSyntax || statement is ContinueStatementSyntax) && loopDepth == 0)
                    Report("DDF323", "L'istruzione può essere usata soltanto all'interno di un ciclo.", statement);
                else if (statement is ExpressionStatementSyntax expressionStatement) CheckExpression(expressionStatement.Expression);
            }

            private void CheckSelect(SelectStatementSyntax selection)
            {
                DdfTypeSymbol selectedType = CheckExpression(selection.Expression);
                var seenCases = new HashSet<string>(StringComparer.Ordinal);
                var coveredEnumMembers = new HashSet<string>(StringComparer.Ordinal);
                var coveredBooleans = new HashSet<bool>();
                int otherwiseCount = 0;
                for (int index = 0; index < selection.Clauses.Count; index++)
                {
                    SelectClauseSyntax clause = selection.Clauses[index];
                    if (clause.IsOtherwise)
                    {
                        otherwiseCount++;
                        if (otherwiseCount > 1)
                            Report("DDF329", "select può dichiarare un solo ramo otherwise.", clause);
                        if (index != selection.Clauses.Count - 1)
                            Report("DDF329", "Il ramo otherwise deve essere l'ultimo ramo di select.", clause);
                    }
                    else
                    {
                        DdfTypeSymbol caseType = CheckExpression(clause.Value);
                        if (!CanCompareEquality(selectedType, caseType))
                            Report("DDF326", "Il caso when di tipo " + caseType +
                                " non è compatibile con il valore select di tipo " +
                                selectedType + ".", clause.Value);
                        if (!IsFixedInitializer(clause.Value))
                            Report("DDF326", "Un caso when deve essere un valore costante deterministico.",
                                clause.Value);
                        string key = GetSelectCaseKey(clause.Value);
                        if (!string.IsNullOrEmpty(key) && !seenCases.Add(key))
                            Report("DDF327", "Il caso when è già dichiarato in questo select.",
                                clause.Value);
                        if (TryGetEnumMember(clause.Value, out string enumName,
                                out string memberName) && enumName == selectedType.Name)
                            coveredEnumMembers.Add(memberName);
                        if (clause.Value is LiteralExpressionSyntax literal &&
                            literal.Kind == DdfLiteralKind.Boolean && literal.BooleanValue.HasValue)
                            coveredBooleans.Add(literal.BooleanValue.Value);
                    }
                    CheckStatement(clause.Body);
                }

                bool exhaustive = otherwiseCount == 1 ||
                    selectedType.IsBoolean && coveredBooleans.Count == 2 ||
                    enums.TryGetValue(selectedType.Name, out EnumInfo selectedEnumeration) &&
                    selectedEnumeration.Members.Count > 0 &&
                    selectedEnumeration.Members.SetEquals(coveredEnumMembers);
                if (!exhaustive)
                    Report("DDF328", "select deve essere esaustivo o terminare con un ramo otherwise.",
                        selection);
            }

            private string GetSelectCaseKey(ExpressionSyntax expression)
            {
                while (expression is ParenthesizedExpressionSyntax group)
                    expression = group.Expression;
                if (TryGetEnumMember(expression, out string enumName, out string memberName))
                    return "enum:" + enumName + "." + memberName;
                if (expression is LiteralExpressionSyntax literal)
                {
                    if (literal.Kind == DdfLiteralKind.Boolean)
                        return "bool:" + literal.BooleanValue.GetValueOrDefault();
                    if (literal.Kind == DdfLiteralKind.Number)
                    {
                        if (int.TryParse(literal.Text, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int integer))
                            return "number:" + integer.ToString(CultureInfo.InvariantCulture);
                        if (double.TryParse(literal.Text, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double floating))
                            return "number:" + floating.ToString("R",
                                CultureInfo.InvariantCulture);
                    }
                    return literal.Kind + ":" + literal.Text;
                }
                if (expression == null || expression.Length <= 0) return string.Empty;
                return "constant:" + new string(source.Substring(expression.Start,
                    Math.Min(expression.Length, source.Length - expression.Start))
                    .Where(character => !char.IsWhiteSpace(character)).ToArray());
            }

            private bool TryGetEnumMember(ExpressionSyntax expression,
                out string enumName, out string memberName)
            {
                while (expression is ParenthesizedExpressionSyntax group)
                    expression = group.Expression;
                if (expression is MemberAccessExpressionSyntax member &&
                    member.Target is NameExpressionSyntax name &&
                    enums.TryGetValue(name.Name, out EnumInfo enumeration) &&
                    enumeration.Members.Contains(member.Name))
                {
                    enumName = name.Name;
                    memberName = member.Name;
                    return true;
                }
                enumName = string.Empty;
                memberName = string.Empty;
                return false;
            }

            private void CheckVariable(VariableDeclarationStatementSyntax variable, bool allowState = false)
            {
                DdfTypeSymbol declared = ResolveType(variable.Type, true);
                AddSpan(variable.Type, declared);
                if (!declared.IsError && !structures.ContainsKey(declared.Name) &&
                    !enums.ContainsKey(declared.Name) && !typeSystem.CanDeclareVariable(declared.Name))
                    Report("DDF305", "Una variabile non può avere tipo " + declared.Name + ".", variable.Type);
                if (variable.IsFixed && variable.Initializer == null)
                    Report("DDF314", "Una dichiarazione fixed richiede un inizializzatore.", variable);
                if (variable.IsFixed && (declared.ArrayRank != 0 || structures.ContainsKey(declared.Name)))
                    Report("DDF317", "fixed accetta soltanto tipi primitivi scalari o enum nominali.", variable.Type);
                if (variable.IsState && !allowState)
                    Report("DDF320", "state è consentito soltanto per valori di modulo.", variable);
                if (variable.IsState && variable.Initializer == null)
                    Report("DDF321", "Una dichiarazione state richiede un inizializzatore.", variable);
                if (variable.IsState && (declared.ArrayRank != 0 || structures.ContainsKey(declared.Name)))
                    Report("DDF322", "La prima versione di state accetta soltanto tipi primitivi scalari.", variable.Type);
                if (variable.Initializer != null)
                {
                    DdfTypeSymbol actual = CheckExpression(variable.Initializer);
                    DdfConversionContext context = IsSingleCharacterLiteral(variable.Initializer)
                        ? DdfConversionContext.VariableInitializerSingleCharacterLiteral
                        : DdfConversionContext.General;
                    if (!CanAssign(declared, actual, context))
                        Report("DDF301", "Impossibile inizializzare " + declared + " con " + actual + ".", variable.Initializer);
                    if (variable.IsFixed && !IsFixedInitializer(variable.Initializer))
                        Report("DDF315", "L'inizializzatore fixed deve essere un'espressione pura composta da letterali, altre costanti e operatori deterministici.", variable.Initializer);
                }
                Declare(variable.Name, declared, variable.NameStart, variable.NameLength, variable.IsFixed);
                AddSpan(variable.NameStart, variable.NameLength, declared);
                foreach (ExpressionSyntax length in variable.Type.ArrayLengths)
                {
                    DdfTypeSymbol lengthType = CheckExpression(length);
                    if (!lengthType.IsError && (lengthType.Name != typeSystem.IndexType || lengthType.ArrayRank != 0))
                        Report("DDF305", "La dimensione di un array deve essere " + typeSystem.IndexType + ".", length);
                }
            }

            private DdfTypeSymbol CheckExpression(ExpressionSyntax expression)
            {
                if (expression == null || expression is MissingExpressionSyntax) return DdfTypeSymbol.Error;
                DdfTypeSymbol result;
                if (expression is LiteralExpressionSyntax literal)
                {
                    result = literal.Kind == DdfLiteralKind.String
                        ? new DdfTypeSymbol(typeSystem.Literals.String)
                        : literal.Kind == DdfLiteralKind.Boolean
                            ? new DdfTypeSymbol(typeSystem.Literals.Boolean)
                            : literal.Text.IndexOf('.') >= 0
                                ? new DdfTypeSymbol(typeSystem.Literals.Decimal)
                                : new DdfTypeSymbol(typeSystem.Literals.Integer);
                }
                else if (expression is NameExpressionSyntax name)
                {
                    result = Lookup(name.Name);
                    if (result.IsError)
                        Report("DDF313", "Valore sconosciuto: '" + name.Name + "'.",
                            name.Start, Math.Max(1, name.Length));
                }
                else if (expression is ParenthesizedExpressionSyntax parenthesized) result = CheckExpression(parenthesized.Expression);
                else if (expression is UnaryExpressionSyntax unary) result = CheckUnary(unary);
                else if (expression is BinaryExpressionSyntax binary) result = CheckBinary(binary);
                else if (expression is PostfixExpressionSyntax postfix)
                {
                    DdfTypeSymbol operand = CheckExpression(postfix.Operand);
                    if (IsFixedTarget(postfix.Operand))
                        Report("DDF316", "Un valore fixed non può essere incrementato o decrementato.", postfix.Operand);
                    else if (!typeSystem.Mutability.AllowsAssignmentTarget(GetAssignmentTargetKind(postfix.Operand)) ||
                        IsReadOnlyStringIndex(postfix.Operand))
                        Report("DDF308", "La destinazione dell'operatore '" + postfix.OperatorText + "' non è modificabile.", postfix.Operand);
                    else if (IsEnumMemberAccess(postfix.Operand))
                        Report("DDF308", "Un valore enum simbolico non è modificabile.", postfix.Operand);
                    ReportReadOnlyHostTarget(postfix.Operand);
                    result = runtime.TryGetPostfixOperation(postfix.OperatorText, out string operation) &&
                        (operation == DdfVmExpressionOperations.Increment || operation == DdfVmExpressionOperations.Decrement) &&
                        IsNumeric(operand)
                        ? operand
                        : InvalidOperator(postfix.OperatorText, operand, null, postfix);
                }
                else if (expression is CallExpressionSyntax call) result = CheckCall(call);
                else if (expression is IndexExpressionSyntax index) result = CheckIndex(index);
                else if (expression is MemberAccessExpressionSyntax member) result = CheckMember(member);
                else result = DdfTypeSymbol.Error;
                expressionTypes[expression] = result;
                AddSpan(expression.Start, expression.Length, result);
                return result;
            }

            private DdfTypeSymbol CheckUnary(UnaryExpressionSyntax unary)
            {
                DdfTypeSymbol operand = CheckExpression(unary.Operand);
                if (!runtime.TryGetUnaryOperation(unary.OperatorText, out string operation))
                    return InvalidOperator(unary.OperatorText, operand, null, unary);
                if (operation == DdfVmExpressionOperations.LogicalNot)
                    return IsBoolean(operand) ? operand : InvalidOperator(unary.OperatorText, operand, null, unary);
                if (operation == DdfVmExpressionOperations.Positive || operation == DdfVmExpressionOperations.Negate)
                    return IsNumeric(operand) ? operand : InvalidOperator(unary.OperatorText, operand, null, unary);
                return InvalidOperator(unary.OperatorText, operand, null, unary);
            }

            private DdfTypeSymbol CheckBinary(BinaryExpressionSyntax binary)
            {
                DdfTypeSymbol left = CheckExpression(binary.Left);
                string op = binary.OperatorText;
                if (!runtime.TryGetBinaryOperation(op, out string operation))
                    return InvalidOperator(op, left, CheckExpression(binary.Right), binary);
                if (operation == DdfVmExpressionOperations.Output) return left;
                DdfTypeSymbol right = CheckExpression(binary.Right);
                if (operation == DdfVmExpressionOperations.Assign)
                {
                    if (IsFixedTarget(binary.Left))
                        Report("DDF316", "Un valore fixed non può essere assegnato.", binary.Left);
                    else if (!typeSystem.Mutability.AllowsAssignmentTarget(GetAssignmentTargetKind(binary.Left)))
                        Report("DDF308", "La destinazione dell'assegnazione non è modificabile.", binary.Left);
                    else if (IsEnumMemberAccess(binary.Left))
                        Report("DDF308", "Un valore enum simbolico non è modificabile.", binary.Left);
                    else if (IsReadOnlyStringIndex(binary.Left))
                        Report("DDF308", "Un carattere di stringa non è una destinazione modificabile.", binary.Left);
                    ReportReadOnlyHostTarget(binary.Left);
                    if (!CanAssign(left, right)) Report("DDF301", "Impossibile assegnare " + right + " a " + left + ".", binary);
                    return left;
                }
                if (operation == DdfVmExpressionOperations.LogicalOr ||
                    operation == DdfVmExpressionOperations.LogicalXor ||
                    operation == DdfVmExpressionOperations.LogicalAnd)
                    return IsBoolean(left) && IsBoolean(right) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if (operation == DdfVmExpressionOperations.Equal || operation == DdfVmExpressionOperations.NotEqual)
                    return CanCompareEquality(left, right) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if (operation == DdfVmExpressionOperations.Less || operation == DdfVmExpressionOperations.LessOrEqual ||
                    operation == DdfVmExpressionOperations.Greater || operation == DdfVmExpressionOperations.GreaterOrEqual ||
                    operation == DdfVmExpressionOperations.BetweenExclusive || operation == DdfVmExpressionOperations.OutsideExclusive)
                    return IsNumeric(left) && IsNumeric(right) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if ((operation == DdfVmExpressionOperations.Add || operation == DdfVmExpressionOperations.Subtract ||
                    operation == DdfVmExpressionOperations.Multiply || operation == DdfVmExpressionOperations.Divide ||
                    operation == DdfVmExpressionOperations.Power || operation == DdfVmExpressionOperations.Root) &&
                    IsNumeric(left) && IsNumeric(right))
                    return new DdfTypeSymbol(left.Name == typeSystem.NumericDominantType || right.Name == typeSystem.NumericDominantType
                        ? typeSystem.NumericDominantType
                        : typeSystem.NumericResultType);
                return InvalidOperator(op, left, right, binary);
            }

            private DdfTypeSymbol CheckCall(CallExpressionSyntax call)
            {
                var target = call.Target as NameExpressionSyntax;
                List<DdfTypeSymbol> arguments = call.Arguments.Select(CheckExpression).ToList();
                if (target == null) return DdfTypeSymbol.Error;
                if (!functions.TryGetValue(target.Name, out FunctionInfo function))
                {
                    Report("DDF312", "Funzione sconosciuta: '" + target.Name + "'.",
                        target.Start, Math.Max(1, target.Length));
                    return DdfTypeSymbol.Error;
                }
                if (typeSystem.Signatures.ExactArity && arguments.Count != function.Parameters.Count)
                    Report("DDF303", "La funzione '" + function.Name + "' richiede " + function.Parameters.Count + " argomenti, ricevuti " + arguments.Count + ".", call);
                int count = Math.Min(arguments.Count, function.Parameters.Count);
                for (int index = 0; index < count; index++)
                    if (!CanAssign(function.Parameters[index], arguments[index])) Report("DDF303", "L'argomento " + (index + 1) + " richiede " + function.Parameters[index] + ", ricevuto " + arguments[index] + ".", call.Arguments[index]);
                AddSpan(target.Start, target.Length, function.ReturnType);
                return function.ReturnType;
            }

            private DdfTypeSymbol CheckIndex(IndexExpressionSyntax index)
            {
                DdfTypeSymbol target = CheckExpression(index.Target);
                DdfTypeSymbol offset = CheckExpression(index.Index);
                if (!offset.IsError && (offset.Name != typeSystem.IndexType || offset.ArrayRank != 0))
                    Report("DDF305", "L'indice deve essere " + typeSystem.IndexType + ".", index.Index);
                if (target.ArrayRank > 0) return target.ElementType;
                string indexedElementType = typeSystem.GetIndexedElementType(target.Name);
                if (!string.IsNullOrEmpty(indexedElementType)) return new DdfTypeSymbol(indexedElementType);
                if (!target.IsError) Report("DDF305", "Il tipo " + target + " non è indicizzabile.", index.Target);
                return DdfTypeSymbol.Error;
            }

            private DdfTypeSymbol CheckMember(MemberAccessExpressionSyntax member)
            {
                if (member.Target is NameExpressionSyntax enumName &&
                    enums.TryGetValue(enumName.Name, out EnumInfo enumeration))
                {
                    DdfTypeSymbol enumType = new DdfTypeSymbol(enumeration.Name);
                    AddSpan(enumName.Start, enumName.Length, enumType);
                    if (enumeration.Members.Contains(member.Name))
                    {
                        AddSpan(member.NameStart, member.NameLength, enumType);
                        return enumType;
                    }
                    Report("DDF305", "Il valore '" + member.Name + "' non esiste nell'enum " +
                        enumeration.Name + ".", member.NameStart, Math.Max(1, member.NameLength));
                    return DdfTypeSymbol.Error;
                }
                DdfTypeSymbol target = CheckExpression(member.Target);
                if (target.ArrayRank == 0 && structures.TryGetValue(target.Name, out StructureInfo structure) && structure.Fields.TryGetValue(member.Name, out DdfTypeSymbol fieldType))
                {
                    AddSpan(member.NameStart, member.NameLength, fieldType);
                    return fieldType;
                }
                if (!target.IsError) Report("DDF305", "Il membro '" + member.Name + "' non esiste in " + target + ".", member.NameStart, Math.Max(1, member.NameLength));
                return DdfTypeSymbol.Error;
            }

            private DdfTypeSymbol ResolveType(TypeReferenceSyntax type, bool reportUnknown)
            {
                if (type == null || string.IsNullOrEmpty(type.Name)) return DdfTypeSymbol.Error;
                if (type.ArrayLengths.Count > 1)
                {
                    Report("DDF310", "Gli array DDF possono avere un solo suffisso di dimensione.", type);
                    return DdfTypeSymbol.Error;
                }
                bool known = typeSystem.IsKnownPrimitive(type.Name) || structures.ContainsKey(type.Name) ||
                    enums.ContainsKey(type.Name);
                if (!known)
                {
                    if (reportUnknown) Report("DDF305", "Tipo sconosciuto: '" + type.Name + "'.", type.Start, Math.Max(1, type.Name.Length));
                    return DdfTypeSymbol.Error;
                }
                return new DdfTypeSymbol(type.Name, type.ArrayLengths.Count);
            }

            private DdfTypeSymbol Lookup(string name)
            {
                for (int index = scopes.Count - 1; index >= 0; index--)
                    if (scopes[index].TryGetValue(name, out DdfTypeSymbol type)) return type;
                return globals.TryGetValue(name, out DdfTypeSymbol global) ? global : DdfTypeSymbol.Error;
            }

            private void Declare(string name, DdfTypeSymbol type, int start, int length,
                bool isFixed = false)
            {
                if (string.IsNullOrEmpty(name)) return;
                if (scopes.Count == 0) return;
                Dictionary<string, DdfTypeSymbol> current = scopes[scopes.Count - 1];
                bool duplicate = current.ContainsKey(name);
                bool shadowsVisible = !typeSystem.Scopes.AllowShadowing &&
                    (globals.ContainsKey(name) || scopes.Take(scopes.Count - 1).Any(scope => scope.ContainsKey(name)));
                if (duplicate || shadowsVisible)
                {
                    Report("DDF311", duplicate
                        ? "Il valore '" + name + "' è già dichiarato in questo scope."
                        : "Il valore '" + name + "' nasconde una dichiarazione già visibile.", start, Math.Max(1, length));
                    return;
                }
                current.Add(name, type);
                if (isFixed) fixedScopes[fixedScopes.Count - 1].Add(name);
            }

            private void PushScope()
            {
                scopes.Add(new Dictionary<string, DdfTypeSymbol>(StringComparer.Ordinal));
                fixedScopes.Add(new HashSet<string>(StringComparer.Ordinal));
            }

            private void PopScope()
            {
                scopes.RemoveAt(scopes.Count - 1);
                fixedScopes.RemoveAt(fixedScopes.Count - 1);
            }

            private bool IsFixedName(string name)
            {
                for (int index = scopes.Count - 1; index >= 0; index--)
                {
                    if (scopes[index].ContainsKey(name)) return fixedScopes[index].Contains(name);
                }
                return fixedGlobals.Contains(name);
            }

            private bool IsFixedTarget(ExpressionSyntax expression)
            {
                while (expression is ParenthesizedExpressionSyntax group) expression = group.Expression;
                if (expression is NameExpressionSyntax name) return IsFixedName(name.Name);
                if (expression is IndexExpressionSyntax index) return IsFixedTarget(index.Target);
                if (expression is MemberAccessExpressionSyntax member) return IsFixedTarget(member.Target);
                return false;
            }

            private bool IsFixedInitializer(ExpressionSyntax expression)
            {
                if (expression == null || expression is MissingExpressionSyntax) return false;
                if (expression is LiteralExpressionSyntax) return true;
                if (expression is NameExpressionSyntax name) return IsFixedName(name.Name);
                if (expression is ParenthesizedExpressionSyntax group)
                    return IsFixedInitializer(group.Expression);
                if (expression is UnaryExpressionSyntax unary)
                    return runtime.TryGetUnaryOperation(unary.OperatorText, out _) &&
                           IsFixedInitializer(unary.Operand);
                if (expression is BinaryExpressionSyntax binary)
                {
                    if (!runtime.TryGetBinaryOperation(binary.OperatorText, out string operation) ||
                        operation == DdfVmExpressionOperations.Assign ||
                        operation == DdfVmExpressionOperations.Output) return false;
                    return IsFixedInitializer(binary.Left) && IsFixedInitializer(binary.Right);
                }
                if (IsEnumMemberAccess(expression)) return true;
                return false;
            }

            private bool IsEnumMemberAccess(ExpressionSyntax expression)
            {
                while (expression is ParenthesizedExpressionSyntax group)
                    expression = group.Expression;
                return expression is MemberAccessExpressionSyntax member &&
                    member.Target is NameExpressionSyntax name &&
                    enums.TryGetValue(name.Name, out EnumInfo enumeration) &&
                    enumeration.Members.Contains(member.Name);
            }

            private void RequireBoolean(DdfTypeSymbol type, DdfSyntaxNode node)
            {
                if (!type.IsError && !IsBoolean(type)) Report("DDF306", "La condizione deve essere " +
                    typeSystem.Literals.Boolean + ", non " + type + ".", node);
            }

            private DdfTypeSymbol InvalidOperator(string op, DdfTypeSymbol left, DdfTypeSymbol right, DdfSyntaxNode node)
            {
                if (!left.IsError && (right == null || !right.IsError))
                    Report("DDF302", "Operatore '" + op + "' non valido per " + left + (right == null ? "." : " e " + right + "."), node);
                return DdfTypeSymbol.Error;
            }

            private bool CanAssign(DdfTypeSymbol target, DdfTypeSymbol value,
                DdfConversionContext context = DdfConversionContext.General)
            {
                if (target.IsError || value.IsError) return typeSystem.Compatibility.ErrorTypeCompatible;
                if (target.Name == value.Name && target.ArrayRank == value.ArrayRank) return true;
                if (target.ArrayRank > 0 || value.ArrayRank > 0)
                {
                    if (typeSystem.Compatibility.ArraysInvariant || target.ArrayRank != value.ArrayRank) return false;
                    return typeSystem.CanImplicitlyConvert(value.Name, target.Name, context);
                }
                return target.ArrayRank == 0 && value.ArrayRank == 0 &&
                    typeSystem.CanImplicitlyConvert(value.Name, target.Name, context);
            }

            private bool IsNumeric(DdfTypeSymbol type) => !type.IsError && type.ArrayRank == 0 &&
                                                           typeSystem.IsNumeric(type.Name);
            private bool IsBoolean(DdfTypeSymbol type) => !type.IsError && type.ArrayRank == 0 &&
                                                           typeSystem.IsBoolean(type.Name);

            private static string GetAssignmentTargetKind(ExpressionSyntax expression)
            {
                if (expression is NameExpressionSyntax) return "Name";
                if (expression is IndexExpressionSyntax) return "Index";
                if (expression is MemberAccessExpressionSyntax) return "Member";
                return string.Empty;
            }

            private bool CanCompareEquality(DdfTypeSymbol left, DdfTypeSymbol right)
            {
                if (left.IsError || right.IsError) return typeSystem.Compatibility.ErrorTypeCompatible;
                if (left.ArrayRank > 0 || right.ArrayRank > 0 ||
                    ddfStructures.Contains(left.Name) || ddfStructures.Contains(right.Name)) return false;
                return CanAssign(left, right) || CanAssign(right, left);
            }

            private bool IsReadOnlyStringIndex(ExpressionSyntax expression)
            {
                if (!(expression is IndexExpressionSyntax index) ||
                    !expressionTypes.TryGetValue(index.Target, out DdfTypeSymbol target)) return false;
                return target.ArrayRank == 0 && target.Name == typeSystem.Literals.String;
            }

            private void ReportReadOnlyHostTarget(ExpressionSyntax expression)
            {
                string rootName = GetRootName(expression);
                if (string.IsNullOrEmpty(rootName) || IsShadowed(rootName) ||
                    !hostObjects.TryGetValue(rootName, out DdfHostObjectDefinition hostObject))
                    return;
                bool writableTarget = hostObject.CanWrite &&
                    IsWritableHostTarget(expression, hostObject.TypeName);
                if (!writableTarget)
                    Report("DDF309", "L'oggetto host '" + rootName +
                        "' è esposto in sola lettura per questa destinazione.", expression);
            }

            private bool IsWritableHostTarget(ExpressionSyntax expression,
                string typeName)
            {
                if (expression is NameExpressionSyntax) return true;
                if (expression is IndexExpressionSyntax index)
                {
                    string indexTargetType = expressionTypes.TryGetValue(index.Target,
                        out DdfTypeSymbol indexResolvedTarget)
                        ? indexResolvedTarget.DisplayName : typeName;
                    if (string.IsNullOrEmpty(indexTargetType) ||
                        !indexTargetType.EndsWith("[]", StringComparison.Ordinal))
                        return false;
                    return IsWritableHostTarget(index.Target, typeName);
                }
                if (!(expression is MemberAccessExpressionSyntax member) ||
                    hostCatalog == null) return false;
                string targetType = expressionTypes.TryGetValue(member.Target,
                    out DdfTypeSymbol resolvedTarget)
                    ? resolvedTarget.DisplayName : typeName;
                if (!IsWritableHostTarget(member.Target, typeName)) return false;
                DdfHostTypeDefinition hostType = hostCatalog.Types.FirstOrDefault(
                    item => string.Equals(item.Name, targetType,
                        StringComparison.Ordinal));
                DdfHostFieldDefinition field = hostType?.Fields.FirstOrDefault(
                    item => string.Equals(item.Name, member.Name,
                        StringComparison.Ordinal));
                return field?.CanWrite == true;
            }

            private bool IsShadowed(string name) => scopes.Any(scope => scope.ContainsKey(name));

            private static string GetRootName(ExpressionSyntax expression)
            {
                while (true)
                {
                    if (expression is NameExpressionSyntax name) return name.Name;
                    if (expression is IndexExpressionSyntax index) expression = index.Target;
                    else if (expression is MemberAccessExpressionSyntax member) expression = member.Target;
                    else return null;
                }
            }

            private void ValidateStructureContainment()
            {
                foreach (StructDeclarationSyntax structure in root.Members.OfType<StructDeclarationSyntax>())
                    if (HasContainmentPath(structure.Name, structure.Name, new HashSet<string>(StringComparer.Ordinal)))
                        Report("DDF312", "La struttura '" + structure.Name +
                            "' ha un ciclo di contenimento e non può essere costruita senza valori nulli.",
                            structure.NameStart, Math.Max(1, structure.NameLength));
            }

            private void ValidateTopLevelDeclarations()
            {
                var ambiguousExternalNames = allRoots.Skip(1)
                    .SelectMany(unit => unit.Members.Select(GetTopLevelName))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();
                foreach (string name in ambiguousExternalNames)
                    Report("DDF311", "La dichiarazione esterna '" + name +
                        "' è esportata da più dipendenze dirette e non può essere " +
                        "risolta in modo univoco.", 0, Math.Min(1, source.Length));

                foreach (MemberSyntax member in root.Members)
                {
                    string name;
                    int start;
                    int length;
                    int count;
                    if (member is StructDeclarationSyntax structure)
                    {
                        name = structure.Name;
                        start = structure.NameStart;
                        length = structure.NameLength;
                        count = allRoots.Sum(unit => unit.Members.OfType<StructDeclarationSyntax>()
                            .Count(item => item.Name == name));
                    }
                    else if (member is EnumDeclarationSyntax enumeration)
                    {
                        name = enumeration.Name;
                        start = enumeration.NameStart;
                        length = enumeration.NameLength;
                        count = allRoots.Sum(unit => unit.Members.OfType<EnumDeclarationSyntax>()
                            .Count(item => item.Name == name));
                    }
                    else if (member is FunctionDeclarationSyntax function)
                    {
                        name = function.Name;
                        start = function.NameStart;
                        length = function.NameLength;
                        count = allRoots.Sum(unit => unit.Members.OfType<FunctionDeclarationSyntax>()
                            .Count(item => item.Name == name));
                    }
                    else if (member is GlobalStatementSyntax global &&
                             global.Statement is VariableDeclarationStatementSyntax variable)
                    {
                        name = variable.Name;
                        start = variable.NameStart;
                        length = variable.NameLength;
                        count = allRoots.Sum(unit => unit.Members.OfType<GlobalStatementSyntax>()
                            .Count(item => item.Statement is VariableDeclarationStatementSyntax candidate &&
                                           candidate.Name == name));
                    }
                    else continue;

                    count = allRoots.Sum(unit => unit.Members.Count(item =>
                        string.Equals(GetTopLevelName(item), name,
                            StringComparison.Ordinal)));

                    if (!string.IsNullOrEmpty(name) && count > 1)
                        Report("DDF311", "La dichiarazione '" + name +
                            "' deve avere un nome univoco nel progetto.", start, Math.Max(1, length));
                }
            }

            private static string GetTopLevelName(MemberSyntax member)
            {
                if (member is StructDeclarationSyntax structure) return structure.Name;
                if (member is EnumDeclarationSyntax enumeration) return enumeration.Name;
                if (member is FunctionDeclarationSyntax function) return function.Name;
                if (member is GlobalStatementSyntax global &&
                    global.Statement is VariableDeclarationStatementSyntax variable)
                    return variable.Name;
                return string.Empty;
            }

            private bool HasContainmentPath(string current, string target, HashSet<string> visited)
            {
                if (!structures.TryGetValue(current, out StructureInfo structure) || !visited.Add(current)) return false;
                foreach (DdfTypeSymbol field in structure.Fields.Values)
                {
                    if (!ddfStructures.Contains(field.Name)) continue;
                    if (field.Name == target || HasContainmentPath(field.Name, target, visited)) return true;
                }
                visited.Remove(current);
                return false;
            }

            private bool AlwaysReturns(StatementSyntax statement)
            {
                if (statement == null) return false;
                if (statement is ReturnStatementSyntax) return true;
                if (statement is BlockStatementSyntax block)
                    return block.Statements.Any(AlwaysReturns);
                if (statement is IfStatementSyntax conditional)
                    return conditional.ElseBody != null && AlwaysReturns(conditional.Body) && AlwaysReturns(conditional.ElseBody);
                if (statement is SelectStatementSyntax selection)
                    return IsSelectExhaustive(selection) &&
                        selection.Clauses.All(clause => AlwaysReturns(clause.Body));
                if (statement is DoWhileStatementSyntax doLoop) return AlwaysReturns(doLoop.Body);
                return false;
            }

            private bool IsSelectExhaustive(SelectStatementSyntax selection)
            {
                if (selection.Clauses.Any(clause => clause.IsOtherwise)) return true;
                if (!expressionTypes.TryGetValue(selection.Expression,
                        out DdfTypeSymbol selectedType) || selectedType.IsError) return false;
                if (selectedType.IsBoolean)
                    return selection.Clauses.Where(clause => !clause.IsOtherwise)
                        .Select(clause => clause.Value as LiteralExpressionSyntax)
                        .Where(literal => literal?.Kind == DdfLiteralKind.Boolean &&
                            literal.BooleanValue.HasValue)
                        .Select(literal => literal.BooleanValue.Value).Distinct().Count() == 2;
                if (!enums.TryGetValue(selectedType.Name, out EnumInfo enumeration))
                    return false;
                var covered = new HashSet<string>(StringComparer.Ordinal);
                foreach (SelectClauseSyntax clause in selection.Clauses)
                    if (TryGetEnumMember(clause.Value, out string enumName,
                            out string memberName) && enumName == selectedType.Name)
                        covered.Add(memberName);
                return enumeration.Members.Count > 0 &&
                    enumeration.Members.SetEquals(covered);
            }

            private bool IsSingleCharacterLiteral(ExpressionSyntax expression)
            {
                if (!(expression is LiteralExpressionSyntax literal) || literal.Text.Length < 2) return false;
                string value = literal.Text.Substring(1, literal.Text.Length - 2);
                return value.Length == 1 || (value.Length == 2 && value[0] == '\\');
            }

            private void AddSpan(DdfSyntaxNode node, DdfTypeSymbol type)
            {
                if (node != null) AddSpan(node.Start, node.Length, type);
            }

            private void AddSpan(int start, int length, DdfTypeSymbol type)
            {
                if (length > 0 && type != null && !type.IsError) spans.Add(new DdfTypedSpan(start, length, type));
            }

            private void Report(string code, string message, DdfSyntaxNode node) => Report(code, message, node.Start, Math.Max(1, node.Length));

            private void Report(string code, string message, int start, int length)
            {
                int line = 1;
                int column = 1;
                for (int index = 0; index < Math.Min(start, source.Length); index++)
                {
                    if (source[index] == '\n') { line++; column = 1; }
                    else if (source[index] != '\r') column++;
                }
                diagnostics.Add(new DdfDiagnostic(code, message, Math.Min(start, Math.Max(0, source.Length - 1)), length, line, column));
            }
        }

        public static DdfTypeCheckResult Check(string source, CompilationUnitSyntax root,
            IEnumerable<CompilationUnitSyntax> externalRoots = null, DdfTypeSystemDefinition typeSystem = null,
            DdfStandardLibraryDefinition standardLibrary = null,
            DdfHostCatalogSnapshot hostCatalog = null, DdfRuntimeDefinition runtime = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            return new Checker(source, root, externalRoots,
                typeSystem ?? DdfLanguageCatalog.Default.Types,
                standardLibrary ?? DdfLanguageCatalog.Default.StandardLibrary,
                hostCatalog, runtime ?? DdfLanguageCatalog.Default.Runtime).Check();
        }
    }
}
