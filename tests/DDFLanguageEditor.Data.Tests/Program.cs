using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Data;
using Microsoft.Data.Sqlite;

namespace DDFLanguageEditor.Data.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("SQLite executes only declared parameterized operations",
                ExecutesDeclaredOperations);
            Run("SQLite commits and rolls back named transactions",
                GovernsTransactions);
            Run("SQLite contract feeds analysis and rejects undeclared SQL",
                ProjectsTheExternalContract);
            Run("SQLite observes host timeout and disposes active transactions",
                ObservesTimeoutAndCleanup);
            Console.WriteLine(failures == 0
                ? "All 4 governed data adapter tests passed."
                : failures + " governed data adapter test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void ExecutesDeclaredOperations()
        {
            using var workspace = new SqliteWorkspace();
            using DdfEditorExecutionService execution = workspace.Execution;
            string hostile = "Ada'); DROP TABLE customers;--";
            DdfEditorExecutionCompletion inserted = Invoke(execution,
                "addCustomer", new Dictionary<string, object>
                {
                    ["name"] = hostile,
                    ["enabled"] = true
                });
            Equal(true, inserted.Succeeded);
            Equal(1, inserted.ExecutionResult.ReturnValue);
            Equal(1, ReturnInt(Invoke(execution, "countCustomers")));
            Equal(hostile, ReturnString(Invoke(execution, "getFirstCustomer")));
        }

        private static void GovernsTransactions()
        {
            using var workspace = new SqliteWorkspace();
            using DdfEditorExecutionService execution = workspace.Execution;
            workspace.Adapter.BeginTransactionAsync("tx.rollback")
                .AsTask().GetAwaiter().GetResult();
            var transaction = new Dictionary<string, string>
            {
                [workspace.Adapter.DatabaseId] = "tx.rollback"
            };
            Equal(1, ReturnInt(Invoke(execution, "addCustomer",
                new Dictionary<string, object>
                {
                    ["name"] = "Temporary",
                    ["enabled"] = false
                }, transaction)));
            Equal(1, ReturnInt(Invoke(execution, "countCustomers", null,
                transaction)));
            workspace.Adapter.RollbackTransactionAsync("tx.rollback")
                .AsTask().GetAwaiter().GetResult();
            Equal(0, ReturnInt(Invoke(execution, "countCustomers")));

            workspace.Adapter.BeginTransactionAsync("tx.commit")
                .AsTask().GetAwaiter().GetResult();
            transaction[workspace.Adapter.DatabaseId] = "tx.commit";
            Equal(1, ReturnInt(Invoke(execution, "addCustomer",
                new Dictionary<string, object>
                {
                    ["name"] = "Persistent",
                    ["enabled"] = true
                }, transaction)));
            workspace.Adapter.CommitTransactionAsync("tx.commit")
                .AsTask().GetAwaiter().GetResult();
            Equal(1, ReturnInt(Invoke(execution, "countCustomers")));
        }

        private static void ProjectsTheExternalContract()
        {
            using var workspace = new SqliteWorkspace();
            DdfExternalApiDefinition api = workspace.Adapter
                .CreateExternalApiDefinition("ddf.dotnet.sqlite", "1.0.0",
                    workspace.Permissions, new[] { "db.access" });
            Equal(3, api.Functions.Count);
            Equal(64, api.Fingerprint.Length);
            DdfHostCatalogSnapshot analysis =
                DdfExternalApiCatalogProjector.CreateAnalysisCatalog(api,
                    workspace.Permissions.Select(item => item.Id));
            Equal(true, analysis.Functions.Any(item =>
                item.Name == "insertCustomer" && item.ReturnType == "int" &&
                item.RequiredPermissions.Contains("db.access")));
            DdfHostCatalogSnapshot runtime =
                DdfExternalApiCatalogProjector.CreateRuntimeCatalog(api,
                    workspace.Adapter.AdapterId, workspace.Adapter,
                    workspace.Permissions.Select(item => item.Id));
            Equal(3, runtime.Functions.Count);
            Throws<InvalidOperationException>(() => workspace.Adapter.InvokeAsync(
                null, "raw.sql", Array.Empty<object>()).AsTask().GetAwaiter()
                .GetResult());
            Throws<ArgumentException>(() => new DdfAdoNetOperationDefinition(
                "bad", "bad.sql", DdfHostDatabaseOperationKind.Query,
                "SELECT @undeclared", "int"));
        }

        private static void ObservesTimeoutAndCleanup()
        {
            using var workspace = new SqliteWorkspace(includeSlowQuery: true);
            using DdfEditorExecutionService execution = workspace.Execution;
            DdfEditorExecutionCompletion timeout = Invoke(execution,
                "runSlowCount",
                executionRequest: new DdfEditorExecutionRequest(
                    hostCallTimeout: TimeSpan.FromMilliseconds(5)));
            if (!timeout.ExecutionResult.Diagnostics.Any(item =>
                    item.Code == "DDF414"))
                throw new InvalidOperationException(
                    "Timeout non osservato; diagnostiche: " + string.Join(", ",
                        timeout.ExecutionResult.Diagnostics.Select(item =>
                            item.Code + " " + item.Message)));

            workspace.Adapter.BeginTransactionAsync("tx.dispose")
                .AsTask().GetAwaiter().GetResult();
            workspace.Adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            using var connection = new SqliteConnection(workspace.ConnectionString);
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM customers";
            Equal(0L, (long)command.ExecuteScalar());
        }

        private static DdfEditorExecutionCompletion Invoke(
            DdfEditorExecutionService execution, string procedure,
            IReadOnlyDictionary<string, object> arguments = null,
            IReadOnlyDictionary<string, string> transactions = null,
            DdfEditorExecutionRequest executionRequest = null)
        {
            executionRequest ??= new DdfEditorExecutionRequest(
                databaseTransactions: transactions);
            return execution.InvokeAsync(new DdfEditorProcedureRequest(procedure,
                arguments, execution: executionRequest)).GetAwaiter().GetResult();
        }

        private static int ReturnInt(DdfEditorExecutionCompletion completion)
        {
            Equal(true, completion.Succeeded);
            return Convert.ToInt32(completion.ExecutionResult.ReturnValue);
        }

        private static string ReturnString(
            DdfEditorExecutionCompletion completion)
        {
            Equal(true, completion.Succeeded);
            return Convert.ToString(completion.ExecutionResult.ReturnValue);
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

        private sealed class SqliteWorkspace : IDisposable
        {
            private readonly string directory;
            private readonly DdfLanguagePackageSession language = new();
            private readonly OpenDocumentCollection documents = new();

            internal SqliteWorkspace(bool includeSlowQuery = false)
            {
                directory = Path.Combine(Path.GetTempPath(), "ddf-sqlite-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string databasePath = Path.Combine(directory, "customers.db");
                ConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                }.ToString();
                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "CREATE TABLE customers (" +
                        "id INTEGER PRIMARY KEY, name TEXT NOT NULL, " +
                        "enabled INTEGER NOT NULL)";
                    command.ExecuteNonQuery();
                }
                Permissions = new[]
                {
                    new DdfHostPermissionDefinition("db.access",
                        "Accesso SQLite"),
                    new DdfHostPermissionDefinition("db.read",
                        "Lettura SQLite"),
                    new DdfHostPermissionDefinition("db.write",
                        "Scrittura SQLite")
                };
                var operations = new List<DdfAdoNetOperationDefinition>
                {
                    new("insertCustomer", "customer.insert",
                        DdfHostDatabaseOperationKind.Command,
                        "INSERT INTO customers(name, enabled) " +
                        "VALUES (@name, @enabled)", "int", new[]
                        {
                            new DdfHostParameterDefinition("name", "string"),
                            new DdfHostParameterDefinition("enabled", "bool")
                        }, DdfAdoNetResultKind.AffectedRows,
                        requiredPermissions: new[] { "db.write" }),
                    new("customerCount", "customer.count",
                        DdfHostDatabaseOperationKind.Query,
                        "SELECT COUNT(*) FROM customers", "int",
                        requiredPermissions: new[] { "db.read" }),
                    new("firstCustomer", "customer.first",
                        DdfHostDatabaseOperationKind.Query,
                        "SELECT name FROM customers ORDER BY id LIMIT 1",
                        "string", requiredPermissions: new[] { "db.read" })
                };
                if (includeSlowQuery)
                    operations.Add(new DdfAdoNetOperationDefinition("slowCount",
                        "customer.slow", DdfHostDatabaseOperationKind.Query,
                        "WITH RECURSIVE counter(value) AS (" +
                        "SELECT 1 UNION ALL SELECT value + 1 FROM counter " +
                        "WHERE value < 10000000) SELECT sum(value) FROM counter",
                        "int", timeout: TimeSpan.FromSeconds(30),
                        requiredPermissions: new[] { "db.read" }));
                Adapter = new DdfAdoNetDatabaseAdapter("customers",
                    "sqlite.customers", token =>
                        ValueTask.FromResult<DbConnection>(
                            new SqliteConnection(ConnectionString)), operations);
                var catalog = new DdfEditorHostCatalog();
                foreach (DdfHostPermissionDefinition permission in Permissions)
                {
                    catalog.RegisterPermission(permission);
                    catalog.SetPermission(permission.Id, true);
                }
                catalog.RegisterDatabase(Adapter.CreateHostDefinition(
                    "Archivio clienti SQLite governato.",
                    new[] { "db.access" }));
                string slow = includeSlowQuery
                    ? " runSlowCount() out int { ret slowCount(); }" :
                    string.Empty;
                documents.CreateVirtual("sqlite.ddf",
                    "main() out int { ret 0; } " +
                    "addCustomer(string name, bool enabled) out int { " +
                    "ret insertCustomer(name, enabled); } " +
                    "countCustomers() out int { ret customerCount(); } " +
                    "getFirstCustomer() out string { ret firstCustomer(); }" +
                    slow);
                Execution = new DdfEditorExecutionService(() => documents,
                    () => language, () => catalog.Snapshot);
            }

            internal string ConnectionString { get; }
            internal DdfAdoNetDatabaseAdapter Adapter { get; }
            internal DdfEditorExecutionService Execution { get; }
            internal IReadOnlyList<DdfHostPermissionDefinition> Permissions {
                get;
            }

            public void Dispose()
            {
                Execution.Dispose();
                Adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
                SqliteConnection.ClearAllPools();
                string full = Path.GetFullPath(directory);
                string root = Path.GetFullPath(Path.GetTempPath());
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(full).StartsWith("ddf-sqlite-",
                        StringComparison.Ordinal))
                {
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        try
                        {
                            Directory.Delete(full, recursive: true);
                            break;
                        }
                        catch (IOException) when (attempt < 9)
                        {
                            Thread.Sleep(25);
                        }
                    }
                }
            }
        }
    }
}
