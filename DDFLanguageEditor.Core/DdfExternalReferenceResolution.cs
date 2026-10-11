using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace DDFLanguageEditor.Core
{
    public enum DdfExternalReferenceContext
    {
        Standalone,
        Embedded
    }

    public enum DdfExternalReferenceBindingKind
    {
        ManagedAssembly,
        TypedAdapter
    }

    public sealed class DdfExternalReferenceResolutionRequest
    {
        public DdfExternalReferenceResolutionRequest(string projectRoot,
            DdfProjectExternalReference reference,
            DdfExternalReferenceContext context)
        {
            ProjectRoot = Path.GetFullPath(projectRoot ?? throw new
                ArgumentNullException(nameof(projectRoot)));
            Reference = reference ?? throw new ArgumentNullException(
                nameof(reference));
            Context = context;
        }

        public string ProjectRoot { get; }
        public DdfProjectExternalReference Reference { get; }
        public DdfExternalReferenceContext Context { get; }
    }

    public sealed class DdfResolvedExternalReference
    {
        internal DdfResolvedExternalReference(
            DdfProjectExternalReference declaration,
            DdfExternalReferenceBindingKind bindingKind,
            string assemblyPath, string adapterId, string sha256, long size,
            IEnumerable<DdfResolvedExternalReferenceDependency> dependencies,
            DdfExternalApiDefinition apiDefinition)
        {
            Id = declaration.Id;
            Kind = declaration.Kind;
            Version = declaration.Version;
            Architecture = declaration.Architecture;
            Origin = declaration.Origin;
            Capabilities = declaration.Capabilities.OrderBy(value => value,
                StringComparer.Ordinal).ToList().AsReadOnly();
            BindingKind = bindingKind;
            AssemblyPath = assemblyPath ?? string.Empty;
            AdapterId = adapterId ?? string.Empty;
            Sha256 = sha256 ?? string.Empty;
            Size = size;
            Dependencies = (dependencies ?? Array.Empty<
                    DdfResolvedExternalReferenceDependency>())
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            ApiDefinition = apiDefinition ?? DdfExternalApiDefinition.Empty(
                declaration.Id, declaration.Version);
        }

        public string Id { get; }
        public string Kind { get; }
        public string Version { get; }
        public string Architecture { get; }
        public string Origin { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public DdfExternalReferenceBindingKind BindingKind { get; }
        public string AssemblyPath { get; }
        public string AdapterId { get; }
        public string Sha256 { get; }
        public long Size { get; }
        public IReadOnlyList<DdfResolvedExternalReferenceDependency>
            Dependencies { get; }
        public DdfExternalApiDefinition ApiDefinition { get; }
        public string DeclarationDocumentPath { get; private set; } =
            string.Empty;
        public int DeclarationStart { get; private set; }
        public int DeclarationLength { get; private set; } = 1;
        public int DeclarationLine { get; private set; } = 1;
        public int DeclarationColumn { get; private set; } = 1;

        internal void AttachDeclarationLocation(
            DdfProjectManifestLocation location)
        {
            if (location == null) return;
            DeclarationDocumentPath = location.DocumentPath;
            DeclarationStart = location.Start;
            DeclarationLength = location.Length;
            DeclarationLine = location.Line;
            DeclarationColumn = location.Column;
        }
    }

    public sealed class DdfResolvedExternalReferenceDependency
    {
        internal DdfResolvedExternalReferenceDependency(string id,
            string version, string path, string sha256, long size)
        {
            Id = id;
            Version = version;
            Path = path;
            Sha256 = sha256;
            Size = size;
        }

        public string Id { get; }
        public string Version { get; }
        public string Path { get; }
        public string Sha256 { get; }
        public long Size { get; }
    }

    public sealed class DdfExternalReferenceResolutionResult
    {
        internal DdfExternalReferenceResolutionResult(
            DdfResolvedExternalReference reference, string code,
            string message, string targetProperty = null)
        {
            Reference = reference;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            TargetProperty = targetProperty ?? string.Empty;
        }

        public DdfResolvedExternalReference Reference { get; }
        public string Code { get; }
        public string Message { get; }
        public string TargetProperty { get; }
        public bool Success => Reference != null && Code.Length == 0;

        internal static DdfExternalReferenceResolutionResult Resolved(
            DdfResolvedExternalReference reference) => new(reference, null,
                null);

        internal static DdfExternalReferenceResolutionResult Rejected(
            string code, string message, string targetProperty = null) =>
            new(null, code, message, targetProperty);
    }

    public interface IDdfExternalReferenceResolver
    {
        DdfExternalReferenceResolutionResult Resolve(
            DdfExternalReferenceResolutionRequest request);
    }

    public sealed class DdfExternalReferenceCatalogEntry
    {
        private DdfExternalReferenceCatalogEntry(string id, string kind,
            string version, string architecture, string origin,
            IEnumerable<string> capabilities,
            DdfExternalReferenceBindingKind bindingKind, string assemblyPath,
            string adapterId,
            IEnumerable<DdfExternalReferenceCatalogDependency> dependencies,
            DdfExternalApiDefinition apiDefinition)
        {
            Id = id;
            Kind = kind;
            Version = version;
            Architecture = architecture;
            Origin = origin;
            Capabilities = (capabilities ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal).ToList()
                .AsReadOnly();
            BindingKind = bindingKind;
            AssemblyPath = assemblyPath ?? string.Empty;
            AdapterId = adapterId ?? string.Empty;
            Dependencies = (dependencies ?? Array.Empty<
                    DdfExternalReferenceCatalogDependency>()).ToList()
                .AsReadOnly();
            ApiDefinition = apiDefinition ?? DdfExternalApiDefinition.Empty(id,
                version);
        }

        public string Id { get; }
        public string Kind { get; }
        public string Version { get; }
        public string Architecture { get; }
        public string Origin { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public DdfExternalReferenceBindingKind BindingKind { get; }
        public string AssemblyPath { get; }
        public string AdapterId { get; }
        public IReadOnlyList<DdfExternalReferenceCatalogDependency>
            Dependencies { get; }
        public DdfExternalApiDefinition ApiDefinition { get; }

        public static DdfExternalReferenceCatalogEntry ManagedAssembly(
            string id, string version, string architecture, string origin,
            IEnumerable<string> capabilities, string assemblyPath,
            IEnumerable<DdfExternalReferenceCatalogDependency> dependencies =
                null, DdfExternalApiDefinition apiDefinition = null) => new(
                id, DdfExternalReferenceKinds.ManagedDotNet, version,
                architecture, origin, capabilities,
                DdfExternalReferenceBindingKind.ManagedAssembly,
                Path.GetFullPath(assemblyPath ?? throw new
                    ArgumentNullException(nameof(assemblyPath))), null,
                dependencies, apiDefinition);

        public static DdfExternalReferenceCatalogEntry TypedAdapter(string id,
            string kind, string version, string architecture, string origin,
            IEnumerable<string> capabilities, string adapterId,
            DdfExternalApiDefinition apiDefinition = null) => new(id,
                kind, version, architecture, origin, capabilities,
                DdfExternalReferenceBindingKind.TypedAdapter, null,
                adapterId, null, apiDefinition);
    }

    public sealed class DdfExternalReferenceCatalogDependency
    {
        public DdfExternalReferenceCatalogDependency(string id,
            string version, string assemblyPath)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Version = version ?? throw new ArgumentNullException(
                nameof(version));
            AssemblyPath = Path.GetFullPath(assemblyPath ?? throw new
                ArgumentNullException(nameof(assemblyPath)));
        }

        public string Id { get; }
        public string Version { get; }
        public string AssemblyPath { get; }
    }

    /// <summary>
    /// Resolves only exact entries supplied by the application. It never scans
    /// the machine, registry, GAC or project directories.
    /// </summary>
    public sealed class DdfExternalReferenceCatalogResolver :
        IDdfExternalReferenceResolver
    {
        private readonly IReadOnlyList<DdfExternalReferenceCatalogEntry>
            entries;
        private readonly Func<DdfExternalReferenceResolutionRequest, bool>
            authorize;

        public DdfExternalReferenceCatalogResolver(
            IEnumerable<DdfExternalReferenceCatalogEntry> entries,
            Func<DdfExternalReferenceResolutionRequest, bool> authorize)
        {
            this.entries = (entries ?? throw new ArgumentNullException(
                    nameof(entries))).ToList().AsReadOnly();
            this.authorize = authorize ?? throw new ArgumentNullException(
                nameof(authorize));
            if (this.entries.Count == 0)
                throw new ArgumentException(
                    "Il catalogo riferimenti non può essere vuoto.",
                    nameof(entries));
        }

        public DdfExternalReferenceResolutionResult Resolve(
            DdfExternalReferenceResolutionRequest request)
        {
            if (request == null) throw new ArgumentNullException(
                nameof(request));
            DdfProjectExternalReference declaration = request.Reference;
            DdfExternalReferenceCatalogEntry[] sameId = entries.Where(entry =>
                    entry.Id == declaration.Id).ToArray();
            if (sameId.Length == 0)
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR001", "Nessuna voce autorizzabile nel catalogo per " +
                    declaration.Id + ".", "id");
            if (!sameId.Any(entry => entry.Kind == declaration.Kind))
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR014", "Tipo " + declaration.Kind +
                    " non disponibile per " + declaration.Id + ".", "kind");
            DdfExternalReferenceCatalogEntry[] sameKind = sameId.Where(
                entry => entry.Kind == declaration.Kind).ToArray();
            if (!sameKind.Any(entry => entry.Version == declaration.Version))
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR012", "Versione " + declaration.Version +
                    " non disponibile per " + declaration.Id +
                    "; versioni catalogate: " + string.Join(", ", sameKind
                        .Select(entry => entry.Version).Distinct(
                            StringComparer.Ordinal).OrderBy(value => value,
                            StringComparer.Ordinal)) + ".", "version");
            DdfExternalReferenceCatalogEntry[] sameVersion = sameKind.Where(
                entry => entry.Version == declaration.Version).ToArray();
            if (!sameVersion.Any(entry => entry.Architecture ==
                    declaration.Architecture))
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR013", "Architettura " + declaration.Architecture +
                    " non disponibile per " + declaration.Id + " " +
                    declaration.Version + "; architetture catalogate: " +
                    string.Join(", ", sameVersion.Select(entry =>
                            entry.Architecture).Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)) +
                    ".", "architecture");
            DdfExternalReferenceCatalogEntry[] matches = sameVersion.Where(entry =>
                    entry.Kind == declaration.Kind &&
                    entry.Architecture == declaration.Architecture &&
                    entry.Origin == declaration.Origin &&
                    entry.Capabilities.SequenceEqual(
                        declaration.Capabilities.OrderBy(value => value,
                            StringComparer.Ordinal), StringComparer.Ordinal))
                .ToArray();
            if (matches.Length == 0)
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR014", "Tipo, origine o capacità non coincidono con " +
                    "la voce catalogata per " + declaration.Id + " " +
                    declaration.Version + ".", MismatchProperty(sameVersion,
                        declaration));
            if (matches.Length != 1)
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR001", "Il catalogo deve contenere una sola " +
                    "corrispondenza esatta per " + declaration.Id + ".",
                    "id");
            DdfExternalReferenceCatalogEntry match = matches[0];
            if (match.ApiDefinition.ReferenceId != declaration.Id ||
                match.ApiDefinition.ReferenceVersion != declaration.Version)
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR008", "Il descrittore API non coincide con identità e " +
                    "versione del riferimento " + declaration.Id + ".",
                    "version");
            if (!authorize(request))
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR002", "Autorizzazione negata per " +
                    declaration.Id + ".", "id");

            if (match.BindingKind ==
                DdfExternalReferenceBindingKind.ManagedAssembly)
            {
                if (request.Context != DdfExternalReferenceContext.Standalone)
                    return DdfExternalReferenceResolutionResult.Rejected(
                        "DDFR003", "Gli assembly gestiti possono essere " +
                        "consegnati soltanto al contesto standalone.", "kind");
                if (!File.Exists(match.AssemblyPath))
                    return DdfExternalReferenceResolutionResult.Rejected(
                        "DDFR004", "Assembly autorizzato non disponibile per " +
                        declaration.Id + ".");
                try
                {
                    _ = AssemblyName.GetAssemblyName(match.AssemblyPath);
                }
                catch (BadImageFormatException)
                {
                    return DdfExternalReferenceResolutionResult.Rejected(
                        "DDFR006", "Il file autorizzato non è un assembly .NET " +
                        "gestito valido per " + declaration.Id + ".");
                }
                var info = new FileInfo(match.AssemblyPath);
                using FileStream stream = File.OpenRead(match.AssemblyPath);
                string hash = Convert.ToHexString(SHA256.HashData(stream))
                    .ToLowerInvariant();
                var dependencies = new List<
                    DdfResolvedExternalReferenceDependency>();
                foreach (DdfExternalReferenceCatalogDependency dependency in
                         match.Dependencies.OrderBy(item => item.Id,
                             StringComparer.Ordinal))
                {
                    if (!File.Exists(dependency.AssemblyPath))
                        return DdfExternalReferenceResolutionResult.Rejected(
                            "DDFR007", "Dipendenza transitiva non disponibile: " +
                            dependency.Id + ".");
                    try
                    {
                        _ = AssemblyName.GetAssemblyName(
                            dependency.AssemblyPath);
                    }
                    catch (BadImageFormatException)
                    {
                        return DdfExternalReferenceResolutionResult.Rejected(
                            "DDFR007", "Dipendenza transitiva non gestita: " +
                            dependency.Id + ".");
                    }
                    var dependencyInfo = new FileInfo(
                        dependency.AssemblyPath);
                    using FileStream dependencyStream = File.OpenRead(
                        dependency.AssemblyPath);
                    dependencies.Add(
                        new DdfResolvedExternalReferenceDependency(
                            dependency.Id, dependency.Version,
                            dependency.AssemblyPath,
                            Convert.ToHexString(SHA256.HashData(
                                dependencyStream)).ToLowerInvariant(),
                            dependencyInfo.Length));
                }
                return DdfExternalReferenceResolutionResult.Resolved(
                    new DdfResolvedExternalReference(declaration,
                        match.BindingKind, match.AssemblyPath, null, hash,
                        info.Length, dependencies, match.ApiDefinition));
            }

            if (string.IsNullOrWhiteSpace(match.AdapterId))
                return DdfExternalReferenceResolutionResult.Rejected(
                    "DDFR005", "Adapter tipizzato non valido per " +
                    declaration.Id + ".", "id");
            return DdfExternalReferenceResolutionResult.Resolved(
                new DdfResolvedExternalReference(declaration,
                    match.BindingKind, null, match.AdapterId, null, 0, null,
                    match.ApiDefinition));
        }

        private static string MismatchProperty(
            IEnumerable<DdfExternalReferenceCatalogEntry> candidates,
            DdfProjectExternalReference declaration)
        {
            DdfExternalReferenceCatalogEntry[] values = candidates.ToArray();
            if (!values.Any(entry => entry.Kind == declaration.Kind))
                return "kind";
            if (!values.Any(entry => entry.Architecture ==
                    declaration.Architecture)) return "architecture";
            if (!values.Any(entry => entry.Origin == declaration.Origin))
                return "origin";
            return "capabilities";
        }
    }
}
