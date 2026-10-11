using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Data
{
    public enum DdfAdoNetResultKind
    {
        Scalar,
        AffectedRows
    }

    public sealed class DdfAdoNetOperationDefinition
    {
        private static readonly Regex ParameterPattern = new(
            "(?<!@)@([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant);

        public DdfAdoNetOperationDefinition(string functionName,
            string statementId, DdfHostDatabaseOperationKind kind, string sql,
            string returnType,
            IEnumerable<DdfHostParameterDefinition> parameters = null,
            DdfAdoNetResultKind resultKind = DdfAdoNetResultKind.Scalar,
            TimeSpan? timeout = null, string documentation = null,
            IEnumerable<string> requiredPermissions = null)
        {
            FunctionName = Identifier(functionName, nameof(functionName));
            StatementId = Identifier(statementId, nameof(statementId));
            if (!Enum.IsDefined(typeof(DdfHostDatabaseOperationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(DdfAdoNetResultKind), resultKind))
                throw new ArgumentOutOfRangeException(nameof(resultKind));
            Kind = kind;
            Sql = RequiredText(sql, nameof(sql));
            ReturnType = TypeName(returnType, nameof(returnType));
            Parameters = UniqueParameters(parameters);
            ResultKind = resultKind;
            Timeout = timeout ?? TimeSpan.FromSeconds(15);
            if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5))
                throw new ArgumentOutOfRangeException(nameof(timeout),
                    "Il timeout ADO.NET deve essere compreso tra zero e cinque minuti.");
            if (Kind == DdfHostDatabaseOperationKind.Query &&
                ResultKind != DdfAdoNetResultKind.Scalar)
                throw new ArgumentException(
                    "Una query ADO.NET governata deve restituire uno scalare.",
                    nameof(resultKind));
            if (Kind == DdfHostDatabaseOperationKind.Command &&
                ResultKind != DdfAdoNetResultKind.AffectedRows)
                throw new ArgumentException(
                    "Un comando ADO.NET governato deve restituire le righe modificate.",
                    nameof(resultKind));
            if (ResultKind == DdfAdoNetResultKind.AffectedRows &&
                ReturnType != "int")
                throw new ArgumentException(
                    "Le righe modificate richiedono un valore di ritorno int.",
                    nameof(returnType));
            ValidateSqlParameters();
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = (requiredPermissions ?? Array.Empty<string>())
                .Select(item => Identifier(item, nameof(requiredPermissions)))
                .Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public string FunctionName { get; }
        public string StatementId { get; }
        public DdfHostDatabaseOperationKind Kind { get; }
        public string Sql { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfHostParameterDefinition> Parameters { get; }
        public DdfAdoNetResultKind ResultKind { get; }
        public TimeSpan Timeout { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }

        internal DdfHostDatabaseOperationDefinition ToHostDefinition() => new(
            FunctionName, StatementId, Kind, ReturnType, Parameters,
            Documentation, RequiredPermissions);

        internal DdfExternalApiFunctionDefinition ToExternalDefinition(
            IEnumerable<string> inheritedPermissions) => new(
            FunctionName, StatementId, ReturnType, Parameters.Select(item =>
                new DdfExternalApiParameterDefinition(item.Name, item.TypeName,
                    item.Documentation)), Documentation,
            (inheritedPermissions ?? Array.Empty<string>())
                .Concat(RequiredPermissions).Distinct(StringComparer.Ordinal),
            Kind == DdfHostDatabaseOperationKind.Command
                ? DdfHostFunctionEffect.HostMutation
                : DdfHostFunctionEffect.Pure);

        private void ValidateSqlParameters()
        {
            string[] sqlNames = ParameterPattern.Matches(Sql)
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray();
            string[] declared = Parameters.Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!sqlNames.SequenceEqual(declared, StringComparer.Ordinal))
                throw new ArgumentException(
                    "I parametri SQL devono coincidere esattamente con quelli dichiarati.",
                    nameof(Sql));
        }

        private static IReadOnlyList<DdfHostParameterDefinition>
            UniqueParameters(IEnumerable<DdfHostParameterDefinition> values)
        {
            var result = new List<DdfHostParameterDefinition>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DdfHostParameterDefinition value in values ??
                     Array.Empty<DdfHostParameterDefinition>())
            {
                if (value == null || !names.Add(value.Name))
                    throw new ArgumentException(
                        "I parametri ADO.NET non possono essere nulli o duplicati.",
                        nameof(values));
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        private static string TypeName(string value, string parameterName)
        {
            string result = RequiredText(value, parameterName);
            string current = result;
            while (current.EndsWith("[]", StringComparison.Ordinal))
                current = current.Substring(0, current.Length - 2);
            Identifier(current, parameterName);
            return result;
        }

        private static string Identifier(string value, string parameterName)
        {
            string result = RequiredText(value, parameterName);
            if (!(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character => !(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '.')))
                throw new ArgumentException(
                    "Identificatore ADO.NET non valido: " + result + ".",
                    parameterName);
            return result;
        }

        private static string RequiredText(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0)
                throw new ArgumentException("Il valore è obbligatorio.",
                    parameterName);
            return result;
        }
    }

    /// <summary>
    /// Executes only host-authored, named and parameterized ADO.NET statements.
    /// Connection strings and credentials remain inside the supplied factory.
    /// </summary>
    public sealed class DdfAdoNetDatabaseAdapter : IDdfHostDatabaseService,
        IDdfExternalApiRuntimeAdapter, IAsyncDisposable
    {
        private readonly Func<CancellationToken, ValueTask<DbConnection>>
            connectionFactory;
        private readonly IReadOnlyDictionary<string, DdfAdoNetOperationDefinition>
            operations;
        private readonly ConcurrentDictionary<string, TransactionSession>
            transactions = new(StringComparer.Ordinal);
        private readonly object lifecycleSync = new();
        private TaskCompletionSource<bool> idleSource = CompletedIdleSource();
        private int activeOperations;
        private int disposed;

        public DdfAdoNetDatabaseAdapter(string databaseId, string adapterId,
            Func<CancellationToken, ValueTask<DbConnection>> connectionFactory,
            IEnumerable<DdfAdoNetOperationDefinition> operations)
        {
            DatabaseId = RequiredIdentifier(databaseId, nameof(databaseId));
            AdapterId = RequiredIdentifier(adapterId, nameof(adapterId));
            this.connectionFactory = connectionFactory ?? throw new
                ArgumentNullException(nameof(connectionFactory));
            DdfAdoNetOperationDefinition[] source = (operations ??
                Array.Empty<DdfAdoNetOperationDefinition>()).ToArray();
            if (source.Length == 0 || source.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesta almeno un'operazione ADO.NET valida.",
                    nameof(operations));
            string duplicate = source.GroupBy(item => item.StatementId,
                    StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate != null || source.Select(item => item.FunctionName)
                    .Distinct(StringComparer.Ordinal).Count() != source.Length)
                throw new ArgumentException(
                    "Statement ID e nomi funzione ADO.NET devono essere univoci.",
                    nameof(operations));
            this.operations = new ReadOnlyDictionary<string,
                DdfAdoNetOperationDefinition>(source.ToDictionary(item =>
                    item.StatementId, StringComparer.Ordinal));
        }

        public string DatabaseId { get; }
        public string AdapterId { get; }
        public IReadOnlyCollection<DdfAdoNetOperationDefinition> Operations =>
            operations.Values.ToList().AsReadOnly();

        public DdfHostDatabaseDefinition CreateHostDefinition(
            string documentation = null,
            IEnumerable<string> requiredPermissions = null) => new(DatabaseId,
                this, operations.Values.Select(item => item.ToHostDefinition()),
                documentation, requiredPermissions);

        public DdfExternalApiDefinition CreateExternalApiDefinition(
            string referenceId, string referenceVersion,
            IEnumerable<DdfHostPermissionDefinition> permissions = null,
            IEnumerable<string> requiredPermissions = null) => new(referenceId,
                referenceVersion, permissions: permissions,
                functions: operations.Values.Select(item =>
                    item.ToExternalDefinition(requiredPermissions)));

        public async ValueTask BeginTransactionAsync(string transactionId,
            IsolationLevel isolationLevel = IsolationLevel.Serializable,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            string id = RequiredIdentifier(transactionId,
                nameof(transactionId));
            DbConnection connection = await connectionFactory(cancellationToken);
            if (connection == null) throw new InvalidOperationException(
                "La factory ADO.NET ha restituito una connessione nulla.");
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);
                DbTransaction transaction = await connection
                    .BeginTransactionAsync(isolationLevel, cancellationToken);
                var session = new TransactionSession(connection, transaction);
                if (!transactions.TryAdd(id, session))
                {
                    await session.RollbackAndDisposeAsync(cancellationToken);
                    throw new InvalidOperationException(
                        "Transazione ADO.NET già attiva: " + id + ".");
                }
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public ValueTask CommitTransactionAsync(string transactionId,
            CancellationToken cancellationToken = default) => EndTransactionAsync(
                transactionId, true, cancellationToken);

        public ValueTask RollbackTransactionAsync(string transactionId,
            CancellationToken cancellationToken = default) => EndTransactionAsync(
                transactionId, false, cancellationToken);

        public ValueTask<object> ExecuteAsync(DdfHostDatabaseRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.Equals(request.DatabaseId, DatabaseId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "La richiesta appartiene a un database diverso.");
            return ExecuteOperationAsync(request.StatementId, request.Parameters,
                request.Invocation, request.TransactionId);
        }

        object IDdfExternalApiRuntimeAdapter.ReadObject(string bindingId) =>
            throw new NotSupportedException(
                "L'adapter ADO.NET non espone oggetti globali.");

        void IDdfExternalApiRuntimeAdapter.WriteObject(string bindingId,
            object value) => throw new NotSupportedException(
                "L'adapter ADO.NET non espone oggetti globali.");

        public ValueTask<object> InvokeAsync(DdfHostInvocationContext context,
            string bindingId, IReadOnlyList<object> arguments)
        {
            if (!operations.TryGetValue(bindingId ?? string.Empty,
                    out DdfAdoNetOperationDefinition operation))
                throw new InvalidOperationException(
                    "Statement ADO.NET non autorizzato: " + bindingId + ".");
            if (context == null) throw new ArgumentNullException(nameof(context));
            if ((arguments?.Count ?? 0) != operation.Parameters.Count)
                throw new ArgumentException(
                    "Numero di argomenti ADO.NET non valido.", nameof(arguments));
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < operation.Parameters.Count; index++)
                values.Add(operation.Parameters[index].Name, arguments[index]);
            context.TryGetDatabaseTransaction(DatabaseId,
                out string transactionId);
            return ExecuteOperationAsync(bindingId,
                new ReadOnlyDictionary<string, object>(values), context,
                transactionId);
        }

        private async ValueTask<object> ExecuteOperationAsync(string statementId,
            IReadOnlyDictionary<string, object> parameters,
            DdfHostInvocationContext context, string transactionId)
        {
            EnterOperation();
            try
            {
                if (!operations.TryGetValue(statementId ?? string.Empty,
                        out DdfAdoNetOperationDefinition operation))
                    throw new InvalidOperationException(
                        "Statement ADO.NET non autorizzato: " + statementId + ".");
                ValidateParameterValues(operation, parameters);
                using CancellationTokenSource timeout = CreateTimeout(context,
                    operation.Timeout);
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    if (!transactions.TryGetValue(transactionId,
                            out TransactionSession session))
                        throw new InvalidOperationException(
                            "Transazione ADO.NET non attiva: " + transactionId + ".");
                    await session.Gate.WaitAsync(timeout.Token);
                    try
                    {
                        return await ExecuteCommandIsolatedAsync(
                            session.Connection, session.Transaction, operation,
                            parameters, timeout.Token);
                    }
                    finally { session.Gate.Release(); }
                }
                DbConnection connection = await connectionFactory(timeout.Token);
                if (connection == null) throw new InvalidOperationException(
                    "La factory ADO.NET ha restituito una connessione nulla.");
                await using (connection)
                {
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync(timeout.Token);
                    return await ExecuteCommandIsolatedAsync(connection, null,
                        operation, parameters, timeout.Token);
                }
            }
            finally { ExitOperation(); }
        }

        private static async ValueTask<object> ExecuteCommandIsolatedAsync(
            DbConnection connection, DbTransaction transaction,
            DdfAdoNetOperationDefinition operation,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            Task<object> execution = Task.Run(async () =>
                await ExecuteCommandAsync(connection, transaction, operation,
                    parameters, cancellationToken), CancellationToken.None);
            try
            {
                return await execution.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                try { await execution; }
                catch (OperationCanceledException) { }
                catch (DbException) { }
                throw;
            }
        }

        private static async ValueTask<object> ExecuteCommandAsync(
            DbConnection connection, DbTransaction transaction,
            DdfAdoNetOperationDefinition operation,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = operation.Sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = Math.Max(1, checked((int)Math.Ceiling(
                operation.Timeout.TotalSeconds)));
            command.Transaction = transaction;
            using CancellationTokenRegistration cancellation =
                cancellationToken.Register(command.Cancel);
            foreach (DdfHostParameterDefinition definition in
                     operation.Parameters)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@" + definition.Name;
                parameter.Value = ToDatabaseValue(parameters[definition.Name]);
                command.Parameters.Add(parameter);
            }
            if (operation.ResultKind == DdfAdoNetResultKind.AffectedRows)
                return await command.ExecuteNonQueryAsync(cancellationToken);
            object value = await command.ExecuteScalarAsync(cancellationToken);
            return FromDatabaseValue(value, operation.ReturnType);
        }

        private static object ToDatabaseValue(object value) => value ??
            DBNull.Value;

        private static object FromDatabaseValue(object value, string typeName)
        {
            if (value == null || value == DBNull.Value)
                throw new InvalidOperationException(
                    "La query ADO.NET non ha restituito un valore DDF.");
            try
            {
                return typeName switch
                {
                    "bool" => Convert.ToBoolean(value,
                        CultureInfo.InvariantCulture),
                    "int" => Convert.ToInt32(value,
                        CultureInfo.InvariantCulture),
                    "float" => Convert.ToDouble(value,
                        CultureInfo.InvariantCulture),
                    "string" => Convert.ToString(value,
                        CultureInfo.InvariantCulture),
                    _ => throw new NotSupportedException(
                        "Tipo scalare ADO.NET non supportato: " + typeName + ".")
                };
            }
            catch (Exception exception) when (exception is FormatException ||
                                               exception is InvalidCastException ||
                                               exception is OverflowException)
            {
                throw new InvalidOperationException(
                    "Il risultato ADO.NET non è convertibile nel tipo DDF " +
                    typeName + ".", exception);
            }
        }

        private static void ValidateParameterValues(
            DdfAdoNetOperationDefinition operation,
            IReadOnlyDictionary<string, object> values)
        {
            if (values == null || values.Count != operation.Parameters.Count ||
                operation.Parameters.Any(item => !values.ContainsKey(item.Name)))
                throw new ArgumentException(
                    "I valori ADO.NET non coincidono con i parametri dichiarati.",
                    nameof(values));
        }

        private static CancellationTokenSource CreateTimeout(
            DdfHostInvocationContext context, TimeSpan operationTimeout)
        {
            var result = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken);
            TimeSpan remaining = operationTimeout;
            if (context.Deadline.HasValue)
            {
                TimeSpan deadline = context.Deadline.Value - DateTimeOffset.UtcNow;
                if (deadline <= TimeSpan.Zero)
                {
                    result.Cancel();
                    return result;
                }
                if (deadline < remaining) remaining = deadline;
            }
            result.CancelAfter(remaining);
            return result;
        }

        private async ValueTask EndTransactionAsync(string transactionId,
            bool commit, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            string id = RequiredIdentifier(transactionId,
                nameof(transactionId));
            if (!transactions.TryRemove(id, out TransactionSession session))
                throw new InvalidOperationException(
                    "Transazione ADO.NET non attiva: " + id + ".");
            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (commit)
                    await session.Transaction.CommitAsync(cancellationToken);
                else
                    await session.Transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                session.Gate.Release();
                await session.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Task idle;
            lock (lifecycleSync) idle = idleSource.Task;
            await idle;
            TransactionSession[] active = transactions.Values.ToArray();
            transactions.Clear();
            foreach (TransactionSession session in active)
                await session.RollbackAndDisposeAsync(CancellationToken.None);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(DdfAdoNetDatabaseAdapter));
        }

        private void EnterOperation()
        {
            lock (lifecycleSync)
            {
                ThrowIfDisposed();
                if (activeOperations++ == 0)
                    idleSource = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private void ExitOperation()
        {
            TaskCompletionSource<bool> completed = null;
            lock (lifecycleSync)
            {
                if (--activeOperations == 0) completed = idleSource;
            }
            completed?.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CompletedIdleSource()
        {
            var result = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            result.SetResult(true);
            return result;
        }

        private static string RequiredIdentifier(string value,
            string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0 ||
                !(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character => !(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '.')))
                throw new ArgumentException(
                    "Identificatore ADO.NET non valido: " + result + ".",
                    parameterName);
            return result;
        }

        private sealed class TransactionSession : IAsyncDisposable
        {
            internal TransactionSession(DbConnection connection,
                DbTransaction transaction)
            {
                Connection = connection;
                Transaction = transaction;
            }

            internal DbConnection Connection { get; }
            internal DbTransaction Transaction { get; }
            internal SemaphoreSlim Gate { get; } = new(1, 1);

            internal async ValueTask RollbackAndDisposeAsync(
                CancellationToken cancellationToken)
            {
                await Gate.WaitAsync(cancellationToken);
                try
                {
                    try
                    {
                        await Transaction.RollbackAsync(cancellationToken);
                    }
                    catch (InvalidOperationException) { }
                }
                finally
                {
                    Gate.Release();
                    await DisposeAsync();
                }
            }

            public async ValueTask DisposeAsync()
            {
                await Transaction.DisposeAsync();
                await Connection.DisposeAsync();
                Gate.Dispose();
            }
        }
    }
}
