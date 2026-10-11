using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Storage;

namespace DDFLanguageEditor.Storage.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("Filesystem exposes only declared logical operations",
                ExposesOnlyLogicalOperations);
            Run("Filesystem validates UTF-8 JSON CSV and quotas",
                ValidatesFormatsAndQuotas);
            Run("Filesystem commits and rolls back staged writes",
                CommitsAndRollsBack);
            Run("Filesystem denies traversal bindings and read-only writes",
                DeniesUnauthorizedAccess);
            Console.WriteLine(failures == 0
                ? "All 4 governed filesystem adapter tests passed."
                : failures + " governed filesystem adapter test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void ExposesOnlyLogicalOperations()
        {
            using TestEnvironment environment = new();
            File.WriteAllText(environment.Path("notes.txt"), "hello",
                new UTF8Encoding(false));
            using DdfGovernedFileSystemAdapter adapter =
                environment.CreateAdapter();
            DdfExternalApiDefinition api = adapter.CreateExternalApiDefinition(
                "ddf.storage.demo", "1.0.0", Permissions(),
                new[] { "storage.access" });
            Equal(6, api.Functions.Count);
            Equal(true, api.Functions.All(item =>
                item.RequiredPermissions.Contains("storage.access")));
            string contract = string.Join("|", api.Functions.Select(item =>
                item.Name + item.BindingId + item.Documentation)) +
                api.Fingerprint;
            Equal(false, contract.Contains(environment.Root,
                StringComparison.OrdinalIgnoreCase));
            Equal(false, contract.Contains("notes.txt",
                StringComparison.OrdinalIgnoreCase));
            DdfHostCatalogSnapshot analysis =
                DdfExternalApiCatalogProjector.CreateAnalysisCatalog(api,
                    Permissions().Select(item => item.Id));
            Equal(true, analysis.Functions.Any(item =>
                item.Name == "readNotes" && item.ReturnType == "string"));
            using DdfEditorExecutionService execution =
                CreateExecution(adapter);
            Equal("hello", ReturnString(Invoke(execution, "loadNotes")));
        }

        private static void ValidatesFormatsAndQuotas()
        {
            using TestEnvironment environment = new();
            using DdfGovernedFileSystemAdapter adapter =
                environment.CreateAdapter();
            using DdfEditorExecutionService execution = CreateExecution(adapter);

            Equal(true, ReturnBool(Invoke(execution, "storeJson",
                new Dictionary<string, object>
                {
                    ["content"] = "{ \"name\": \"Ada\", \"active\": true }"
                })));
            Equal("{\"name\":\"Ada\",\"active\":true}",
                ReturnString(Invoke(execution, "loadJson")));
            Equal(true, ReturnBool(Invoke(execution, "storeCsv",
                new Dictionary<string, object>
                {
                    ["content"] = "name,note\r\nAda,\"one,two\"\r\n"
                })));
            Equal("name,note\r\nAda,\"one,two\"\r\n",
                ReturnString(Invoke(execution, "loadCsv")));

            Equal(false, Invoke(execution, "storeJson",
                new Dictionary<string, object> { ["content"] = "{bad}" })
                .Succeeded);
            Equal(false, Invoke(execution, "storeCsv",
                new Dictionary<string, object> { ["content"] = "a,b\n1\n" })
                .Succeeded);
            Equal(false, Invoke(execution, "storeNotes",
                new Dictionary<string, object>
                {
                    ["content"] = new string('x', 65)
                }).Succeeded);
        }

        private static void CommitsAndRollsBack()
        {
            using TestEnvironment environment = new();
            File.WriteAllText(environment.Path("notes.txt"), "initial",
                new UTF8Encoding(false));
            using DdfGovernedFileSystemAdapter adapter =
                environment.CreateAdapter();
            using DdfEditorExecutionService execution = CreateExecution(adapter);

            DdfEditorExecutionRequest transactional = new(
                hostEffectPolicy: DdfHostEffectPolicy.RollbackOnFailure,
                hostEffectTransactionFactory: adapter.CreateTransactionFactory());
            DdfEditorExecutionCompletion failed = Invoke(execution,
                "storeThenFail", new Dictionary<string, object>
                {
                    ["content"] = "must rollback"
                }, transactional);
            Equal(false, failed.Succeeded);
            Equal(DdfHostEffectDisposition.RolledBack,
                failed.HostEffects.Disposition);
            Equal("initial", File.ReadAllText(environment.Path("notes.txt")));

            DdfEditorExecutionCompletion committed = Invoke(execution,
                "storeNotes", new Dictionary<string, object>
                {
                    ["content"] = "committed"
                }, transactional);
            Equal(true, committed.Succeeded);
            Equal(DdfHostEffectDisposition.Committed,
                committed.HostEffects.Disposition);
            Equal("committed", File.ReadAllText(environment.Path("notes.txt")));
            Equal(false, Directory.Exists(Path.Combine(environment.Root,
                ".ddf-file-staging")) && Directory.EnumerateFileSystemEntries(
                    Path.Combine(environment.Root, ".ddf-file-staging")).Any());
        }

        private static void DeniesUnauthorizedAccess()
        {
            using TestEnvironment environment = new();
            Throws<ArgumentException>(() => new DdfLogicalFileDefinition(
                "escape", @"..\secret.txt"));
            Throws<ArgumentException>(() => new DdfLogicalFileDefinition(
                "absolute", environment.Path("secret.txt")));
            Throws<ArgumentException>(() => new DdfGovernedFileSystemAdapter(
                "invalid", environment.Root,
                new[]
                {
                    new DdfLogicalFileDefinition("readonly", "read.txt")
                }, new[]
                {
                    new DdfLogicalFileOperationDefinition("writeReadonly",
                        "readonly.write", "readonly",
                        DdfLogicalFileOperationKind.Write)
                }));
            using DdfGovernedFileSystemAdapter adapter =
                environment.CreateAdapter();
            Throws<InvalidOperationException>(() => adapter.InvokeAsync(null,
                "unknown.binding", Array.Empty<object>()).AsTask().GetAwaiter()
                .GetResult());
            File.WriteAllBytes(environment.Path("notes.txt"),
                new byte[] { 0xff, 0xfe });
            using DdfEditorExecutionService execution = CreateExecution(adapter);
            Equal(false, Invoke(execution, "loadNotes").Succeeded);
        }

        private static DdfEditorExecutionService CreateExecution(
            DdfGovernedFileSystemAdapter adapter)
        {
            var catalog = new DdfEditorHostCatalog();
            foreach (DdfHostPermissionDefinition permission in Permissions())
            {
                catalog.RegisterPermission(permission);
                catalog.SetPermission(permission.Id, true);
            }
            catalog.RegisterFunctionLibrary(adapter.CreateHostLibrary(
                "demo.storage", "Storage demo", requiredPermissions:
                    new[] { "storage.access" }));
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("storage.ddf",
                "main() out int { ret 0; } " +
                "loadNotes() out string { ret readNotes(); } " +
                "storeNotes(string content) out bool { ret writeNotes(content); } " +
                "loadJson() out string { ret readSettings(); } " +
                "storeJson(string content) out bool { ret writeSettings(content); } " +
                "loadCsv() out string { ret readTable(); } " +
                "storeCsv(string content) out bool { ret writeTable(content); } " +
                "storeThenFail(string content) out int { " +
                "writeNotes(content); int zero << 0; ret 1 / zero; }");
            var language = new DdfLanguagePackageSession();
            return new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);
        }

        private static IReadOnlyList<DdfHostPermissionDefinition> Permissions()
            => new[]
            {
                new DdfHostPermissionDefinition("storage.access", "Storage"),
                new DdfHostPermissionDefinition("storage.read", "Lettura file"),
                new DdfHostPermissionDefinition("storage.write", "Scrittura file")
            };

        private static DdfEditorExecutionCompletion Invoke(
            DdfEditorExecutionService execution, string procedure,
            IReadOnlyDictionary<string, object> arguments = null,
            DdfEditorExecutionRequest request = null) => execution.InvokeAsync(
                new DdfEditorProcedureRequest(procedure, arguments,
                    execution: request)).GetAwaiter().GetResult();

        private static string ReturnString(
            DdfEditorExecutionCompletion completion)
        {
            Equal(true, completion.Succeeded);
            return Convert.ToString(completion.ExecutionResult.ReturnValue);
        }

        private static bool ReturnBool(DdfEditorExecutionCompletion completion)
        {
            if (!completion.Succeeded)
                throw new InvalidOperationException(string.Join(" | ",
                    completion.ExecutionResult.Diagnostics.Select(item =>
                        item.Code + ": " + item.Message)));
            return Convert.ToBoolean(completion.ExecutionResult.ReturnValue);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": " + exception);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected '" + expected +
                    "', got '" + actual + "'.");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name +
                ".");
        }

        private sealed class TestEnvironment : IDisposable
        {
            internal TestEnvironment()
            {
                Root = System.IO.Path.Combine(AppContext.BaseDirectory,
                    "ddf-storage-tests-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            internal string Root { get; }
            internal string Path(string relative) => System.IO.Path.Combine(
                Root, relative);

            internal DdfGovernedFileSystemAdapter CreateAdapter() => new(
                "storage.demo", Root, new[]
                {
                    new DdfLogicalFileDefinition("notes", "notes.txt",
                        DdfLogicalFileFormat.Utf8Text,
                        DdfLogicalFileAccess.ReadWrite, 64),
                    new DdfLogicalFileDefinition("settings", "settings.json",
                        DdfLogicalFileFormat.Json,
                        DdfLogicalFileAccess.ReadWrite, 1024),
                    new DdfLogicalFileDefinition("table", "table.csv",
                        DdfLogicalFileFormat.Csv,
                        DdfLogicalFileAccess.ReadWrite, 1024)
                }, new[]
                {
                    Operation("readNotes", "notes.read", "notes",
                        DdfLogicalFileOperationKind.Read, "storage.read"),
                    Operation("writeNotes", "notes.write", "notes",
                        DdfLogicalFileOperationKind.Write, "storage.write"),
                    Operation("readSettings", "settings.read", "settings",
                        DdfLogicalFileOperationKind.Read, "storage.read"),
                    Operation("writeSettings", "settings.write", "settings",
                        DdfLogicalFileOperationKind.Write, "storage.write"),
                    Operation("readTable", "table.read", "table",
                        DdfLogicalFileOperationKind.Read, "storage.read"),
                    Operation("writeTable", "table.write", "table",
                        DdfLogicalFileOperationKind.Write, "storage.write")
                }, maxTransactionBytes: 2048);

            private static DdfLogicalFileOperationDefinition Operation(
                string function, string binding, string resource,
                DdfLogicalFileOperationKind kind, string permission) => new(
                    function, binding, resource, kind,
                    requiredPermissions: new[] { permission });

            public void Dispose()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
