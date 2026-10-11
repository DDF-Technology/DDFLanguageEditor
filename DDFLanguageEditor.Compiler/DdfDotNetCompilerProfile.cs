using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetReferenceDescriptor
    {
        private readonly byte[] image;

        private DdfDotNetReferenceDescriptor(string path, string assemblyName,
            string sha256, long size, byte[] image = null)
        {
            Path = path;
            AssemblyName = assemblyName;
            Sha256 = sha256;
            Size = size;
            this.image = image?.ToArray();
        }

        public string Path { get; }
        public string AssemblyName { get; }
        public string Sha256 { get; }
        public long Size { get; }

        public static DdfDotNetReferenceDescriptor FromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path))
                throw new ArgumentException("È richiesto un percorso assoluto esplicito.",
                    nameof(path));
            string fullPath = System.IO.Path.GetFullPath(path);
            if (!File.Exists(fullPath) ||
                !string.Equals(System.IO.Path.GetExtension(fullPath), ".dll",
                    StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Reference assembly non trovata.", fullPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            using SHA256 sha = SHA256.Create();
            string hash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
            string assemblyName;
            try
            {
                assemblyName = System.Reflection.AssemblyName
                    .GetAssemblyName(fullPath).FullName;
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is FileLoadException)
            {
                throw new ArgumentException("Il riferimento non è un assembly .NET valido.",
                    nameof(path), exception);
            }
            return new DdfDotNetReferenceDescriptor(fullPath.Replace('\\', '/'),
                assemblyName, hash, bytes.LongLength);
        }

        internal static DdfDotNetReferenceDescriptor FromImage(
            string logicalPath, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(logicalPath))
                throw new ArgumentException("È richiesto un nome logico.",
                    nameof(logicalPath));
            byte[] image = (bytes ?? throw new ArgumentNullException(
                nameof(bytes))).ToArray();
            if (image.Length == 0)
                throw new ArgumentException("L'immagine assembly è vuota.",
                    nameof(bytes));
            string assemblyName = ReadAssemblyIdentity(image);
            string hash = Convert.ToHexString(SHA256.HashData(image))
                .ToLowerInvariant();
            return new DdfDotNetReferenceDescriptor(logicalPath,
                assemblyName, hash, image.LongLength, image);
        }

        internal MetadataReference CreateMetadataReference() => image == null
            ? MetadataReference.CreateFromFile(Path)
            : MetadataReference.CreateFromImage(image);

        private static string ReadAssemblyIdentity(byte[] bytes)
        {
            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata)
                    throw new BadImageFormatException(
                        "L'immagine non contiene metadati .NET.");
                MetadataReader metadata = pe.GetMetadataReader();
                if (!metadata.IsAssembly)
                    throw new BadImageFormatException(
                        "L'immagine .NET non descrive un assembly.");
                AssemblyDefinition definition = metadata.GetAssemblyDefinition();
                var name = new AssemblyName
                {
                    Name = metadata.GetString(definition.Name),
                    Version = definition.Version,
                    CultureName = definition.Culture.IsNil ? string.Empty :
                        metadata.GetString(definition.Culture),
                    Flags = (AssemblyNameFlags)definition.Flags
                };
                if (!definition.PublicKey.IsNil)
                    name.SetPublicKey(metadata.GetBlobBytes(
                        definition.PublicKey));
                else
                    name.SetPublicKeyToken(Array.Empty<byte>());
                return name.FullName ?? throw new BadImageFormatException(
                    "Identità assembly assente.");
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is InvalidOperationException)
            {
                throw new ArgumentException(
                    "Il payload non è un assembly .NET gestito valido.",
                    nameof(bytes), exception);
            }
        }
    }

    public sealed class DdfDotNetReferenceSet
    {
        private DdfDotNetReferenceSet(IEnumerable<DdfDotNetReferenceDescriptor> references)
        {
            References = references.OrderBy(item => item.AssemblyName,
                StringComparer.Ordinal).ToList().AsReadOnly();
            Fingerprint = ComputeFingerprint();
        }

        public IReadOnlyList<DdfDotNetReferenceDescriptor> References { get; }
        public string Fingerprint { get; }

        public static DdfDotNetReferenceSet Create(IEnumerable<string> explicitPaths)
        {
            var references = (explicitPaths ?? throw new ArgumentNullException(
                    nameof(explicitPaths)))
                .Select(DdfDotNetReferenceDescriptor.FromFile).ToList();
            if (references.Count == 0)
                throw new ArgumentException("È richiesto almeno un reference assembly.",
                    nameof(explicitPaths));
            if (references.Select(item => item.AssemblyName)
                    .Distinct(StringComparer.Ordinal).Count() != references.Count)
                throw new ArgumentException("Reference assembly duplicata.",
                    nameof(explicitPaths));
            return new DdfDotNetReferenceSet(references);
        }

        internal static DdfDotNetReferenceSet Create(
            IEnumerable<string> explicitPaths,
            IEnumerable<DdfDotNetReferenceDescriptor> inMemoryReferences)
        {
            var references = (explicitPaths ?? Array.Empty<string>())
                .Select(DdfDotNetReferenceDescriptor.FromFile)
                .Concat(inMemoryReferences ?? Array.Empty<
                    DdfDotNetReferenceDescriptor>()).ToList();
            if (references.Count == 0)
                throw new ArgumentException(
                    "È richiesto almeno un reference assembly.",
                    nameof(explicitPaths));
            if (references.Select(item => item.AssemblyName)
                    .Distinct(StringComparer.Ordinal).Count() != references.Count)
                throw new ArgumentException("Reference assembly duplicata.",
                    nameof(explicitPaths));
            return new DdfDotNetReferenceSet(references);
        }

        internal IReadOnlyList<MetadataReference> CreateMetadataReferences() =>
            References.Select(item => item.CreateMetadataReference())
                .ToList().AsReadOnly();

        private string ComputeFingerprint()
        {
            var value = new StringBuilder();
            foreach (DdfDotNetReferenceDescriptor reference in References)
                value.Append(reference.AssemblyName).Append(':')
                    .Append(reference.Sha256).Append(':').Append(reference.Size)
                    .Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }
    }

    public static class DdfDotNetReferencePackResolver
    {
        public static DdfDotNetReferenceSet Resolve(string referencePackDirectory,
            string runtimeAssemblyPath,
            IEnumerable<string> additionalAssemblyPaths = null)
        {
            if (string.IsNullOrWhiteSpace(referencePackDirectory) ||
                !Path.IsPathRooted(referencePackDirectory))
                throw new ArgumentException(
                    "È richiesta una directory reference pack assoluta.",
                    nameof(referencePackDirectory));
            string directory = Path.GetFullPath(referencePackDirectory);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(
                    "Reference pack non trovato: " + directory + ".");
            var paths = Directory.GetFiles(directory, "*.dll",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count == 0)
                throw new ArgumentException(
                    "La directory non contiene reference assembly .NET.",
                    nameof(referencePackDirectory));
            paths.Add(runtimeAssemblyPath);
            paths.AddRange(additionalAssemblyPaths ?? Array.Empty<string>());
            return DdfDotNetReferenceSet.Create(paths);
        }
    }

    public static class DdfDotNetCompilerProfile
    {
        public const string FormatVersion = "1.0";
        public const LanguageVersion CSharpLanguageVersion = LanguageVersion.CSharp14;

        public static string RoslynVersion => typeof(CSharpCompilation).Assembly
            .GetName().Version?.ToString() ?? string.Empty;
        public static CSharpParseOptions ParseOptions { get; } =
            new CSharpParseOptions(CSharpLanguageVersion,
                DocumentationMode.None, SourceCodeKind.Regular);
        public static CSharpCompilationOptions LibraryOptions { get; } =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                checkOverflow: true,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true,
                concurrentBuild: false,
                platform: Platform.AnyCpu,
                metadataImportOptions: MetadataImportOptions.Public);
    }
}
