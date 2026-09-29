using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Immutable view of the declarations exported by a project's direct module dependencies.
    /// It is deliberately separate from <see cref="DdfWorkspaceIndex"/> so installed sources do
    /// not appear as editable project files and private module declarations remain inaccessible.
    /// </summary>
    public sealed class DdfModuleEditorIndex
    {
        private DdfModuleEditorIndex(IReadOnlyList<DdfWorkspaceDocument> documents,
            IReadOnlyList<DdfBuildIssue> issues)
        {
            Documents = documents;
            Issues = issues;
        }

        public IReadOnlyList<DdfWorkspaceDocument> Documents { get; }
        public IReadOnlyList<DdfBuildIssue> Issues { get; }

        public static DdfModuleEditorIndex Empty { get; } = new DdfModuleEditorIndex(
            Array.Empty<DdfWorkspaceDocument>(), Array.Empty<DdfBuildIssue>());

        public static DdfModuleEditorIndex Load(string projectRoot, DdfLanguagePackage package,
            CancellationToken cancellationToken = default, IDdfModuleResolver resolver = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException(
                "Cartella progetto richiesta.", nameof(projectRoot));
            if (package == null) throw new ArgumentNullException(nameof(package));

            string root = Path.GetFullPath(projectRoot);
            cancellationToken.ThrowIfCancellationRequested();
            DdfProjectManifestLoadResult loadedProject = DdfProjectManifestLoader.Load(root);
            if (!loadedProject.Success)
                return new DdfModuleEditorIndex(Array.Empty<DdfWorkspaceDocument>(), loadedProject.Issues);

            DdfProjectManifest project = loadedProject.Manifest;
            var issues = new List<DdfBuildIssue>();
            var documents = new List<DdfWorkspaceDocument>();
            IDdfModuleResolver moduleResolver = resolver ?? new DdfProjectModuleStoreResolver();

            foreach (DdfProjectDependency dependency in project.Dependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string moduleRoot = moduleResolver.ResolveModuleRoot(root, dependency);
                if (string.IsNullOrWhiteSpace(moduleRoot))
                {
                    issues.Add(DdfBuildIssue.Configuration("DDFB013",
                        "Modulo richiesto non trovato: " + dependency.Id + "@" + dependency.Version + "."));
                    continue;
                }

                DdfProjectManifestLoadResult loadedModule = DdfProjectManifestLoader.Load(moduleRoot);
                if (!loadedModule.Success)
                {
                    issues.AddRange(loadedModule.Issues);
                    continue;
                }

                DdfProjectManifest module = loadedModule.Manifest;
                if (module.Module.Id != dependency.Id || module.Module.Version != dependency.Version)
                {
                    issues.Add(DdfBuildIssue.Configuration("DDFB014",
                        "Il modulo risolto dichiara '" + module.Module.Id + "@" + module.Module.Version +
                        "', ma era richiesto '" + dependency.Id + "@" + dependency.Version + "'.",
                        Path.Combine(moduleRoot, DdfProjectManifest.FileName)));
                    continue;
                }
                if (module.LanguageId != project.LanguageId ||
                    module.LanguagePackageVersion != project.LanguagePackageVersion ||
                    module.Backend != project.Backend || module.LanguageId != package.Id ||
                    module.LanguagePackageVersion != package.PackageVersion)
                {
                    issues.Add(DdfBuildIssue.Configuration("DDFB017",
                        "Il modulo '" + dependency.Id + "@" + dependency.Version +
                        "' non usa lo stesso linguaggio, pacchetto e backend del progetto.",
                        Path.Combine(moduleRoot, DdfProjectManifest.FileName),
                        DdfDiagnosticDomain.LanguageDefinition));
                    continue;
                }

                var exports = new HashSet<string>(module.Module.Exports, StringComparer.Ordinal);
                foreach (string path in ResolveSourcePaths(moduleRoot, module, issues))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string source = File.ReadAllText(path, Encoding.UTF8);
                    DdfParseResult parse = DdfParser.Parse(source, package.Language);
                    CompilationUnitSyntax rootSyntax = ExportedRoot(parse.Root, exports);
                    IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parse, source).Symbols
                        .Where(symbol => exports.Contains(symbol.Name) && IsVisible(symbol.Kind))
                        .ToList().AsReadOnly();
                    if (rootSyntax.Members.Count == 0 && symbols.Count == 0) continue;
                    string relative = Path.GetRelativePath(moduleRoot, path).Replace('\\', '/');
                    string origin = "@" + module.Module.Id + "/" + module.Module.Version + "/" + relative;
                    documents.Add(new DdfWorkspaceDocument(Path.GetFullPath(path), origin, source,
                        rootSyntax, symbols));
                }

                foreach (string export in exports)
                {
                    if (!documents.Any(document => document.RelativePath.StartsWith(
                            "@" + module.Module.Id + "/" + module.Module.Version + "/",
                            StringComparison.Ordinal) && document.Symbols.Any(symbol => symbol.Name == export)))
                    {
                        issues.Add(DdfBuildIssue.Configuration("DDFB012",
                            "L'esportazione '" + export + "' non corrisponde a una dichiarazione del modulo '" +
                            module.Module.Id + "'.", Path.Combine(moduleRoot, DdfProjectManifest.FileName)));
                    }
                }
            }

            return new DdfModuleEditorIndex(documents.AsReadOnly(), issues.AsReadOnly());
        }

        public IReadOnlyList<CompilationUnitSyntax> GetExternalRoots(string currentPath)
        {
            return Documents.Where(document => !SamePath(document.Path, currentPath))
                .Select(document => document.Root).ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfDocumentSymbol> GetExternalSymbols(string currentPath)
        {
            return Documents.Where(document => !SamePath(document.Path, currentPath))
                .SelectMany(document => document.Symbols).ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfCompletionItem> GetCompletionItems(string currentPath)
        {
            return Documents.Where(document => !SamePath(document.Path, currentPath))
                .SelectMany(document => document.Symbols.Select(symbol =>
                    DdfCompletionService.CreateSymbolItem(symbol, document.RelativePath)))
                .ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfWorkspaceSymbol> FindDefinitions(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Array.Empty<DdfWorkspaceSymbol>();
            return Documents.SelectMany(document => document.Symbols
                    .Where(symbol => string.Equals(symbol.Name, name, StringComparison.Ordinal))
                    .Select(symbol => new DdfWorkspaceSymbol(document, symbol)))
                .ToList().AsReadOnly();
        }

        private static IEnumerable<string> ResolveSourcePaths(string root, DdfProjectManifest manifest,
            ICollection<DdfBuildIssue> issues)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pattern in manifest.Sources)
            {
                if (pattern.StartsWith("**/*.", StringComparison.Ordinal))
                {
                    string extension = Path.GetExtension(pattern.Substring(3));
                    foreach (string path in Directory.EnumerateFiles(root, "*" + extension,
                        SearchOption.AllDirectories))
                    {
                        if (!IsExcludedPath(root, manifest, path)) paths.Add(Path.GetFullPath(path));
                    }
                }
                else
                {
                    string path = Path.GetFullPath(Path.Combine(root, pattern));
                    if (File.Exists(path)) paths.Add(path);
                    else issues.Add(DdfBuildIssue.Configuration("DDFB007",
                        "Sorgente non trovato: " + pattern + ".", path));
                }
            }
            return paths.OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal);
        }

        private static bool IsExcludedPath(string root, DdfProjectManifest manifest, string path)
        {
            string fullPath = Path.GetFullPath(path);
            string output = Path.GetFullPath(Path.Combine(root, manifest.Output.Directory))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string store = Path.GetFullPath(Path.Combine(root, DdfProjectModuleStoreResolver.StoreDirectory))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(output, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(store, StringComparison.OrdinalIgnoreCase);
        }

        private static CompilationUnitSyntax ExportedRoot(CompilationUnitSyntax root,
            ISet<string> exports)
        {
            return new CompilationUnitSyntax(root.Members.Where(member => exports.Contains(MemberName(member)))
                .ToList().AsReadOnly(), root.Length);
        }

        private static string MemberName(MemberSyntax member) => member switch
        {
            FunctionDeclarationSyntax function => function.Name,
            StructDeclarationSyntax structure => structure.Name,
            GlobalStatementSyntax global when global.Statement is VariableDeclarationStatementSyntax variable => variable.Name,
            _ => string.Empty
        };

        private static bool IsVisible(DdfSymbolKind kind) =>
            kind == DdfSymbolKind.Library || kind == DdfSymbolKind.Structure ||
            kind == DdfSymbolKind.Function || kind == DdfSymbolKind.Variable;

        private static bool SamePath(string left, string right) =>
            !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
            string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
    }
}
