using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DDFLanguageEditor.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetEmittedArtifact
    {
        internal DdfDotNetEmittedArtifact(string moduleIdentity,
            string assemblyPath, string portablePdbPath, string sourceMapPath,
            string manifestPath, byte[] assemblyBytes, byte[] portablePdbBytes,
            byte[] sourceMapBytes, byte[] manifestBytes,
            DdfDotNetArtifactManifest manifest, DdfDotNetSourceMap sourceMap)
        {
            ModuleIdentity = moduleIdentity;
            AssemblyPath = assemblyPath;
            PortablePdbPath = portablePdbPath;
            SourceMapPath = sourceMapPath;
            ManifestPath = manifestPath;
            AssemblyBytes = assemblyBytes?.ToArray() ?? Array.Empty<byte>();
            PortablePdbBytes = portablePdbBytes?.ToArray() ?? Array.Empty<byte>();
            SourceMapBytes = sourceMapBytes?.ToArray() ?? Array.Empty<byte>();
            ManifestBytes = manifestBytes?.ToArray() ?? Array.Empty<byte>();
            Manifest = manifest;
            SourceMap = sourceMap;
        }

        public string ModuleIdentity { get; }
        public string AssemblyPath { get; }
        public string PortablePdbPath { get; }
        public string SourceMapPath { get; }
        public string ManifestPath { get; }
        public byte[] AssemblyBytes { get; }
        public byte[] PortablePdbBytes { get; }
        public byte[] SourceMapBytes { get; }
        public byte[] ManifestBytes { get; }
        public DdfDotNetArtifactManifest Manifest { get; }
        public DdfDotNetSourceMap SourceMap { get; }
    }

    public sealed class DdfDotNetArtifactEmissionResult
    {
        internal DdfDotNetArtifactEmissionResult(
            IEnumerable<DdfDotNetEmittedArtifact> artifacts,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Artifacts = (artifacts ?? Array.Empty<DdfDotNetEmittedArtifact>())
                .ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfDotNetEmittedArtifact> Artifacts { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Artifacts.Count > 0 && Issues.Count == 0;
    }

    /// <summary>
    /// Emits deterministic managed PE images and their versioned manifests in
    /// memory. It never loads assemblies and performs no filesystem writes.
    /// </summary>
    public static class DdfDotNetArtifactEmitter
    {
        public static DdfDotNetArtifactEmissionResult Emit(
            DdfDotNetCompilationPlan plan,
            DdfClrTypeMappingDefinition mapping)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            DdfDotNetCompilationGraphResult graph =
                DdfDotNetPlanCompilationBuilder.BuildGraph(plan, mapping);
            if (!graph.Success)
                return new DdfDotNetArtifactEmissionResult(null, graph.Issues);

            var issues = new List<DdfDotNetContractIssue>();
            var artifacts = new List<DdfDotNetEmittedArtifact>();
            var byIdentity = plan.WorkItems.ToDictionary(item => item.Identity,
                StringComparer.Ordinal);
            DdfDotNetBuildReceipt receipt =
                DdfDotNetBuildReceipt.FromPlan(plan);
            foreach (DdfDotNetCompilationWorkItem item in plan.WorkItems)
            {
                CSharpCompilation compilation = graph.Compilations[item.Identity];
                byte[] assemblyBytes;
                byte[] portablePdbBytes;
                using (var stream = new MemoryStream())
                using (var pdbStream = new MemoryStream())
                {
                    EmitResult emitted = compilation.Emit(stream, pdbStream,
                        options: new EmitOptions(
                            debugInformationFormat:
                                DebugInformationFormat.PortablePdb,
                            pdbFilePath: Path.GetFileName(
                                item.PortablePdbPath)));
                    foreach (Diagnostic diagnostic in emitted.Diagnostics.Where(
                                 value => value.Severity == DiagnosticSeverity.Error))
                        issues.Add(DdfDotNetDiagnosticMapper.Create("DDFC407",
                            item, diagnostic, item.Identity + ": "));
                    if (!emitted.Success || issues.Count != 0) break;
                    assemblyBytes = stream.ToArray();
                    portablePdbBytes = pdbStream.ToArray();
                }

                DdfDotNetSourceMap sourceMap =
                    DdfDotNetSourceMapBuilder.Create(item,
                        compilation.SyntaxTrees);
                byte[] sourceMapBytes;
                try
                {
                    sourceMapBytes = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeSourceMap(sourceMap));
                }
                catch (ArgumentException exception)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC408",
                        item.Identity + ": mappa sorgente emessa non valida: " +
                        exception.Message));
                    break;
                }

                var dependencies = new List<DdfDotNetDependencyDescriptor>();
                foreach (string identity in item.DependencyIdentities)
                {
                    DdfDotNetCompilationWorkItem dependency = byIdentity[identity];
                    dependencies.Add(new DdfDotNetDependencyDescriptor
                    {
                        ModuleId = dependency.Unit.ModuleId,
                        ModuleVersion = dependency.Unit.ModuleVersion,
                        CompilationFingerprint = dependency.Unit.Fingerprint,
                        AssemblyName = dependency.AssemblyName
                    });
                }
                DdfDotNetArtifactManifest manifest;
                byte[] manifestBytes;
                try
                {
                    manifest = DdfDotNetArtifactManifest.Create(item.Unit,
                        plan.Options.TargetFramework,
                        plan.Options.RuntimeAbiVersion, item.AssemblyName,
                        new[]
                        {
                            DdfDotNetPayloadDescriptor.FromBytes(
                                DdfDotNetPayloadKind.Assembly,
                                Path.GetFileName(item.AssemblyPath),
                                assemblyBytes),
                            DdfDotNetPayloadDescriptor.FromBytes(
                                DdfDotNetPayloadKind.PortablePdb,
                                Path.GetFileName(item.PortablePdbPath),
                                portablePdbBytes),
                            DdfDotNetPayloadDescriptor.FromBytes(
                                DdfDotNetPayloadKind.SourceMap,
                                Path.GetFileName(item.SourceMapPath),
                                sourceMapBytes)
                        }, dependencies,
                        plan.Options.RuntimeIdentifier,
                        RequiredHostCapabilities(item),
                        buildReceipt: receipt,
                        imports: RequiredImports(item, byIdentity));
                    manifestBytes = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeManifest(manifest));
                    IReadOnlyList<DdfDotNetContractIssue> bundleIssues =
                        DdfDotNetArtifactContract.ValidateBundle(manifest,
                            sourceMap);
                    if (bundleIssues.Count != 0)
                        throw new ArgumentException(string.Join(", ",
                            bundleIssues.Select(issue => issue.ToString())));
                }
                catch (ArgumentException exception)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC408",
                        item.Identity + ": manifest emesso non valido: " +
                        exception.Message));
                    break;
                }
                artifacts.Add(new DdfDotNetEmittedArtifact(item.Identity,
                    item.AssemblyPath, item.PortablePdbPath,
                    item.SourceMapPath, item.ManifestPath, assemblyBytes,
                    portablePdbBytes, sourceMapBytes, manifestBytes, manifest,
                    sourceMap));
            }
            return new DdfDotNetArtifactEmissionResult(
                issues.Count == 0 ? artifacts :
                    Array.Empty<DdfDotNetEmittedArtifact>(), issues);
        }

        private static IReadOnlyList<string> RequiredHostCapabilities(
            DdfDotNetCompilationWorkItem item) =>
            DdfDotNetHostCapabilityCollector.Collect(item);

        private static IReadOnlyList<DdfDotNetImportDescriptor> RequiredImports(
            DdfDotNetCompilationWorkItem item,
            IReadOnlyDictionary<string, DdfDotNetCompilationWorkItem> byIdentity)
        {
            var imports = new List<DdfDotNetImportDescriptor>();
            foreach (string dependencyIdentity in item.DependencyIdentities)
            {
                DdfDotNetCompilationWorkItem dependency =
                    byIdentity[dependencyIdentity];
                imports.AddRange(dependency.Unit.PublicInterface.Exports.Select(
                    exported => new DdfDotNetImportDescriptor
                    {
                        ModuleId = dependency.Unit.ModuleId,
                        ModuleVersion = dependency.Unit.ModuleVersion,
                        Name = exported.Name,
                        Kind = exported.Kind,
                        Signature = exported.Signature
                    }));
            }
            return imports.OrderBy(imported => imported.ModuleId,
                    StringComparer.Ordinal)
                .ThenBy(imported => imported.ModuleVersion,
                    StringComparer.Ordinal)
                .ThenBy(imported => imported.Name, StringComparer.Ordinal)
                .ThenBy(imported => imported.Kind).ToList().AsReadOnly();
        }
    }
}
