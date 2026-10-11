using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DDFLanguageEditor.Cli;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Cli.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-cli-test-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit(
                    "CLI contract", package, "main.ddf");
                manifest.Module.Id = "test.cli";
                manifest.Module.Version = "1.0.0";
                manifest.Module.Exports.Add("main");
                File.WriteAllText(Path.Combine(root,
                    DdfProjectManifest.FileName), JsonSerializer.Serialize(
                    manifest, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { print(\"cli\"); ret 7; }",
                    new UTF8Encoding(false));

                Result validated = Invoke("validate", root,
                    "--diagnostics", "json");
                Require(validated.ExitCode == 0 &&
                    validated.Output.Contains("\"success\": true"),
                    "validate JSON non riuscito: " + validated.Error);

                Result tested = Invoke("test", root, "--diagnostics", "json");
                Require(tested.ExitCode == 0 &&
                    tested.Output.Contains("\"state\": \"passed\"") &&
                    tested.Output.Contains("\"total\": 7") &&
                    tested.Output.Contains("\"passed\": 7") &&
                    tested.Output.Contains("\"failed\": 0") &&
                    tested.Output.Contains(
                        "\"name\": \"corpus:control-flow\""),
                    "test non espone il corpus dichiarativo: " +
                    tested.Error + tested.Output);

                string output = Path.Combine(root, "compiled");
                Result first = Invoke("build", root, "--output", output,
                    "--diagnostics", "json");
                Require(first.ExitCode == 0 &&
                    first.Output.Contains("\"state\": \"compiled\"") &&
                    Directory.Exists(output),
                    "prima build CLI non riuscita: " + first.Error);
                Result second = Invoke("build", root, "--output", output,
                    "--diagnostics", "json");
                Require(second.ExitCode == 0 &&
                    second.Output.Contains("\"state\": \"cache-hit\""),
                    "la seconda build CLI non usa la cache verificata.");

                Result run = Invoke("run", root, "--output", output,
                    "--diagnostics", "json");
                Require(run.ExitCode == 0 &&
                    run.Output.Contains("\"state\": \"completed\"") &&
                    run.Output.Contains("\"result\": 7") &&
                    run.Output.Contains("\"cli\""),
                    "run non esegue il bundle nel worker: " + run.Error +
                    run.Output);

                string publishRoot = Path.Combine(root, "published");
                Result packageResult = Invoke("package", root, "--runtime",
                    "win-x64", "--name", "CliDdfProgram", "--output",
                    publishRoot, "--diagnostics", "json");
                string executable = Path.Combine(publishRoot,
                    "CliDdfProgram.exe");
                Require(packageResult.ExitCode == 0 &&
                    packageResult.Output.Contains(
                        "\"state\": \"self-contained\"") &&
                    File.Exists(executable) &&
                    File.Exists(Path.Combine(publishRoot,
                        "support", "ddf-publish.json")) &&
                    File.Exists(Path.Combine(publishRoot, "support",
                        "LICENSE.txt")) &&
                    File.Exists(Path.Combine(publishRoot,
                        "support", "THIRD-PARTY-NOTICES.txt")) &&
                    File.Exists(Path.Combine(publishRoot,
                        "support", "DOTNET-LICENSE.txt")) &&
                    File.Exists(Path.Combine(publishRoot,
                        "support", "DOTNET-THIRD-PARTY-NOTICES.txt")) &&
                    Directory.EnumerateFiles(publishRoot, "*",
                        SearchOption.TopDirectoryOnly).Count() == 1,
                    "package self-contained non riuscito: " +
                    packageResult.Error + packageResult.Output);
                Require(!Directory.EnumerateFiles(publishRoot, "*",
                        SearchOption.AllDirectories)
                    .Select(Path.GetFileName)
                    .Any(name => name.Contains("CodeAnalysis",
                                     StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals(
                                     "DDFLanguageEditor.Compiler.dll",
                                     StringComparison.OrdinalIgnoreCase)),
                    "il package distribuisce dipendenze di compilazione.");
                var publishedStart = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetTempPath(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                publishedStart.Environment["PATH"] = string.Empty;
                publishedStart.Environment["DOTNET_ROOT"] = Path.Combine(
                    Path.GetTempPath(), "ddf-cli-no-global-dotnet");
                publishedStart.Environment["DOTNET_ROOT_X64"] = Path.Combine(
                    Path.GetTempPath(), "ddf-cli-no-global-dotnet-x64");
                publishedStart.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
                publishedStart.Environment.Remove("DOTNET_HOST_PATH");
                using (var publishedProcess = Process.Start(publishedStart) ??
                       throw new InvalidOperationException(
                               "impossibile avviare l'EXE pubblicato"))
                {
                    string publishedOutput = publishedProcess.StandardOutput
                        .ReadToEnd();
                    string publishedError = publishedProcess.StandardError
                        .ReadToEnd();
                    Require(publishedProcess.WaitForExit(30000) &&
                            publishedProcess.ExitCode == 0 &&
                            publishedOutput.Contains("cli") &&
                            publishedOutput.Contains("Valore restituito: 7"),
                        "l'EXE pubblicato non esegue il programma DDF: " +
                        publishedError + publishedOutput);
                }

                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { int zero << 0; ret 10 / zero; }",
                    new UTF8Encoding(false));
                Result runtimeFailure = Invoke("run", root, "--output",
                    output, "--diagnostics", "json");
                Require(runtimeFailure.ExitCode ==
                        (int)DdfCliExitCode.ExecutionError &&
                    runtimeFailure.Output.Contains("\"code\": \"DDF404\""),
                    "run non conserva diagnostica ed exit code runtime: " +
                    runtimeFailure.Error + runtimeFailure.Output);

                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { ret ; }", new UTF8Encoding(false));
                Result invalid = Invoke("validate", root,
                    "--diagnostics", "json");
                Require(invalid.ExitCode ==
                        (int)DdfCliExitCode.ValidationError &&
                    invalid.Output.Contains("\"success\": false") &&
                    invalid.Output.Contains("\"diagnostics\""),
                    "validate non segnala il sorgente invalido.");

                Result usage = Invoke("unknown");
                Require(usage.ExitCode == (int)DdfCliExitCode.UsageError &&
                    usage.Error.Contains("DDFCLI001"),
                    "il contratto degli errori d'uso non è stabile.");
                Console.WriteLine("PASS CLI validate/test/build/run/package: corpus, JSON, exit code, cache, worker, EXE self-contained e diagnostiche");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static Result Invoke(params string[] args)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            int exitCode = DdfCliApplication.RunAsync(args, output, error)
                .GetAwaiter().GetResult();
            return new Result(exitCode, output.ToString(), error.ToString());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed record Result(int ExitCode, string Output, string Error);
    }
}
