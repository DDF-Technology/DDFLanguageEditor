using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Cli
{
    public enum DdfCliExitCode
    {
        Success = 0,
        InternalError = 1,
        ValidationError = 2,
        ExecutionError = 3,
        UsageError = 64,
        EnvironmentError = 70
    }

    public static class DdfCliApplication
    {
        public const string Version = "0.13.3-alpha";

        public static Task<int> RunAsync(string[] args, TextWriter output,
            TextWriter error)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (error == null) throw new ArgumentNullException(nameof(error));
            return Task.FromResult(Run(args ?? Array.Empty<string>(), output,
                error));
        }

        private static int Run(IReadOnlyList<string> args, TextWriter output,
            TextWriter error)
        {
            if (args.Count == 0 || Is(args[0], "--help") || Is(args[0], "-h"))
            {
                WriteHelp(output);
                return (int)DdfCliExitCode.Success;
            }
            if (Is(args[0], "--version"))
            {
                output.WriteLine("ddf " + Version);
                return (int)DdfCliExitCode.Success;
            }

            string command = args[0].ToLowerInvariant();
            if (command != "validate" && command != "build" &&
                command != "run" && command != "test" &&
                command != "package")
                return Usage(error, "Comando sconosciuto: " + args[0] + ".");

            if (!TryParse(args.Skip(1).ToArray(), command,
                    out DdfCliOptions options, out string parseError))
                return Usage(error, parseError);

            try
            {
                return Execute(command, options, output, error);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException ||
                                               exception is InvalidOperationException)
            {
                WriteResult(options.Diagnostics, command, false, null,
                    new[] { new CliDiagnostic("DDFCLI003", exception.Message) },
                    output, error);
                return (int)DdfCliExitCode.EnvironmentError;
            }
            catch (Exception exception)
            {
                WriteResult(options.Diagnostics, command, false, null,
                    new[] { new CliDiagnostic("DDFCLI999",
                        "Errore interno della CLI: " + exception.Message) },
                    output, error);
                return (int)DdfCliExitCode.InternalError;
            }
        }

        private static int Execute(string command, DdfCliOptions options,
            TextWriter output, TextWriter error)
        {
            string root = ResolveProjectRoot(options.ProjectPath);
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            if (!loaded.Success)
                return FailBuildIssues(command, options.Diagnostics,
                    loaded.Issues, output, error);

            DdfLanguagePackageLoadResult packageLoad = DdfLanguagePackageLoader
                .LoadEmbeddedDefault();
            if (!packageLoad.Success)
            {
                IEnumerable<CliDiagnostic> diagnostics = packageLoad.Diagnostics
                    .Select(item => new CliDiagnostic(item.Code, item.Message,
                        item.File));
                WriteResult(options.Diagnostics, command, false, null,
                    diagnostics, output, error);
                return (int)DdfCliExitCode.EnvironmentError;
            }

            DdfBuildResult prepared = new DdfBuildService().Validate(root,
                loaded.Manifest, packageLoad.Package);
            if (!prepared.Success)
                return FailBuildIssues(command, options.Diagnostics,
                    prepared.Issues, output, error);

            if (command == "test")
            {
                IReadOnlyList<DdfLanguagePackageTestResult> results =
                    DdfLanguagePackageTestRunner.Run(packageLoad.Package,
                        packageLoad.Package.Tests);
                WriteTestResult(options.Diagnostics, root,
                    packageLoad.Package, results, output, error);
                return results.All(item => item.Passed)
                    ? (int)DdfCliExitCode.Success
                    : (int)DdfCliExitCode.ValidationError;
            }

            if (command == "validate")
            {
                WriteResult(options.Diagnostics, command, true, root,
                    prepared.Issues.Select(CliDiagnostic.FromBuildIssue),
                    output, error);
                return (int)DdfCliExitCode.Success;
            }

            DdfDotNetResolvedReferenceSetResult resolvedReferences =
                DdfDotNetResolvedReferenceSetBuilder.Create(
                    FindNet10ReferencePack(),
                    typeof(DdfRuntimeAbi).Assembly.Location, prepared);
            if (!resolvedReferences.Success)
                return FailContractIssues(command, options.Diagnostics,
                    resolvedReferences.Issues, output, error);
            DdfDotNetReferenceSet references =
                resolvedReferences.ReferenceSet;
            DdfDotNetCompilationPlanResult planned =
                DdfDotNetCompilationPlanner.Create(prepared,
                    new DdfDotNetCompilationOptions("net10.0",
                        DdfRuntimeAbi.Version, referenceSet: references));
            if (!planned.Success)
                return FailContractIssues(command, options.Diagnostics,
                    planned.Issues, output, error);

            string outputRoot = command == "package" ||
                                options.OutputPath == null
                ? Path.Combine(root, loaded.Manifest.Output.Directory,
                    "dotnet")
                : ResolveOutput(root, options.OutputPath);
            DdfDotNetArtifactCacheResult cached =
                DdfDotNetArtifactCache.GetOrEmit(planned.Plan,
                    packageLoad.Package.Language.Types.ClrMapping, outputRoot);
            if (!cached.Success)
                return FailContractIssues(command, options.Diagnostics,
                    cached.Issues, output, error);

            if (command == "build")
            {
                WriteResult(options.Diagnostics, command, true,
                    cached.EntryPath, Array.Empty<CliDiagnostic>(), output,
                    error, cached.CacheHit ? "cache-hit" : "compiled");
                return (int)DdfCliExitCode.Success;
            }

            DdfDotNetArtifactLinkResult linked = DdfDotNetArtifactLinker.Link(
                planned.Plan.EntryModuleIdentity,
                cached.Emission.Artifacts.Select(item => item.Manifest));
            if (!linked.Success)
                return FailContractIssues(command, options.Diagnostics,
                    linked.Issues, output, error);
            if (linked.Program.RequiredHostCapabilities.Count != 0)
            {
                WriteResult(options.Diagnostics, command, false, null,
                    new[] { new CliDiagnostic("DDFCLI004",
                        "Il progetto richiede capability host non disponibili " +
                        "nella CLI: " + string.Join(", ", linked.Program
                            .RequiredHostCapabilities) + ".") }, output, error);
                return (int)DdfCliExitCode.ValidationError;
            }
            DdfDotNetLinkedBundleResult bundle =
                DdfDotNetLinkedBundleBuilder.CreateWithPackagedReferences(
                    linked.Program, cached.Emission, references,
                    resolvedReferences.ManagedReferencePaths);
            if (!bundle.Success)
                return FailContractIssues(command, options.Diagnostics,
                    bundle.Issues, output, error);

            if (command == "package")
            {
                string executableName = string.IsNullOrWhiteSpace(
                    options.ExecutableName)
                    ? DefaultExecutableName(loaded.Manifest.Module.Id)
                    : options.ExecutableName;
                string destination = options.OutputPath == null
                    ? Path.Combine(root, loaded.Manifest.Output.Directory,
                        "publish", options.RuntimeIdentifier, executableName)
                    : ResolveOutput(root, options.OutputPath);
                var publishOptions = new DdfDotNetExecutablePublishOptions(
                    destination, options.RuntimeIdentifier, executableName,
                    prepared.Artifact.MaxInstructions,
                    ResolveProductFile("LICENSE"),
                    ResolveProductFile("THIRD_PARTY_NOTICES.md"));
                DdfDotNetExecutablePublishResult published =
                    DdfDotNetExecutablePublisher.Publish(planned.Plan,
                        bundle.Bundle, cached.Emission, publishOptions);
                if (!published.Success)
                    return FailContractIssues(command, options.Diagnostics,
                        published.Issues, output, error);
                WriteResult(options.Diagnostics, command, true,
                    published.ExecutablePath, Array.Empty<CliDiagnostic>(),
                    output, error, "self-contained");
                return (int)DdfCliExitCode.Success;
            }

            string workerPath = ResolveWorkerPath(options.WorkerPath);
            var environment = new DdfDotNetRuntimeEnvironment(
                DdfDotNetArtifactHostKind.CommandLine, "net10.0",
                DdfRuntimeAbi.Version);
            DdfDotNetWorkerRequest request = DdfDotNetWorkerProtocol
                .CreateRequest(bundle.Bundle, references, environment,
                    prepared.Artifact.MaxInstructions);
            DdfDotNetWorkerExecutionResult execution =
                DdfDotNetWorkerClient.ExecuteAsync(workerPath, request,
                    TimeSpan.FromSeconds(options.TimeoutSeconds))
                .GetAwaiter().GetResult();
            if (!execution.Success)
            {
                IEnumerable<DdfDotNetContractIssue> issues = execution.Issues
                    .Count == 0
                    ? new[] { new DdfDotNetContractIssue("DDFCLI005",
                        "Esecuzione worker fallita senza diagnostica.") }
                    : execution.Issues;
                WriteResult(options.Diagnostics, command, false, null,
                    issues.Select(item => new CliDiagnostic(item.Code,
                        item.Message, item.SourcePath, item.StartLine,
                        item.StartColumn)), output, error);
                bool runtimeFailure = execution.Response != null &&
                    !string.IsNullOrWhiteSpace(execution.Response.ErrorCode) &&
                    execution.Response.ErrorCode.StartsWith("DDF",
                        StringComparison.Ordinal) &&
                    !execution.Response.ErrorCode.StartsWith("DDFC",
                        StringComparison.Ordinal);
                return runtimeFailure
                    ? (int)DdfCliExitCode.ExecutionError
                    : (int)DdfCliExitCode.EnvironmentError;
            }
            WriteRunResult(options.Diagnostics, cached.EntryPath,
                execution.Response, output);
            return (int)DdfCliExitCode.Success;
        }

        private static int FailBuildIssues(string command, string format,
            IEnumerable<DdfBuildIssue> issues, TextWriter output,
            TextWriter error)
        {
            WriteResult(format, command, false, null,
                issues.Select(CliDiagnostic.FromBuildIssue), output, error);
            return (int)DdfCliExitCode.ValidationError;
        }

        private static int FailContractIssues(string command, string format,
            IEnumerable<DdfDotNetContractIssue> issues, TextWriter output,
            TextWriter error)
        {
            WriteResult(format, command, false, null,
                issues.Select(item => new CliDiagnostic(item.Code,
                    item.Message, item.SourcePath, item.StartLine,
                    item.StartColumn)), output, error);
            return (int)DdfCliExitCode.ValidationError;
        }

        private static void WriteResult(string format, string command,
            bool success, string resultPath,
            IEnumerable<CliDiagnostic> diagnostics, TextWriter output,
            TextWriter error, string state = null)
        {
            CliDiagnostic[] values = diagnostics?.ToArray() ??
                Array.Empty<CliDiagnostic>();
            if (format == "json")
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0",
                    command,
                    success,
                    state,
                    resultPath,
                    diagnostics = values
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }

            TextWriter target = success ? output : error;
            foreach (CliDiagnostic diagnostic in values)
                target.WriteLine(diagnostic.ToText());
            if (success)
                target.WriteLine(command switch
                {
                    "validate" => "Progetto DDF valido: " + resultPath,
                    "package" => "EXE DDF pubblicato (" + state + "): " +
                        resultPath,
                    _ => "Build DDF completata (" + state + "): " + resultPath
                });
        }

        private static void WriteRunResult(string format, string artifactPath,
            DdfDotNetWorkerResponse response, TextWriter output)
        {
            if (format == "json")
            {
                JsonElement result = JsonSerializer.Deserialize<JsonElement>(
                    string.IsNullOrWhiteSpace(response.ResultJson)
                        ? "null" : response.ResultJson);
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0",
                    command = "run",
                    success = true,
                    state = "completed",
                    resultPath = artifactPath,
                    execution = new
                    {
                        result,
                        response.Ended,
                        response.Instructions,
                        response.Output,
                        response.ReducedUserTokenApplied
                    },
                    diagnostics = Array.Empty<CliDiagnostic>()
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }
            foreach (string line in response.Output) output.WriteLine(line);
            output.WriteLine("Valore restituito: " +
                (response.ResultJson ?? "null"));
            output.WriteLine("Esecuzione completata: (" +
                response.Instructions + " passi runtime)");
        }

        private static void WriteTestResult(string format, string root,
            DdfLanguagePackage package,
            IReadOnlyList<DdfLanguagePackageTestResult> results,
            TextWriter output, TextWriter error)
        {
            int passed = results.Count(item => item.Passed);
            int failed = results.Count - passed;
            if (format == "json")
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0",
                    command = "test",
                    success = failed == 0,
                    state = failed == 0 ? "passed" : "failed",
                    resultPath = root,
                    languagePackage = new
                    {
                        package.Id,
                        package.PackageVersion,
                        package.LanguageVersion
                    },
                    summary = new { total = results.Count, passed, failed },
                    tests = results.Select(item => new
                    {
                        item.Name,
                        item.Passed,
                        item.Message
                    }),
                    diagnostics = results.Where(item => !item.Passed)
                        .Select(item => new CliDiagnostic("DDFT001",
                            "Test dichiarativo '" + item.Name +
                            "' non riuscito: " + item.Message))
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }
            TextWriter target = failed == 0 ? output : error;
            foreach (DdfLanguagePackageTestResult result in results)
                target.WriteLine((result.Passed ? "PASS " : "FAIL ") +
                    result.Name + (result.Passed ? string.Empty : ": " +
                        result.Message));
            target.WriteLine("Test pacchetto " + package.Id + " " +
                package.PackageVersion + ": " + passed + "/" +
                results.Count + " superati.");
        }

        private static bool TryParse(IReadOnlyList<string> args,
            string command, out DdfCliOptions options, out string error)
        {
            string project = null;
            string output = null;
            string worker = null;
            string runtimeIdentifier = null;
            string executableName = null;
            int timeoutSeconds = 30;
            string diagnostics = "text";
            for (int index = 0; index < args.Count; index++)
            {
                string value = args[index];
                if (value == "--diagnostics")
                {
                    if (++index >= args.Count ||
                        (args[index] != "text" && args[index] != "json"))
                    {
                        options = null;
                        error = "--diagnostics richiede 'text' oppure 'json'.";
                        return false;
                    }
                    diagnostics = args[index];
                }
                else if (value == "--output")
                {
                    if (command == "validate" || command == "test" ||
                        ++index >= args.Count ||
                        string.IsNullOrWhiteSpace(args[index]))
                    {
                        options = null;
                        error = "--output è valido soltanto per build e richiede un percorso.";
                        return false;
                    }
                    output = args[index];
                }
                else if (value == "--worker")
                {
                    if (command != "run" || ++index >= args.Count ||
                        string.IsNullOrWhiteSpace(args[index]))
                    {
                        options = null;
                        error = "--worker è valido soltanto per run e richiede un percorso.";
                        return false;
                    }
                    worker = args[index];
                }
                else if (value == "--runtime")
                {
                    if (command != "package" || ++index >= args.Count ||
                        string.IsNullOrWhiteSpace(args[index]))
                    {
                        options = null;
                        error = "--runtime è obbligatorio per package e richiede un RID.";
                        return false;
                    }
                    runtimeIdentifier = args[index];
                }
                else if (value == "--name")
                {
                    if (command != "package" || ++index >= args.Count ||
                        string.IsNullOrWhiteSpace(args[index]))
                    {
                        options = null;
                        error = "--name è valido soltanto per package e richiede un nome.";
                        return false;
                    }
                    executableName = args[index];
                }
                else if (value == "--timeout")
                {
                    if (command != "run" || ++index >= args.Count ||
                        !int.TryParse(args[index], out timeoutSeconds) ||
                        timeoutSeconds < 1 || timeoutSeconds > 3600)
                    {
                        options = null;
                        error = "--timeout richiede da 1 a 3600 secondi ed è valido soltanto per run.";
                        return false;
                    }
                }
                else if (value.StartsWith("-", StringComparison.Ordinal))
                {
                    options = null;
                    error = "Opzione sconosciuta: " + value + ".";
                    return false;
                }
                else if (project == null)
                    project = value;
                else
                {
                    options = null;
                    error = "È consentito un solo percorso progetto.";
                    return false;
                }
            }
            if (command == "package" &&
                string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                options = null;
                error = "package richiede --runtime; il primo profilo supportato è win-x64.";
                return false;
            }
            options = new DdfCliOptions(project ?? Directory.GetCurrentDirectory(),
                output, diagnostics, worker, timeoutSeconds,
                runtimeIdentifier, executableName);
            error = null;
            return true;
        }

        private static string ResolveProjectRoot(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) && string.Equals(Path.GetFileName(fullPath),
                    DdfProjectManifest.FileName,
                    StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException(
                    "Cartella progetto non trovata: " + fullPath + ".");
            return fullPath;
        }

        private static string ResolveOutput(string root, string path) =>
            Path.IsPathRooted(path) ? Path.GetFullPath(path) :
                Path.GetFullPath(Path.Combine(root, path));

        private static string ResolveWorkerPath(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string selected = Path.GetFullPath(explicitPath);
                if (File.Exists(selected)) return selected;
                throw new FileNotFoundException(
                    "Worker DDF non trovato.", selected);
            }
            string configured = Environment.GetEnvironmentVariable(
                "DDF_RUNNER_PATH");
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(Path.GetFullPath(configured)))
                return Path.GetFullPath(configured);
            string[] workerNames = { "DDFLanguageEditor.Runner.dll" };
            foreach (string name in workerNames)
            {
                string besideCli = Path.Combine(AppContext.BaseDirectory, name);
                if (File.Exists(besideCli)) return besideCli;
            }
            DirectoryInfo directory = new DirectoryInfo(
                AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 6;
                 depth++, directory = directory.Parent)
            {
                foreach (string configuration in new[] { "Release", "Debug" })
                foreach (string name in workerNames)
                {
                    string candidate = Path.Combine(directory.FullName,
                        "DDFLanguageEditor.Runner", "bin", configuration,
                        name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            throw new FileNotFoundException(
                "Worker DDF non trovato. Usare --worker o DDF_RUNNER_PATH.");
        }

        private static string ResolveProductFile(string name)
        {
            DirectoryInfo directory = new DirectoryInfo(
                AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 7;
                 depth++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, name);
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException(
                "File di prodotto richiesto non trovato: " + name + ".");
        }

        private static string DefaultExecutableName(string moduleId)
        {
            string value = new string((moduleId ?? string.Empty).Select(character =>
                    char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                        ? character : '-').ToArray()).Trim('-', '.', '_');
            if (value.Length == 0 || !char.IsLetter(value[0]))
                value = "DdfProgram-" + value;
            return value.Length <= 64 ? value : value.Substring(0, 64);
        }

        private static string FindNet10ReferencePack()
        {
            DirectoryInfo runtime = new DirectoryInfo(
                RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            string packs = Path.Combine(runtime.Parent?.Parent?.Parent?.FullName ??
                string.Empty, "packs", "Microsoft.NETCore.App.Ref");
            string selected = Directory.Exists(packs)
                ? Directory.GetDirectories(packs)
                    .Select(path => Path.Combine(path, "ref", "net10.0"))
                    .Where(Directory.Exists)
                    .OrderByDescending(path => path,
                        StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                : null;
            return selected ?? throw new DirectoryNotFoundException(
                "Reference pack Microsoft.NETCore.App.Ref net10.0 non trovato.");
        }

        private static bool Is(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static int Usage(TextWriter error, string message)
        {
            error.WriteLine("DDFCLI001: " + message);
            error.WriteLine("Usare 'ddf --help' per la sintassi.");
            return (int)DdfCliExitCode.UsageError;
        }

        private static void WriteHelp(TextWriter output)
        {
            output.WriteLine("DDF CLI " + Version);
            output.WriteLine("Uso:");
            output.WriteLine("  ddf validate [progetto] [--diagnostics text|json]");
            output.WriteLine("  ddf test [progetto] [--diagnostics text|json]");
            output.WriteLine("  ddf build [progetto] [--output directory] [--diagnostics text|json]");
            output.WriteLine("  ddf run [progetto] [--output directory] [--worker path] [--timeout secondi] [--diagnostics text|json]");
            output.WriteLine("  ddf package [progetto] --runtime win-x64 [--name nome] [--output directory] [--diagnostics text|json]");
            output.WriteLine("  ddf --version");
            output.WriteLine();
            output.WriteLine("Il progetto può essere una cartella o un ddf-project.json.");
        }

        private sealed record DdfCliOptions(string ProjectPath,
            string OutputPath, string Diagnostics, string WorkerPath,
            int TimeoutSeconds, string RuntimeIdentifier,
            string ExecutableName);

        private sealed record CliDiagnostic(string Code, string Message,
            string Path = null, int? Line = null, int? Column = null)
        {
            internal static CliDiagnostic FromBuildIssue(DdfBuildIssue issue) =>
                new(issue.Code, issue.Message,
                    string.IsNullOrEmpty(issue.DocumentPath) ? null :
                        issue.DocumentPath, issue.Line, issue.Column);

            internal string ToText()
            {
                string location = string.IsNullOrEmpty(Path) ? string.Empty :
                    Path + "(" + (Line ?? 1) + "," + (Column ?? 1) + "): ";
                return location + Code + ": " + Message;
            }
        }
    }
}
