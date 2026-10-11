using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetCompilationOptions
    {
        public DdfDotNetCompilationOptions(string targetFramework,
            string runtimeAbiVersion, string runtimeIdentifier = null,
            DdfDotNetReferenceSet referenceSet = null)
        {
            if (string.IsNullOrWhiteSpace(targetFramework))
                throw new ArgumentException("Target Framework richiesto.",
                    nameof(targetFramework));
            if (!Version.TryParse(runtimeAbiVersion, out _))
                throw new ArgumentException("Versione runtime ABI richiesta.",
                    nameof(runtimeAbiVersion));
            if (!string.IsNullOrEmpty(runtimeIdentifier) && !Regex.IsMatch(
                    runtimeIdentifier, "^[A-Za-z0-9][A-Za-z0-9.-]*$"))
                throw new ArgumentException("Runtime Identifier non valido.",
                    nameof(runtimeIdentifier));
            TargetFramework = targetFramework;
            RuntimeAbiVersion = runtimeAbiVersion;
            RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
            ReferenceSet = referenceSet;
        }

        public string TargetFramework { get; }
        public string RuntimeAbiVersion { get; }
        public string RuntimeIdentifier { get; }
        public DdfDotNetReferenceSet ReferenceSet { get; }
    }

    public sealed class DdfDotNetCompilationWorkItem
    {
        internal DdfDotNetCompilationWorkItem(DdfCompilationUnit unit,
            bool isEntryModule, string assemblyName, string outputDirectory,
            IEnumerable<string> dependencyIdentities, DdfDotNetNameMap generatedNames,
            DdfDotNetReferenceSet referenceSet)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            IsEntryModule = isEntryModule;
            AssemblyName = assemblyName ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            DependencyIdentities = (dependencyIdentities ?? Array.Empty<string>())
                .OrderBy(item => item, StringComparer.Ordinal).ToList().AsReadOnly();
            GeneratedNames = generatedNames ??
                throw new ArgumentNullException(nameof(generatedNames));
            ReferenceSet = referenceSet;
        }

        public DdfCompilationUnit Unit { get; }
        public DdfBoundCompilationUnit BoundUnit =>
            Unit.BoundIntermediateRepresentation;
        public string Identity => Unit.Identity;
        public bool IsEntryModule { get; }
        public bool IsLibrary => !IsEntryModule;
        public string AssemblyName { get; }
        public string OutputDirectory { get; }
        public string AssemblyPath => OutputDirectory + "/" + AssemblyName + ".dll";
        public string PortablePdbPath => OutputDirectory + "/" + AssemblyName + ".pdb";
        public string SourceMapPath => OutputDirectory + "/" + AssemblyName +
            ".ddfmap.json";
        public string ManifestPath => OutputDirectory + "/" + AssemblyName +
            ".ddfartifact.json";
        public IReadOnlyList<string> DependencyIdentities { get; }
        public DdfDotNetNameMap GeneratedNames { get; }
        public DdfDotNetReferenceSet ReferenceSet { get; }
    }

    public sealed class DdfDotNetCompilationPlan
    {
        internal DdfDotNetCompilationPlan(DdfDotNetCompilationOptions options,
            IEnumerable<DdfDotNetCompilationWorkItem> workItems,
            string entryModuleIdentity, string preparationFingerprint)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            WorkItems = (workItems ?? Array.Empty<DdfDotNetCompilationWorkItem>())
                .ToList().AsReadOnly();
            EntryModuleIdentity = entryModuleIdentity ?? string.Empty;
            PreparationFingerprint = preparationFingerprint ?? string.Empty;
            Fingerprint = ComputeFingerprint();
        }

        public const string FormatVersion = "1.0";
        public DdfDotNetCompilationOptions Options { get; }
        public IReadOnlyList<DdfDotNetCompilationWorkItem> WorkItems { get; }
        public string EntryModuleIdentity { get; }
        public string PreparationFingerprint { get; }
        public string Fingerprint { get; }

        private string ComputeFingerprint()
        {
            var value = new StringBuilder();
            value.Append(FormatVersion).Append('\n')
                .Append(DdfDotNetNameMangler.FormatVersion).Append('\n')
                .Append(DdfDotNetCompilerProfile.FormatVersion).Append('\n')
                .Append(DdfDotNetCompilerProfile.RoslynVersion).Append('\n')
                .Append(DdfDotNetCompilerProfile.CSharpLanguageVersion).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.NullableContextOptions).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.CheckOverflow).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.AllowUnsafe).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.OptimizationLevel).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.Deterministic).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.ConcurrentBuild).Append('\n')
                .Append(DdfDotNetCompilerProfile.LibraryOptions.Platform).Append('\n')
                .Append(Options.TargetFramework).Append('\n')
                .Append(Options.RuntimeAbiVersion).Append('\n')
                .Append(Options.RuntimeIdentifier).Append('\n')
                .Append(Options.ReferenceSet?.Fingerprint ?? "unresolved-references")
                .Append('\n')
                .Append(EntryModuleIdentity).Append('\n')
                .Append(PreparationFingerprint).Append('\n');
            foreach (DdfDotNetCompilationWorkItem item in WorkItems)
            {
                value.Append(item.Identity).Append(':')
                    .Append(item.Unit.Fingerprint).Append(':')
                    .Append(item.AssemblyName).Append(':')
                    .Append(item.GeneratedNames.NamespaceName).Append(':')
                    .Append(item.GeneratedNames.ContainerTypeName).Append('\n');
                foreach (string dependency in item.DependencyIdentities)
                    value.Append("dep:").Append(dependency).Append('\n');
            }
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }
    }

    public sealed class DdfDotNetCompilationPlanResult
    {
        internal DdfDotNetCompilationPlanResult(DdfDotNetCompilationPlan plan,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Plan = plan;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetCompilationPlan Plan { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Plan != null && Issues.Count == 0;
    }

    /// <summary>
    /// Converts a successful shared Core preparation into one deterministic work
    /// item per module. It performs no C# generation and no Roslyn emit.
    /// </summary>
    public static class DdfDotNetCompilationPlanner
    {
        public static DdfDotNetCompilationPlanResult Create(DdfBuildResult preparation,
            DdfDotNetCompilationOptions options)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (options == null)
                issues.Add(new DdfDotNetContractIssue("DDFC300",
                    "Opzioni di compilazione richieste."));
            else if (options.RuntimeAbiVersion != DdfRuntimeAbi.Version)
                issues.Add(new DdfDotNetContractIssue("DDFC307",
                    "Runtime ABI non supportata dal compilatore: " +
                    options.RuntimeAbiVersion + ". Attesa " +
                    DdfRuntimeAbi.Version + "."));
            if (preparation == null || !preparation.Success ||
                preparation.Artifact == null)
                issues.Add(new DdfDotNetContractIssue("DDFC301",
                    "È richiesta una preparazione Core completata senza errori."));
            if (issues.Count != 0)
                return new DdfDotNetCompilationPlanResult(null, issues);

            IReadOnlyList<DdfCompilationUnit> units =
                preparation.Artifact.CompilationUnits ??
                Array.Empty<DdfCompilationUnit>();
            var byIdentity = new Dictionary<string, DdfCompilationUnit>(
                StringComparer.Ordinal);
            foreach (DdfCompilationUnit unit in units)
            {
                if (unit == null || !byIdentity.TryAdd(unit.Identity, unit))
                    issues.Add(new DdfDotNetContractIssue("DDFC302",
                        "Unità di compilazione assente o duplicata."));
            }
            string entryIdentity = preparation.Artifact.ModuleId + "@" +
                preparation.Artifact.ModuleVersion;
            if (!byIdentity.ContainsKey(entryIdentity))
                issues.Add(new DdfDotNetContractIssue("DDFC303",
                    "Unità del modulo di ingresso non trovata."));
            if (issues.Count != 0)
                return new DdfDotNetCompilationPlanResult(null, issues);

            var ordered = new List<DdfCompilationUnit>();
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            void Visit(DdfCompilationUnit unit)
            {
                if (completed.Contains(unit.Identity)) return;
                if (!visiting.Add(unit.Identity))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC304",
                        "Ciclo nel piano di compilazione: " + unit.Identity + "."));
                    return;
                }
                foreach (DdfCompilationDependency dependency in unit.Dependencies)
                {
                    if (!byIdentity.TryGetValue(dependency.Identity,
                            out DdfCompilationUnit dependencyUnit))
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC305",
                            "Unità dipendente non preparata: " +
                            dependency.Identity + "."));
                        continue;
                    }
                    Visit(dependencyUnit);
                }
                visiting.Remove(unit.Identity);
                if (completed.Add(unit.Identity)) ordered.Add(unit);
            }
            Visit(byIdentity[entryIdentity]);
            if (issues.Count != 0)
                return new DdfDotNetCompilationPlanResult(null, issues);

            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var workItems = new List<DdfDotNetCompilationWorkItem>();
            foreach (DdfCompilationUnit unit in ordered)
            {
                string assemblyName = unit.ModuleId + "." + unit.ModuleVersion;
                if (!assemblyNames.Add(assemblyName))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC306",
                        "Nome assembly duplicato nel piano: " + assemblyName + "."));
                    continue;
                }
                string outputDirectory = "modules/" + unit.ModuleId + "/" +
                    unit.ModuleVersion;
                workItems.Add(new DdfDotNetCompilationWorkItem(unit,
                    unit.Identity == entryIdentity, assemblyName, outputDirectory,
                    unit.Dependencies.Select(item => item.Identity),
                    DdfDotNetNameMangler.Create(unit.BoundIntermediateRepresentation),
                    options.ReferenceSet));
            }
            if (issues.Count != 0)
                return new DdfDotNetCompilationPlanResult(null, issues);
            return new DdfDotNetCompilationPlanResult(new DdfDotNetCompilationPlan(
                options, workItems, entryIdentity,
                preparation.Artifact.Fingerprint), issues);
        }
    }
}
