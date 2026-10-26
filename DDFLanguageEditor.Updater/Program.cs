using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Updater
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                WorkerArguments parsed = WorkerArguments.Parse(args);
                DdfProductUpdateInstallationPlan plan =
                    DdfProductUpdateInstallationJournalStore.ReadPlan(
                        parsed.PlanPath);
                EnsureWorkerOutsideTarget(plan.TargetRoot);
                var worker = new DdfProductUpdateWorker();
                DdfProductUpdateWorkerResult result = parsed.Cleanup
                    ? worker.Cleanup(parsed.PlanPath, parsed.JournalPath,
                        parsed.Nonce)
                    : parsed.Recover
                        ? worker.Recover(parsed.PlanPath, parsed.JournalPath,
                            parsed.Nonce)
                        : await worker.ApplyAsync(parsed.PlanPath,
                            parsed.JournalPath, parsed.Nonce)
                            .ConfigureAwait(false);
                if (result.Kind ==
                    DdfProductUpdateWorkerResultKind.AwaitingHealthCheck)
                    result = await worker.FinalizeAsync(parsed.PlanPath,
                        parsed.JournalPath, parsed.Nonce,
                        new DdfProductUpdateProcessHealthCheck(
                            TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                if (!parsed.Cleanup &&
                    result.Kind == DdfProductUpdateWorkerResultKind.Completed &&
                    plan.RestartRequested)
                    Restart(plan);
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    state = result.Kind.ToString(),
                    quarantineRoot = result.QuarantineRoot.Length == 0
                        ? null
                        : result.QuarantineRoot
                }));
                return result.Kind == DdfProductUpdateWorkerResultKind.NoAction
                    ? 2
                    : 0;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException ||
                exception is IOException || exception is UnauthorizedAccessException ||
                exception is TimeoutException)
            {
                Console.Error.WriteLine("DDF updater: " + exception.Message);
                return 1;
            }
        }

        private static void EnsureWorkerOutsideTarget(string targetRoot)
        {
            string workerRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(AppContext.BaseDirectory));
            string target = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(targetRoot));
            if (workerRoot.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                workerRoot.StartsWith(target + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Il worker updater deve risiedere fuori dall'installazione sostituita.");
        }

        private static void Restart(DdfProductUpdateInstallationPlan plan)
        {
            string executable = Path.GetFullPath(Path.Combine(plan.TargetRoot,
                plan.ApplicationExecutable.Replace('/',
                    Path.DirectorySeparatorChar)));
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = plan.TargetRoot,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException(
                "Riavvio dell'editor aggiornato non riuscito.");
        }

        private sealed class WorkerArguments
        {
            private WorkerArguments(string planPath, string journalPath,
                string nonce, bool recover, bool cleanup)
            {
                PlanPath = planPath;
                JournalPath = journalPath;
                Nonce = nonce;
                Recover = recover;
                Cleanup = cleanup;
            }

            public string PlanPath { get; }
            public string JournalPath { get; }
            public string Nonce { get; }
            public bool Recover { get; }
            public bool Cleanup { get; }

            public static WorkerArguments Parse(string[] args)
            {
                if (args == null) throw new ArgumentNullException(nameof(args));
                string plan = null;
                string journal = null;
                string nonce = null;
                bool recover = false;
                bool cleanup = false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < args.Length; index++)
                {
                    string option = args[index];
                    if (!seen.Add(option))
                        throw new ArgumentException("Argomento worker duplicato: " +
                            option + ".");
                    if (option == "--recover")
                    {
                        recover = true;
                        continue;
                    }
                    if (option == "--cleanup")
                    {
                        cleanup = true;
                        continue;
                    }
                    if (option != "--plan" && option != "--journal" &&
                        option != "--nonce")
                        throw new ArgumentException("Argomento worker sconosciuto: " +
                            option + ".");
                    if (++index >= args.Length)
                        throw new ArgumentException("Valore argomento worker mancante.");
                    string value = args[index];
                    if (option == "--plan") plan = value;
                    else if (option == "--journal") journal = value;
                    else nonce = value;
                }
                if (string.IsNullOrWhiteSpace(plan) ||
                    string.IsNullOrWhiteSpace(journal) ||
                    string.IsNullOrWhiteSpace(nonce))
                    throw new ArgumentException(
                        "Sono richiesti --plan, --journal e --nonce.");
                if (!Path.IsPathFullyQualified(plan) ||
                    !Path.IsPathFullyQualified(journal))
                    throw new ArgumentException(
                        "Piano e journal devono usare percorsi assoluti.");
                if (recover && cleanup)
                    throw new ArgumentException(
                        "--recover e --cleanup sono mutuamente esclusivi.");
                return new WorkerArguments(Path.GetFullPath(plan),
                    Path.GetFullPath(journal), nonce, recover, cleanup);
            }
        }
    }
}
