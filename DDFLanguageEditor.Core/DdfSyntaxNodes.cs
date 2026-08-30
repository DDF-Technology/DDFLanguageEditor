using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public abstract class DdfSyntaxNode
    {
        protected DdfSyntaxNode(int start, int length)
        {
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
        }

        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
    }

    public sealed class CompilationUnitSyntax : DdfSyntaxNode
    {
        public CompilationUnitSyntax(IReadOnlyList<MemberSyntax> members, int length)
            : base(0, length)
        {
            Members = members;
        }

        public IReadOnlyList<MemberSyntax> Members { get; }
    }

    public abstract class MemberSyntax : DdfSyntaxNode
    {
        protected MemberSyntax(int start, int length) : base(start, length) { }
    }

    public sealed class LibraryDirectiveSyntax : MemberSyntax
    {
        public LibraryDirectiveSyntax(string text, int start, int length) : base(start, length)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public sealed class StructDeclarationSyntax : MemberSyntax
    {
        public StructDeclarationSyntax(string name, int nameStart, int nameLength, int openBraceStart, int closeBraceStart, bool hasCloseBrace, IReadOnlyList<VariableDeclarationStatementSyntax> fields, int start, int length)
            : base(start, length)
        {
            Name = name;
            NameStart = nameStart;
            NameLength = nameLength;
            OpenBraceStart = openBraceStart;
            CloseBraceStart = closeBraceStart;
            HasCloseBrace = hasCloseBrace;
            Fields = fields;
        }

        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
        public int OpenBraceStart { get; }
        public int CloseBraceStart { get; }
        public bool HasCloseBrace { get; }
        public IReadOnlyList<VariableDeclarationStatementSyntax> Fields { get; }
    }

    public sealed class FunctionDeclarationSyntax : MemberSyntax
    {
        public FunctionDeclarationSyntax(string name, int nameStart, int nameLength, IReadOnlyList<ParameterSyntax> parameters, TypeReferenceSyntax returnType, BlockStatementSyntax body, int start, int length)
            : base(start, length)
        {
            Name = name;
            NameStart = nameStart;
            NameLength = nameLength;
            Parameters = parameters;
            ReturnType = returnType;
            Body = body;
        }

        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }
        public TypeReferenceSyntax ReturnType { get; }
        public BlockStatementSyntax Body { get; }
    }

    public sealed class GlobalStatementSyntax : MemberSyntax
    {
        public GlobalStatementSyntax(StatementSyntax statement) : base(statement.Start, statement.Length)
        {
            Statement = statement;
        }

        public StatementSyntax Statement { get; }
    }

    public sealed class ParameterSyntax : DdfSyntaxNode
    {
        public ParameterSyntax(TypeReferenceSyntax type, string name, int nameStart, int nameLength, int start, int length) : base(start, length)
        {
            Type = type;
            Name = name;
            NameStart = nameStart;
            NameLength = nameLength;
        }

        public TypeReferenceSyntax Type { get; }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
    }

    public sealed class TypeReferenceSyntax : DdfSyntaxNode
    {
        public TypeReferenceSyntax(string name, IReadOnlyList<ExpressionSyntax> arrayLengths, int start, int length) : base(start, length)
        {
            Name = name;
            ArrayLengths = arrayLengths;
        }

        public string Name { get; }
        public IReadOnlyList<ExpressionSyntax> ArrayLengths { get; }
    }

    public abstract class StatementSyntax : DdfSyntaxNode
    {
        protected StatementSyntax(int start, int length) : base(start, length) { }
    }

    public sealed class BlockStatementSyntax : StatementSyntax
    {
        public BlockStatementSyntax(IReadOnlyList<StatementSyntax> statements, int closeBraceStart, bool hasCloseBrace, int start, int length) : base(start, length)
        {
            Statements = statements;
            CloseBraceStart = closeBraceStart;
            HasCloseBrace = hasCloseBrace;
        }

        public IReadOnlyList<StatementSyntax> Statements { get; }
        public int CloseBraceStart { get; }
        public bool HasCloseBrace { get; }
    }

    public sealed class VariableDeclarationStatementSyntax : StatementSyntax
    {
        public VariableDeclarationStatementSyntax(TypeReferenceSyntax type, string name, int nameStart, int nameLength, ExpressionSyntax initializer, int start, int length)
            : base(start, length)
        {
            Type = type;
            Name = name;
            NameStart = nameStart;
            NameLength = nameLength;
            Initializer = initializer;
        }

        public TypeReferenceSyntax Type { get; }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
        public ExpressionSyntax Initializer { get; }
    }

    public sealed class IfStatementSyntax : StatementSyntax
    {
        public IfStatementSyntax(ExpressionSyntax condition, StatementSyntax body, int start, int length) : base(start, length)
        {
            Condition = condition;
            Body = body;
        }

        public ExpressionSyntax Condition { get; }
        public StatementSyntax Body { get; }
    }

    public sealed class WhileStatementSyntax : StatementSyntax
    {
        public WhileStatementSyntax(ExpressionSyntax condition, StatementSyntax body, int start, int length) : base(start, length)
        {
            Condition = condition;
            Body = body;
        }

        public ExpressionSyntax Condition { get; }
        public StatementSyntax Body { get; }
    }

    public sealed class DoWhileStatementSyntax : StatementSyntax
    {
        public DoWhileStatementSyntax(StatementSyntax body, ExpressionSyntax condition, int start, int length) : base(start, length)
        {
            Body = body;
            Condition = condition;
        }

        public StatementSyntax Body { get; }
        public ExpressionSyntax Condition { get; }
    }

    public sealed class ForStatementSyntax : StatementSyntax
    {
        public ForStatementSyntax(StatementSyntax initializer, ExpressionSyntax condition, ExpressionSyntax increment, StatementSyntax body, int start, int length)
            : base(start, length)
        {
            Initializer = initializer;
            Condition = condition;
            Increment = increment;
            Body = body;
        }

        public StatementSyntax Initializer { get; }
        public ExpressionSyntax Condition { get; }
        public ExpressionSyntax Increment { get; }
        public StatementSyntax Body { get; }
    }

    public sealed class ReturnStatementSyntax : StatementSyntax
    {
        public ReturnStatementSyntax(ExpressionSyntax expression, int start, int length) : base(start, length)
        {
            Expression = expression;
        }

        public ExpressionSyntax Expression { get; }
    }

    public sealed class BreakStatementSyntax : StatementSyntax
    {
        public BreakStatementSyntax(int start, int length) : base(start, length) { }
    }

    public sealed class EndStatementSyntax : StatementSyntax
    {
        public EndStatementSyntax(int start, int length) : base(start, length) { }
    }

    public sealed class ExpressionStatementSyntax : StatementSyntax
    {
        public ExpressionStatementSyntax(ExpressionSyntax expression, int start, int length) : base(start, length)
        {
            Expression = expression;
        }

        public ExpressionSyntax Expression { get; }
    }

    public abstract class ExpressionSyntax : DdfSyntaxNode
    {
        protected ExpressionSyntax(int start, int length) : base(start, length) { }
    }

    public sealed class MissingExpressionSyntax : ExpressionSyntax
    {
        public MissingExpressionSyntax(int position) : base(position, 0) { }
    }

    public sealed class LiteralExpressionSyntax : ExpressionSyntax
    {
        public LiteralExpressionSyntax(string text, int start, int length) : base(start, length) { Text = text; }
        public string Text { get; }
    }

    public sealed class NameExpressionSyntax : ExpressionSyntax
    {
        public NameExpressionSyntax(string name, int start, int length) : base(start, length) { Name = name; }
        public string Name { get; }
    }

    public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
    {
        public ParenthesizedExpressionSyntax(ExpressionSyntax expression, int start, int length) : base(start, length) { Expression = expression; }
        public ExpressionSyntax Expression { get; }
    }

    public sealed class UnaryExpressionSyntax : ExpressionSyntax
    {
        public UnaryExpressionSyntax(string operatorText, ExpressionSyntax operand, int start, int length) : base(start, length)
        {
            OperatorText = operatorText;
            Operand = operand;
        }

        public string OperatorText { get; }
        public ExpressionSyntax Operand { get; }
    }

    public sealed class BinaryExpressionSyntax : ExpressionSyntax
    {
        public BinaryExpressionSyntax(ExpressionSyntax left, string operatorText, ExpressionSyntax right, int start, int length) : base(start, length)
        {
            Left = left;
            OperatorText = operatorText;
            Right = right;
        }

        public ExpressionSyntax Left { get; }
        public string OperatorText { get; }
        public ExpressionSyntax Right { get; }
    }

    public sealed class PostfixExpressionSyntax : ExpressionSyntax
    {
        public PostfixExpressionSyntax(ExpressionSyntax operand, string operatorText, int start, int length) : base(start, length)
        {
            Operand = operand;
            OperatorText = operatorText;
        }

        public ExpressionSyntax Operand { get; }
        public string OperatorText { get; }
    }

    public sealed class CallExpressionSyntax : ExpressionSyntax
    {
        public CallExpressionSyntax(ExpressionSyntax target, IReadOnlyList<ExpressionSyntax> arguments, int start, int length) : base(start, length)
        {
            Target = target;
            Arguments = arguments;
        }

        public ExpressionSyntax Target { get; }
        public IReadOnlyList<ExpressionSyntax> Arguments { get; }
    }

    public sealed class IndexExpressionSyntax : ExpressionSyntax
    {
        public IndexExpressionSyntax(ExpressionSyntax target, ExpressionSyntax index, int start, int length) : base(start, length)
        {
            Target = target;
            Index = index;
        }

        public ExpressionSyntax Target { get; }
        public ExpressionSyntax Index { get; }
    }

    public sealed class MemberAccessExpressionSyntax : ExpressionSyntax
    {
        public MemberAccessExpressionSyntax(ExpressionSyntax target, string name, int nameStart, int nameLength, int start, int length)
            : base(start, length)
        {
            Target = target;
            Name = name;
            NameStart = nameStart;
            NameLength = nameLength;
        }

        public ExpressionSyntax Target { get; }
        public string Name { get; }
        public int NameStart { get; }
        public int NameLength { get; }
    }
}
