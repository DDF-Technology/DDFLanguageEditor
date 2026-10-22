using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Data;
using DDFLanguageEditor.Hardware;
using DDFLanguageEditor.Network;
using DDFLanguageEditor.Runtime;
using DDFLanguageEditor.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using RoslynCompilationUnitSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
using RoslynForStatementSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax;
using RoslynLiteralExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax;
using PersistedRuntimeStateEntry = DDFLanguageEditor.Runtime.DdfPersistedStateEntry;
using PersistedRuntimeStateSnapshot = DDFLanguageEditor.Runtime.DdfPersistedStateSnapshot;

namespace DDFLanguageEditor.Compiler.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if ((args ?? Array.Empty<string>()).Contains(
                        "--worker-write-gate", StringComparer.Ordinal))
                {
                    RunsTransactionalFileBrokerOutOfProcess(
                        requireReducedToken: false);
                    Console.WriteLine(
                        "Transactional worker write gate passed.");
                    return 0;
                }
                if ((args ?? Array.Empty<string>()).Contains("--adapter-gate",
                        StringComparer.Ordinal))
                {
                    VerifiesGovernedAdaptersAcrossVmAndCompiledBackend();
                    Console.WriteLine(
                        "Governed adapter VM/Roslyn cross-backend gate passed.");
                    return 0;
                }
                if ((args ?? Array.Empty<string>()).Contains(
                        "--debug-language-matrix", StringComparer.Ordinal))
                {
                    DebugsActiveLanguageConstructsMatrix();
                    return 0;
                }
                if ((args ?? Array.Empty<string>()).Contains(
                        "--debug-suspension-matrix", StringComparer.Ordinal))
                {
                    VerifiesCompiledCapabilitySuspension();
                    RunsInteractiveInputOverDuplexWorker();
                    return 0;
                }
                RoundTripsVersionedDotNetArtifactContracts();
                RejectsUnsafeOrIncompatibleArtifactMetadata();
                KeepsJsonSchemasAlignedWithRuntimeContracts();
                PlansModulesAndLibrariesThroughTheSharedPipeline();
                MatchesVmOnDifferentialExecutionCorpus();
                MatchesVersionedNormativeLanguageCorpus();
                BridgesExplicitStandaloneHostCapabilities();
                VerifiesCompiledCapabilitySuspension();
                MatchesCleanupOrderAcrossVmAndCompiledBackend();
                PersistsStateAcrossVmAndCompiledSessions();
                PreservesTypedErrorsAcrossVmAndCompiledFiles();
                VerifiesGovernedAdaptersAcrossVmAndCompiledBackend();
                RunsReadOnlyFileBrokerOutOfProcess();
                RunsTransactionalFileBrokerOutOfProcess();
                GovernsHostSideHttpAcquisition();
                ValidatesDuplexWorkerFramingAndLifecycle();
                RunsNetworkSnapshotBrokerOutOfProcess();
                RunsLiveNetworkBrokerOverDuplexWorker();
                RunsBufferedInputOutOfProcess();
                RunsInteractiveInputOverDuplexWorker();
                BindsStableSymbolsTypesConversionsLocationsAndCalls();
                CreatesSafeDeterministicGeneratedNames();
                PinsCompilerProfileAndExplicitReferences();
                BuildsVerifiedManagedExternalReferenceSets();
                BuildsEditorArtifactsThroughStandaloneBridge();
                StressesShippedLargeWorkspaceAcrossVmAndCompiledBackend();
                DebugsShippedLanguageFeaturesWorkspace();
                DebugsActiveLanguageConstructsMatrix();
                GeneratesSafeSyntaxTreesAndModuleInitializers();
                Console.WriteLine("All 30 compiler contract tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void DebugsShippedLanguageFeaturesWorkspace()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples",
                "language-features-debug-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Require(loaded.Success, "Il manifest del workspace di debug non è valido.");

            var documents = new OpenDocumentCollection();
            string mainPath = Path.Combine(root, "main.ddf");
            OpenDocumentBuffer main = documents.Open(mainPath,
                File.ReadAllText(mainPath, Encoding.UTF8));

            DdfEditorExecutionBuildContext Build(
                DdfEditorExecutionRequest request,
                IReadOnlyList<DdfEditorBuildDocumentSnapshot> snapshots,
                DdfLanguagePackage activePackage)
            {
                var buffers = snapshots.Where(item =>
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .ToDictionary(item => Path.GetFullPath(item.FilePath),
                        item => item.SourceText, StringComparer.OrdinalIgnoreCase);
                DdfBuildResult result = new DdfBuildService().Validate(root,
                    loaded.Manifest, activePackage, buffers);
                var paths = snapshots.Where(item =>
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .ToDictionary(item => item.Id,
                        item => Path.GetFullPath(item.FilePath),
                        StringComparer.Ordinal);
                return new DdfEditorExecutionBuildContext(result,
                    activePackage, paths);
            }

            var language = new DdfLanguagePackageSession(package);
            using var debugger = new DdfDotNetEditorDebugService(
                () => documents, () => language,
                (request, snapshots, activePackage) =>
                    Build(request, snapshots, activePackage));
            debugger.SetBreakpoints(new[]
            {
                new DdfEditorBreakpoint(main.Id, 27, true)
            });
            int pauses = 0;
            debugger.Paused += (_, _) =>
            {
                pauses++;
                debugger.Continue();
            };

            DdfEditorExecutionCompletion completion = debugger.RunAsync(
                    new DdfEditorExecutionRequest(timeout: TimeSpan.FromMinutes(2)))
                .GetAwaiter().GetResult();
            Require(completion.BuildIssues.All(item =>
                    item.Severity != DdfDiagnosticSeverity.Error),
                "Il debugger ha rifiutato il workspace: " + string.Join("; ",
                    completion.BuildIssues.Select(item => item.Code + ": " +
                        item.Message)));
            Require(string.IsNullOrEmpty(completion.InternalError),
                "Il debugger compilato è fallito: " + completion.InternalError);
            Require(completion.ExecutionResult?.Succeeded == true,
                "Il debugger non ha completato l'esecuzione del workspace.");
            Equal("419", Convert.ToString(completion.ExecutionResult.ReturnValue,
                CultureInfo.InvariantCulture));
            Equal(1, pauses);

            using var debuggerWithoutBreakpoints =
                new DdfDotNetEditorDebugService(
                    () => documents, () => language,
                    (request, snapshots, activePackage) =>
                        Build(request, snapshots, activePackage));
            int unexpectedPauses = 0;
            debuggerWithoutBreakpoints.Paused += (_, _) => unexpectedPauses++;
            DdfEditorExecutionCompletion completionWithoutBreakpoints =
                debuggerWithoutBreakpoints.RunAsync(
                        new DdfEditorExecutionRequest(
                            timeout: TimeSpan.FromMinutes(2)))
                    .GetAwaiter().GetResult();
            Require(string.IsNullOrEmpty(
                    completionWithoutBreakpoints.InternalError),
                "Il debug senza breakpoint ha creato un piano worker vuoto: " +
                completionWithoutBreakpoints.InternalError);
            Require(completionWithoutBreakpoints.ExecutionResult?.Succeeded ==
                    true,
                "Il debug senza breakpoint non completa l'esecuzione.");
            Equal("419", Convert.ToString(completionWithoutBreakpoints
                .ExecutionResult.ReturnValue, CultureInfo.InvariantCulture));
            Equal(0, unexpectedPauses);
        }

        private static void StressesShippedLargeWorkspaceAcrossVmAndCompiledBackend()
        {
            string root = Path.Combine(RepositoryRoot(), "samples",
                "debug-large-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Require(loaded.Success,
                "Il manifest del workspace di stress non e' valido.");
            DdfBuildResult prepared = new DdfBuildService().Validate(root,
                loaded.Manifest, package);
            Require(prepared.Success && prepared.Artifact.Sources.Count == 42,
                "Il workspace di stress non attraversa tutti i 42 sorgenti: " +
                string.Join("; ", prepared.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));

            DdfDotNetReferenceSet references =
                DdfDotNetReferencePackResolver.Resolve(FindNet10ReferencePack(),
                    typeof(DdfRuntimeAbi).Assembly.Location);
            Stopwatch compileWatch = Stopwatch.StartNew();
            DdfDotNetCompilationPlanResult planned =
                DdfDotNetCompilationPlanner.Create(prepared,
                    new DdfDotNetCompilationOptions("net10.0",
                        DdfRuntimeAbi.Version, referenceSet: references));
            Require(planned.Success,
                "Il piano Roslyn del workspace di stress non e' valido.");
            DdfDotNetArtifactEmissionResult emission =
                DdfDotNetArtifactEmitter.Emit(planned.Plan,
                    package.Language.Types.ClrMapping);
            compileWatch.Stop();
            Require(emission.Success && emission.Artifacts.Count == 1 &&
                    emission.Artifacts.Single().SourceMap.Documents.Count == 42,
                "L'emit Roslyn del workspace di stress ha perso sorgenti o mapping: " +
                string.Join("; ", emission.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));

            var vmOutput = new List<string>();
            Stopwatch vmWatch = Stopwatch.StartNew();
            DdfExecutionResult vm = DdfInterpreter.Execute(
                prepared.Artifact.RuntimeModules, package.Language,
                new DdfExecutionOptions
                {
                    EntryPoint = prepared.Artifact.EntryPoint,
                    Output = vmOutput.Add
                });
            vmWatch.Stop();
            var compiledOutput = new List<string>();
            Stopwatch compiledWatch = Stopwatch.StartNew();
            DdfRuntimeExecutionResult compiled = ExecuteForVerification(
                planned.Plan, emission, compiledOutput);
            compiledWatch.Stop();
            Require(vm.Succeeded && Convert.ToInt32(vm.ReturnValue,
                        CultureInfo.InvariantCulture) == 3387 &&
                    Convert.ToInt32(compiled.Value,
                        CultureInfo.InvariantCulture) == 3387 &&
                    vmOutput.SequenceEqual(compiledOutput) &&
                    vmOutput.SequenceEqual(new[]
                        { "Large debugger workspace completed" }),
                "VM e Roslyn divergono sul workspace di stress attivo.");

            Console.WriteLine("PASS stress 42 file VM/Roslyn: piano+emit=" +
                compileWatch.ElapsedMilliseconds + " ms, VM=" +
                vmWatch.ElapsedMilliseconds + " ms, Roslyn=" +
                compiledWatch.ElapsedMilliseconds + " ms");
        }

        private static void DebugsActiveLanguageConstructsMatrix()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples",
                "language-features-debug-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Require(loaded.Success,
                "Il manifest del workspace per la matrice debugger non è valido.");

            var documents = new OpenDocumentCollection();
            var opened = new Dictionary<string, IDdfEditorDocument>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(root, "*.ddf")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                IDdfEditorDocument document = documents.Open(path,
                    File.ReadAllText(path, Encoding.UTF8));
                opened[Path.GetFileName(path)] = document;
            }

            DdfEditorExecutionBuildContext Build(
                DdfEditorExecutionRequest request,
                IReadOnlyList<DdfEditorBuildDocumentSnapshot> snapshots,
                DdfLanguagePackage activePackage)
            {
                var buffers = snapshots.Where(item =>
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .ToDictionary(item => Path.GetFullPath(item.FilePath),
                        item => item.SourceText, StringComparer.OrdinalIgnoreCase);
                DdfBuildResult result = new DdfBuildService().Validate(root,
                    loaded.Manifest, activePackage, buffers);
                var paths = snapshots.Where(item =>
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .ToDictionary(item => item.Id,
                        item => Path.GetFullPath(item.FilePath),
                        StringComparer.Ordinal);
                return new DdfEditorExecutionBuildContext(result,
                    activePackage, paths);
            }

            var language = new DdfLanguagePackageSession(package);
            DdfDotNetEditorDebugService CreateDebugger() => new(
                () => documents, () => language,
                (request, snapshots, activePackage) =>
                    Build(request, snapshots, activePackage));
            int LineOf(string fileName, string fragment)
            {
                string source = opened[fileName].SourceText;
                int start = source.IndexOf(fragment, StringComparison.Ordinal);
                Require(start >= 0, "Frammento debugger non trovato in " +
                    fileName + ": " + fragment);
                return LineAt(source, start);
            }

            IDdfEditorDocument main = opened["main.ddf"];
            IDdfEditorDocument calculations = opened["calculations.ddf"];
            using (var debugger = CreateDebugger())
            using (var pauses = new DebugPauseRecorder(debugger))
            {
                debugger.SetWatchExpressions(new[]
                {
                    "completedRuns", "localOffset", "priority"
                });
                debugger.SetBreakpoints(new[]
                {
                    new DdfEditorBreakpoint(main.Id,
                        LineOf("main.ddf", "completedRuns++;")),
                    new DdfEditorBreakpoint(main.Id,
                        LineOf("main.ddf", "fixed int localOffset")),
                    new DdfEditorBreakpoint(calculations.Id,
                        LineOf("calculations.ddf", "continue;")),
                    new DdfEditorBreakpoint(main.Id,
                        LineOf("main.ddf", "priorityWeight(DefaultPriority)"))
                });
                Task<DdfEditorExecutionCompletion> run = debugger.RunAsync(
                    new DdfEditorExecutionRequest(timeout: TimeSpan.FromMinutes(2)));

                DdfEditorDebugSnapshot statePause = pauses.Wait();
                Require(statePause.Location.DocumentId == main.Id &&
                        statePause.States.Any(variable =>
                            variable.Name == "completedRuns" &&
                            variable.Kind == DdfRuntimeVariableKind.State) &&
                        statePause.Globals.Any(variable =>
                            variable.Name == "DefaultPriority" &&
                            variable.IsFixed && variable.IsReadOnly) &&
                        statePause.CallStack.All(frame =>
                            !frame.FunctionName.StartsWith("DdfGenerated_",
                                StringComparison.Ordinal)),
                    "La pausa su state/fixed espone metadati o frame non DDF.");
                DdfEditorWatchResult stateWatch = debugger.EvaluateWatch(
                    "completedRuns");
                Require(stateWatch.Available &&
                        stateWatch.Value.IsTransactionalState,
                    "Watch non espone lo state con la categoria DDF corretta.");
                Require(debugger.Continue(),
                    "Continua non riparte dalla pausa sullo state.");

                DdfEditorDebugSnapshot fixedPause = pauses.Wait();
                Require(fixedPause.Location.DocumentId == main.Id &&
                        fixedPause.Location.Line == LineOf("main.ddf",
                            "fixed int localOffset"),
                    "Il breakpoint sulla dichiarazione fixed non è stabile.");
                Require(debugger.StepOver(),
                    "Step Over non parte dalla dichiarazione fixed.");
                DdfEditorDebugSnapshot afterFixed = pauses.Wait();
                Require(afterFixed.Location.DocumentId == main.Id &&
                        afterFixed.Location.Line > fixedPause.Location.Line &&
                        afterFixed.Variables.Any(variable =>
                            variable.Name == "localOffset" &&
                            variable.DisplayValue == "2" &&
                            variable.IsFixed && variable.IsReadOnly),
                    "Step Over non supera fixed o perde la sua proiezione read-only.");
                DdfEditorWatchResult fixedWatch = debugger.EvaluateWatch(
                    "localOffset");
                Require(fixedWatch.Available && fixedWatch.Value.IsFixed &&
                        fixedWatch.Value.IsReadOnly &&
                        fixedWatch.Value.DisplayValue == "2",
                    "Watch non conserva il contratto read-only di fixed.");
                Require(debugger.Continue(),
                    "Continua non riparte dopo la dichiarazione fixed.");

                DdfEditorDebugSnapshot continuePause = pauses.Wait();
                Require(continuePause.Location.DocumentId == calculations.Id &&
                        continuePause.Location.Line == LineOf("calculations.ddf",
                            "continue;"),
                    "Il breakpoint dentro continue non raggiunge il file esterno.");
                Require(debugger.StepOver(),
                    "Step Over non parte dal costrutto continue.");
                DdfEditorDebugSnapshot afterContinue = pauses.Wait();
                Require(afterContinue.Location.DocumentId == calculations.Id &&
                        (afterContinue.Location.Line != continuePause.Location.Line ||
                         afterContinue.Location.Start != continuePause.Location.Start),
                    "Step Over resta bloccato sullo stesso continue.");
                Require(debugger.Continue(),
                    "Continua non riparte dopo il costrutto continue.");

                DdfEditorDebugSnapshot callPause = pauses.Wait();
                Require(callPause.Location.DocumentId == main.Id &&
                        callPause.Location.Line == LineOf("main.ddf",
                            "priorityWeight(DefaultPriority)"),
                    "Il breakpoint prima della chiamata enum/select non è stabile.");
                Require(debugger.StepInto(),
                    "Step Into non parte dalla chiamata priorityWeight.");
                DdfEditorDebugSnapshot selectPause = pauses.Wait();
                Require(selectPause.Location.DocumentId ==
                            opened["classification.ddf"].Id &&
                        selectPause.CallStack.Count >= 2 &&
                        selectPause.CallStack[0].FunctionName == "priorityWeight" &&
                        selectPause.CallStack[1].FunctionName == "main" &&
                        selectPause.Variables.Any(variable =>
                            variable.Name == "priority" &&
                            variable.TypeName == "Priority"),
                    "Step Into non entra nel select enum con Call Stack DDF.");
                DdfEditorWatchResult enumWatch = debugger.EvaluateWatch("priority");
                Require(enumWatch.Available &&
                        enumWatch.Value.TypeName == "Priority" &&
                        enumWatch.Value.DisplayValue == "Priority.High",
                    "Watch non espone il valore enum nominale nella funzione esterna: " +
                    (enumWatch.Available
                        ? enumWatch.Value.TypeName + "=" +
                          enumWatch.Value.DisplayValue
                        : enumWatch.Message));
                Require(debugger.StepOver(),
                    "Step Over non parte dentro il select enum.");
                DdfEditorDebugSnapshot insideSelect = pauses.Wait();
                Require(insideSelect.Location.DocumentId ==
                            opened["classification.ddf"].Id &&
                        insideSelect.CallStack[0].FunctionName == "priorityWeight",
                    "Step Over dentro select esce prematuramente dal file DDF.");
                Require(debugger.StepOut(),
                    "Step Out non parte dalla funzione con select.");
                DdfEditorDebugSnapshot callerPause = pauses.Wait();
                Require(callerPause.Location.DocumentId == main.Id &&
                        callerPause.CallStack.Count == 1 &&
                        callerPause.CallStack[0].FunctionName == "main",
                    "Step Out non torna al chiamante DDF nel file principale.");
                Require(debugger.Continue(),
                    "Continua non riparte dopo Step Out.");
                Require(run.Wait(TimeSpan.FromSeconds(30)) &&
                        run.Result.Succeeded &&
                        Convert.ToString(run.Result.ExecutionResult.ReturnValue,
                            CultureInfo.InvariantCulture) == "419",
                    "La matrice Step/Continue non completa il workspace con risultato 419.");
            }

            using (var debugger = CreateDebugger())
            using (var pauses = new DebugPauseRecorder(debugger))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfEditorBreakpoint(main.Id,
                        LineOf("main.ddf", "completedRuns++;"))
                });
                Task<DdfEditorExecutionCompletion> run = debugger.RunAsync(
                    new DdfEditorExecutionRequest(timeout: TimeSpan.FromMinutes(2)));
                pauses.Wait();
                Require(debugger.Stop(),
                    "Stop non viene accettato durante una pausa sui nuovi costrutti.");
                Require(run.Wait(TimeSpan.FromSeconds(15)) &&
                        run.Result.WasCancelled &&
                        debugger.State == DdfEditorDebugState.Idle,
                    "Stop non chiude in modo coerente la sessione compilata.");
            }

            string remappedSource = Environment.NewLine + main.SourceText;
            Require(documents.UpdateDocument(main.Id, remappedSource, true),
                "Il buffer main non accetta la modifica usata per il remapping.");
            using (var debugger = CreateDebugger())
            using (var pauses = new DebugPauseRecorder(debugger))
            {
                int remappedLine = LineOf("main.ddf", "completedRuns++;");
                debugger.SetBreakpoints(new[]
                {
                    new DdfEditorBreakpoint(main.Id, remappedLine)
                });
                Task<DdfEditorExecutionCompletion> run = debugger.RunAsync(
                    new DdfEditorExecutionRequest(timeout: TimeSpan.FromMinutes(2)));
                DdfEditorDebugSnapshot remapped = pauses.Wait();
                Require(remapped.Location.DocumentId == main.Id &&
                        remapped.Location.Line == remappedLine &&
                        remapped.Location.Start == remappedSource.IndexOf(
                            "completedRuns++;", StringComparison.Ordinal),
                    "Il breakpoint non viene rimappato sul buffer modificato.");
                Require(debugger.Continue(),
                    "Continua non riparte dopo il breakpoint rimappato.");
                Require(run.Wait(TimeSpan.FromSeconds(30)) &&
                        run.Result.Succeeded,
                    "Il workspace non termina dopo il remapping del breakpoint.");
            }

            Console.WriteLine(
                "PASS matrice debugger costrutti attivi: state/fixed, continue, enum/select, Step Into/Over/Out, Continua, Stop e remapping multifile");
        }

        private static void RoundTripsVersionedDotNetArtifactContracts()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-compiler-contract-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                string sourcePath = Path.Combine(root, "main.ddf");
                const string source = "main() out int { ret 7; }";
                File.WriteAllText(sourcePath, source, new UTF8Encoding(false));
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Compiler contract", package, "main.ddf");
                project.Module.Id = "example.compiled";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package);
                Require(prepared.Success, "La preparazione dell'unità è fallita.");
                DdfCompilationUnit unit = prepared.Artifact.CompilationUnits.Single();

                var map = new DdfDotNetSourceMap
                {
                    CompilationFingerprint = unit.Fingerprint,
                    LanguageProfileId = unit.LanguageId,
                    LanguageProfileVersion = unit.LanguagePackageVersion,
                    Documents = new List<DdfDotNetSourceMapDocument>
                    {
                        new DdfDotNetSourceMapDocument
                        {
                            SourcePath = "main.ddf",
                            SourceSha256 = unit.Sources.Single().Sha256,
                            GeneratedPath = "generated/example_compiled.g.cs",
                            SequencePoints = new List<DdfDotNetSequencePoint>
                            {
                                new DdfDotNetSequencePoint
                                {
                                    DdfStart = 0,
                                    DdfLength = source.Length,
                                    GeneratedStartLine = 1,
                                    GeneratedStartColumn = 1,
                                    GeneratedEndLine = 8,
                                    GeneratedEndColumn = 2
                                }
                            }
                        }
                    }
                };
                string mapJson = DdfDotNetArtifactContract.SerializeSourceMap(map);
                DdfDotNetContractLoadResult<DdfDotNetSourceMap> loadedMap =
                    DdfDotNetArtifactContract.DeserializeSourceMap(mapJson);
                Require(loadedMap.Success, "Round-trip mappa sorgente fallito.");
                Equal(unit.Fingerprint, loadedMap.Value.CompilationFingerprint);
                Equal("main.ddf", loadedMap.Value.Documents.Single().SourcePath);
                Equal(mapJson, DdfDotNetArtifactContract.SerializeSourceMap(
                    loadedMap.Value));

                byte[] assemblyBytes = { 0x4d, 0x5a };
                byte[] pdbBytes = { 0x42, 0x53, 0x4a, 0x42 };
                byte[] mapBytes = Encoding.UTF8.GetBytes(mapJson);
                var payloads = new[]
                {
                    DdfDotNetPayloadDescriptor.FromBytes(
                        DdfDotNetPayloadKind.Assembly, "example.compiled.dll",
                        assemblyBytes),
                    DdfDotNetPayloadDescriptor.FromBytes(
                        DdfDotNetPayloadKind.PortablePdb, "example.compiled.pdb",
                        pdbBytes),
                    DdfDotNetPayloadDescriptor.FromBytes(
                        DdfDotNetPayloadKind.SourceMap,
                        "example.compiled.ddfmap.json", mapBytes)
                };
                DdfDotNetArtifactManifest manifest = DdfDotNetArtifactManifest.Create(
                    unit, "net10.0", "1.0.0", "example.compiled", payloads,
                    runtimeIdentifier: "win-x64",
                    requiredHostCapabilities: new[] { "database.read" },
                    buildReceipt: TestBuildReceipt());
                string json = DdfDotNetArtifactContract.SerializeManifest(manifest);
                DdfDotNetContractLoadResult<DdfDotNetArtifactManifest> loaded =
                    DdfDotNetArtifactContract.DeserializeManifest(json);
                Require(loaded.Success, "Round-trip manifest .NET fallito.");
                Equal(DdfDotNetArtifactManifest.SchemaVersionValue,
                    loaded.Value.SchemaVersion);
                Equal("example.compiled", loaded.Value.AssemblyName);
                Equal(unit.Fingerprint, loaded.Value.CompilationFingerprint);
                Equal("main", loaded.Value.Exports.Single().Name);
                Equal(0, loaded.Value.Imports.Count);
                Equal(3, loaded.Value.Payloads.Count);
                Equal(json, DdfDotNetArtifactContract.SerializeManifest(loaded.Value));
                Require(DdfDotNetArtifactContract.ValidateBundle(loaded.Value,
                    loadedMap.Value).Count == 0,
                    "Manifest e mappa non formano un bundle coerente.");
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.Standalone, "net10.0", "1.0.0",
                    "win-x64", new[] { "database.read" });
                Require(DdfDotNetArtifactCompatibility.Inspect(loaded.Value,
                    environment, sourceMap: loadedMap.Value).IsCompatible,
                    "Un ambiente standalone compatibile è stato rifiutato.");
                DdfDotNetCompatibilityResult embedded =
                    DdfDotNetArtifactCompatibility.Inspect(loaded.Value,
                        new DdfDotNetRuntimeEnvironment(
                            DdfDotNetArtifactHostKind.Embedded, "net10.0", "1.0.0",
                            "win-x64", new[] { "database.read" }),
                        sourceMap: loadedMap.Value);
                Require(embedded.Issues.Any(item => item.Code == "DDFC201"),
                    "L'host embedded non ha rifiutato l'artefatto compilato.");
                DdfDotNetCompatibilityResult missingCapability =
                    DdfDotNetArtifactCompatibility.Inspect(loaded.Value,
                        new DdfDotNetRuntimeEnvironment(
                            DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                            "1.0.0", "win-x64"), sourceMap: loadedMap.Value);
                Require(missingCapability.Issues.Any(item => item.Code == "DDFC205"),
                    "Una capability host mancante non è stata rilevata.");

                File.WriteAllBytes(Path.Combine(root, "example.compiled.dll"),
                    assemblyBytes);
                File.WriteAllBytes(Path.Combine(root, "example.compiled.pdb"),
                    pdbBytes);
                File.WriteAllBytes(Path.Combine(root,
                    "example.compiled.ddfmap.json"), mapBytes);
                Require(DdfDotNetArtifactCompatibility.VerifyPayloads(loaded.Value,
                    root).IsCompatible, "I payload integri sono stati rifiutati.");
                File.WriteAllBytes(Path.Combine(root, "example.compiled.dll"),
                    new byte[] { 0x4d, 0x5b });
                Require(DdfDotNetArtifactCompatibility.VerifyPayloads(loaded.Value,
                    root).Issues.Any(item => item.Code == "DDFC213"),
                    "La manomissione della DLL non è stata rilevata.");
                File.WriteAllBytes(Path.Combine(root, "example.compiled.dll"),
                    assemblyBytes);
                var strictLimits = new DdfDotNetArtifactLoadLimits(
                    maxPayloadBytes: 1, maxTotalBytes: 3);
                Require(DdfDotNetArtifactCompatibility.VerifyPayloads(loaded.Value,
                    root, strictLimits).Issues.Any(item => item.Code == "DDFC206"),
                    "Il limite di caricamento non è stato applicato.");
                Require(!json.Contains("sourceText", StringComparison.OrdinalIgnoreCase),
                    "Il manifest contiene testo sorgente.");
                Require(!json.Contains("intermediateRepresentation",
                    StringComparison.OrdinalIgnoreCase), "Il manifest contiene IR.");
                Console.WriteLine("PASS manifest, compatibilità host e payload .NET verificati senza caricamento");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsUnsafeOrIncompatibleArtifactMetadata()
        {
            var invalid = new DdfDotNetArtifactManifest
            {
                ModuleId = "example.invalid",
                ModuleVersion = "1.0.0",
                CompilationUnitVersion = DdfCompilationUnit.ContractVersion,
                CompilationFingerprint = new string('a', 64),
                IrFormatVersion = DdfIrProgram.FormatVersion,
                LanguageId = "ddf",
                LanguagePackageVersion = "0.12.3",
                TargetFramework = "net10.0",
                RuntimeAbiVersion = "1.0.0",
                AssemblyName = "example.invalid",
                Payloads = new List<DdfDotNetPayloadDescriptor>
                {
                    new DdfDotNetPayloadDescriptor
                    {
                        Kind = DdfDotNetPayloadKind.Assembly,
                        Path = "../escape.dll",
                        Sha256 = new string('b', 64),
                        Size = 10
                    }
                }
            };
            IReadOnlyList<DdfDotNetContractIssue> issues =
                DdfDotNetArtifactContract.Validate(invalid);
            Require(issues.Any(item => item.Code == "DDFC013"),
                "Il path traversal del payload non è stato rifiutato.");

            DdfDotNetContractLoadResult<DdfDotNetArtifactManifest> unknown =
                DdfDotNetArtifactContract.DeserializeManifest(
                    "{\"format\":\"ddf-dotnet-artifact\",\"unknown\":true}");
            Require(!unknown.Success && unknown.Issues.Any(item =>
                    item.Code == "DDFC001"),
                "Una proprietà sconosciuta non è stata rifiutata.");

            var invalidMap = new DdfDotNetSourceMap
            {
                CompilationFingerprint = new string('c', 64),
                LanguageProfileId = "ddf",
                LanguageProfileVersion = "0.12.3",
                Documents = new List<DdfDotNetSourceMapDocument>
                {
                    new DdfDotNetSourceMapDocument
                    {
                        SourcePath = "main.ddf",
                        SourceSha256 = new string('d', 64),
                        GeneratedPath = "generated/main.g.cs",
                        SequencePoints = new List<DdfDotNetSequencePoint>
                        {
                            new DdfDotNetSequencePoint
                            {
                                DdfStart = 5, DdfLength = 3,
                                GeneratedStartLine = 2, GeneratedStartColumn = 1,
                                GeneratedEndLine = 2, GeneratedEndColumn = 4
                            },
                            new DdfDotNetSequencePoint
                            {
                                DdfStart = 4, DdfLength = 2,
                                GeneratedStartLine = 3, GeneratedStartColumn = 1,
                                GeneratedEndLine = 3, GeneratedEndColumn = 3
                            }
                        }
                    }
                }
            };
            Require(DdfDotNetArtifactContract.Validate(invalidMap).Any(item =>
                    item.Code == "DDFC106"),
                "Le coordinate sorgente sovrapposte non sono state rifiutate.");
            Console.WriteLine("PASS metadati incompatibili e percorsi non sicuri rifiutati");
        }

        private static void KeepsJsonSchemasAlignedWithRuntimeContracts()
        {
            string root = RepositoryRoot();
            using JsonDocument artifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                root, "ddf-dotnet-artifact.schema.json"), Encoding.UTF8));
            using JsonDocument sourceMap = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                root, "ddf-dotnet-source-map.schema.json"), Encoding.UTF8));
            using JsonDocument linkedPackage = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(root,
                    "ddf-dotnet-linked-package.schema.json"), Encoding.UTF8));
            Equal(DdfDotNetArtifactManifest.FormatValue, artifact.RootElement
                .GetProperty("properties").GetProperty("format").GetProperty("const")
                .GetString());
            Equal(DdfDotNetArtifactManifest.SchemaVersionValue, artifact.RootElement
                .GetProperty("properties").GetProperty("schemaVersion")
                .GetProperty("const").GetString());
            Require(artifact.RootElement.GetProperty("required").EnumerateArray()
                    .Any(item => item.GetString() == "imports") &&
                    artifact.RootElement.GetProperty("properties")
                        .TryGetProperty("imports", out _),
                "Lo schema manifest non rende obbligatori gli import tipizzati.");
            Equal(DdfDotNetSourceMap.FormatValue, sourceMap.RootElement
                .GetProperty("properties").GetProperty("format").GetProperty("const")
                .GetString());
            Equal(DdfDotNetSourceMap.SchemaVersionValue, sourceMap.RootElement
                .GetProperty("properties").GetProperty("schemaVersion")
                .GetProperty("const").GetString());
            Equal(DdfDotNetLinkedPackageManifest.FormatValue, linkedPackage
                .RootElement.GetProperty("properties").GetProperty("format")
                .GetProperty("const").GetString());
            Equal(DdfDotNetLinkedPackageManifest.SchemaVersionValue,
                linkedPackage.RootElement.GetProperty("properties")
                    .GetProperty("schemaVersion").GetProperty("const")
                    .GetString());
            Equal(DdfDotNetLinkedBundle.FormatVersion, linkedPackage.RootElement
                .GetProperty("properties").GetProperty("bundleFormatVersion")
                .GetProperty("const").GetString());
            Console.WriteLine("PASS schemi JSON allineati ai contratti runtime");
        }

        private static void BuildsEditorArtifactsThroughStandaloneBridge()
        {
            string projectRoot = Path.Combine(RepositoryRoot(), "samples",
                "module-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded =
                DdfProjectManifestLoader.Load(projectRoot);
            Require(loaded.Success,
                "Il progetto del gate UI build non è valido.");
            DdfBuildResult prepared = new DdfBuildService().Validate(
                projectRoot, loaded.Manifest, package);
            Require(prepared.Success,
                "La preparazione Core del gate UI build è fallita.");

            string temporary = Path.Combine(Path.GetTempPath(),
                "ddf-editor-build-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new DdfDotNetEditorBuildService();
                string debugRoot = Path.Combine(temporary, "bin", "Debug",
                    "win-x64");
                string debugCache = Path.Combine(temporary, "obj", "ddf",
                    "cache", "Debug", "win-x64");
                DdfEditorCompiledBuildResult first = service.Build(
                    new DdfEditorCompiledBuildRequest(prepared, package,
                        projectRoot, debugRoot,
                        DdfEditorBuildConfiguration.Debug, "win-x64",
                        debugCache));
                Require(first.Success && !first.CacheHit &&
                        first.ArtifactDirectory == Path.Combine(debugRoot,
                            "artifacts") &&
                        first.CacheRoot == debugCache &&
                        first.CacheEntryDirectory.StartsWith(debugCache,
                            StringComparison.OrdinalIgnoreCase) &&
                        !first.ArtifactDirectory.Contains("v1.0",
                            StringComparison.OrdinalIgnoreCase) &&
                        Directory.EnumerateFiles(first.ArtifactDirectory,
                            "*.dll", SearchOption.AllDirectories).Any() &&
                        Directory.EnumerateFiles(first.ArtifactDirectory,
                            "*.pdb", SearchOption.AllDirectories).Any() &&
                        Directory.EnumerateFiles(first.ArtifactDirectory,
                            "*.ddfartifact.json", SearchOption.AllDirectories)
                            .Any(),
                    "Il bridge standalone non ha prodotto DLL, PDB e manifest: " +
                    string.Join(", ", first.Issues));
                DdfEditorCompiledBuildResult repeated = service.Build(
                    new DdfEditorCompiledBuildRequest(prepared, package,
                        projectRoot, debugRoot,
                        DdfEditorBuildConfiguration.Debug, "win-x64",
                        debugCache));
                Require(repeated.Success && repeated.CacheHit &&
                        repeated.ArtifactDirectory == first.ArtifactDirectory &&
                        repeated.CacheEntryDirectory ==
                            first.CacheEntryDirectory,
                    "La seconda build UI non ha riusato la cache verificata.");

                string releaseRoot = Path.Combine(temporary, "bin", "Release",
                    "win-x64");
                string releaseCache = Path.Combine(temporary, "obj", "ddf",
                    "cache", "Release", "win-x64");
                DdfEditorCompiledBuildResult release = service.Build(
                    new DdfEditorCompiledBuildRequest(prepared, package,
                        projectRoot, releaseRoot,
                        DdfEditorBuildConfiguration.Release, "win-x64",
                        releaseCache));
                Require(release.Success &&
                        release.ArtifactDirectory == Path.Combine(releaseRoot,
                            "artifacts") &&
                        release.CacheEntryDirectory.StartsWith(releaseCache,
                            StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(release.ArtifactDirectory,
                            first.ArtifactDirectory,
                            StringComparison.OrdinalIgnoreCase),
                    "Debug e Release non sono rimaste in directory separate.");
                DdfEditorCompiledRunResult executed = service
                    .RunLastBuildAsync(TimeSpan.FromSeconds(30))
                    .GetAwaiter().GetResult();
                Require(executed.Success && executed.ResultJson == "42" &&
                        executed.Instructions > 0 &&
                        executed.Output.SequenceEqual(new[] { "42" }) &&
                        executed.ArtifactDirectory ==
                            release.ArtifactDirectory,
                    "Esegui ultima build non ha usato il worker o l'artefatto " +
                    "Release verificato: " + string.Join(", ",
                        executed.Issues));
                string publishRoot = Path.Combine(temporary, "publish",
                    "EditorBridgeSample");
                DdfEditorCompiledPublishResult published =
                    service.PublishLastBuild(
                        new DdfEditorCompiledPublishRequest(publishRoot,
                            "EditorBridgeSample", "win-x64"));
                Require(published.Success &&
                        File.Exists(published.ExecutablePath) &&
                        File.Exists(Path.Combine(publishRoot,
                            "support", "ddf-publish.json")) &&
                        File.Exists(Path.Combine(publishRoot,
                            "support", "LICENSE.txt")) &&
                        Directory.EnumerateFiles(publishRoot, "*",
                            SearchOption.TopDirectoryOnly).Count() == 1 &&
                        !published.PublishedFiles.Any(path =>
                            path.EndsWith("DDFLanguageEditor.Compiler.dll",
                                StringComparison.OrdinalIgnoreCase)),
                    "Pubblica EXE tramite bridge editor non ha prodotto un " +
                    "package self-contained verificato: " +
                    string.Join(", ", published.Issues));
                VerifyPublishedPackageManifest(publishRoot);
                Require(!service.Build(new DdfEditorCompiledBuildRequest(
                            prepared, package, projectRoot,
                            Path.Combine(temporary, "unsupported"),
                            DdfEditorBuildConfiguration.Debug, "linux-x64",
                            Path.Combine(temporary, "obj", "unsupported")))
                        .Success,
                    "Il bridge standalone ha accettato un target non supportato.");
            }
            finally
            {
                if (Directory.Exists(temporary))
                    Directory.Delete(temporary, true);
            }
            Console.WriteLine("PASS bridge build editor standalone: Debug/Release, " +
                "cache interna, output stabile, target, worker e pubblicazione " +
                "EXE verificati");
        }

        private static void PlansModulesAndLibrariesThroughTheSharedPipeline()
        {
            string root = Path.Combine(RepositoryRoot(), "samples",
                "module-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Require(loaded.Success, "Il workspace moduli campione non è valido.");
            DdfBuildResult prepared = new DdfBuildService().Validate(root,
                loaded.Manifest, package);
            Require(prepared.Success,
                "La pipeline Core condivisa non ha preparato il workspace moduli.");
            var options = new DdfDotNetCompilationOptions("net10.0", DdfRuntimeAbi.Version,
                "win-x64");
            DdfDotNetCompilationPlanResult first =
                DdfDotNetCompilationPlanner.Create(prepared, options);
            Require(first.Success, "Il piano multi-modulo non è stato creato.");
            Equal(3, first.Plan.WorkItems.Count);
            Equal("ddf.sample.module-app@0.1.0",
                first.Plan.EntryModuleIdentity);
            Require(first.Plan.WorkItems.Take(2).All(item => item.IsLibrary),
                "Le librerie non precedono il modulo applicativo.");
            DdfDotNetCompilationWorkItem entry = first.Plan.WorkItems.Last();
            Require(entry.IsEntryModule, "Il modulo di ingresso non è ultimo.");
            Require(DdfDotNetCompilationBuilder.Create(entry,
                    package.Language.Types.ClrMapping).Issues.Any(issue =>
                        issue.Code == "DDFC404"),
                "La validazione Roslyn ha accettato una work item senza reference set.");
            Equal(2, entry.DependencyIdentities.Count);
            Require(entry.DependencyIdentities.Contains("ddf.sample.math@1.0.0") &&
                    entry.DependencyIdentities.Contains("ddf.sample.scale@1.0.0"),
                "Le dipendenze dirette non sono state conservate.");
            Require(first.Plan.WorkItems.All(item =>
                    !Path.IsPathRooted(item.AssemblyPath) &&
                    item.AssemblyPath.EndsWith(".dll", StringComparison.Ordinal)),
                "Il piano contiene output non relativi o non deterministici.");
            Require(first.Plan.WorkItems.All(item =>
                    item.Unit.BoundIrFormatVersion == "2.1" &&
                    item.Unit.BoundIntermediateRepresentation.Documents.Count ==
                    item.Unit.Sources.Count),
                "Il piano contiene unità prive della IR bound 2.1.");
            Require(first.Plan.WorkItems.All(item =>
                    item.GeneratedNames.Symbols.Count == item.BoundUnit.Symbols.Count),
                "Il piano contiene unità prive del name map completo.");
            DdfBoundCompilationUnit entryBound =
                entry.Unit.BoundIntermediateRepresentation;
            Require(!entryBound.Symbols.Any(item => item.Name == "hiddenOffset"),
                "Un simbolo privato di una dipendenza è trapelato nel modulo chiamante.");
            Require(entryBound.Documents.Single().Expressions.Count(item =>
                    item.IsResolvedCall) == 2,
                "Le chiamate esportate multi-modulo non sono state risolte.");
            Require(!DdfDotNetSyntaxTreeGenerator.Generate(entry,
                    package.Language.Types.ClrMapping).Success,
                "Una chiamata inter-modulo è stata generata senza tabella link.");
            DdfDotNetLinkNameTable links = DdfDotNetLinkNameTable.Create(
                first.Plan.WorkItems);
            DdfDotNetSyntaxGenerationResult linkedSyntax =
                DdfDotNetSyntaxTreeGenerator.Generate(entry,
                    package.Language.Types.ClrMapping, links);
            Require(linkedSyntax.Success,
                "Le chiamate inter-modulo non sono state qualificate: " +
                string.Join(", ", linkedSyntax.Issues.Select(issue => issue.Message)));
            string linkedText = linkedSyntax.SyntaxTrees.Single().GetRoot()
                .ToFullString();
            foreach (DdfBoundSymbol external in entryBound.Symbols.Where(symbol =>
                         symbol.Kind == DdfBoundSymbolKind.Function &&
                         symbol.ModuleIdentity != entry.Identity))
            {
                DdfDotNetLinkedSymbolName linked = links.Resolve(external);
                Require(linkedText.Contains(linked.NamespaceName,
                            StringComparison.Ordinal) &&
                        linkedText.Contains(linked.ContainerTypeName,
                            StringComparison.Ordinal) &&
                        linkedText.Contains(linked.Identifier,
                            StringComparison.Ordinal),
                    "La destinazione inter-modulo non usa nomi generati qualificati.");
            }
            foreach (string dependencyIdentity in entry.DependencyIdentities)
            {
                DdfDotNetLinkedModuleName dependency = links.ResolveModule(
                    dependencyIdentity);
                Require(linkedText.Contains(dependency.NamespaceName,
                            StringComparison.Ordinal) &&
                        linkedText.Contains(dependency.ContainerTypeName,
                            StringComparison.Ordinal) &&
                        linkedText.Contains(dependency.ModuleInitializerName,
                            StringComparison.Ordinal),
                    "Il coordinatore non inizializza una dipendenza diretta.");
            }
            DdfDotNetCompilationPlanResult second =
                DdfDotNetCompilationPlanner.Create(prepared, options);
            Equal(first.Plan.Fingerprint, second.Plan.Fingerprint);
            DdfDotNetReferenceSet references =
                DdfDotNetReferencePackResolver.Resolve(FindNet10ReferencePack(),
                    typeof(DdfRuntimeAbi).Assembly.Location);
            DdfDotNetCompilationPlanResult referenced =
                DdfDotNetCompilationPlanner.Create(prepared,
                    new DdfDotNetCompilationOptions("net10.0", DdfRuntimeAbi.Version,
                        referenceSet: references));
            Require(referenced.Success && referenced.Plan.Options.ReferenceSet ==
                    references && referenced.Plan.Fingerprint != first.Plan.Fingerprint,
                "Il reference set non partecipa al piano e al relativo fingerprint.");
            DdfDotNetPlanCompilationBuildResult planCompilation =
                DdfDotNetPlanCompilationBuilder.Create(referenced.Plan,
                    package.Language.Types.ClrMapping);
            Require(planCompilation.Success &&
                    planCompilation.CompiledModuleIdentities.SequenceEqual(
                        referenced.Plan.WorkItems.Select(item => item.Identity)),
                "Il piano multi-modulo non supera la validazione Roslyn: " +
                string.Join(", ", planCompilation.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            DdfDotNetArtifactEmissionResult firstEmission =
                DdfDotNetArtifactEmitter.Emit(referenced.Plan,
                    package.Language.Types.ClrMapping);
            DdfDotNetArtifactEmissionResult secondEmission =
                DdfDotNetArtifactEmitter.Emit(referenced.Plan,
                    package.Language.Types.ClrMapping);
            Require(firstEmission.Success && secondEmission.Success &&
                    firstEmission.Artifacts.Count == referenced.Plan.WorkItems.Count,
                "Il primo emit PE deterministico è fallito: " +
                string.Join(", ", firstEmission.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            List<DdfDotNetArtifactManifest> linkManifests = firstEmission
                .Artifacts.Select(artifact => artifact.Manifest).ToList();
            DdfDotNetArtifactLinkResult linkResult = DdfDotNetArtifactLinker.Link(
                referenced.Plan.EntryModuleIdentity, linkManifests);
            DdfDotNetArtifactLinkResult linkedReversed =
                DdfDotNetArtifactLinker.Link(
                    referenced.Plan.EntryModuleIdentity,
                    linkManifests.AsEnumerable().Reverse());
            Require(linkResult.Success && linkedReversed.Success &&
                    linkResult.Program.Fingerprint ==
                        linkedReversed.Program.Fingerprint &&
                    linkResult.Program.Artifacts.Select(artifact =>
                            artifact.Identity)
                        .SequenceEqual(referenced.Plan.WorkItems.Select(item =>
                            item.Identity)) &&
                    linkManifests.Single(manifest => manifest.ModuleId + "@" +
                            manifest.ModuleVersion ==
                            referenced.Plan.EntryModuleIdentity).Imports.Count > 0,
                "Il linker statico non ha risolto in modo deterministico il programma multi-modulo: " +
                string.Join(", ", linkResult.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));

            DdfDotNetArtifactManifest linkEntry = linkManifests.Single(manifest =>
                manifest.ModuleId + "@" + manifest.ModuleVersion ==
                referenced.Plan.EntryModuleIdentity);
            DdfDotNetDependencyDescriptor firstLinkedDependency =
                linkEntry.Dependencies.First();
            string firstLinkedIdentity = firstLinkedDependency.ModuleId + "@" +
                firstLinkedDependency.ModuleVersion;
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity,
                        linkManifests.Where(manifest => manifest.ModuleId + "@" +
                            manifest.ModuleVersion != firstLinkedIdentity))
                    .Issues.Any(issue => issue.Code == "DDFC502"),
                "Una dipendenza compilata mancante è stata collegata.");

            List<DdfDotNetArtifactManifest> missingExport = linkManifests
                .Select(CloneManifest).ToList();
            DdfDotNetImportDescriptor requiredImport = missingExport.Single(
                    manifest => manifest.ModuleId + "@" +
                        manifest.ModuleVersion ==
                        referenced.Plan.EntryModuleIdentity)
                .Imports.First(imported => imported.ModuleIdentity ==
                    firstLinkedIdentity);
            missingExport.Single(manifest => manifest.ModuleId + "@" +
                    manifest.ModuleVersion == firstLinkedIdentity)
                .Exports.RemoveAll(exported => exported.Name ==
                    requiredImport.Name);
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity, missingExport)
                    .Issues.Any(issue => issue.Code == "DDFC503"),
                "Un import privo del relativo export è stato collegato.");

            List<DdfDotNetArtifactManifest> incompatibleExport = linkManifests
                .Select(CloneManifest).ToList();
            incompatibleExport.Single(manifest => manifest.ModuleId + "@" +
                    manifest.ModuleVersion == firstLinkedIdentity)
                .Exports.Single(exported => exported.Name == requiredImport.Name)
                .Signature += "!";
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity, incompatibleExport)
                    .Issues.Any(issue => issue.Code == "DDFC503"),
                "Un export con firma incompatibile è stato collegato.");
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity,
                        linkManifests.Concat(new[] { linkManifests[0] }))
                    .Issues.Any(issue => issue.Code == "DDFC500"),
                "Un artefatto modulo duplicato è stato collegato.");
            List<DdfDotNetArtifactManifest> duplicateExport = linkManifests
                .Select(CloneManifest).ToList();
            DdfDotNetArtifactManifest duplicateExportProvider =
                duplicateExport.Single(manifest => manifest.ModuleId + "@" +
                    manifest.ModuleVersion == firstLinkedIdentity);
            DdfDotNetExportDescriptor duplicated =
                duplicateExportProvider.Exports.First();
            duplicateExportProvider.Exports.Add(new DdfDotNetExportDescriptor
            {
                Name = duplicated.Name,
                Kind = duplicated.Kind,
                Signature = duplicated.Signature
            });
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity, duplicateExport)
                    .Issues.Any(issue => issue.Code == "DDFC500"),
                "Un export duplicato è stato collegato.");

            List<DdfDotNetArtifactManifest> cyclic = linkManifests
                .Select(CloneManifest).ToList();
            DdfDotNetArtifactManifest cyclicDependency = cyclic.Single(
                manifest => manifest.ModuleId + "@" + manifest.ModuleVersion ==
                    firstLinkedIdentity);
            cyclicDependency.Dependencies.Add(new DdfDotNetDependencyDescriptor
            {
                ModuleId = linkEntry.ModuleId,
                ModuleVersion = linkEntry.ModuleVersion,
                CompilationFingerprint = linkEntry.CompilationFingerprint,
                AssemblyName = linkEntry.AssemblyName
            });
            Require(DdfDotNetArtifactLinker.Link(
                        referenced.Plan.EntryModuleIdentity, cyclic)
                    .Issues.Any(issue => issue.Code == "DDFC504"),
                "Un ciclo tra artefatti compilati è stato collegato.");

            DdfDotNetLinkedBundleResult firstBundle =
                DdfDotNetLinkedBundleBuilder.Create(linkResult.Program,
                    firstEmission, references);
            DdfDotNetLinkedBundleResult secondBundle =
                DdfDotNetLinkedBundleBuilder.Create(linkedReversed.Program,
                    secondEmission, references);
            Require(firstBundle.Success && secondBundle.Success &&
                    firstBundle.Bundle.Fingerprint ==
                        secondBundle.Bundle.Fingerprint &&
                    firstBundle.Bundle.Artifacts.Select(artifact =>
                            artifact.Identity).SequenceEqual(
                        referenced.Plan.WorkItems.Select(item => item.Identity)),
                "Il bundle collegato non è strutturalmente valido o deterministico: " +
                string.Join(", ", firstBundle.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            DdfDotNetContractLoadResult<DdfDotNetLinkedPackageManifest>
                packageManifest = DdfDotNetLinkedPackageContract.Deserialize(
                    Encoding.UTF8.GetString(firstBundle.Bundle
                        .GetPackageManifestBytes()));
            Require(packageManifest.Success &&
                    packageManifest.Value.ReferenceSetFingerprint ==
                        references.Fingerprint &&
                    packageManifest.Value.ManagedReferences.Count ==
                        references.References.Count &&
                    packageManifest.Value.ManagedReferences.All(reference =>
                        !reference.AssemblyIdentity.Contains('/') &&
                        !reference.AssemblyIdentity.Contains('\\')),
                "Il package non persiste una allowlist canonica e portabile.");
            packageManifest.Value.ManagedReferences[0].Sha256 =
                new string('0', 64);
            Require(DdfDotNetLinkedPackageContract.Validate(
                        packageManifest.Value).Any(issue =>
                        issue.Code == "DDFC516"),
                "Una allowlist persistita manomessa è stata accettata.");
            DdfDotNetPeVerificationResult verifiedPe =
                DdfDotNetPeVerifier.Verify(linkResult.Program,
                    firstEmission.Artifacts, references);
            Require(verifiedPe.Success && verifiedPe.MethodBodies > 0 &&
                    verifiedPe.Instructions > 0 &&
                    verifiedPe.TotalIlBytes > 0,
                "Il verifier non ha attraversato realmente i corpi IL.");
            Require(DdfDotNetPeVerifier.Verify(linkResult.Program,
                        firstEmission.Artifacts, references,
                        new DdfDotNetPeVerificationLimits(maxMethods: 1))
                    .Issues.Any(issue => issue.Code == "DDFC511"),
                "Il budget strutturale dei metadati non è stato applicato.");
            DdfDotNetReferenceSet restrictedReferences =
                DdfDotNetReferenceSet.Create(references.References
                    .Where(reference => !reference.AssemblyName.StartsWith(
                        "System.Runtime,", StringComparison.Ordinal))
                    .Select(reference => reference.Path));
            Require(DdfDotNetPeVerifier.Verify(linkResult.Program,
                        firstEmission.Artifacts, restrictedReferences)
                    .Issues.Any(issue => issue.Code == "DDFC510"),
                "Un reference set diverso o incompleto è stato accettato.");
            DdfDotNetRuntimeEnvironment loaderEnvironment = new(
                DdfDotNetArtifactHostKind.Standalone,
                linkEntry.TargetFramework, linkEntry.RuntimeAbiVersion,
                linkEntry.RuntimeIdentifier,
                linkResult.Program.RequiredHostCapabilities);
            DdfDotNetStandaloneLoadResult loadedProgram =
                DdfDotNetStandaloneLoader.Load(firstBundle.Bundle, references,
                    loaderEnvironment);
            Require(loadedProgram.Success &&
                    loadedProgram.Session.ModuleOrder.SequenceEqual(
                        linkResult.Program.Artifacts.Select(item =>
                            item.Identity)),
                "Il loader standalone non ha ricostruito il programma: " +
                string.Join(", ", loadedProgram.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            using DdfDotNetCooperativeDebugger loaderDebugger =
                loadedProgram.Session.CreateDebugger();
            var loaderOutput = new List<string>();
            DdfRuntimeExecutionResult loaderExecution = loadedProgram.Session
                .Execute(new DdfRuntimeContext(100000,
                    output: value => loaderOutput.Add(value)));
            Require(Convert.ToInt32(loaderExecution.Value) == 42 &&
                    loaderExecution.Instructions > 0 &&
                    loaderOutput.SequenceEqual(new[] { "42" }),
                "Il loader standalone source-free diverge dal risultato atteso.");
            bool secondExecutionRejected = false;
            try
            {
                loadedProgram.Session.Execute(new DdfRuntimeContext(100000));
            }
            catch (InvalidOperationException)
            {
                secondExecutionRejected = true;
            }
            Require(secondExecutionRejected,
                "La sessione monouso ha conservato stato tra due esecuzioni.");
            loadedProgram.Session.Dispose();
            Require(loadedProgram.Session.IsDisposed,
                "La sessione standalone non è stata scaricata esplicitamente.");
            Require(DdfDotNetStandaloneLoader.Load(firstBundle.Bundle,
                        references, new DdfDotNetRuntimeEnvironment(
                            DdfDotNetArtifactHostKind.Embedded,
                            linkEntry.TargetFramework,
                            linkEntry.RuntimeAbiVersion,
                            linkEntry.RuntimeIdentifier,
                            linkResult.Program.RequiredHostCapabilities))
                    .Issues.Any(issue => issue.Code == "DDFC520"),
                "Il loader compilato è stato esposto all'embedded.");
            string workerPath = RunnerPath();
            DdfDotNetWorkerRequest workerRequest =
                DdfDotNetWorkerProtocol.CreateRequest(firstBundle.Bundle,
                    references, loaderEnvironment, 100000,
                    "compiler-contract-worker");
            Require(workerRequest.AccessPolicy.FileSystemAccess ==
                        DdfDotNetWorkerFileSystemAccess.None &&
                    workerRequest.AccessPolicy.NetworkAccess ==
                        DdfDotNetWorkerNetworkAccess.None &&
                    workerRequest.AccessPolicy.RequireReducedUserToken ==
                        OperatingSystem.IsWindows() &&
                    workerRequest.AccessPolicy.AllowedBrokerCapabilities.Count ==
                        0,
                "La richiesta worker non usa la policy deny-all predefinita.");
            workerRequest.AccessPolicy.NetworkAccess =
                DdfDotNetWorkerNetworkAccess.BrokeredClient;
            DdfDotNetWorkerPackageResult unsupportedWorkerAccess =
                DdfDotNetWorkerProtocol.Import(workerRequest,
                    reducedUserTokenApplied: OperatingSystem.IsWindows());
            Require(!unsupportedWorkerAccess.Success &&
                    unsupportedWorkerAccess.Issues.Any(
                        issue => issue.Code == "DDFC536"),
                "Il worker ha accettato accesso rete senza broker disponibile.");
            workerRequest.AccessPolicy.NetworkAccess =
                DdfDotNetWorkerNetworkAccess.None;
            if (OperatingSystem.IsWindows())
            {
                DdfDotNetWorkerPackageResult unattestedWorker =
                    DdfDotNetWorkerProtocol.Import(workerRequest,
                        reducedUserTokenApplied: false);
                Require(!unattestedWorker.Success &&
                        unattestedWorker.Issues.Any(issue =>
                            issue.Code == "DDFC537"),
                    "Il worker ha accettato un token ridotto non attestato.");
            }
            DdfDotNetWorkerExecutionResult workerExecution =
                DdfDotNetWorkerClient.ExecuteAsync(workerPath, workerRequest,
                    new DdfDotNetWorkerResourceLimits(
                        TimeSpan.FromSeconds(30),
                        1024L * 1024 * 1024,
                        TimeSpan.FromSeconds(30))).GetAwaiter().GetResult();
            Require(workerExecution.Success &&
                    workerExecution.Response.ResultJson == "42" &&
                    workerExecution.Response.Instructions > 0 &&
                    workerExecution.Response.Output.SequenceEqual(
                        new[] { "42" }) &&
                    workerExecution.IsolatedWorkingDirectory &&
                    workerExecution.SanitizedEnvironment &&
                    (!OperatingSystem.IsWindows() ||
                     (workerExecution.OsResourceLimitsApplied &&
                      workerExecution.ReducedUserTokenApplied)),
                "Il worker isolato non ha eseguito il bundle: " +
                string.Join(", ", workerExecution.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            DdfDotNetWorkerExecutionResult protectedDirectoryWorker =
                DdfDotNetWorkerClient.ExecuteAsync(workerPath, workerRequest,
                    new DdfDotNetWorkerResourceLimits(
                        TimeSpan.FromSeconds(30),
                        1024L * 1024 * 1024,
                        TimeSpan.FromSeconds(30),
                        requireProtectedWorkingDirectory: true))
                    .GetAwaiter().GetResult();
            Require((protectedDirectoryWorker.Success &&
                     protectedDirectoryWorker
                         .WorkingDirectoryAccessControlApplied) ||
                    (!protectedDirectoryWorker.Success &&
                     protectedDirectoryWorker.Issues.Any(issue =>
                         issue.Code == "DDFC538")),
                "La directory ACL non è stata applicata né rifiutata fail-closed.");
            DdfDotNetWorkerExecutionResult timedOutWorker =
                DdfDotNetWorkerClient.ExecuteAsync(workerPath, workerRequest,
                    TimeSpan.FromMilliseconds(1)).GetAwaiter().GetResult();
            Require(timedOutWorker.TimedOut && timedOutWorker.Issues.Any(
                    issue => issue.Code == "DDFC531"),
                "Il worker oltre scadenza non è stato terminato.");
            DdfDotNetLinkedBinaryArtifact immutableBinary =
                firstBundle.Bundle.Artifacts[0];
            byte expectedFirstByte = immutableBinary.GetAssemblyBytes()[0];
            byte[] exposedCopy = immutableBinary.GetAssemblyBytes();
            exposedCopy[0] ^= 0xff;
            Require(immutableBinary.GetAssemblyBytes()[0] == expectedFirstByte,
                "Il bundle collegato espone un buffer binario modificabile.");
            byte savedBundlePdbByte =
                firstEmission.Artifacts[0].PortablePdbBytes[0];
            firstEmission.Artifacts[0].PortablePdbBytes[0] ^= 0xff;
            Require(DdfDotNetLinkedBundleBuilder.Create(linkResult.Program,
                        firstEmission, references).Issues.Any(issue =>
                        issue.Code == "DDFC506"),
                "Un PDB manomesso è entrato nel bundle collegato.");
            firstEmission.Artifacts[0].PortablePdbBytes[0] = savedBundlePdbByte;

            DdfDotNetEmittedArtifact invalidPeArtifact =
                secondEmission.Artifacts[0];
            byte savedPeByte = invalidPeArtifact.AssemblyBytes[0];
            DdfDotNetPayloadDescriptor invalidPePayload =
                invalidPeArtifact.Manifest.Payloads.Single(payload =>
                    payload.Kind == DdfDotNetPayloadKind.Assembly);
            string savedPeHash = invalidPePayload.Sha256;
            invalidPeArtifact.AssemblyBytes[0] ^= 0xff;
            invalidPePayload.Sha256 = DdfDotNetPayloadDescriptor.FromBytes(
                DdfDotNetPayloadKind.Assembly,
                Path.GetFileName(invalidPeArtifact.AssemblyPath),
                invalidPeArtifact.AssemblyBytes).Sha256;
            DdfDotNetArtifactLinkResult invalidPeProgram =
                DdfDotNetArtifactLinker.Link(
                    referenced.Plan.EntryModuleIdentity,
                    secondEmission.Artifacts.Select(artifact =>
                        artifact.Manifest));
            Require(invalidPeProgram.Success &&
                    DdfDotNetPeVerifier.Verify(invalidPeProgram.Program,
                            secondEmission.Artifacts, references).Issues.Any(issue =>
                            issue.Code == "DDFC507"),
                "Un PE strutturalmente corrotto è stato accettato.");
            invalidPeArtifact.AssemblyBytes[0] = savedPeByte;
            invalidPePayload.Sha256 = savedPeHash;

            int invalidOpcodeOffset = FirstMethodIlOffset(
                invalidPeArtifact.AssemblyBytes);
            byte savedOpcode = invalidPeArtifact.AssemblyBytes[
                invalidOpcodeOffset];
            invalidPeArtifact.AssemblyBytes[invalidOpcodeOffset] = 0x24;
            invalidPePayload.Sha256 = DdfDotNetPayloadDescriptor.FromBytes(
                DdfDotNetPayloadKind.Assembly,
                Path.GetFileName(invalidPeArtifact.AssemblyPath),
                invalidPeArtifact.AssemblyBytes).Sha256;
            DdfDotNetArtifactLinkResult invalidIlProgram =
                DdfDotNetArtifactLinker.Link(
                    referenced.Plan.EntryModuleIdentity,
                    secondEmission.Artifacts.Select(artifact =>
                        artifact.Manifest));
            Require(invalidIlProgram.Success &&
                    DdfDotNetPeVerifier.Verify(invalidIlProgram.Program,
                            secondEmission.Artifacts, references).Issues.Any(
                            issue => issue.Code == "DDFC512"),
                "Un opcode IL sconosciuto è stato accettato.");
            invalidPeArtifact.AssemblyBytes[invalidOpcodeOffset] = savedOpcode;
            invalidPePayload.Sha256 = savedPeHash;

            invalidPeArtifact.AssemblyBytes[invalidOpcodeOffset] = 0x26;
            invalidPePayload.Sha256 = DdfDotNetPayloadDescriptor.FromBytes(
                DdfDotNetPayloadKind.Assembly,
                Path.GetFileName(invalidPeArtifact.AssemblyPath),
                invalidPeArtifact.AssemblyBytes).Sha256;
            DdfDotNetArtifactLinkResult invalidStackProgram =
                DdfDotNetArtifactLinker.Link(
                    referenced.Plan.EntryModuleIdentity,
                    secondEmission.Artifacts.Select(artifact =>
                        artifact.Manifest));
            Require(invalidStackProgram.Success &&
                    DdfDotNetPeVerifier.Verify(invalidStackProgram.Program,
                            secondEmission.Artifacts, references).Issues.Any(
                            issue => issue.Code == "DDFC513"),
                "Un underflow dello stack IL è stato accettato.");
            invalidPeArtifact.AssemblyBytes[invalidOpcodeOffset] = savedOpcode;
            invalidPePayload.Sha256 = savedPeHash;

            (int invalidLocalOffset, byte invalidLocalOpcode) =
                FirstInvalidLocalOpcode(
                    invalidPeArtifact.AssemblyBytes);
            byte savedLocalOpcode = invalidPeArtifact.AssemblyBytes[
                invalidLocalOffset];
            invalidPeArtifact.AssemblyBytes[invalidLocalOffset] =
                invalidLocalOpcode;
            invalidPePayload.Sha256 = DdfDotNetPayloadDescriptor.FromBytes(
                DdfDotNetPayloadKind.Assembly,
                Path.GetFileName(invalidPeArtifact.AssemblyPath),
                invalidPeArtifact.AssemblyBytes).Sha256;
            DdfDotNetArtifactLinkResult invalidSignatureProgram =
                DdfDotNetArtifactLinker.Link(
                    referenced.Plan.EntryModuleIdentity,
                    secondEmission.Artifacts.Select(artifact =>
                        artifact.Manifest));
            Require(invalidSignatureProgram.Success &&
                    DdfDotNetPeVerifier.Verify(
                            invalidSignatureProgram.Program,
                            secondEmission.Artifacts, references).Issues.Any(
                            issue => issue.Code == "DDFC517"),
                "Un indice locale fuori dalla firma è stato accettato.");
            invalidPeArtifact.AssemblyBytes[invalidLocalOffset] =
                savedLocalOpcode;
            invalidPePayload.Sha256 = savedPeHash;
            var compiledOutput = new List<string>();
            DdfRuntimeExecutionResult compiledResult = ExecuteForVerification(
                referenced.Plan, firstEmission, compiledOutput);
            Require(Convert.ToInt32(compiledResult.Value) == 42 &&
                    !compiledResult.Ended && compiledResult.Instructions > 0 &&
                    compiledOutput.SequenceEqual(new[] { "42" }),
                "Il primo programma multi-modulo compilato diverge dal risultato DDF atteso.");
            DdfDotNetCompilationWorkItem mathItem = referenced.Plan.WorkItems
                .Single(item => item.Identity == "ddf.sample.math@1.0.0");
            DdfIrFunction addFunction = mathItem.BoundUnit.Documents
                .SelectMany(document => document.StructuralProgram.Members)
                .OfType<DdfIrFunction>().Single(function =>
                    function.Name == "add");
            int addBreakpointStart = addFunction.Body.Statements[0].Start;
            DdfIrModule vmMath = prepared.Artifact.RuntimeModules.Single(module =>
                module.ModuleId == "ddf.sample.math" &&
                module.RelativePath == "math.ddf");
            using var vmDebugger = new DdfDebuggerSession();
            using var vmPauseReached = new ManualResetEventSlim(false);
            DdfDebugPauseInfo vmPause = null;
            vmDebugger.SetBreakpoints(new[]
            {
                new DdfSourceBreakpoint(vmMath.DocumentPath,
                    LineAt(vmMath.Source, addBreakpointStart))
            });
            vmDebugger.Paused = pause =>
            {
                vmPause = pause;
                vmPauseReached.Set();
            };
            var vmOutput = new List<string>();
            Task<DdfExecutionResult> vmRun = Task.Run(() =>
                DdfInterpreter.Execute(prepared.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        DebuggerSession = vmDebugger,
                        Output = value => vmOutput.Add(value)
                    }));
            Require(vmPauseReached.Wait(TimeSpan.FromSeconds(5)),
                "La VM non ha raggiunto il breakpoint differenziale.");
            using (var debugger = new DdfDotNetCooperativeDebugger(
                       firstEmission.Artifacts.Select(item => item.SourceMap)))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfDotNetSourceBreakpoint("math.ddf",
                        addBreakpointStart)
                });
                var debugOutput = new List<string>();
                Task<DdfRuntimeExecutionResult> debugRun = Task.Run(() =>
                    ExecuteForVerification(referenced.Plan, firstEmission,
                        debugOutput, debugger));
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)),
                    "Il runner compilato non si è fermato sul breakpoint DDF.");
                DdfDotNetCompiledPause breakpointPause = debugger.CurrentPause;
                Require(breakpointPause.Location.DocumentPath == "math.ddf" &&
                        breakpointPause.Location.Start == addBreakpointStart &&
                        breakpointPause.Frames.Select(frame => frame.Name)
                            .SequenceEqual(new[] { "add", "main" }),
                    "La pausa compilata non conserva posizione o Call Stack DDF.");
                DdfDotNetCompiledFrame addFrame = breakpointPause.Frames[0];
                Require(addFrame.Variables.Any(variable =>
                            variable.Name == "left" &&
                            variable.TypeName == "int" &&
                            variable.DisplayValue == "10" &&
                            variable.Kind == DdfRuntimeDebugVariableKind.Parameter) &&
                        addFrame.Variables.Any(variable =>
                            variable.Name == "right" &&
                            variable.DisplayValue == "11" &&
                            variable.Kind == DdfRuntimeDebugVariableKind.Parameter) &&
                        addFrame.Variables.Any(variable =>
                            variable.Name == "offset" &&
                            variable.DisplayValue == "1" &&
                            variable.Kind == DdfRuntimeDebugVariableKind.Global) &&
                        addFrame.Variables.Count(variable =>
                            variable.Name == "offset") == 1 &&
                        addFrame.Variables.All(variable =>
                            !variable.Name.StartsWith("DdfGenerated_",
                                StringComparison.Ordinal)),
                    "Parametri e globali compilati non sono proiettati come simboli DDF.");
                Require(DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                            breakpointPause, firstEmission.Artifacts.Select(
                                item => item.SourceMap)).Count == 0,
                    "La pausa al breakpoint diverge tra VM e Roslyn: " +
                    string.Join("; ", DdfDotNetDifferentialDebugTrace.Compare(
                        vmPause, breakpointPause, firstEmission.Artifacts.Select(
                            item => item.SourceMap))));
                DdfDebugPauseInfo breakpointVmPause = vmPause;
                vmPauseReached.Reset();
                vmDebugger.StepInto();
                debugger.Resume(DdfDotNetDebugCommand.StepInto);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)),
                    "Step Into compilato non ha raggiunto la funzione chiamata.");
                Require(vmPauseReached.Wait(TimeSpan.FromSeconds(5)),
                    "Step Into VM non ha raggiunto la funzione chiamata.");
                DdfDotNetCompiledPause stepPause = debugger.CurrentPause;
                Require(stepPause.Frames.Count == 3 &&
                        stepPause.Frames[0].Name == "hiddenOffset" &&
                        stepPause.Frames[1].Name == "add" &&
                        stepPause.Frames[2].Name == "main",
                    "Step Into compilato non ricostruisce lo stack annidato DDF.");
                Require(DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                            stepPause, firstEmission.Artifacts.Select(
                                item => item.SourceMap)).Count == 0,
                    "Step Into diverge tra VM e Roslyn: " + string.Join("; ",
                        DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                            stepPause, firstEmission.Artifacts.Select(
                                item => item.SourceMap))));
                Require(DdfDotNetDifferentialDebugTrace.Compare(
                            breakpointVmPause, stepPause,
                            firstEmission.Artifacts.Select(item => item.SourceMap))
                        .Count > 0,
                    "Il comparatore differenziale non rileva una traccia volutamente diversa.");
                vmPauseReached.Reset();
                vmDebugger.StepOver();
                debugger.Resume(DdfDotNetDebugCommand.StepOver);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames[0].Variables.Any(variable =>
                            variable.Name == "box" &&
                            variable.TypeName == "Box" &&
                            variable.DisplayValue == "{Box}" &&
                            variable.Children.Single().Name == "value" &&
                            variable.Children.Single().DisplayValue == "0"),
                    "Lo snapshot compilato non espone il locale DDF inizializzato.");
                Require(vmPauseReached.Wait(TimeSpan.FromSeconds(5)) &&
                        DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                            debugger.CurrentPause, firstEmission.Artifacts.Select(
                                item => item.SourceMap)).Count == 0,
                    "Il primo Step Over diverge tra VM e Roslyn.");
                vmPauseReached.Reset();
                vmDebugger.StepOver();
                debugger.Resume(DdfDotNetDebugCommand.StepOver);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames[0].Variables.Single(variable =>
                            variable.Name == "box").Children.Single().DisplayValue ==
                        "1",
                    "Il campo della struttura DDF non si aggiorna dopo l'assegnazione.");
                Require(vmPauseReached.Wait(TimeSpan.FromSeconds(5)) &&
                        DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                            debugger.CurrentPause, firstEmission.Artifacts.Select(
                                item => item.SourceMap)).Count == 0,
                    "Il secondo Step Over diverge tra VM e Roslyn.");
                vmDebugger.Continue();
                debugger.Resume(DdfDotNetDebugCommand.Continue);
                Require(debugRun.Wait(TimeSpan.FromSeconds(5)) &&
                        Convert.ToInt32(debugRun.Result.Value) == 42 &&
                        debugOutput.SequenceEqual(new[] { "42" }),
                    "Continua non completa correttamente il programma compilato.");
                Require(vmRun.Wait(TimeSpan.FromSeconds(5)) &&
                        vmRun.Result.Succeeded &&
                        Convert.ToInt32(vmRun.Result.ReturnValue) == 42 &&
                        vmOutput.SequenceEqual(new[] { "42" }),
                    "La VM non completa il percorso differenziale con risultato 42.");
            }
            DdfIrFunction mainFunction = entry.BoundUnit.Documents
                .SelectMany(document => document.StructuralProgram.Members)
                .OfType<DdfIrFunction>().Single(function =>
                    function.Name == "main");
            int mainCallStart = mainFunction.Body.Statements[0].Start;
            using (var debugger = new DdfDotNetCooperativeDebugger(
                       firstEmission.Artifacts.Select(item => item.SourceMap)))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfDotNetSourceBreakpoint("main.ddf", mainCallStart)
                });
                Task<DdfRuntimeExecutionResult> debugRun = Task.Run(() =>
                    ExecuteForVerification(referenced.Plan, firstEmission,
                        new List<string>(), debugger));
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Location.DocumentPath == "main.ddf" &&
                        debugger.CurrentPause.Frames[0].Name == "main",
                    "Il breakpoint sulla chiamata non ha fermato il chiamante.");
                debugger.Resume(DdfDotNetDebugCommand.StepInto);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Location.DocumentPath == "math.ddf" &&
                        debugger.CurrentPause.Frames.Count == 2 &&
                        debugger.CurrentPause.Frames[0].Name == "add" &&
                        debugger.CurrentPause.Frames[1].Name == "main",
                    "F11 da un breakpoint sulla chiamata si è fermato di nuovo " +
                    "nel chiamante invece di entrare nel file esterno.");
                debugger.Resume(DdfDotNetDebugCommand.Continue);
                Require(debugRun.Wait(TimeSpan.FromSeconds(5)) &&
                        Convert.ToInt32(debugRun.Result.Value) == 42,
                    "Il runner non termina dopo lo Step Into multifile.");
            }
            int mainOutputStart = mainFunction.Body.Statements[1].Start;
            using (var debugger = new DdfDotNetCooperativeDebugger(
                       firstEmission.Artifacts.Select(item => item.SourceMap)))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfDotNetSourceBreakpoint("main.ddf", mainOutputStart)
                });
                Task<DdfRuntimeExecutionResult> debugRun = Task.Run(() =>
                    ExecuteForVerification(referenced.Plan, firstEmission,
                        new List<string>(), debugger));
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames[0].Name == "main" &&
                        debugger.CurrentPause.Frames[0].Variables.Any(variable =>
                            variable.Name == "total" &&
                            variable.TypeName == "int" &&
                            variable.DisplayValue == "42" &&
                            variable.Kind == DdfRuntimeDebugVariableKind.Local),
                    "Il debugger compilato non espone il locale total = 42.");
                debugger.Resume(DdfDotNetDebugCommand.Continue);
                Require(debugRun.Wait(TimeSpan.FromSeconds(5)) &&
                        Convert.ToInt32(debugRun.Result.Value) == 42,
                    "Il runner non termina dopo la verifica del locale compilato.");
            }
            using (var debugger = new DdfDotNetCooperativeDebugger(
                       firstEmission.Artifacts.Select(item => item.SourceMap)))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfDotNetSourceBreakpoint("math.ddf",
                        addBreakpointStart)
                });
                Task<DdfRuntimeExecutionResult> debugRun = Task.Run(() =>
                    ExecuteForVerification(referenced.Plan, firstEmission,
                        new List<string>(), debugger));
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)),
                    "Il secondo runner non ha raggiunto il breakpoint DDF.");
                debugger.Resume(DdfDotNetDebugCommand.StepOver);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames.Count == 1 &&
                        debugger.CurrentPause.Frames[0].Name == "main",
                    "Step Over è entrato in una funzione sorella invece di tornare al chiamante.");
                debugger.Resume(DdfDotNetDebugCommand.Continue);
                Require(debugRun.Wait(TimeSpan.FromSeconds(5)) &&
                        Convert.ToInt32(debugRun.Result.Value) == 42,
                    "Il runner non termina dopo Step Over e Continua.");
            }
            DdfIrFunction hiddenFunction = mathItem.BoundUnit.Documents
                .SelectMany(document => document.StructuralProgram.Members)
                .OfType<DdfIrFunction>().Single(function =>
                    function.Name == "hiddenOffset");
            using var stepOutVmDebugger = new DdfDebuggerSession();
            using var stepOutVmReached = new ManualResetEventSlim(false);
            DdfDebugPauseInfo stepOutVmPause = null;
            stepOutVmDebugger.SetBreakpoints(new[]
            {
                new DdfSourceBreakpoint(vmMath.DocumentPath,
                    LineAt(vmMath.Source,
                        hiddenFunction.Body.Statements[0].Start))
            });
            stepOutVmDebugger.Paused = pause =>
            {
                stepOutVmPause = pause;
                stepOutVmReached.Set();
            };
            Task<DdfExecutionResult> stepOutVmRun = Task.Run(() =>
                DdfInterpreter.Execute(prepared.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        DebuggerSession = stepOutVmDebugger
                    }));
            Require(stepOutVmReached.Wait(TimeSpan.FromSeconds(5)),
                "La VM non ha raggiunto il breakpoint annidato per Step Out.");
            using (var debugger = new DdfDotNetCooperativeDebugger(
                       firstEmission.Artifacts.Select(item => item.SourceMap)))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfDotNetSourceBreakpoint("math.ddf",
                        hiddenFunction.Body.Statements[0].Start)
                });
                Task<DdfRuntimeExecutionResult> debugRun = Task.Run(() =>
                    ExecuteForVerification(referenced.Plan, firstEmission,
                        new List<string>(), debugger));
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames.Count == 3,
                    "Il runner non ha raggiunto il breakpoint annidato.");
                Require(DdfDotNetDifferentialDebugTrace.Compare(stepOutVmPause,
                            debugger.CurrentPause,
                            firstEmission.Artifacts.Select(item => item.SourceMap))
                        .Count == 0,
                    "La pausa annidata diverge tra VM e Roslyn.");
                stepOutVmReached.Reset();
                stepOutVmDebugger.StepOut();
                debugger.Resume(DdfDotNetDebugCommand.StepOut);
                Require(debugger.WaitForPause(TimeSpan.FromSeconds(5)) &&
                        debugger.CurrentPause.Frames.Count < 3,
                    "Step Out non è uscito dal frame annidato.");
                Require(stepOutVmReached.Wait(TimeSpan.FromSeconds(5)) &&
                        DdfDotNetDifferentialDebugTrace.Compare(stepOutVmPause,
                            debugger.CurrentPause,
                            firstEmission.Artifacts.Select(item => item.SourceMap))
                        .Count == 0,
                    "Step Out diverge tra VM e Roslyn.");
                stepOutVmDebugger.Continue();
                Require(stepOutVmRun.Wait(TimeSpan.FromSeconds(5)) &&
                        stepOutVmRun.Result.Succeeded &&
                        Convert.ToInt32(stepOutVmRun.Result.ReturnValue) == 42,
                    "La VM non termina dopo Step Out differenziale.");
                debugger.Resume(DdfDotNetDebugCommand.Stop);
                Require(SpinWait.SpinUntil(() => debugRun.IsCompleted,
                            TimeSpan.FromSeconds(5)) &&
                        (debugRun.IsCanceled || debugRun.IsFaulted &&
                            debugRun.Exception?.InnerException is
                                OperationCanceledException),
                    "Stop non ha cancellato il runner compilato in pausa.");
            }
            for (int index = 0; index < firstEmission.Artifacts.Count; index++)
            {
                DdfDotNetEmittedArtifact artifact = firstEmission.Artifacts[index];
                DdfDotNetEmittedArtifact repeated = secondEmission.Artifacts[index];
                Require(artifact.ModuleIdentity == repeated.ModuleIdentity &&
                        artifact.AssemblyBytes.SequenceEqual(repeated.AssemblyBytes) &&
                        artifact.PortablePdbBytes.SequenceEqual(
                            repeated.PortablePdbBytes) &&
                        artifact.SourceMapBytes.SequenceEqual(
                            repeated.SourceMapBytes) &&
                        artifact.ManifestBytes.SequenceEqual(repeated.ManifestBytes),
                    "L'emit ripetuto non è byte-per-byte deterministico.");
                using var pe = new PEReader(new MemoryStream(
                    artifact.AssemblyBytes, writable: false));
                Require(pe.HasMetadata && artifact.AssemblyBytes.Length > 2 &&
                        artifact.AssemblyBytes[0] == 0x4d &&
                        artifact.AssemblyBytes[1] == 0x5a,
                    "Il payload emesso non è un PE gestito valido.");
                using MetadataReaderProvider pdbProvider =
                    MetadataReaderProvider.FromPortablePdbStream(
                        new MemoryStream(artifact.PortablePdbBytes,
                            writable: false));
                MetadataReader pdb = pdbProvider.GetMetadataReader();
                var pdbDocumentNames = pdb.Documents.Select(handle =>
                    pdb.GetString(pdb.GetDocument(handle).Name)).ToHashSet(
                        StringComparer.Ordinal);
                var pdbLocalNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (LocalScopeHandle scopeHandle in pdb.LocalScopes)
                {
                    LocalScope scope = pdb.GetLocalScope(scopeHandle);
                    foreach (LocalVariableHandle variableHandle in
                             scope.GetLocalVariables())
                        pdbLocalNames.Add(pdb.GetString(
                            pdb.GetLocalVariable(variableHandle).Name));
                }
                int mappedDdfPoints = 0;
                int exposedGeneratedPoints = 0;
                foreach (MethodDebugInformationHandle handle in
                         pdb.MethodDebugInformation)
                {
                    MethodDebugInformation debug =
                        pdb.GetMethodDebugInformation(handle);
                    DocumentHandle currentDocument = debug.Document;
                    foreach (SequencePoint point in debug.GetSequencePoints())
                    {
                        if (!point.Document.IsNil)
                            currentDocument = point.Document;
                        if (point.IsHidden || currentDocument.IsNil) continue;
                        string documentName = pdb.GetString(
                            pdb.GetDocument(currentDocument).Name);
                        if (documentName.EndsWith(".ddf",
                                StringComparison.OrdinalIgnoreCase))
                            mappedDdfPoints++;
                        else
                            exposedGeneratedPoints++;
                    }
                }
                Require(pdb.Documents.Count > 0 &&
                        artifact.SourceMap.Documents.Any(document =>
                            pdbDocumentNames.Contains(document.SourcePath)) &&
                        mappedDdfPoints > 0 && exposedGeneratedPoints == 0 &&
                        artifact.SourceMap.Documents.Count ==
                            referenced.Plan.WorkItems[index].Unit.Sources.Count &&
                        artifact.SourceMap.Documents.Any(document =>
                            document.SequencePoints.Count > 0) &&
                        DdfDotNetArtifactContract.ValidateBundle(
                            artifact.Manifest, artifact.SourceMap).Count == 0,
                    "Portable PDB o source map DDF non sono validi.");
                DdfDotNetCompilationWorkItem debugItem =
                    referenced.Plan.WorkItems[index];
                IReadOnlyList<DdfBoundSymbol> expectedDebugSymbols =
                    debugItem.BoundUnit.Symbols.Where(symbol =>
                        symbol.ModuleIdentity == debugItem.Identity &&
                        debugItem.Unit.Sources.Any(source =>
                            source.RelativePath == symbol.RelativePath) &&
                        symbol.Length > 0).ToList();
                Require(artifact.SourceMap.Symbols.Count ==
                            expectedDebugSymbols.Count &&
                        expectedDebugSymbols.All(symbol =>
                            artifact.SourceMap.Symbols.Any(mapped =>
                                mapped.SymbolId == symbol.Id &&
                                mapped.Name == symbol.Name &&
                                mapped.Kind == symbol.Kind.ToString() &&
                                mapped.Type == symbol.Type.DisplayName &&
                                mapped.SourcePath == symbol.RelativePath &&
                                mapped.DdfStart == symbol.Start &&
                                mapped.DdfLength == symbol.Length &&
                                mapped.StorageName == debugItem.GeneratedNames
                                    .GetIdentifier(symbol.Id) &&
                                mapped.IsWritable == symbol.IsWritable)) &&
                        artifact.SourceMap.Symbols.All(mapped =>
                            mapped.StorageName != mapped.Name &&
                            mapped.StorageName.StartsWith("DdfGenerated_",
                                StringComparison.Ordinal)),
                    "La mappa dei simboli debug non conserva identità, nomi DDF, tipi e storage CLR.");
                Require(pdbLocalNames.Where(storageName => storageName.StartsWith(
                            "DdfGenerated_", StringComparison.Ordinal)).All(storageName =>
                            artifact.SourceMap.Symbols.Any(mapped =>
                                mapped.Kind == DdfBoundSymbolKind.Local.ToString() &&
                                mapped.StorageName == storageName)),
                    "Il Portable PDB contiene storage DDF non traducibile in simboli DDF.");
                DdfDotNetDebugAdapterResult adapterResult =
                    DdfDotNetDebugAdapter.Create(artifact);
                Require(adapterResult.Success &&
                        adapterResult.Adapter.Frames.Count > 0,
                    "L'adapter non ha ricostruito i frame DDF da PE e PDB: " +
                    string.Join(", ", adapterResult.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                foreach (DdfDotNetDebugFrameInfo frame in
                         adapterResult.Adapter.Frames)
                {
                    Require(!frame.Function.Name.StartsWith("DdfGenerated_",
                                StringComparison.Ordinal) &&
                            frame.SequencePoints.Count > 0,
                        "Un frame espone un nome CLR o non possiede punti DDF.");
                    DdfDotNetDebugLocation firstPoint = frame.SequencePoints[0];
                    DdfDotNetDebugLocation bound = adapterResult.Adapter
                        .BindBreakpoint(firstPoint.SourcePath, firstPoint.Line);
                    Require(bound != null && bound.MethodToken ==
                            firstPoint.MethodToken && bound.Line == firstPoint.Line,
                        "Il breakpoint DDF non è stato legato alla sequence point prevista.");
                    Require(adapterResult.Adapter.GetVisibleSymbols(
                                frame.MethodToken, firstPoint.IlOffset).All(symbol =>
                                !symbol.Name.StartsWith("DdfGenerated_",
                                    StringComparison.Ordinal)),
                        "Variabili o parametri espongono nomi CLR generati.");
                }
                Require(artifact.Manifest.BuildReceipt.CompilationPlanFingerprint ==
                            referenced.Plan.Fingerprint &&
                        artifact.Manifest.BuildReceipt.ReferenceSetFingerprint ==
                            references.Fingerprint &&
                        DdfDotNetArtifactContract.Validate(artifact.Manifest).Count == 0,
                    "Manifest o ricevuta del compilatore non sono coerenti.");
                DdfDotNetCompilationWorkItem emittedItem =
                    referenced.Plan.WorkItems[index];
                Equal(emittedItem.DependencyIdentities.Count,
                    artifact.Manifest.Dependencies.Count);
                Equal(DdfDotNetPayloadDescriptor.FromBytes(
                        DdfDotNetPayloadKind.Assembly, "payload.dll",
                        artifact.AssemblyBytes).Sha256,
                    artifact.Manifest.Payloads.Single(payload => payload.Kind ==
                        DdfDotNetPayloadKind.Assembly).Sha256);
                Equal(3, artifact.Manifest.Payloads.Count);
            }
            IReadOnlyList<DdfDotNetDebugAdapter> debugAdapters =
                firstEmission.Artifacts.Select(artifact =>
                    DdfDotNetDebugAdapter.Create(artifact).Adapter).ToList();
            Require(debugAdapters.SelectMany(adapter => adapter.Frames)
                    .Select(frame => frame.Function.Name).ToHashSet(
                        StringComparer.Ordinal).IsSupersetOf(new[]
                        { "main", "add", "scale", "hiddenOffset" }),
                "Il Call Stack DDF non è ricostruibile dal bundle.");
            byte savedPdbByte = secondEmission.Artifacts[0].PortablePdbBytes[0];
            secondEmission.Artifacts[0].PortablePdbBytes[0] ^= 0xff;
            Require(!DdfDotNetDebugAdapter.Create(
                    secondEmission.Artifacts[0]).Success,
                "L'adapter ha accettato un Portable PDB manomesso.");
            secondEmission.Artifacts[0].PortablePdbBytes[0] = savedPdbByte;
            string publicationParent = Path.Combine(Path.GetTempPath(),
                "ddf-dotnet-publish-" + Guid.NewGuid().ToString("N"));
            string publicationRoot = Path.Combine(publicationParent, "bundle");
            try
            {
                DdfDotNetArtifactPublicationResult publication =
                    DdfDotNetArtifactPublisher.Publish(firstEmission,
                        publicationRoot);
                Require(publication.Success &&
                        publication.PublishedPaths.Count ==
                            firstEmission.Artifacts.Count * 4,
                    "La pubblicazione atomica del bundle è fallita: " +
                    string.Join(", ", publication.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                foreach (DdfDotNetEmittedArtifact artifact in
                         firstEmission.Artifacts)
                {
                    string assemblyPath = Path.Combine(publicationRoot,
                        artifact.AssemblyPath.Replace('/',
                            Path.DirectorySeparatorChar));
                    string manifestPath = Path.Combine(publicationRoot,
                        artifact.ManifestPath.Replace('/',
                            Path.DirectorySeparatorChar));
                    string pdbPath = Path.Combine(publicationRoot,
                        artifact.PortablePdbPath.Replace('/',
                            Path.DirectorySeparatorChar));
                    string mapPath = Path.Combine(publicationRoot,
                        artifact.SourceMapPath.Replace('/',
                            Path.DirectorySeparatorChar));
                    Require(File.ReadAllBytes(assemblyPath).SequenceEqual(
                                artifact.AssemblyBytes) &&
                            File.ReadAllBytes(pdbPath).SequenceEqual(
                                artifact.PortablePdbBytes) &&
                            File.ReadAllBytes(mapPath).SequenceEqual(
                                artifact.SourceMapBytes) &&
                            File.ReadAllBytes(manifestPath).SequenceEqual(
                                artifact.ManifestBytes) &&
                            DdfDotNetArtifactCompatibility.VerifyPayloads(
                                artifact.Manifest,
                                Path.GetDirectoryName(manifestPath)).IsCompatible,
                        "Il bundle pubblicato non corrisponde all'emit in memoria.");
                }
                Require(DdfDotNetArtifactPublisher.Publish(firstEmission,
                            publicationRoot).Issues.Any(issue =>
                            issue.Code == "DDFC409"),
                    "Una destinazione esistente è stata sovrascritta.");

                secondEmission.Artifacts[0].AssemblyBytes[0] ^= 0xff;
                string rejectedRoot = Path.Combine(publicationParent,
                    "tampered");
                Require(DdfDotNetArtifactPublisher.Publish(secondEmission,
                            rejectedRoot).Issues.Any(issue =>
                            issue.Code == "DDFC410") &&
                        !Directory.Exists(rejectedRoot),
                    "Un emit manomesso è stato pubblicato o ha lasciato residui.");
                Require(!Directory.GetDirectories(publicationParent,
                        ".*.ddf-tmp-*", SearchOption.TopDirectoryOnly).Any(),
                    "La pubblicazione ha lasciato directory di staging.");
            }
            finally
            {
                if (Directory.Exists(publicationParent))
                    Directory.Delete(publicationParent, true);
            }
            string cacheParent = Path.Combine(Path.GetTempPath(),
                "ddf-dotnet-cache-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfDotNetArtifactCacheResult cacheMiss =
                    DdfDotNetArtifactCache.GetOrEmit(referenced.Plan,
                        package.Language.Types.ClrMapping, cacheParent);
                Require(cacheMiss.Success && !cacheMiss.CacheHit &&
                        !cacheMiss.Invalidated &&
                        cacheMiss.CacheKey.Length == 64 &&
                        Directory.Exists(cacheMiss.EntryPath) &&
                        cacheMiss.Emission.Artifacts.Count ==
                            firstEmission.Artifacts.Count,
                    "Il primo popolamento della cache compilata è fallito: " +
                    string.Join(", ", cacheMiss.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                DdfDotNetArtifactCacheResult cacheHit =
                    DdfDotNetArtifactCache.GetOrEmit(referenced.Plan,
                        package.Language.Types.ClrMapping, cacheParent);
                Require(cacheHit.Success && cacheHit.CacheHit &&
                        !cacheHit.Invalidated &&
                        cacheHit.CacheKey == cacheMiss.CacheKey &&
                        cacheHit.Emission.Artifacts.Zip(
                            firstEmission.Artifacts).All(pair =>
                            pair.First.AssemblyBytes.SequenceEqual(
                                pair.Second.AssemblyBytes) &&
                            pair.First.PortablePdbBytes.SequenceEqual(
                                pair.Second.PortablePdbBytes) &&
                            pair.First.SourceMapBytes.SequenceEqual(
                                pair.Second.SourceMapBytes) &&
                            pair.First.ManifestBytes.SequenceEqual(
                                pair.Second.ManifestBytes)),
                    "Un hit della cache non restituisce l'emit deterministico atteso.");

                DdfDotNetEmittedArtifact cachedArtifact =
                    cacheHit.Emission.Artifacts[0];
                string cachedAssemblyPath = Path.Combine(cacheHit.EntryPath,
                    cachedArtifact.AssemblyPath.Replace('/',
                        Path.DirectorySeparatorChar));
                byte[] corruptedAssembly = File.ReadAllBytes(cachedAssemblyPath);
                corruptedAssembly[0] ^= 0xff;
                File.WriteAllBytes(cachedAssemblyPath, corruptedAssembly);
                DdfDotNetArtifactCacheResult rebuilt =
                    DdfDotNetArtifactCache.GetOrEmit(referenced.Plan,
                        package.Language.Types.ClrMapping, cacheParent);
                Require(rebuilt.Success && !rebuilt.CacheHit &&
                        rebuilt.Invalidated &&
                        rebuilt.Emission.Artifacts[0].AssemblyBytes.SequenceEqual(
                            firstEmission.Artifacts[0].AssemblyBytes) &&
                        !Directory.GetDirectories(
                            Path.GetDirectoryName(rebuilt.EntryPath),
                            ".*.ddf-stale-*", SearchOption.TopDirectoryOnly).Any() &&
                        !Directory.GetDirectories(
                            Path.GetDirectoryName(rebuilt.EntryPath),
                            ".*.ddf-tmp-*", SearchOption.TopDirectoryOnly).Any(),
                    "La voce cache corrotta non è stata invalidata e sostituita atomicamente.");
                Require(DdfDotNetArtifactCache.GetOrEmit(referenced.Plan,
                            package.Language.Types.ClrMapping, "cache-relativa")
                        .Issues.Any(issue => issue.Code == "DDFC412"),
                    "Una radice cache relativa è stata accettata.");
            }
            finally
            {
                if (Directory.Exists(cacheParent))
                    Directory.Delete(cacheParent, true);
            }
            Require(!DdfDotNetCompilationPlanner.Create(
                    DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                        "TEST", "preparazione fallita")), options).Success,
                "Una preparazione Core fallita è stata accettata.");
            Require(DdfDotNetCompilationPlanner.Create(prepared,
                    new DdfDotNetCompilationOptions("net10.0", "2.0.0"))
                    .Issues.Any(item => item.Code == "DDFC307"),
                "Una runtime ABI non implementata è stata accettata.");
            Console.WriteLine("PASS emit, cache, linker, allowlist, verifier IL e bundle deterministici; esecuzione confinata");
        }

        private static void MatchesVmOnDifferentialExecutionCorpus()
        {
            var cases = new (string Name, string Source, string Input)[]
            {
                ("control-flow",
                    "main() out int\n{\n" +
                    "    int total << 0;\n" +
                    "    for(int index << 0; index < 4; index++)\n" +
                    "    {\n        total << total + index;\n    }\n" +
                    "    while(total < 8)\n    {\n        total++;\n    }\n" +
                    "    do\n    {\n        total++;\n    } while(total < 9);\n" +
                    "    if(total == 9)\n    {\n        total >> Console;\n    }\n" +
                    "    ret total;\n}\n", null),
                ("continue",
                    "main() out int\n{\n    int total << 0;\n" +
                    "    for(int index << 0; index < 6; index++)\n    {\n" +
                    "        if(index == 2) { continue; }\n" +
                    "        total << total + index;\n    }\n" +
                    "    ret total;\n}\n", null),
                ("nested-control-transfers",
                    "main() out int\n{\n    int total << 0;\n" +
                    "    for(int outer << 0; outer < 3; outer++)\n    {\n" +
                    "        int inner << 0;\n        while(inner < 4)\n        {\n" +
                    "            inner++;\n            if(inner == 1) { continue; }\n" +
                    "            if(outer == 1 && inner == 2) { brk; }\n" +
                    "            total++;\n        }\n        int probe << 0;\n" +
                    "        do\n        {\n            probe++;\n" +
                    "            if(probe == 1) { continue; }\n" +
                    "            if(probe == 2) { brk; }\n        } while(probe < 3);\n" +
                    "        if(outer == 0) { continue; }\n        select(outer)\n        {\n" +
                    "            when 2 { if(total > 0) { ret total + 35; } }\n" +
                    "            otherwise { total++; }\n        }\n    }\n" +
                    "    ret 4000;\n}\n", null),
                ("nested-end",
                    "main() out int\n{\n    for(int outer << 0; outer < 2; outer++)\n    {\n" +
                    "        int inner << 0;\n        while(inner < 2)\n        {\n" +
                    "            inner++;\n            select(inner)\n            {\n" +
                    "                when 1 { if(outer == 1) { end; } }\n" +
                    "                otherwise { }\n            }\n        }\n    }\n" +
                    "    ret 0;\n}\n", null),
                ("nominal-enum",
                    "enum Color { Red, Green, Blue }\n\n" +
                    "main() out int\n{\n    Color selected;\n" +
                    "    if(selected == Color.Red)\n    {\n        ret 42;\n    }\n" +
                    "    ret 0;\n}\n", null),
                ("fixed-enum",
                    "enum Color { Red, Green, Blue }\n\n" +
                    "fixed Color preferred << Color.Green;\n\n" +
                    "main() out int\n{\n    fixed Color selected << preferred;\n" +
                    "    if(selected == Color.Green)\n    {\n        ret 42;\n    }\n" +
                    "    ret 0;\n}\n", null),
                ("exhaustive-select",
                    "enum Color { Red, Green, Blue }\n\n" +
                    "main() out int\n{\n    Color selected << Color.Green;\n" +
                    "    select(selected)\n    {\n" +
                    "        when Color.Red { ret 1; }\n" +
                    "        when Color.Green { ret 42; }\n" +
                    "        when Color.Blue { ret 3; }\n    }\n}\n", null),
                ("select-otherwise",
                    "enum Color { Red, Green, Blue }\n\n" +
                    "main() out int\n{\n    Color selected << Color.Blue;\n" +
                    "    select(selected)\n    {\n" +
                    "        when Color.Red { ret 1; }\n" +
                    "        when Color.Green { ret 2; }\n" +
                    "        otherwise { ret 42; }\n    }\n}\n", null),
                ("arrays-structures",
                    "struct Box\n{\n    int value;\n}\n\n" +
                    "main() out int\n{\n    int[2] values;\n    Box box;\n" +
                    "    values[0] << 4;\n    values[1] << 5;\n" +
                    "    box.value << values[0] + values[1];\n" +
                    "    box.value >> Console;\n    ret box.value;\n}\n", null),
                ("array-identity-and-single-length-evaluation",
                    "int lengthCalls << 0;\n" +
                    "size() out int { lengthCalls++; ret 3; }\n" +
                    "mutate(int[3] values) out void { values[1] << 20; }\n" +
                    "replaceLocally(int[3] values) out void { int[3] replacement; " +
                    "values << replacement; values[0] << 99; }\n" +
                    "main() out int\n{\n    int[size()] values;\n" +
                    "    int[3] alias << values;\n    values[0] << 20;\n" +
                    "    mutate(alias);\n    values[2] << 1;\n" +
                    "    replaceLocally(alias);\n" +
                    "    ret lengthCalls + values[0] + alias[1] + values[2];\n}\n", null),
                ("input-conversion",
                    "main() out float\n{\n    string text << readLine();\n" +
                    "    float value << toFloat(text);\n    value >> Console;\n" +
                    "    ret value;\n}\n", "1.5"),
                ("active-scalar-conversions",
                    "main() out float\n{\n    int whole << toInt(\"  +20  \");\n" +
                    "    float fraction << toFloat(\"2.2e1\");\n" +
                    "    char letter << \"D\";\n    string text << \"DDF\";\n" +
                    "    bool matches << letter == text[0];\n" +
                    "    if(!matches) { ret 0; }\n" +
                    "    ret whole + fraction;\n}\n", null),
                ("integer-division",
                    "main() out int\n{\n    ret -7 / 2;\n}\n", null),
                ("end",
                    "main() out int\n{\n    end;\n    ret 0;\n}\n", null),
                ("division-by-zero",
                    "main() out int\n{\n    int zero << 0;\n" +
                    "    ret 4 / zero;\n}\n", null),
                ("integer-overflow",
                    "main() out int\n{\n    int maximum << 2147483647;\n" +
                    "    ret maximum + 1;\n}\n", null),
                ("non-finite-float",
                    "main() out float\n{\n    float huge << toFloat(\"1e200\");\n" +
                    "    ret huge * huge;\n}\n", null),
                ("negative-integer-power",
                    "main() out int\n{\n    ret 2 ^ -1;\n}\n", null),
                ("invalid-conversion",
                    "main() out int\n{\n    ret toInt(\"DDF\");\n}\n", null),
                ("integer-conversion-overflow",
                    "main() out int\n{\n    ret toInt(\"2147483648\");\n}\n", null),
                ("non-finite-conversion",
                    "main() out float\n{\n    ret toFloat(\"1e309\");\n}\n", null),
                ("array-bounds",
                    "main() out int\n{\n    int[1] values;\n" +
                    "    ret values[2];\n}\n", null),
                ("negative-array-length",
                    "main() out int\n{\n    int[-1] values;\n    ret 0;\n}\n", null)
            };
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfDotNetReferenceSet references =
                DdfDotNetReferencePackResolver.Resolve(FindNet10ReferencePack(),
                    typeof(DdfRuntimeAbi).Assembly.Location);
            int successful = 0, failed = 0, terminated = 0;
            string root = Path.Combine(Path.GetTempPath(), "ddf-differential-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                for (int index = 0; index < cases.Length; index++)
                {
                    var test = cases[index];
                    string caseRoot = Path.Combine(root, index.ToString("D2") +
                        "-" + test.Name);
                    Directory.CreateDirectory(caseRoot);
                    File.WriteAllText(Path.Combine(caseRoot, "main.ddf"),
                        test.Source, new UTF8Encoding(false));
                    DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                        test.Name, package, "main.ddf");
                    project.Module.Id = "ddf.differential.case" + index;
                    project.Module.Version = "1.0.0";
                    project.Module.Exports.Add("main");
                    DdfBuildResult prepared = new DdfBuildService().Validate(
                        caseRoot, project, package);
                    Require(prepared.Success,
                        "Il caso differenziale non supera la pipeline Core: " +
                        test.Name + " — " + string.Join("; ", prepared.Issues));
                    DdfDotNetCompilationPlanResult planned =
                        DdfDotNetCompilationPlanner.Create(prepared,
                            new DdfDotNetCompilationOptions("net10.0",
                                DdfRuntimeAbi.Version, referenceSet: references));
                    Require(planned.Success,
                        "Piano Roslyn differenziale non valido: " + test.Name);
                    DdfDotNetArtifactEmissionResult emission =
                        DdfDotNetArtifactEmitter.Emit(planned.Plan,
                            package.Language.Types.ClrMapping);
                    Require(emission.Success,
                        "Emit differenziale fallito: " + test.Name + " — " +
                        string.Join("; ", emission.Issues.Select(issue =>
                            issue.Code + " " + issue.Message)));
                    var vmOutput = new List<string>();
                    DdfExecutionResult vm = DdfInterpreter.Execute(
                        prepared.Artifact.RuntimeModules, package.Language,
                        new DdfExecutionOptions
                        {
                            EntryPoint = "main",
                            Output = value => vmOutput.Add(value),
                            Input = test.Input == null ? null : () => test.Input
                        });
                    var compiledOutput = new List<string>();
                    DdfRuntimeExecutionResult compiled = null;
                    DdfRuntimeException compiledError = null;
                    try
                    {
                        compiled = ExecuteForVerification(planned.Plan, emission,
                            compiledOutput, input: test.Input == null
                                ? null : () => test.Input);
                    }
                    catch (DdfRuntimeException exception)
                    {
                        compiledError = exception;
                    }
                    if (vm.Succeeded)
                    {
                        Require(compiledError == null && compiled != null,
                            test.Name + ": Roslyn fallisce mentre la VM riesce.");
                        Require(EquivalentValue(vm.ReturnValue, compiled.Value) &&
                                vm.WasTerminated == compiled.Ended &&
                                vmOutput.SequenceEqual(compiledOutput),
                            test.Name + ": risultato, terminazione o Output divergono." +
                            " VM=<" + DdfRuntimeContext.Format(vm.ReturnValue) +
                            "> Roslyn=<" + DdfRuntimeContext.Format(compiled.Value) +
                            ">.");
                        successful++;
                        if (vm.WasTerminated) terminated++;
                    }
                    else
                    {
                        DdfDiagnostic diagnostic = vm.Diagnostics.Single();
                        Require(compiledError != null && compiled == null &&
                                compiledError.Code == diagnostic.Code &&
                                compiledError.Location.Start == diagnostic.Start &&
                                Path.GetFileName(compiledError.Location.DocumentPath) ==
                                Path.GetFileName(diagnostic.DocumentPath),
                            test.Name + ": diagnostica runtime divergente. VM=<" +
                            diagnostic.Code + "@" + diagnostic.Start +
                            "> Roslyn=<" + (compiledError?.Code ?? "nessuna") +
                            "@" + (compiledError?.Location.Start.ToString() ?? "-") +
                            ">.");
                        Require(compiledOutput.SequenceEqual(vmOutput),
                            test.Name + ": Output precedente all'errore divergente.");
                        failed++;
                    }
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            Equal(14, successful);
            Equal(9, failed);
            Equal(2, terminated);
            Console.WriteLine("PASS corpus differenziale VM/Roslyn: risultati, enum, select, identità array, Output, flusso, conversioni invariant e 9 errori runtime");
        }

        private static bool EquivalentValue(object expected, object actual)
        {
            if (expected is double expectedDouble && actual is double actualDouble)
                return expectedDouble.Equals(actualDouble);
            return Equals(expected, actual);
        }

        private static void MatchesVersionedNormativeLanguageCorpus()
        {
            string corpusRoot = Path.Combine(RepositoryRoot(), "samples",
                "language-corpus");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(corpusRoot, "corpus.json"), Encoding.UTF8));
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfDotNetReferenceSet references =
                DdfDotNetReferencePackResolver.Resolve(FindNet10ReferencePack(),
                    typeof(DdfRuntimeAbi).Assembly.Location);
            int validCount = 0, runtimeErrorCount = 0, workspaceCount = 0;

            foreach (JsonElement test in manifest.RootElement
                         .GetProperty("valid").EnumerateArray())
            {
                string id = test.GetProperty("id").GetString();
                string file = test.GetProperty("file").GetString();
                string root = Path.Combine(corpusRoot, "valid");
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Corpus " + id, package, file);
                project.Sources = new List<string> { file };
                project.Module.Id = "ddf.corpus.compiler." + id;
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DifferentialExecution execution = ExecuteDifferential(
                    root, project, package, references);
                RequireEquivalentDifferential(execution, id);
                object expected = ParseCorpusValue(
                    test.GetProperty("returnClrType").GetString(),
                    test.GetProperty("returnValue").GetString());
                Require(EquivalentValue(expected, execution.Vm.ReturnValue),
                    id + ": il risultato VM non coincide con l'oracolo del corpus.");
                string[] expectedOutput = test.GetProperty("output")
                    .EnumerateArray().Select(item => item.GetString()).ToArray();
                Require(execution.VmOutput.SequenceEqual(expectedOutput),
                    id + ": l'Output non coincide con l'oracolo del corpus.");
                validCount++;
            }

            foreach (JsonElement test in manifest.RootElement
                         .GetProperty("invalid").EnumerateArray()
                         .Where(item => string.Equals(
                             item.GetProperty("stage").GetString(), "runtime",
                             StringComparison.Ordinal)))
            {
                string id = test.GetProperty("id").GetString();
                string file = test.GetProperty("file").GetString();
                string root = Path.Combine(corpusRoot, "invalid");
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Corpus " + id, package, file);
                project.Sources = new List<string> { file };
                project.Module.Id = "ddf.corpus.compiler." + id;
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DifferentialExecution execution = ExecuteDifferential(
                    root, project, package, references);
                RequireEquivalentDifferential(execution, id);
                string[] expectedCodes = test.GetProperty("diagnostics")
                    .EnumerateArray().Select(item => item.GetString()).ToArray();
                Require(!execution.Vm.Succeeded &&
                        expectedCodes.Contains(execution.Vm.Diagnostics.Single().Code,
                            StringComparer.Ordinal),
                    id + ": la diagnostica VM non coincide con l'oracolo del corpus.");
                runtimeErrorCount++;
            }

            foreach (JsonElement test in manifest.RootElement
                         .GetProperty("multiFile").EnumerateArray()
                         .Where(item => string.Equals(
                             item.GetProperty("status").GetString(), "success",
                             StringComparison.Ordinal)))
            {
                string id = test.GetProperty("id").GetString();
                string root = Path.Combine(corpusRoot, "multi-file",
                    test.GetProperty("directory").GetString());
                DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader
                    .Load(root);
                Require(loaded.Success,
                    id + ": manifesto workspace normativo non valido.");
                DifferentialExecution execution = ExecuteDifferential(
                    root, loaded.Manifest, package, references);
                RequireEquivalentDifferential(execution, id);
                object expected = ParseCorpusValue(
                    test.GetProperty("returnClrType").GetString(),
                    test.GetProperty("returnValue").GetString());
                Require(EquivalentValue(expected, execution.Vm.ReturnValue),
                    id + ": risultato multifile diverso dall'oracolo del corpus.");
                string[] expectedOutput = test.GetProperty("output")
                    .EnumerateArray().Select(item => item.GetString()).ToArray();
                Require(execution.VmOutput.SequenceEqual(expectedOutput),
                    id + ": Output multifile diverso dall'oracolo del corpus.");
                workspaceCount++;
            }

            Equal(12, validCount);
            Equal(2, runtimeErrorCount);
            Equal(4, workspaceCount);
            Console.WriteLine("PASS corpus normativo versionato VM/Roslyn: " +
                "12 validi, 2 errori runtime e 4 workspace multifile");
        }

        private static DifferentialExecution ExecuteDifferential(string root,
            DdfProjectManifest project, DdfLanguagePackage package,
            DdfDotNetReferenceSet references)
        {
            DdfBuildResult prepared = new DdfBuildService().Validate(root,
                project, package);
            Require(prepared.Success,
                project.Name + ": preparazione Core fallita — " +
                string.Join("; ", prepared.Issues));
            DdfDotNetCompilationPlanResult planned =
                DdfDotNetCompilationPlanner.Create(prepared,
                    new DdfDotNetCompilationOptions("net10.0",
                        DdfRuntimeAbi.Version, referenceSet: references));
            Require(planned.Success,
                project.Name + ": piano Roslyn non valido — " +
                string.Join("; ", planned.Issues));
            DdfDotNetArtifactEmissionResult emission =
                DdfDotNetArtifactEmitter.Emit(planned.Plan,
                    package.Language.Types.ClrMapping);
            Require(emission.Success,
                project.Name + ": emit Roslyn fallito — " +
                string.Join("; ", emission.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            var result = new DifferentialExecution();
            result.Vm = DdfInterpreter.Execute(prepared.Artifact.RuntimeModules,
                package.Language, new DdfExecutionOptions
                {
                    EntryPoint = project.EntryPoint,
                    MaxInstructions = project.Options.MaxInstructions ?? 100000,
                    Output = value => result.VmOutput.Add(value)
                });
            try
            {
                result.Compiled = ExecuteForVerification(planned.Plan, emission,
                    result.CompiledOutput);
            }
            catch (DdfRuntimeException exception)
            {
                result.CompiledError = exception;
            }
            return result;
        }

        private static void RequireEquivalentDifferential(
            DifferentialExecution execution, string id)
        {
            if (execution.Vm.Succeeded)
            {
                Require(execution.CompiledError == null &&
                        execution.Compiled != null,
                    id + ": Roslyn fallisce mentre la VM riesce.");
                Require(EquivalentValue(execution.Vm.ReturnValue,
                            execution.Compiled.Value) &&
                        execution.Vm.WasTerminated == execution.Compiled.Ended &&
                        execution.VmOutput.SequenceEqual(execution.CompiledOutput),
                    id + ": risultato, terminazione o Output divergono.");
                return;
            }

            DdfDiagnostic diagnostic = execution.Vm.Diagnostics.Single();
            Require(execution.Compiled == null &&
                    execution.CompiledError != null &&
                    execution.CompiledError.Code == diagnostic.Code &&
                    execution.CompiledError.Location.Start == diagnostic.Start &&
                    Path.GetFileName(execution.CompiledError.Location.DocumentPath) ==
                    Path.GetFileName(diagnostic.DocumentPath) &&
                    execution.VmOutput.SequenceEqual(execution.CompiledOutput),
                id + ": diagnostica runtime o Output precedente divergenti. VM=<" +
                diagnostic.Code + "@" + diagnostic.Start + "> Roslyn=<" +
                (execution.CompiledError?.Code ?? "nessuna") + "@" +
                (execution.CompiledError?.Location.Start.ToString(
                    CultureInfo.InvariantCulture) ?? "-") + ">.");
        }

        private static object ParseCorpusValue(string clrType, string value) =>
            clrType switch
            {
                "System.Int32" => int.Parse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                "System.Double" => double.Parse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture),
                "System.Boolean" => bool.Parse(value),
                "System.String" => value,
                _ => throw new InvalidOperationException(
                    "Tipo oracolo non supportato: " + clrType)
            };

        private static void VerifiesGovernedAdaptersAcrossVmAndCompiledBackend()
        {
            string root = Path.Combine(AppContext.BaseDirectory,
                "ddf-adapter-cross-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DdfAdoNetDatabaseAdapter data = null;
            DdfGovernedFileSystemAdapter storage = null;
            DdfGovernedHardwareAdapter hardware = null;
            DdfLoopbackHardwareDevice hardwareDevice = null;
            DdfHttpJsonAdapter network = null;
            HttpClient client = null;
            try
            {
                string noteRoot = Path.Combine(root, "storage");
                Directory.CreateDirectory(noteRoot);
                File.WriteAllText(Path.Combine(noteRoot, "note.txt"), "hello",
                    new UTF8Encoding(false));

                data = new DdfAdoNetDatabaseAdapter("cross.database",
                    "cross.data", token => ValueTask.FromResult<
                        System.Data.Common.DbConnection>(new SqliteConnection(
                            "Data Source=:memory:")), new[]
                    {
                        new DdfAdoNetOperationDefinition("adapterDbValue",
                            "value.read", DdfHostDatabaseOperationKind.Query,
                            "SELECT 7", "int", requiredPermissions:
                                new[] { "cross.db.read" })
                    });
                client = new HttpClient(new CrossAdapterHttpHandler());
                network = new DdfHttpJsonAdapter("cross.network",
                    new Uri("https://cross.example.test/"), client, new[]
                    {
                        new DdfHttpOperationDefinition("adapterRemoteValue",
                            "value.remote", HttpMethod.Get, "/value", "int",
                            jsonPath: "value", requiredPermissions:
                                new[] { "cross.net.read" })
                    });
                storage = new DdfGovernedFileSystemAdapter("cross.storage",
                    noteRoot, new[]
                    {
                        new DdfLogicalFileDefinition("note", "note.txt",
                            DdfLogicalFileFormat.Utf8Text,
                            DdfLogicalFileAccess.ReadOnly, 64)
                    }, new[]
                    {
                        new DdfLogicalFileOperationDefinition(
                            "adapterReadNote", "note.read", "note",
                            DdfLogicalFileOperationKind.Read,
                            requiredPermissions: new[] { "cross.file.read" })
                    });
                var hardwareIdentity = new DdfHardwareDeviceIdentity(
                    "cross.loopback", DdfHardwareTransportKind.Custom,
                    "cross.provider", "simulated-device");
                var hardwareDefinition = new DdfHardwareDeviceDefinition(
                    hardwareIdentity, DdfHardwareAccessMode.Exclusive,
                    DdfHardwarePermission.Configure,
                    new[] { "verified" });
                hardwareDevice = new DdfLoopbackHardwareDevice(
                    hardwareIdentity, new[] { "verified" });
                hardware = new DdfGovernedHardwareAdapter("cross.hardware",
                    new[] { hardwareDefinition },
                    new IDdfHardwareDevice[] { hardwareDevice }, new[]
                    {
                        new DdfHardwareOperationDefinition(
                            "adapterConfigureHardware", "device.configure",
                            "cross.loopback",
                            DdfHardwareOperationKind.ApplyProfile, "verified",
                            requiredPermissions: new[]
                            {
                                "cross.hardware.configure"
                            })
                    });

                DdfHostPermissionDefinition[] dataPermissions =
                {
                    new("cross.db.read", "Lettura dati cross-backend")
                };
                DdfHostPermissionDefinition[] networkPermissions =
                {
                    new("cross.net.read", "Lettura rete cross-backend")
                };
                DdfHostPermissionDefinition[] storagePermissions =
                {
                    new("cross.file.read", "Lettura file cross-backend")
                };
                DdfHostPermissionDefinition[] hardwarePermissions =
                {
                    new("cross.hardware.configure",
                        "Configurazione hardware cross-backend")
                };
                DdfExternalApiDefinition dataApi = data
                    .CreateExternalApiDefinition("ddf.dotnet.cross.data",
                        "1.0.0", dataPermissions);
                DdfExternalApiDefinition networkApi = network
                    .CreateExternalApiDefinition("ddf.dotnet.cross.network",
                        "1.0.0", networkPermissions);
                DdfExternalApiDefinition storageApi = storage
                    .CreateExternalApiDefinition("ddf.dotnet.cross.storage",
                        "1.0.0", storagePermissions);
                DdfExternalApiDefinition hardwareApi = hardware
                    .CreateExternalApiDefinition(
                        "ddf.platform.cross.hardware", "1.0.0",
                        hardwarePermissions);
                var grants = new HashSet<string>(new[]
                {
                    "cross.db.read", "cross.net.read", "cross.file.read",
                    "cross.hardware.configure"
                }, StringComparer.Ordinal);
                var catalog = new DdfEditorHostCatalog();
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(catalog,
                    dataApi, data.AdapterId, data,
                    new[] { "cross.db.read" });
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(catalog,
                    networkApi, network.AdapterId, network,
                    new[] { "cross.net.read" });
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(catalog,
                    storageApi, storage.AdapterId, storage,
                    new[] { "cross.file.read" });
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(catalog,
                    hardwareApi, hardware.AdapterId, hardware,
                    new[] { "cross.hardware.configure" });

                const string source =
                    "main() out int\n{\n" +
                    "    adapterConfigureHardware();\n" +
                    "    int db << adapterDbValue();\n" +
                    "    int remote << adapterRemoteValue();\n" +
                    "    string note << adapterReadNote();\n" +
                    "    ret db + remote + length(note);\n" +
                    "}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Cross adapter gate", package, "main.ddf");
                project.Module.Id = "ddf.test.cross-adapters";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                var buildService = new DdfBuildService();
                DdfBuildResult embeddedValidation = buildService.Validate(root,
                    project, package, null, catalog.Snapshot);
                DdfBuildResult prepared = buildService.Build(root, project,
                    package, null, catalog.Snapshot);
                Require(embeddedValidation.Success && prepared.Success &&
                        embeddedValidation.Artifact.Fingerprint ==
                        prepared.Artifact.Fingerprint &&
                        string.IsNullOrEmpty(
                            embeddedValidation.ArtifactPath) &&
                        File.Exists(prepared.ArtifactPath),
                    "Validate embedded e Build standalone non condividono lo " +
                    "stesso contratto tipizzato o la corretta policy degli " +
                    "artefatti: " + string.Join("; ",
                        embeddedValidation.Issues.Concat(prepared.Issues)));

                DdfExecutionResult vm = DdfInterpreter.Execute(
                    embeddedValidation.Artifact.RuntimeModules,
                    package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        HostCatalog = catalog.Snapshot
                    });
                Require(vm.Succeeded && Convert.ToInt32(vm.ReturnValue,
                            CultureInfo.InvariantCulture) == 42,
                    "Gli adapter non restituiscono 42 nella VM embedded.");
                Require(hardwareDevice.LastProfile == "verified" &&
                        hardwareDevice.OpenSessions == 0,
                    "La VM non applica o non rilascia il loopback hardware.");

                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                Require(planned.Success,
                    "Piano compilato degli adapter non valido.");
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success,
                    "Emit compilato degli adapter fallito: " + string.Join("; ",
                        emission.Issues.Select(item => item.Code + " " +
                            item.Message)));
                var provider = new DdfDotNetExternalApiCapabilityProvider(new[]
                {
                    Binding(dataApi, data.AdapterId, data, grants),
                    Binding(networkApi, network.AdapterId, network, grants),
                    Binding(storageApi, storage.AdapterId, storage, grants),
                    Binding(hardwareApi, hardware.AdapterId, hardware, grants)
                });
                DdfRuntimeExecutionResult compiled = ExecuteForVerification(
                    planned.Plan, emission, new List<string>(),
                    capabilityProvider: provider);
                Require(Convert.ToInt32(compiled.Value,
                            CultureInfo.InvariantCulture) == 42,
                    "Gli adapter non restituiscono 42 nel backend compilato.");
                Require(hardwareDevice.LastProfile == "verified" &&
                        hardwareDevice.OpenSessions == 0,
                    "Il compilato non applica o non rilascia il loopback hardware.");
                Require(emission.Artifacts.Single().Manifest
                        .RequiredHostCapabilities.SequenceEqual(
                            provider.CapabilityIds, StringComparer.Ordinal),
                    "Manifest e provider non espongono le stesse capability.");

                DdfIrModule vmEntry = embeddedValidation.Artifact
                    .RuntimeModules.Single(module => module.RelativePath ==
                        "main.ddf");
                DdfIrFunction vmMain = vmEntry.Program.Members
                    .OfType<DdfIrFunction>().Single(function =>
                        function.Name == "main");
                int debugStart = vmMain.Body.Statements[0].Start;
                using (var vmDebugger = new DdfDebuggerSession())
                using (var vmPaused = new ManualResetEventSlim(false))
                using (var compiledDebugger =
                       new DdfDotNetCooperativeDebugger(emission.Artifacts
                           .Select(item => item.SourceMap)))
                {
                    DdfDebugPauseInfo vmPause = null;
                    vmDebugger.SetBreakpoints(new[]
                    {
                        new DdfSourceBreakpoint(vmEntry.DocumentPath,
                            LineAt(vmEntry.Source, debugStart))
                    });
                    vmDebugger.Paused = pause =>
                    {
                        vmPause = pause;
                        vmPaused.Set();
                    };
                    compiledDebugger.SetBreakpoints(new[]
                    {
                        new DdfDotNetSourceBreakpoint("main.ddf", debugStart)
                    });
                    Task<DdfExecutionResult> vmDebugRun = Task.Run(() =>
                        DdfInterpreter.Execute(embeddedValidation.Artifact
                            .RuntimeModules, package.Language,
                            new DdfExecutionOptions
                            {
                                EntryPoint = "main",
                                HostCatalog = catalog.Snapshot,
                                DebuggerSession = vmDebugger
                            }));
                    Task<DdfRuntimeExecutionResult> compiledDebugRun = Task.Run(
                        () => ExecuteForVerification(planned.Plan, emission,
                            new List<string>(), compiledDebugger,
                            capabilityProvider: provider));
                    Require(vmPaused.Wait(TimeSpan.FromSeconds(5)) &&
                            compiledDebugger.WaitForPause(
                                TimeSpan.FromSeconds(5)),
                        "Il debug non raggiunge lo stesso breakpoint nei due " +
                        "contesti.");
                    Require(DdfDotNetDifferentialDebugTrace.Compare(vmPause,
                                compiledDebugger.CurrentPause,
                                emission.Artifacts.Select(item =>
                                    item.SourceMap)).Count == 0,
                        "La pausa sul contratto esterno diverge fra debug " +
                        "embedded e standalone compilato.");
                    vmDebugger.Continue();
                    Require(vmDebugRun.Wait(TimeSpan.FromSeconds(5)) &&
                            vmDebugRun.Result.Succeeded &&
                            Convert.ToInt32(vmDebugRun.Result.ReturnValue,
                                CultureInfo.InvariantCulture) == 42,
                        "Il debug embedded non completa il contratto esterno.");
                    compiledDebugger.Resume(DdfDotNetDebugCommand.Continue);
                    Require(compiledDebugRun.Wait(TimeSpan.FromSeconds(5)) &&
                            Convert.ToInt32(compiledDebugRun.Result.Value,
                                CultureInfo.InvariantCulture) == 42,
                        "Il debug standalone compilato non completa il " +
                        "contratto esterno.");
                }

                using (var replacementClient = new HttpClient(
                           new CrossAdapterHttpHandler()))
                using (var replacementNetwork = new DdfHttpJsonAdapter(
                           "cross.network",
                           new Uri("https://cross.example.test/"),
                           replacementClient, new[]
                           {
                               new DdfHttpOperationDefinition(
                                   "adapterRemoteValue", "value.remote",
                                   HttpMethod.Get, "/value", "int",
                                   jsonPath: "value", requiredPermissions:
                                       new[] { "cross.net.read" })
                           }))
                {
                    DdfExternalApiDefinition replacementApi =
                        replacementNetwork.CreateExternalApiDefinition(
                            "ddf.dotnet.cross.network", "1.0.0",
                            networkPermissions);
                    Require(replacementApi.Fingerprint ==
                            networkApi.Fingerprint,
                        "Un aggiornamento compatibile ha alterato il contratto " +
                        "pubblico della libreria.");
                    var replacementCatalog = new DdfEditorHostCatalog();
                    DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                        replacementCatalog, dataApi, data.AdapterId, data,
                        new[] { "cross.db.read" });
                    DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                        replacementCatalog, replacementApi,
                        replacementNetwork.AdapterId, replacementNetwork,
                        new[] { "cross.net.read" });
                    DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                        replacementCatalog, storageApi, storage.AdapterId,
                        storage, new[] { "cross.file.read" });
                    DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                        replacementCatalog, hardwareApi, hardware.AdapterId,
                        hardware, new[] { "cross.hardware.configure" });
                    DdfExecutionResult replacementVm = DdfInterpreter.Execute(
                        embeddedValidation.Artifact.RuntimeModules,
                        package.Language, new DdfExecutionOptions
                        {
                            EntryPoint = "main",
                            HostCatalog = replacementCatalog.Snapshot
                        });
                    var replacementProvider =
                        new DdfDotNetExternalApiCapabilityProvider(new[]
                        {
                            Binding(dataApi, data.AdapterId, data, grants),
                            Binding(replacementApi,
                                replacementNetwork.AdapterId,
                                replacementNetwork, grants),
                            Binding(storageApi, storage.AdapterId, storage,
                                grants),
                            Binding(hardwareApi, hardware.AdapterId, hardware,
                                grants)
                        });
                    DdfRuntimeExecutionResult replacementCompiled =
                        ExecuteForVerification(planned.Plan, emission,
                            new List<string>(), capabilityProvider:
                                replacementProvider);
                    Require(replacementVm.Succeeded &&
                            Convert.ToInt32(replacementVm.ReturnValue,
                                CultureInfo.InvariantCulture) == 42 &&
                            Convert.ToInt32(replacementCompiled.Value,
                                CultureInfo.InvariantCulture) == 42,
                        "L'aggiornamento compatibile della libreria non viene " +
                        "accettato allo stesso modo da VM e Roslyn.");
                }

                grants.Remove("cross.net.read");
                var revokedCatalog = new DdfEditorHostCatalog();
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                    revokedCatalog, dataApi, data.AdapterId, data,
                    new[] { "cross.db.read" });
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                    revokedCatalog, networkApi, network.AdapterId, network,
                    Array.Empty<string>());
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                    revokedCatalog, storageApi, storage.AdapterId, storage,
                    new[] { "cross.file.read" });
                DdfExternalApiCatalogProjector.RegisterRuntimeCatalog(
                    revokedCatalog, hardwareApi, hardware.AdapterId, hardware,
                    new[] { "cross.hardware.configure" });
                DdfExecutionResult revokedVm = DdfInterpreter.Execute(
                    prepared.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        HostCatalog = revokedCatalog.Snapshot
                    });
                Require(!revokedVm.Succeeded && revokedVm.Diagnostics.Any(
                        item => item.Code == "DDF409"),
                    "La VM non rifiuta il permesso adapter revocato con DDF409.");
                RequireRuntimeCode("DDF409", () => ExecuteForVerification(
                    planned.Plan, emission, new List<string>(),
                    capabilityProvider: provider),
                    "Il compilato non rifiuta il permesso adapter revocato.");
                grants.Add("cross.net.read");
                RequireRuntimeCode("DDF409", () => ExecuteForVerification(
                    planned.Plan, emission, new List<string>()),
                    "Il compilato ha accettato adapter assenti.");
                var analysisOnlyCatalog = new DdfEditorHostCatalog();
                DdfExternalApiCatalogProjector.RegisterAnalysisCatalog(
                    analysisOnlyCatalog, dataApi,
                    new[] { "cross.db.read" });
                DdfExternalApiCatalogProjector.RegisterAnalysisCatalog(
                    analysisOnlyCatalog, networkApi,
                    new[] { "cross.net.read" });
                DdfExternalApiCatalogProjector.RegisterAnalysisCatalog(
                    analysisOnlyCatalog, storageApi,
                    new[] { "cross.file.read" });
                DdfExternalApiCatalogProjector.RegisterAnalysisCatalog(
                    analysisOnlyCatalog, hardwareApi,
                    new[] { "cross.hardware.configure" });
                DdfExecutionResult absentVm = DdfInterpreter.Execute(
                    prepared.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        HostCatalog = analysisOnlyCatalog.Snapshot
                    });
                Require(!absentVm.Succeeded && absentVm.Diagnostics.Any(item =>
                        item.Code == "DDF409"),
                    "La VM non rifiuta gli adapter assenti con DDF409.");

                using var updatedClient = new HttpClient(
                    new CrossAdapterHttpHandler());
                using var updatedNetwork = new DdfHttpJsonAdapter(
                    "cross.network", new Uri("https://cross.example.test/"),
                    updatedClient, new[]
                    {
                        new DdfHttpOperationDefinition("adapterRemoteValue",
                            "value.remote", HttpMethod.Get, "/value", "int",
                            jsonPath: "value", documentation:
                                "Contratto aggiornato incompatibile",
                            requiredPermissions: new[] { "cross.net.read" })
                    });
                DdfExternalApiDefinition updatedApi = updatedNetwork
                    .CreateExternalApiDefinition("ddf.dotnet.cross.network",
                        "1.0.0", networkPermissions);
                bool updateRejected = false;
                try
                {
                    _ = new DdfDotNetExternalApiBinding(networkApi.ReferenceId,
                        networkApi.ReferenceVersion, networkApi.Fingerprint,
                        updatedApi, updatedNetwork.AdapterId, updatedNetwork,
                        grants.Contains);
                }
                catch (InvalidOperationException) { updateRejected = true; }
                Require(updateRejected && updatedApi.Fingerprint !=
                        networkApi.Fingerprint,
                    "Un aggiornamento API divergente ha ignorato il lock.");
                Console.WriteLine("PASS contratto tipizzato standalone/embedded: " +
                    "Validate/Build, esecuzione, debug, aggiornamento, revoca e " +
                    "dipendenza assente in parità VM/Roslyn");
            }
            finally
            {
                data?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                network?.Dispose();
                client?.Dispose();
                storage?.Dispose();
                hardware?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static DdfDotNetExternalApiBinding Binding(
            DdfExternalApiDefinition definition, string adapterId,
            IDdfExternalApiRuntimeAdapter adapter, ISet<string> grants) => new(
                definition.ReferenceId, definition.ReferenceVersion,
                definition.Fingerprint, definition, adapterId, adapter,
                grants.Contains);

        private static void RequireRuntimeCode(string code,
            Func<DdfRuntimeExecutionResult> action, string message)
        {
            try { action(); }
            catch (DdfRuntimeException exception) when (exception.Code == code)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }

        private static void BridgesExplicitStandaloneHostCapabilities()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-standalone-capabilities-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    int remembered << hostRemember(20);\n" +
                    "    counter << remembered - 2;\n" +
                    "    panel.customer.address.city << \"DDF\";\n" +
                    "    scores[1] << counter;\n" +
                    "    ret hostScale(scores[1] + " +
                    "length(panel.customer.address.city));\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                int vmEffects = 0;
                int vmCounter = 0;
                int vmCounterWrites = 0;
                int vmPanelWrites = 0;
                int vmScoresWrites = 0;
                var vmPanel = new Dictionary<string, object>
                {
                    ["customer"] = new Dictionary<string, object>
                    {
                        ["address"] = new Dictionary<string, object>
                        {
                            ["city"] = "white"
                        }
                    }
                };
                int[] vmScores = { 0, 0, 0 };
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "standalone.execute", "Esecuzione standalone",
                    "Autorizza le capability esplicite del runner standalone."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "hostRemember", "int", new[]
                    {
                        new DdfHostParameterDefinition("value", "int")
                    }, arguments =>
                    {
                        vmEffects++;
                        return Convert.ToInt32(arguments[0],
                            CultureInfo.InvariantCulture);
                    }, requiredPermissions: new[] { "standalone.execute" },
                    effect: DdfHostFunctionEffect.HostMutation));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "hostScale", "int", new[]
                    {
                        new DdfHostParameterDefinition("value", "int")
                    }, arguments => Convert.ToInt32(arguments[0],
                        CultureInfo.InvariantCulture) * 2,
                    requiredPermissions: new[] { "standalone.execute" }));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "hostUnused", "int",
                    Array.Empty<DdfHostParameterDefinition>(), arguments => 0,
                    requiredPermissions: new[] { "standalone.execute" }));
                catalog.RegisterType(new DdfHostTypeDefinition("Address", new[]
                {
                    new DdfHostFieldDefinition("city", "string",
                        access: DdfHostValueAccess.ReadWrite,
                        memberKind: DdfHostMemberKind.Property)
                }));
                catalog.RegisterType(new DdfHostTypeDefinition("Customer", new[]
                {
                    new DdfHostFieldDefinition("address", "Address",
                        access: DdfHostValueAccess.ReadWrite,
                        memberKind: DdfHostMemberKind.Property)
                }));
                catalog.RegisterType(new DdfHostTypeDefinition("Panel", new[]
                {
                    new DdfHostFieldDefinition("customer", "Customer",
                        access: DdfHostValueAccess.ReadWrite,
                        memberKind: DdfHostMemberKind.Property)
                }));
                catalog.RegisterObject(new DdfHostObjectDefinition(
                    "counter", "int", () => vmCounter,
                    requiredPermissions: new[] { "standalone.execute" },
                    writeValue: value =>
                    {
                        vmCounterWrites++;
                        vmCounter = Convert.ToInt32(value,
                            CultureInfo.InvariantCulture);
                    }));
                catalog.RegisterObject(new DdfHostObjectDefinition(
                    "unusedCounter", "int", () => 99,
                    requiredPermissions: new[] { "standalone.execute" }));
                catalog.RegisterObject(new DdfHostObjectDefinition(
                    "panel", "Panel", () => new Dictionary<string, object>(
                        vmPanel, StringComparer.Ordinal),
                    requiredPermissions: new[] { "standalone.execute" },
                    writeValue: value =>
                    {
                        vmPanelWrites++;
                        var fields = (IReadOnlyDictionary<string, object>)value;
                        vmPanel["customer"] = fields["customer"];
                    }));
                catalog.RegisterObject(new DdfHostObjectDefinition(
                    "scores", "int[]", () => vmScores,
                    requiredPermissions: new[] { "standalone.execute" },
                    writeValue: value =>
                    {
                        vmScoresWrites++;
                        vmScores = ((System.Collections.IEnumerable)value)
                            .Cast<object>().Select(item => Convert.ToInt32(item,
                                CultureInfo.InvariantCulture)).ToArray();
                    }));
                catalog.SetPermission("standalone.execute", true);

                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Standalone capability bridge", package, "main.ddf");
                project.Module.Id = "ddf.test.standalone-capability";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success,
                    "La capability standalone non supera la pipeline Core: " +
                    string.Join("; ", prepared.Issues));
                DdfBoundCompilationUnit capabilityBound = prepared
                    .Artifact.CompilationUnits.Single()
                    .BoundIntermediateRepresentation;
                Require(capabilityBound.Symbols.Single(symbol => symbol.Kind ==
                            DdfBoundSymbolKind.HostFunction && symbol.Name ==
                            "hostRemember").HasSideEffects &&
                        !capabilityBound.Symbols.Single(symbol => symbol.Kind ==
                            DdfBoundSymbolKind.HostFunction && symbol.Name ==
                            "hostScale").HasSideEffects,
                    "La IR bound non distingue funzione host mutante e pura.");
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                Require(planned.Success, "Piano capability standalone non valido.");
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success,
                    "Emit capability standalone fallito: " + string.Join("; ",
                        emission.Issues.Select(issue => issue.Code + " " +
                            issue.Message)));

                string rememberCapability = DdfDotNetHostCapabilities.ForFunction(
                    "hostRemember");
                string scaleCapability = DdfDotNetHostCapabilities.ForFunction(
                    "hostScale");
                string counterRead = DdfDotNetHostCapabilities.ForObjectRead(
                    "counter");
                string counterWrite = DdfDotNetHostCapabilities.ForObjectWrite(
                    "counter");
                string cityRead = DdfDotNetHostCapabilities.ForMemberRead(
                    "panel", "customer", "address", "city");
                string cityWrite = DdfDotNetHostCapabilities.ForMemberWrite(
                    "panel", "customer", "address", "city");
                string scoresRead = DdfDotNetHostCapabilities.ForElementRead(
                    "scores");
                string scoresWrite = DdfDotNetHostCapabilities.ForElementWrite(
                    "scores");
                DdfDotNetEmittedArtifact entry = emission.Artifacts.Single();
                Require(entry.Manifest.RequiredHostCapabilities.SequenceEqual(
                        new[]
                        {
                            rememberCapability, scaleCapability,
                            counterRead, counterWrite, cityRead, cityWrite,
                            scoresRead, scoresWrite
                        }
                            .OrderBy(value => value, StringComparer.Ordinal)),
                    "Il manifest non dichiara soltanto le capability realmente usate.");
                Require(DdfDotNetArtifactCompatibility.Inspect(entry.Manifest,
                        new DdfDotNetRuntimeEnvironment(
                            DdfDotNetArtifactHostKind.Standalone, "net10.0",
                            DdfRuntimeAbi.Version), sourceMap: entry.SourceMap)
                    .Issues.Any(issue => issue.Code == "DDFC205"),
                    "Un runner privo di capability ha superato la compatibilità.");
                Require(DdfDotNetArtifactCompatibility.Inspect(entry.Manifest,
                        new DdfDotNetRuntimeEnvironment(
                            DdfDotNetArtifactHostKind.Standalone, "net10.0",
                            DdfRuntimeAbi.Version, hostCapabilities: new[]
                            {
                                rememberCapability, scaleCapability,
                                counterRead, counterWrite, cityRead, cityWrite,
                                scoresRead, scoresWrite
                            }), sourceMap: entry.SourceMap).IsCompatible,
                    "Un runner con capability esatte è stato rifiutato.");

                DdfExecutionResult vm = DdfInterpreter.Execute(
                    prepared.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        HostCatalog = catalog.Snapshot
                    });
                int compiledEffects = 0;
                int compiledCounter = 0;
                string compiledCity = "white";
                int[] compiledScores = { 0, 0, 0 };
                int compiledCounterWrites = 0;
                int compiledCityWrites = 0;
                int compiledScoresWrites = 0;
                var provider = new DelegateCapabilityProvider(new Dictionary<
                    string, Func<IReadOnlyList<object>, object>>
                {
                    [rememberCapability] = arguments =>
                    {
                        compiledEffects++;
                        return Convert.ToInt32(arguments[0],
                            CultureInfo.InvariantCulture);
                    },
                    [scaleCapability] = arguments => Convert.ToInt32(
                        arguments[0], CultureInfo.InvariantCulture) * 2,
                    [counterRead] = arguments => compiledCounter,
                    [counterWrite] = arguments =>
                    {
                        compiledCounterWrites++;
                        compiledCounter = Convert.ToInt32(arguments[0],
                            CultureInfo.InvariantCulture);
                        return compiledCounter;
                    },
                    [cityRead] = arguments => compiledCity,
                    [cityWrite] = arguments =>
                    {
                        compiledCityWrites++;
                        compiledCity = Convert.ToString(arguments[0],
                            CultureInfo.InvariantCulture);
                        return null;
                    },
                    [scoresRead] = arguments => compiledScores[
                        Convert.ToInt32(arguments[0],
                            CultureInfo.InvariantCulture)],
                    [scoresWrite] = arguments =>
                    {
                        compiledScoresWrites++;
                        compiledScores[Convert.ToInt32(arguments[0],
                            CultureInfo.InvariantCulture)] = Convert.ToInt32(
                            arguments[1], CultureInfo.InvariantCulture);
                        return null;
                    }
                });
                DdfRuntimeExecutionResult compiled = ExecuteForVerification(
                    planned.Plan, emission, new List<string>(),
                    capabilityProvider: provider);
                Require(vm.Succeeded && Convert.ToInt32(vm.ReturnValue,
                            CultureInfo.InvariantCulture) == 42 &&
                        Convert.ToInt32(compiled.Value,
                            CultureInfo.InvariantCulture) == 42 &&
                        vmEffects == 1 && compiledEffects == 1 &&
                        vmCounter == 18 && compiledCounter == 18 &&
                        (string)((IReadOnlyDictionary<string, object>)
                            ((IReadOnlyDictionary<string, object>)vmPanel[
                                "customer"])["address"])["city"] == "DDF" &&
                        compiledCity == "DDF" && vmScores[1] == 18 &&
                        compiledScores[1] == 18 && vmCounterWrites == 1 &&
                        vmPanelWrites == 1 && compiledCounterWrites == 1 &&
                        compiledCityWrites == 1 && vmScoresWrites == 1 &&
                        compiledScoresWrites == 1,
                    "Risultato o cardinalità degli effetti host divergenti.");
                Require(compiled.CapabilityEffects.Disposition ==
                        DdfRuntimeCapabilityEffectDisposition.Applied &&
                    compiled.CapabilityEffects.Effects.Select(effect =>
                        effect.CapabilityId).SequenceEqual(new[]
                        {
                            rememberCapability, counterWrite, cityWrite,
                            scoresWrite
                        }) &&
                    compiled.CapabilityEffects.Effects.All(effect =>
                        effect.Status ==
                            DdfRuntimeCapabilityEffectStatus.Applied),
                    "La ricevuta compilata non distingue esattamente i quattro effetti mutanti.");

                var transactionEvents = new List<string>();
                DdfRuntimeExecutionResult transactional = ExecuteForVerification(
                    planned.Plan, emission, new List<string>(),
                    capabilityProvider: provider,
                    capabilityEffectPolicy:
                        DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure,
                    capabilityTransactionFactory:
                        new CompilerTransactionFactory(transactionEvents));
                Require(Convert.ToInt32(transactional.Value,
                            CultureInfo.InvariantCulture) == 42 &&
                        transactional.CapabilityEffects.Disposition ==
                            DdfRuntimeCapabilityEffectDisposition.Committed &&
                        transactional.CapabilityEffects.TransactionId ==
                            "compiled-tx" &&
                        provider.LastTransactionId == "compiled-tx" &&
                        transactionEvents.SequenceEqual(new[]
                        {
                            "begin", "commit", "dispose"
                        }),
                    "L'assembly compilato non ha completato la transazione capability.");

                DdfRuntimeException denied = null;
                try
                {
                    ExecuteForVerification(planned.Plan, emission,
                        new List<string>());
                }
                catch (DdfRuntimeException exception)
                {
                    denied = exception;
                }
                Require(denied?.Code == "DDF409" && denied.Location.Start ==
                        source.IndexOf("hostRemember", StringComparison.Ordinal),
                    "Il runner deny-by-default non ha prodotto DDF409 sulla chiamata host.");
                Console.WriteLine("PASS bridge capability standalone esplicito: " +
                    "manifest, deny-by-default, risultato ed effetto VM/Roslyn");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void VerifiesCompiledCapabilitySuspension()
        {
            VerifiesCompiledCapabilitySuspensionAsync().GetAwaiter().GetResult();
        }

        private static async Task VerifiesCompiledCapabilitySuspensionAsync()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-compiled-async-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n    ret hostWait(41) + 1;\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "async.test", "Async test", "Test capability asincrona."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "hostWait", "int", new[]
                    {
                        new DdfHostParameterDefinition("value", "int")
                    }, arguments => arguments[0],
                    requiredPermissions: new[] { "async.test" }));
                catalog.SetPermission("async.test", true);
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Compiled async suspension", package, "main.ddf");
                project.Module.Id = "ddf.test.compiled-async";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success,
                    "Il test async non supera la pipeline Core.");
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                Require(planned.Success, "Piano Roslyn async non valido.");
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit Roslyn async fallito: " +
                    string.Join("; ", emission.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                var provider = new SuspendingCapabilityProvider(
                    DdfDotNetHostCapabilities.ForFunction("hostWait"));
                Task<DdfRuntimeExecutionResult> execution =
                    ExecuteForVerificationAsync(planned.Plan, emission,
                        provider);
                await provider.Entered.Task.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(!execution.IsCompleted && provider.SyncInvocations == 0,
                    "L'assembly Roslyn non si è sospeso in modo cooperativo o " +
                    "ha usato il fallback sincrono.");
                provider.Release.TrySetResult(41);
                DdfRuntimeExecutionResult result = await execution.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(Convert.ToInt32(result.Value,
                            CultureInfo.InvariantCulture) == 42 &&
                        provider.AsyncInvocations == 1 &&
                        provider.SyncInvocations == 0,
                    "La ripresa della capability Roslyn async non è deterministica.");

                var timedOutProvider = new LateCapabilityProvider(
                    DdfDotNetHostCapabilities.ForFunction("hostWait"));
                Task<DdfRuntimeExecutionResult> timedOutExecution =
                    ExecuteForVerificationAsync(planned.Plan, emission,
                        timedOutProvider, capabilityTimeout:
                            TimeSpan.FromMilliseconds(100));
                await timedOutProvider.Entered.Task.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                DdfRuntimeException timeoutError = null;
                try { await timedOutExecution.ConfigureAwait(false); }
                catch (DdfRuntimeException exception)
                {
                    timeoutError = exception;
                }
                Require(timeoutError?.Code == "DDF414" &&
                        timedOutProvider.Resource.DisposeCount == 1 &&
                        !timedOutProvider.Completed.Task.IsCompleted,
                    "Timeout o cleanup della capability compilata sospesa non " +
                    "sono deterministici.");
                timedOutProvider.Release.TrySetResult(99);
                await timedOutProvider.Completed.Task.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(timedOutProvider.Resource.DisposeCount == 1,
                    "Una risposta tardiva ha riaperto o rilasciato due volte " +
                    "la risorsa capability.");

                using var stop = new CancellationTokenSource();
                var stoppedProvider = new LateCapabilityProvider(
                    DdfDotNetHostCapabilities.ForFunction("hostWait"));
                Task<DdfRuntimeExecutionResult> stoppedExecution =
                    ExecuteForVerificationAsync(planned.Plan, emission,
                        stoppedProvider, stop.Token,
                        TimeSpan.FromSeconds(5));
                await stoppedProvider.Entered.Task.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                stop.Cancel();
                bool cancelled = false;
                try { await stoppedExecution.ConfigureAwait(false); }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled &&
                        stoppedProvider.Resource.DisposeCount == 1 &&
                        !stoppedProvider.Completed.Task.IsCompleted,
                    "Stop non ha cancellato e ripulito l'assembly sospeso.");
                stoppedProvider.Release.TrySetResult(100);
                await stoppedProvider.Completed.Task.WaitAsync(
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(stoppedProvider.Resource.DisposeCount == 1,
                    "La risposta successiva a Stop ha alterato il cleanup.");
                Console.WriteLine("PASS assembly Roslyn: sospensione, ripresa, timeout, Stop, risposta tardiva e cleanup");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void MatchesCleanupOrderAcrossVmAndCompiledBackend()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-cleanup-differential-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    int mode << hostAcquire();\n" +
                    "    if(mode == 1)\n    {\n" +
                    "        int zero << 0;\n" +
                    "        ret 1 / zero;\n    }\n" +
                    "    ret 42;\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfEditorHostCatalog buildCatalog = CreateCleanupCatalog(
                    CleanupScenario.Success, new List<string>());
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Cleanup differential", package, "main.ddf");
                project.Module.Id = "ddf.test.cleanup-differential";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, buildCatalog.Snapshot);
                Require(prepared.Success,
                    "Il caso cleanup non supera la pipeline Core: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                Require(planned.Success,
                    "Piano Roslyn cleanup differenziale non valido.");
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success,
                    "Emit cleanup differenziale fallito: " + string.Join("; ",
                        emission.Issues.Select(issue => issue.Code + " " +
                            issue.Message)));
                string capability = DdfDotNetHostCapabilities.ForFunction(
                    "hostAcquire");

                foreach (CleanupScenario scenario in new[]
                         {
                             CleanupScenario.Success,
                             CleanupScenario.RuntimeError,
                             CleanupScenario.Timeout,
                             CleanupScenario.CleanupFailure
                         })
                {
                    var vmEvents = new List<string>();
                    var compiledEvents = new List<string>();
                    DdfExecutionResult vm = DdfInterpreter.Execute(
                        prepared.Artifact.RuntimeModules, package.Language,
                        new DdfExecutionOptions
                        {
                            EntryPoint = "main",
                            HostCatalog = CreateCleanupCatalog(scenario,
                                vmEvents).Snapshot,
                            HostCallTimeout = scenario == CleanupScenario.Timeout
                                ? TimeSpan.FromMilliseconds(75)
                                : TimeSpan.FromSeconds(2)
                        });
                    var provider = new CleanupMatrixCapabilityProvider(
                        capability, scenario, compiledEvents);
                    DdfRuntimeExecutionResult compiled = null;
                    DdfRuntimeException compiledError = null;
                    try
                    {
                        compiled = ExecuteForVerificationAsync(planned.Plan,
                                emission, provider, capabilityTimeout:
                                    scenario == CleanupScenario.Timeout
                                        ? TimeSpan.FromMilliseconds(75)
                                        : TimeSpan.FromSeconds(2))
                            .GetAwaiter().GetResult();
                    }
                    catch (DdfRuntimeException exception)
                    {
                        compiledError = exception;
                    }

                    string[] expectedOrder = scenario ==
                        CleanupScenario.CleanupFailure
                        ? new[] { "third", "failing", "first" }
                        : new[] { "second", "first" };
                    Require(vmEvents.SequenceEqual(expectedOrder) &&
                            compiledEvents.SequenceEqual(expectedOrder) &&
                            vmEvents.SequenceEqual(compiledEvents),
                        scenario + ": ordine cleanup divergente. VM=<" +
                        string.Join(",", vmEvents) + "> Roslyn=<" +
                        string.Join(",", compiledEvents) + ">.");

                    if (scenario == CleanupScenario.Success)
                    {
                        Require(vm.Succeeded && compiledError == null &&
                                compiled != null &&
                                Convert.ToInt32(vm.ReturnValue,
                                    CultureInfo.InvariantCulture) == 42 &&
                                Convert.ToInt32(compiled.Value,
                                    CultureInfo.InvariantCulture) == 42,
                            "Il cleanup sul successo diverge fra VM e Roslyn.");
                    }
                    else
                    {
                        string expectedCode = scenario switch
                        {
                            CleanupScenario.RuntimeError => "DDF404",
                            CleanupScenario.Timeout => "DDF414",
                            CleanupScenario.CleanupFailure => "DDF415",
                            _ => throw new InvalidOperationException()
                        };
                        Require(!vm.Succeeded && vm.Diagnostics.Any(item =>
                                    item.Code == expectedCode) &&
                                compiled == null &&
                                compiledError?.Code == expectedCode,
                            scenario + ": esito o diagnostica cleanup divergente; " +
                            "atteso " + expectedCode + ".");
                    }
                }

                var transferCases = new[]
                {
                    new
                    {
                        Name = "continue",
                        Source = "main() out int\n{\n    int mode << hostAcquire();\n" +
                            "    int total << 0;\n" +
                            "    for(int index << 0; index < 3; index++)\n    {\n" +
                            "        if(index == 1) { continue; }\n" +
                            "        total++;\n    }\n    ret total + 40;\n}\n",
                        ExpectedValue = 42,
                        Ended = false
                    },
                    new
                    {
                        Name = "break",
                        Source = "main() out int\n{\n    int mode << hostAcquire();\n" +
                            "    int total << 2;\n    while(true)\n    {\n" +
                            "        brk;\n        total << 1000;\n    }\n" +
                            "    ret total + 40;\n}\n",
                        ExpectedValue = 42,
                        Ended = false
                    },
                    new
                    {
                        Name = "return",
                        Source = "main() out int\n{\n    int mode << hostAcquire();\n" +
                            "    if(mode == 0) { ret 42; }\n    ret 0;\n}\n",
                        ExpectedValue = 42,
                        Ended = false
                    },
                    new
                    {
                        Name = "end",
                        Source = "main() out int\n{\n    int mode << hostAcquire();\n" +
                            "    for(int index << 0; index < 2; index++)\n    {\n" +
                            "        if(index == 0) { end; }\n    }\n    ret 0;\n}\n",
                        ExpectedValue = 0,
                        Ended = true
                    }
                };
                foreach (var transfer in transferCases)
                {
                    string caseRoot = Path.Combine(root, "transfer-" +
                        transfer.Name);
                    Directory.CreateDirectory(caseRoot);
                    File.WriteAllText(Path.Combine(caseRoot, "main.ddf"),
                        transfer.Source, new UTF8Encoding(false));
                    DdfProjectManifest transferProject =
                        DdfProjectManifest.CreateImplicit(
                            "Cleanup transfer " + transfer.Name, package,
                            "main.ddf");
                    transferProject.Module.Id =
                        "ddf.test.cleanup-transfer." + transfer.Name;
                    transferProject.Module.Version = "1.0.0";
                    transferProject.Module.Exports.Add("main");
                    DdfBuildResult transferPrepared = new DdfBuildService()
                        .Validate(caseRoot, transferProject, package, null,
                            buildCatalog.Snapshot);
                    Require(transferPrepared.Success,
                        transfer.Name + ": preparazione cleanup fallita — " +
                        string.Join("; ", transferPrepared.Issues));
                    DdfDotNetCompilationPlanResult transferPlanned =
                        DdfDotNetCompilationPlanner.Create(transferPrepared,
                            new DdfDotNetCompilationOptions("net10.0",
                                DdfRuntimeAbi.Version,
                                referenceSet: references));
                    Require(transferPlanned.Success,
                        transfer.Name + ": piano cleanup non valido.");
                    DdfDotNetArtifactEmissionResult transferEmission =
                        DdfDotNetArtifactEmitter.Emit(transferPlanned.Plan,
                            package.Language.Types.ClrMapping);
                    Require(transferEmission.Success,
                        transfer.Name + ": emit cleanup fallito — " +
                        string.Join("; ", transferEmission.Issues));

                    var vmTransferEvents = new List<string>();
                    DdfExecutionResult vmTransfer = DdfInterpreter.Execute(
                        transferPrepared.Artifact.RuntimeModules,
                        package.Language, new DdfExecutionOptions
                        {
                            EntryPoint = "main",
                            HostCatalog = CreateCleanupCatalog(
                                CleanupScenario.Success,
                                vmTransferEvents).Snapshot
                        });
                    var compiledTransferEvents = new List<string>();
                    var transferProvider = new CleanupMatrixCapabilityProvider(
                        capability, CleanupScenario.Success,
                        compiledTransferEvents);
                    DdfRuntimeExecutionResult compiledTransfer =
                        ExecuteForVerificationAsync(transferPlanned.Plan,
                            transferEmission, transferProvider)
                        .GetAwaiter().GetResult();
                    string[] expectedTransferOrder = { "second", "first" };
                    Require(vmTransfer.Succeeded &&
                            vmTransfer.WasTerminated == transfer.Ended &&
                            compiledTransfer.Ended == transfer.Ended &&
                            vmTransferEvents.SequenceEqual(
                                expectedTransferOrder) &&
                            compiledTransferEvents.SequenceEqual(
                                expectedTransferOrder),
                        transfer.Name +
                        ": cleanup singolo/LIFO o terminazione divergente. VM=<" +
                        string.Join(",", vmTransferEvents) + "> Roslyn=<" +
                        string.Join(",", compiledTransferEvents) + ">.");
                    if (!transfer.Ended)
                    {
                        Require(Convert.ToInt32(vmTransfer.ReturnValue,
                                    CultureInfo.InvariantCulture) ==
                                transfer.ExpectedValue &&
                                Convert.ToInt32(compiledTransfer.Value,
                                    CultureInfo.InvariantCulture) ==
                                transfer.ExpectedValue,
                            transfer.Name +
                            ": risultato divergente durante il cleanup.");
                    }
                }

                var vmStopEvents = new List<string>();
                using var vmStop = new CancellationTokenSource();
                using var vmEntered = new ManualResetEventSlim(false);
                Task<DdfExecutionResult> vmStopped = Task.Run(() =>
                    DdfInterpreter.Execute(prepared.Artifact.RuntimeModules,
                        package.Language, new DdfExecutionOptions
                        {
                            EntryPoint = "main",
                            HostCatalog = CreateCleanupCatalog(
                                CleanupScenario.Stop, vmStopEvents,
                                vmEntered).Snapshot,
                            HostCallTimeout = TimeSpan.FromSeconds(5),
                            CancellationToken = vmStop.Token
                        }));
                Require(vmEntered.Wait(TimeSpan.FromSeconds(5)),
                    "La VM non ha raggiunto la capability prima dello Stop.");
                vmStop.Cancel();
                Require(vmStopped.Wait(TimeSpan.FromSeconds(5)) &&
                        vmStopped.Result.WasCancelled,
                    "La VM non ha completato lo Stop differenziale.");

                var compiledStopEvents = new List<string>();
                using var compiledStop = new CancellationTokenSource();
                var stoppedProvider = new CleanupMatrixCapabilityProvider(
                    capability, CleanupScenario.Stop, compiledStopEvents);
                Task<DdfRuntimeExecutionResult> compiledStopped =
                    ExecuteForVerificationAsync(planned.Plan, emission,
                        stoppedProvider, compiledStop.Token,
                        TimeSpan.FromSeconds(5));
                Require(stoppedProvider.Entered.Wait(TimeSpan.FromSeconds(5)),
                    "Roslyn non ha raggiunto la capability prima dello Stop.");
                compiledStop.Cancel();
                bool compiledCancelled = false;
                try
                {
                    compiledStopped.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    compiledCancelled = true;
                }
                Require(compiledCancelled &&
                        vmStopEvents.SequenceEqual(new[] { "second", "first" }) &&
                        compiledStopEvents.SequenceEqual(vmStopEvents),
                    "Stop o ordine cleanup divergente fra VM e Roslyn.");

                Console.WriteLine("PASS cleanup differenziale VM/Roslyn: " +
                    "continue, brk, ret, end, successo, errore, timeout, Stop e fallimento in ordine LIFO");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void PersistsStateAcrossVmAndCompiledSessions()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-state-differential-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "state int calls << 0;\n" +
                    "main() out int\n{\n    calls++;\n    ret calls;\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "State differential", package, "main.ddf");
                project.Module.Id = "ddf.test.state-differential";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package);
                Require(prepared.Success,
                    "Il caso state non supera la pipeline Core: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                Require(planned.Success, "Piano Roslyn state non valido.");
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit state fallito: " +
                    string.Join("; ", emission.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));

                var vmState = new DdfStateStore();
                DdfExecutionResult vmFirst = DdfInterpreter.Execute(
                    prepared.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        StateStore = vmState
                    });
                DdfExecutionResult vmSecond = DdfInterpreter.Execute(
                    prepared.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions
                    {
                        EntryPoint = "main",
                        StateStore = vmState
                    });
                Require(vmFirst.Succeeded && vmSecond.Succeeded &&
                        Convert.ToInt32(vmFirst.ReturnValue,
                            CultureInfo.InvariantCulture) == 1 &&
                        Convert.ToInt32(vmSecond.ReturnValue,
                            CultureInfo.InvariantCulture) == 2,
                    "La VM non ha mantenuto state fra due esecuzioni.");

                DdfDotNetLinkedBundle bundle = CreateBundle(planned.Plan,
                    emission, references);
                DdfDotNetRuntimeEnvironment environment = new(
                    DdfDotNetArtifactHostKind.Standalone, "net10.0",
                    DdfRuntimeAbi.Version, string.Empty,
                    Array.Empty<string>());
                PersistedRuntimeStateSnapshot firstSnapshot;
                using (DdfDotNetStandaloneSession session =
                       LoadSession(bundle, references, environment))
                {
                    DdfRuntimeExecutionResult result = session.Execute(
                        new DdfRuntimeContext(100000));
                    firstSnapshot = session.CaptureState();
                    Require(Convert.ToInt32(result.Value,
                                CultureInfo.InvariantCulture) == 1 &&
                            firstSnapshot.Entries.Count == 1 &&
                            (int)firstSnapshot.Entries[0].Value == 1,
                        "La prima sessione compilata non ha catturato state.");
                }

                PersistedRuntimeStateSnapshot secondSnapshot;
                using (DdfDotNetStandaloneSession session =
                       LoadSession(bundle, references, environment))
                {
                    session.RestoreState(firstSnapshot);
                    DdfRuntimeExecutionResult result = session.Execute(
                        new DdfRuntimeContext(100000));
                    secondSnapshot = session.CaptureState();
                    Require(Convert.ToInt32(result.Value,
                                CultureInfo.InvariantCulture) == 2 &&
                            (int)secondSnapshot.Entries.Single().Value == 2,
                        "La seconda sessione compilata non ha ripristinato state.");
                }
                Require(secondSnapshot.Entries.Single().Key ==
                            vmState.Snapshot().Single().Key &&
                        secondSnapshot.Entries.Single().TypeName == "int" &&
                        (int)secondSnapshot.Entries.Single().Value ==
                            (int)vmState.Snapshot().Single().Value,
                    "Chiave, tipo o valore state divergono fra VM e Roslyn.");

                using (DdfDotNetStandaloneSession session =
                       LoadSession(bundle, references, environment))
                {
                    bool languageRejected = false;
                    try
                    {
                        session.RestoreState(new PersistedRuntimeStateSnapshot(
                            PersistedRuntimeStateSnapshot.CurrentSchemaVersion,
                            "different-language",
                            firstSnapshot.LanguagePackageVersion,
                            firstSnapshot.Entries));
                    }
                    catch (InvalidOperationException)
                    {
                        languageRejected = true;
                    }
                    Require(languageRejected,
                        "Uno snapshot di un altro linguaggio è stato accettato.");
                }

                using (DdfDotNetStandaloneSession session =
                       LoadSession(bundle, references, environment))
                {
                    PersistedRuntimeStateEntry current =
                        firstSnapshot.Entries.Single();
                    session.RestoreState(new PersistedRuntimeStateSnapshot(
                        PersistedRuntimeStateSnapshot.CurrentSchemaVersion,
                        firstSnapshot.LanguageId,
                        firstSnapshot.LanguagePackageVersion,
                        new[] { new PersistedRuntimeStateEntry(current.Key,
                            "string", "wrong-type") }));
                    bool typeRejected = false;
                    try
                    {
                        session.Execute(new DdfRuntimeContext(100000));
                    }
                    catch (InvalidOperationException)
                    {
                        typeRejected = true;
                    }
                    bool failedStateUnavailable = false;
                    try { session.CaptureState(); }
                    catch (InvalidOperationException)
                    {
                        failedStateUnavailable = true;
                    }
                    Require(typeRejected && failedStateUnavailable,
                        "Uno state incompatibile è stato applicato o committato.");
                }

                using (DdfDotNetStandaloneSession session =
                       LoadSession(bundle, references, environment))
                {
                    session.RestoreState(firstSnapshot);
                    DdfRuntimeExecutionResult result = session.Execute(
                        new DdfRuntimeContext(100000));
                    Require(Convert.ToInt32(result.Value,
                                CultureInfo.InvariantCulture) == 2,
                        "Un tentativo state fallito ha alterato lo snapshot committato.");
                }
                Console.WriteLine("PASS state persistente VM/Roslyn: " +
                    "chiavi stabili, restore tipizzato, commit e rollback");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void PreservesTypedErrorsAcrossVmAndCompiledFiles()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-error-differential-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string mainSource =
                    "main() out int\n{\n    ret bridge();\n}\n";
                const string helperSource =
                    "fail() out int\n{\n    int zero << 0;\n" +
                    "    ret 42 / zero;\n}\n\n" +
                    "bridge() out int\n{\n    ret fail();\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), mainSource,
                    new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "errors.ddf"), helperSource,
                    new UTF8Encoding(false));

                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Typed error differential", package, "main.ddf");
                project.Module.Id = "ddf.test.error-differential";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DifferentialExecution execution = ExecuteDifferential(root,
                    project, package, references);
                RequireEquivalentTypedError(execution,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["main.ddf"] = mainSource,
                        ["errors.ddf"] = helperSource
                    }, new[] { "fail", "bridge", "main" }, new[]
                    {
                        "errors.ddf", "errors.ddf", "main.ddf"
                    }, "multifile");

                string moduleRoot = Path.Combine(root, "module-workspace");
                string moduleSample = Path.Combine(RepositoryRoot(), "samples",
                    "module-workspace");
                foreach (string file in Directory.EnumerateFiles(moduleSample,
                             "*", SearchOption.AllDirectories))
                {
                    string destination = Path.Combine(moduleRoot,
                        Path.GetRelativePath(moduleSample, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination);
                }
                const string scaleSource =
                    "scale(int value) out int\n{\n    int zero << 0;\n" +
                    "    ret value / zero;\n}\n";
                string scalePath = Path.Combine(moduleRoot, ".ddf", "modules",
                    "ddf.sample.scale", "1.0.0", "scale.ddf");
                File.WriteAllText(scalePath, scaleSource,
                    new UTF8Encoding(false));
                DdfProjectManifestLoadResult loaded =
                    DdfProjectManifestLoader.Load(moduleRoot);
                Require(loaded.Success,
                    "Il workspace modulare per gli errori non è valido.");
                DifferentialExecution moduleExecution = ExecuteDifferential(
                    moduleRoot, loaded.Manifest, package, references);
                string moduleMainSource = File.ReadAllText(Path.Combine(
                    moduleRoot, "main.ddf"), Encoding.UTF8);
                RequireEquivalentTypedError(moduleExecution,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["main.ddf"] = moduleMainSource,
                        ["scale.ddf"] = scaleSource
                    }, new[] { "scale", "main" }, new[]
                    {
                        "scale.ddf", "main.ddf"
                    }, "modulo versionato");

                Console.WriteLine("PASS errori tipizzati VM/Roslyn: codice, " +
                    "tipo, messaggio, posizione e stack invariati fra file e moduli");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RequireEquivalentTypedError(
            DifferentialExecution execution,
            IReadOnlyDictionary<string, string> sources,
            IReadOnlyList<string> expectedFunctions,
            IReadOnlyList<string> expectedFiles, string scenario)
        {
            Require(!execution.Vm.Succeeded && execution.Vm.Error != null &&
                    execution.Compiled == null &&
                    execution.CompiledError != null,
                "Il caso errore " + scenario +
                " non è fallito su entrambi i backend.");
            DdfErrorValue vmError = execution.Vm.Error;
            DdfRuntimeException compiledError = execution.CompiledError;
            Require(DdfErrorCatalog.TryGetByCode(compiledError.Code,
                        out DdfErrorDefinition compiledDefinition) &&
                    vmError.Code == compiledError.Code &&
                    vmError.TypeId == compiledDefinition.TypeId &&
                    vmError.Message == compiledError.Message &&
                    vmError.Start == compiledError.Location.Start &&
                    vmError.Length == compiledError.Location.Length &&
                    Path.GetFileName(vmError.DocumentPath) ==
                    Path.GetFileName(compiledError.Location.DocumentPath),
                scenario + ": codice, tipo, messaggio o posizione divergono.");
            Require(vmError.Stack.Count == expectedFunctions.Count &&
                    compiledError.Frames.Count == vmError.Stack.Count,
                scenario + ": cardinalità dello stack DDF non valida.");
            for (int index = 0; index < vmError.Stack.Count; index++)
            {
                DdfErrorStackFrame vmFrame = vmError.Stack[index];
                DdfRuntimeErrorFrame compiledFrame = compiledError.Frames[index];
                string declarationFile = Path.GetFileName(
                    compiledFrame.Declaration.DocumentPath);
                Require(sources.TryGetValue(declarationFile,
                        out string declarationSource),
                    scenario + ": sorgente dichiarazione non riconosciuto: " +
                    declarationFile + ".");
                string declarationText = declarationSource.Substring(
                    compiledFrame.Declaration.Start,
                    compiledFrame.Declaration.Length);
                Require(declarationText.StartsWith(vmFrame.FunctionName + "(",
                            StringComparison.Ordinal) &&
                        compiledFrame.Location.Start <= vmFrame.Start &&
                        compiledFrame.Location.Start +
                            compiledFrame.Location.Length >=
                            vmFrame.Start + vmFrame.Length &&
                        Path.GetFileName(vmFrame.DocumentPath) ==
                        Path.GetFileName(compiledFrame.Location.DocumentPath),
                    scenario + ": frame errore " + index +
                    " divergente fra VM e Roslyn.");
            }
            Require(vmError.Stack.Select(frame => frame.FunctionName)
                    .SequenceEqual(expectedFunctions) &&
                    vmError.Stack.Select(frame => Path.GetFileName(
                            frame.DocumentPath)).SequenceEqual(expectedFiles),
                scenario + ": ordine o provenienza dello stack non validi.");
        }

        private static DdfDotNetLinkedBundle CreateBundle(
            DdfDotNetCompilationPlan plan,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetReferenceSet references)
        {
            DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker.Link(
                plan.EntryModuleIdentity,
                emission.Artifacts.Select(item => item.Manifest));
            Require(linked.Success, "Link state fallito: " +
                string.Join("; ", linked.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            DdfDotNetLinkedBundleResult bundled =
                DdfDotNetLinkedBundleBuilder.Create(linked.Program, emission,
                    references);
            Require(bundled.Success, "Bundle state fallito: " +
                string.Join("; ", bundled.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            return bundled.Bundle;
        }

        private static DdfDotNetStandaloneSession LoadSession(
            DdfDotNetLinkedBundle bundle, DdfDotNetReferenceSet references,
            DdfDotNetRuntimeEnvironment environment)
        {
            DdfDotNetStandaloneLoadResult loaded =
                DdfDotNetStandaloneLoader.Load(bundle, references,
                    environment);
            Require(loaded.Success, "Load state fallito: " +
                string.Join("; ", loaded.Issues.Select(issue =>
                    issue.Code + " " + issue.Message)));
            return loaded.Session;
        }

        private static DdfEditorHostCatalog CreateCleanupCatalog(
            CleanupScenario scenario, IList<string> events,
            ManualResetEventSlim entered = null)
        {
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "hostAcquire", "int",
                Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: async (context, arguments) =>
                {
                    RegisterCleanupResources(scenario, events,
                        context.RegisterResource,
                        context.RegisterAsyncResource);
                    entered?.Set();
                    if (scenario is CleanupScenario.Timeout or
                        CleanupScenario.Stop)
                        await Task.Delay(Timeout.InfiniteTimeSpan,
                            context.CancellationToken).ConfigureAwait(false);
                    return scenario == CleanupScenario.RuntimeError ? 1 : 0;
                }));
            return catalog;
        }

        private static void RegisterCleanupResources(CleanupScenario scenario,
            IList<string> events, Action<IDisposable> register,
            Action<IAsyncDisposable> registerAsync)
        {
            register(new OrderedCleanupResource("first", events));
            if (scenario == CleanupScenario.CleanupFailure)
            {
                register(new OrderedCleanupResource("failing", events,
                    throwOnDispose: true));
                registerAsync(new OrderedAsyncCleanupResource("third", events));
            }
            else
                registerAsync(new OrderedAsyncCleanupResource("second", events));
        }

        private static void RunsReadOnlyFileBrokerOutOfProcess()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-file-broker-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    string value << brokerReadText(\"config\");\n" +
                    "    print(value);\n" +
                    "    ret length(value);\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                string brokerFile = Path.Combine(root, "config.txt");
                File.WriteAllText(brokerFile, "broker", new UTF8Encoding(false));
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "broker.file.read", "Lettura file broker",
                    "Legge soltanto snapshot preautorizzati."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "brokerReadText", "string", new[]
                    {
                        new DdfHostParameterDefinition("logicalName", "string")
                    }, arguments => string.Empty,
                    requiredPermissions: new[] { "broker.file.read" }));
                catalog.SetPermission("broker.file.read", true);
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "File broker", package, "main.ddf");
                project.Module.Id = "ddf.test.file-broker";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success, "Preparazione file broker fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit file broker fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success, "Bundle file broker fallito.");
                string capability = DdfDotNetWorkerBrokerCapabilities.ReadText;
                var policy = DdfDotNetWorkerAccessPolicy.DenyAll(
                    OperatingSystem.IsWindows());
                policy.FileSystemAccess =
                    DdfDotNetWorkerFileSystemAccess.BrokeredRead;
                policy.AllowedBrokerCapabilities.Add(capability);
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version,
                    hostCapabilities: new[] { capability });
                DdfDotNetWorkerFileSnapshot snapshot =
                    DdfDotNetWorkerFileSnapshot.FromFile("config", brokerFile);
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-file-broker", policy,
                        new[] { snapshot });
                string serializedRequest = DdfDotNetWorkerProtocol
                    .SerializeRequest(request);
                Require(!serializedRequest.Contains(brokerFile,
                        StringComparison.OrdinalIgnoreCase),
                    "Il percorso originale del file broker è entrato nell'IPC.");
                string savedHash = snapshot.Sha256;
                snapshot.Sha256 = new string('0', 64);
                DdfDotNetWorkerPackageResult tampered =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied: OperatingSystem.IsWindows());
                Require(!tampered.Success && tampered.Issues.Any(issue =>
                        issue.Code == "DDFC539"),
                    "Lo snapshot file broker manomesso è stato accettato.");
                snapshot.Sha256 = savedHash;
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "6" &&
                        execution.Response.Output.SequenceEqual(
                            new[] { "broker" }),
                    "Il file broker read-only non ha eseguito lo snapshot: " +
                    string.Join("; ", execution.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                request.BrokerFiles.Clear();
                DdfDotNetWorkerExecutionResult missingResource =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!missingResource.Success &&
                        missingResource.Issues.Any(issue =>
                            issue.Code == "DDF409"),
                    "Il worker ha consentito una risorsa logica non autorizzata: " +
                    string.Join("; ", missingResource.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                request.BrokerFiles.Add(snapshot);
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                var directoriesBefore = Directory.GetDirectories(temporaryRoot,
                        "ddf-worker-*").Select(Path.GetFullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                for (int repetition = 0; repetition < 5; repetition++)
                {
                    request.CorrelationId = "compiler-file-broker-stress-" +
                        repetition.ToString(CultureInfo.InvariantCulture);
                    DdfDotNetWorkerExecutionResult repeated =
                        DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                            TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                    Require(repeated.Success &&
                            repeated.Response.ResultJson == "6",
                        "Sessione worker ripetuta fallita al ciclo " +
                        repetition + ".");
                }
                string[] leakedDirectories = Directory.GetDirectories(
                        temporaryRoot, "ddf-worker-*")
                    .Select(Path.GetFullPath)
                    .Where(path => !directoriesBefore.Contains(path)).ToArray();
                Require(leakedDirectories.Length == 0,
                    "Il worker ha lasciato directory temporanee: " +
                    string.Join(", ", leakedDirectories));
                Console.WriteLine("PASS file broker read-only fuori processo: " +
                    "snapshot, hash, capability minima, stress e cleanup");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RunsTransactionalFileBrokerOutOfProcess(
            bool requireReducedToken = true)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-file-broker-write-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    bool saved << brokerWriteText(\"config\", \"updated\");\n" +
                    "    if(saved)\n    {\n        ret 1;\n    }\n" +
                    "    ret 0;\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                string brokerFile = Path.Combine(root, "config.txt");
                File.WriteAllText(brokerFile, "original",
                    new UTF8Encoding(false));
                string brokerLink = Path.Combine(root, "config-link.txt");
                try
                {
                    File.CreateSymbolicLink(brokerLink, brokerFile);
                    bool rejectedLink = false;
                    try
                    {
                        DdfDotNetWorkerFileCommitTarget.FromFile("linked",
                            brokerLink);
                    }
                    catch (IOException) { rejectedLink = true; }
                    Require(rejectedLink,
                        "Il target broker ha accettato un link simbolico.");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (PlatformNotSupportedException) { }
                finally
                {
                    try { if (File.Exists(brokerLink)) File.Delete(brokerLink); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "broker.file.write", "Scrittura file broker",
                    "Scrive soltanto snapshot preautorizzati."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "brokerWriteText", "bool", new[]
                    {
                        new DdfHostParameterDefinition("logicalName", "string"),
                        new DdfHostParameterDefinition("content", "string")
                    }, arguments => true,
                    requiredPermissions: new[] { "broker.file.write" },
                    effect: DdfHostFunctionEffect.HostMutation));
                catalog.SetPermission("broker.file.write", true);
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "File broker write", package, "main.ddf");
                project.Module.Id = "ddf.test.file-broker-write";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success,
                    "Preparazione file broker write fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit file broker write fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success, "Bundle file broker write fallito.");
                string capability = DdfDotNetWorkerBrokerCapabilities.WriteText;
                var policy = DdfDotNetWorkerAccessPolicy.DenyAll(
                    requireReducedToken && OperatingSystem.IsWindows());
                policy.FileSystemAccess =
                    DdfDotNetWorkerFileSystemAccess.BrokeredReadWrite;
                policy.AllowedBrokerCapabilities.Add(capability);
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version,
                    hostCapabilities: new[] { capability });
                DdfDotNetWorkerFileSnapshot snapshot =
                    DdfDotNetWorkerFileSnapshot.FromFile("config", brokerFile,
                        writable: true);
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-file-broker-write", policy,
                        new[] { snapshot });
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult missingTarget =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!missingTarget.Success && missingTarget.Issues.Any(
                        issue => issue.Code == "DDFC540") &&
                    File.ReadAllText(brokerFile) == "original",
                    "Una scrittura senza target host non è stata bloccata.");
                DdfDotNetWorkerFileCommitTarget target =
                    DdfDotNetWorkerFileCommitTarget.FromFile("config",
                        brokerFile);
                File.Delete(brokerFile);
                File.WriteAllText(brokerFile, "original",
                    new UTF8Encoding(false));
                DdfDotNetWorkerExecutionResult replacedIdentity =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), new[] { target })
                    .GetAwaiter().GetResult();
                Require(!replacedIdentity.Success && replacedIdentity.Issues
                        .Any(issue => issue.Code == "DDFC541") &&
                    File.ReadAllText(brokerFile) == "original",
                    "La sostituzione del file con contenuto identico non è " +
                    "stata rilevata tramite identità fisica.");
                target = DdfDotNetWorkerFileCommitTarget.FromFile("config",
                    brokerFile);
                File.WriteAllText(brokerFile, "external",
                    new UTF8Encoding(false));
                DdfDotNetWorkerExecutionResult conflict =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), new[] { target })
                    .GetAwaiter().GetResult();
                Require(!conflict.Success && conflict.Issues.Any(issue =>
                        issue.Code == "DDFC540") &&
                    File.ReadAllText(brokerFile) == "external",
                    "Il commit broker non ha protetto la modifica concorrente.");
                File.WriteAllText(brokerFile, "original",
                    new UTF8Encoding(false));
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), new[] { target })
                    .GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "1" &&
                        execution.CommittedBrokerFiles.SequenceEqual(
                            new[] { "config" }) &&
                        execution.BrokerFileProtections.Count == 1 &&
                        execution.BrokerFileProtections[0]
                            .ReparsePointsRejected &&
                        execution.BrokerFileProtections[0]
                            .ReadWriteAccessVerified &&
                        execution.BrokerFileProtections[0].IdentityVerified ==
                            OperatingSystem.IsWindows() &&
                        File.ReadAllText(brokerFile) == "updated",
                    "Il commit broker transazionale non ha applicato la " +
                    "scrittura: " + string.Join("; ", execution.Issues.Select(
                        issue => issue.Code + " " + issue.Message)));
                Console.WriteLine("PASS file broker write fuori processo: " +
                    "staging, ACL, identità, conflitto e commit atomico");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void GovernsHostSideHttpAcquisition()
        {
            var resolver = new FixedHostResolver(IPAddress.Parse(
                "93.184.216.34"));
            var transport = new RecordingNetworkTransport(
                new DdfDotNetWorkerNetworkTransportResponse(200,
                    "application/json", Encoding.UTF8.GetBytes(
                        "{\"value\":21}")));
            var broker = new DdfDotNetWorkerNetworkBroker(resolver, transport);
            var grant = new DdfDotNetWorkerNetworkGrant("weather",
                new Uri("https://api.example.test/v1/data?token=secret"),
                new[] { "application/json" }, 1024,
                TimeSpan.FromSeconds(2));
            DdfDotNetWorkerNetworkAcquisitionResult acquired = broker
                .AcquireAsync(grant, request => request.Headers.TryAddWithoutValidation(
                    "Authorization", "Bearer top-secret"))
                .GetAwaiter().GetResult();
            Require(acquired.Success &&
                    acquired.Snapshot.Content == "{\"value\":21}" &&
                    acquired.Receipt.ResponseBytes == 12 &&
                    acquired.Receipt.MediaType == "application/json" &&
                    transport.ApprovedAddresses.SequenceEqual(new[]
                    {
                        IPAddress.Parse("93.184.216.34")
                    }) && transport.Authorization == "Bearer top-secret",
                "Il broker HTTP host non ha acquisito la risposta autorizzata: " +
                string.Join("; ", acquired.Issues));
            string transported = JsonSerializer.Serialize(acquired.Snapshot);
            Require(!transported.Contains("api.example.test",
                        StringComparison.OrdinalIgnoreCase) &&
                    !transported.Contains("secret",
                        StringComparison.OrdinalIgnoreCase) &&
                    !acquired.Receipt.HostSha256.Contains("example",
                        StringComparison.OrdinalIgnoreCase),
                "Endpoint o credenziali sono trapelati nello snapshot IPC.");

            var privateTransport = new RecordingNetworkTransport(
                transport.Response);
            var privateBroker = new DdfDotNetWorkerNetworkBroker(
                new FixedHostResolver(IPAddress.Parse("93.184.216.34"),
                    IPAddress.Loopback), privateTransport);
            DdfDotNetWorkerNetworkAcquisitionResult rebound = privateBroker
                .AcquireAsync(grant).GetAwaiter().GetResult();
            Require(!rebound.Success && rebound.Issues.Any(issue =>
                        issue.Code == "DDFC545") &&
                    privateTransport.SendCount == 0,
                "Il broker HTTP non ha bloccato una risoluzione DNS mista " +
                "pubblica/locale.");
            var insecure = new DdfDotNetWorkerNetworkGrant("weather",
                new Uri("http://api.example.test/data"));
            Require(!broker.AcquireAsync(insecure).GetAwaiter().GetResult()
                    .Success && transport.SendCount == 1,
                "Il broker HTTP ha accettato HTTP senza grant esplicito.");
            DdfDotNetWorkerNetworkAcquisitionResult retargeted = broker
                .AcquireAsync(grant, request => request.RequestUri = new Uri(
                    "https://evil.example.test/steal"))
                .GetAwaiter().GetResult();
            Require(!retargeted.Success && retargeted.Issues.Any(issue =>
                    issue.Code == "DDFC545"),
                "Il provider credenziali ha potuto cambiare la destinazione.");

            transport.Response = new DdfDotNetWorkerNetworkTransportResponse(
                302, "text/plain", Encoding.UTF8.GetBytes("redirect"));
            Require(!broker.AcquireAsync(grant).GetAwaiter().GetResult().Success,
                "Il broker HTTP ha seguito o accettato un redirect.");
            transport.Response = new DdfDotNetWorkerNetworkTransportResponse(
                200, "text/html", Encoding.UTF8.GetBytes("<html/>"));
            Require(!broker.AcquireAsync(grant).GetAwaiter().GetResult().Success,
                "Il broker HTTP ha accettato un media type non autorizzato.");
            Console.WriteLine("PASS acquisizione HTTP governata: HTTPS, IP " +
                "pinnati, anti-rebinding, redirect, media type e segreti host-only");
        }

        private static void RunsNetworkSnapshotBrokerOutOfProcess()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-network-broker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    string value << brokerReadResponse(\"weather\");\n" +
                    "    print(value);\n" +
                    "    ret length(value);\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "broker.network.read", "Risposta rete broker",
                    "Legge soltanto risposte preautorizzate dall'host."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "brokerReadResponse", "string", new[]
                    {
                        new DdfHostParameterDefinition("logicalName", "string")
                    }, arguments => string.Empty,
                    requiredPermissions: new[] { "broker.network.read" }));
                catalog.SetPermission("broker.network.read", true);
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Network broker", package, "main.ddf");
                project.Module.Id = "ddf.test.network-broker";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success,
                    "Preparazione network broker fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit network broker fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success, "Bundle network broker fallito.");
                string capability = DdfDotNetWorkerBrokerCapabilities
                    .ReadNetworkResponse;
                var policy = DdfDotNetWorkerAccessPolicy.DenyAll(
                    OperatingSystem.IsWindows());
                policy.NetworkAccess =
                    DdfDotNetWorkerNetworkAccess.BrokeredClient;
                policy.AllowedBrokerCapabilities.Add(capability);
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version,
                    hostCapabilities: new[] { capability });
                DdfDotNetWorkerNetworkSnapshot snapshot =
                    DdfDotNetWorkerNetworkSnapshot.FromContent("weather",
                        "network", "application/json");
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-network-broker", policy,
                        networkResponses: new[] { snapshot });
                string savedHash = snapshot.Sha256;
                snapshot.Sha256 = new string('0', 64);
                DdfDotNetWorkerPackageResult tampered =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied: OperatingSystem.IsWindows());
                Require(!tampered.Success && tampered.Issues.Any(issue =>
                        issue.Code == "DDFC544"),
                    "La risposta network broker manomessa è stata accettata.");
                snapshot.Sha256 = savedHash;
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "7" &&
                        execution.Response.Output.SequenceEqual(
                            new[] { "network" }),
                    "La risposta network broker non è stata consumata: " +
                    string.Join("; ", execution.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                request.BrokerNetworkResponses.Clear();
                DdfDotNetWorkerExecutionResult missing =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!missing.Success && missing.Issues.Any(issue =>
                        issue.Code == "DDF409"),
                    "Il worker ha consentito una risposta di rete non concessa.");
                Console.WriteLine("PASS network broker fuori processo: " +
                    "snapshot logico, hash e risorsa deny-by-default");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RunsLiveNetworkBrokerOverDuplexWorker()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-live-network-broker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    string value << brokerFetchResponse(\"weather\");\n" +
                    "    print(value);\n" +
                    "    ret length(value);\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                var catalog = new DdfEditorHostCatalog();
                catalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "broker.network.fetch", "Richiesta rete live broker",
                    "Richiede all'host una risorsa HTTP preautorizzata."));
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "brokerFetchResponse", "string", new[]
                    {
                        new DdfHostParameterDefinition("logicalName", "string")
                    }, arguments => string.Empty,
                    requiredPermissions: new[] { "broker.network.fetch" }));
                catalog.SetPermission("broker.network.fetch", true);
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Live network broker", package, "main.ddf");
                project.Module.Id = "ddf.test.live-network-broker";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package, null, catalog.Snapshot);
                Require(prepared.Success,
                    "Preparazione live network broker fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success,
                    "Emit live network broker fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success,
                    "Bundle live network broker fallito.");

                string capability = DdfDotNetWorkerBrokerCapabilities
                    .FetchNetworkResponse;
                var policy = DdfDotNetWorkerAccessPolicy.DenyAll(
                    OperatingSystem.IsWindows());
                policy.NetworkAccess =
                    DdfDotNetWorkerNetworkAccess.BrokeredClient;
                policy.AllowedBrokerCapabilities.Add(capability);
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version,
                    hostCapabilities: new[] { capability });
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-live-network-broker", policy,
                        liveNetworkResources: new[] { "weather" });
                var transport = new RecordingNetworkTransport(
                    new DdfDotNetWorkerNetworkTransportResponse(200,
                        "application/json",
                        Encoding.UTF8.GetBytes("live-network")));
                var broker = new DdfDotNetWorkerNetworkBroker(
                    new FixedHostResolver(IPAddress.Parse("93.184.216.34")),
                    transport);
                var handler = new DdfDotNetWorkerNetworkCapabilityHandler(
                    new[]
                    {
                        new DdfDotNetWorkerNetworkGrant("weather",
                            new Uri("https://example.test/weather"))
                    }, broker);
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), handler)
                    .GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "12" &&
                        execution.Response.Output.SequenceEqual(
                            new[] { "live-network" }) &&
                        transport.SendCount == 1 && handler.Receipts.Count == 1,
                    "La capability HTTP live duplex non è stata consumata: " +
                    string.Join("; ", execution.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));

                request.CorrelationId = "compiler-live-network-denied";
                request.LiveNetworkResources = new List<string> { "other" };
                DdfDotNetWorkerExecutionResult denied =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), handler)
                    .GetAwaiter().GetResult();
                Require(!denied.Success && transport.SendCount == 1,
                    "Il worker ha inoltrato una risorsa HTTP live non concessa.");

                request.CorrelationId = "compiler-live-network-timeout";
                request.LiveNetworkResources = new List<string> { "weather" };
                var lateHandler = new LateDuplexCapabilityHandler();
                Task<DdfDotNetWorkerExecutionResult> timedOutTask =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromMilliseconds(750),
                            maxProcessCpuTime: TimeSpan.FromSeconds(30)),
                        lateHandler);
                lateHandler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();
                DdfDotNetWorkerExecutionResult timedOut = timedOutTask
                    .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Require(timedOut.TimedOut && !timedOut.Success &&
                        timedOut.Issues.Any(issue => issue.Code == "DDFC531") &&
                        lateHandler.CancellationObserved.Task.IsCompleted &&
                        !lateHandler.Completed.Task.IsCompleted,
                    "Il timeout worker non ha cancellato il callback locale " +
                    "senza attenderlo indefinitamente.");
                lateHandler.Release.TrySetResult("late-network");
                lateHandler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();
                Require(!timedOut.Success && timedOut.Response == null,
                    "La risposta tardiva ha alterato l'esito già chiuso del worker.");

                request.CorrelationId = "compiler-live-network-stop";
                var stoppedHandler = new LateDuplexCapabilityHandler();
                using var stopSource = new CancellationTokenSource();
                Task<DdfDotNetWorkerExecutionResult> stoppedTask =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), stoppedHandler,
                        stopSource.Token);
                stoppedHandler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();
                stopSource.Cancel();
                bool stopped = false;
                try
                {
                    stoppedTask.WaitAsync(TimeSpan.FromSeconds(5))
                        .GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { stopped = true; }
                Require(stopped &&
                        stoppedHandler.CancellationObserved.Task.IsCompleted &&
                        !stoppedHandler.Completed.Task.IsCompleted,
                    "Stop non ha interrotto il worker con callback host tardivo.");
                stoppedHandler.Release.TrySetResult("after-stop");
                stoppedHandler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();
                Console.WriteLine("PASS network broker live duplex: grant, " +
                    "deny-by-default, timeout, Stop e risposta tardiva");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ValidatesDuplexWorkerFramingAndLifecycle()
        {
            var inputHandler = new DdfDotNetWorkerInteractiveInputHandler(
                cancellationToken => ValueTask.FromResult("router-input"));
            var debugHandler = new DdfDotNetWorkerInteractiveDebugHandler(
                (pause, cancellationToken) => ValueTask.FromResult(
                    DdfDotNetDebugCommand.Continue));
            var router = new DdfDotNetWorkerDuplexCapabilityRouter(new[]
            {
                new KeyValuePair<string,
                    IDdfDotNetWorkerDuplexCapabilityHandler>(
                        DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                        inputHandler),
                new KeyValuePair<string,
                    IDdfDotNetWorkerDuplexCapabilityHandler>(
                        DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                        debugHandler)
            });
            Require(router.CapabilityIds.SequenceEqual(new[]
                {
                    DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                    DdfDotNetWorkerBrokerCapabilities.NextDebugCommand
                }, StringComparer.Ordinal),
                "Il router duplex non espone registrazioni deterministiche.");
            DdfDotNetWorkerDuplexCapabilityResult routedInput = router
                .InvokeAsync(DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                    Array.Empty<string>(), CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
            Require(routedInput.Success &&
                    JsonSerializer.Deserialize<string>(routedInput.ResultJson) ==
                    "router-input" && inputHandler.RequestCount == 1,
                "Il router duplex non inoltra l'input al solo handler registrato.");
            DdfDotNetWorkerDuplexCapabilityResult denied = router.InvokeAsync(
                    "worker.unregistered", Array.Empty<string>(),
                    CancellationToken.None).AsTask().GetAwaiter().GetResult();
            Require(!denied.Success && denied.ErrorCode == "DDFC551" &&
                    debugHandler.PauseCount == 0,
                "Il router duplex non nega capability assenti dal catalogo host.");
            bool duplicateRejected = false;
            try
            {
                _ = new DdfDotNetWorkerDuplexCapabilityRouter(new[]
                {
                    new KeyValuePair<string,
                        IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                            inputHandler),
                    new KeyValuePair<string,
                        IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                            debugHandler)
                });
            }
            catch (ArgumentException)
            {
                duplicateRejected = true;
            }
            Require(duplicateRejected,
                "Il router duplex accetta registrazioni capability ambigue.");

            const string correlation = "duplex-contract";
            var start = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.Start,
                CorrelationId = correlation,
                Sequence = 0,
                StartRequest = new DdfDotNetWorkerRequest
                {
                    CorrelationId = correlation
                }
            };
            var capabilityRequest = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityRequest,
                CorrelationId = correlation,
                Sequence = 1,
                RequestId = "request-1",
                CapabilityId = DdfDotNetWorkerBrokerCapabilities
                    .ReadNetworkResponse,
                ArgumentsJson = new List<string> { "\"weather\"" }
            };
            var capabilityResponse = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityResponse,
                CorrelationId = correlation,
                Sequence = 2,
                RequestId = "request-1",
                CapabilitySucceeded = true,
                ResultJson = "\"network\""
            };
            var complete = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.Complete,
                CorrelationId = correlation,
                Sequence = 3,
                FinalResponse = new DdfDotNetWorkerResponse
                {
                    CorrelationId = correlation,
                    Success = true,
                    ResultJson = "7"
                }
            };
            DdfDotNetWorkerDuplexFrame[] frames =
            {
                start, capabilityRequest, capabilityResponse, complete
            };
            var transcript = new DdfDotNetWorkerDuplexTranscript();
            foreach (DdfDotNetWorkerDuplexFrame frame in frames)
            {
                string encoded = DdfDotNetWorkerDuplexProtocol.SerializeFrame(
                    frame);
                Require(!encoded.Contains('\n') && !encoded.Contains('\r'),
                    "Il frame duplex serializzato non è JSONL-safe.");
                DdfDotNetWorkerDuplexFrame decoded =
                    DdfDotNetWorkerDuplexProtocol.DeserializeFrame(encoded);
                Require(transcript.Accept(decoded) == null,
                    "Transcript duplex valido rifiutato al frame " +
                    frame.Kind + ".");
            }
            Require(transcript.Started && transcript.Completed &&
                    transcript.CapabilityCalls == 1,
                "Lifecycle duplex valido non contabilizzato correttamente.");

            var pending = new DdfDotNetWorkerDuplexTranscript();
            Require(pending.Accept(start) == null &&
                    pending.Accept(capabilityRequest) == null,
                "Preparazione transcript negativo fallita.");
            var premature = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.Complete,
                CorrelationId = correlation,
                Sequence = 2,
                FinalResponse = complete.FinalResponse
            };
            Require(pending.Accept(premature)?.Code == "DDFC546",
                "Il transcript è terminato con una capability pendente.");
            var wrongResponse = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityResponse,
                CorrelationId = correlation,
                Sequence = 2,
                RequestId = "request-other",
                CapabilitySucceeded = true,
                ResultJson = "null"
            };
            Require(pending.Accept(wrongResponse)?.Code == "DDFC546",
                "Il transcript ha accettato una risposta non correlata.");
            var badArguments = new DdfDotNetWorkerDuplexFrame
            {
                Kind = DdfDotNetWorkerDuplexFrameKind.CapabilityRequest,
                CorrelationId = correlation,
                Sequence = 1,
                RequestId = "bad-json",
                CapabilityId = "host.function.test",
                ArgumentsJson = new List<string> { "not-json" }
            };
            bool invalidRejected = false;
            try
            {
                DdfDotNetWorkerDuplexProtocol.SerializeFrame(badArguments);
            }
            catch (NotSupportedException) { invalidRejected = true; }
            Require(invalidRejected,
                "Il framing duplex ha accettato un argomento non JSON.");
            bool multilineRejected = false;
            try
            {
                DdfDotNetWorkerDuplexProtocol.DeserializeFrame(
                    DdfDotNetWorkerDuplexProtocol.SerializeFrame(start) + "\n");
            }
            catch (NotSupportedException) { multilineRejected = true; }
            Require(multilineRejected,
                "Il framing duplex ha accettato più righe nello stesso frame.");
            Console.WriteLine("PASS protocollo duplex: framing JSONL, " +
                "sequenza, correlazione, quote e lifecycle fail-closed");
        }

        private static void RunsBufferedInputOutOfProcess()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-worker-input-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    string first << readLine();\n" +
                    "    string second << readLine();\n" +
                    "    print(first);\n" +
                    "    print(second);\n" +
                    "    ret length(first) + length(second);\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Worker input", package, "main.ddf");
                project.Module.Id = "ddf.test.worker-input";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package);
                Require(prepared.Success, "Preparazione input worker fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success, "Emit input worker fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success, "Bundle input worker fallito.");
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version);
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-worker-input",
                        bufferedInput: new[] { "alpha", "beta" });
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "9" &&
                        execution.Response.Output.SequenceEqual(
                            new[] { "alpha", "beta" }) &&
                        execution.Response.ConsumedInputCount == 2,
                    "Il worker non ha consumato la coda Input in ordine: " +
                    string.Join("; ", execution.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                int firstDeclaration = source.IndexOf(
                    "string first << readLine();", StringComparison.Ordinal);
                request.CorrelationId = "compiler-worker-debug-batch";
                request.BufferedInput = new List<string> { "alpha", "beta" };
                request.DebugPlan = new DdfDotNetWorkerDebugPlan
                {
                    Breakpoints = new List<DdfDotNetWorkerDebugBreakpoint>
                    {
                        new()
                        {
                            SourcePath = "main.ddf",
                            Start = firstDeclaration,
                            Length = "string first << readLine();".Length
                        }
                    },
                    Commands = new List<DdfDotNetDebugCommand>
                    {
                        DdfDotNetDebugCommand.StepOver,
                        DdfDotNetDebugCommand.Continue
                    },
                    MaxPauses = 4
                };
                DdfDotNetWorkerExecutionResult debugExecution =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(debugExecution.Success &&
                        debugExecution.Response.DebugTrace.Count == 2 &&
                        debugExecution.Response.DebugTrace[0].Location
                            .DocumentPath == "main.ddf" &&
                        debugExecution.Response.DebugTrace.Any(pause =>
                            pause.Frames.Any(
                            frame => frame.Name == "main" &&
                                frame.Variables.Any(variable =>
                                    variable.Name == "first" &&
                                    variable.DisplayValue == "alpha"))),
                    "Il worker non ha restituito la traccia debug DDF " +
                    "deterministica (success=" + debugExecution.Success +
                    ", pause=" + (debugExecution.Response?.DebugTrace.Count ??
                        -1) + ", path=" + debugExecution.Response?.DebugTrace
                            .FirstOrDefault()?.Location.DocumentPath +
                    ", frame=" + string.Join(",", debugExecution.Response?
                        .DebugTrace.SelectMany(pause => pause.Frames)
                        .Select(frame => frame.Name) ?? Array.Empty<string>()) +
                    ", dati=" + string.Join(" | ",
                        debugExecution.Response?.DebugTrace.SelectMany(pause =>
                            pause.Frames).SelectMany(frame => frame.Variables)
                            .Select(variable => variable.Name + "=" +
                                variable.DisplayValue) ?? Array.Empty<string>()) +
                    "): " + string.Join("; ",
                        debugExecution.Issues.Select(issue => issue.Code +
                            " " + issue.Message)));

                request.CorrelationId = "compiler-worker-debug-interactive";
                request.BufferedInput = new List<string> { "alpha", "beta" };
                request.DebugPlan = new DdfDotNetWorkerDebugPlan
                {
                    Breakpoints = new List<DdfDotNetWorkerDebugBreakpoint>
                    {
                        new()
                        {
                            SourcePath = "main.ddf",
                            Start = firstDeclaration,
                            Length = "string first << readLine();".Length
                        }
                    },
                    Interactive = true,
                    MaxPauses = 4
                };
                var interactiveCommands = new Queue<DdfDotNetDebugCommand>(
                    new[]
                    {
                        DdfDotNetDebugCommand.StepOver,
                        DdfDotNetDebugCommand.Continue
                    });
                var hostPauses = new List<DdfDotNetWorkerDebugPause>();
                var debugHandler =
                    new DdfDotNetWorkerInteractiveDebugHandler(
                        (pause, cancellationToken) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            hostPauses.Add(pause);
                            return ValueTask.FromResult(
                                interactiveCommands.Dequeue());
                        });
                DdfDotNetWorkerExecutionResult interactiveDebug =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), debugHandler)
                    .GetAwaiter().GetResult();
                Require(interactiveDebug.Success &&
                        debugHandler.PauseCount == 2 &&
                        hostPauses.Count == 2 &&
                        interactiveDebug.Response.DebugTrace.Count == 2 &&
                        hostPauses[0].Location.DocumentPath == "main.ddf" &&
                        hostPauses[1].Frames.Any(frame => frame.Name == "main" &&
                            frame.Variables.Any(variable =>
                                variable.Name == "first" &&
                                variable.DisplayValue == "alpha")),
                    "Il debug interattivo duplex non ha scambiato pause e " +
                    "comandi DDF: " + string.Join("; ",
                        interactiveDebug.Issues.Select(issue => issue.Code +
                            " " + issue.Message)));

                request.CorrelationId =
                    "compiler-worker-debug-interactive-legacy";
                DdfDotNetWorkerExecutionResult legacyInteractiveDebug =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!legacyInteractiveDebug.Success &&
                        legacyInteractiveDebug.Issues.Any(issue =>
                            issue.Code == "DDFC550"),
                    "Il debug interattivo è stato accettato senza duplex.");

                request.CorrelationId =
                    "compiler-worker-debug-interactive-stop";
                var stopHandler =
                    new DdfDotNetWorkerInteractiveDebugHandler(
                        (pause, cancellationToken) => ValueTask.FromResult(
                            DdfDotNetDebugCommand.Stop));
                DdfDotNetWorkerExecutionResult stoppedDebug =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), stopHandler)
                    .GetAwaiter().GetResult();
                Require(!stoppedDebug.Success && stopHandler.PauseCount == 1 &&
                        stoppedDebug.Issues.Any(issue =>
                            issue.Code == "DDFC550"),
                    "Stop non ha arrestato in modo controllato il debug live.");
                Console.WriteLine("PASS debug interattivo duplex: snapshot DDF, " +
                    "Step Over, Continua, Stop e deny senza canale");

                request.CorrelationId = "compiler-worker-input-exhausted";
                request.BufferedInput = new List<string> { "only-one" };
                request.DebugPlan = null;
                DdfDotNetWorkerExecutionResult exhausted =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!exhausted.Success && exhausted.Issues.Any(issue =>
                        issue.Code == "DDF408"),
                    "L'esaurimento della coda Input non ha prodotto DDF408.");
                Require(exhausted.Response?.ErrorLocation != null &&
                        exhausted.Response.ErrorLocation.DocumentPath ==
                            "main.ddf" &&
                        exhausted.Response.ErrorStack.Count == 1 &&
                        !string.IsNullOrWhiteSpace(
                            exhausted.Response.ErrorStack[0].SymbolId),
                    "Il worker non ha trasportato posizione e stack DDF " +
                    "dell'errore tipizzato.");
                bool budgetRejected = false;
                try
                {
                    DdfDotNetWorkerProtocol.CreateRequest(bundled.Bundle,
                        references, environment, 100000,
                        bufferedInput: Enumerable.Repeat("x", 1025));
                }
                catch (NotSupportedException) { budgetRejected = true; }
                Require(budgetRejected,
                    "Il protocollo ha accettato una coda Input oltre quota.");
                request.BufferedInput = new List<string> { "alpha", "beta" };
                request.DebugPlan = new DdfDotNetWorkerDebugPlan
                {
                    Breakpoints = new List<DdfDotNetWorkerDebugBreakpoint>
                    {
                        new()
                        {
                            SourcePath = "main.ddf",
                            Start = firstDeclaration,
                            Length = 1
                        }
                    },
                    Commands = new List<DdfDotNetDebugCommand>
                    {
                        DdfDotNetDebugCommand.Stop
                    }
                };
                DdfDotNetWorkerPackageResult invalidDebug =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied: OperatingSystem.IsWindows());
                Require(!invalidDebug.Success && invalidDebug.Issues.Any(
                        issue => issue.Code == "DDFC543"),
                    "Il protocollo ha accettato un comando Stop nel piano " +
                    "debug batch.");
                Console.WriteLine("PASS worker protocollo 2.0: input " +
                    "bufferizzato, debug batch DDF, DDF408 tipizzato e quote");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RunsInteractiveInputOverDuplexWorker()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-worker-interactive-input-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "main() out int\n{\n" +
                    "    string first << readLine();\n" +
                    "    string second << readLine();\n" +
                    "    print(first);\n" +
                    "    print(second);\n" +
                    "    ret length(first) + length(second);\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Worker interactive input", package, "main.ddf");
                project.Module.Id = "ddf.test.worker-interactive-input";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package);
                Require(prepared.Success,
                    "Preparazione input interattivo fallita: " +
                    string.Join("; ", prepared.Issues));
                DdfDotNetReferenceSet references =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version, referenceSet: references));
                DdfDotNetArtifactEmissionResult emission =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emission.Success,
                    "Emit input interattivo fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emission.Artifacts.Select(item => item.Manifest));
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder.Create(linked.Program,
                        emission, references);
                Require(bundled.Success,
                    "Bundle input interattivo fallito.");
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                    DdfRuntimeAbi.Version);
                var policy = DdfDotNetWorkerAccessPolicy.DenyAll(
                    OperatingSystem.IsWindows());
                policy.ConsoleInputAccess =
                    DdfDotNetWorkerConsoleInputAccess.BrokeredInteractive;
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, references, environment,
                        100000, "compiler-worker-interactive-input", policy,
                        debugPlan: new DdfDotNetWorkerDebugPlan
                        {
                            Breakpoints = new List<
                                DdfDotNetWorkerDebugBreakpoint>
                            {
                                new()
                                {
                                    SourcePath = "main.ddf",
                                    Start = source.IndexOf(
                                        "string first",
                                        StringComparison.Ordinal),
                                    Length = 1
                                }
                            },
                            Interactive = true,
                            MaxPauses = 4
                        },
                        interactiveInput:
                            new DdfDotNetWorkerInteractiveInputOptions
                            {
                                MaxRequests = 2,
                                MaxValueBytes = 64
                            });
                var values = new Queue<string>(new[] { "live", "input" });
                var inputHandler = new DdfDotNetWorkerInteractiveInputHandler(
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return ValueTask.FromResult(values.Dequeue());
                    });
                var debugHandler = new DdfDotNetWorkerInteractiveDebugHandler(
                    (pause, cancellationToken) => ValueTask.FromResult(
                        DdfDotNetDebugCommand.Continue));
                var handler = new DdfDotNetWorkerDuplexCapabilityRouter(new[]
                {
                    new KeyValuePair<string,
                        IDdfDotNetWorkerDuplexCapabilityHandler>(
                        DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                        inputHandler),
                    new KeyValuePair<string,
                        IDdfDotNetWorkerDuplexCapabilityHandler>(
                        DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                        debugHandler)
                });
                var observer = new CooperativeWorkerObserver();
                string workerPath = RunnerPath();
                DdfDotNetWorkerExecutionResult execution =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(30)), handler, observer)
                    .GetAwaiter().GetResult();
                Require(execution.Success &&
                        execution.Response.ResultJson == "9" &&
                        execution.Response.Output.SequenceEqual(
                            new[] { "live", "input" }) &&
                        execution.Response.ConsumedInputCount == 2 &&
                        inputHandler.RequestCount == 2 &&
                        debugHandler.PauseCount == 1 &&
                        observer.Awaiting.Count == 2 &&
                        observer.Resumed.Count == 2 &&
                        observer.Awaiting.All(item =>
                            item.CapabilityId ==
                                DdfDotNetWorkerBrokerCapabilities
                                    .ReadConsoleInput &&
                            item.DebugSnapshot?.Location?.DocumentPath ==
                                "main.ddf" &&
                            item.DebugSnapshot.Frames.Count > 0),
                    "L'input interattivo duplex non è stato consumato: " +
                    string.Join("; ", execution.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                Require(observer.Awaiting.Zip(observer.Resumed,
                        (awaiting, resumed) =>
                            awaiting.RequestId == resumed.RequestId &&
                            awaiting.CorrelationId == resumed.CorrelationId)
                        .All(value => value),
                    "Le notifiche di sospensione e ripresa non sono correlate.");
                string projectedSnapshots = JsonSerializer.Serialize(
                    observer.Awaiting.Select(item => item.DebugSnapshot));
                Require(observer.Awaiting.All(item =>
                            item.DebugSnapshot.Frames.All(frame =>
                                frame.Name == "main" &&
                                frame.Location.DocumentPath == "main.ddf" &&
                                frame.Variables.All(variable =>
                                    !string.IsNullOrWhiteSpace(variable.Name) &&
                                    !variable.Name.Contains("<",
                                        StringComparison.Ordinal)))) &&
                        !projectedSnapshots.Contains("DdfGenerated_",
                            StringComparison.Ordinal) &&
                        !projectedSnapshots.Contains("Microsoft.CodeAnalysis",
                            StringComparison.Ordinal) &&
                        !projectedSnapshots.Contains("System.Runtime.CompilerServices",
                            StringComparison.Ordinal),
                    "La sospensione cooperativa espone frame o temporanei CLR/Roslyn.");

                request.CorrelationId =
                    "compiler-worker-interactive-input-timeout";
                var timeoutEntered = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var timeoutInput = new DdfDotNetWorkerInteractiveInputHandler(
                    async cancellationToken =>
                    {
                        timeoutEntered.TrySetResult(null);
                        await Task.Delay(Timeout.InfiniteTimeSpan,
                            cancellationToken).ConfigureAwait(false);
                        return string.Empty;
                    });
                var timeoutRouter = new DdfDotNetWorkerDuplexCapabilityRouter(
                    new[]
                    {
                        new KeyValuePair<string,
                            IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                            timeoutInput),
                        new KeyValuePair<string,
                            IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                            debugHandler)
                    });
                var timeoutObserver = new CooperativeWorkerObserver();
                Task<DdfDotNetWorkerExecutionResult> timeoutExecution =
                    DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                        request, new DdfDotNetWorkerResourceLimits(
                            TimeSpan.FromSeconds(5)), timeoutRouter,
                        timeoutObserver);
                timeoutEntered.Task.WaitAsync(TimeSpan.FromSeconds(4))
                    .GetAwaiter().GetResult();
                DdfDotNetWorkerExecutionResult timedOut = timeoutExecution
                    .GetAwaiter().GetResult();
                Require(timedOut.TimedOut &&
                        timeoutObserver.Awaiting.Count == 1 &&
                        timeoutObserver.Resumed.Count == 1 &&
                        timeoutObserver.Awaiting[0].RequestId ==
                            timeoutObserver.Resumed[0].RequestId,
                    "Il timeout worker non chiude in modo correlato la " +
                    "sospensione cooperativa.");

                request.CorrelationId =
                    "compiler-worker-interactive-input-stop";
                var stopEntered = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var stopInput = new DdfDotNetWorkerInteractiveInputHandler(
                    async cancellationToken =>
                    {
                        stopEntered.TrySetResult(null);
                        await Task.Delay(Timeout.InfiniteTimeSpan,
                            cancellationToken).ConfigureAwait(false);
                        return string.Empty;
                    });
                var stopRouter = new DdfDotNetWorkerDuplexCapabilityRouter(
                    new[]
                    {
                        new KeyValuePair<string,
                            IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.ReadConsoleInput,
                            stopInput),
                        new KeyValuePair<string,
                            IDdfDotNetWorkerDuplexCapabilityHandler>(
                            DdfDotNetWorkerBrokerCapabilities.NextDebugCommand,
                            debugHandler)
                    });
                var stopObserver = new CooperativeWorkerObserver();
                using (var stop = new CancellationTokenSource())
                {
                    Task<DdfDotNetWorkerExecutionResult> stoppedExecution =
                        DdfDotNetWorkerClient.ExecuteDuplexAsync(workerPath,
                            request, new DdfDotNetWorkerResourceLimits(
                                TimeSpan.FromSeconds(30)), stopRouter,
                            stopObserver, stop.Token);
                    stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5))
                        .GetAwaiter().GetResult();
                    stop.Cancel();
                    bool cancelled = false;
                    try { stoppedExecution.GetAwaiter().GetResult(); }
                    catch (OperationCanceledException) { cancelled = true; }
                    Require(cancelled &&
                            stopObserver.Awaiting.Count == 1 &&
                            stopObserver.Resumed.Count == 1 &&
                            stopObserver.Awaiting[0].RequestId ==
                                stopObserver.Resumed[0].RequestId,
                        "Stop non chiude in modo correlato la sospensione " +
                        "cooperativa del worker.");
                }

                request.CorrelationId =
                    "compiler-worker-interactive-input-legacy";
                DdfDotNetWorkerExecutionResult legacy =
                    DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                        TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Require(!legacy.Success && legacy.Issues.Any(issue =>
                        issue.Code == "DDFC549"),
                    "L'input interattivo è stato accettato senza canale duplex.");
                bool quotaRejected = false;
                try
                {
                    DdfDotNetWorkerProtocol.CreateRequest(bundled.Bundle,
                        references, environment, 100000, accessPolicy: policy,
                        interactiveInput:
                            new DdfDotNetWorkerInteractiveInputOptions
                            {
                                MaxRequests = 257
                            });
                }
                catch (NotSupportedException) { quotaRejected = true; }
                Require(quotaRejected,
                    "Il protocollo ha accettato una quota input live invalida.");
                Console.WriteLine("PASS input console duplex: richieste live, " +
                    "snapshot DDF correlati, timeout, Stop, quote e rifiuto " +
                    "senza canale interattivo");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void BindsStableSymbolsTypesConversionsLocationsAndCalls()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-bound-ir-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string source =
                    "sum(int left, int right) out int\n" +
                    "{\n" +
                    "    ret left + right;\n" +
                    "}\n\n" +
                    "main() out float\n" +
                    "{\n" +
                    "    float total << sum(1, 2);\n" +
                    "    total << total + 1;\n" +
                    "    ret total;\n" +
                    "}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Bound IR", package, "main.ddf");
                project.Module.Id = "example.bound";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                project.Module.Exports.Add("sum");
                var service = new DdfBuildService();
                DdfBuildResult first = service.Validate(root, project, package);
                DdfBuildResult second = service.Validate(root, project, package);
                Require(first.Success && second.Success,
                    "La pipeline condivisa non ha prodotto la IR bound.");
                DdfCompilationUnit firstUnit = first.Artifact.CompilationUnits.Single();
                DdfCompilationUnit secondUnit = second.Artifact.CompilationUnits.Single();
                DdfBoundCompilationUnit bound =
                    firstUnit.BoundIntermediateRepresentation;
                Equal(DdfBoundCompilationUnit.FormatVersion, bound.Version);
                Equal(bound.Fingerprint,
                    secondUnit.BoundIntermediateRepresentation.Fingerprint);
                Equal(firstUnit.Fingerprint, secondUnit.Fingerprint);
                Require(bound.Symbols.Select(item => item.Id)
                        .Distinct(StringComparer.Ordinal).Count() == bound.Symbols.Count,
                    "Le identità simboliche bound non sono univoche.");
                DdfBoundSymbol sum = bound.Symbols.Single(item =>
                    item.Kind == DdfBoundSymbolKind.Function && item.Name == "sum");
                DdfBoundIrDocument document = bound.Documents.Single();
                Equal(2, document.FunctionFlows.Count);
                DdfBoundSymbol main = bound.Symbols.Single(item =>
                    item.Kind == DdfBoundSymbolKind.Function && item.Name == "main");
                DdfBoundFunctionFlow mainFlow = document.FunctionFlows.Single(item =>
                    item.FunctionSymbolId == main.Id);
                Require(mainFlow.AlwaysReturns && !mainFlow.CanFallThrough,
                    "L'esito del controllo di flusso della funzione non è bound.");
                DdfBoundExpression call = document.Expressions.Single(item =>
                    item.CallTargetSymbolId == sum.Id);
                Equal("int", call.Type.Name);
                Equal(DdfBoundConversionKind.Implicit,
                    call.ContextualConversion.Kind);
                Equal("float", call.ContextualConversion.TargetType.Name);
                Require(document.Expressions.Any(item => item.IsAssignable &&
                        bound.Symbols.Any(symbol => symbol.Id == item.ReferencedSymbolId &&
                            symbol.Name == "total")),
                    "La destinazione assegnabile locale non è stata materializzata.");
                Require(document.Expressions.Where(item => item.Length > 0)
                        .All(item => !item.Type.IsError),
                    "Una espressione valida è rimasta senza tipo bound.");
                Console.WriteLine("PASS IR bound 2.1 con simboli, tipi, conversioni, l-value, effetti e chiamate risolte");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void CreatesSafeDeterministicGeneratedNames()
        {
            string root = Path.Combine(RepositoryRoot(), "samples",
                "module-workspace");
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Require(loaded.Success, "Il workspace per il name mangling non è valido.");
            DdfBuildResult prepared = new DdfBuildService().Validate(root,
                loaded.Manifest, package);
            Require(prepared.Success, "La preparazione per il name mangling è fallita.");
            DdfCompilationUnit unit = prepared.Artifact.CompilationUnits.Single(item =>
                item.Identity == "ddf.sample.module-app@0.1.0");
            DdfDotNetNameMap first = DdfDotNetNameMangler.Create(
                unit.BoundIntermediateRepresentation);
            DdfDotNetNameMap second = DdfDotNetNameMangler.Create(
                unit.BoundIntermediateRepresentation);
            Equal(first.NamespaceName, second.NamespaceName);
            Equal(first.ContainerTypeName, second.ContainerTypeName);
            Equal(string.Join("|", first.Symbols.Select(item =>
                    item.SymbolId + "=" + item.Identifier)),
                string.Join("|", second.Symbols.Select(item =>
                    item.SymbolId + "=" + item.Identifier)));
            Require(first.Symbols.Select(item => item.Identifier)
                    .Distinct(StringComparer.Ordinal).Count() == first.Symbols.Count,
                "Il name mangling ha prodotto collisioni.");
            Require(first.Symbols.All(item => item.Identifier.StartsWith(
                    "DdfGenerated_", StringComparison.Ordinal) &&
                    item.Identifier.All(character => char.IsLetterOrDigit(character) ||
                        character == '_')),
                "Il name mangling ha prodotto token C# non sicuri.");
            foreach (DdfBoundSymbol symbol in unit.BoundIntermediateRepresentation.Symbols)
            {
                string generated = first.GetIdentifier(symbol.Id);
                Require(!generated.Contains(symbol.Name, StringComparison.Ordinal),
                    "Un nome DDF è trapelato nel token C# generato.");
            }
            Console.WriteLine("PASS name mangling C# deterministico, univoco e indipendente dal testo DDF");
        }

        private static void PinsCompilerProfileAndExplicitReferences()
        {
            Equal(LanguageVersion.CSharp14,
                DdfDotNetCompilerProfile.CSharpLanguageVersion);
            Equal(OptimizationLevel.Release,
                DdfDotNetCompilerProfile.LibraryOptions.OptimizationLevel);
            Equal(NullableContextOptions.Enable,
                DdfDotNetCompilerProfile.LibraryOptions.NullableContextOptions);
            Equal(Platform.AnyCpu,
                DdfDotNetCompilerProfile.LibraryOptions.Platform);
            Require(DdfDotNetCompilerProfile.LibraryOptions.Deterministic &&
                    DdfDotNetCompilerProfile.LibraryOptions.CheckOverflow &&
                    !DdfDotNetCompilerProfile.LibraryOptions.AllowUnsafe &&
                    !DdfDotNetCompilerProfile.LibraryOptions.ConcurrentBuild,
                "Il profilo Roslyn non applica tutte le opzioni riproducibili e sicure.");

            string[] paths =
            {
                typeof(object).Assembly.Location,
                typeof(DdfRuntimeAbi).Assembly.Location
            };
            DdfDotNetReferenceSet first = DdfDotNetReferenceSet.Create(paths);
            DdfDotNetReferenceSet second = DdfDotNetReferenceSet.Create(
                paths.Reverse());
            Equal(first.Fingerprint, second.Fingerprint);
            Equal(2, first.References.Count);
            Require(first.References.All(item => Path.IsPathRooted(item.Path) &&
                    item.Sha256.Length == 64 && item.Size > 0),
                "Il reference set non registra identità, hash e percorso assoluto.");
            bool relativeRejected = false;
            try
            {
                DdfDotNetReferenceSet.Create(new[] { "System.Runtime.dll" });
            }
            catch (ArgumentException)
            {
                relativeRejected = true;
            }
            Require(relativeRejected,
                "Un riferimento implicito o relativo è stato accettato.");
            bool duplicateRejected = false;
            try
            {
                DdfDotNetReferenceSet.Create(new[] { paths[0], paths[0] });
            }
            catch (ArgumentException)
            {
                duplicateRejected = true;
            }
            Require(duplicateRejected, "Un reference assembly duplicato è stato accettato.");
            Console.WriteLine("PASS profilo Roslyn fissato e reference set esplicito riproducibile");
        }

        private static void GeneratesSafeSyntaxTreesAndModuleInitializers()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-safe-syntax-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                const string hostile = "#error injected; namespace Evil { }";
                string source =
                    "fixed int fixedBase << 40;\n\n" +
                    "state int stateCounter << 0;\n\n" +
                    "message() out string\n" +
                    "{\n" +
                    "    ret \"" + hostile + "\";\n" +
                    "}\n\n" +
                    "sum(int left, int right) out int\n" +
                    "{\n" +
                    "    ret left + right;\n" +
                    "}\n\n" +
                    "main() out int\n" +
                    "{\n" +
                    "    int[3] values;\n" +
                    "    for(int index << 0; index < 3; index++)\n" +
                    "    {\n" +
                    "        values[index] << index;\n" +
                    "    }\n" +
                    "    fixed int fixedOffset << 1;\n" +
                    "    int total << sum(values[1], fixedBase + fixedOffset);\n" +
                    "    print(message());\n" +
                    "    length(message());\n" +
                    "    toInt(\"2\");\n" +
                    "    toFloat(\"1.5\");\n" +
                    "    readLine();\n" +
                    "    if(total == 42)\n" +
                    "    {\n" +
                    "        ret total;\n" +
                    "    }\n" +
                    "    ret 0;\n" +
                    "}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), source,
                    new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Safe syntax", package, "main.ddf");
                project.Module.Id = "example.safe-syntax";
                project.Module.Version = "1.0.0";
                project.Module.Exports.Add("main");
                project.Module.Exports.Add("sum");
                project.Module.Exports.Add("message");
                DdfBuildResult prepared = new DdfBuildService().Validate(root,
                    project, package);
                Require(prepared.Success, "Il sorgente del test Roslyn non è valido.");
                DdfDotNetReferenceSet referenceSet =
                    DdfDotNetReferencePackResolver.Resolve(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location);
                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0", DdfRuntimeAbi.Version,
                            referenceSet: referenceSet));
                Require(planned.Success, "Il piano del test Roslyn non è valido.");
                DdfDotNetCompilationWorkItem item = planned.Plan.WorkItems.Single();
                DdfDotNetSyntaxGenerationResult generated =
                    DdfDotNetSyntaxTreeGenerator.Generate(item,
                        package.Language.Types.ClrMapping);
                Require(generated.Success && generated.SyntaxTrees.Count == 1,
                    "Il primo slice Roslyn non ha generato l'albero atteso: " +
                    string.Join(", ", generated.Issues.Select(issue => issue.Code)));
                RoslynCompilationUnitSyntax syntax = (RoslynCompilationUnitSyntax)generated
                    .SyntaxTrees.Single().GetRoot();
                List<SyntaxTrivia> directives = syntax.DescendantTrivia(
                        descendIntoTrivia: true).Where(trivia =>
                        trivia.IsDirective).ToList();
                Require(directives.Count > 0 && directives.All(trivia =>
                            trivia.GetStructure() is LineDirectiveTriviaSyntax line &&
                            (string.IsNullOrEmpty(line.File.ValueText) ||
                             line.File.ValueText == "main.ddf")) &&
                        directives.All(trivia => !trivia.ToFullString().Contains(
                            hostile, StringComparison.Ordinal)),
                    "Una direttiva generata non appartiene alla mappa DDF sicura.");
                Require(syntax.DescendantNodes().OfType<RoslynLiteralExpressionSyntax>()
                        .Any(literal => string.Equals(literal.Token.ValueText,
                            hostile, StringComparison.Ordinal)),
                    "Il literal ostile non è stato conservato come singolo token sicuro.");
                Require(!syntax.DescendantTokens().Where(token =>
                        token.IsKind(CSharpSyntaxKind.IdentifierToken)).Any(token =>
                        token.ValueText is "main" or "sum" or "message" or
                            "left" or "right" or "total"),
                    "Un identificatore DDF è trapelato nell'albero C#.");
                Require(generated.SyntaxTrees.Single().GetDiagnostics().All(diagnostic =>
                        diagnostic.Severity != DiagnosticSeverity.Error),
                    "L'albero C# sicuro contiene errori sintattici.");
                Require(syntax.DescendantNodes().OfType<RoslynForStatementSyntax>().Count() == 1 &&
                        syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString()
                                .EndsWith("ArrayElement", StringComparison.Ordinal)) &&
                        syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString()
                                .EndsWith("PostIncrement", StringComparison.Ordinal)),
                    "For, array indicizzato o postfix non sono entrati nel secondo slice.");
                Require(item.BoundUnit.Symbols.Where(symbol =>
                            symbol.Kind == DdfBoundSymbolKind.StandardFunction)
                        .All(symbol => !string.IsNullOrWhiteSpace(
                            symbol.SemanticBindingId)) &&
                        syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString()
                                .EndsWith("DdfRuntime_Context.Write",
                                    StringComparison.Ordinal)) &&
                        syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString()
                                .EndsWith("DdfRuntimeNumeric.ToInt",
                                    StringComparison.Ordinal)) &&
                        syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString()
                                .EndsWith("RegisterDebugState",
                                    StringComparison.Ordinal)),
                    "I binding standard non sono stati tradotti per identità semantica.");
                Require(item.BoundUnit.Symbols.Any(symbol =>
                            symbol.Kind == DdfBoundSymbolKind.Global &&
                            symbol.Name == "fixedBase" && !symbol.IsWritable) &&
                        item.BoundUnit.Symbols.Any(symbol =>
                            symbol.Kind == DdfBoundSymbolKind.Local &&
                            symbol.Name == "fixedOffset" && !symbol.IsWritable) &&
                        item.BoundUnit.Symbols.Any(symbol =>
                            symbol.Kind == DdfBoundSymbolKind.Global &&
                            symbol.Name == "stateCounter" && symbol.IsState &&
                            symbol.IsWritable),
                    "fixed o state non sono sopravvissuti nella IR bound.");
                DdfBoundSymbol fixedBase = item.BoundUnit.Symbols.Single(symbol =>
                    symbol.Kind == DdfBoundSymbolKind.Global &&
                    symbol.Name == "fixedBase");
                DdfBoundSymbol fixedOffset = item.BoundUnit.Symbols.Single(symbol =>
                    symbol.Kind == DdfBoundSymbolKind.Local &&
                    symbol.Name == "fixedOffset");
                foreach (DdfBoundSymbol fixedSymbol in new[]
                         { fixedBase, fixedOffset })
                {
                    InvocationExpressionSyntax registration = syntax
                        .DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Single(invocation => invocation.ArgumentList.Arguments.Count == 5 &&
                            invocation.ArgumentList.Arguments[0].Expression is
                                RoslynLiteralExpressionSyntax literal &&
                            string.Equals(literal.Token.ValueText,
                                fixedSymbol.Id, StringComparison.Ordinal));
                    Require(registration.ArgumentList.Arguments[3].Expression
                                .IsKind(CSharpSyntaxKind.TrueLiteralExpression) &&
                            registration.ArgumentList.Arguments[4].Expression
                                .IsKind(CSharpSyntaxKind.TrueLiteralExpression),
                        "La registrazione debug Roslyn di fixed non espone " +
                        "sola lettura e identità fixed.");
                }
                DdfDotNetCompilationBuildResult semantic =
                    DdfDotNetCompilationBuilder.Create(item,
                        package.Language.Types.ClrMapping);
                Require(semantic.Success && semantic.ValidatedSyntaxTreeCount == 1,
                    "La validazione semantica Roslyn in memoria è fallita: " +
                    string.Join(", ", semantic.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));

                string initializerSource =
                    "helper() out int\n{\n    ret 1;\n}\n\n" +
                    "struct Box\n{\n    int value << helper();\n}\n\n" +
                    "int shared << helper();\n\n" +
                    "main() out int\n{\n    Box box;\n" +
                    "    ret box.value + shared;\n}\n";
                File.WriteAllText(Path.Combine(root, "main.ddf"), initializerSource,
                    new UTF8Encoding(false));
                project.Module.Exports.Clear();
                project.Module.Exports.Add("main");
                DdfBuildResult initializerPrepared = new DdfBuildService().Validate(
                    root, project, package);
                Require(initializerPrepared.Success,
                    "Il sorgente con inizializzatori non supera la preparazione Core.");
                DdfDotNetCompilationWorkItem initializerItem =
                    DdfDotNetCompilationPlanner.Create(initializerPrepared,
                        new DdfDotNetCompilationOptions("net10.0", DdfRuntimeAbi.Version,
                            referenceSet: referenceSet))
                    .Plan.WorkItems.Single();
                DdfDotNetSyntaxGenerationResult initialized =
                    DdfDotNetSyntaxTreeGenerator.Generate(initializerItem,
                        package.Language.Types.ClrMapping);
                Require(initialized.Success && initialized.SyntaxTrees.Count == 1,
                    "Gli inizializzatori espliciti non sono stati generati: " +
                    string.Join(", ", initialized.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                RoslynCompilationUnitSyntax initializedSyntax =
                    (RoslynCompilationUnitSyntax)initialized.SyntaxTrees.Single().GetRoot();
                Require(initializedSyntax.DescendantNodes()
                        .OfType<MethodDeclarationSyntax>().Any(method =>
                            method.Identifier.ValueText == initializerItem.GeneratedNames
                                .ModuleInitializerName &&
                            method.Modifiers.Any(modifier => modifier.IsKind(
                                CSharpSyntaxKind.AsyncKeyword))) &&
                        initializedSyntax.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>().Any(method =>
                                method.Identifier.ValueText == "CreateAsync" &&
                                method.Modifiers.Any(modifier => modifier.IsKind(
                                    CSharpSyntaxKind.AsyncKeyword)) &&
                                method.ParameterList.Parameters.Any(parameter =>
                                    parameter.Identifier.ValueText ==
                                        "DdfRuntime_Context")),
                    "Il coordinatore di modulo o la factory contestuale async manca.");
                DdfDotNetCompilationBuildResult initializedSemantic =
                    DdfDotNetCompilationBuilder.Create(initializerItem,
                        package.Language.Types.ClrMapping);
                Require(initializedSemantic.Success,
                    "Gli inizializzatori espliciti non superano Roslyn: " +
                    string.Join(", ", initializedSemantic.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));
                Console.WriteLine("PASS SyntaxFactory sicura e inizializzazione esplicita di campi e globali");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(
                AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName,
                        "DDF - Program Language Editor.sln")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException(
                "Radice del repository DDFLanguageEditor non trovata.");
        }

        private static string RunnerPath()
        {
            string adjacent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "DDFLanguageEditor.Runner.dll");
            return File.Exists(adjacent) ? adjacent : Path.Combine(
                RepositoryRoot(), "DDFLanguageEditor.Runner", "bin", "Release",
                "DDFLanguageEditor.Runner.dll");
        }

        private static void BuildsVerifiedManagedExternalReferenceSets()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-managed-reference-set-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string mainAssembly = Path.Combine(root, "contract-main.dll");
                string dependencyAssembly = Path.Combine(root,
                    "contract-dependency.dll");
                File.Copy(typeof(DdfBuildService).Assembly.Location,
                    mainAssembly);
                File.Copy(typeof(DdfAdoNetDatabaseAdapter).Assembly.Location,
                    dependencyAssembly);

                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { ret 7; }", new UTF8Encoding(false));
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Managed reference set", package, "main.ddf");
                project.References.Add(new DdfProjectExternalReference
                {
                    Id = "example.contract",
                    Kind = DdfExternalReferenceKinds.ManagedDotNet,
                    Version = "1.0.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Configured,
                    Capabilities = new List<string> { "library.invoke" }
                });
                string manifestPath = Path.Combine(root,
                    DdfProjectManifest.FileName);
                string manifestSource = JsonSerializer.Serialize(project,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, manifestSource,
                    new UTF8Encoding(false));
                var resolver = new DdfExternalReferenceCatalogResolver(new[]
                {
                    DdfExternalReferenceCatalogEntry.ManagedAssembly(
                        "example.contract", "1.0.0", "any", "configured",
                        new[] { "library.invoke" }, mainAssembly, new[]
                        {
                            new DdfExternalReferenceCatalogDependency(
                                "example.contract.dependency", "1.0.0",
                                dependencyAssembly)
                        })
                }, _ => true);
                DdfBuildResult prepared = new DdfBuildService(
                    externalReferenceResolver: resolver,
                    externalReferenceContext:
                    DdfExternalReferenceContext.Standalone).Validate(root,
                    project, package);
                Require(prepared.Success,
                    "La reference .NET gestita non supera la risoluzione Core: " +
                    string.Join(", ", prepared.Issues.Select(issue =>
                        issue.Code + " " + issue.Message)));

                DdfDotNetResolvedReferenceSetResult verified =
                    DdfDotNetResolvedReferenceSetBuilder.Create(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location, prepared);
                Require(verified.Success &&
                        verified.ManagedReferencePaths.Count == 2 &&
                        verified.ManagedReferencePaths.Contains(mainAssembly,
                            StringComparer.OrdinalIgnoreCase) &&
                        verified.ManagedReferencePaths.Contains(
                            dependencyAssembly,
                            StringComparer.OrdinalIgnoreCase) &&
                        verified.ReferenceSet.References.Any(reference =>
                            string.Equals(reference.Path.Replace('/', '\\'),
                                mainAssembly, StringComparison.OrdinalIgnoreCase)),
                    "Il reference set Roslyn non contiene DLL e dipendenza " +
                    "transitiva verificate: " + string.Join(", ",
                        verified.Issues.Select(issue => issue.ToString())));

                DdfDotNetCompilationPlanResult planned =
                    DdfDotNetCompilationPlanner.Create(prepared,
                        new DdfDotNetCompilationOptions("net10.0",
                            DdfRuntimeAbi.Version,
                            referenceSet: verified.ReferenceSet));
                Require(planned.Success,
                    "Il piano con reference esterne non è valido.");
                DdfDotNetArtifactEmissionResult emitted =
                    DdfDotNetArtifactEmitter.Emit(planned.Plan,
                        package.Language.Types.ClrMapping);
                Require(emitted.Success,
                    "L'emit con reference esterne è fallito.");
                DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker
                    .Link(planned.Plan.EntryModuleIdentity,
                        emitted.Artifacts.Select(item => item.Manifest));
                Require(linked.Success,
                    "Il link con reference esterne è fallito.");
                DdfDotNetLinkedBundleResult bundled =
                    DdfDotNetLinkedBundleBuilder
                        .CreateWithPackagedReferences(linked.Program, emitted,
                            verified.ReferenceSet,
                            verified.ManagedReferencePaths);
                Require(bundled.Success &&
                        bundled.Bundle.PackagedReferences.Count == 2 &&
                        bundled.Bundle.ManagedReferences.Count(reference =>
                            reference.Packaged) == 2,
                    "Le reference esterne non sono state incluse nel bundle: " +
                    string.Join(", ", bundled.Issues.Select(issue =>
                        issue.ToString())));
                string productLicense = Path.Combine(RepositoryRoot(),
                    "LICENSE");
                string productNotices = Path.Combine(RepositoryRoot(),
                    "THIRD_PARTY_NOTICES.md");
                DdfDotNetExecutablePublishResult deniedPublication =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundled.Bundle, emitted,
                        new DdfDotNetExecutablePublishOptions(Path.Combine(
                                root, "publish-without-license"), "win-x64",
                            "ManagedReferenceProgram", 100000,
                            productLicense, productNotices));
                Require(!deniedPublication.Success &&
                        deniedPublication.Issues.Any(issue =>
                            issue.Code == "DDFC605"),
                    "Il publisher ha redistribuito DLL esterne senza ricevute " +
                    "di licenza esplicite.");
                var environment = new DdfDotNetRuntimeEnvironment(
                    DdfDotNetArtifactHostKind.Standalone, "net10.0",
                    DdfRuntimeAbi.Version);
                DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                    .CreateRequest(bundled.Bundle, verified.ReferenceSet,
                        environment, prepared.Artifact.MaxInstructions,
                        "packaged-reference-contract");
                Require(request.PackagedReferences.Count == 2 &&
                        !request.ReferencePaths.Contains(mainAssembly,
                            StringComparer.OrdinalIgnoreCase) &&
                        !request.ReferencePaths.Contains(dependencyAssembly,
                            StringComparer.OrdinalIgnoreCase),
                    "Il worker dipende ancora dai percorsi originali delle DLL.");
                byte[] packagedBytes = request.PackagedReferences[0]
                    .AssemblyBytes.ToArray();
                request.PackagedReferences[0].AssemblyBytes[^1] ^= 0x01;
                DdfDotNetWorkerPackageResult alteredPayload =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied: OperatingSystem.IsWindows());
                Require(!alteredPayload.Success && alteredPayload.Issues.Any(
                        issue => issue.Code == "DDFC552"),
                    "Un payload DLL alterato è stato accettato dal worker.");
                request.PackagedReferences[0].AssemblyBytes = packagedBytes;

                byte[] tamperedBytes = File.ReadAllBytes(mainAssembly);
                tamperedBytes[tamperedBytes.Length - 1] ^= 0x01;
                File.WriteAllBytes(mainAssembly, tamperedBytes);
                DdfDotNetResolvedReferenceSetResult stale =
                    DdfDotNetResolvedReferenceSetBuilder.Create(
                        FindNet10ReferencePack(),
                        typeof(DdfRuntimeAbi).Assembly.Location, prepared);
                Require(!stale.Success && stale.Issues.Any(issue =>
                        issue.Code == "DDFC415"),
                    "Una DLL modificata dopo la risoluzione è stata consegnata " +
                    "a Roslyn.");
                DdfDotNetContractIssue staleIssue = stale.Issues.Single(issue =>
                    issue.Code == "DDFC415");
                Require(string.Equals(staleIssue.SourcePath, manifestPath,
                            StringComparison.OrdinalIgnoreCase) &&
                        staleIssue.StartLine.HasValue &&
                        staleIssue.StartColumn.HasValue,
                    "DDFC415 non punta alla dichiarazione del riferimento nel " +
                    "manifest.");
                int idOffset = manifestSource.IndexOf("\"example.contract\"",
                    StringComparison.Ordinal);
                int expectedLine = manifestSource.Take(idOffset).Count(value =>
                    value == '\n') + 1;
                Require(staleIssue.StartLine.Value == expectedLine,
                    "DDFC415 punta alla riga manifest errata.");
                File.Delete(mainAssembly);
                File.Delete(dependencyAssembly);
                DdfDotNetWorkerPackageResult imported =
                    DdfDotNetWorkerProtocol.Import(request,
                        reducedUserTokenApplied: OperatingSystem.IsWindows());
                Require(imported.Success,
                    "Il worker non ha ricostruito le reference incluse dopo " +
                    "la rimozione degli originali: " + string.Join(", ",
                        imported.Issues.Select(issue => issue.ToString())));
                DdfDotNetStandaloneLoadResult loaded =
                    DdfDotNetStandaloneLoader.Load(imported.Package.Bundle,
                        imported.Package.ReferenceSet,
                        imported.Package.Environment);
                Require(loaded.Success,
                    "Il loader isolato non accetta le reference incluse: " +
                    string.Join(", ", loaded.Issues.Select(issue =>
                        issue.ToString())));
                using (loaded.Session)
                {
                    DdfRuntimeExecutionResult execution = loaded.Session
                        .Execute(new DdfRuntimeContext(100000));
                    Require(Convert.ToInt32(execution.Value) == 7,
                        "Il bundle autosufficiente ha prodotto un risultato " +
                        "inatteso.");
                }
                string publishRoot = Path.Combine(root,
                    "published managed references");
                var distributions = bundled.Bundle.PackagedReferences.Select(
                    reference =>
                        new DdfDotNetExecutableReferenceDistribution(
                            reference.AssemblyIdentity, productLicense,
                            productNotices));
                DdfDotNetExecutablePublishResult publication =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundled.Bundle, emitted,
                        new DdfDotNetExecutablePublishOptions(publishRoot,
                            "win-x64", "ManagedReferenceProgram", 100000,
                            productLicense, productNotices, distributions));
                Require(publication.Success,
                    "Il package con DLL esterne autorizzate è fallito: " +
                    string.Join(", ", publication.Issues.Select(issue =>
                        issue.ToString())));
                foreach (DdfDotNetPackagedReference reference in
                         bundled.Bundle.PackagedReferences)
                {
                    string simpleName = new AssemblyName(
                        reference.AssemblyIdentity).Name;
                    string publishedAssembly = Path.Combine(publishRoot,
                        "support", simpleName + ".dll");
                    Require(File.Exists(publishedAssembly) &&
                            new FileInfo(publishedAssembly).Length ==
                            reference.Size &&
                            Convert.ToHexString(SHA256.HashData(
                                File.ReadAllBytes(publishedAssembly)))
                                .ToLowerInvariant() == reference.Sha256 &&
                            File.Exists(Path.Combine(publishRoot,
                                "support", "THIRD-PARTY-LICENSES", simpleName,
                                "LICENSE.txt")) &&
                            File.Exists(Path.Combine(publishRoot,
                                "support", "THIRD-PARTY-LICENSES", simpleName,
                                "NOTICE.txt")),
                        "DLL, hash o avvisi redistributivi mancanti per " +
                        reference.AssemblyIdentity + ".");
                }
                string defaultPublicationFingerprint;
                using (JsonDocument manifest = JsonDocument.Parse(
                           File.ReadAllText(Path.Combine(publishRoot,
                               "support", "ddf-publish.json"), Encoding.UTF8)))
                {
                    defaultPublicationFingerprint = manifest.RootElement
                        .GetProperty("publicationFingerprint").GetString();
                    Require(manifest.RootElement.GetProperty("version")
                                .GetString() == "1.2" &&
                            manifest.RootElement.GetProperty(
                                "supportDirectory").GetString() == "support" &&
                            manifest.RootElement.GetProperty("icon")
                                .GetProperty("source").GetString() ==
                                "ddf-default" &&
                            defaultPublicationFingerprint?.Length == 64 &&
                            manifest.RootElement.GetProperty(
                                "externalReferences").GetArrayLength() == 2,
                        "Il manifest EXE non registra le reference esterne.");
                }
                Require(Directory.EnumerateFiles(publishRoot, "*",
                            SearchOption.TopDirectoryOnly).Single() ==
                        publication.ExecutablePath &&
                        Directory.EnumerateDirectories(publishRoot, "*",
                            SearchOption.TopDirectoryOnly).Select(
                                Path.GetFileName).SequenceEqual(
                                new[] { "support" },
                                StringComparer.OrdinalIgnoreCase),
                    "La radice del package non rispetta l'allowlist " +
                    "EXE più support.");
                VerifyPublishedPackageManifest(publishRoot);
                Require(new[] { "hostfxr.dll", "hostpolicy.dll", "coreclr.dll",
                            "System.Private.CoreLib.dll" }.All(name =>
                            File.Exists(Path.Combine(publishRoot, "support",
                                name))),
                    "Il package non contiene l'intero runtime .NET " +
                    "self-contained richiesto.");
                using (Process process = Process.Start(
                           IsolatedPackageStartInfo(
                               publication.ExecutablePath)) ??
                       throw new InvalidOperationException(
                           "Impossibile avviare l'EXE con reference esterne."))
                {
                    string publishedOutput = process.StandardOutput.ReadToEnd();
                    string publishedError = process.StandardError.ReadToEnd();
                    Require(process.WaitForExit(30000) &&
                            process.ExitCode == 0 &&
                            publishedOutput.Contains("Valore restituito: 7"),
                        "L'EXE con reference esterne non è eseguibile: " +
                        publishedError + publishedOutput);
                }
                string customIcon = Path.Combine(RepositoryRoot(),
                    "Resource", "AppIcon.ico");
                string customPublishRoot = Path.Combine(root,
                    "published custom icon");
                DdfDotNetExecutablePublishResult customPublication =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundled.Bundle, emitted,
                        new DdfDotNetExecutablePublishOptions(
                            customPublishRoot, "win-x64",
                            "ManagedReferenceProgram", 100000,
                            productLicense, productNotices, distributions,
                            customIcon));
                Require(customPublication.Success,
                    "Il package con icona personalizzata è fallito: " +
                    string.Join(", ", customPublication.Issues.Select(
                        issue => issue.ToString())));
                using (JsonDocument customManifest = JsonDocument.Parse(
                           File.ReadAllText(Path.Combine(customPublishRoot,
                               "support", "ddf-publish.json"),
                               Encoding.UTF8)))
                {
                    JsonElement icon = customManifest.RootElement
                        .GetProperty("icon");
                    Require(icon.GetProperty("source").GetString() ==
                                "custom" &&
                            icon.GetProperty("name").GetString() ==
                                "AppIcon.ico" &&
                            icon.GetProperty("sha256").GetString() ==
                                Convert.ToHexString(SHA256.HashData(
                                    File.ReadAllBytes(customIcon)))
                                    .ToLowerInvariant() &&
                            customManifest.RootElement.GetProperty(
                                "publicationFingerprint").GetString() !=
                                defaultPublicationFingerprint &&
                            !File.ReadAllBytes(customPublication.ExecutablePath)
                                .SequenceEqual(File.ReadAllBytes(
                                    publication.ExecutablePath)),
                        "Icona e fingerprint personalizzati non sono " +
                        "registrati correttamente.");
                }
                string invalidIcon = Path.Combine(root, "invalid.ico");
                File.WriteAllText(invalidIcon, "not an icon");
                DdfDotNetExecutablePublishResult invalidIconPublication =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundled.Bundle, emitted,
                        new DdfDotNetExecutablePublishOptions(Path.Combine(
                                root, "invalid-icon-package"), "win-x64",
                            "InvalidIconProgram", 100000, productLicense,
                            productNotices, distributions, invalidIcon));
                Require(!invalidIconPublication.Success &&
                        invalidIconPublication.Issues.Any(issue =>
                            issue.Code == "DDFC606"),
                    "Il publisher ha accettato un file ICO non valido.");
                string protectedRoot = Path.Combine(root,
                    "existing protected package");
                Directory.CreateDirectory(protectedRoot);
                string sentinel = Path.Combine(protectedRoot, "keep.txt");
                File.WriteAllText(sentinel, "unchanged", Encoding.UTF8);
                DdfDotNetExecutablePublishResult protectedPublication =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundled.Bundle, emitted,
                        new DdfDotNetExecutablePublishOptions(protectedRoot,
                            "win-x64", "ManagedReferenceProgram", 100000,
                            productLicense, productNotices, distributions));
                Require(!protectedPublication.Success &&
                        protectedPublication.Issues.Any(issue =>
                            issue.Code == "DDFC600") &&
                        File.ReadAllText(sentinel, Encoding.UTF8) ==
                            "unchanged" &&
                        !Directory.EnumerateDirectories(root,
                                ".existing protected package.ddf-publish-*",
                                SearchOption.TopDirectoryOnly).Any(),
                    "La pubblicazione ha alterato la destinazione esistente " +
                    "o lasciato staging residui.");
                Console.WriteLine("PASS reference .NET verificate, incluse, licenziate, pubblicate e caricate senza percorsi originali");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string FindNet10ReferencePack()
        {
            DirectoryInfo runtime = new DirectoryInfo(
                RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string packs = Path.Combine(runtime.Parent?.Parent?.Parent?.FullName ??
                string.Empty, "packs", "Microsoft.NETCore.App.Ref");
            string selected = Directory.Exists(packs)
                ? Directory.GetDirectories(packs)
                    .Select(path => Path.Combine(path, "ref", "net10.0"))
                    .Where(Directory.Exists)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : null;
            if (selected == null)
                throw new DirectoryNotFoundException(
                    "Reference pack Microsoft.NETCore.App.Ref net10.0 non trovato.");
            return selected;
        }

        private static DdfDotNetBuildReceipt TestBuildReceipt() => new()
        {
            CompilationPlanVersion = DdfDotNetCompilationPlan.FormatVersion,
            CompilationPlanFingerprint = new string('a', 64),
            CompilerProfileVersion = DdfDotNetCompilerProfile.FormatVersion,
            RoslynVersion = DdfDotNetCompilerProfile.RoslynVersion,
            CSharpLanguageVersion = DdfDotNetCompilerProfile
                .CSharpLanguageVersion.ToString(),
            ReferenceSetFingerprint = new string('b', 64)
        };

        private static ProcessStartInfo IsolatedPackageStartInfo(
            string executablePath)
        {
            var start = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.Environment["PATH"] = string.Empty;
            start.Environment["DOTNET_ROOT"] = Path.Combine(
                Path.GetTempPath(), "ddf-no-global-dotnet");
            start.Environment["DOTNET_ROOT_X64"] = Path.Combine(
                Path.GetTempPath(), "ddf-no-global-dotnet-x64");
            start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            start.Environment.Remove("DOTNET_HOST_PATH");
            return start;
        }

        private static void VerifyPublishedPackageManifest(string packageRoot)
        {
            string manifestPath = Path.Combine(packageRoot, "support",
                "ddf-publish.json");
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(manifestPath, Encoding.UTF8));
            var declared = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement file in manifest.RootElement.GetProperty(
                         "files").EnumerateArray())
            {
                string relative = file.GetProperty("path").GetString()
                    ?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
                string full = Path.GetFullPath(Path.Combine(packageRoot,
                    relative));
                string prefix = Path.GetFullPath(packageRoot).TrimEnd(
                    Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Require(relative.Length != 0 &&
                        !Path.IsPathRooted(relative) &&
                        full.StartsWith(prefix,
                            StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(full),
                    "Il manifest contiene un percorso file non confinato o " +
                    "assente: " + relative + ".");
                Require(new FileInfo(full).Length ==
                            file.GetProperty("size").GetInt64() &&
                        Convert.ToHexString(SHA256.HashData(
                            File.ReadAllBytes(full))).ToLowerInvariant() ==
                            file.GetProperty("sha256").GetString(),
                    "Dimensione o SHA-256 del package non coincide per " +
                    relative + ".");
                declared.Add(Path.GetRelativePath(packageRoot, full));
            }
            string relativeManifest = Path.GetRelativePath(packageRoot,
                manifestPath);
            string[] actual = Directory.EnumerateFiles(packageRoot, "*",
                    SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(packageRoot, path))
                .Where(path => !string.Equals(path, relativeManifest,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            Require(actual.Length == declared.Count &&
                    actual.All(declared.Contains),
                "Il package contiene file non dichiarati nel manifest.");
        }

        private static DdfDotNetArtifactManifest CloneManifest(
            DdfDotNetArtifactManifest manifest)
        {
            DdfDotNetContractLoadResult<DdfDotNetArtifactManifest> loaded =
                DdfDotNetArtifactContract.DeserializeManifest(
                    DdfDotNetArtifactContract.SerializeManifest(manifest));
            if (!loaded.Success)
                throw new InvalidOperationException(
                    "Clonazione del manifest di test fallita.");
            return loaded.Value;
        }

        private static DdfRuntimeExecutionResult ExecuteForVerification(
            DdfDotNetCompilationPlan plan,
            DdfDotNetArtifactEmissionResult emission,
            ICollection<string> output,
            IDdfRuntimeDebugObserver debugObserver = null,
            Func<string> input = null,
            IDdfRuntimeCapabilityProvider capabilityProvider = null,
            DdfRuntimeCapabilityEffectPolicy capabilityEffectPolicy =
                DdfRuntimeCapabilityEffectPolicy.Immediate,
            IDdfRuntimeCapabilityTransactionFactory
                capabilityTransactionFactory = null)
        {
            var loadContext = new VerificationLoadContext(emission.Artifacts);
            try
            {
                DdfDotNetCompilationWorkItem entry = plan.WorkItems.Single(item =>
                    item.Identity == plan.EntryModuleIdentity);
                DdfDotNetEmittedArtifact entryArtifact = emission.Artifacts.Single(
                    item => item.ModuleIdentity == entry.Identity);
                Assembly assembly = loadContext.LoadArtifact(entryArtifact);
                Type container = assembly.GetType(entry.GeneratedNames.NamespaceName +
                    "." + entry.GeneratedNames.ContainerTypeName, throwOnError: true);
                DdfBoundSymbol main = entry.BoundUnit.Symbols.Single(symbol =>
                    symbol.ModuleIdentity == entry.Identity &&
                    symbol.Kind == DdfBoundSymbolKind.Function &&
                    symbol.Name == "main");
                MethodInfo initializer = container.GetMethod(
                    entry.GeneratedNames.ModuleInitializerName,
                    BindingFlags.Public | BindingFlags.Static) ??
                    throw new InvalidOperationException(
                        "Coordinatore del modulo compilato non trovato.");
                MethodInfo entryPoint = container.GetMethod(
                    entry.GeneratedNames.GetIdentifier(main.Id),
                    BindingFlags.Public | BindingFlags.Static) ??
                    throw new InvalidOperationException(
                        "Entry point compilato non trovato.");
                var context = new DdfRuntimeContext(100000,
                    output: value => output.Add(value),
                    input: input,
                    capabilityProvider: capabilityProvider,
                    debugObserver: debugObserver,
                    capabilityEffectPolicy: capabilityEffectPolicy,
                    capabilityTransactionFactory:
                        capabilityTransactionFactory);
                return context.Execute(runtime =>
                {
                    Invoke(initializer, runtime);
                    return Invoke(entryPoint, runtime);
                });
            }
            finally
            {
                loadContext.Unload();
            }
        }

        private static async Task<DdfRuntimeExecutionResult>
            ExecuteForVerificationAsync(DdfDotNetCompilationPlan plan,
                DdfDotNetArtifactEmissionResult emission,
                IDdfRuntimeCapabilityProvider capabilityProvider,
                CancellationToken cancellationToken = default,
                TimeSpan? capabilityTimeout = null)
        {
            var loadContext = new VerificationLoadContext(emission.Artifacts);
            try
            {
                DdfDotNetCompilationWorkItem entry = plan.WorkItems.Single(item =>
                    item.Identity == plan.EntryModuleIdentity);
                DdfDotNetEmittedArtifact entryArtifact = emission.Artifacts.Single(
                    item => item.ModuleIdentity == entry.Identity);
                Assembly assembly = loadContext.LoadArtifact(entryArtifact);
                Type container = assembly.GetType(entry.GeneratedNames.NamespaceName +
                    "." + entry.GeneratedNames.ContainerTypeName,
                    throwOnError: true);
                DdfBoundSymbol main = entry.BoundUnit.Symbols.Single(symbol =>
                    symbol.ModuleIdentity == entry.Identity &&
                    symbol.Kind == DdfBoundSymbolKind.Function &&
                    symbol.Name == "main");
                MethodInfo initializer = container.GetMethod(
                    entry.GeneratedNames.ModuleInitializerName,
                    BindingFlags.Public | BindingFlags.Static) ??
                    throw new InvalidOperationException(
                        "Coordinatore del modulo compilato non trovato.");
                MethodInfo entryPoint = container.GetMethod(
                    entry.GeneratedNames.GetIdentifier(main.Id),
                    BindingFlags.Public | BindingFlags.Static) ??
                    throw new InvalidOperationException(
                        "Entry point compilato non trovato.");
                var context = new DdfRuntimeContext(100000,
                    cancellationToken, capabilityProvider: capabilityProvider,
                    capabilityTimeout: capabilityTimeout);
                return await context.ExecuteAsync(async runtime =>
                {
                    await InvokeAsync(initializer, runtime).ConfigureAwait(false);
                    return await InvokeAsync(entryPoint, runtime)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            finally
            {
                loadContext.Unload();
            }
        }

        private static object Invoke(MethodInfo method,
            DdfRuntimeContext context)
        {
            object invocation;
            try { invocation = method.Invoke(null, new object[] { context }); }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (invocation is not Task task) return invocation;
            task.GetAwaiter().GetResult();
            PropertyInfo result = task.GetType().GetProperty("Result",
                BindingFlags.Public | BindingFlags.Instance);
            return result?.GetValue(task);
        }

        private static async Task<object> InvokeAsync(MethodInfo method,
            DdfRuntimeContext context)
        {
            object invocation;
            try { invocation = method.Invoke(null, new object[] { context }); }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (invocation is not Task task) return invocation;
            await task.ConfigureAwait(false);
            PropertyInfo result = task.GetType().GetProperty("Result",
                BindingFlags.Public | BindingFlags.Instance);
            return result?.GetValue(task);
        }

        private static int LineAt(string source, int start)
        {
            int line = 1;
            int end = Math.Min(Math.Max(0, start), source?.Length ?? 0);
            for (int index = 0; index < end; index++)
                if (source[index] == '\n') line++;
            return line;
        }

        private sealed class DebugPauseRecorder : IDisposable
        {
            private readonly object sync = new();
            private readonly Queue<DdfEditorDebugSnapshot> snapshots = new();
            private readonly AutoResetEvent available = new(false);
            private readonly IDdfEditorDebugService debugger;

            public DebugPauseRecorder(IDdfEditorDebugService debugger)
            {
                this.debugger = debugger ??
                    throw new ArgumentNullException(nameof(debugger));
                debugger.Paused += DebuggerOnPaused;
            }

            public DdfEditorDebugSnapshot Wait()
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (true)
                {
                    lock (sync)
                        if (snapshots.Count > 0) return snapshots.Dequeue();
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !available.WaitOne(remaining))
                        throw new InvalidOperationException(
                            "Timeout in attesa della prossima pausa DDF.");
                }
            }

            public void Dispose()
            {
                debugger.Paused -= DebuggerOnPaused;
                available.Dispose();
            }

            private void DebuggerOnPaused(object sender,
                DdfEditorDebugPausedEventArgs args)
            {
                lock (sync) snapshots.Enqueue(args.Snapshot);
                available.Set();
            }
        }

        private static int FirstMethodIlOffset(byte[] assemblyBytes)
        {
            using var stream = new MemoryStream(assemblyBytes, writable: false);
            using var pe = new PEReader(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            MethodDefinition method = metadata.MethodDefinitions
                .Select(metadata.GetMethodDefinition)
                .First(item => item.RelativeVirtualAddress != 0);
            int rva = method.RelativeVirtualAddress;
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(item =>
                rva >= item.VirtualAddress &&
                rva < item.VirtualAddress + Math.Max(item.VirtualSize,
                    item.SizeOfRawData));
            int methodOffset = section.PointerToRawData +
                rva - section.VirtualAddress;
            byte first = assemblyBytes[methodOffset];
            int headerSize;
            if ((first & 0x3) == 0x2)
                headerSize = 1;
            else if ((first & 0x3) == 0x3)
            {
                ushort flagsAndSize = BitConverter.ToUInt16(assemblyBytes,
                    methodOffset);
                headerSize = (flagsAndSize >> 12) * 4;
            }
            else
                throw new BadImageFormatException(
                    "Header del primo metodo non riconosciuto.");
            return methodOffset + headerSize;
        }

        private static (int Offset, byte Replacement) FirstInvalidLocalOpcode(
            byte[] assemblyBytes)
        {
            using var stream = new MemoryStream(assemblyBytes, writable: false);
            using var pe = new PEReader(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            IReadOnlyDictionary<byte, OpCode> oneByte = typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => field.GetValue(null))
                .OfType<OpCode>().Where(opCode => opCode.Size == 1)
                .ToDictionary(opCode => (byte)opCode.Value);
            IReadOnlyDictionary<byte, OpCode> twoByte = typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => field.GetValue(null))
                .OfType<OpCode>().Where(opCode => opCode.Size == 2)
                .ToDictionary(opCode => (byte)(opCode.Value & 0xff));
            foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
            {
                MethodDefinition method = metadata.GetMethodDefinition(handle);
                if (method.RelativeVirtualAddress == 0) continue;
                MethodBodyBlock body = pe.GetMethodBody(
                    method.RelativeVirtualAddress);
                if (body.LocalSignature.IsNil) continue;
                BlobReader locals = metadata.GetBlobReader(metadata
                    .GetStandaloneSignature(body.LocalSignature).Signature);
                locals.ReadSignatureHeader();
                int localCount = locals.ReadCompressedInteger();
                if (localCount <= 0 || localCount >= 4) continue;
                byte[] il = body.GetILBytes().ToArray();
                int position = 0;
                while (position < il.Length)
                {
                    int instructionOffset = position;
                    byte first = il[position++];
                    OpCode opCode;
                    if (first == 0xfe)
                        opCode = twoByte[il[position++]];
                    else
                        opCode = oneByte[first];
                    if (opCode == OpCodes.Ldloc_0 ||
                        opCode == OpCodes.Ldloc_1 ||
                        opCode == OpCodes.Ldloc_2 ||
                        opCode == OpCodes.Stloc_0 ||
                        opCode == OpCodes.Stloc_1 ||
                        opCode == OpCodes.Stloc_2)
                    {
                        int ilStart = MethodIlOffset(assemblyBytes, pe,
                            method.RelativeVirtualAddress);
                        bool load = opCode.Name.StartsWith("ldloc",
                            StringComparison.Ordinal);
                        return (ilStart + instructionOffset,
                            load ? (byte)0x09 : (byte)0x0d);
                    }
                    SkipOperand(il, ref position, opCode.OperandType);
                }
            }
            throw new InvalidOperationException(
                "Nessun locale implicito idoneo al gate negativo trovato.");
        }

        private static int MethodIlOffset(byte[] assemblyBytes, PEReader pe,
            int rva)
        {
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(item =>
                rva >= item.VirtualAddress && rva < item.VirtualAddress +
                    Math.Max(item.VirtualSize, item.SizeOfRawData));
            int methodOffset = section.PointerToRawData + rva -
                section.VirtualAddress;
            byte first = assemblyBytes[methodOffset];
            return methodOffset + ((first & 0x3) == 0x2 ? 1 :
                (BitConverter.ToUInt16(assemblyBytes, methodOffset) >> 12) * 4);
        }

        private static void SkipOperand(byte[] il, ref int position,
            OperandType operandType)
        {
            int size = operandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                    OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineBrTarget or OperandType.InlineField or
                    OperandType.InlineI or OperandType.InlineMethod or
                    OperandType.InlineSig or OperandType.InlineString or
                    OperandType.InlineTok or OperandType.InlineType or
                    OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => -1,
                _ => throw new BadImageFormatException(
                    "Operando IL non riconosciuto nel test.")
            };
            if (size >= 0)
            {
                position += size;
                return;
            }
            int count = BitConverter.ToInt32(il, position);
            position += 4 + count * 4;
        }

        private sealed class CrossAdapterHttpHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent("{\"value\":30}",
                        Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class FixedHostResolver :
            IDdfDotNetWorkerHostResolver
        {
            private readonly IReadOnlyList<IPAddress> addresses;

            internal FixedHostResolver(params IPAddress[] addresses)
            {
                this.addresses = addresses;
            }

            public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(addresses);
            }
        }

        private sealed class RecordingNetworkTransport :
            IDdfDotNetWorkerNetworkTransport
        {
            internal RecordingNetworkTransport(
                DdfDotNetWorkerNetworkTransportResponse response)
            {
                Response = response;
            }

            internal DdfDotNetWorkerNetworkTransportResponse Response {
                get; set;
            }
            internal IReadOnlyList<IPAddress> ApprovedAddresses { get;
                private set;
            } = Array.Empty<IPAddress>();
            internal string Authorization { get; private set; } = string.Empty;
            internal int SendCount { get; private set; }

            public ValueTask<DdfDotNetWorkerNetworkTransportResponse> SendAsync(
                Uri endpoint, IReadOnlyList<IPAddress> approvedAddresses,
                int maxResponseBytes, Action<HttpRequestMessage> authorize,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SendCount++;
                ApprovedAddresses = approvedAddresses.ToArray();
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    endpoint);
                authorize?.Invoke(request);
                Authorization = request.Headers.TryGetValues("Authorization",
                        out IEnumerable<string> values)
                    ? values.Single()
                    : string.Empty;
                return ValueTask.FromResult(Response);
            }
        }

        private sealed class VerificationLoadContext : AssemblyLoadContext
        {
            private readonly IReadOnlyDictionary<string, DdfDotNetEmittedArtifact>
                artifacts;

            internal VerificationLoadContext(
                IEnumerable<DdfDotNetEmittedArtifact> artifacts)
                : base("DDF compiler verification " + Guid.NewGuid().ToString("N"),
                    isCollectible: true)
            {
                this.artifacts = artifacts.ToDictionary(item =>
                    item.Manifest.AssemblyName, StringComparer.OrdinalIgnoreCase);
            }

            internal Assembly LoadArtifact(DdfDotNetEmittedArtifact artifact)
            {
                using var assembly = new MemoryStream(artifact.AssemblyBytes,
                    writable: false);
                using var symbols = new MemoryStream(artifact.PortablePdbBytes,
                    writable: false);
                return LoadFromStream(assembly, symbols);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (string.Equals(assemblyName.Name,
                        typeof(DdfRuntimeAbi).Assembly.GetName().Name,
                        StringComparison.OrdinalIgnoreCase))
                    return typeof(DdfRuntimeAbi).Assembly;
                return artifacts.TryGetValue(assemblyName.Name ?? string.Empty,
                    out DdfDotNetEmittedArtifact artifact)
                    ? LoadArtifact(artifact)
                    : null;
            }
        }

        private sealed class DifferentialExecution
        {
            internal DdfExecutionResult Vm { get; set; }
            internal List<string> VmOutput { get; } = new();
            internal DdfRuntimeExecutionResult Compiled { get; set; }
            internal DdfRuntimeException CompiledError { get; set; }
            internal List<string> CompiledOutput { get; } = new();
        }

        private enum CleanupScenario
        {
            Success,
            RuntimeError,
            Timeout,
            Stop,
            CleanupFailure
        }

        private sealed class OrderedCleanupResource : IDisposable
        {
            private readonly string name;
            private readonly IList<string> events;
            private readonly bool throwOnDispose;
            private int disposed;

            internal OrderedCleanupResource(string name, IList<string> events,
                bool throwOnDispose = false)
            {
                this.name = name;
                this.events = events;
                this.throwOnDispose = throwOnDispose;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    throw new InvalidOperationException(
                        "Risorsa rilasciata due volte: " + name + ".");
                events.Add(name);
                if (throwOnDispose)
                    throw new InvalidOperationException(
                        "Cleanup intenzionalmente fallito: " + name + ".");
            }
        }

        private sealed class OrderedAsyncCleanupResource : IAsyncDisposable
        {
            private readonly string name;
            private readonly IList<string> events;
            private int disposed;

            internal OrderedAsyncCleanupResource(string name,
                IList<string> events)
            {
                this.name = name;
                this.events = events;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    throw new InvalidOperationException(
                        "Risorsa asincrona rilasciata due volte: " + name + ".");
                events.Add(name);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class CleanupMatrixCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly string capability;
            private readonly CleanupScenario scenario;
            private readonly IList<string> events;

            internal CleanupMatrixCapabilityProvider(string capability,
                CleanupScenario scenario, IList<string> events)
            {
                this.capability = capability;
                this.scenario = scenario;
                this.events = events;
            }

            internal ManualResetEventSlim Entered { get; } = new(false);

            public object Invoke(string capabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException(
                    "Il gate cleanup richiede il percorso asincrono.");

            public async ValueTask<object> InvokeAsync(string capabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                if (!string.Equals(capabilityId, capability,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capability cleanup inattesa: " + capabilityId + ".");
                RegisterCleanupResources(scenario, events,
                    context.RegisterResource, context.RegisterAsyncResource);
                Entered.Set();
                if (scenario is CleanupScenario.Timeout or CleanupScenario.Stop)
                    await Task.Delay(Timeout.InfiniteTimeSpan,
                        context.CancellationToken).ConfigureAwait(false);
                return scenario == CleanupScenario.RuntimeError ? 1 : 0;
            }
        }

        private sealed class DelegateCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly IReadOnlyDictionary<string,
                Func<IReadOnlyList<object>, object>> functions;

            internal DelegateCapabilityProvider(IReadOnlyDictionary<string,
                Func<IReadOnlyList<object>, object>> functions)
            {
                this.functions = functions ?? throw new ArgumentNullException(
                    nameof(functions));
            }

            internal string LastTransactionId { get; private set; }

            public object Invoke(string capabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!functions.TryGetValue(capabilityId,
                        out Func<IReadOnlyList<object>, object> function))
                    throw new InvalidOperationException(
                        "Capability non autorizzata: " + capabilityId + ".");
                return function(arguments ?? Array.Empty<object>());
            }

            public async ValueTask<object> InvokeAsync(string capabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                await Task.Yield();
                LastTransactionId = context.TransactionId;
                return Invoke(capabilityId, arguments,
                    context.CancellationToken);
            }
        }

        private sealed class SuspendingCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly string capabilityId;

            internal SuspendingCapabilityProvider(string capabilityId) =>
                this.capabilityId = capabilityId;

            internal TaskCompletionSource<bool> Entered { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<object> Release { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal int SyncInvocations { get; private set; }
            internal int AsyncInvocations { get; private set; }

            public object Invoke(string requestedCapabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken)
            {
                SyncInvocations++;
                throw new InvalidOperationException(
                    "Il fallback sincrono non deve essere invocato.");
            }

            public async ValueTask<object> InvokeAsync(
                string requestedCapabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                if (!string.Equals(requestedCapabilityId, capabilityId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capability asincrona inattesa: " +
                        requestedCapabilityId + ".");
                AsyncInvocations++;
                Entered.TrySetResult(true);
                return await Release.Task.WaitAsync(context.CancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private sealed class LateCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly string capabilityId;

            internal LateCapabilityProvider(string capabilityId) =>
                this.capabilityId = capabilityId;

            internal TaskCompletionSource<bool> Entered { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<object> Release { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<bool> Completed { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TrackedAsyncResource Resource { get; } = new();

            public object Invoke(string requestedCapabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken) => throw new
                InvalidOperationException(
                    "Il provider tardivo non ammette il fallback sincrono.");

            public async ValueTask<object> InvokeAsync(
                string requestedCapabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                if (!string.Equals(requestedCapabilityId, capabilityId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capability tardiva inattesa.");
                context.RegisterAsyncResource(Resource);
                Entered.TrySetResult(true);
                try
                {
                    // Deliberatamente non usa il token: il runtime deve poter
                    // terminare comunque e ignorare il risultato tardivo.
                    return await Release.Task.ConfigureAwait(false);
                }
                finally { Completed.TrySetResult(true); }
            }
        }

        private sealed class LateDuplexCapabilityHandler :
            IDdfDotNetWorkerDuplexCapabilityHandler
        {
            internal TaskCompletionSource<bool> Entered { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<string> Release { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<bool> CancellationObserved {
                get;
            } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<bool> Completed { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public async ValueTask<DdfDotNetWorkerDuplexCapabilityResult>
                InvokeAsync(string capabilityId,
                    IReadOnlyList<string> argumentsJson,
                    CancellationToken cancellationToken)
            {
                using CancellationTokenRegistration registration =
                    cancellationToken.Register(() =>
                        CancellationObserved.TrySetResult(true));
                Entered.TrySetResult(true);
                try
                {
                    // Simula un adapter host guasto che ignora Stop/timeout.
                    string value = await Release.Task.ConfigureAwait(false);
                    return DdfDotNetWorkerDuplexCapabilityResult.Succeeded(
                        JsonSerializer.Serialize(value));
                }
                finally { Completed.TrySetResult(true); }
            }
        }

        private sealed class CooperativeWorkerObserver :
            IDdfDotNetWorkerCooperativeExecutionObserver
        {
            internal List<DdfDotNetWorkerCooperativeSuspension> Awaiting {
                get;
            } = new();
            internal List<DdfDotNetWorkerCooperativeSuspension> Resumed {
                get;
            } = new();

            public void OnAwaitingCapability(
                DdfDotNetWorkerCooperativeSuspension suspension) =>
                Awaiting.Add(suspension);

            public void OnCapabilityResumed(
                DdfDotNetWorkerCooperativeSuspension suspension) =>
                Resumed.Add(suspension);
        }

        private sealed class TrackedAsyncResource : IAsyncDisposable
        {
            private int disposeCount;
            internal int DisposeCount => Volatile.Read(ref disposeCount);

            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref disposeCount);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class CompilerTransactionFactory :
            IDdfRuntimeCapabilityTransactionFactory
        {
            private readonly IList<string> events;

            internal CompilerTransactionFactory(IList<string> events) =>
                this.events = events;

            public ValueTask<IDdfRuntimeCapabilityTransaction> BeginAsync(
                DdfRuntimeCapabilityTransactionContext context)
            {
                events.Add("begin");
                return ValueTask.FromResult<IDdfRuntimeCapabilityTransaction>(
                    new CompilerTransaction(events));
            }
        }

        private sealed class CompilerTransaction :
            IDdfRuntimeCapabilityTransaction
        {
            private readonly IList<string> events;

            internal CompilerTransaction(IList<string> events) =>
                this.events = events;

            public string Id => "compiled-tx";

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                events.Add("commit");
                return ValueTask.CompletedTask;
            }

            public ValueTask RollbackAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                events.Add("rollback");
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                events.Add("dispose");
                return ValueTask.CompletedTask;
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("expected <" + expected +
                    "> but was <" + actual + ">");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
