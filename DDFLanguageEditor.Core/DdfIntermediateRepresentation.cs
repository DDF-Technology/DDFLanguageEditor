using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public abstract class DdfIrNode : DdfSyntaxNode
    {
        protected DdfIrNode(int start, int length) : base(start, length) { }
    }

    public sealed class DdfIrProgram : DdfIrNode
    {
        internal DdfIrProgram(IEnumerable<DdfIrMember> members,
            IEnumerable<DdfPlannedSemanticOperation> semanticOperations, int length)
            : base(0, length)
        {
            Members = members.ToList().AsReadOnly();
            SemanticOperations = semanticOperations.ToList().AsReadOnly();
        }
        public const string FormatVersion = "1.0";
        public IReadOnlyList<DdfIrMember> Members { get; }
        public IReadOnlyList<DdfPlannedSemanticOperation> SemanticOperations { get; }
    }

    public abstract class DdfIrMember : DdfIrNode
    {
        protected DdfIrMember(int start, int length) : base(start, length) { }
    }

    public sealed class DdfIrLibrary : DdfIrMember
    {
        internal DdfIrLibrary(string text, int start, int length) : base(start, length) { Text = text; }
        public string Text { get; }
    }

    public sealed class DdfIrStructure : DdfIrMember
    {
        internal DdfIrStructure(string name, int nameStart, int nameLength,
            IEnumerable<DdfIrVariableDeclaration> fields, int start, int length) : base(start, length)
        {
            Name = name; NameStart = nameStart; NameLength = nameLength;
            Fields = fields.ToList().AsReadOnly();
        }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
        public IReadOnlyList<DdfIrVariableDeclaration> Fields { get; }
    }

    public sealed class DdfIrFunction : DdfIrMember
    {
        internal DdfIrFunction(string name, int nameStart, int nameLength,
            IEnumerable<DdfIrParameter> parameters, DdfIrType returnType, DdfIrBlock body,
            int start, int length) : base(start, length)
        {
            Name = name; NameStart = nameStart; NameLength = nameLength;
            Parameters = parameters.ToList().AsReadOnly(); ReturnType = returnType; Body = body;
        }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
        public IReadOnlyList<DdfIrParameter> Parameters { get; }
        public DdfIrType ReturnType { get; }
        public DdfIrBlock Body { get; }
    }

    public sealed class DdfIrGlobal : DdfIrMember
    {
        internal DdfIrGlobal(DdfIrStatement statement) : base(statement.Start, statement.Length) { Statement = statement; }
        public DdfIrStatement Statement { get; }
    }

    public sealed class DdfIrParameter : DdfIrNode
    {
        internal DdfIrParameter(DdfIrType type, string name, int nameStart, int nameLength,
            int start, int length) : base(start, length)
        { Type = type; Name = name; NameStart = nameStart; NameLength = nameLength; }
        public DdfIrType Type { get; }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
    }

    public sealed class DdfIrType : DdfIrNode
    {
        internal DdfIrType(string name, IEnumerable<DdfIrExpression> arrayLengths, int start, int length)
            : base(start, length)
        { Name = name; ArrayLengths = arrayLengths.ToList().AsReadOnly(); }
        public string Name { get; }
        public IReadOnlyList<DdfIrExpression> ArrayLengths { get; }
    }

    public abstract class DdfIrStatement : DdfIrNode
    {
        protected DdfIrStatement(int start, int length) : base(start, length) { }
    }

    public sealed class DdfIrBlock : DdfIrStatement
    {
        internal DdfIrBlock(IEnumerable<DdfIrStatement> statements, int start, int length) : base(start, length)
        { Statements = statements.ToList().AsReadOnly(); }
        public IReadOnlyList<DdfIrStatement> Statements { get; }
    }

    public sealed class DdfIrVariableDeclaration : DdfIrStatement
    {
        internal DdfIrVariableDeclaration(DdfIrType type, string name, DdfIrExpression initializer,
            int start, int length) : base(start, length)
        { Type = type; Name = name; Initializer = initializer; }
        public DdfIrType Type { get; }
        public string Name { get; }
        public DdfIrExpression Initializer { get; }
    }

    public sealed class DdfIrExpressionStatement : DdfIrStatement
    {
        internal DdfIrExpressionStatement(DdfIrExpression expression, int start, int length) : base(start, length)
        { Expression = expression; }
        public DdfIrExpression Expression { get; }
    }

    public sealed class DdfIrIf : DdfIrStatement
    {
        internal DdfIrIf(DdfIrExpression condition, DdfIrStatement body, DdfIrStatement elseBody,
            int start, int length) : base(start, length)
        { Condition = condition; Body = body; ElseBody = elseBody; }
        public DdfIrExpression Condition { get; }
        public DdfIrStatement Body { get; }
        public DdfIrStatement ElseBody { get; }
    }

    public sealed class DdfIrWhile : DdfIrStatement
    {
        internal DdfIrWhile(DdfIrExpression condition, DdfIrStatement body, int start, int length) : base(start, length)
        { Condition = condition; Body = body; }
        public DdfIrExpression Condition { get; }
        public DdfIrStatement Body { get; }
    }

    public sealed class DdfIrDoWhile : DdfIrStatement
    {
        internal DdfIrDoWhile(DdfIrStatement body, DdfIrExpression condition, int start, int length) : base(start, length)
        { Body = body; Condition = condition; }
        public DdfIrStatement Body { get; }
        public DdfIrExpression Condition { get; }
    }

    public sealed class DdfIrFor : DdfIrStatement
    {
        internal DdfIrFor(DdfIrStatement initializer, DdfIrExpression condition,
            DdfIrExpression increment, DdfIrStatement body, int start, int length) : base(start, length)
        { Initializer = initializer; Condition = condition; Increment = increment; Body = body; }
        public DdfIrStatement Initializer { get; }
        public DdfIrExpression Condition { get; }
        public DdfIrExpression Increment { get; }
        public DdfIrStatement Body { get; }
    }

    public sealed class DdfIrReturn : DdfIrStatement
    {
        internal DdfIrReturn(DdfIrExpression expression, int start, int length) : base(start, length) { Expression = expression; }
        public DdfIrExpression Expression { get; }
    }
    public sealed class DdfIrBreak : DdfIrStatement { internal DdfIrBreak(int start, int length) : base(start, length) { } }
    public sealed class DdfIrEnd : DdfIrStatement { internal DdfIrEnd(int start, int length) : base(start, length) { } }

    public abstract class DdfIrExpression : DdfIrNode
    {
        protected DdfIrExpression(int start, int length) : base(start, length) { }
    }
    public sealed class DdfIrMissingExpression : DdfIrExpression { internal DdfIrMissingExpression(int start) : base(start, 0) { } }
    public sealed class DdfIrLiteral : DdfIrExpression
    {
        internal DdfIrLiteral(string text, int start, int length) : base(start, length) { Text = text; }
        public string Text { get; }
    }
    public sealed class DdfIrName : DdfIrExpression
    {
        internal DdfIrName(string name, int start, int length) : base(start, length) { Name = name; }
        public string Name { get; }
    }
    public sealed class DdfIrGroup : DdfIrExpression
    {
        internal DdfIrGroup(DdfIrExpression expression, int start, int length) : base(start, length) { Expression = expression; }
        public DdfIrExpression Expression { get; }
    }
    public sealed class DdfIrUnary : DdfIrExpression
    {
        internal DdfIrUnary(string operatorText, DdfIrExpression operand, int start, int length) : base(start, length)
        { OperatorText = operatorText; Operand = operand; }
        public string OperatorText { get; }
        public DdfIrExpression Operand { get; }
    }
    public sealed class DdfIrBinary : DdfIrExpression
    {
        internal DdfIrBinary(DdfIrExpression left, string operatorText, DdfIrExpression right,
            int start, int length) : base(start, length)
        { Left = left; OperatorText = operatorText; Right = right; }
        public DdfIrExpression Left { get; }
        public string OperatorText { get; }
        public DdfIrExpression Right { get; }
    }
    public sealed class DdfIrPostfix : DdfIrExpression
    {
        internal DdfIrPostfix(DdfIrExpression operand, string operatorText, int start, int length) : base(start, length)
        { Operand = operand; OperatorText = operatorText; }
        public DdfIrExpression Operand { get; }
        public string OperatorText { get; }
    }
    public sealed class DdfIrCall : DdfIrExpression
    {
        internal DdfIrCall(DdfIrExpression target, IEnumerable<DdfIrExpression> arguments, int start, int length)
            : base(start, length) { Target = target; Arguments = arguments.ToList().AsReadOnly(); }
        public DdfIrExpression Target { get; }
        public IReadOnlyList<DdfIrExpression> Arguments { get; }
    }
    public sealed class DdfIrIndex : DdfIrExpression
    {
        internal DdfIrIndex(DdfIrExpression target, DdfIrExpression index, int start, int length) : base(start, length)
        { Target = target; Index = index; }
        public DdfIrExpression Target { get; }
        public DdfIrExpression Index { get; }
    }
    public sealed class DdfIrMemberAccess : DdfIrExpression
    {
        internal DdfIrMemberAccess(DdfIrExpression target, string name, int start, int length) : base(start, length)
        { Target = target; Name = name; }
        public DdfIrExpression Target { get; }
        public string Name { get; }
    }

    public sealed class DdfIrBuildResult
    {
        internal DdfIrBuildResult(DdfIrProgram program, IEnumerable<DdfDiagnostic> diagnostics)
        { Program = program; Diagnostics = diagnostics.ToList().AsReadOnly(); }
        public DdfIrProgram Program { get; }
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public bool Success => Program != null && Diagnostics.Count == 0;
    }

    public static class DdfIrLowerer
    {
        public static DdfIrBuildResult Lower(string source, DdfParseResult parseResult,
            DdfLanguageDefinition language = null)
        {
            if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
            language = language ?? DdfLanguageCatalog.Default;
            IReadOnlyList<DdfPlannedSemanticOperation> plan = parseResult.GenericRoot == null
                ? Array.Empty<DdfPlannedSemanticOperation>()
                : DdfSemanticOperationPlanner.Create(parseResult.GenericRoot, language.Semantics);
            return Lower(source, parseResult.Root, plan, parseResult.GrammarDiagnostics);
        }

        public static DdfIrBuildResult Lower(string source, CompilationUnitSyntax root,
            IEnumerable<DdfPlannedSemanticOperation> semanticOperations = null,
            IEnumerable<DdfDiagnostic> inputDiagnostics = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            var diagnostics = new List<DdfDiagnostic>(inputDiagnostics ?? Array.Empty<DdfDiagnostic>());
            var members = new List<DdfIrMember>();
            foreach (MemberSyntax member in root.Members)
            {
                DdfIrMember lowered = LowerMember(source, member, diagnostics);
                if (lowered != null) members.Add(lowered);
            }
            var program = new DdfIrProgram(members,
                semanticOperations ?? Array.Empty<DdfPlannedSemanticOperation>(), root.Length);
            return new DdfIrBuildResult(program, diagnostics);
        }

        private static DdfIrMember LowerMember(string source, MemberSyntax member, ICollection<DdfDiagnostic> diagnostics)
        {
            if (member is LibraryDirectiveSyntax library)
                return new DdfIrLibrary(library.Text, library.Start, library.Length);
            if (member is StructDeclarationSyntax structure)
                return new DdfIrStructure(structure.Name, structure.NameStart, structure.NameLength,
                    structure.Fields.Select(field => LowerVariable(source, field, diagnostics)),
                    structure.Start, structure.Length);
            if (member is FunctionDeclarationSyntax function)
                return new DdfIrFunction(function.Name, function.NameStart, function.NameLength,
                    function.Parameters.Select(parameter => new DdfIrParameter(LowerType(source, parameter.Type, diagnostics),
                        parameter.Name, parameter.NameStart, parameter.NameLength, parameter.Start, parameter.Length)),
                    LowerType(source, function.ReturnType, diagnostics),
                    (DdfIrBlock)LowerStatement(source, function.Body, diagnostics), function.Start, function.Length);
            if (member is GlobalStatementSyntax global)
                return new DdfIrGlobal(LowerStatement(source, global.Statement, diagnostics));
            ReportUnsupported(source, member, diagnostics, "member");
            return null;
        }

        private static DdfIrType LowerType(string source, TypeReferenceSyntax type, ICollection<DdfDiagnostic> diagnostics) =>
            type == null
                ? new DdfIrType(string.Empty, Array.Empty<DdfIrExpression>(), 0, 0)
                : new DdfIrType(type.Name, type.ArrayLengths.Select(value => LowerExpression(source, value, diagnostics)),
                    type.Start, type.Length);

        private static DdfIrVariableDeclaration LowerVariable(string source,
            VariableDeclarationStatementSyntax variable, ICollection<DdfDiagnostic> diagnostics) =>
            new DdfIrVariableDeclaration(LowerType(source, variable.Type, diagnostics), variable.Name,
                variable.Initializer == null ? null : LowerExpression(source, variable.Initializer, diagnostics),
                variable.Start, variable.Length);

        private static DdfIrStatement LowerStatement(string source, StatementSyntax statement,
            ICollection<DdfDiagnostic> diagnostics)
        {
            if (statement == null) return null;
            if (statement is BlockStatementSyntax block)
                return new DdfIrBlock(block.Statements.Select(value => LowerStatement(source, value, diagnostics)), block.Start, block.Length);
            if (statement is VariableDeclarationStatementSyntax variable) return LowerVariable(source, variable, diagnostics);
            if (statement is ExpressionStatementSyntax expression)
                return new DdfIrExpressionStatement(LowerExpression(source, expression.Expression, diagnostics), expression.Start, expression.Length);
            if (statement is IfStatementSyntax conditional)
                return new DdfIrIf(LowerExpression(source, conditional.Condition, diagnostics),
                    LowerStatement(source, conditional.Body, diagnostics), LowerStatement(source, conditional.ElseBody, diagnostics),
                    conditional.Start, conditional.Length);
            if (statement is WhileStatementSyntax loop)
                return new DdfIrWhile(LowerExpression(source, loop.Condition, diagnostics),
                    LowerStatement(source, loop.Body, diagnostics), loop.Start, loop.Length);
            if (statement is DoWhileStatementSyntax postLoop)
                return new DdfIrDoWhile(LowerStatement(source, postLoop.Body, diagnostics),
                    LowerExpression(source, postLoop.Condition, diagnostics), postLoop.Start, postLoop.Length);
            if (statement is ForStatementSyntax forLoop)
                return new DdfIrFor(LowerStatement(source, forLoop.Initializer, diagnostics),
                    LowerExpression(source, forLoop.Condition, diagnostics), LowerExpression(source, forLoop.Increment, diagnostics),
                    LowerStatement(source, forLoop.Body, diagnostics), forLoop.Start, forLoop.Length);
            if (statement is ReturnStatementSyntax returned)
                return new DdfIrReturn(LowerExpression(source, returned.Expression, diagnostics), returned.Start, returned.Length);
            if (statement is BreakStatementSyntax) return new DdfIrBreak(statement.Start, statement.Length);
            if (statement is EndStatementSyntax) return new DdfIrEnd(statement.Start, statement.Length);
            ReportUnsupported(source, statement, diagnostics, "statement");
            return null;
        }

        private static DdfIrExpression LowerExpression(string source, ExpressionSyntax expression,
            ICollection<DdfDiagnostic> diagnostics)
        {
            if (expression == null) return null;
            if (expression is MissingExpressionSyntax) return new DdfIrMissingExpression(expression.Start);
            if (expression is LiteralExpressionSyntax literal) return new DdfIrLiteral(literal.Text, literal.Start, literal.Length);
            if (expression is NameExpressionSyntax name) return new DdfIrName(name.Name, name.Start, name.Length);
            if (expression is ParenthesizedExpressionSyntax group)
                return new DdfIrGroup(LowerExpression(source, group.Expression, diagnostics), group.Start, group.Length);
            if (expression is UnaryExpressionSyntax unary)
                return new DdfIrUnary(unary.OperatorText, LowerExpression(source, unary.Operand, diagnostics), unary.Start, unary.Length);
            if (expression is BinaryExpressionSyntax binary)
                return new DdfIrBinary(LowerExpression(source, binary.Left, diagnostics), binary.OperatorText,
                    LowerExpression(source, binary.Right, diagnostics), binary.Start, binary.Length);
            if (expression is PostfixExpressionSyntax postfix)
                return new DdfIrPostfix(LowerExpression(source, postfix.Operand, diagnostics), postfix.OperatorText, postfix.Start, postfix.Length);
            if (expression is CallExpressionSyntax call)
                return new DdfIrCall(LowerExpression(source, call.Target, diagnostics),
                    call.Arguments.Select(value => LowerExpression(source, value, diagnostics)), call.Start, call.Length);
            if (expression is IndexExpressionSyntax index)
                return new DdfIrIndex(LowerExpression(source, index.Target, diagnostics),
                    LowerExpression(source, index.Index, diagnostics), index.Start, index.Length);
            if (expression is MemberAccessExpressionSyntax member)
                return new DdfIrMemberAccess(LowerExpression(source, member.Target, diagnostics), member.Name,
                    member.Start, member.Length);
            ReportUnsupported(source, expression, diagnostics, "expression");
            return new DdfIrMissingExpression(expression.Start);
        }

        private static void ReportUnsupported(string source, DdfSyntaxNode node,
            ICollection<DdfDiagnostic> diagnostics, string category)
        {
            int start = Math.Min(node?.Start ?? 0, Math.Max(0, source.Length - 1));
            GenericSyntaxUtilities.GetLineAndColumn(source, start, out int line, out int column);
            diagnostics.Add(new DdfDiagnostic("DDFIR001", "Unsupported " + category +
                " during IR lowering: " + (node?.GetType().Name ?? "null") + ".", start,
                Math.Max(1, node?.Length ?? 1), line, column, DdfDiagnosticSeverity.Error,
                null, null, DdfDiagnosticDomain.EngineCapability));
        }
    }
}
