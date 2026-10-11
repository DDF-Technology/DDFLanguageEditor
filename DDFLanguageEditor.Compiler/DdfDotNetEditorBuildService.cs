using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Backend di build innestato esclusivamente dal launcher standalone.
    /// Produce il bundle di sviluppo verificato (DLL, PDB, source map e
    /// manifest) nella cache content-addressed della configurazione scelta.
    /// </summary>
    public sealed class DdfDotNetEditorBuildService :
        IDdfEditorCompiledBuildService
    {
        private readonly object sync = new();
        private readonly string workerPath;
        private LastBuildState lastBuild;

        public DdfDotNetEditorBuildService(string workerPath = null)
        {
            this.workerPath = ResolveWorkerPath(workerPath);
        }

        public bool HasRunnableBuild
        {
            get { lock (sync) return lastBuild != null; }
        }

        public string LastArtifactDirectory
        {
            get { lock (sync) return lastBuild?.ArtifactDirectory ?? string.Empty; }
        }

        public DdfEditorCompiledBuildResult Build(
            DdfEditorCompiledBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var issues = new List<DdfEditorCompiledBuildIssue>();
            if (!request.Preparation.Success)
                issues.Add(new DdfEditorCompiledBuildIssue("DDFC301",
                    "La preparazione Core non è stata completata."));
            if (!Path.IsPathRooted(request.ProjectRoot) ||
                !Path.IsPathRooted(request.OutputRoot) ||
                !Path.IsPathRooted(request.CacheRoot))
                issues.Add(new DdfEditorCompiledBuildIssue("DDFC412",
                    "Progetto, output e cache devono essere percorsi assoluti."));
            if (!string.Equals(request.Target, "win-x64",
                    StringComparison.OrdinalIgnoreCase))
                issues.Add(new DdfEditorCompiledBuildIssue("DDFC307",
                    "Target standalone non supportato: " + request.Target + "."));
            if (issues.Count != 0)
                return Failure(request.OutputRoot, issues);

            try
            {
                DdfDotNetResolvedReferenceSetResult resolved =
                    DdfDotNetResolvedReferenceSetBuilder.Create(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location,
                        request.Preparation);
                if (!resolved.Success)
                    return Failure(request.OutputRoot, Convert(resolved.Issues));

                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(request.Preparation,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, request.Target,
                            resolved.ReferenceSet));
                if (!planned.Success)
                    return Failure(request.OutputRoot, Convert(planned.Issues));

                DdfDotNetArtifactCacheResult cached =
                    DdfDotNetArtifactCache.GetOrEmit(planned.Plan,
                        request.LanguagePackage.Language.Types.ClrMapping,
                        request.CacheRoot);
                if (!cached.Success)
                    return Failure(request.OutputRoot, Convert(cached.Issues));

                string artifactDirectory = Path.Combine(request.OutputRoot,
                    "artifacts");
                DdfDotNetArtifactPublicationResult stable =
                    DdfDotNetStableArtifactOutput.Replace(cached.Emission,
                        artifactDirectory);
                if (!stable.Success)
                    return Failure(request.OutputRoot, Convert(stable.Issues));
                lock (sync)
                    lastBuild = new LastBuildState(planned.Plan, cached,
                        resolved.ReferenceSet,
                        resolved.ManagedReferencePaths,
                        request.Preparation.Artifact.MaxInstructions,
                        artifactDirectory);
                return new DdfEditorCompiledBuildResult(request.OutputRoot,
                    artifactDirectory, request.CacheRoot, cached.EntryPath,
                    cached.CacheHit, null);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is DirectoryNotFoundException ||
                exception is BadImageFormatException)
            {
                return Failure(request.OutputRoot, new[]
                {
                    new DdfEditorCompiledBuildIssue("DDFC490",
                        "Build Roslyn standalone fallita: " + exception.Message)
                });
            }
        }

        public async Task<DdfEditorCompiledRunResult> RunLastBuildAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            LastBuildState state;
            lock (sync) state = lastBuild;
            if (state == null)
                return RunFailure(null, new DdfEditorCompiledBuildIssue(
                    "DDFC491", "Nessuna build valida disponibile."));
            try
            {
                DdfDotNetArtifactLinkResult linked =
                    DdfDotNetArtifactLinker.Link(
                        state.Plan.EntryModuleIdentity,
                        state.Cache.Emission.Artifacts.Select(item =>
                            item.Manifest));
                if (!linked.Success)
                    return RunFailure(state.ArtifactDirectory,
                        Convert(linked.Issues));
                if (linked.Program.RequiredHostCapabilities.Count != 0)
                    return RunFailure(state.ArtifactDirectory,
                        new DdfEditorCompiledBuildIssue("DDFC492",
                            "L'ultima build richiede capability host non " +
                            "disponibili nell'esecuzione standalone: " +
                            string.Join(", ", linked.Program
                                .RequiredHostCapabilities) + "."));

                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.CreateWithPackagedReferences(
                        linked.Program, state.Cache.Emission,
                        state.ReferenceSet,
                        state.ManagedReferencePaths);
                if (!bundled.Success)
                    return RunFailure(state.ArtifactDirectory,
                        Convert(bundled.Issues));

                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.Standalone, "net10.0",
                    DdfRuntimeAbi.Version,
                    state.Plan.Options.RuntimeIdentifier);
                DdfDotNetWorkerRequest workerRequest =
                    DdfDotNetWorkerProtocol.CreateRequest(bundled.Bundle,
                        state.ReferenceSet, environment,
                        state.MaxInstructions);
                DdfDotNetWorkerExecutionResult execution = await
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath,
                        workerRequest, timeout ?? TimeSpan.FromMinutes(5),
                        cancellationToken).ConfigureAwait(false);
                if (!execution.Success)
                {
                    var issues = Convert(execution.Issues).ToList();
                    if (execution.Response != null &&
                        !string.IsNullOrWhiteSpace(
                            execution.Response.ErrorCode))
                        issues.Add(new DdfEditorCompiledBuildIssue(
                            execution.Response.ErrorCode,
                            execution.Response.ErrorMessage));
                    if (issues.Count == 0)
                        issues.Add(new DdfEditorCompiledBuildIssue("DDFC493",
                            execution.TimedOut
                                ? "Esecuzione dell'ultima build scaduta."
                                : "Il worker ha terminato senza un esito valido."));
                    return RunFailure(state.ArtifactDirectory, issues);
                }
                return new DdfEditorCompiledRunResult(
                    state.ArtifactDirectory,
                    execution.Response.ResultJson,
                    execution.Response.Instructions,
                    execution.Response.Ended,
                    execution.Response.Output, null);
            }
            catch (OperationCanceledException)
            {
                return RunFailure(state.ArtifactDirectory,
                    new DdfEditorCompiledBuildIssue("DDFC494",
                        "Esecuzione dell'ultima build annullata."));
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is BadImageFormatException)
            {
                return RunFailure(state.ArtifactDirectory,
                    new DdfEditorCompiledBuildIssue("DDFC493",
                        "Esecuzione dell'ultima build fallita: " +
                        exception.Message));
            }
        }

        public DdfEditorCompiledPublishResult PublishLastBuild(
            DdfEditorCompiledPublishRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            LastBuildState state;
            lock (sync) state = lastBuild;
            if (state == null)
                return PublishFailure(request.Destination,
                    new DdfEditorCompiledBuildIssue("DDFC495",
                        "Nessuna build valida disponibile per la pubblicazione."));
            if (!string.Equals(request.Target,
                    state.Plan.Options.RuntimeIdentifier,
                    StringComparison.OrdinalIgnoreCase))
                return PublishFailure(request.Destination,
                    new DdfEditorCompiledBuildIssue("DDFC307",
                        "Target di pubblicazione non coerente con la build: " +
                        request.Target + "."));
            try
            {
                DdfDotNetArtifactLinkResult linked =
                    DdfDotNetArtifactLinker.Link(
                        state.Plan.EntryModuleIdentity,
                        state.Cache.Emission.Artifacts.Select(item =>
                            item.Manifest));
                if (!linked.Success)
                    return PublishFailure(request.Destination,
                        Convert(linked.Issues));
                if (linked.Program.RequiredHostCapabilities.Count != 0)
                    return PublishFailure(request.Destination,
                        new DdfEditorCompiledBuildIssue("DDFC492",
                            "La build richiede capability host non pubblicabili " +
                            "nello standalone: " + string.Join(", ",
                                linked.Program.RequiredHostCapabilities) + "."));

                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.CreateWithPackagedReferences(
                        linked.Program, state.Cache.Emission,
                        state.ReferenceSet, state.ManagedReferencePaths);
                if (!bundled.Success)
                    return PublishFailure(request.Destination,
                        Convert(bundled.Issues));

                var options = new DdfDotNetExecutablePublishOptions(
                    request.Destination, request.Target,
                    request.ExecutableName, state.MaxInstructions,
                    ResolveProductFile("LICENSE"),
                    ResolveProductFile("THIRD_PARTY_NOTICES.md"),
                    iconPath: request.IconPath);
                DdfDotNetExecutablePublishResult result =
                    DdfDotNetExecutablePublisher.Publish(state.Plan,
                        bundled.Bundle, state.Cache.Emission, options);
                return new DdfEditorCompiledPublishResult(result.OutputRoot,
                    result.ExecutablePath, result.PublishedFiles,
                    Convert(result.Issues));
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is DirectoryNotFoundException ||
                exception is BadImageFormatException)
            {
                return PublishFailure(request.Destination,
                    new DdfEditorCompiledBuildIssue("DDFC495",
                        "Pubblicazione standalone fallita: " +
                        exception.Message));
            }
        }

        private static DdfEditorCompiledBuildResult Failure(string outputRoot,
            IEnumerable<DdfEditorCompiledBuildIssue> issues) =>
            new DdfEditorCompiledBuildResult(outputRoot, null, false, issues);

        private static DdfEditorCompiledRunResult RunFailure(
            string artifactDirectory,
            params DdfEditorCompiledBuildIssue[] issues) =>
            RunFailure(artifactDirectory,
                (IEnumerable<DdfEditorCompiledBuildIssue>)issues);

        private static DdfEditorCompiledRunResult RunFailure(
            string artifactDirectory,
            IEnumerable<DdfEditorCompiledBuildIssue> issues) =>
            new DdfEditorCompiledRunResult(artifactDirectory, null, 0, false,
                null, issues);

        private static DdfEditorCompiledPublishResult PublishFailure(
            string outputRoot,
            params DdfEditorCompiledBuildIssue[] issues) =>
            PublishFailure(outputRoot,
                (IEnumerable<DdfEditorCompiledBuildIssue>)issues);

        private static DdfEditorCompiledPublishResult PublishFailure(
            string outputRoot,
            IEnumerable<DdfEditorCompiledBuildIssue> issues) =>
            new DdfEditorCompiledPublishResult(outputRoot, null, null, issues);

        private static IEnumerable<DdfEditorCompiledBuildIssue> Convert(
            IEnumerable<DdfDotNetContractIssue> issues) =>
            (issues ?? Array.Empty<DdfDotNetContractIssue>()).Select(issue =>
                new DdfEditorCompiledBuildIssue(issue.Code, issue.Message,
                    issue.SourcePath, issue.StartLine, issue.StartColumn));

        private static string FindNet10ReferencePack()
        {
            DirectoryInfo runtime = new(RuntimeEnvironment.GetRuntimeDirectory()
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            string packs = Path.Combine(runtime.Parent?.Parent?.Parent?.FullName ??
                string.Empty, "packs", "Microsoft.NETCore.App.Ref");
            string selected = Directory.Exists(packs)
                ? Directory.GetDirectories(packs)
                    .Select(path => Path.Combine(path, "ref", "net10.0"))
                    .Where(Directory.Exists)
                    .OrderByDescending(path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : null;
            return selected ?? throw new DirectoryNotFoundException(
                "Reference pack Microsoft.NETCore.App.Ref net10.0 non trovato.");
        }

        private static string ResolveProductFile(string name)
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 8;
                 depth++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, name);
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException(
                "File di prodotto richiesto non trovato: " + name + ".");
        }

        private static string ResolveWorkerPath(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(Path.GetFullPath(configured)))
                return Path.GetFullPath(configured);
            string environment = Environment.GetEnvironmentVariable(
                "DDF_RUNNER_PATH");
            if (!string.IsNullOrWhiteSpace(environment) &&
                File.Exists(Path.GetFullPath(environment)))
                return Path.GetFullPath(environment);
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 7;
                 depth++, directory = directory.Parent)
            foreach (string configuration in new[] { "Release", "Debug" })
            {
                string candidate = Path.Combine(directory.FullName,
                    "DDFLanguageEditor.Runner", "bin", configuration,
                    "DDFLanguageEditor.Runner.dll");
                if (File.Exists(candidate)) return candidate;
            }
            string beside = Path.Combine(AppContext.BaseDirectory,
                "DDFLanguageEditor.Runner.dll");
            if (File.Exists(beside)) return beside;
            throw new FileNotFoundException(
                "Worker DDF standalone non trovato.");
        }

        private sealed class LastBuildState
        {
            public LastBuildState(DdfDotNetCompilationPlan plan,
                DdfDotNetArtifactCacheResult cache,
                DdfDotNetReferenceSet referenceSet,
                IReadOnlyList<string> managedReferencePaths,
                int maxInstructions, string artifactDirectory)
            {
                Plan = plan;
                Cache = cache;
                ReferenceSet = referenceSet;
                ManagedReferencePaths = managedReferencePaths;
                MaxInstructions = maxInstructions;
                ArtifactDirectory = artifactDirectory ?? string.Empty;
            }

            public DdfDotNetCompilationPlan Plan { get; }
            public DdfDotNetArtifactCacheResult Cache { get; }
            public DdfDotNetReferenceSet ReferenceSet { get; }
            public IReadOnlyList<string> ManagedReferencePaths { get; }
            public int MaxInstructions { get; }
            public string ArtifactDirectory { get; }
        }
    }
}
