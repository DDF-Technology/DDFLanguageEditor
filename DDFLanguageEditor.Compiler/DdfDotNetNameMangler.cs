using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DDFLanguageEditor.Core;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetGeneratedName
    {
        internal DdfDotNetGeneratedName(string symbolId, DdfBoundSymbolKind symbolKind,
            string identifier)
        {
            SymbolId = symbolId ?? throw new ArgumentNullException(nameof(symbolId));
            SymbolKind = symbolKind;
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        public string SymbolId { get; }
        public DdfBoundSymbolKind SymbolKind { get; }
        public string Identifier { get; }
    }

    public sealed class DdfDotNetNameMap
    {
        private readonly IReadOnlyDictionary<string, DdfDotNetGeneratedName> bySymbolId;

        internal DdfDotNetNameMap(string moduleIdentity, string namespaceName,
            string containerTypeName, IEnumerable<DdfDotNetGeneratedName> symbols)
        {
            ModuleIdentity = moduleIdentity ?? throw new ArgumentNullException(nameof(moduleIdentity));
            NamespaceName = namespaceName ?? throw new ArgumentNullException(nameof(namespaceName));
            ContainerTypeName = containerTypeName ??
                throw new ArgumentNullException(nameof(containerTypeName));
            ModuleInitializerName = "DdfGenerated_Helper_ModuleInitialize";
            InitializationStateName = "DdfGenerated_Helper_ModuleInitialized";
            StateCaptureName = "DdfGenerated_Helper_StateCapture";
            StateApplyName = "DdfGenerated_Helper_StateApply";
            var values = (symbols ?? Array.Empty<DdfDotNetGeneratedName>()).ToList();
            if (values.Select(item => item.SymbolId).Distinct(StringComparer.Ordinal).Count() !=
                values.Count)
                throw new InvalidOperationException("Identità simboliche duplicate nel name map.");
            if (values.Select(item => item.Identifier)
                    .Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new InvalidOperationException("Collisione nel name mangling .NET.");
            Symbols = values.OrderBy(item => item.SymbolId, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            bySymbolId = Symbols.ToDictionary(item => item.SymbolId,
                StringComparer.Ordinal);
        }

        public string ModuleIdentity { get; }
        public string NamespaceName { get; }
        public string ContainerTypeName { get; }
        public string ModuleInitializerName { get; }
        public string InitializationStateName { get; }
        public string StateCaptureName { get; }
        public string StateApplyName { get; }
        public IReadOnlyList<DdfDotNetGeneratedName> Symbols { get; }

        public string GetIdentifier(string symbolId)
        {
            if (!bySymbolId.TryGetValue(symbolId ?? string.Empty,
                    out DdfDotNetGeneratedName value))
                throw new KeyNotFoundException("Simbolo bound non presente nel name map: " +
                    (symbolId ?? string.Empty) + ".");
            return value.Identifier;
        }

        public string GetDocumentInitializerName(string relativePath) =>
            "DdfGenerated_Helper_DocumentInitialize_" +
            DdfDotNetNameMangler.HashValue((relativePath ?? string.Empty)
                .Replace('\\', '/'), 24);

        public string GetDocumentStateCaptureName(string relativePath) =>
            "DdfGenerated_Helper_DocumentStateCapture_" +
            DdfDotNetNameMangler.HashValue((relativePath ?? string.Empty)
                .Replace('\\', '/'), 24);

        public string GetDocumentStateApplyName(string relativePath) =>
            "DdfGenerated_Helper_DocumentStateApply_" +
            DdfDotNetNameMangler.HashValue((relativePath ?? string.Empty)
                .Replace('\\', '/'), 24);
    }

    public sealed class DdfDotNetLinkedSymbolName
    {
        internal DdfDotNetLinkedSymbolName(string moduleIdentity,
            string namespaceName, string containerTypeName, string identifier)
        {
            ModuleIdentity = moduleIdentity;
            NamespaceName = namespaceName;
            ContainerTypeName = containerTypeName;
            Identifier = identifier;
        }

        public string ModuleIdentity { get; }
        public string NamespaceName { get; }
        public string ContainerTypeName { get; }
        public string Identifier { get; }
    }

    public sealed class DdfDotNetLinkedModuleName
    {
        internal DdfDotNetLinkedModuleName(string moduleIdentity,
            string namespaceName, string containerTypeName,
            string moduleInitializerName)
        {
            ModuleIdentity = moduleIdentity;
            NamespaceName = namespaceName;
            ContainerTypeName = containerTypeName;
            ModuleInitializerName = moduleInitializerName;
        }

        public string ModuleIdentity { get; }
        public string NamespaceName { get; }
        public string ContainerTypeName { get; }
        public string ModuleInitializerName { get; }
    }

    public sealed class DdfDotNetLinkNameTable
    {
        private readonly IReadOnlyDictionary<string, DdfDotNetNameMap> modules;

        private DdfDotNetLinkNameTable(
            IReadOnlyDictionary<string, DdfDotNetNameMap> modules)
        {
            this.modules = modules;
        }

        public static DdfDotNetLinkNameTable Create(
            IEnumerable<DdfDotNetCompilationWorkItem> workItems)
        {
            if (workItems == null) throw new ArgumentNullException(nameof(workItems));
            var modules = new Dictionary<string, DdfDotNetNameMap>(
                StringComparer.Ordinal);
            foreach (DdfDotNetCompilationWorkItem item in workItems)
            {
                if (item == null || !modules.TryAdd(item.Identity,
                        item.GeneratedNames))
                    throw new ArgumentException(
                        "Work item assente o modulo duplicato nella tabella link.",
                        nameof(workItems));
            }
            return new DdfDotNetLinkNameTable(modules);
        }

        public DdfDotNetLinkedSymbolName Resolve(DdfBoundSymbol symbol)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (!modules.TryGetValue(symbol.ModuleIdentity,
                    out DdfDotNetNameMap module))
                throw new KeyNotFoundException("Modulo non presente nella tabella link: " +
                    symbol.ModuleIdentity + ".");
            return new DdfDotNetLinkedSymbolName(symbol.ModuleIdentity,
                module.NamespaceName, module.ContainerTypeName,
                module.GetIdentifier(symbol.Id));
        }

        public DdfDotNetLinkedModuleName ResolveModule(string moduleIdentity)
        {
            if (!modules.TryGetValue(moduleIdentity ?? string.Empty,
                    out DdfDotNetNameMap module))
                throw new KeyNotFoundException("Modulo non presente nella tabella link: " +
                    (moduleIdentity ?? string.Empty) + ".");
            return new DdfDotNetLinkedModuleName(moduleIdentity,
                module.NamespaceName, module.ContainerTypeName,
                module.ModuleInitializerName);
        }
    }

    /// <summary>
    /// Produces identifiers solely from bound symbol identities. User text never
    /// becomes a generated C# token.
    /// </summary>
    public static class DdfDotNetNameMangler
    {
        public const string FormatVersion = "1.0";

        public static DdfDotNetNameMap Create(DdfBoundCompilationUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            string moduleHash = Hash(unit.ModuleIdentity, 20);
            string namespaceName = "Ddf.Generated.M" + moduleHash;
            string containerTypeName = "Module_" + moduleHash;
            var names = unit.Symbols.Select(symbol => new DdfDotNetGeneratedName(
                symbol.Id, symbol.Kind, Mangle(symbol))).ToList();
            ValidateIdentifier(containerTypeName);
            foreach (string part in namespaceName.Split('.')) ValidateIdentifier(part);
            foreach (DdfDotNetGeneratedName name in names)
                ValidateIdentifier(name.Identifier);
            var result = new DdfDotNetNameMap(unit.ModuleIdentity, namespaceName,
                containerTypeName, names);
            ValidateIdentifier(result.ModuleInitializerName);
            ValidateIdentifier(result.InitializationStateName);
            ValidateIdentifier(result.StateCaptureName);
            ValidateIdentifier(result.StateApplyName);
            foreach (DdfBoundIrDocument document in unit.Documents)
            {
                ValidateIdentifier(result.GetDocumentInitializerName(
                    document.RelativePath));
                ValidateIdentifier(result.GetDocumentStateCaptureName(
                    document.RelativePath));
                ValidateIdentifier(result.GetDocumentStateApplyName(
                    document.RelativePath));
            }
            return result;
        }

        internal static NameSyntax CreateNamespaceSyntax(string namespaceName)
        {
            string[] parts = (namespaceName ?? string.Empty).Split('.');
            if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Namespace generato non valido.",
                    nameof(namespaceName));
            foreach (string part in parts) ValidateIdentifier(part);
            NameSyntax result = SyntaxFactory.IdentifierName(parts[0]);
            for (int index = 1; index < parts.Length; index++)
                result = SyntaxFactory.QualifiedName(result,
                    SyntaxFactory.IdentifierName(parts[index]));
            return result;
        }

        internal static RoslynExpressionSyntax CreateQualifiedExpression(
            params string[] identifiers)
        {
            if (identifiers == null || identifiers.Length == 0)
                throw new ArgumentException("È richiesto almeno un identificatore.",
                    nameof(identifiers));
            foreach (string identifier in identifiers) ValidateIdentifier(identifier);
            RoslynExpressionSyntax result = SyntaxFactory.IdentifierName(identifiers[0]);
            for (int index = 1; index < identifiers.Length; index++)
                result = SyntaxFactory.MemberAccessExpression(
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression,
                    result, SyntaxFactory.IdentifierName(identifiers[index]));
            return result;
        }

        private static string Mangle(DdfBoundSymbol symbol)
        {
            string prefix = symbol.Kind switch
            {
                DdfBoundSymbolKind.Structure => "T",
                DdfBoundSymbolKind.Enumeration => "E",
                DdfBoundSymbolKind.EnumerationMember => "EV",
                DdfBoundSymbolKind.HostType => "HT",
                DdfBoundSymbolKind.Function => "F",
                DdfBoundSymbolKind.StandardFunction => "SF",
                DdfBoundSymbolKind.HostFunction => "HF",
                DdfBoundSymbolKind.Global => "G",
                DdfBoundSymbolKind.Field => "D",
                DdfBoundSymbolKind.Parameter => "P",
                DdfBoundSymbolKind.Local => "L",
                DdfBoundSymbolKind.HostObject => "HO",
                _ => "S"
            };
            return "DdfGenerated_" + prefix + "_" + Hash(symbol.Id, 32);
        }

        internal static string HashValue(string value, int characters) =>
            Hash(value, characters);

        private static string Hash(string value, int characters)
        {
            using SHA256 sha = SHA256.Create();
            string result = Convert.ToHexString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
            return result.Substring(0, Math.Min(characters, result.Length));
        }

        private static void ValidateIdentifier(string value)
        {
            if (!SyntaxFacts.IsValidIdentifier(value) ||
                SyntaxFacts.GetKeywordKind(value) !=
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ||
                SyntaxFacts.GetContextualKeywordKind(value) !=
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
                throw new InvalidOperationException(
                    "Il name mangling ha prodotto un identificatore C# non valido.");
        }
    }
}
