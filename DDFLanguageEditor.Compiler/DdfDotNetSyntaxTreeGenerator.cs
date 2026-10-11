using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DDFLanguageEditor.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using RoslynCompilationUnitSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
using RoslynExpressionStatementSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax;
using RoslynExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax;
using RoslynForStatementSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax;
using RoslynParameterSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax;
using RoslynStatementSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetSyntaxGenerationResult
    {
        internal DdfDotNetSyntaxGenerationResult(IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            SyntaxTrees = (syntaxTrees ?? Array.Empty<SyntaxTree>()).ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>()).ToList().AsReadOnly();
        }

        public IReadOnlyList<SyntaxTree> SyntaxTrees { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => SyntaxTrees.Count > 0 && Issues.Count == 0;
    }

    /// <summary>
    /// Closed vertical slice of the safe C# generator. It never parses DDF
    /// text as C#: all nodes, tokens, identifiers and literals use Roslyn factories.
    /// Unsupported IR is rejected rather than approximated.
    /// </summary>
    public static class DdfDotNetSyntaxTreeGenerator
    {
        internal const string SourceSpanAnnotationKind = "DDF.SourceSpan";

        private sealed class Generator
        {
            private readonly DdfDotNetCompilationWorkItem workItem;
            private readonly DdfClrTypeMappingDefinition mapping;
            private readonly DdfDotNetLinkNameTable linkNames;
            private readonly List<DdfDotNetContractIssue> issues;
            private readonly Dictionary<string, Queue<DdfBoundExpression>> expressions;
            private readonly string relativePath;
            private readonly string documentInitializerName;
            private readonly bool includeModuleInitializer;
            private readonly IReadOnlyList<string> documentInitializerNames;
            private readonly SourceText sourceText;

            internal Generator(DdfDotNetCompilationWorkItem workItem,
                DdfClrTypeMappingDefinition mapping, DdfBoundIrDocument document,
                List<DdfDotNetContractIssue> issues,
                DdfDotNetLinkNameTable linkNames, string documentInitializerName,
                bool includeModuleInitializer,
                IReadOnlyList<string> documentInitializerNames)
            {
                this.workItem = workItem;
                this.mapping = mapping;
                this.issues = issues;
                this.linkNames = linkNames;
                relativePath = document.RelativePath;
                this.documentInitializerName = documentInitializerName;
                this.includeModuleInitializer = includeModuleInitializer;
                this.documentInitializerNames = documentInitializerNames;
                sourceText = SourceText.From(workItem.Unit.Sources.Single(item =>
                    string.Equals(item.RelativePath, relativePath,
                        StringComparison.Ordinal)).SourceText, Encoding.UTF8);
                expressions = document.Expressions
                    .GroupBy(item => Key(item.Start, item.Length), StringComparer.Ordinal)
                    .ToDictionary(group => group.Key,
                        group => new Queue<DdfBoundExpression>(group.OrderBy(item =>
                            item.NodeId, StringComparer.Ordinal)), StringComparer.Ordinal);
            }

            internal RoslynCompilationUnitSyntax Generate(DdfIrProgram program)
            {
                var declarations = new List<MemberDeclarationSyntax>();
                foreach (DdfIrMember member in program.Members)
                {
                    if (member is DdfIrLibrary) continue;
                    if (member is DdfIrFunction function)
                    {
                        MethodDeclarationSyntax method = GenerateFunction(function);
                        if (method != null) declarations.Add(method);
                    }
                    else if (member is DdfIrStructure structure)
                    {
                        ClassDeclarationSyntax generated = GenerateStructure(structure);
                        if (generated != null) declarations.Add(generated);
                    }
                    else if (member is DdfIrGlobal global)
                    {
                        FieldDeclarationSyntax generated = GenerateGlobal(global);
                        if (generated != null) declarations.Add(generated);
                    }
                    else Unsupported(member,
                        "Dichiarazione non supportata dallo slice Roslyn corrente.");
                }
                MethodDeclarationSyntax documentInitializer =
                    GenerateDocumentInitializer(program);
                if (documentInitializer != null)
                    declarations.Add(documentInitializer);
                if (includeModuleInitializer)
                {
                    declarations.Add(GenerateInitializationState());
                    MethodDeclarationSyntax moduleInitializer =
                        GenerateModuleInitializer();
                    if (moduleInitializer != null)
                        declarations.Add(moduleInitializer);
                }
                ClassDeclarationSyntax container = SyntaxFactory.ClassDeclaration(
                        workItem.GeneratedNames.ContainerTypeName)
                    .AddModifiers(SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.PartialKeyword))
                    .AddMembers(declarations.ToArray());
                NamespaceDeclarationSyntax @namespace = SyntaxFactory.NamespaceDeclaration(
                        DdfDotNetNameMangler.CreateNamespaceSyntax(
                            workItem.GeneratedNames.NamespaceName))
                    .AddMembers(container);
                return SyntaxFactory.CompilationUnit()
                    .AddUsings(
                        SyntaxFactory.UsingDirective(DdfDotNetNameMangler
                            .CreateNamespaceSyntax("System")),
                        SyntaxFactory.UsingDirective(DdfDotNetNameMangler
                            .CreateNamespaceSyntax("System.Collections.Generic")),
                        SyntaxFactory.UsingDirective(DdfDotNetNameMangler
                            .CreateNamespaceSyntax("DDFLanguageEditor.Runtime")))
                    .AddMembers(@namespace)
                    .WithLeadingTrivia(
                        SyntaxFactory.Trivia(SyntaxFactory.LineDirectiveTrivia(
                            SyntaxFactory.Token(CSharpSyntaxKind.HiddenKeyword),
                            isActive: true)),
                        SyntaxFactory.ElasticCarriageReturnLineFeed)
                    .NormalizeWhitespace();
            }

            private MethodDeclarationSyntax GenerateFunction(DdfIrFunction function)
            {
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Function,
                    function.NameStart, function.Name);
                if (symbol == null) return null;
                TypeSyntax returnType = GenerateType(Type(function.ReturnType), function);
                if (returnType == null) return null;
                var parameters = new List<RoslynParameterSyntax>
                {
                    RuntimeContextParameter()
                };
                foreach (DdfIrParameter parameter in function.Parameters)
                {
                    DdfBoundSymbol parameterSymbol = FindDeclared(
                        DdfBoundSymbolKind.Parameter, parameter.NameStart, parameter.Name);
                    TypeSyntax parameterType = GenerateType(Type(parameter.Type), parameter);
                    if (parameterSymbol == null || parameterType == null) return null;
                    parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(
                            workItem.GeneratedNames.GetIdentifier(parameterSymbol.Id)))
                        .WithType(parameterType));
                }
                BlockSyntax body = GenerateBlock(function.Body);
                if (body == null) return null;
                var prologue = new List<RoslynStatementSyntax>
                {
                    RuntimeDebugCall("EnterFunction", symbol.Id,
                        Location(function))
                };
                foreach (DdfIrParameter parameter in function.Parameters)
                {
                    DdfBoundSymbol parameterSymbol = FindDeclared(
                        DdfBoundSymbolKind.Parameter, parameter.NameStart,
                        parameter.Name);
                    if (parameterSymbol == null) return null;
                    prologue.Add(RegisterDebugValue("RegisterDebugParameter",
                        parameterSymbol, Type(parameter.Type).DisplayName,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(parameterSymbol.Id))));
                }
                prologue.Add(SyntaxFactory.TryStatement(body,
                        default,
                        SyntaxFactory.FinallyClause(SyntaxFactory.Block(
                            RuntimeDebugCall("ExitFunction", symbol.Id)))));
                body = SyntaxFactory.Block(prologue);
                return SyntaxFactory.MethodDeclaration(returnType,
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .GetIdentifier(symbol.Id)))
                    .AddModifiers(SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .WithParameterList(SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(parameters)))
                    .WithBody(body);
            }

            private ClassDeclarationSyntax GenerateStructure(DdfIrStructure structure)
            {
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Structure,
                    structure.NameStart, structure.Name);
                if (symbol == null) return null;
                var fields = new List<MemberDeclarationSyntax>();
                var initializers = new List<RoslynStatementSyntax>();
                var debugMembers = new List<(DdfBoundSymbol Symbol,
                    string TypeName)>();
                foreach (DdfIrVariableDeclaration field in structure.Fields)
                {
                    DdfBoundSymbol fieldSymbol = FindDeclared(DdfBoundSymbolKind.Field,
                        field.Start, field.Name);
                    TypeSyntax type = GenerateType(Type(field.Type), field.Type);
                    if (fieldSymbol == null || type == null) return null;
                    debugMembers.Add((fieldSymbol, Type(field.Type).DisplayName));
                    RoslynExpressionSyntax initializer = field.Initializer == null
                        ? DefaultInitializer(field)
                        : GenerateExpression(field.Initializer);
                    if (issues.Count != 0) return null;
                    VariableDeclaratorSyntax declarator = SyntaxFactory.VariableDeclarator(
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .GetIdentifier(fieldSymbol.Id)));
                    fields.Add(SyntaxFactory.FieldDeclaration(
                            SyntaxFactory.VariableDeclaration(type)
                                .AddVariables(declarator))
                        .AddModifiers(SyntaxFactory.Token(
                            CSharpSyntaxKind.PublicKeyword)));
                    if (initializer != null)
                    {
                        initializers.Add(Tick(field));
                        initializers.Add(SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                CSharpSyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.MemberAccessExpression(
                                    CSharpSyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.ThisExpression(),
                                    SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                    .GetIdentifier(fieldSymbol.Id))), initializer)));
                    }
                }
                ConstructorDeclarationSyntax constructor =
                    SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(
                            workItem.GeneratedNames.GetIdentifier(symbol.Id)))
                        .AddModifiers(SyntaxFactory.Token(
                            CSharpSyntaxKind.PublicKeyword))
                        .AddParameterListParameters(RuntimeContextParameter())
                        .WithBody(SyntaxFactory.Block(initializers));
                fields.Add(constructor);
                fields.Add(GenerateStructureDebugMembers(debugMembers));
                return SyntaxFactory.ClassDeclaration(SyntaxFactory.Identifier(
                        workItem.GeneratedNames.GetIdentifier(symbol.Id)))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.SealedKeyword))
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.IdentifierName(
                            "IDdfRuntimeDebugStructure")))
                    .AddMembers(fields.ToArray());
            }

            private MethodDeclarationSyntax GenerateStructureDebugMembers(
                IEnumerable<(DdfBoundSymbol Symbol, string TypeName)> members)
            {
                var values = members.Select(member =>
                    (RoslynExpressionSyntax)SyntaxFactory.ObjectCreationExpression(
                            SyntaxFactory.IdentifierName("DdfRuntimeDebugMember"))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(member.Symbol.Id))),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(member.TypeName))),
                            SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ThisExpression(),
                                SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                    .GetIdentifier(member.Symbol.Id)))))).ToArray();
                ArrayCreationExpressionSyntax array =
                    SyntaxFactory.ArrayCreationExpression(
                            SyntaxFactory.ArrayType(SyntaxFactory.IdentifierName(
                                    "DdfRuntimeDebugMember"))
                                .AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier(
                                    SyntaxFactory.SingletonSeparatedList<
                                        RoslynExpressionSyntax>(
                                        SyntaxFactory.OmittedArraySizeExpression()))))
                        .WithInitializer(SyntaxFactory.InitializerExpression(
                            CSharpSyntaxKind.ArrayInitializerExpression,
                            SyntaxFactory.SeparatedList(values)));
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.GenericName("IReadOnlyList")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntimeDebugMember")),
                        SyntaxFactory.Identifier("GetDebugMembers"))
                    .AddModifiers(SyntaxFactory.Token(
                        CSharpSyntaxKind.PublicKeyword))
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(array)));
            }

            private FieldDeclarationSyntax GenerateGlobal(DdfIrGlobal global)
            {
                if (global.Statement is not DdfIrVariableDeclaration variable)
                {
                    Unsupported(global, "Dichiarazione globale non valida.");
                    return null;
                }
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Global,
                    variable.Start, variable.Name);
                TypeSyntax type = GenerateType(Type(variable.Type), variable.Type);
                if (symbol == null || type == null) return null;
                VariableDeclaratorSyntax declarator = SyntaxFactory.VariableDeclarator(
                    SyntaxFactory.Identifier(workItem.GeneratedNames
                        .GetIdentifier(symbol.Id)));
                return SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(type).AddVariables(declarator))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword));
            }

            private MethodDeclarationSyntax GenerateDocumentInitializer(
                DdfIrProgram program)
            {
                var statements = new List<RoslynStatementSyntax>();
                foreach (DdfIrGlobal global in program.Members.OfType<DdfIrGlobal>())
                {
                    if (global.Statement is not DdfIrVariableDeclaration variable)
                    {
                        Unsupported(global, "Dichiarazione globale non valida.");
                        return null;
                    }
                    DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Global,
                        variable.Start, variable.Name);
                    RoslynExpressionSyntax initializer = variable.Initializer == null
                        ? DefaultInitializer(variable)
                        : GenerateExpression(variable.Initializer);
                    if (symbol == null || issues.Count != 0) return null;
                    statements.Add(Tick(variable));
                    if (initializer != null)
                        statements.Add(SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                CSharpSyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                    .GetIdentifier(symbol.Id)), initializer)));
                    statements.Add(RegisterDebugValue("RegisterDebugGlobal",
                        symbol, Type(variable.Type).DisplayName,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(symbol.Id))));
                }
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(documentInitializerName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .AddParameterListParameters(RuntimeContextParameter())
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private FieldDeclarationSyntax GenerateInitializationState() =>
                SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.BoolKeyword)))
                    .AddVariables(SyntaxFactory.VariableDeclarator(
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .InitializationStateName))))
                .AddModifiers(
                    SyntaxFactory.Token(CSharpSyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword));

            private MethodDeclarationSyntax GenerateModuleInitializer()
            {
                var statements = new List<RoslynStatementSyntax>
                {
                    SyntaxFactory.IfStatement(
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .InitializationStateName),
                        SyntaxFactory.ReturnStatement())
                };
                foreach (string dependency in workItem.DependencyIdentities)
                {
                    if (linkNames == null)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC406",
                            "Tabella link richiesta per inizializzare " + dependency + "."));
                        return null;
                    }
                    DdfDotNetLinkedModuleName linked;
                    try { linked = linkNames.ResolveModule(dependency); }
                    catch (KeyNotFoundException exception)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC406",
                            exception.Message));
                        return null;
                    }
                    RoslynExpressionSyntax target =
                        DdfDotNetNameMangler.CreateQualifiedExpression(
                            linked.NamespaceName.Split('.').Concat(new[]
                            {
                                linked.ContainerTypeName,
                                linked.ModuleInitializerName
                            }).ToArray());
                    statements.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(target)
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Context")))));
                }
                foreach (string initializer in documentInitializerNames)
                    statements.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName(initializer))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Context")))));
                statements.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        CSharpSyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .InitializationStateName),
                        SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.TrueLiteralExpression))));
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .ModuleInitializerName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .AddParameterListParameters(RuntimeContextParameter())
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private static RoslynParameterSyntax RuntimeContextParameter() =>
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(
                        "DdfRuntime_Context"))
                    .WithType(SyntaxFactory.IdentifierName("DdfRuntimeContext"));

            private BlockSyntax GenerateBlock(DdfIrBlock block)
            {
                if (block == null) return null;
                var statements = new List<RoslynStatementSyntax>();
                foreach (DdfIrStatement statement in block.Statements)
                {
                    statements.Add(Tick(statement));
                    RoslynStatementSyntax generated = GenerateStatement(statement);
                    if (generated != null) statements.Add(generated);
                    if (statement is DdfIrVariableDeclaration variable)
                    {
                        RoslynStatementSyntax registration = RegisterLocal(variable);
                        if (registration != null) statements.Add(registration);
                    }
                }
                return DebugScope(statements);
            }

            private RoslynStatementSyntax GenerateStatement(DdfIrStatement statement)
            {
                if (statement is DdfIrBlock block) return GenerateBlock(block);
                if (statement is DdfIrVariableDeclaration variable)
                {
                    DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Local,
                        variable.Start, variable.Name);
                    TypeSyntax type = GenerateType(Type(variable.Type), variable);
                    RoslynExpressionSyntax initializer = GenerateExpression(variable.Initializer);
                    if (symbol == null || type == null ||
                        (variable.Initializer != null && initializer == null)) return null;
                    VariableDeclaratorSyntax declarator = SyntaxFactory.VariableDeclarator(
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .GetIdentifier(symbol.Id)));
                    if (initializer != null)
                        declarator = declarator.WithInitializer(
                            SyntaxFactory.EqualsValueClause(initializer));
                    else
                    {
                        RoslynExpressionSyntax defaultValue = DefaultInitializer(variable);
                        if (defaultValue == null && issues.Count == 0 &&
                            mapping.TryGetPrimitive(Type(variable.Type).Name,
                                out DdfClrPrimitiveTypeMapping _))
                            defaultValue = SyntaxFactory.DefaultExpression(type);
                        if (defaultValue != null)
                            declarator = declarator.WithInitializer(
                                SyntaxFactory.EqualsValueClause(defaultValue));
                    }
                    return SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(type)
                            .AddVariables(declarator));
                }
                if (statement is DdfIrExpressionStatement expression)
                {
                    RoslynExpressionSyntax value = GenerateExpression(expression.Expression);
                    return value == null ? null : SyntaxFactory.ExpressionStatement(value);
                }
                if (statement is DdfIrReturn returned)
                {
                    RoslynExpressionSyntax value = GenerateExpression(returned.Expression);
                    return returned.Expression == null
                        ? SyntaxFactory.ReturnStatement()
                        : value == null ? null : SyntaxFactory.ReturnStatement(value);
                }
                if (statement is DdfIrBreak) return SyntaxFactory.BreakStatement();
                if (statement is DdfIrEnd)
                    return SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.IdentifierName("End"))));
                if (statement is DdfIrIf conditional)
                {
                    RoslynExpressionSyntax condition = GenerateExpression(conditional.Condition);
                    RoslynStatementSyntax body = GenerateEmbedded(conditional.Body);
                    RoslynStatementSyntax elseBody = conditional.ElseBody == null
                        ? null : GenerateEmbedded(conditional.ElseBody);
                    if (condition == null || body == null ||
                        (conditional.ElseBody != null && elseBody == null)) return null;
                    return SyntaxFactory.IfStatement(condition, body,
                        elseBody == null ? null : SyntaxFactory.ElseClause(elseBody));
                }
                if (statement is DdfIrWhile loop)
                {
                    RoslynExpressionSyntax condition = GenerateExpression(loop.Condition);
                    RoslynStatementSyntax body = GenerateEmbedded(loop.Body);
                    return condition == null || body == null ? null :
                        SyntaxFactory.WhileStatement(condition, body);
                }
                if (statement is DdfIrDoWhile postLoop)
                {
                    RoslynStatementSyntax body = GenerateEmbedded(postLoop.Body);
                    RoslynExpressionSyntax condition = GenerateExpression(postLoop.Condition);
                    return condition == null || body == null ? null :
                        SyntaxFactory.DoStatement(body, condition);
                }
                if (statement is DdfIrFor counted)
                {
                    RoslynStatementSyntax initializer = counted.Initializer == null
                        ? null : GenerateStatement(counted.Initializer);
                    RoslynExpressionSyntax condition = counted.Condition == null
                        ? null : GenerateExpression(counted.Condition);
                    RoslynExpressionSyntax increment = counted.Increment == null
                        ? null : GenerateExpression(counted.Increment);
                    RoslynStatementSyntax body = GenerateEmbedded(counted.Body);
                    if ((counted.Initializer != null && initializer == null) ||
                        (counted.Condition != null && condition == null) ||
                        (counted.Increment != null && increment == null) || body == null)
                        return null;
                    RoslynForStatementSyntax generated = SyntaxFactory.ForStatement(body)
                        .WithCondition(condition);
                    if (initializer is LocalDeclarationStatementSyntax declaration)
                    {
                        if (counted.Initializer is not DdfIrVariableDeclaration forVariable)
                        {
                            Unsupported(counted.Initializer,
                                "Inizializzatore locale for privo di simbolo DDF.");
                            return null;
                        }
                        RoslynStatementSyntax registration = RegisterLocal(forVariable);
                        if (registration == null) return null;
                        if (increment != null)
                            generated = generated.WithIncrementors(
                                SyntaxFactory.SingletonSeparatedList(increment));
                        return DebugScope(new RoslynStatementSyntax[]
                        {
                            declaration,
                            registration,
                            generated
                        });
                    }
                    else if (initializer is RoslynExpressionStatementSyntax expressionInitializer)
                        generated = generated.WithInitializers(
                            SyntaxFactory.SingletonSeparatedList(
                                expressionInitializer.Expression));
                    else if (initializer != null)
                    {
                        Unsupported(counted.Initializer,
                            "Inizializzatore for non rappresentabile in modo sicuro.");
                        return null;
                    }
                    if (increment != null)
                        generated = generated.WithIncrementors(
                            SyntaxFactory.SingletonSeparatedList(increment));
                    return generated;
                }
                Unsupported(statement, "Statement non supportato dal primo slice Roslyn.");
                return null;
            }

            private RoslynStatementSyntax GenerateEmbedded(DdfIrStatement statement)
            {
                if (statement is DdfIrBlock block) return GenerateBlock(block);
                RoslynStatementSyntax generated = GenerateStatement(statement);
                if (generated == null) return null;
                var statements = new List<RoslynStatementSyntax>
                {
                    Tick(statement),
                    generated
                };
                if (statement is DdfIrVariableDeclaration variable)
                {
                    RoslynStatementSyntax registration = RegisterLocal(variable);
                    if (registration != null) statements.Add(registration);
                }
                return DebugScope(statements);
            }

            private RoslynExpressionSyntax GenerateExpression(DdfIrExpression expression)
            {
                if (expression == null) return null;
                DdfBoundExpression bound = Take(expression);
                if (bound == null) return null;
                RoslynExpressionSyntax result = null;
                if (expression is DdfIrLiteral literal) result = GenerateLiteral(literal);
                else if (expression is DdfIrName)
                {
                    if (string.IsNullOrEmpty(bound.ReferencedSymbolId))
                    {
                        Unsupported(expression, "Nome privo di simbolo bound.");
                        return null;
                    }
                    DdfBoundSymbol referenced = workItem.BoundUnit.Symbols
                        .FirstOrDefault(item => item.Id ==
                            bound.ReferencedSymbolId);
                    result = referenced?.Kind == DdfBoundSymbolKind.HostObject &&
                        referenced.ModuleIdentity == "host"
                        ? GenerateHostCapabilityValue(
                            DdfDotNetHostCapabilities.ForObjectRead(
                                referenced.Name), Array.Empty<RoslynExpressionSyntax>(),
                            bound.Type, expression)
                        : SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(bound.ReferencedSymbolId));
                }
                else if (expression is DdfIrGroup group)
                {
                    RoslynExpressionSyntax inner = GenerateExpression(group.Expression);
                    result = inner == null ? null : SyntaxFactory.ParenthesizedExpression(inner);
                }
                else if (expression is DdfIrUnary unary)
                    result = GenerateUnary(unary);
                else if (expression is DdfIrBinary binary)
                    result = GenerateBinary(binary);
                else if (expression is DdfIrCall call)
                    result = GenerateCall(call, bound);
                else if (expression is DdfIrPostfix postfix)
                    result = GeneratePostfix(postfix);
                else if (expression is DdfIrIndex index)
                    result = GenerateIndex(index, bound);
                else if (expression is DdfIrMemberAccess member)
                    result = GenerateMemberAccess(member, bound);
                else Unsupported(expression, "Espressione non supportata dal primo slice Roslyn.");
                if (result == null) return null;
                if (bound.ContextualConversion.Kind == DdfBoundConversionKind.Implicit &&
                    !bound.ContextualConversion.SourceType.Equals(
                        bound.ContextualConversion.TargetType))
                {
                    TypeSyntax target = GenerateType(
                        bound.ContextualConversion.TargetType, expression);
                    if (target == null) return null;
                    result = SyntaxFactory.CastExpression(target,
                        SyntaxFactory.ParenthesizedExpression(result));
                }
                return result;
            }

            private RoslynExpressionSyntax GenerateUnary(DdfIrUnary unary)
            {
                RoslynExpressionSyntax operand = GenerateExpression(unary.Operand);
                if (operand == null) return null;
                if (unary.OperationId == DdfVmExpressionOperations.LogicalNot)
                    return SyntaxFactory.PrefixUnaryExpression(
                        CSharpSyntaxKind.LogicalNotExpression, operand);
                if (unary.OperationId == DdfVmExpressionOperations.Positive)
                    return SyntaxFactory.PrefixUnaryExpression(
                        CSharpSyntaxKind.UnaryPlusExpression, operand);
                if (unary.OperationId == DdfVmExpressionOperations.Negate)
                    return RuntimeNumeric("Negate", new[] { operand }, unary);
                Unsupported(unary, "Operatore unario non supportato dal backend Roslyn.");
                return null;
            }

            private RoslynExpressionSyntax GenerateBinary(DdfIrBinary binary)
            {
                if (binary.OperationId == DdfVmExpressionOperations.Assign &&
                    TryGenerateHostAssignment(binary,
                        out RoslynExpressionSyntax hostAssignment))
                    return hostAssignment;
                RoslynExpressionSyntax left = GenerateExpression(binary.Left);
                if (left == null) return null;
                if (binary.OperationId == DdfVmExpressionOperations.Output)
                {
                    DdfBoundExpression destination = Take(binary.Right);
                    DdfBoundSymbol destinationSymbol = destination == null ? null :
                        workItem.BoundUnit.Symbols.FirstOrDefault(item =>
                            item.Id == destination.ReferencedSymbolId);
                    if (destinationSymbol?.Kind != DdfBoundSymbolKind.HostObject ||
                        destinationSymbol.ModuleIdentity != "runtime")
                    {
                        Unsupported(binary.Right,
                            "Destinazione Output non riconosciuta dalla IR bound.");
                        return null;
                    }
                    return SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName("WriteValue")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(left));
                }
                RoslynExpressionSyntax right = GenerateExpression(binary.Right);
                if (right == null) return null;
                if (binary.OperationId == DdfVmExpressionOperations.Assign)
                    return SyntaxFactory.AssignmentExpression(
                        CSharpSyntaxKind.SimpleAssignmentExpression, left, right);
                string helper = binary.OperationId switch
                {
                    DdfVmExpressionOperations.Add => "Add",
                    DdfVmExpressionOperations.Subtract => "Subtract",
                    DdfVmExpressionOperations.Multiply => "Multiply",
                    DdfVmExpressionOperations.Divide => "Divide",
                    DdfVmExpressionOperations.Power => "Power",
                    DdfVmExpressionOperations.Root => "Root",
                    _ => null
                };
                if (helper != null) return RuntimeNumeric(helper,
                    new[] { left, right }, binary);
                CSharpSyntaxKind kind = binary.OperationId switch
                {
                    DdfVmExpressionOperations.LogicalOr => CSharpSyntaxKind.LogicalOrExpression,
                    DdfVmExpressionOperations.LogicalAnd => CSharpSyntaxKind.LogicalAndExpression,
                    DdfVmExpressionOperations.LogicalXor => CSharpSyntaxKind.ExclusiveOrExpression,
                    DdfVmExpressionOperations.Equal => CSharpSyntaxKind.EqualsExpression,
                    DdfVmExpressionOperations.NotEqual => CSharpSyntaxKind.NotEqualsExpression,
                    DdfVmExpressionOperations.Less => CSharpSyntaxKind.LessThanExpression,
                    DdfVmExpressionOperations.LessOrEqual => CSharpSyntaxKind.LessThanOrEqualExpression,
                    DdfVmExpressionOperations.Greater => CSharpSyntaxKind.GreaterThanExpression,
                    DdfVmExpressionOperations.GreaterOrEqual => CSharpSyntaxKind.GreaterThanOrEqualExpression,
                    _ => CSharpSyntaxKind.None
                };
                if (kind != CSharpSyntaxKind.None)
                    return SyntaxFactory.BinaryExpression(kind, left, right);
                Unsupported(binary, "Operatore binario non supportato dal primo slice Roslyn.");
                return null;
            }

            private RoslynExpressionSyntax GenerateCall(DdfIrCall call, DdfBoundExpression bound)
            {
                if (string.IsNullOrEmpty(bound.CallTargetSymbolId))
                {
                    Unsupported(call, "Chiamata priva di destinazione bound.");
                    return null;
                }
                DdfBoundSymbol target = workItem.BoundUnit.Symbols.FirstOrDefault(item =>
                    item.Id == bound.CallTargetSymbolId);
                if (target == null || !target.IsCallable)
                {
                    Unsupported(call, "Destinazione della chiamata non supportata.");
                    return null;
                }
                Take(call.Target);
                var values = new List<RoslynExpressionSyntax>();
                foreach (DdfIrExpression argument in call.Arguments)
                {
                    RoslynExpressionSyntax generated = GenerateExpression(argument);
                    if (generated == null) return null;
                    values.Add(generated);
                }
                if (target.Kind == DdfBoundSymbolKind.StandardFunction)
                    return GenerateStandardCall(target, values, call);
                if (target.Kind == DdfBoundSymbolKind.HostFunction)
                    return GenerateHostCall(target, values, call);
                if (target.Kind != DdfBoundSymbolKind.Function)
                {
                    Unsupported(call,
                        "Le funzioni host non appartengono al backend standalone.");
                    return null;
                }
                RoslynExpressionSyntax callable;
                if (target.ModuleIdentity == workItem.Identity)
                    callable = SyntaxFactory.IdentifierName(
                        workItem.GeneratedNames.GetIdentifier(target.Id));
                else
                {
                    if (linkNames == null)
                    {
                        Unsupported(call,
                            "Tabella link richiesta per una chiamata inter-modulo.");
                        return null;
                    }
                    DdfDotNetLinkedSymbolName linked;
                    try { linked = linkNames.Resolve(target); }
                    catch (KeyNotFoundException exception)
                    {
                        Unsupported(call, exception.Message);
                        return null;
                    }
                    callable = DdfDotNetNameMangler.CreateQualifiedExpression(
                        linked.NamespaceName.Split('.')
                            .Concat(new[] { linked.ContainerTypeName,
                                linked.Identifier }).ToArray());
                }
                var arguments = new List<ArgumentSyntax>
                {
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("DdfRuntime_Context"))
                };
                arguments.AddRange(values.Select(SyntaxFactory.Argument));
                InvocationExpressionSyntax invocation =
                    SyntaxFactory.InvocationExpression(callable)
                    .WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(arguments)));
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.IdentifierName("AtCall")))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(Location(call)),
                        SyntaxFactory.Argument(
                            SyntaxFactory.ParenthesizedLambdaExpression(
                                invocation)));
            }

            private RoslynExpressionSyntax GenerateHostCall(
                DdfBoundSymbol target,
                IReadOnlyList<RoslynExpressionSyntax> arguments,
                DdfIrCall call)
            {
                ArrayCreationExpressionSyntax values = ObjectArray(arguments);
                InvocationExpressionSyntax invocation =
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName("InvokeCapability")))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(
                                        DdfDotNetHostCapabilities.ForFunction(
                                            target.Name)))),
                            SyntaxFactory.Argument(values),
                            SyntaxFactory.Argument(Location(call)));
                if (target.HasSideEffects)
                    invocation = invocation.AddArgumentListArguments(
                        SyntaxFactory.Argument(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntimeCapabilityEffectKind"),
                                SyntaxFactory.IdentifierName("Function"))));
                if (target.Type.Name == "void" && target.Type.ArrayRank == 0)
                    return invocation;
                TypeSyntax returnType = GenerateType(target.Type, call);
                return returnType == null ? null : invocation.WithExpression(
                    SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                        SyntaxFactory.GenericName("InvokeCapabilityValue")
                            .AddTypeArgumentListArguments(returnType)));
            }

            private bool TryGenerateHostAssignment(DdfIrBinary binary,
                out RoslynExpressionSyntax result)
            {
                result = null;
                if (!TryResolveHostAccess(binary.Left, null,
                        out HostAccess access)) return false;
                if (!access.HostObject.IsWritable ||
                    access.Members.Any(member => !member.IsWritable))
                {
                    Unsupported(binary.Left,
                        "L'accesso host è esposto in sola lettura.");
                    return true;
                }
                ConsumeHostAccess(binary.Left);
                RoslynExpressionSyntax offset = access.Index == null
                    ? null : GenerateExpression(access.Index.Index);
                if (access.Index != null && offset == null) return true;
                RoslynExpressionSyntax value = GenerateExpression(binary.Right);
                if (value == null) return true;
                string[] path = access.Members.Select(member => member.Name)
                    .ToArray();
                if (access.Index != null)
                {
                    result = GenerateHostElementWrite(
                        DdfDotNetHostCapabilities.ForElementWrite(
                            access.HostObject.Name, path), offset, value,
                        access.Type, access.Index.Index);
                }
                else
                {
                    string capability = path.Length == 0
                        ? DdfDotNetHostCapabilities.ForObjectWrite(
                            access.HostObject.Name)
                        : DdfDotNetHostCapabilities.ForMemberWrite(
                            access.HostObject.Name, path);
                    result = GenerateHostCapabilityWrite(capability, value,
                        access.Type, binary.Left);
                }
                return true;
            }

            private RoslynExpressionSyntax GenerateHostCapabilityValue(
                string capability, IReadOnlyList<RoslynExpressionSyntax> arguments,
                DdfBoundType type, DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.GenericName("InvokeCapabilityValue")
                                    .AddTypeArgumentListArguments(returnType)))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(capability))),
                            SyntaxFactory.Argument(ObjectArray(arguments)),
                            SyntaxFactory.Argument(Location(location)));
            }

            private RoslynExpressionSyntax GenerateHostCapabilityWrite(
                string capability, RoslynExpressionSyntax value,
                DdfBoundType type, DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("WriteCapabilityValue")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(value),
                        SyntaxFactory.Argument(Location(location)));
            }

            private RoslynExpressionSyntax GenerateHostElementRead(
                string capability, RoslynExpressionSyntax offset,
                DdfBoundType type, DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("ReadCapabilityElement")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(Location(location)));
            }

            private RoslynExpressionSyntax GenerateHostElementWrite(
                string capability, RoslynExpressionSyntax offset,
                RoslynExpressionSyntax value, DdfBoundType type,
                DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("WriteCapabilityElement")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(value),
                        SyntaxFactory.Argument(Location(location)));
            }

            private static ArrayCreationExpressionSyntax ObjectArray(
                IEnumerable<RoslynExpressionSyntax> arguments) =>
                SyntaxFactory.ArrayCreationExpression(
                        SyntaxFactory.ArrayType(
                                SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(
                                        CSharpSyntaxKind.ObjectKeyword)))
                            .AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier(
                                SyntaxFactory.SingletonSeparatedList<
                                    RoslynExpressionSyntax>(
                                    SyntaxFactory.OmittedArraySizeExpression()))))
                    .WithInitializer(SyntaxFactory.InitializerExpression(
                        CSharpSyntaxKind.ArrayInitializerExpression,
                        SyntaxFactory.SeparatedList(arguments)));

            private sealed class HostAccess
            {
                internal DdfBoundSymbol HostObject { get; set; }
                internal List<DdfBoundSymbol> Members { get; } = new();
                internal DdfIrIndex Index { get; set; }
                internal DdfBoundType Type { get; set; }
            }

            private bool TryResolveHostAccess(DdfIrExpression expression,
                DdfBoundExpression knownBound, out HostAccess access)
            {
                access = null;
                if (expression is DdfIrName)
                {
                    DdfBoundExpression bound = knownBound ?? Peek(expression);
                    DdfBoundSymbol hostObject = bound == null ? null :
                        workItem.BoundUnit.Symbols
                        .FirstOrDefault(item => item.Id ==
                            bound.ReferencedSymbolId);
                    if (hostObject?.Kind != DdfBoundSymbolKind.HostObject ||
                        hostObject.ModuleIdentity != "host") return false;
                    access = new HostAccess
                    {
                        HostObject = hostObject,
                        Type = bound.Type
                    };
                    return true;
                }
                if (expression is DdfIrMemberAccess memberAccess)
                {
                    if (!TryResolveHostAccess(memberAccess.Target, null,
                            out access) || access.Index != null) return false;
                    DdfBoundExpression memberBound = knownBound ??
                        Peek(memberAccess);
                    DdfBoundSymbol member = memberBound == null ? null :
                        workItem.BoundUnit.Symbols.FirstOrDefault(item => item.Id ==
                            memberBound.ReferencedSymbolId);
                    if (member?.Kind != DdfBoundSymbolKind.Field ||
                        member.ModuleIdentity != "host") return false;
                    access.Members.Add(member);
                    access.Type = memberBound.Type;
                    return true;
                }
                if (expression is DdfIrIndex index)
                {
                    if (!TryResolveHostAccess(index.Target, null, out access) ||
                        access.Index != null) return false;
                    DdfBoundExpression indexBound = knownBound ?? Peek(index);
                    if (indexBound == null) return false;
                    access.Index = index;
                    access.Type = indexBound.Type;
                    return true;
                }
                return false;
            }

            private void ConsumeHostAccess(DdfIrExpression expression)
            {
                Take(expression);
                if (expression is DdfIrMemberAccess member)
                    ConsumeHostAccess(member.Target);
                else if (expression is DdfIrIndex index)
                    ConsumeHostAccess(index.Target);
            }

            private RoslynExpressionSyntax GenerateStandardCall(
                DdfBoundSymbol target, IReadOnlyList<RoslynExpressionSyntax> arguments,
                DdfIrCall call)
            {
                RoslynExpressionSyntax Member(string owner, string method) =>
                    SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(owner),
                        SyntaxFactory.IdentifierName(method));
                InvocationExpressionSyntax Invoke(RoslynExpressionSyntax targetExpression,
                    IEnumerable<RoslynExpressionSyntax> values) =>
                    SyntaxFactory.InvocationExpression(targetExpression)
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(values.Select(
                                SyntaxFactory.Argument))));

                string binding = target.SemanticBindingId;
                if (binding == DdfHostFunctionBindings.Output && arguments.Count == 1)
                    return Invoke(Member("DdfRuntime_Context", "Write"), arguments);
                if (binding == DdfHostFunctionBindings.Input && arguments.Count == 0)
                    return Invoke(Member("DdfRuntime_Context", "ReadLine"),
                        new[] { Location(call) });
                if (binding == DdfHostFunctionBindings.StringLength &&
                    arguments.Count == 1)
                    return Invoke(Member("DdfRuntimeValues", "StringLength"),
                        arguments.Concat(new[] { Location(call) }));
                if (binding == DdfHostFunctionBindings.ToInt && arguments.Count == 1)
                    return Invoke(Member("DdfRuntimeNumeric", "ToInt"),
                        arguments.Concat(new[] { Location(call) }));
                if (binding == DdfHostFunctionBindings.ToFloat && arguments.Count == 1)
                    return Invoke(Member("DdfRuntimeNumeric", "ToFloat"),
                        arguments.Concat(new[] { Location(call) }));
                Unsupported(call, "Binding standard non supportato: " + binding + ".");
                return null;
            }

            private RoslynExpressionSyntax GeneratePostfix(DdfIrPostfix postfix)
            {
                RoslynExpressionSyntax operand = GenerateExpression(postfix.Operand);
                if (operand == null) return null;
                string method = postfix.OperationId switch
                {
                    DdfVmExpressionOperations.Increment => "PostIncrement",
                    DdfVmExpressionOperations.Decrement => "PostDecrement",
                    _ => null
                };
                if (method == null)
                {
                    Unsupported(postfix,
                        "Operatore postfix non supportato dal backend Roslyn.");
                    return null;
                }
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntimeNumeric"),
                            SyntaxFactory.IdentifierName(method)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(operand).WithRefKindKeyword(
                            SyntaxFactory.Token(CSharpSyntaxKind.RefKeyword)),
                        SyntaxFactory.Argument(Location(postfix)));
            }

            private RoslynExpressionSyntax GenerateIndex(DdfIrIndex index,
                DdfBoundExpression bound)
            {
                if (TryResolveHostAccess(index, bound, out HostAccess access))
                {
                    ConsumeHostAccess(index.Target);
                    RoslynExpressionSyntax hostOffset =
                        GenerateExpression(index.Index);
                    if (hostOffset == null) return null;
                    return GenerateHostElementRead(
                        DdfDotNetHostCapabilities.ForElementRead(
                            access.HostObject.Name, access.Members.Select(
                                member => member.Name).ToArray()),
                        hostOffset, bound.Type, index.Index);
                }
                DdfBoundExpression targetBound = Peek(index.Target);
                RoslynExpressionSyntax target = GenerateExpression(index.Target);
                RoslynExpressionSyntax offset = GenerateExpression(index.Index);
                if (targetBound == null || target == null || offset == null) return null;
                string method;
                if (targetBound.Type.ArrayRank > 0)
                    method = "ArrayElement";
                else if (targetBound.Type.ArrayRank == 0 &&
                         targetBound.Type.Name == "string")
                    method = "StringCharacterAt";
                else
                {
                    Unsupported(index,
                        "Indicizzazione supportata soltanto per array e stringhe.");
                    return null;
                }
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntimeValues"),
                            SyntaxFactory.IdentifierName(method)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(target),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(Location(index.Index)));
            }

            private RoslynExpressionSyntax GenerateMemberAccess(
                DdfIrMemberAccess member, DdfBoundExpression bound)
            {
                if (TryResolveHostAccess(member, bound, out HostAccess access))
                {
                    ConsumeHostAccess(member.Target);
                    return GenerateHostCapabilityValue(
                        DdfDotNetHostCapabilities.ForMemberRead(
                            access.HostObject.Name, access.Members.Select(
                                item => item.Name).ToArray()),
                        Array.Empty<RoslynExpressionSyntax>(), bound.Type, member);
                }
                RoslynExpressionSyntax target = GenerateExpression(member.Target);
                if (target == null || string.IsNullOrEmpty(bound.ReferencedSymbolId))
                {
                    Unsupported(member, "Membro privo di simbolo field bound.");
                    return null;
                }
                DdfBoundSymbol field = workItem.BoundUnit.Symbols.FirstOrDefault(
                    item => item.Id == bound.ReferencedSymbolId &&
                        item.Kind == DdfBoundSymbolKind.Field);
                if (field == null || field.ModuleIdentity != workItem.Identity)
                {
                    Unsupported(member,
                        "Lo slice corrente supporta membri di strutture locali al modulo.");
                    return null;
                }
                return SyntaxFactory.MemberAccessExpression(
                    CSharpSyntaxKind.SimpleMemberAccessExpression, target,
                    SyntaxFactory.IdentifierName(workItem.GeneratedNames
                        .GetIdentifier(field.Id)));
            }

            private RoslynExpressionSyntax GenerateLiteral(DdfIrLiteral literal)
            {
                try
                {
                    if (literal.Kind == DdfLiteralKind.Boolean)
                        return SyntaxFactory.LiteralExpression(literal.BooleanValue == true
                            ? CSharpSyntaxKind.TrueLiteralExpression
                            : CSharpSyntaxKind.FalseLiteralExpression);
                    if (literal.Kind == DdfLiteralKind.String)
                    {
                        string value = Unescape(literal.Text.Substring(1,
                            Math.Max(0, literal.Text.Length - 2)));
                        return SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(value));
                    }
                    if (literal.Text.IndexOf('.') >= 0)
                    {
                        double value = double.Parse(literal.Text,
                            CultureInfo.InvariantCulture);
                        return SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(value));
                    }
                    int integer = int.Parse(literal.Text, CultureInfo.InvariantCulture);
                    return SyntaxFactory.LiteralExpression(
                        CSharpSyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal(integer));
                }
                catch (Exception exception) when (exception is FormatException ||
                                                   exception is OverflowException ||
                                                   exception is ArgumentOutOfRangeException)
                {
                    Unsupported(literal, "Letterale DDF non rappresentabile in modo sicuro.");
                    return null;
                }
            }

            private InvocationExpressionSyntax RuntimeNumeric(string method,
                IEnumerable<RoslynExpressionSyntax> arguments, DdfIrNode node)
            {
                var values = arguments.Select(SyntaxFactory.Argument).ToList();
                values.Add(SyntaxFactory.Argument(Location(node)));
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntimeNumeric"),
                            SyntaxFactory.IdentifierName(method)))
                    .WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(values)));
            }

            private RoslynStatementSyntax Tick(DdfIrNode node)
            {
                RoslynStatementSyntax statement =
                    SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.IdentifierName("Tick")))
                    .AddArgumentListArguments(SyntaxFactory.Argument(Location(node))))
                    .WithAdditionalAnnotations(new SyntaxAnnotation(
                        SourceSpanAnnotationKind,
                        node.Start.ToString(CultureInfo.InvariantCulture) + ":" +
                        Math.Max(1, node.Length).ToString(
                            CultureInfo.InvariantCulture)));
                int position = Math.Max(0, Math.Min(node.Start, sourceText.Length));
                int line = sourceText.Lines.GetLineFromPosition(position)
                    .LineNumber + 1;
                return statement.WithLeadingTrivia(
                        SyntaxFactory.Trivia(SyntaxFactory.LineDirectiveTrivia(
                            SyntaxFactory.Literal(line),
                            SyntaxFactory.Literal(relativePath),
                            isActive: true)),
                        SyntaxFactory.ElasticCarriageReturnLineFeed)
                    .WithTrailingTrivia(
                        SyntaxFactory.ElasticCarriageReturnLineFeed,
                        SyntaxFactory.Trivia(SyntaxFactory.LineDirectiveTrivia(
                            SyntaxFactory.Token(CSharpSyntaxKind.HiddenKeyword),
                            isActive: true)),
                        SyntaxFactory.ElasticCarriageReturnLineFeed);
            }

            private static RoslynStatementSyntax RuntimeDebugCall(string method,
                string symbolId, RoslynExpressionSyntax location = null)
            {
                var arguments = new List<ArgumentSyntax>
                {
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        CSharpSyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(symbolId)))
                };
                if (location != null)
                    arguments.Add(SyntaxFactory.Argument(location));
                return SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName(method)))
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(arguments))));
            }

            private RoslynStatementSyntax RegisterLocal(
                DdfIrVariableDeclaration variable)
            {
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Local,
                    variable.Start, variable.Name);
                return symbol == null ? null : RegisterDebugValue(
                    "RegisterDebugLocal", symbol,
                    Type(variable.Type).DisplayName,
                    SyntaxFactory.IdentifierName(workItem.GeneratedNames
                        .GetIdentifier(symbol.Id)));
            }

            private static RoslynStatementSyntax RegisterDebugValue(string method,
                DdfBoundSymbol symbol, string typeName,
                RoslynExpressionSyntax value)
            {
                ParenthesizedLambdaExpressionSyntax reader =
                    SyntaxFactory.ParenthesizedLambdaExpression(value);
                return SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName(method)))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(symbol.Id))),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(typeName))),
                            SyntaxFactory.Argument(reader)));
            }

            private static BlockSyntax DebugScope(
                IEnumerable<RoslynStatementSyntax> statements) =>
                SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName("EnterDebugScope")))),
                    SyntaxFactory.TryStatement(
                        SyntaxFactory.Block(statements), default,
                        SyntaxFactory.FinallyClause(SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(
                                            "DdfRuntime_Context"),
                                        SyntaxFactory.IdentifierName(
                                            "ExitDebugScope"))))))));

            private ObjectCreationExpressionSyntax Location(DdfIrNode node) =>
                SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.IdentifierName("DdfRuntimeSourceLocation"))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(relativePath))),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(node.Start))),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(node.Length))));

            private TypeSyntax GenerateType(DdfBoundType type, DdfIrNode node)
            {
                if (type == null || type.IsError || type.ArrayRank > 1)
                {
                    Unsupported(node, "Tipo non supportato dallo slice Roslyn corrente: " +
                        (type?.DisplayName ?? "?") + ".");
                    return null;
                }
                TypeSyntax element;
                if (mapping.TryGetPrimitive(type.Name,
                        out DdfClrPrimitiveTypeMapping primitive))
                {
                    element = primitive.CSharpTypeName switch
                    {
                        "int" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.IntKeyword)),
                        "double" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.DoubleKeyword)),
                        "char" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.CharKeyword)),
                        "bool" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.BoolKeyword)),
                        "void" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.VoidKeyword)),
                        "string" => SyntaxFactory.PredefinedType(SyntaxFactory.Token(CSharpSyntaxKind.StringKeyword)),
                        _ => null
                    };
                }
                else
                {
                    DdfBoundSymbol structure = workItem.BoundUnit.Symbols
                        .FirstOrDefault(item =>
                            item.Kind == DdfBoundSymbolKind.Structure &&
                            item.ModuleIdentity == workItem.Identity &&
                            item.Name == type.Name);
                    element = structure == null ? null :
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(structure.Id));
                }
                if (element == null)
                {
                    Unsupported(node, "Mapping CLR o struttura locale non supportati: " +
                        type.Name + ".");
                    return null;
                }
                if (type.ArrayRank == 0) return element;
                return SyntaxFactory.ArrayType(element)
                    .AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier(
                        SyntaxFactory.SingletonSeparatedList<RoslynExpressionSyntax>(
                            SyntaxFactory.OmittedArraySizeExpression())));
            }

            private RoslynExpressionSyntax DefaultInitializer(
                DdfIrVariableDeclaration variable)
            {
                if (variable.Type.ArrayLengths.Count > 1)
                {
                    Unsupported(variable,
                        "Lo slice corrente supporta array monodimensionali.");
                    return null;
                }
                if (variable.Type.ArrayLengths.Count == 1)
                {
                    RoslynExpressionSyntax length = GenerateExpression(
                        variable.Type.ArrayLengths[0]);
                    TypeSyntax elementType = GenerateType(
                        new DdfBoundType(variable.Type.Name), variable.Type);
                    if (length == null || elementType == null) return null;
                    GenericNameSyntax method = SyntaxFactory.GenericName(
                            SyntaxFactory.Identifier("CreateArray"))
                        .AddTypeArgumentListArguments(elementType);
                    return SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntimeValues"), method))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(length),
                            SyntaxFactory.Argument(Location(variable.Type)));
                }
                if (variable.Type.Name == "string")
                    return SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.PredefinedType(
                            SyntaxFactory.Token(CSharpSyntaxKind.StringKeyword)),
                        SyntaxFactory.IdentifierName("Empty"));
                DdfBoundSymbol structure = workItem.BoundUnit.Symbols
                    .FirstOrDefault(item => item.Kind == DdfBoundSymbolKind.Structure &&
                        item.ModuleIdentity == workItem.Identity &&
                        item.Name == variable.Type.Name);
                if (structure != null)
                    return SyntaxFactory.ObjectCreationExpression(
                            SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                .GetIdentifier(structure.Id)))
                        .AddArgumentListArguments(SyntaxFactory.Argument(
                            SyntaxFactory.IdentifierName("DdfRuntime_Context")));
                return null;
            }

            private DdfBoundExpression Take(DdfIrExpression expression)
            {
                string key = Key(expression.Start, expression.Length);
                if (!expressions.TryGetValue(key, out Queue<DdfBoundExpression> queue) ||
                    queue.Count == 0)
                {
                    Unsupported(expression, "Annotazione bound mancante.");
                    return null;
                }
                return queue.Dequeue();
            }

            private DdfBoundExpression Peek(DdfIrExpression expression)
            {
                string key = Key(expression.Start, expression.Length);
                if (expressions.TryGetValue(key,
                        out Queue<DdfBoundExpression> queue) && queue.Count != 0)
                    return queue.Peek();
                Unsupported(expression, "Annotazione bound mancante.");
                return null;
            }

            private DdfBoundSymbol FindDeclared(DdfBoundSymbolKind kind, int start,
                string name)
            {
                DdfBoundSymbol symbol = workItem.BoundUnit.Symbols.FirstOrDefault(item =>
                    item.Kind == kind && item.RelativePath == relativePath &&
                    item.Start == start && item.Name == name);
                if (symbol == null)
                    issues.Add(new DdfDotNetContractIssue("DDFC401",
                        "Simbolo bound dichiarato non trovato in " + relativePath +
                        " alla posizione " + start + "."));
                return symbol;
            }

            private void Unsupported(DdfIrNode node, string message) =>
                issues.Add(new DdfDotNetContractIssue("DDFC400",
                    relativePath + "@" + (node?.Start ?? 0) + ": " + message));

            private static DdfBoundType Type(DdfIrType type) => type == null
                ? DdfBoundType.Error
                : new DdfBoundType(type.Name, type.ArrayLengths.Count);
            private static string Key(int start, int length) => start + ":" + length;
            private static string Unescape(string text) => (text ?? string.Empty)
                .Replace("\\\"", "\"").Replace("\\n", "\n")
                .Replace("\\r", "\r").Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }

        public static DdfDotNetSyntaxGenerationResult Generate(
            DdfDotNetCompilationWorkItem workItem,
            DdfClrTypeMappingDefinition mapping,
            DdfDotNetLinkNameTable linkNames = null)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            var issues = new List<DdfDotNetContractIssue>();
            var trees = new List<SyntaxTree>();
            IReadOnlyList<DdfBoundIrDocument> documents = workItem.BoundUnit.Documents;
            IReadOnlyList<string> initializerNames = documents.Select(document =>
                    workItem.GeneratedNames.GetDocumentInitializerName(
                        document.RelativePath))
                .ToList().AsReadOnly();
            for (int documentIndex = 0; documentIndex < documents.Count;
                 documentIndex++)
            {
                DdfBoundIrDocument document = documents[documentIndex];
                var generator = new Generator(workItem, mapping, document, issues,
                    linkNames, initializerNames[documentIndex], documentIndex == 0,
                    initializerNames);
                RoslynCompilationUnitSyntax root = generator.Generate(document.StructuralProgram);
                if (issues.Count != 0) continue;
                string generatedPath = "generated/" + workItem.AssemblyName + "/" +
                    document.RelativePath.Replace('\\', '/').Replace(".ddf", ".g.cs",
                        StringComparison.OrdinalIgnoreCase);
                SyntaxTree tree = CSharpSyntaxTree.Create(root,
                    DdfDotNetCompilerProfile.ParseOptions, generatedPath,
                    Encoding.UTF8);
                IEnumerable<Diagnostic> errors = tree.GetDiagnostics().Where(item =>
                    item.Severity == DiagnosticSeverity.Error);
                foreach (Diagnostic error in errors)
                    issues.Add(new DdfDotNetContractIssue("DDFC402",
                        "Albero C# generato non valido: " + error.Id + " " +
                        error.GetMessage(CultureInfo.InvariantCulture)));
                if (issues.Count == 0) trees.Add(tree);
            }
            return new DdfDotNetSyntaxGenerationResult(
                issues.Count == 0 ? trees : Array.Empty<SyntaxTree>(), issues);
        }
    }
}
