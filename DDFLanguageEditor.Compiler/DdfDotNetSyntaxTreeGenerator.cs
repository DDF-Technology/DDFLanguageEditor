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
using RoslynEnumDeclarationSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax;
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
            private readonly string documentStateCaptureName;
            private readonly string documentStateApplyName;
            private readonly bool includeModuleInitializer;
            private readonly IReadOnlyList<string> documentInitializerNames;
            private readonly IReadOnlyList<string> documentStateCaptureNames;
            private readonly IReadOnlyList<string> documentStateApplyNames;
            private readonly SourceText sourceText;

            internal Generator(DdfDotNetCompilationWorkItem workItem,
                DdfClrTypeMappingDefinition mapping, DdfBoundIrDocument document,
                List<DdfDotNetContractIssue> issues,
                DdfDotNetLinkNameTable linkNames, string documentInitializerName,
                string documentStateCaptureName, string documentStateApplyName,
                bool includeModuleInitializer,
                IReadOnlyList<string> documentInitializerNames,
                IReadOnlyList<string> documentStateCaptureNames,
                IReadOnlyList<string> documentStateApplyNames)
            {
                this.workItem = workItem;
                this.mapping = mapping;
                this.issues = issues;
                this.linkNames = linkNames;
                relativePath = document.RelativePath;
                this.documentInitializerName = documentInitializerName;
                this.documentStateCaptureName = documentStateCaptureName;
                this.documentStateApplyName = documentStateApplyName;
                this.includeModuleInitializer = includeModuleInitializer;
                this.documentInitializerNames = documentInitializerNames;
                this.documentStateCaptureNames = documentStateCaptureNames;
                this.documentStateApplyNames = documentStateApplyNames;
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
                    else if (member is DdfIrEnum enumeration)
                    {
                        RoslynEnumDeclarationSyntax generated = GenerateEnum(enumeration);
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
                declarations.Add(GenerateDocumentStateCapture(program));
                declarations.Add(GenerateDocumentStateApply(program));
                if (includeModuleInitializer)
                {
                    declarations.Add(GenerateInitializationState());
                    MethodDeclarationSyntax moduleInitializer =
                        GenerateModuleInitializer();
                    if (moduleInitializer != null)
                        declarations.Add(moduleInitializer);
                    declarations.Add(GenerateModuleStateCapture());
                    declarations.Add(GenerateModuleStateApply());
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
                            .CreateNamespaceSyntax("System.Threading.Tasks")),
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
                DdfBoundType boundReturnType = Type(function.ReturnType);
                TypeSyntax valueReturnType = GenerateType(boundReturnType, function);
                if (valueReturnType == null) return null;
                TypeSyntax returnType = AsyncReturnType(boundReturnType,
                    valueReturnType);
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
                    AwaitCompletedTask(),
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
                CatchClauseSyntax captureRuntimeError = SyntaxFactory.CatchClause()
                    .WithDeclaration(SyntaxFactory.CatchDeclaration(
                            SyntaxFactory.IdentifierName("DdfRuntimeException"))
                        .WithIdentifier(SyntaxFactory.Identifier("exception")))
                    .WithBlock(SyntaxFactory.Block(
                        RuntimeCaptureErrorCall(),
                        SyntaxFactory.ThrowStatement()));
                prologue.Add(SyntaxFactory.TryStatement(body,
                        SyntaxFactory.SingletonList(captureRuntimeError),
                        SyntaxFactory.FinallyClause(SyntaxFactory.Block(
                            RuntimeDebugCall("ExitFunction", symbol.Id)))));
                body = SyntaxFactory.Block(prologue);
                return SyntaxFactory.MethodDeclaration(returnType,
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .GetIdentifier(symbol.Id)))
                    .AddModifiers(SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.AsyncKeyword))
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
                            CSharpSyntaxKind.PrivateKeyword))
                        .WithBody(SyntaxFactory.Block());
                fields.Add(constructor);
                string typeName = workItem.GeneratedNames.GetIdentifier(symbol.Id);
                var factoryStatements = new List<RoslynStatementSyntax>
                {
                    AwaitCompletedTask(),
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.IdentifierName(typeName))
                            .AddVariables(SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier("DdfRuntime_Value"))
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(
                                            SyntaxFactory.IdentifierName(typeName))
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList())))))
                };
                factoryStatements.AddRange(initializers.Select(statement =>
                    statement.ReplaceNodes(statement.DescendantNodesAndSelf()
                            .OfType<ThisExpressionSyntax>(),
                        (_, _) => SyntaxFactory.IdentifierName(
                            "DdfRuntime_Value"))));
                factoryStatements.Add(SyntaxFactory.ReturnStatement(
                    SyntaxFactory.IdentifierName("DdfRuntime_Value")));
                fields.Add(SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.GenericName("Task")
                                .AddTypeArgumentListArguments(
                                    SyntaxFactory.IdentifierName(typeName)),
                            SyntaxFactory.Identifier("CreateAsync"))
                        .AddModifiers(
                            SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword),
                            SyntaxFactory.Token(CSharpSyntaxKind.AsyncKeyword))
                        .AddParameterListParameters(RuntimeContextParameter())
                        .WithBody(SyntaxFactory.Block(factoryStatements)));
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

            private RoslynEnumDeclarationSyntax GenerateEnum(DdfIrEnum enumeration)
            {
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Enumeration,
                    enumeration.NameStart, enumeration.Name);
                if (symbol == null) return null;
                var members = new List<EnumMemberDeclarationSyntax>();
                foreach (DdfIrEnumMember value in enumeration.Members)
                {
                    DdfBoundSymbol valueSymbol = FindDeclared(
                        DdfBoundSymbolKind.EnumerationMember, value.Start, value.Name);
                    if (valueSymbol == null) return null;
                    members.Add(SyntaxFactory.EnumMemberDeclaration(
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .GetIdentifier(valueSymbol.Id))));
                }
                return SyntaxFactory.EnumDeclaration(SyntaxFactory.Identifier(
                        workItem.GeneratedNames.GetIdentifier(symbol.Id)))
                    .AddModifiers(SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword))
                    .AddMembers(members.ToArray());
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
                    RoslynExpressionSyntax initializer = variable.ConstantValue != null
                        ? GenerateConstant(variable.ConstantValue)
                        : variable.Initializer == null
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
                    statements.Add(RegisterDebugValue(variable.IsState
                            ? "RegisterDebugState"
                            : "RegisterDebugGlobal",
                        symbol, Type(variable.Type).DisplayName,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(symbol.Id)), variable.IsFixed,
                        variable.IsFixed));
                }
                statements.Insert(0, AwaitCompletedTask());
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.IdentifierName("Task"),
                        SyntaxFactory.Identifier(documentInitializerName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.AsyncKeyword))
                    .AddParameterListParameters(RuntimeContextParameter())
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private MethodDeclarationSyntax GenerateDocumentStateCapture(
                DdfIrProgram program)
            {
                var statements = new List<RoslynStatementSyntax>();
                foreach (DdfIrGlobal global in program.Members.OfType<DdfIrGlobal>())
                {
                    if (global.Statement is not DdfIrVariableDeclaration variable ||
                        !variable.IsState) continue;
                    DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Global,
                        variable.Start, variable.Name);
                    if (symbol == null) continue;
                    statements.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    CSharpSyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("DdfRuntime_Entries"),
                                    SyntaxFactory.IdentifierName("Add")))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.IdentifierName(
                                            "DdfPersistedStateEntry"))
                                    .AddArgumentListArguments(
                                        StringArgument(StateKey(variable.Name)),
                                        StringArgument(Type(variable.Type).DisplayName),
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName(
                                                workItem.GeneratedNames
                                                    .GetIdentifier(symbol.Id))))))));
                }
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(documentStateCaptureName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(
                            SyntaxFactory.Identifier("DdfRuntime_Entries"))
                        .WithType(SyntaxFactory.GenericName("ICollection")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.IdentifierName(
                                    "DdfPersistedStateEntry"))))
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private MethodDeclarationSyntax GenerateDocumentStateApply(
                DdfIrProgram program)
            {
                var statements = new List<RoslynStatementSyntax>();
                foreach (DdfIrGlobal global in program.Members.OfType<DdfIrGlobal>())
                {
                    if (global.Statement is not DdfIrVariableDeclaration variable ||
                        !variable.IsState) continue;
                    DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Global,
                        variable.Start, variable.Name);
                    TypeSyntax type = GenerateType(Type(variable.Type), variable.Type);
                    if (symbol == null || type == null) continue;
                    string entryName = "DdfRuntime_State_" +
                        workItem.GeneratedNames.GetIdentifier(symbol.Id);
                    InvocationExpressionSyntax lookup = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Entries"),
                                SyntaxFactory.IdentifierName("TryGetValue")))
                        .AddArgumentListArguments(
                            StringArgument(StateKey(variable.Name)),
                            SyntaxFactory.Argument(SyntaxFactory.DeclarationExpression(
                                    SyntaxFactory.IdentifierName(
                                        "DdfPersistedStateEntry"),
                                    SyntaxFactory.SingleVariableDesignation(
                                        SyntaxFactory.Identifier(entryName))))
                                .WithRefOrOutKeyword(SyntaxFactory.Token(
                                    CSharpSyntaxKind.OutKeyword)));
                    RoslynExpressionSyntax value = SyntaxFactory.CastExpression(type,
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    CSharpSyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(entryName),
                                    SyntaxFactory.IdentifierName("RequireValue")))
                            .AddArgumentListArguments(StringArgument(
                                Type(variable.Type).DisplayName)));
                    statements.Add(SyntaxFactory.IfStatement(lookup,
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                CSharpSyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                    .GetIdentifier(symbol.Id)), value))));
                }
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(documentStateApplyName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(
                            SyntaxFactory.Identifier("DdfRuntime_Entries"))
                        .WithType(SyntaxFactory.GenericName("IReadOnlyDictionary")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                                    CSharpSyntaxKind.StringKeyword)),
                                SyntaxFactory.IdentifierName(
                                    "DdfPersistedStateEntry"))))
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private MethodDeclarationSyntax GenerateModuleStateCapture()
            {
                var statements = new List<RoslynStatementSyntax>
                {
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.GenericName("List")
                                    .AddTypeArgumentListArguments(
                                        SyntaxFactory.IdentifierName(
                                            "DdfPersistedStateEntry")))
                            .AddVariables(SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier("DdfRuntime_Entries"))
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(
                                            SyntaxFactory.GenericName("List")
                                                .AddTypeArgumentListArguments(
                                                    SyntaxFactory.IdentifierName(
                                                        "DdfPersistedStateEntry")))
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList())))))
                };
                statements.AddRange(documentStateCaptureNames.Select(name =>
                    (RoslynStatementSyntax)SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName(name))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Entries"))))));
                statements.Add(SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Entries"),
                            SyntaxFactory.IdentifierName("AsReadOnly")))));
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.GenericName("IReadOnlyList")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.IdentifierName(
                                    "DdfPersistedStateEntry")),
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .StateCaptureName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private MethodDeclarationSyntax GenerateModuleStateApply()
            {
                var statements = documentStateApplyNames.Select(name =>
                    (RoslynStatementSyntax)SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName(name))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Entries"))))).ToList();
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                            CSharpSyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .StateApplyName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(
                            SyntaxFactory.Identifier("DdfRuntime_Entries"))
                        .WithType(SyntaxFactory.GenericName("IReadOnlyDictionary")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(
                                    CSharpSyntaxKind.StringKeyword)),
                                SyntaxFactory.IdentifierName(
                                    "DdfPersistedStateEntry"))))
                    .WithBody(SyntaxFactory.Block(statements));
            }

            private static ArgumentSyntax StringArgument(string value) =>
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                    CSharpSyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(value ?? string.Empty)));

            private string StateKey(string name) => workItem.Identity + "::" +
                (relativePath ?? string.Empty) + "::" + (name ?? string.Empty);

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
                        Await(SyntaxFactory.InvocationExpression(target)
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Context"))))));
                }
                foreach (string initializer in documentInitializerNames)
                    statements.Add(SyntaxFactory.ExpressionStatement(
                        Await(SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName(initializer))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(
                                    "DdfRuntime_Context"))))));
                statements.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        CSharpSyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .InitializationStateName),
                        SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.TrueLiteralExpression))));
                statements.Insert(1, AwaitCompletedTask());
                return SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.IdentifierName("Task"),
                        SyntaxFactory.Identifier(workItem.GeneratedNames
                            .ModuleInitializerName))
                    .AddModifiers(
                        SyntaxFactory.Token(CSharpSyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(CSharpSyntaxKind.AsyncKeyword))
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
                    RoslynExpressionSyntax initializer = variable.ConstantValue != null
                        ? GenerateConstant(variable.ConstantValue)
                        : GenerateExpression(variable.Initializer);
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
                if (statement is DdfIrContinue) return SyntaxFactory.ContinueStatement();
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
                if (statement is DdfIrSelect selection)
                    return GenerateSelect(selection);
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

            private RoslynStatementSyntax GenerateSelect(DdfIrSelect selection)
            {
                RoslynExpressionSyntax selected = GenerateExpression(selection.Expression);
                if (selected == null) return null;
                string temporaryName = "DdfRuntime_Select_" +
                    selection.Start.ToString(CultureInfo.InvariantCulture);
                var declaration = SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName("var"))
                        .AddVariables(SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(temporaryName))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(selected))));

                DdfIrSelectClause fallback = selection.Clauses.LastOrDefault(
                    clause => clause.IsOtherwise);
                RoslynStatementSyntax chain = fallback == null
                    ? SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(
                            SyntaxFactory.ParseTypeName("System.InvalidOperationException"))
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(
                                        "Un select DDF esaustivo non ha trovato un ramo.")))))))
                    : GenerateEmbedded(fallback.Body);
                if (chain == null) return null;
                foreach (DdfIrSelectClause clause in selection.Clauses
                             .Where(item => !item.IsOtherwise).Reverse())
                {
                    RoslynExpressionSyntax value = GenerateExpression(clause.Value);
                    RoslynStatementSyntax body = GenerateEmbedded(clause.Body);
                    if (value == null || body == null) return null;
                    RoslynExpressionSyntax condition = SyntaxFactory.BinaryExpression(
                        CSharpSyntaxKind.EqualsExpression,
                        SyntaxFactory.IdentifierName(temporaryName), value);
                    chain = SyntaxFactory.IfStatement(condition, body,
                        chain == null ? null : SyntaxFactory.ElseClause(chain));
                }
                return SyntaxFactory.Block(declaration, chain);
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
                    if (bound.ContextualConversion.SourceType.ArrayRank == 0 &&
                        bound.ContextualConversion.SourceType.Name == "string" &&
                        bound.ContextualConversion.TargetType.ArrayRank == 0 &&
                        bound.ContextualConversion.TargetType.Name == "char")
                    {
                        result = SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    CSharpSyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("DdfRuntimeValues"),
                                    SyntaxFactory.IdentifierName(
                                        "StringCharacterAt")))
                            .AddArgumentListArguments(
                                SyntaxFactory.Argument(result),
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        CSharpSyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(0))),
                                SyntaxFactory.Argument(Location(expression)));
                    }
                    else
                    {
                        TypeSyntax target = GenerateType(
                            bound.ContextualConversion.TargetType, expression);
                        if (target == null) return null;
                        result = SyntaxFactory.CastExpression(target,
                            SyntaxFactory.ParenthesizedExpression(result));
                    }
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
                if (binary.OperationId == DdfVmExpressionOperations.Assign &&
                    TryGenerateArrayElementAssignment(binary,
                        out RoslynExpressionSyntax elementAssignment))
                    return elementAssignment;
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

            private bool TryGenerateArrayElementAssignment(DdfIrBinary binary,
                out RoslynExpressionSyntax result)
            {
                result = null;
                if (binary.Left is not DdfIrIndex index) return false;
                DdfBoundExpression indexBound = Take(index);
                DdfBoundExpression targetBound = Peek(index.Target);
                if (indexBound == null || targetBound == null ||
                    targetBound.Type.ArrayRank == 0) return false;
                RoslynExpressionSyntax target = GenerateExpression(index.Target);
                RoslynExpressionSyntax offset = GenerateExpression(index.Index);
                RoslynExpressionSyntax value = GenerateExpression(binary.Right);
                if (target == null || offset == null || value == null) return true;
                TypeSyntax elementType = GenerateType(indexBound.Type, index);
                if (elementType == null) return true;
                result = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntimeValues"),
                            SyntaxFactory.GenericName("SetArrayElement")
                                .AddTypeArgumentListArguments(elementType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(target),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(value),
                        SyntaxFactory.Argument(Location(index)));
                return true;
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
                return Await(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.IdentifierName("AtCallAsync")))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(Location(call)),
                        SyntaxFactory.Argument(
                            SyntaxFactory.ParenthesizedLambdaExpression(
                                Await(invocation))
                            .WithAsyncKeyword(SyntaxFactory.Token(
                                CSharpSyntaxKind.AsyncKeyword)))));
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
                                SyntaxFactory.IdentifierName("InvokeCapabilityAsync")))
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
                    return Await(invocation);
                TypeSyntax returnType = GenerateType(target.Type, call);
                return returnType == null ? null : Await(invocation.WithExpression(
                    SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                        SyntaxFactory.GenericName("InvokeCapabilityValueAsync")
                            .AddTypeArgumentListArguments(returnType))));
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
                return Await(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.GenericName("InvokeCapabilityValueAsync")
                                    .AddTypeArgumentListArguments(returnType)))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                CSharpSyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(capability))),
                            SyntaxFactory.Argument(ObjectArray(arguments)),
                            SyntaxFactory.Argument(Location(location))));
            }

            private RoslynExpressionSyntax GenerateHostCapabilityWrite(
                string capability, RoslynExpressionSyntax value,
                DdfBoundType type, DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return Await(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("WriteCapabilityValueAsync")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(value),
                        SyntaxFactory.Argument(Location(location))));
            }

            private RoslynExpressionSyntax GenerateHostElementRead(
                string capability, RoslynExpressionSyntax offset,
                DdfBoundType type, DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return Await(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("ReadCapabilityElementAsync")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(Location(location))));
            }

            private RoslynExpressionSyntax GenerateHostElementWrite(
                string capability, RoslynExpressionSyntax offset,
                RoslynExpressionSyntax value, DdfBoundType type,
                DdfIrNode location)
            {
                TypeSyntax returnType = GenerateType(type, location);
                if (returnType == null) return null;
                return Await(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            CSharpSyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                            SyntaxFactory.GenericName("WriteCapabilityElementAsync")
                                .AddTypeArgumentListArguments(returnType)))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            CSharpSyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(capability))),
                        SyntaxFactory.Argument(offset),
                        SyntaxFactory.Argument(value),
                        SyntaxFactory.Argument(Location(location))));
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
                        (item.Kind == DdfBoundSymbolKind.Field ||
                         item.Kind == DdfBoundSymbolKind.EnumerationMember));
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

            private RoslynExpressionSyntax GenerateConstant(DdfConstantValue constant)
            {
                if (constant == null) return null;
                if (constant.Kind == DdfConstantKind.Boolean)
                    return SyntaxFactory.LiteralExpression((bool)constant.Value
                        ? CSharpSyntaxKind.TrueLiteralExpression
                        : CSharpSyntaxKind.FalseLiteralExpression);
                if (constant.Kind == DdfConstantKind.String)
                    return SyntaxFactory.LiteralExpression(CSharpSyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal((string)constant.Value));
                if (constant.Kind == DdfConstantKind.Character)
                    return SyntaxFactory.LiteralExpression(CSharpSyntaxKind.CharacterLiteralExpression,
                        SyntaxFactory.Literal((char)constant.Value));
                if (constant.Kind == DdfConstantKind.Floating)
                    return SyntaxFactory.LiteralExpression(CSharpSyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal((double)constant.Value));
                if (constant.Kind == DdfConstantKind.Enumeration)
                    return GenerateEnumerationConstant(constant);
                return SyntaxFactory.LiteralExpression(CSharpSyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal((int)constant.Value));
            }

            private RoslynExpressionSyntax GenerateEnumerationConstant(
                DdfConstantValue constant)
            {
                if (!(constant.Value is DdfEnumValue value)) return null;
                DdfBoundSymbol enumeration = workItem.BoundUnit.Symbols
                    .Where(item => item.Kind == DdfBoundSymbolKind.Enumeration &&
                        item.Name == value.TypeName)
                    .OrderByDescending(item => item.ModuleIdentity == workItem.Identity)
                    .FirstOrDefault();
                DdfBoundSymbol member = enumeration == null ? null :
                    workItem.BoundUnit.Symbols.FirstOrDefault(item =>
                        item.Kind == DdfBoundSymbolKind.EnumerationMember &&
                        item.ModuleIdentity == enumeration.ModuleIdentity &&
                        item.Type.Name == enumeration.Name &&
                        item.Name == value.MemberName);
                if (enumeration == null || member == null)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC400",
                        relativePath + ": valore enum fixed non risolto: " +
                        value + "."));
                    return null;
                }
                if (enumeration.ModuleIdentity == workItem.Identity)
                    return SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(enumeration.Id)),
                        SyntaxFactory.IdentifierName(workItem.GeneratedNames
                            .GetIdentifier(member.Id)));
                if (linkNames == null)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC400",
                        relativePath + ": tabella link richiesta per il valore enum fixed " +
                        value + "."));
                    return null;
                }
                try
                {
                    DdfDotNetLinkedSymbolName linkedEnumeration =
                        linkNames.Resolve(enumeration);
                    DdfDotNetLinkedSymbolName linkedMember = linkNames.Resolve(member);
                    return DdfDotNetNameMangler.CreateQualifiedExpression(
                        linkedEnumeration.NamespaceName.Split('.')
                            .Concat(new[] { linkedEnumeration.ContainerTypeName,
                                linkedEnumeration.Identifier,
                                linkedMember.Identifier }).ToArray());
                }
                catch (KeyNotFoundException exception)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC400",
                        relativePath + ": " + exception.Message));
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

            private static RoslynStatementSyntax RuntimeCaptureErrorCall() =>
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DdfRuntime_Context"),
                                SyntaxFactory.IdentifierName("CaptureError")))
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(
                                    SyntaxFactory.IdentifierName("exception"))))));

            private RoslynStatementSyntax RegisterLocal(
                DdfIrVariableDeclaration variable)
            {
                DdfBoundSymbol symbol = FindDeclared(DdfBoundSymbolKind.Local,
                    variable.Start, variable.Name);
                return symbol == null ? null : RegisterDebugValue(
                    "RegisterDebugLocal", symbol,
                    Type(variable.Type).DisplayName,
                    SyntaxFactory.IdentifierName(workItem.GeneratedNames
                        .GetIdentifier(symbol.Id)), variable.IsFixed,
                    variable.IsFixed);
            }

            private static RoslynStatementSyntax RegisterDebugValue(string method,
                DdfBoundSymbol symbol, string typeName,
                RoslynExpressionSyntax value, bool isReadOnly = false,
                bool isFixed = false)
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
                            SyntaxFactory.Argument(reader),
                            SyntaxFactory.Argument(isReadOnly
                                ? SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.TrueLiteralExpression)
                                : SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.FalseLiteralExpression)),
                            SyntaxFactory.Argument(isFixed
                                ? SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.TrueLiteralExpression)
                                : SyntaxFactory.LiteralExpression(
                                    CSharpSyntaxKind.FalseLiteralExpression))));
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
                            (item.Kind == DdfBoundSymbolKind.Structure ||
                             item.Kind == DdfBoundSymbolKind.Enumeration) &&
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
                    return Await(SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                CSharpSyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(workItem.GeneratedNames
                                    .GetIdentifier(structure.Id)),
                                SyntaxFactory.IdentifierName("CreateAsync")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(
                            SyntaxFactory.IdentifierName("DdfRuntime_Context"))));
                DdfBoundSymbol enumeration = workItem.BoundUnit.Symbols
                    .FirstOrDefault(item => item.Kind == DdfBoundSymbolKind.Enumeration &&
                        item.ModuleIdentity == workItem.Identity &&
                        item.Name == variable.Type.Name);
                if (enumeration != null)
                    return SyntaxFactory.DefaultExpression(SyntaxFactory.IdentifierName(
                        workItem.GeneratedNames.GetIdentifier(enumeration.Id)));
                return null;
            }

            private static TypeSyntax AsyncReturnType(DdfBoundType type,
                TypeSyntax valueType) => type.Name == "void" &&
                type.ArrayRank == 0
                    ? SyntaxFactory.IdentifierName("Task")
                    : SyntaxFactory.GenericName("Task")
                        .AddTypeArgumentListArguments(valueType);

            private static AwaitExpressionSyntax Await(
                RoslynExpressionSyntax expression) =>
                SyntaxFactory.AwaitExpression(expression);

            private static RoslynStatementSyntax AwaitCompletedTask() =>
                SyntaxFactory.ExpressionStatement(Await(
                    SyntaxFactory.MemberAccessExpression(
                        CSharpSyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Task"),
                        SyntaxFactory.IdentifierName("CompletedTask"))));

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
            IReadOnlyList<string> stateCaptureNames = documents.Select(document =>
                    workItem.GeneratedNames.GetDocumentStateCaptureName(
                        document.RelativePath))
                .ToList().AsReadOnly();
            IReadOnlyList<string> stateApplyNames = documents.Select(document =>
                    workItem.GeneratedNames.GetDocumentStateApplyName(
                        document.RelativePath))
                .ToList().AsReadOnly();
            for (int documentIndex = 0; documentIndex < documents.Count;
                 documentIndex++)
            {
                DdfBoundIrDocument document = documents[documentIndex];
                var generator = new Generator(workItem, mapping, document, issues,
                    linkNames, initializerNames[documentIndex],
                    stateCaptureNames[documentIndex],
                    stateApplyNames[documentIndex], documentIndex == 0,
                    initializerNames, stateCaptureNames, stateApplyNames);
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
