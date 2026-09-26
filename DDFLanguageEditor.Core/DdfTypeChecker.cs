using System;
using System.Collections.Generic;
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

        private sealed class Checker
        {
            private readonly string source;
            private readonly CompilationUnitSyntax root;
            private readonly DdfTypeSystemDefinition typeSystem;
            private readonly DdfStandardLibraryDefinition standardLibrary;
            private readonly DdfHostCatalogSnapshot hostCatalog;
            private readonly Dictionary<string, FunctionInfo> functions = new Dictionary<string, FunctionInfo>(StringComparer.Ordinal);
            private readonly Dictionary<string, StructureInfo> structures = new Dictionary<string, StructureInfo>(StringComparer.Ordinal);
            private readonly Dictionary<string, DdfTypeSymbol> globals = new Dictionary<string, DdfTypeSymbol>(StringComparer.Ordinal);
            private readonly Dictionary<string, DdfHostObjectDefinition> hostObjects = new Dictionary<string, DdfHostObjectDefinition>(StringComparer.Ordinal);
            private readonly List<Dictionary<string, DdfTypeSymbol>> scopes = new List<Dictionary<string, DdfTypeSymbol>>();
            private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
            private readonly List<DdfTypedSpan> spans = new List<DdfTypedSpan>();
            private DdfTypeSymbol currentReturnType = DdfTypeSymbol.Error;

            public Checker(string source, CompilationUnitSyntax root, IEnumerable<CompilationUnitSyntax> externalRoots,
                DdfTypeSystemDefinition typeSystem, DdfStandardLibraryDefinition standardLibrary,
                DdfHostCatalogSnapshot hostCatalog)
            {
                this.source = source;
                this.root = root;
                this.typeSystem = typeSystem;
                this.standardLibrary = standardLibrary;
                this.hostCatalog = hostCatalog;
                var roots = new List<CompilationUnitSyntax> { root };
                if (externalRoots != null) roots.AddRange(externalRoots.Where(item => item != null));
                CollectDeclarations(roots);
            }

            public DdfTypeCheckResult Check()
            {
                foreach (MemberSyntax member in root.Members)
                {
                    if (member is FunctionDeclarationSyntax function) CheckFunction(function);
                    else if (member is StructDeclarationSyntax structure) CheckStructure(structure);
                    else if (member is GlobalStatementSyntax global) CheckStatement(global.Statement);
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
                foreach (VariableDeclarationStatementSyntax field in structure.Fields) CheckVariable(field);
                PopScope();
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
                    Declare(parameter.Name, type);
                    AddSpan(parameter.NameStart, parameter.NameLength, type);
                }
                CheckStatement(function.Body);
                if (typeSystem.Signatures.RequireReturnForNonVoid && !currentReturnType.IsError &&
                    currentReturnType.Name != typeSystem.Signatures.VoidType && !ContainsReturn(function.Body))
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
                else if (statement is WhileStatementSyntax loop)
                {
                    RequireBoolean(CheckExpression(loop.Condition), loop.Condition);
                    CheckStatement(loop.Body);
                }
                else if (statement is DoWhileStatementSyntax doLoop)
                {
                    CheckStatement(doLoop.Body);
                    RequireBoolean(CheckExpression(doLoop.Condition), doLoop.Condition);
                }
                else if (statement is ForStatementSyntax forLoop)
                {
                    PushScope();
                    CheckStatement(forLoop.Initializer);
                    if (forLoop.Condition != null) RequireBoolean(CheckExpression(forLoop.Condition), forLoop.Condition);
                    CheckExpression(forLoop.Increment);
                    CheckStatement(forLoop.Body);
                    PopScope();
                }
                else if (statement is ReturnStatementSyntax returnStatement)
                {
                    DdfTypeSymbol actual = returnStatement.Expression == null
                        ? new DdfTypeSymbol(typeSystem.Signatures.VoidType)
                        : CheckExpression(returnStatement.Expression);
                    if (!CanAssign(currentReturnType, actual)) Report("DDF304", "Il valore restituito è " + actual + ", ma la funzione richiede " + currentReturnType + ".", returnStatement);
                }
                else if (statement is ExpressionStatementSyntax expressionStatement) CheckExpression(expressionStatement.Expression);
            }

            private void CheckVariable(VariableDeclarationStatementSyntax variable)
            {
                DdfTypeSymbol declared = ResolveType(variable.Type, true);
                AddSpan(variable.Type, declared);
                if (!declared.IsError && !structures.ContainsKey(declared.Name) && !typeSystem.CanDeclareVariable(declared.Name))
                    Report("DDF305", "Una variabile non può avere tipo " + declared.Name + ".", variable.Type);
                if (variable.Initializer != null)
                {
                    DdfTypeSymbol actual = CheckExpression(variable.Initializer);
                    DdfConversionContext context = IsSingleCharacterLiteral(variable.Initializer)
                        ? DdfConversionContext.VariableInitializerSingleCharacterLiteral
                        : DdfConversionContext.General;
                    if (!CanAssign(declared, actual, context))
                        Report("DDF301", "Impossibile inizializzare " + declared + " con " + actual + ".", variable.Initializer);
                }
                Declare(variable.Name, declared);
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
                    result = literal.Text.StartsWith("\"", StringComparison.Ordinal)
                        ? new DdfTypeSymbol(typeSystem.Literals.String)
                        : literal.Text.IndexOf('.') >= 0
                            ? new DdfTypeSymbol(typeSystem.Literals.Decimal)
                            : literal.Text == "true" || literal.Text == "false" || literal.Text == "True" || literal.Text == "False"
                                ? new DdfTypeSymbol(typeSystem.Literals.Boolean)
                                : new DdfTypeSymbol(typeSystem.Literals.Integer);
                }
                else if (expression is NameExpressionSyntax name)
                {
                    result = Lookup(name.Name);
                }
                else if (expression is ParenthesizedExpressionSyntax parenthesized) result = CheckExpression(parenthesized.Expression);
                else if (expression is UnaryExpressionSyntax unary) result = CheckUnary(unary);
                else if (expression is BinaryExpressionSyntax binary) result = CheckBinary(binary);
                else if (expression is PostfixExpressionSyntax postfix)
                {
                    DdfTypeSymbol operand = CheckExpression(postfix.Operand);
                    ReportReadOnlyHostTarget(postfix.Operand);
                    result = IsNumeric(operand) ? operand : InvalidOperator(postfix.OperatorText, operand, null, postfix);
                }
                else if (expression is CallExpressionSyntax call) result = CheckCall(call);
                else if (expression is IndexExpressionSyntax index) result = CheckIndex(index);
                else if (expression is MemberAccessExpressionSyntax member) result = CheckMember(member);
                else result = DdfTypeSymbol.Error;
                AddSpan(expression.Start, expression.Length, result);
                return result;
            }

            private DdfTypeSymbol CheckUnary(UnaryExpressionSyntax unary)
            {
                DdfTypeSymbol operand = CheckExpression(unary.Operand);
                if (unary.OperatorText == "!") return IsBoolean(operand) ? operand : InvalidOperator(unary.OperatorText, operand, null, unary);
                return IsNumeric(operand) ? operand : InvalidOperator(unary.OperatorText, operand, null, unary);
            }

            private DdfTypeSymbol CheckBinary(BinaryExpressionSyntax binary)
            {
                DdfTypeSymbol left = CheckExpression(binary.Left);
                DdfTypeSymbol right = CheckExpression(binary.Right);
                string op = binary.OperatorText;
                if (op == "<<")
                {
                    if (!typeSystem.Mutability.AllowsAssignmentTarget(GetAssignmentTargetKind(binary.Left)))
                        Report("DDF308", "La destinazione dell'assegnazione non è modificabile.", binary.Left);
                    ReportReadOnlyHostTarget(binary.Left);
                    if (!CanAssign(left, right)) Report("DDF301", "Impossibile assegnare " + right + " a " + left + ".", binary);
                    return left;
                }
                if (op == ">>") return left;
                if (op == "||" || op == "|&|" || op == "&") return IsBoolean(left) && IsBoolean(right) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if (op == "==" || op == "!!") return CanAssign(left, right) || CanAssign(right, left) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if (op == "<" || op == "<=" || op == ">" || op == ">=" || op == ">><<" || op == "<<>>")
                    return IsNumeric(left) && IsNumeric(right) ? new DdfTypeSymbol(typeSystem.Literals.Boolean) : InvalidOperator(op, left, right, binary);
                if (IsNumeric(left) && IsNumeric(right))
                    return new DdfTypeSymbol(left.Name == typeSystem.NumericDominantType || right.Name == typeSystem.NumericDominantType
                        ? typeSystem.NumericDominantType
                        : typeSystem.NumericResultType);
                return InvalidOperator(op, left, right, binary);
            }

            private DdfTypeSymbol CheckCall(CallExpressionSyntax call)
            {
                var target = call.Target as NameExpressionSyntax;
                List<DdfTypeSymbol> arguments = call.Arguments.Select(CheckExpression).ToList();
                if (target == null || !functions.TryGetValue(target.Name, out FunctionInfo function)) return DdfTypeSymbol.Error;
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
                bool known = typeSystem.IsKnownPrimitive(type.Name) || structures.ContainsKey(type.Name);
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

            private void Declare(string name, DdfTypeSymbol type)
            {
                if (string.IsNullOrEmpty(name)) return;
                if (scopes.Count == 0) globals[name] = type;
                else scopes[scopes.Count - 1][name] = type;
            }

            private void PushScope() => scopes.Add(new Dictionary<string, DdfTypeSymbol>(StringComparer.Ordinal));
            private void PopScope() => scopes.RemoveAt(scopes.Count - 1);

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

            private void ReportReadOnlyHostTarget(ExpressionSyntax expression)
            {
                string rootName = GetRootName(expression);
                if (string.IsNullOrEmpty(rootName) || IsShadowed(rootName) ||
                    !hostObjects.TryGetValue(rootName, out DdfHostObjectDefinition hostObject))
                    return;
                bool writableTarget = hostObject.CanWrite &&
                    (expression is NameExpressionSyntax ||
                     IsWritableHostMember(expression, hostObject));
                if (!writableTarget)
                    Report("DDF309", "L'oggetto host '" + rootName +
                        "' è esposto in sola lettura per questa destinazione.", expression);
            }

            private bool IsWritableHostMember(ExpressionSyntax expression,
                DdfHostObjectDefinition hostObject)
            {
                if (!(expression is MemberAccessExpressionSyntax member) ||
                    hostCatalog == null) return false;
                DdfHostTypeDefinition type = hostCatalog.Types.FirstOrDefault(item =>
                    string.Equals(item.Name, hostObject.TypeName, StringComparison.Ordinal));
                DdfHostFieldDefinition field = type?.Fields.FirstOrDefault(item =>
                    string.Equals(item.Name, member.Name, StringComparison.Ordinal));
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

            private static bool ContainsReturn(StatementSyntax statement)
            {
                if (statement == null) return false;
                if (statement is ReturnStatementSyntax) return true;
                if (statement is BlockStatementSyntax block) return block.Statements.Any(ContainsReturn);
                if (statement is IfStatementSyntax conditional) return conditional.ElseBody != null &&
                    ContainsReturn(conditional.Body) && ContainsReturn(conditional.ElseBody);
                if (statement is WhileStatementSyntax loop) return ContainsReturn(loop.Body);
                if (statement is DoWhileStatementSyntax doLoop) return ContainsReturn(doLoop.Body);
                if (statement is ForStatementSyntax forLoop) return ContainsReturn(forLoop.Body);
                return false;
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
            DdfHostCatalogSnapshot hostCatalog = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            return new Checker(source, root, externalRoots,
                typeSystem ?? DdfLanguageCatalog.Default.Types,
                standardLibrary ?? DdfLanguageCatalog.Default.StandardLibrary,
                hostCatalog).Check();
        }
    }
}
