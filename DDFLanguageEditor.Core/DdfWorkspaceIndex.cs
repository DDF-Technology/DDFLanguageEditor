using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfWorkspaceDocument
    {
        internal DdfWorkspaceDocument(string path, string relativePath, string source, CompilationUnitSyntax root, IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            Path = path;
            RelativePath = relativePath;
            Source = source;
            Root = root;
            Symbols = symbols;
        }

        public string Path { get; }
        public string RelativePath { get; }
        public string Source { get; }
        public CompilationUnitSyntax Root { get; }
        public IReadOnlyList<DdfDocumentSymbol> Symbols { get; }
    }

    public sealed class DdfWorkspaceSymbol
    {
        internal DdfWorkspaceSymbol(DdfWorkspaceDocument document, DdfDocumentSymbol symbol)
        {
            Document = document;
            Symbol = symbol;
        }

        public DdfWorkspaceDocument Document { get; }
        public DdfDocumentSymbol Symbol { get; }
    }

    public sealed class DdfWorkspaceIndex
    {
        private readonly IReadOnlyList<string> fileExtensions;

        private DdfWorkspaceIndex(string rootPath, IReadOnlyList<DdfWorkspaceDocument> documents,
            IReadOnlyList<string> fileExtensions)
        {
            RootPath = rootPath;
            Documents = documents;
            this.fileExtensions = fileExtensions;
        }

        public string RootPath { get; }
        public IReadOnlyList<DdfWorkspaceDocument> Documents { get; }

        public static DdfWorkspaceIndex Load(string rootPath)
        {
            return Load(rootPath, CancellationToken.None);
        }

        public static DdfWorkspaceIndex Load(string rootPath, CancellationToken cancellationToken)
        {
            return Load(rootPath, cancellationToken, DdfLanguageCatalog.DefaultPackage.FileExtensions);
        }

        public static DdfWorkspaceIndex Load(string rootPath, CancellationToken cancellationToken,
            IEnumerable<string> fileExtensions)
        {
            string normalizedRoot = NormalizeRoot(rootPath);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> normalizedExtensions = NormalizeExtensions(fileExtensions);
            var documents = new List<DdfWorkspaceDocument>();
            var paths = new List<string>();
            foreach (string extension in normalizedExtensions)
            {
                foreach (string path in Directory.EnumerateFiles(
                    normalizedRoot, "*" + extension, SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    paths.Add(path);
                }
            }
            paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(path);
                documents.Add(CreateDocument(normalizedRoot, fullPath, DdfDocumentFile.Load(fullPath)));
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new DdfWorkspaceIndex(normalizedRoot, documents.AsReadOnly(), normalizedExtensions);
        }

        public DdfWorkspaceIndex WithDocument(string path, string source)
        {
            return WithDocument(path, source, DdfParser.Parse(source ?? string.Empty).Root);
        }

        public DdfWorkspaceIndex WithDocumentSource(string path, string source)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            string fullPath = Path.GetFullPath(path);
            DdfWorkspaceDocument existing = Documents.FirstOrDefault(document =>
                string.Equals(document.Path, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing == null) return this;

            var updated = new DdfWorkspaceDocument(existing.Path, existing.RelativePath, source ?? string.Empty,
                existing.Root, existing.Symbols);
            var documents = Documents.Select(document => ReferenceEquals(document, existing) ? updated : document)
                .ToList();
            return new DdfWorkspaceIndex(RootPath, documents.AsReadOnly(), fileExtensions);
        }

        public DdfWorkspaceIndex WithDocument(string path, string source, CompilationUnitSyntax root)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (root == null) throw new ArgumentNullException(nameof(root));
            string fullPath = Path.GetFullPath(path);
            if (!ContainsPath(fullPath) || !fileExtensions.Contains(
                Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            {
                return this;
            }

            var documents = Documents
                .Where(document => !string.Equals(document.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            documents.Add(CreateDocument(RootPath, fullPath, source ?? string.Empty, root));
            documents.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
            return new DdfWorkspaceIndex(RootPath, documents.AsReadOnly(), fileExtensions);
        }

        private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            var result = (extensions ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.StartsWith(".", StringComparison.Ordinal) ? item : "." + item)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (result.Count == 0) result.Add(".ddf");
            return result.AsReadOnly();
        }

        public IReadOnlyList<DdfWorkspaceSymbol> FindDefinitions(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<DdfWorkspaceSymbol>().AsReadOnly();
            var result = new List<DdfWorkspaceSymbol>();
            foreach (DdfWorkspaceDocument document in Documents)
            {
                foreach (DdfDocumentSymbol symbol in document.Symbols)
                {
                    if (IsWorkspaceVisible(symbol.Kind) && string.Equals(symbol.Name, name, StringComparison.Ordinal))
                    {
                        result.Add(new DdfWorkspaceSymbol(document, symbol));
                    }
                }
            }

            return result.AsReadOnly();
        }

        public IReadOnlyList<DdfDocumentSymbol> GetExternalSymbols(string currentPath)
        {
            return Documents
                .Where(document => !string.Equals(document.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                .SelectMany(document => document.Symbols)
                .Where(symbol => IsWorkspaceVisible(symbol.Kind))
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<CompilationUnitSyntax> GetExternalRoots(string currentPath)
        {
            return Documents
                .Where(document => !string.Equals(document.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                .Select(document => document.Root)
                .ToList()
                .AsReadOnly();
        }

        public bool ContainsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string fullPath = Path.GetFullPath(path);
            string prefix = RootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? RootPath
                : RootPath + Path.DirectorySeparatorChar;
            return string.Equals(fullPath, RootPath, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static DdfWorkspaceDocument CreateDocument(string rootPath, string path, string source)
        {
            DdfParseResult parseResult = DdfParser.Parse(source);
            return CreateDocument(rootPath, path, source, parseResult.Root,
                DdfSymbolIndex.Create(parseResult, source).Symbols);
        }

        private static DdfWorkspaceDocument CreateDocument(string rootPath, string path, string source, CompilationUnitSyntax root)
        {
            DdfParseResult parseResult = DdfParser.Parse(source);
            return CreateDocument(rootPath, path, source, root,
                DdfSymbolIndex.Create(parseResult, source).Symbols);
        }

        private static DdfWorkspaceDocument CreateDocument(string rootPath, string path, string source,
            CompilationUnitSyntax root, IReadOnlyList<DdfDocumentSymbol> symbols)
        {
            string prefix = rootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;
            string relativePath = path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length)
                : Path.GetFileName(path);
            return new DdfWorkspaceDocument(path, relativePath, source, root, symbols);
        }

        private static string NormalizeRoot(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("A workspace root is required.", nameof(rootPath));
            string fullPath = Path.GetFullPath(rootPath);
            string pathRoot = Path.GetPathRoot(fullPath);
            if (!string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);
            return fullPath;
        }

        private static bool IsWorkspaceVisible(DdfSymbolKind kind)
        {
            return kind == DdfSymbolKind.Library || kind == DdfSymbolKind.Structure ||
                   kind == DdfSymbolKind.Function || kind == DdfSymbolKind.Variable;
        }
    }
}
