using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public enum DdfCompilationExportKind
    {
        Function,
        Structure,
        Global
    }

    /// <summary>
    /// Exact, immutable source input of one compilation unit. Source text is an
    /// in-memory input and is not a persistent compiled artifact.
    /// </summary>
    public sealed class DdfCompilationSourceSnapshot
    {
        internal DdfCompilationSourceSnapshot(string relativePath, string sourceText,
            bool fromUnsavedBuffer)
        {
            RelativePath = Normalize(relativePath);
            SourceText = sourceText ?? string.Empty;
            FromUnsavedBuffer = fromUnsavedBuffer;
            Sha256 = Hash(SourceText);
        }

        public string RelativePath { get; }
        public string SourceText { get; }
        public string Sha256 { get; }
        public bool FromUnsavedBuffer { get; }

        private static string Normalize(string path) =>
            (path ?? string.Empty).Replace('\\', '/');

        private static string Hash(string value)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
        }
    }

    /// <summary>
    /// IR associated with one source snapshot. It remains an in-memory compiler
    /// input; serializers and emitters must define their own versioned format.
    /// </summary>
    public sealed class DdfCompilationIrDocument
    {
        internal DdfCompilationIrDocument(string relativePath, DdfIrProgram program)
        {
            RelativePath = (relativePath ?? string.Empty).Replace('\\', '/');
            Program = program ?? throw new ArgumentNullException(nameof(program));
        }

        public string RelativePath { get; }
        public string IrFormatVersion => DdfIrProgram.FormatVersion;
        public DdfIrProgram Program { get; }
    }

    public sealed class DdfCompilationExport
    {
        internal DdfCompilationExport(string name, DdfCompilationExportKind kind,
            string signature, string relativePath, int start, int length)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            Signature = signature ?? string.Empty;
            RelativePath = (relativePath ?? string.Empty).Replace('\\', '/');
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
        }

        public string Name { get; }
        public DdfCompilationExportKind Kind { get; }
        public string Signature { get; }
        public string RelativePath { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfCompilationDependency
    {
        internal DdfCompilationDependency(string moduleId, string moduleVersion)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            ModuleVersion = moduleVersion ?? throw new ArgumentNullException(nameof(moduleVersion));
        }

        public string ModuleId { get; }
        public string ModuleVersion { get; }
        public string Identity => ModuleId + "@" + ModuleVersion;
    }

    /// <summary>
    /// Public contract offered by one module to direct consumers. It contains no
    /// implementation IR and no compiler-specific CLR or Roslyn symbols.
    /// </summary>
    public sealed class DdfCompilationInterface
    {
        internal DdfCompilationInterface(string moduleId, string moduleVersion,
            IEnumerable<DdfCompilationExport> exports)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            ModuleVersion = moduleVersion ?? throw new ArgumentNullException(nameof(moduleVersion));
            Exports = (exports ?? Array.Empty<DdfCompilationExport>())
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        public string ModuleId { get; }
        public string ModuleVersion { get; }
        public string Identity => ModuleId + "@" + ModuleVersion;
        public IReadOnlyList<DdfCompilationExport> Exports { get; }
    }

    /// <summary>
    /// Backend-neutral compilation unit. One DDF module is one compilation unit;
    /// source, structural IR and exported interface are separate immutable layers.
    /// This object is never itself a persisted assembly or package.
    /// </summary>
    public sealed class DdfCompilationUnit
    {
        public const string ContractVersion = "1.0";

        internal DdfCompilationUnit(string languageId, string languagePackageVersion,
            DdfCompilationInterface publicInterface,
            IEnumerable<DdfCompilationDependency> dependencies,
            IEnumerable<DdfCompilationSourceSnapshot> sources,
            IEnumerable<DdfCompilationIrDocument> intermediateRepresentation,
            DdfBoundCompilationUnit boundIntermediateRepresentation)
        {
            LanguageId = languageId ?? throw new ArgumentNullException(nameof(languageId));
            LanguagePackageVersion = languagePackageVersion ??
                throw new ArgumentNullException(nameof(languagePackageVersion));
            PublicInterface = publicInterface ??
                throw new ArgumentNullException(nameof(publicInterface));
            Dependencies = (dependencies ?? Array.Empty<DdfCompilationDependency>())
                .OrderBy(item => item.ModuleId, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            Sources = (sources ?? Array.Empty<DdfCompilationSourceSnapshot>())
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            IntermediateRepresentation = (intermediateRepresentation ??
                    Array.Empty<DdfCompilationIrDocument>())
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToList().AsReadOnly();
            if (Sources.Count == 0 || Sources.Count != IntermediateRepresentation.Count)
                throw new ArgumentException(
                    "Ogni unità di compilazione richiede sorgenti e documenti IR corrispondenti.");
            if (!Sources.Select(item => item.RelativePath).SequenceEqual(
                    IntermediateRepresentation.Select(item => item.RelativePath),
                    StringComparer.Ordinal))
                throw new ArgumentException(
                    "I documenti IR devono corrispondere esattamente agli snapshot sorgente.");
            BoundIntermediateRepresentation = boundIntermediateRepresentation ??
                throw new ArgumentNullException(nameof(boundIntermediateRepresentation));
            if (BoundIntermediateRepresentation.ModuleIdentity != PublicInterface.Identity ||
                !Sources.Select(item => item.RelativePath).SequenceEqual(
                    BoundIntermediateRepresentation.Documents.Select(item => item.RelativePath),
                    StringComparer.Ordinal))
                throw new ArgumentException(
                    "La IR bound deve corrispondere esattamente all'unità di compilazione.");
            Fingerprint = ComputeFingerprint();
        }

        public string Version => ContractVersion;
        public string ModuleId => PublicInterface.ModuleId;
        public string ModuleVersion => PublicInterface.ModuleVersion;
        public string Identity => PublicInterface.Identity;
        public string LanguageId { get; }
        public string LanguagePackageVersion { get; }
        public string IrFormatVersion => DdfIrProgram.FormatVersion;
        public string BoundIrFormatVersion => DdfBoundCompilationUnit.FormatVersion;
        public DdfCompilationInterface PublicInterface { get; }
        public IReadOnlyList<DdfCompilationDependency> Dependencies { get; }
        public IReadOnlyList<DdfCompilationSourceSnapshot> Sources { get; }
        public IReadOnlyList<DdfCompilationIrDocument> IntermediateRepresentation { get; }
        public DdfBoundCompilationUnit BoundIntermediateRepresentation { get; }
        public string Fingerprint { get; }

        private string ComputeFingerprint()
        {
            var value = new StringBuilder();
            value.Append(ContractVersion).Append('\n')
                .Append(Identity).Append('\n')
                .Append(LanguageId).Append('@').Append(LanguagePackageVersion).Append('\n')
                .Append(DdfIrProgram.FormatVersion).Append('\n')
                .Append(DdfBoundCompilationUnit.FormatVersion).Append(':')
                .Append(BoundIntermediateRepresentation.Fingerprint).Append('\n');
            foreach (DdfCompilationDependency dependency in Dependencies)
                value.Append("dep:").Append(dependency.Identity).Append('\n');
            foreach (DdfCompilationExport export in PublicInterface.Exports)
                value.Append("export:").Append(export.Kind).Append(':')
                    .Append(export.Name).Append(':').Append(export.Signature).Append('\n');
            foreach (DdfCompilationSourceSnapshot source in Sources)
                value.Append("source:").Append(source.RelativePath).Append(':')
                    .Append(source.Sha256).Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
        }
    }

    internal static class DdfCompilationUnitFactory
    {
        internal static DdfCompilationUnit Create(string languageId,
            string languagePackageVersion, DdfBuildModuleInput module,
            IReadOnlyList<DdfBuildSource> sources,
            IReadOnlyList<DdfIrModule> irModules,
            IReadOnlyList<DdfIrModule> visibleIrModules,
            IReadOnlyDictionary<string, DdfTypeCheckResult> typeResults,
            DdfLanguageDefinition language, DdfHostCatalogSnapshot hostCatalog)
        {
            var sourceSnapshots = sources.Select(source =>
                new DdfCompilationSourceSnapshot(source.RelativePath, source.Source,
                    source.FromUnsavedBuffer)).ToList();
            var irDocuments = sources.Select(source =>
            {
                DdfIrModule ir = irModules.Single(item =>
                    string.Equals(item.DocumentPath, source.Path,
                        StringComparison.OrdinalIgnoreCase));
                return new DdfCompilationIrDocument(source.RelativePath, ir.Program);
            }).ToList();
            var exports = new List<DdfCompilationExport>();
            foreach (string name in module.Manifest.Module.Exports)
            {
                DdfCompilationExport export = FindExport(name, sources, irModules);
                if (export != null) exports.Add(export);
            }
            var publicInterface = new DdfCompilationInterface(
                module.Manifest.Module.Id, module.Manifest.Module.Version, exports);
            DdfBoundCompilationUnit bound = DdfBoundIrBuilder.Create(
                module.Manifest.Module.Id, module.Manifest.Module.Version, sources,
                irModules, visibleIrModules, typeResults, language, hostCatalog);
            return new DdfCompilationUnit(languageId, languagePackageVersion,
                publicInterface, module.Manifest.Dependencies.Select(item =>
                    new DdfCompilationDependency(item.Id, item.Version)),
                sourceSnapshots, irDocuments, bound);
        }

        private static DdfCompilationExport FindExport(string name,
            IReadOnlyList<DdfBuildSource> sources,
            IReadOnlyList<DdfIrModule> irModules)
        {
            foreach (DdfBuildSource source in sources)
            {
                DdfIrModule ir = irModules.FirstOrDefault(item =>
                    string.Equals(item.DocumentPath, source.Path,
                        StringComparison.OrdinalIgnoreCase));
                if (ir == null) continue;
                foreach (DdfIrMember member in ir.Program.Members)
                {
                    if (member is DdfIrFunction function && function.Name == name)
                        return new DdfCompilationExport(name,
                            DdfCompilationExportKind.Function,
                            FunctionSignature(function, source.Source),
                            source.RelativePath, function.Start, function.Length);
                    if (member is DdfIrStructure structure && structure.Name == name)
                        return new DdfCompilationExport(name,
                            DdfCompilationExportKind.Structure,
                            StructureSignature(structure, source.Source),
                            source.RelativePath, structure.Start, structure.Length);
                    if (member is DdfIrGlobal global &&
                        global.Statement is DdfIrVariableDeclaration variable &&
                        variable.Name == name)
                        return new DdfCompilationExport(name,
                            DdfCompilationExportKind.Global,
                            name + ":" + TypeSignature(variable.Type, source.Source),
                            source.RelativePath, variable.Start, variable.Length);
                }
            }
            return null;
        }

        private static string FunctionSignature(DdfIrFunction function, string source) =>
            function.Name + "(" + string.Join(",", function.Parameters.Select(parameter =>
                TypeSignature(parameter.Type, source))) + ")->" +
            TypeSignature(function.ReturnType, source);

        private static string StructureSignature(DdfIrStructure structure, string source) =>
            structure.Name + "{" + string.Join(",", structure.Fields.Select(field =>
                field.Name + ":" + TypeSignature(field.Type, source))) + "}";

        private static string TypeSignature(DdfIrType type, string source)
        {
            if (type == null) return string.Empty;
            if (type.Length > 0 && type.Start >= 0 && source != null &&
                type.Start + type.Length <= source.Length)
                return new string(source.Substring(type.Start, type.Length)
                    .Where(character => !char.IsWhiteSpace(character)).ToArray());
            return type.Name + string.Concat(type.ArrayLengths.Select(item => "[]"));
        }
    }
}
