using System;
using System.Collections.Generic;
using System.Linq;
using DDFLanguageEditor.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetCompilationBuildResult
    {
        internal DdfDotNetCompilationBuildResult(CSharpCompilation compilation,
            DdfDotNetSyntaxGenerationResult syntax,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Compilation = compilation;
            Syntax = syntax;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        internal CSharpCompilation Compilation { get; }
        public DdfDotNetSyntaxGenerationResult Syntax { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public int ValidatedSyntaxTreeCount => Compilation?.SyntaxTrees.Count() ?? 0;
        public bool Success => Compilation != null && Syntax?.Success == true &&
            Issues.Count == 0;
    }

    /// <summary>
    /// Builds and semantically validates an immutable Roslyn compilation. This
    /// boundary deliberately does not expose the compilation or invoke Emit.
    /// </summary>
    public static class DdfDotNetCompilationBuilder
    {
        public static DdfDotNetCompilationBuildResult Create(
            DdfDotNetCompilationWorkItem workItem,
            DdfClrTypeMappingDefinition mapping)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            if (workItem.ReferenceSet == null)
                return new DdfDotNetCompilationBuildResult(null, null,
                    new[] { new DdfDotNetContractIssue("DDFC404",
                        "Reference set esplicito richiesto per la validazione Roslyn.") });
            DdfDotNetSyntaxGenerationResult syntax =
                DdfDotNetSyntaxTreeGenerator.Generate(workItem, mapping);
            if (!syntax.Success)
                return new DdfDotNetCompilationBuildResult(null, syntax,
                    syntax.Issues);
            CSharpCompilation compilation = CSharpCompilation.Create(
                workItem.AssemblyName, syntax.SyntaxTrees,
                workItem.ReferenceSet.CreateMetadataReferences(),
                DdfDotNetCompilerProfile.LibraryOptions);
            var issues = compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => DdfDotNetDiagnosticMapper.Create(
                    "DDFC403", workItem, diagnostic,
                    "Validazione semantica fallita: "))
                .ToList();
            return new DdfDotNetCompilationBuildResult(
                issues.Count == 0 ? compilation : null, syntax, issues);
        }
    }

    public sealed class DdfDotNetPlanCompilationBuildResult
    {
        internal DdfDotNetPlanCompilationBuildResult(
            IEnumerable<string> compiledModuleIdentities,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            CompiledModuleIdentities = (compiledModuleIdentities ??
                    Array.Empty<string>()).ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public IReadOnlyList<string> CompiledModuleIdentities { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => CompiledModuleIdentities.Count > 0 &&
            Issues.Count == 0;
    }

    internal sealed class DdfDotNetCompilationGraphResult
    {
        internal DdfDotNetCompilationGraphResult(
            IReadOnlyDictionary<string, CSharpCompilation> compilations,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Compilations = compilations ??
                new Dictionary<string, CSharpCompilation>(StringComparer.Ordinal);
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        internal IReadOnlyDictionary<string, CSharpCompilation> Compilations { get; }
        internal IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        internal bool Success => Compilations.Count > 0 && Issues.Count == 0;
    }

    /// <summary>
    /// Validates every module as a separate Roslyn compilation in topological
    /// order. Dependencies are compilation references kept in memory; no PE or
    /// PDB is emitted and no assembly is loaded.
    /// </summary>
    public static class DdfDotNetPlanCompilationBuilder
    {
        public static DdfDotNetPlanCompilationBuildResult Create(
            DdfDotNetCompilationPlan plan,
            DdfClrTypeMappingDefinition mapping)
        {
            DdfDotNetCompilationGraphResult graph = BuildGraph(plan, mapping);
            return new DdfDotNetPlanCompilationBuildResult(
                graph.Success ? plan.WorkItems.Select(item => item.Identity) :
                    Array.Empty<string>(), graph.Issues);
        }

        internal static DdfDotNetCompilationGraphResult BuildGraph(
            DdfDotNetCompilationPlan plan,
            DdfClrTypeMappingDefinition mapping)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            var issues = new List<DdfDotNetContractIssue>();
            if (plan.Options.ReferenceSet == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC404",
                    "Reference set esplicito richiesto per il piano Roslyn."));
                return new DdfDotNetCompilationGraphResult(null, issues);
            }
            DdfDotNetLinkNameTable links = DdfDotNetLinkNameTable.Create(
                plan.WorkItems);
            var compilations = new Dictionary<string, CSharpCompilation>(
                StringComparer.Ordinal);
            foreach (DdfDotNetCompilationWorkItem item in plan.WorkItems)
            {
                DdfDotNetSyntaxGenerationResult syntax =
                    DdfDotNetSyntaxTreeGenerator.Generate(item, mapping, links);
                if (!syntax.Success)
                {
                    issues.AddRange(syntax.Issues);
                    break;
                }
                var references = plan.Options.ReferenceSet
                    .CreateMetadataReferences().ToList();
                foreach (string dependency in item.DependencyIdentities)
                {
                    if (!compilations.TryGetValue(dependency,
                            out CSharpCompilation dependencyCompilation))
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC405",
                            "Compilazione dipendente non disponibile: " +
                            dependency + "."));
                        break;
                    }
                    references.Add(dependencyCompilation.ToMetadataReference());
                }
                if (issues.Count != 0) break;
                CSharpCompilation compilation = CSharpCompilation.Create(
                    item.AssemblyName, syntax.SyntaxTrees, references,
                    DdfDotNetCompilerProfile.LibraryOptions);
                foreach (Diagnostic diagnostic in compilation.GetDiagnostics()
                             .Where(value => value.Severity ==
                                 DiagnosticSeverity.Error))
                    issues.Add(DdfDotNetDiagnosticMapper.Create("DDFC403",
                        item, diagnostic, item.Identity + ": "));
                if (issues.Count != 0) break;
                compilations.Add(item.Identity, compilation);
            }
            return new DdfDotNetCompilationGraphResult(
                issues.Count == 0 ? compilations : null, issues);
        }
    }
}
